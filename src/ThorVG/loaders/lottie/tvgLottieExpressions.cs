// Ported from ThorVG/src/loaders/lottie/tvgLottieExpressions.h and tvgLottieExpressions.cpp
// Uses the approved Jint 4.17.0 (BSD-2-Clause) interpreter in place of JerryScript.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Jint;
using Jint.Native;
using Jint.Runtime.Interop;

namespace ThorVG
{
    /// <summary>
    /// Expression evaluation engine. Mirrors C++ LottieExpressions.
    /// Uses Jint JavaScript interpreter to evaluate Lottie expressions.
    /// </summary>
    public class LottieExpressions
    {
        // Reserved expression specifier names (matching C++ static const strings)
        private const string EXP_NAME = "name";
        private const string EXP_CONTENT = "content";
        private const string EXP_WIDTH = "width";
        private const string EXP_HEIGHT = "height";
        private const string EXP_CYCLE = "cycle";
        private const string EXP_PINGPONG = "pingpong";
        private const string EXP_OFFSET = "offset";
        private const string EXP_CONTINUE = "continue";
        private const string EXP_TIME = "time";
        private const string EXP_VALUE = "value";
        private const string EXP_INDEX = "index";
        private const string EXP_EFFECT = "effect";
        private const string EXP_SIZE = "size";
        private const string EXP_POSITION = "position";

        private const int LOOP_OUT_OFFSET = 4;

        private static LottieExpressions? _instance;
        private static readonly object InstanceLock = new();
        private static uint _references;

        // Upstream expressions and JerryScript Math.random share the platform C
        // runtime's generator. Keep that dependency (and its platform sequence)
        // rather than substituting System.Random's different algorithm.
        [DllImport("libc", EntryPoint = "rand", CallingConvention = CallingConvention.Cdecl)]
        private static extern int UnixRand();
        [DllImport("libc", EntryPoint = "srand", CallingConvention = CallingConvention.Cdecl)]
        private static extern void UnixSeed(uint seed);
        [DllImport("libSystem.B.dylib", EntryPoint = "rand", CallingConvention = CallingConvention.Cdecl)]
        private static extern int AppleRand();
        [DllImport("libSystem.B.dylib", EntryPoint = "srand", CallingConvention = CallingConvention.Cdecl)]
        private static extern void AppleSeed(uint seed);
        [DllImport("ucrtbase.dll", EntryPoint = "rand", CallingConvention = CallingConvention.Cdecl)]
        private static extern int WindowsRand();
        [DllImport("ucrtbase.dll", EntryPoint = "srand", CallingConvention = CallingConvention.Cdecl)]
        private static extern void WindowsSeed(uint seed);

        /// <summary>Seed the shared C runtime generator used by expressions.</summary>
        public static void SeedRandom(uint seed)
        {
            if (OperatingSystem.IsWindows()) WindowsSeed(seed);
            else if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS()) AppleSeed(seed);
            else UnixSeed(seed);
        }

        internal static int NextRandom() => OperatingSystem.IsWindows() ? WindowsRand() :
            OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() ? AppleRand() : UnixRand();

        // JerryScript native pointers are hidden metadata, not JS properties.
        // Weak keys also allow self-referential JS values without rooting them.
        private static readonly ConditionalWeakTable<Jint.Native.Object.ObjectInstance, LottiePropertyRef> NativeProperties = new();

        private sealed class Context
        {
            public Engine? Engine;
            public float Time;
            public bool TimePending;
            public LottieExpression? Expression;
            public float Frame;
            public JsValue? Comp;
            public Jint.Native.JsObject? ThisComp;
            public Jint.Native.JsObject? ThisLayer;
            public Jint.Native.JsObject? ThisProperty;
        }
        // Native owns one persistent interpreter context per worker thread.
        private readonly ThreadLocal<Context> _contexts = new(() => new Context(), true);

        private LottieExpressions()
        {
        }

        /// <summary>
        /// Create a native-style external function with variadic arguments,
        /// construction and lazy prototype metadata.
        /// </summary>
        private static JsValue MakeVariadicFunc(Engine engine, string name, Func<JsValue[], JsValue> fn)
        {
            return new LottieExpressionFunction(engine, name, (thisObj, args) => fn(args));
        }

        /// <summary>
        /// Creates a callable object (mirrors C++ _layerChild).
        /// In C++, the layer object IS a function that can be called with an index
        /// or ADBE name. This creates the same external function pattern.
        /// </summary>
        private JsValue MakeLayerChildObj(Engine engine, float frameNo, LottieObject obj, LottieExpression exp)
        {
            // C++ stores LottieObject* in a union with Array<LottieObject*>*.
            // When called by name, it uses data->obj. When called by index from
            // a children-array context, it uses data->data. We split these into
            // separate methods: this one handles the "object" case.
            var fn = new LottieExpressionFunction(engine, "_layerChild", (thisObj, args) =>
            {
                if (args.Length == 0) return JsValue.Undefined;
                var arg = args[0];

                // Name-based lookups on the object itself
                if (!arg.IsNumber())
                {
                    var name = arg.AsString();
                    if (name == "ADBE Root Vectors Group" || name == "ADBE Vectors Group")
                    {
                        if (obj is LottieGroup grp &&
                            (grp.type == LottieObject.ObjectType.Group || grp.type == LottieObject.ObjectType.Layer))
                        {
                            return MakeChildrenCallable(engine, frameNo, grp.children, exp);
                        }
                    }
                    else if (name == "ADBE Vector Shape")
                    {
                        if (obj is LottiePath path)
                        {
                            var pathObj = new Jint.Native.JsObject(engine);
                            BuildPathExpansionOnObj(engine, pathObj, frameNo, path.pathset);
                            return pathObj;
                        }
                    }
                }
                return JsValue.Undefined;
            });

            // Set layer properties on the function object
            if (obj is LottieLayer layer)
                SetLayerProps(engine, fn, frameNo, layer, layer.precomp, exp);

            return fn;
        }

        /// <summary>
        /// Creates a callable children-list wrapper (for ADBE groups).
        /// Called by 1-based index to access children, or by ADBE name.
        /// Mirrors C++ _layerChild when data->data is Array&lt;LottieObject*&gt;*.
        /// </summary>
        private JsValue MakeChildrenCallable(Engine engine, float frameNo, List<LottieObject> children, LottieExpression exp)
        {
            return new LottieExpressionFunction(engine, "_layerChild", (thisObj, args) =>
            {
                if (args.Length == 0) return JsValue.Undefined;
                var arg = args[0];

                // Index-based: 1-based, returns callable child object
                if (arg.IsNumber())
                {
                    var idx = (int)arg.AsNumber() - 1;
                    if (idx >= 0 && idx < children.Count)
                        return MakeLayerChildObj(engine, frameNo, children[idx], exp);
                    return JsValue.Undefined;
                }

                // Name-based: ADBE keywords
                var name = arg.AsString();
                if (name == "ADBE Root Vectors Group" || name == "ADBE Vectors Group")
                {
                    // Re-expose the same children list
                    return MakeChildrenCallable(engine, frameNo, children, exp);
                }
                else if (name == "ADBE Vector Shape")
                {
                    foreach (var child in children)
                    {
                        if (child is LottiePath path)
                        {
                            var pathObj = new Jint.Native.JsObject(engine);
                            BuildPathExpansionOnObj(engine, pathObj, frameNo, path.pathset);
                            return pathObj;
                        }
                    }
                }
                return JsValue.Undefined;
            });
        }

        // ================================================================
        //  Shared instance, reference count, and per-thread contexts
        // ================================================================

        public static LottieExpressions? Instance()
        {
            lock (InstanceLock)
            {
                _instance ??= new LottieExpressions();
                ++_references;
                return _instance;
            }
        }

        public static void Retrieve(LottieExpressions? instance)
        {
            if (instance == null) return;
            lock (InstanceLock)
            {
                if (!ReferenceEquals(_instance, instance) || _references == 0) return;
                if (--_references != 0) return;
                _instance = null;
                instance._contexts.Dispose();
            }
        }

        public void Update(float curTime)
        {
            var context = _contexts.Value!;
            context.Time = curTime;
            context.TimePending = context.Engine == null;
            if (context.Engine != null) context.Engine.SetValue(EXP_TIME, (double)curTime);
        }

        // ================================================================
        //  Result methods -- called from property Evaluate overloads
        // ================================================================

        public bool ResultFloat(LottieFloat prop, float frameNo, out float outVal, LottieExpression exp)
        {
            outVal = default;
            var bm_rt = Evaluate(frameNo, exp);
            if (bm_rt == null) return false;

            if (bm_rt is LottiePropertyRef pref && pref.Property is LottieFloat fProp)
            {
                outVal = fProp.Evaluate(frameNo);
            }
            else
            {
                outVal = ToFloat(bm_rt);
            }
            return true;
        }

        public bool ResultInteger(LottieInteger prop, float frameNo, out int outVal, LottieExpression exp)
        {
            outVal = default;
            var bm_rt = Evaluate(frameNo, exp);
            if (bm_rt == null) return false;

            if (bm_rt is LottiePropertyRef pref && pref.Property is LottieInteger iProp)
            {
                outVal = iProp.Evaluate(frameNo);
            }
            else
            {
                outVal = (int)ToFloat(bm_rt);
            }
            return true;
        }

        public bool ResultScalar(LottieScalar prop, float frameNo, out Point outVal, LottieExpression exp)
        {
            outVal = default;
            var bm_rt = Evaluate(frameNo, exp);
            if (bm_rt == null) return false;

            if (bm_rt is LottiePropertyRef pref && pref.Property is LottieScalar sProp)
            {
                outVal = sProp.Evaluate(frameNo);
            }
            else
            {
                outVal = ToPoint2d(bm_rt);
            }
            return true;
        }

        public bool ResultVector(LottieVector prop, float frameNo, out Point outVal, LottieExpression exp)
        {
            outVal = default;
            var bm_rt = Evaluate(frameNo, exp);
            if (bm_rt == null) return false;

            if (bm_rt is LottiePropertyRef pref && pref.Property is LottieVector vProp)
            {
                outVal = vProp.Evaluate(frameNo);
            }
            else
            {
                outVal = ToPoint2d(bm_rt);
            }
            return true;
        }

        public bool ResultColor(LottieColor prop, float frameNo, out RGB32 outVal, LottieExpression exp)
        {
            outVal = default;
            var bm_rt = Evaluate(frameNo, exp);
            if (bm_rt == null) return false;

            if (bm_rt is LottiePropertyRef pref && pref.Property is LottieColor cProp)
            {
                outVal = cProp.Evaluate(frameNo);
            }
            else
            {
                outVal = ToColor(bm_rt);
            }
            return true;
        }

        public bool ResultOpacity(LottieOpacity prop, float frameNo, out byte outVal, LottieExpression exp)
        {
            outVal = default;
            var bm_rt = Evaluate(frameNo, exp);
            if (bm_rt == null) return false;

            if (bm_rt is LottiePropertyRef pref && pref.Property is LottieOpacity oProp)
            {
                outVal = oProp.Evaluate(frameNo);
            }
            else
            {
                var scaled = ToFloat(bm_rt) * 2.55f;
                outVal = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ?
                    unchecked((byte)(uint)scaled) : unchecked((byte)(int)scaled);
            }
            return true;
        }

        public unsafe bool ResultPathSet(LottiePathSet prop, float frameNo, RenderPath @out, Matrix* transform, LottieModifier? modifier, LottieExpression exp)
        {
            var bm_rt = Evaluate(frameNo, exp);
            if (bm_rt == null) return false;

            if (bm_rt is LottiePropertyRef pref && pref.Property is LottiePathSet psProp)
            {
                psProp.Evaluate(frameNo, @out, transform, null, modifier);
            }
            return true;
        }

        public bool ResultColorStop(LottieColorStop prop, float frameNo, Fill fill, LottieExpression exp)
        {
            var bm_rt = Evaluate(frameNo, exp);
            if (bm_rt == null) return false;

            if (bm_rt is LottiePropertyRef pref && pref.Property is LottieColorStop csProp)
            {
                csProp.Evaluate(frameNo, fill);
            }
            return true;
        }

        /// <summary>
        /// Applies the pinned expression adapter's UTF16-unit byte capacity,
        /// copying complete UTF8 characters and retaining initialized old bytes.
        /// If the result would require reading an unwritten grown-allocation tail,
        /// returns false and leaves the document unchanged. This bounded contract
        /// does not promise that a Unicode return replaces the whole text.
        /// </summary>
        public bool ResultTextDoc(float frameNo, TextDocument doc, LottieExpression exp)
        {
            var bm_rt = Evaluate(frameNo, exp);
            if (bm_rt == null) return false;

            if (bm_rt is string text)
            {
                // The pinned native adapter sizes its UTF8 destination with
                // jerry_string_length (UTF16 units), then copies complete UTF8
                // characters into that capacity and terminates at that length.
                // Preserve its initialized old bytes when a character will not
                // fit. In particular, do not silently replace this with a full
                // Unicode assignment: that changes the rendered text.
                if (!AssignText(doc, text))
                {
                    TvgCommon.TVGERR("LOTTIE", "Expression text exceeds the initialized native UTF8 destination");
                    return false;
                }
            }
            return true;
        }

        private static bool AssignText(TextDocument doc, string text)
        {
            var capacity = text.Length;
            if (Encoding.UTF8.GetByteCount(text) == capacity)
            {
                var nul = text.IndexOf('\0');
                doc.text = nul < 0 ? text : text[..nul];
                return true;
            }
            var old = TvgUtf8.Encode(doc.text ?? "");
            var buffer = new byte[capacity + 1];
            old.AsSpan(0, Math.Min(old.Length, capacity)).CopyTo(buffer);
            var written = 0;
            foreach (var rune in text.EnumerateRunes())
            {
                if (rune.Utf8SequenceLength > capacity - written) break;
                written += rune.EncodeToUtf8(buffer.AsSpan(written));
            }
            // Native leaves unwritten bytes from a grown allocation undefined.
            // Reject that undefined read instead of manufacturing garbage text.
            var initialized = Math.Min(capacity, Math.Max(doc.text == null ? 0 : old.Length + 1, written));
            var end = buffer.AsSpan(0, initialized).IndexOf((byte)0);
            if (end < 0 && initialized < capacity) return false;
            if (end < 0) end = capacity;
            doc.text = TvgUtf8.Decode(buffer.AsSpan(0, end));
            return true;
        }

        // ================================================================
        //  Core evaluate -- sets up the JS engine and runs the expression
        // ================================================================

        private object? Evaluate(float frameNo, LottieExpression exp)
        {
            if (exp.disabled) return null;
            if (exp.code == null) return null;

            try
            {
                var context = _contexts.Value!;
                context.Expression = exp;
                context.Frame = frameNo;
                var engine = context.Engine;
                if (engine == null)
                {
                    engine = context.Engine = LottieExpressionRuntime.CreateEngine();
                    BuildCompFunction(engine);
                    context.ThisComp = new Jint.Native.JsObject(engine);
                    context.ThisLayer = new Jint.Native.JsObject(engine);
                    context.ThisProperty = new Jint.Native.JsObject(engine);
                    engine.SetValue("thisComp", context.ThisComp);
                    engine.SetValue("thisLayer", context.ThisLayer);
                    engine.SetValue("thisProperty", context.ThisProperty);
                    BuildMath(engine);
                    LottieExpressionNumbers.Install(engine);
                }
                if (context.TimePending)
                {
                    engine.SetValue(EXP_TIME, (double)context.Time);
                    context.TimePending = false;
                }

                // Refresh per-frame state (overwrites globals each call, like C++)
                BuildGlobal(engine, frameNo, exp);
                BuildComp(engine, exp.comp, frameNo, exp);
                BuildThisComp(engine, frameNo, exp);
                BuildProperty(engine, frameNo, exp, "");
                BuildThisLayer(engine, frameNo, exp);
                BuildThisProperty(engine, frameNo, exp);

                // Transform context
                if (exp.obj?.type == LottieObject.ObjectType.Transform)
                    BuildTransformGlobal(engine, frameNo, (LottieTransform)exp.obj);

                // Execute the expression code
                engine.Execute(LottieExpressionNumbers.Prepare(exp));

                // Get $bm_rt (the standard After Effects expression result variable)
                var bmRt = engine.GetValue("$bm_rt");
                if (bmRt == null || bmRt.IsUndefined())
                    return null;

                return UnwrapJsValue(bmRt);
            }
            catch (Exception)
            {
                TvgCommon.TVGERR("LOTTIE", "Failed to dispatch the expressions!");
                exp.disabled = true;
                return null;
            }
        }

        // ================================================================
        //  JS value wrapping/unwrapping
        // ================================================================

        /// <summary>
        /// Marker class for wrapping a LottieProperty reference passed through JS.
        /// </summary>
        private class LottiePropertyRef
        {
            public LottieProperty Property;
            public LottiePropertyRef(LottieProperty p) { Property = p; }
        }

        private static object? UnwrapJsValue(JsValue val)
        {
            if (val.IsUndefined()) return null;
            if (val.IsNull()) return val;
            if (val.IsString()) return val.AsString();
            if (val.IsNumber()) return val.AsNumber();
            if (val.IsBoolean()) return val.AsBoolean();

            if (val.IsObject())
            {
                var jsObj = val.AsObject();
                if (NativeProperties.TryGetValue(jsObj, out var native)) return native;
                return jsObj;
            }
            return null;
        }

        // ================================================================
        //  Conversion helpers (JS values to C# types)
        // ================================================================

        private static float ToFloat(object? val)
        {
            if (val == null) return 0;
            if (val is double d) return (float)d;
            if (val is float f) return f;
            if (val is int i) return i;
            if (val is Jint.Native.JsValue jv)
            {
                if (jv.IsNumber()) return (float)jv.AsNumber();
                if (jv.IsObject())
                {
                    var obj = jv.AsObject();
                    return Numeric(obj.Get("0"));
                }
            }
            if (val is Jint.Native.JsNumber jn) return (float)jn.AsNumber();
            return 0;
        }

        private static Point ToPoint2d(object? val)
        {
            if (val == null) return default;
            if (val is Jint.Native.JsValue jv && jv.IsObject())
            {
                var obj = jv.AsObject();
                var v0 = obj.Get("0");
                var v1 = obj.Get("1");
                return new Point(Numeric(v0), Numeric(v1));
            }
            return default;
        }

        private static RGB32 ToColor(object? val)
        {
            if (val == null) return default;
            if (val is Jint.Native.JsValue jv && jv.IsObject())
            {
                var obj = jv.AsObject();
                var r = obj.Get("0");
                var g = obj.Get("1");
                var b = obj.Get("2");
                if (r != null && !r.IsUndefined() && g != null && !g.IsUndefined() && b != null && !b.IsUndefined())
                    return new RGB32(TvgMath.Remap255<int>(Numeric(r)),
                        TvgMath.Remap255<int>(Numeric(g)), TvgMath.Remap255<int>(Numeric(b)));
            }
            return default;
        }

        // ================================================================
        //  JS object builders (creating JS objects that mirror C++ structs)
        // ================================================================

        private JsValue MakeNumber(Engine engine, float value)
        {
            var obj = new Jint.Native.JsObject(engine);
            obj.Set("0", new JsNumber(value));
            obj.Set(EXP_VALUE, new JsNumber(value));
            return obj;
        }

        private JsValue MakePoint2d(Engine engine, Point pt)
        {
            var obj = new Jint.Native.JsObject(engine);
            obj.Set("0", new JsNumber(pt.x));
            obj.Set("1", new JsNumber(pt.y));
            return obj;
        }

        private JsValue MakePointWithValue(Engine engine, Point pt)
        {
            var obj = MakePoint2d(engine, pt);
            obj.AsObject().Set(EXP_VALUE, obj);
            return obj;
        }

        private JsValue MakeColorObj(Engine engine, RGB32 rgb)
        {
            var obj = new Jint.Native.JsObject(engine);
            obj.Set("0", new JsNumber(rgb.r / 255.0f));
            obj.Set("1", new JsNumber(rgb.g / 255.0f));
            obj.Set("2", new JsNumber(rgb.b / 255.0f));
            return obj;
        }

        private JsValue MakeNativeRef(Engine engine, LottieProperty property)
        {
            var obj = new Jint.Native.JsObject(engine);
            NativeProperties.Add(obj, new LottiePropertyRef(property));
            return obj;
        }

        private JsValue BuildValue(Engine engine, float frameNo, LottieProperty property)
        {
            switch (property.type)
            {
                case LottieProperty.PropertyType.Integer:
                    return MakeNumber(engine, ((LottieInteger)property).Evaluate(frameNo));
                case LottieProperty.PropertyType.Float:
                    return MakeNumber(engine, ((LottieFloat)property).Evaluate(frameNo));
                case LottieProperty.PropertyType.Scalar:
                    return MakePointWithValue(engine, ((LottieScalar)property).Evaluate(frameNo));
                case LottieProperty.PropertyType.Vector:
                    return MakePointWithValue(engine, ((LottieVector)property).Evaluate(frameNo));
                case LottieProperty.PropertyType.PathSet:
                    return MakeNativeRef(engine, property);
                case LottieProperty.PropertyType.Color:
                    return MakeColorObj(engine, ((LottieColor)property).Evaluate(frameNo));
                case LottieProperty.PropertyType.Opacity:
                    return new JsNumber(((LottieOpacity)property).Evaluate(frameNo) / 2.55f);
                case LottieProperty.PropertyType.TextDoc:
                    return new JsString(((LottieTextDoc)property).Evaluate(frameNo).text ?? "");
                default:
                    TvgCommon.TVGERR("LOTTIE", "Non supported type for value? = {0}", (int)property.type);
                    break;
            }
            return JsValue.Undefined;
        }

        // ================================================================
        //  Build math functions into the global scope
        // ================================================================

        private float Rand()
        {
            return (float)(NextRandom() % 10000001) * 0.0000001f;
        }

        private void BuildMath(Engine engine)
        {
            // $bm_mul, $bm_sum, $bm_add, $bm_sub, $bm_div
            engine.SetValue("$bm_mul", new LottieExpressionFunction(engine, "$bm_mul", (_, args) => DoMulDiv(engine, args[0], JsToNumber(args[1]))));
            engine.SetValue("$bm_sum", new LottieExpressionFunction(engine, "$bm_sum", (_, args) => DoAddSub(engine, args[0], args[1], 1.0f)));
            engine.SetValue("$bm_add", new LottieExpressionFunction(engine, "$bm_add", (_, args) => DoAddSub(engine, args[0], args[1], 1.0f)));
            engine.SetValue("$bm_sub", new LottieExpressionFunction(engine, "$bm_sub", (_, args) => DoAddSub(engine, args[0], args[1], -1.0f)));
            engine.SetValue("$bm_div", new LottieExpressionFunction(engine, "$bm_div", (_, args) => DoMulDiv(engine, args[0], 1.0f / JsToNumber(args[1]))));

            // Non-prefixed versions
            engine.SetValue("mul", new LottieExpressionFunction(engine, "mul", (_, args) => DoMulDiv(engine, args[0], JsToNumber(args[1]))));
            engine.SetValue("sum", new LottieExpressionFunction(engine, "sum", (_, args) => DoAddSub(engine, args[0], args[1], 1.0f)));
            engine.SetValue("add", new LottieExpressionFunction(engine, "add", (_, args) => DoAddSub(engine, args[0], args[1], 1.0f)));
            engine.SetValue("sub", new LottieExpressionFunction(engine, "sub", (_, args) => DoAddSub(engine, args[0], args[1], -1.0f)));
            engine.SetValue("div", new LottieExpressionFunction(engine, "div", (_, args) => DoMulDiv(engine, args[0], 1.0f / JsToNumber(args[1]))));

            // $bm_mod, mod
            engine.SetValue("$bm_mod", new LottieExpressionFunction(engine, "$bm_mod", (_, args) => new JsNumber(JsToNumber(args[0]) % JsToNumber(args[1]))));
            engine.SetValue("mod", new LottieExpressionFunction(engine, "mod", (_, args) => new JsNumber(JsToNumber(args[0]) % JsToNumber(args[1]))));

            // clamp
            engine.SetValue("clamp", new LottieExpressionFunction(engine, "clamp", (_, args) =>
            {
                var n = JsToNumber(args[0]);
                var l1 = JsToNumber(args[1]);
                var l2 = JsToNumber(args[2]);
                if (n < l1) n = l1;
                if (n > l2) n = l2;
                return new JsNumber(n);
            }));

            // dot, cross, normalize, length
            engine.SetValue("dot", new LottieExpressionFunction(engine, "dot", (_, args) =>
            {
                var pa = JsToPoint(args[0]); var pb = JsToPoint(args[1]);
                return new JsNumber(TvgMath.Dot(pa, pb));
            }));
            engine.SetValue("cross", new LottieExpressionFunction(engine, "cross", (_, args) =>
            {
                var pa = JsToPoint(args[0]); var pb = JsToPoint(args[1]);
                return new JsNumber(TvgMath.Cross(pa, pb));
            }));
            engine.SetValue("normalize", new LottieExpressionFunction(engine, "normalize", (_, args) =>
            {
                var pt = JsToPoint(args[0]);
                var len = TvgMath.PointLength(pt);
                pt = TvgMath.PointDiv(pt, len);
                return MakePoint2d(engine, pt);
            }));
            engine.SetValue("length", new LottieExpressionFunction(engine, "length", (_, args) =>
            {
                return new JsNumber(TvgMath.PointLength(JsToPoint(args[0])));
            }));

            // random
            engine.SetValue("random", new LottieExpressionFunction(engine, "random", (_, args) => new JsNumber(Rand())));
            engine.GetValue("Math").AsObject().Set("random", new ClrFunction(engine, "random", (_, args) =>
            {
                double max = OperatingSystem.IsWindows() ? 32767 : int.MaxValue;
                return new JsNumber(NextRandom() / max * (max - 1) / max);
            }, 0, Jint.Runtime.Descriptors.PropertyFlag.Configurable));

            // degreesToRadians, radiansToDegrees
            engine.SetValue("degreesToRadians", new LottieExpressionFunction(engine, "degreesToRadians", (_, args) => new JsNumber(TvgMath.Deg2Rad(JsToNumber(args[0])))));
            engine.SetValue("radiansToDegrees", new LottieExpressionFunction(engine, "radiansToDegrees", (_, args) => new JsNumber(TvgMath.Rad2Deg(JsToNumber(args[0])))));

            // linear, ease, easeIn, easeOut
            engine.SetValue("linear", MakeVariadicFunc(engine, "linear", args => DoLinear(engine, args)));
            engine.SetValue("ease", MakeVariadicFunc(engine, "ease", args => DoEase(engine, args)));
            engine.SetValue("easeIn", MakeVariadicFunc(engine, "easeIn", args => DoEaseIn(engine, args)));
            engine.SetValue("easeOut", MakeVariadicFunc(engine, "easeOut", args => DoEaseOut(engine, args)));
        }

        // ================================================================
        //  Math operation helpers
        // ================================================================

        private static Point JsToPoint(JsValue v)
        {
            if (v.IsObject())
            {
                var obj = v.AsObject();
                return new Point(Numeric(obj.Get("0")), Numeric(obj.Get("1")));
            }
            return default;
        }

        private static float Numeric(JsValue value) => value.IsNumber() ? (float)value.AsNumber() : 0;

        private static float JsToNumber(JsValue v)
        {
            if (v.IsNumber()) return (float)v.AsNumber();
            if (v.IsObject())
            {
                var obj = v.AsObject();
                var val = obj.Get("0");
                return Numeric(val);
            }
            return 0;
        }

        private static bool Number(JsValue value, out Point result)
        {
            result = default;
            if (value.IsNumber())
            {
                result.x = (float)value.AsNumber();
                return true;
            }
            if (!value.IsObject()) return true;
            var obj = value.AsObject();
            var x = obj.Get("0");
            var y = obj.Get("1");
            result.x = Numeric(x);
            if (y.IsUndefined()) return true;
            result.y = Numeric(y);
            return false;
        }

        private JsValue DoAddSub(Engine engine, JsValue a, JsValue b, float addsub)
        {
            // string + string
            if (a.IsString() || b.IsString())
            {
                return new JsString(a.ToString() + b.ToString());
            }

            var n1 = Number(a, out var v1);
            var n2 = Number(b, out var v2);

            // 1d + 1d
            if (n1 && n2) return new JsNumber(v1.x + addsub * v2.x);
            if (!n1 && !n2) return MakePoint2d(engine, TvgMath.PointAdd(v1, TvgMath.PointMul(v2, addsub)));
            if (n1)
            {
                v2.x = v1.x + addsub * v2.x;
                return MakePoint2d(engine, v2);
            }
            v1.x += addsub * v2.x;
            return MakePoint2d(engine, v1);
        }

        private JsValue DoMulDiv(Engine engine, JsValue arg1, float arg2)
        {
            var scalar = Number(arg1, out var value);
            if (scalar) return new JsNumber(value.x * arg2);
            return MakePoint2d(engine, TvgMath.PointMul(value, arg2));
        }

        private JsValue DoInterp(Engine engine, float t, JsValue[] args, int offset)
        {
            var tMin = JsToNumber(args[1]);
            var tMax = JsToNumber(args[2]);
            var idx = 2;

            t = (t - tMin) / (tMax - tMin);
            if (t < 0) t = 0.0f;
            else if (t > 1) t = 1.0f;

            // 2d
            if (args[idx + 1].IsObject() && args[idx + 2].IsObject())
            {
                var p1 = JsToPoint(args[idx + 1]);
                var p2 = JsToPoint(args[idx + 2]);
                var result = TvgMath.Lerp(p1, p2, t);
                return MakePoint2d(engine, result);
            }

            // 1d
            return new JsNumber(TvgMath.Lerp(JsToNumber(args[idx + 1]), JsToNumber(args[idx + 2]), t));
        }

        private JsValue DoLinear(Engine engine, JsValue[] args)
        {
            return DoInterp(engine, JsToNumber(args[0]), args, 0);
        }

        private JsValue DoEase(Engine engine, JsValue[] args)
        {
            var t = JsToNumber(args[0]);
            t = (t < 0.5f) ? (4 * t * t * t) : (1.0f - MathF.Pow(-2.0f * t + 2.0f, 3) * 0.5f);
            return DoInterp(engine, t, args, 0);
        }

        private JsValue DoEaseIn(Engine engine, JsValue[] args)
        {
            var t = JsToNumber(args[0]);
            t = t * t * t;
            return DoInterp(engine, t, args, 0);
        }

        private JsValue DoEaseOut(Engine engine, JsValue[] args)
        {
            var t = JsToNumber(args[0]);
            t = 1.0f - MathF.Pow(1.0f - t, 3);
            return DoInterp(engine, t, args, 0);
        }

        // ================================================================
        //  Build global context for each evaluation
        // ================================================================

        private void BuildGlobal(Engine engine, float frameNo, LottieExpression exp)
        {
            engine.SetValue(EXP_INDEX, (double)exp.layer!.ix);
            engine.SetValue("inPoint", new JsNumber(exp.comp != null ? exp.layer.inFrame / exp.comp.frameRate : 0));
            engine.SetValue("outPoint", new JsNumber(exp.comp != null ? exp.layer.outFrame / exp.comp.frameRate : 0));

        }

        private void BuildCompFunction(Engine engine)
        {
            // Created before thisComp/thisLayer/thisProperty and native math.
            var context = _contexts.Value!;
            if (context.Comp != null) return;
            context.Comp = new LottieExpressionFunction(engine, "comp", (_, args) =>
            {
                var exp = context.Expression!;
                var frameNo = context.Frame;
                var name = args[0].AsString();
                var id = TvgCompressor.Djb2EncodeNative(name);
                var layer = exp.layer?.LayerById(id);
                if (layer == null) return JsValue.Undefined;
                var obj = new Jint.Native.JsObject(engine);
                SetLayerProps(engine, obj, frameNo, layer, exp.comp!.root!, exp);
                return obj;
            });
            engine.SetValue("comp", context.Comp);
        }

        private void BuildComp(Engine engine, LottieComposition? comp, float frameNo, LottieExpression exp)
        {
            if (comp?.root == null) return;

            // layer(index/name) function on comp -- also set layer and numLayers as properties on it
            var compFunc = new LottieExpressionFunction(engine, "comp", (_, args) =>
            {
                var root = comp.root;
                LottieLayer? layer;
                if (args[0].IsNumber())
                    layer = root.LayerByIdx((short)args[0].AsNumber());
                else
                    layer = root.LayerById(TvgCompressor.Djb2EncodeNative(args[0].AsString()));
                if (layer == null) return JsValue.Undefined;
                return MakeLayerChildObj(engine, frameNo, layer, exp);
            });
            // Also set layer and numLayers as properties on the comp function object
            var compJsObj = _contexts.Value!.Comp!;
            if (compJsObj.IsObject())
            {
                compJsObj.AsObject().Set("layer", compFunc);
                compJsObj.AsObject().Set("numLayers", new JsNumber(comp.root.children.Count));
            }
        }

        private void BuildThisComp(Engine engine, float frameNo, LottieExpression exp)
        {
            var thisCompObj = _contexts.Value!.ThisComp!;

            if (exp.comp != null)
            {
                thisCompObj.Set(EXP_WIDTH, new JsNumber(exp.comp.w));
                thisCompObj.Set(EXP_HEIGHT, new JsNumber(exp.comp.h));
                thisCompObj.Set("duration", new JsNumber(exp.comp.Duration()));
                thisCompObj.Set("frameDuration", new JsNumber(exp.comp.frameRate > 0 ? 1.0f / exp.comp.frameRate : 0));
                if (exp.comp.name != null)
                    thisCompObj.Set(EXP_NAME, new JsString(exp.comp.name));
            }


            // layer(index/name) on thisComp
            var layerComp = exp.layer?.precomp ?? exp.comp?.root;
            if (layerComp != null)
            {
                thisCompObj.Set("layer", new LottieExpressionFunction(engine, "layer", (_, args) =>
                {
                    LottieLayer? layer;
                    if (args[0].IsNumber())
                        layer = layerComp.LayerByIdx((short)args[0].AsNumber());
                    else
                        layer = layerComp.LayerById(TvgCompressor.Djb2EncodeNative(args[0].AsString()));
                    if (layer == null) return JsValue.Undefined;
                    return MakeLayerChildObj(engine, frameNo, layer, exp);
                }));
                thisCompObj.Set("numLayers", new JsNumber(layerComp.children.Count));
            }


        }

        private void BuildThisLayer(Engine engine, float frameNo, LottieExpression exp)
        {
            var thisLayerObj = _contexts.Value!.ThisLayer!;
            if (exp.layer != null)
            {
                SetLayerProps(engine, thisLayerObj, frameNo, exp.layer, exp.comp?.root, exp);
            }
        }

        private void BuildThisProperty(Engine engine, float frameNo, LottieExpression exp)
        {
            var thisPropertyObj = _contexts.Value!.ThisProperty!;
            if (exp.property != null)
            {
                SetPropertyProps(engine, thisPropertyObj, frameNo, exp);
            }
            if (exp.property != null)
            {
                if (NativeProperties.TryGetValue(thisPropertyObj, out var reference)) reference.Property = exp.property;
                else NativeProperties.Add(thisPropertyObj, new LottiePropertyRef(exp.property));
            }
        }

        private void BuildTransformGlobal(Engine engine, float frameNo, LottieTransform transform) =>
            BuildTransform(engine, engine.Global, frameNo, transform);

        private void BuildTransform(Engine engine, Jint.Native.Object.ObjectInstance context, float frameNo, LottieTransform transform)
        {
            var transformObj = new Jint.Native.JsObject(engine);
            context.Set("transform", transformObj);
            transformObj.Set("anchorPoint", MakePointWithValue(engine, transform.anchor.Evaluate(frameNo)));
            transformObj.Set("position", MakePointWithValue(engine, transform.position.Evaluate(frameNo)));
            transformObj.Set("scale", MakePointWithValue(engine, transform.scale.Evaluate(frameNo)));
            transformObj.Set("rotation", new JsNumber(transform.rotation.Evaluate(frameNo)));
            transformObj.Set("opacity", new JsNumber(transform.opacity.Evaluate(frameNo) / 2.55f));
        }

        // ================================================================
        //  Build property onto a context (global or thisProperty)
        // ================================================================

        private sealed class PropertyBinding
        {
            public LottieExpression Expression = null!;
            public float Frame;
        }

        private static readonly ConditionalWeakTable<Jint.Native.Object.ObjectInstance, PropertyBinding> FunctionBindings = new();

        private static PropertyBinding? Binding(JsValue value) => value.IsObject() &&
            FunctionBindings.TryGetValue(value.AsObject(), out var binding) ? binding : null;

        private void BuildProperty(Engine engine, float frameNo, LottieExpression exp, string prefix) =>
            SetPropertyProps(engine, engine.Global, frameNo, exp);

        private void SetPropertyProps(Engine engine, Jint.Native.Object.ObjectInstance ctx, float frameNo, LottieExpression exp)
        {
            if (exp.property == null) return;
            ctx.Set(EXP_VALUE, BuildValue(engine, frameNo, exp.property));
            var init = ctx.Get("valueAtTime").IsUndefined();

            // These functions each carry an expression pointer upstream. An
            // alias keeps its identity while the registered function's binding
            // is refreshed; overwritten JS functions retain their JS behavior.
            void SetFunction(string name, Func<PropertyBinding, JsValue[], JsValue> callback)
            {
                var binding = init ? new PropertyBinding() : Binding(ctx.Get(name));
                if (binding == null) return;
                binding.Expression = exp;
                binding.Frame = frameNo;
                if (!init) return;
                var function = new LottieExpressionFunction(engine, name, (_, args) => callback(binding, args));
                FunctionBindings.Add(function, binding);
                ctx.Set(name, function);
            }

            SetFunction("valueAtTime", (b, args) => BuildValue(engine,
                b.Expression.comp!.FrameAtTime(JsToNumber(args[0])), b.Expression.property!));
            SetFunction("velocityAtTime", (b, args) => DoVelocityAtTime(engine, b.Expression, JsToNumber(args[0])));
            SetFunction("speedAtTime", (b, args) => DoSpeedAtTime(engine, b.Expression, JsToNumber(args[0])));
            SetFunction("key", (b, args) => DoKey(engine, b.Expression, (int)Numeric(args[0])));
            SetFunction("nearestKey", (b, args) =>
            {
                var frame = b.Expression.comp!.FrameAtTime(JsToNumber(args[0]));
                var obj = new Jint.Native.JsObject(engine);
                obj.Set(EXP_INDEX, new JsNumber(b.Expression.property!.Nearest(frame)));
                return obj;
            });
            ctx.Set("velocity", new JsNumber(0));
            ctx.Set("speed", new JsNumber(0));
            ctx.Set("propertyIndex", new JsNumber(exp.property.ix));
            ctx.Set("numKeys", new JsNumber(exp.property.FrameCnt()));

            // Seven property functions share one ExpContent; content/effect
            // share another. Update the same binding through the same keys as
            // native so saved aliases and function overwrites have its lifetime.
            void Shared(string name, PropertyBinding binding, Func<PropertyBinding, JsValue[], JsValue> callback)
            {
                var function = new LottieExpressionFunction(engine, name, (_, args) => callback(binding, args));
                FunctionBindings.Add(function, binding);
                ctx.Set(name, function);
            }
            if (init)
            {
                var property = new PropertyBinding { Expression = exp, Frame = frameNo };
                Shared("wiggle", property, (b, args) => DoWiggle(engine, b.Frame, b.Expression, args));
                Shared("temporalWiggle", property, (b, args) => DoTemporalWiggle(engine, b.Frame, b.Expression, args));
                Shared("propertyGroup", property, (b, args) => DoPropertyGroup(engine, b.Frame, b.Expression, (int)Numeric(args[0])));
                Shared("loopIn", property, (b, args) => DoLoopIn(engine, b.Frame, b.Expression, args));
                Shared("loopOut", property, (b, args) => DoLoopOut(engine, b.Frame, b.Expression, args));
                Shared("loopInDuration", property, (b, args) => DoLoopInDuration(engine, b.Frame, b.Expression, args));
                Shared("loopOutDuration", property, (b, args) => DoLoopOutDuration(engine, b.Frame, b.Expression, args));
                var layer = new PropertyBinding { Expression = exp, Frame = frameNo };
                Shared(EXP_CONTENT, layer, (b, args) => DoContent(engine, b.Frame, b.Expression.layer!, args[0]));
                Shared(EXP_EFFECT, layer, (b, args) => DoEffect(engine, b.Frame, b.Expression, b.Expression.layer!, args[0]));
            }
            else
            {
                foreach (var key in new[] { "wiggle", EXP_CONTENT })
                {
                    var binding = Binding(ctx.Get(key));
                    if (binding == null) { TvgCommon.TVGERR("LOTTIE", "ExpContent lost due to function overwrite"); continue; }
                    binding.Expression = exp;
                    binding.Frame = frameNo;
                }
            }
            if (exp.property.type == LottieProperty.PropertyType.PathSet)
                BuildPathExpansionOnObj(engine, ctx, frameNo, exp.property);
        }

        // ================================================================
        //  Layer builder
        // ================================================================

        private void SetLayerProps(Engine engine, Jint.Native.Object.ObjectInstance ctx, float frameNo, LottieLayer layer, LottieRootLayer? comp, LottieExpression exp)
        {
            ctx.Set(EXP_WIDTH, new JsNumber(layer.w));
            ctx.Set(EXP_HEIGHT, new JsNumber(layer.h));
            ctx.Set(EXP_INDEX, new JsNumber(layer.ix));

            // parent
            ctx.Set("parent", new Jint.Native.JsObject(engine));

            ctx.Set("hasParent", layer.parent != null ? JsBoolean.True : JsBoolean.False);
            ctx.Set("inPoint", new JsNumber(exp.comp != null ? layer.inFrame / exp.comp.frameRate : 0));
            ctx.Set("outPoint", new JsNumber(exp.comp != null ? layer.outFrame / exp.comp.frameRate : 0));
            ctx.Set("startTime", new JsNumber(exp.comp != null ? exp.comp.TimeAtFrame(layer.startFrame) : 0));
            ctx.Set("hasVideo", JsBoolean.False);
            ctx.Set("hasAudio", JsBoolean.False);
            ctx.Set("enabled", layer.hidden ? JsBoolean.False : JsBoolean.True);
            ctx.Set("audioActive", JsBoolean.False);

            if (layer.transform != null) BuildTransform(engine, ctx, frameNo, layer.transform);

            // timeRemap
            ctx.Set("timeRemap", MakeNativeRef(engine, layer.timeRemap));

            // toComp(point) -- transform point to composition space
            ctx.Set("toComp", new LottieExpressionFunction(engine, "toComp", (_, args) =>
            {
                var pt = JsToPoint(args[0]);
                pt = TvgMath.Transform(pt, layer.cacheMatrix);
                return MakePoint2d(engine, pt);
            }));

            // content(name)
            ctx.Set(EXP_CONTENT, new LottieExpressionFunction(engine, EXP_CONTENT, (_, args) =>
                DoContent(engine, frameNo, layer, args[0])));

            // effect(name/index)
            ctx.Set(EXP_EFFECT, new LottieExpressionFunction(engine, EXP_EFFECT, (_, args) =>
                DoEffect(engine, frameNo, exp, layer, args[0])));
        }

        // ================================================================
        //  Content lookup (mirrors C++ _content)
        // ================================================================

        private JsValue DoContent(Engine engine, float frameNo, LottieGroup group, JsValue nameArg)
        {
            var name = nameArg.AsString();
            var id = TvgCompressor.Djb2EncodeNative(name);
            var target = group.Content(id);
            if (target == null) return JsValue.Undefined;

            switch (target.type)
            {
                case LottieObject.ObjectType.Group:
                {
                    var grp = (LottieGroup)target;
                    var obj = new LottieExpressionFunction(engine, EXP_CONTENT, (_, args) =>
                        DoContent(engine, frameNo, grp, args[0]));
                    // Attach transform
                    foreach (var child in grp.children)
                    {
                        if (child.type == LottieObject.ObjectType.Transform)
                        {
                            BuildTransform(engine, obj, frameNo, (LottieTransform)child);
                            break;
                        }
                    }
                    obj.Set(EXP_CONTENT, obj);
                    return obj;
                }
                case LottieObject.ObjectType.Path:
                {
                    var path = (LottiePath)target;
                    var obj = MakeNativeRef(engine, path.pathset).AsObject();
                    obj.Set("path", obj);
                    return obj;
                }
                case LottieObject.ObjectType.Polystar:
                {
                    var polystar = (LottiePolyStar)target;
                    var obj = new Jint.Native.JsObject(engine);
                    obj.Set("position", MakeNativeRef(engine, polystar.position));
                    obj.Set("innerRadius", new JsNumber(polystar.innerRadius.Evaluate(frameNo)));
                    obj.Set("outerRadius", new JsNumber(polystar.outerRadius.Evaluate(frameNo)));
                    obj.Set("innerRoundness", new JsNumber(polystar.innerRoundness.Evaluate(frameNo)));
                    obj.Set("outerRoundness", new JsNumber(polystar.outerRoundness.Evaluate(frameNo)));
                    obj.Set("rotation", new JsNumber(polystar.rotation.Evaluate(frameNo)));
                    obj.Set("points", new JsNumber(polystar.ptsCnt.Evaluate(frameNo)));
                    return obj;
                }
                case LottieObject.ObjectType.Rect:
                {
                    var rect = (LottieRect)target;
                    var obj = new Jint.Native.JsObject(engine);
                    obj.Set(EXP_SIZE, BuildValue(engine, frameNo, rect.size));
                    obj.Set(EXP_POSITION, BuildValue(engine, frameNo, rect.position));
                    obj.Set("roundness", new JsNumber(rect.radius.Evaluate(frameNo)));
                    return obj;
                }
                case LottieObject.ObjectType.Ellipse:
                {
                    var ellipse = (LottieEllipse)target;
                    var obj = new Jint.Native.JsObject(engine);
                    obj.Set(EXP_SIZE, BuildValue(engine, frameNo, ellipse.size));
                    obj.Set(EXP_POSITION, BuildValue(engine, frameNo, ellipse.position));
                    return obj;
                }
                case LottieObject.ObjectType.Trimpath:
                {
                    var trimpath = (LottieTrimpath)target;
                    var obj = new Jint.Native.JsObject(engine);
                    obj.Set("start", new JsNumber(trimpath.start.Evaluate(frameNo)));
                    obj.Set("end", new JsNumber(trimpath.end.Evaluate(frameNo)));
                    obj.Set(EXP_OFFSET, new JsNumber(trimpath.offset.Evaluate(frameNo)));
                    return obj;
                }
                default:
                    break;
            }
            return JsValue.Undefined;
        }

        // ================================================================
        //  Effect lookup (mirrors C++ _effect / _effectProperty)
        // ================================================================

        private JsValue DoEffect(Engine engine, float frameNo, LottieExpression exp, LottieLayer layer, JsValue nameArg)
        {
            LottieEffect? effect;
            if (nameArg.IsNumber())
                effect = layer.EffectByIdx((short)nameArg.AsNumber());
            else
                effect = layer.EffectById(TvgCompressor.Djb2EncodeNative(nameArg.AsString()));

            if (effect == null) return JsValue.Undefined;

            // Return a function that looks up effect properties by name
            return new LottieExpressionFunction(engine, "effect", (_, args) =>
            {
                if (effect is LottieFxCustom custom)
                {
                    var propName = args[0].AsString();
                    var property = custom.FindProperty(propName);
                    if (property == null) return JsValue.Undefined;
                    return BuildValue(engine, frameNo, property);
                }
                return JsValue.Undefined;
            });
        }

        // ================================================================
        //  Path expansion (mirrors C++ _buildPath)
        // ================================================================

        private void BuildPathExpansion(Engine engine, float frameNo, LottieProperty pathset)
        {
            // points() -- returns native ref to the pathset property
            engine.SetValue("points", new LottieExpressionFunction(engine, "points", (_, args) => MakeNativeRef(engine, pathset)));

            // pointOnPath(progress) -- evaluate path at progress and return point
            engine.SetValue("pointOnPath", new LottieExpressionFunction(engine, "pointOnPath", (_, args) =>
                DoPointOnPath(engine, frameNo, pathset, args[0])));

            // tangentOnPath(progress) -- evaluate tangent at progress and return normalized direction
            engine.SetValue("tangentOnPath", new LottieExpressionFunction(engine, "tangentOnPath", (_, args) =>
                DoTangentOnPath(engine, frameNo, pathset, args[0])));
        }

        private void BuildPathExpansionOnObj(Engine engine, Jint.Native.Object.ObjectInstance ctx, float frameNo, LottieProperty pathset)
        {
            ctx.Set("points", new LottieExpressionFunction(engine, "points", (_, args) => MakeNativeRef(engine, pathset)));
            ctx.Set("pointOnPath", new LottieExpressionFunction(engine, "pointOnPath", (_, args) =>
                DoPointOnPath(engine, frameNo, pathset, args[0])));
            ctx.Set("tangentOnPath", new LottieExpressionFunction(engine, "tangentOnPath", (_, args) =>
                DoTangentOnPath(engine, frameNo, pathset, args[0])));
        }

        private JsValue DoPointOnPath(Engine engine, float frameNo, LottieProperty pathset, JsValue progressArg)
        {
            if (pathset is LottiePathSet ps)
            {
                var progress = JsToNumber(progressArg);
                var renderPath = RenderPath.Scratch();
                unsafe { ps.DefaultPath(frameNo, renderPath, null); }
                var pt = renderPath.PointAt(progress);
                return MakePoint2d(engine, pt);
            }
            return JsValue.Undefined;
        }

        private JsValue DoTangentOnPath(Engine engine, float frameNo, LottieProperty pathset, JsValue progressArg)
        {
            if (pathset is LottiePathSet ps)
            {
                var progress = JsToNumber(progressArg);
                var renderPath = RenderPath.Scratch();
                unsafe { ps.DefaultPath(frameNo, renderPath, null); }
                var a = renderPath.PointAt(Math.Max(0.0f, progress - 0.001f));
                var b = renderPath.PointAt(Math.Min(1.0f, progress + 0.001f));
                var t = new Point(b.x - a.x, b.y - a.y);
                var len = TvgMath.PointLength(t);
                if (len > 0.0f) { t.x /= len; t.y /= len; }
                return MakePoint2d(engine, t);
            }
            return JsValue.Undefined;
        }

        // ================================================================
        //  Wiggle (mirrors C++ _wiggle)
        // ================================================================

        private JsValue DoWiggle(Engine engine, float frameNo, LottieExpression exp, JsValue[] args)
        {
            var freq = JsToNumber(args[0]);
            var amp = JsToNumber(args[1]);
            var octaves = args.Length > 2 ? (int)args[2].AsNumber() : 1;
            var ampm = args.Length > 3 ? JsToNumber(args[3]) : 0.5f;
            var time = args.Length > 4 ? JsToNumber(args[4]) : (exp.comp != null ? exp.comp.TimeAtFrame(frameNo) : 0);

            Point result = default;
            var property = exp.property;

            if (property != null)
            {
                if (property.type == LottieProperty.PropertyType.Vector)
                    result = ((LottieVector)property).Evaluate(frameNo);
                else if (property.type == LottieProperty.PropertyType.Scalar)
                    result = ((LottieScalar)property).Evaluate(frameNo);
            }

            // 1D Perlin noise (replaces old RNG-based wiggle)
            static float Perlin1D(float x, int seed)
            {
                var x0 = (int)MathF.Floor(x);
                var x1 = x0 + 1;
                var fx = x - (float)x0;

                // Quintic fade curve for smooth interpolation (6t^5 - 15t^4 + 10t^3)
                var u = fx * fx * fx * TvgMath.MultiplyAdd(fx, TvgMath.MultiplyAdd(fx, 6.0f, -15.0f), 10.0f);

                // Deterministic random generator using glibc's LCG algorithm
                static float Gradient1D(long seed)
                {
                    seed = (seed * 1103515245 + 12345) & 0x7fffffff;
                    return (float)((double)seed / 2147483647) < 0.5f ? -1.0f : 1.0f;
                }

                // Calculate dot products (in 1D, just multiplication with distance)
                var d0 = Gradient1D(x0 * 100000 + seed) * fx;
                var d1 = Gradient1D(x1 * 100000 + seed) * (fx - 1.0f);

                // Interpolate between the two gradient influences and scale by 3.0
                return TvgMath.Lerp(d0, d1, u) * 3.0f;
            }

            for (int o = 0; o < octaves; ++o)
            {
                var repeat = time * freq;
                // Factors (1000000, 2000000) separate X/Y axes to prevent seed collisions across octaves
                result.x = TvgMath.MultiplyAdd(Perlin1D(repeat, 1000000 + o), amp, result.x);
                result.y = TvgMath.MultiplyAdd(Perlin1D(repeat, 2000000 + o), amp, result.y);
                freq *= 2.0f;
                amp *= ampm;
            }
            return MakePoint2d(engine, result);
        }

        // ================================================================
        //  Temporal wiggle (mirrors C++ _temporalWiggle)
        // ================================================================

        private JsValue DoTemporalWiggle(Engine engine, float frameNo, LottieExpression exp, JsValue[] args)
        {
            var freq = JsToNumber(args[0]);
            var amp = JsToNumber(args[1]);
            var octaves = args.Length > 2 ? (int)args[2].AsNumber() : 1;
            var ampm = args.Length > 3 ? JsToNumber(args[3]) : 5.0f;
            var time = args.Length > 4 ? JsToNumber(args[4]) : (exp.comp != null ? exp.comp.TimeAtFrame(frameNo) : 0);
            var wiggleTime = time;

            for (int o = 0; o < octaves; ++o)
            {
                var repeat = (int)(time * freq);
                var frac = (time * freq - repeat);
                for (int i = 0; i < repeat; ++i)
                {
                    wiggleTime = TvgMath.MultiplyAdd((Rand() * 2.0f - 1.0f) * amp, frac, wiggleTime);
                }
                freq *= 2.0f;
                amp *= ampm;
            }

            if (exp.property != null && exp.comp != null)
                return BuildValue(engine, exp.comp.FrameAtTime(wiggleTime), exp.property);
            return JsValue.Undefined;
        }

        // ================================================================
        //  Loop helpers (mirrors C++ _loopIn, _loopOut, etc.)
        // ================================================================

        private static LottieProperty.Loop ParseLoopMode(JsValue[] args)
        {
            var mode = LottieProperty.Loop.InCycle;
            if (args.Length > 0 && args[0].IsString())
            {
                var name = args[0].AsString();
                if (name == EXP_CYCLE) mode = LottieProperty.Loop.InCycle;
                else if (name == EXP_PINGPONG) mode = LottieProperty.Loop.InPingPong;
                else if (name == EXP_OFFSET) mode = LottieProperty.Loop.InOffset;
                else if (name == EXP_CONTINUE) mode = LottieProperty.Loop.InContinue;
            }
            return mode;
        }

        private JsValue DoLoopOut(Engine engine, float frameNo, LottieExpression exp, JsValue[] args)
        {
            if (exp.property == null || exp.layer == null) return JsValue.Undefined;
            var mode = (LottieProperty.Loop)((int)ParseLoopMode(args) + LOOP_OUT_OFFSET);
            var key = args.Length > 1 ? (uint)args[1].AsNumber() : 0u;
            var loopedFrame = exp.property.DoLoop(frameNo, key, mode, exp.layer.outFrame);
            return BuildValue(engine, loopedFrame, exp.property);
        }

        private JsValue DoLoopOutDuration(Engine engine, float frameNo, LottieExpression exp, JsValue[] args)
        {
            if (exp.property == null) return JsValue.Undefined;
            var mode = (LottieProperty.Loop)((int)ParseLoopMode(args) + LOOP_OUT_OFFSET);
            var outFrame = (args.Length > 1 && exp.comp != null) ? exp.comp.FrameAtTime(JsToNumber(args[1])) : float.MaxValue;
            var loopedFrame = exp.property.DoLoop(frameNo, 0, mode, outFrame);
            return BuildValue(engine, loopedFrame, exp.property);
        }

        private JsValue DoLoopIn(Engine engine, float frameNo, LottieExpression exp, JsValue[] args)
        {
            if (exp.property == null || exp.layer == null) return JsValue.Undefined;
            var mode = ParseLoopMode(args);
            var key = args.Length > 1 ? (uint)args[1].AsNumber() : 0u;
            var loopedFrame = exp.property.DoLoop(frameNo, key, mode, exp.layer.outFrame);
            return BuildValue(engine, loopedFrame, exp.property);
        }

        private JsValue DoLoopInDuration(Engine engine, float frameNo, LottieExpression exp, JsValue[] args)
        {
            if (exp.property == null) return JsValue.Undefined;
            var mode = ParseLoopMode(args);
            var inFrame = (args.Length > 1 && exp.comp != null) ? exp.comp.FrameAtTime(JsToNumber(args[1])) : float.MaxValue;
            var loopedFrame = exp.property.DoLoop(frameNo, 0, mode, inFrame);
            return BuildValue(engine, loopedFrame, exp.property);
        }

        // ================================================================
        //  Key access (mirrors C++ _key)
        // ================================================================

        private JsValue DoKey(Engine engine, LottieExpression exp, int index)
        {
            if (exp.property == null || exp.comp == null) return JsValue.Undefined;
            var fn = exp.property.FrameNo(index);
            var time = exp.comp.TimeAtFrame(fn);
            var value = BuildValue(engine, fn, exp.property);

            var obj = new Jint.Native.JsObject(engine);
            obj.Set(EXP_TIME, new JsNumber(time));
            obj.Set(EXP_INDEX, new JsNumber(index));
            obj.Set(EXP_VALUE, value);

            // Direct access: key[0], key[1]
            if (exp.property.type == LottieProperty.PropertyType.Float)
            {
                obj.Set("0", value);
            }
            else if (exp.property.type == LottieProperty.PropertyType.Scalar ||
                     exp.property.type == LottieProperty.PropertyType.Vector)
            {
                if (value.IsObject())
                {
                    var vObj = value.AsObject();
                    var v0 = vObj.Get("0");
                    var v1 = vObj.Get("1");
                    if (v0 != null && !v0.IsUndefined()) obj.Set("0", v0);
                    if (v1 != null && !v1.IsUndefined()) obj.Set("1", v1);
                }
            }

            return obj;
        }

        // ================================================================
        //  Velocity/Speed at time (mirrors C++ _velocityAtTime, _speedAtTime)
        // ================================================================

        private JsValue DoVelocityAtTime(Engine engine, LottieExpression exp, float time)
        {
            if (exp.property == null || exp.comp == null) return JsValue.Undefined;

            var key = (int)exp.property.Nearest(exp.comp.FrameAtTime(time));
            var pframe = exp.property.FrameNo(key - 1);
            var cframe = exp.property.FrameNo(key);
            var elapsed = (cframe - pframe) / exp.comp.frameRate;

            switch (exp.property.type)
            {
                case LottieProperty.PropertyType.Float:
                {
                    var prv = ((LottieFloat)exp.property).Evaluate(pframe);
                    var cur = ((LottieFloat)exp.property).Evaluate(cframe);
                    return new JsNumber((cur - prv) / elapsed);
                }
                case LottieProperty.PropertyType.Scalar:
                {
                    var prv = ((LottieScalar)exp.property).Evaluate(pframe);
                    var cur = ((LottieScalar)exp.property).Evaluate(cframe);
                    return MakePoint2d(engine, new Point((cur.x - prv.x) / elapsed, (cur.y - prv.y) / elapsed));
                }
                case LottieProperty.PropertyType.Vector:
                {
                    var prv = ((LottieVector)exp.property).Evaluate(pframe);
                    var cur = ((LottieVector)exp.property).Evaluate(cframe);
                    return MakePoint2d(engine, new Point((cur.x - prv.x) / elapsed, (cur.y - prv.y) / elapsed));
                }
                default:
                    TvgCommon.TVGLOG("LOTTIE", "Non supported type for velocityAtTime?");
                    break;
            }
            return JsValue.Undefined;
        }

        private JsValue DoSpeedAtTime(Engine engine, LottieExpression exp, float time)
        {
            if (exp.property == null || exp.comp == null) return JsValue.Undefined;

            var key = (int)exp.property.Nearest(exp.comp.FrameAtTime(time));
            var pframe = exp.property.FrameNo(key - 1);
            var cframe = exp.property.FrameNo(key);

            Point prv, cur;
            switch (exp.property.type)
            {
                case LottieProperty.PropertyType.Scalar:
                    prv = ((LottieScalar)exp.property).Evaluate(pframe);
                    cur = ((LottieScalar)exp.property).Evaluate(cframe);
                    break;
                case LottieProperty.PropertyType.Vector:
                    prv = ((LottieVector)exp.property).Evaluate(pframe);
                    cur = ((LottieVector)exp.property).Evaluate(cframe);
                    break;
                default:
                    TvgCommon.TVGLOG("LOTTIE", "Non supported type for speedAtTime?");
                    return JsValue.Undefined;
            }

            var elapsed = (cframe - pframe) / exp.comp.frameRate;
            var speed = MathF.Sqrt((float)(Math.Pow(cur.x - prv.x, 2) + Math.Pow(cur.y - prv.y, 2))) / elapsed;
            return new JsNumber(speed);
        }

        // ================================================================
        //  Property group (mirrors C++ _propertyGroup)
        // ================================================================

        private JsValue DoPropertyGroup(Engine engine, float frameNo, LottieExpression exp, int level)
        {
            if (level == 1 && exp.obj != null)
            {
                // Return a function that looks up properties by index
                return new LottieExpressionFunction(engine, "propertyGroup", (_, args) =>
                {
                    var property = exp.obj.FindProperty((ushort)args[0].AsNumber());
                    if (property == null) return JsValue.Undefined;
                    return BuildValue(engine, frameNo, property);
                });
            }

            TvgCommon.TVGLOG("LOTTIE", "propertyGroup({0})?", level);
            return JsValue.Undefined;
        }
    }
}
