// Ported from ThorVG/src/loaders/lottie/tvgLottieExpressions.h and tvgLottieExpressions.cpp
// Uses Jint (MIT-licensed) as the JavaScript engine, replacing JerryScript from the C++ version.

using System;
using System.Collections.Generic;
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

        private const int LOOP_OUT_OFFSET = 4;

        // Kept for API compat with Retrieve(); no longer used.
        #pragma warning disable CS0169
        private static LottieExpressions? _instance;
        #pragma warning restore CS0169

        // Per-instance random
        private readonly Random _random = new();

        // The Jint engine is created once and reused across evaluations.
        // BuildMath sets up constant math functions once; per-frame state is refreshed each call.
        private Engine? _engine;

        private LottieExpressions()
        {
        }

        /// <summary>
        /// Create a Jint FunctionInstance that properly handles variadic arguments.
        /// Jint's Func&lt;JsValue[], JsValue&gt; doesn't work for JS variadic calls;
        /// use this wrapper which creates a proper FunctionInstance.
        /// </summary>
        private static JsValue MakeVariadicFunc(Engine engine, string name, Func<JsValue[], JsValue> fn)
        {
            return new Jint.Runtime.Interop.ClrFunction(engine, name, (thisObj, args) => fn(args));
        }

        /// <summary>
        /// Creates a callable object (mirrors C++ _layerChild).
        /// In C++, the layer object IS a function that can be called with an index
        /// or ADBE name. This creates the same pattern using ClrFunction.
        /// </summary>
        private JsValue MakeLayerChildObj(Engine engine, float frameNo, LottieObject obj, LottieExpression exp)
        {
            // C++ stores LottieObject* in a union with Array<LottieObject*>*.
            // When called by name, it uses data->obj. When called by index from
            // a children-array context, it uses data->data. We split these into
            // separate methods: this one handles the "object" case.
            var fn = new Jint.Runtime.Interop.ClrFunction(engine, "_layerChild", (thisObj, args) =>
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
                SetLayerProps(engine, fn, frameNo, layer, layer.comp, exp);

            return fn;
        }

        /// <summary>
        /// Creates a callable children-list wrapper (for ADBE groups).
        /// Called by 1-based index to access children, or by ADBE name.
        /// Mirrors C++ _layerChild when data->data is Array&lt;LottieObject*&gt;*.
        /// </summary>
        private JsValue MakeChildrenCallable(Engine engine, float frameNo, List<LottieObject> children, LottieExpression exp)
        {
            return new Jint.Runtime.Interop.ClrFunction(engine, "_layerChild", (thisObj, args) =>
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
        //  Singleton (no thread safety, matching C++)
        // ================================================================

        public static LottieExpressions? Instance()
        {
            // Each caller gets its own instance with its own Jint engine.
            // Unlike the C++ upstream (which uses a singleton JerryScript context),
            // Jint's engine accumulates global state across evaluations, so sharing
            // a single instance causes cross-animation pollution.
            return new LottieExpressions();
        }

        public static void Retrieve(LottieExpressions? instance)
        {
            // No-op: each instance is independently owned by its LottieBuilder.
        }

        public void Update(float curTime)
        {
            // time is set per-evaluation in Evaluate(); store for later
            _currentTime = curTime;
        }

        private float _currentTime;

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
                outVal = (byte)TvgMath.Clamp((int)ToFloat(bm_rt), 0, 255);
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

        public bool ResultTextDoc(float frameNo, TextDocument doc, LottieExpression exp)
        {
            var bm_rt = Evaluate(frameNo, exp);
            if (bm_rt == null) return false;

            if (bm_rt is string text)
            {
                doc.text = text;
            }
            return true;
        }

        // ================================================================
        //  Core evaluate -- sets up the JS engine and runs the expression
        // ================================================================

        private object? Evaluate(float frameNo, LottieExpression exp)
        {
            if (exp.disabled && exp.writables.Count == 0) return null;
            if (exp.code == null) return null;

            try
            {
                // Create engine once, reuse across evaluations (matches C++ JerryScript pattern)
                if (_engine == null)
                {
                    _engine = new Engine(cfg => cfg.Strict(false));
                    BuildMath(_engine);
                }

                // Refresh per-frame state (overwrites globals each call, like C++)
                BuildGlobal(_engine, frameNo, exp);
                BuildComp(_engine, exp.comp, frameNo, exp);
                BuildThisComp(_engine, frameNo, exp);
                BuildProperty(_engine, frameNo, exp, "");
                BuildThisLayer(_engine, frameNo, exp);
                BuildThisProperty(_engine, frameNo, exp);

                // Transform context
                if (exp.obj?.type == LottieObject.ObjectType.Transform)
                    BuildTransformGlobal(_engine, frameNo, (LottieTransform)exp.obj);

                // Writable values
                BuildWritables(_engine, exp);

                // time
                _engine.SetValue(EXP_TIME, (double)_currentTime);

                // Execute the expression code
                _engine.Execute(exp.code);

                // Get $bm_rt (the standard After Effects expression result variable)
                var bmRt = _engine.GetValue("$bm_rt");
                if (bmRt == null || bmRt.IsUndefined() || bmRt.IsNull())
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
            if (val.IsUndefined() || val.IsNull()) return null;
            if (val.IsString()) return val.AsString();
            if (val.IsNumber()) return val.AsNumber();
            if (val.IsBoolean()) return val.AsBoolean();

            // Check for our CLR-wrapped objects
            var obj = val.ToObject();
            if (obj is LottiePropertyRef) return obj;
            if (obj is double[] arr) return arr;
            if (obj is Dictionary<string, object?> dict) return dict;

            // For plain JS objects, convert to a dictionary
            if (val.IsObject())
            {
                var jsObj = val.AsObject();
                // Check for _nativeRef (our marker for native property refs)
                var nativeRef = jsObj.Get("_nativeRef");
                if (nativeRef != null && !nativeRef.IsUndefined() && !nativeRef.IsNull())
                {
                    var native = nativeRef.ToObject();
                    if (native is LottiePropertyRef) return native;
                }
                return jsObj;
            }

            return obj;
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
                    // Try index 0 or "value" property
                    var obj = jv.AsObject();
                    var v = obj.Get("0");
                    if (v != null && !v.IsUndefined()) return (float)v.AsNumber();
                    v = obj.Get(EXP_VALUE);
                    if (v != null && !v.IsUndefined()) return (float)v.AsNumber();
                }
            }
            if (val is Jint.Native.JsNumber jn) return (float)jn.AsNumber();
            try { return Convert.ToSingle(val); }
            catch { return 0; }
        }

        private static Point ToPoint2d(object? val)
        {
            if (val == null) return default;
            if (val is Jint.Native.JsValue jv && jv.IsObject())
            {
                var obj = jv.AsObject();
                var v0 = obj.Get("0");
                var v1 = obj.Get("1");
                if (v0 != null && !v0.IsUndefined() && v1 != null && !v1.IsUndefined())
                    return new Point((float)v0.AsNumber(), (float)v1.AsNumber());
                // Fallback: try x/y
                var vx = obj.Get("x");
                var vy = obj.Get("y");
                if (vx != null && !vx.IsUndefined() && vy != null && !vy.IsUndefined())
                    return new Point((float)vx.AsNumber(), (float)vy.AsNumber());
            }
            // Single number -> Point(val, val)
            var f = ToFloat(val);
            return new Point(f, f);
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
                    return new RGB32((int)r.AsNumber(), (int)g.AsNumber(), (int)b.AsNumber());
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
            // C++ does obj.value = obj (self-reference). Jint detects cycles,
            // so create a separate object with the same point data for "value".
            var valObj = MakePoint2d(engine, pt);
            obj.AsObject().Set(EXP_VALUE, valObj);
            return obj;
        }

        private JsValue MakeColorObj(Engine engine, RGB32 rgb)
        {
            var obj = new Jint.Native.JsObject(engine);
            obj.Set("0", new JsNumber(rgb.r));
            obj.Set("1", new JsNumber(rgb.g));
            obj.Set("2", new JsNumber(rgb.b));
            return obj;
        }

        private JsValue MakeNativeRef(Engine engine, LottieProperty property)
        {
            var obj = new Jint.Native.JsObject(engine);
            obj.Set("_nativeRef", JsValue.FromObject(engine, new LottiePropertyRef(property)));
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
                    return new JsNumber(((LottieOpacity)property).Evaluate(frameNo));
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
            return (float)(_random.Next(10000001)) * 0.0000001f;
        }

        private void BuildMath(Engine engine)
        {
            // $bm_mul, $bm_sum, $bm_add, $bm_sub, $bm_div
            engine.SetValue("$bm_mul", new ClrFunction(engine, "$bm_mul", (_, args) => DoMulDiv(engine, args[0], JsToNumber(args[1]))));
            engine.SetValue("$bm_sum", new ClrFunction(engine, "$bm_sum", (_, args) => DoAddSub(engine, args[0], args[1], 1.0f)));
            engine.SetValue("$bm_add", new ClrFunction(engine, "$bm_add", (_, args) => DoAddSub(engine, args[0], args[1], 1.0f)));
            engine.SetValue("$bm_sub", new ClrFunction(engine, "$bm_sub", (_, args) => DoAddSub(engine, args[0], args[1], -1.0f)));
            engine.SetValue("$bm_div", new ClrFunction(engine, "$bm_div", (_, args) => DoMulDiv(engine, args[0], 1.0f / JsToNumber(args[1]))));

            // Non-prefixed versions
            engine.SetValue("mul", new ClrFunction(engine, "mul", (_, args) => DoMulDiv(engine, args[0], JsToNumber(args[1]))));
            engine.SetValue("sum", new ClrFunction(engine, "sum", (_, args) => DoAddSub(engine, args[0], args[1], 1.0f)));
            engine.SetValue("add", new ClrFunction(engine, "add", (_, args) => DoAddSub(engine, args[0], args[1], 1.0f)));
            engine.SetValue("sub", new ClrFunction(engine, "sub", (_, args) => DoAddSub(engine, args[0], args[1], -1.0f)));
            engine.SetValue("div", new ClrFunction(engine, "div", (_, args) => DoMulDiv(engine, args[0], 1.0f / JsToNumber(args[1]))));

            // $bm_mod, mod
            engine.SetValue("$bm_mod", new ClrFunction(engine, "$bm_mod", (_, args) => new JsNumber(JsToNumber(args[0]) % JsToNumber(args[1]))));
            engine.SetValue("mod", new ClrFunction(engine, "mod", (_, args) => new JsNumber(JsToNumber(args[0]) % JsToNumber(args[1]))));

            // clamp
            engine.SetValue("clamp", new ClrFunction(engine, "clamp", (_, args) =>
            {
                var n = JsToNumber(args[0]);
                var l1 = JsToNumber(args[1]);
                var l2 = JsToNumber(args[2]);
                if (n < l1) n = l1;
                if (n > l2) n = l2;
                return new JsNumber(n);
            }));

            // dot, cross, normalize, length
            engine.SetValue("dot", new ClrFunction(engine, "dot", (_, args) =>
            {
                var pa = JsToPoint(args[0]); var pb = JsToPoint(args[1]);
                return new JsNumber(TvgMath.Dot(pa, pb));
            }));
            engine.SetValue("cross", new ClrFunction(engine, "cross", (_, args) =>
            {
                var pa = JsToPoint(args[0]); var pb = JsToPoint(args[1]);
                return new JsNumber(TvgMath.Cross(pa, pb));
            }));
            engine.SetValue("normalize", new ClrFunction(engine, "normalize", (_, args) =>
            {
                var pt = JsToPoint(args[0]);
                var len = TvgMath.PointLength(pt);
                if (len > 0) pt = TvgMath.PointDiv(pt, len);
                return MakePoint2d(engine, pt);
            }));
            engine.SetValue("length", new ClrFunction(engine, "length", (_, args) =>
            {
                if (args[0].IsNumber()) return new JsNumber(MathF.Abs(JsToNumber(args[0])));
                return new JsNumber(TvgMath.PointLength(JsToPoint(args[0])));
            }));

            // random
            engine.SetValue("random", new ClrFunction(engine, "random", (_, args) => new JsNumber(Rand())));

            // degreesToRadians, radiansToDegrees
            engine.SetValue("degreesToRadians", new ClrFunction(engine, "degreesToRadians", (_, args) => new JsNumber(TvgMath.Deg2Rad(JsToNumber(args[0])))));
            engine.SetValue("radiansToDegrees", new ClrFunction(engine, "radiansToDegrees", (_, args) => new JsNumber(TvgMath.Rad2Deg(JsToNumber(args[0])))));

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
                var v0 = obj.Get("0");
                var v1 = obj.Get("1");
                if (v0 != null && !v0.IsUndefined())
                {
                    if (v1 != null && !v1.IsUndefined())
                        return new Point((float)v0.AsNumber(), (float)v1.AsNumber());
                    // 1d value wrapped as object (e.g. from MakeNumber)
                    var f0 = (float)v0.AsNumber();
                    return new Point(f0, f0);
                }
            }
            var f = (float)v.AsNumber();
            return new Point(f, f);
        }

        private static float JsToNumber(JsValue v)
        {
            if (v.IsNumber()) return (float)v.AsNumber();
            if (v.IsObject())
            {
                var obj = v.AsObject();
                var val = obj.Get("0");
                if (val != null && !val.IsUndefined()) return (float)val.AsNumber();
            }
            return 0;
        }

        private JsValue DoAddSub(Engine engine, JsValue a, JsValue b, float addsub)
        {
            // string + string
            if (a.IsString() || b.IsString())
            {
                return new JsString(a.ToString() + b.ToString());
            }

            var n1 = a.IsNumber();
            var n2 = b.IsNumber();

            // 1d + 1d
            if (n1 && n2) return new JsNumber(JsToNumber(a) + addsub * JsToNumber(b));

            var pt = JsToPoint(n1 ? b : a);

            // 2d + 1d
            if (n1 || n2)
            {
                var secondary = n1 ? 0 : 1;
                var val3 = JsToNumber(secondary == 0 ? a : b);
                if (secondary == 0) pt.x = (pt.x * addsub) + val3;
                else pt.x += (addsub * val3);
            }
            else
            {
                // 2d + 2d
                var pt2 = JsToPoint(b);
                pt.x += pt2.x * addsub;
                pt.y += pt2.y * addsub;
            }

            return MakePoint2d(engine, pt);
        }

        private JsValue DoMulDiv(Engine engine, JsValue arg1, float arg2)
        {
            if (arg1.IsNumber()) return new JsNumber(JsToNumber(arg1) * arg2);
            var pt = JsToPoint(arg1);
            return MakePoint2d(engine, TvgMath.PointMul(pt, arg2));
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
                var result = new Point(p1.x + (p2.x - p1.x) * t, p1.y + (p2.y - p1.y) * t);
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

            // comp(name) function
            engine.SetValue("comp", new ClrFunction(engine, "comp", (_, args) =>
            {
                var name = args[0].AsString();
                var id = TvgCompressor.Djb2Encode(name);
                var layer = exp.comp?.root?.LayerById(id);
                if (layer == null) return JsValue.Undefined;
                var obj = new Jint.Native.JsObject(engine);
                SetLayerProps(engine, obj, frameNo, layer, exp.comp!.root!, exp);
                return obj;
            }));
        }

        private void BuildComp(Engine engine, LottieComposition? comp, float frameNo, LottieExpression exp)
        {
            if (comp?.root == null) return;

            // layer(index/name) function on comp -- also set layer and numLayers as properties on it
            var compFunc = new ClrFunction(engine, "comp", (_, args) =>
            {
                var root = comp.root;
                LottieLayer? layer;
                if (args[0].IsNumber())
                    layer = root.LayerByIdx((short)args[0].AsNumber());
                else
                    layer = root.LayerById(TvgCompressor.Djb2Encode(args[0].AsString()));
                if (layer == null) return JsValue.Undefined;
                var obj = new Jint.Native.JsObject(engine);
                SetLayerProps(engine, obj, frameNo, layer, root, exp);
                return obj;
            });
            engine.SetValue("comp", compFunc);

            // Also set layer and numLayers as properties on the comp function object
            var compJsObj = engine.GetValue("comp");
            if (compJsObj.IsObject())
            {
                compJsObj.AsObject().Set("layer", compFunc);
                compJsObj.AsObject().Set("numLayers", new JsNumber(comp.root.children.Count));
            }
        }

        private void BuildThisComp(Engine engine, float frameNo, LottieExpression exp)
        {
            var thisCompObj = new Jint.Native.JsObject(engine);

            // layer(index/name) on thisComp
            var layerComp = exp.layer?.comp ?? exp.comp?.root;
            if (layerComp != null)
            {
                thisCompObj.Set("layer", new ClrFunction(engine, "layer", (_, args) =>
                {
                    LottieLayer? layer;
                    if (args[0].IsNumber())
                        layer = layerComp.LayerByIdx((short)args[0].AsNumber());
                    else
                        layer = layerComp.LayerById(TvgCompressor.Djb2Encode(args[0].AsString()));
                    if (layer == null) return JsValue.Undefined;
                    return MakeLayerChildObj(engine, frameNo, layer, exp);
                }));
                thisCompObj.Set("numLayers", new JsNumber(layerComp.children.Count));
            }

            if (exp.comp != null)
            {
                thisCompObj.Set(EXP_WIDTH, new JsNumber(exp.comp.w));
                thisCompObj.Set(EXP_HEIGHT, new JsNumber(exp.comp.h));
                thisCompObj.Set("duration", new JsNumber(exp.comp.Duration()));
                thisCompObj.Set("frameDuration", new JsNumber(exp.comp.frameRate > 0 ? 1.0f / exp.comp.frameRate : 0));
                if (exp.comp.name != null)
                    thisCompObj.Set(EXP_NAME, new JsString(exp.comp.name));
            }

            engine.SetValue("thisComp", thisCompObj);
        }

        private void BuildThisLayer(Engine engine, float frameNo, LottieExpression exp)
        {
            var thisLayerObj = new Jint.Native.JsObject(engine);
            if (exp.layer != null)
            {
                SetLayerProps(engine, thisLayerObj, frameNo, exp.layer, exp.comp?.root, exp);
            }
            engine.SetValue("thisLayer", thisLayerObj);
        }

        private void BuildThisProperty(Engine engine, float frameNo, LottieExpression exp)
        {
            var thisPropertyObj = new Jint.Native.JsObject(engine);
            if (exp.property != null)
            {
                SetPropertyProps(engine, thisPropertyObj, frameNo, exp);
            }
            engine.SetValue("thisProperty", thisPropertyObj);
        }

        private void BuildTransformGlobal(Engine engine, float frameNo, LottieTransform transform)
        {
            var transformObj = new Jint.Native.JsObject(engine);
            transformObj.Set("anchorPoint", MakePointWithValue(engine, transform.anchor.Evaluate(frameNo)));
            transformObj.Set("position", MakePointWithValue(engine, transform.position.Evaluate(frameNo)));
            transformObj.Set("scale", MakePointWithValue(engine, transform.scale.Evaluate(frameNo)));
            transformObj.Set("rotation", new JsNumber(transform.rotation.Evaluate(frameNo)));
            transformObj.Set("opacity", new JsNumber(transform.opacity.Evaluate(frameNo)));
            engine.SetValue("transform", transformObj);
        }

        private void BuildWritables(Engine engine, LottieExpression exp)
        {
            if (exp.writables.Count == 0) return;
            foreach (var w in exp.writables)
            {
                if (w.var_ != null)
                    engine.SetValue(w.var_, (double)w.val);
            }
        }

        // ================================================================
        //  Build property onto a context (global or thisProperty)
        // ================================================================

        private void BuildProperty(Engine engine, float frameNo, LottieExpression exp, string prefix)
        {
            if (exp.property == null) return;

            // value
            var value = BuildValue(engine, frameNo, exp.property);
            engine.SetValue(EXP_VALUE, value);

            // valueAtTime
            engine.SetValue("valueAtTime", new ClrFunction(engine, "valueAtTime", (_, args) =>
            {
                var time = (float)args[0].AsNumber();
                var fn = exp.comp!.FrameAtTime(time);
                return BuildValue(engine, fn, exp.property);
            }));

            // velocity (static 0)
            engine.SetValue("velocity", new JsNumber(0));

            // velocityAtTime
            engine.SetValue("velocityAtTime", new ClrFunction(engine, "velocityAtTime", (_, args) =>
                DoVelocityAtTime(engine, exp, (float)args[0].AsNumber())));

            // speed (static 0)
            engine.SetValue("speed", new JsNumber(0));

            // speedAtTime
            engine.SetValue("speedAtTime", new ClrFunction(engine, "speedAtTime", (_, args) =>
                DoSpeedAtTime(engine, exp, (float)args[0].AsNumber())));

            // propertyIndex
            engine.SetValue("propertyIndex", new JsNumber(exp.property.ix));

            // wiggle
            engine.SetValue("wiggle", MakeVariadicFunc(engine, "wiggle", args =>
                DoWiggle(engine, frameNo, exp, args)));

            // temporalWiggle
            engine.SetValue("temporalWiggle", MakeVariadicFunc(engine, "temporalWiggle", args =>
                DoTemporalWiggle(engine, frameNo, exp, args)));

            // propertyGroup
            engine.SetValue("propertyGroup", new ClrFunction(engine, "propertyGroup", (_, args) =>
                DoPropertyGroup(engine, frameNo, exp, (int)args[0].AsNumber())));

            // loopIn, loopOut, loopInDuration, loopOutDuration
            engine.SetValue("loopIn", MakeVariadicFunc(engine, "loopIn", args =>
                DoLoopIn(engine, frameNo, exp, args)));
            engine.SetValue("loopOut", MakeVariadicFunc(engine, "loopOut", args =>
                DoLoopOut(engine, frameNo, exp, args)));
            engine.SetValue("loopInDuration", MakeVariadicFunc(engine, "loopInDuration", args =>
                DoLoopInDuration(engine, frameNo, exp, args)));
            engine.SetValue("loopOutDuration", MakeVariadicFunc(engine, "loopOutDuration", args =>
                DoLoopOutDuration(engine, frameNo, exp, args)));

            // key(index)
            engine.SetValue("key", new ClrFunction(engine, "key", (_, args) =>
                DoKey(engine, exp, (int)args[0].AsNumber())));

            // nearestKey(time)
            engine.SetValue("nearestKey", new ClrFunction(engine, "nearestKey", (_, args) =>
            {
                var time = (float)args[0].AsNumber();
                var fn = exp.comp!.FrameAtTime(time);
                var idx = exp.property.Nearest(fn);
                var obj = new Jint.Native.JsObject(engine);
                obj.Set(EXP_INDEX, new JsNumber(idx));
                return obj;
            }));

            // numKeys
            engine.SetValue("numKeys", new JsNumber(exp.property.FrameCnt()));

            // content(name) -- look for named property from layer
            if (exp.layer != null)
            {
                engine.SetValue(EXP_CONTENT, new ClrFunction(engine, EXP_CONTENT, (_, args) =>
                    DoContent(engine, frameNo, exp.layer, args[0])));
                engine.SetValue(EXP_EFFECT, new ClrFunction(engine, EXP_EFFECT, (_, args) =>
                    DoEffect(engine, frameNo, exp, exp.layer, args[0])));
            }

            // Path expansions
            if (exp.property.type == LottieProperty.PropertyType.PathSet)
            {
                BuildPathExpansion(engine, frameNo, exp.property);
            }
        }

        private void SetPropertyProps(Engine engine, Jint.Native.JsObject ctx, float frameNo, LottieExpression exp)
        {
            if (exp.property == null) return;

            ctx.Set(EXP_VALUE, BuildValue(engine, frameNo, exp.property));
            ctx.Set("valueAtTime", new ClrFunction(engine, "valueAtTime", (_, args) =>
            {
                var time = (float)args[0].AsNumber();
                var fn = exp.comp!.FrameAtTime(time);
                return BuildValue(engine, fn, exp.property);
            }));
            ctx.Set("velocity", new JsNumber(0));
            ctx.Set("velocityAtTime", new ClrFunction(engine, "velocityAtTime", (_, args) =>
                DoVelocityAtTime(engine, exp, (float)args[0].AsNumber())));
            ctx.Set("speed", new JsNumber(0));
            ctx.Set("speedAtTime", new ClrFunction(engine, "speedAtTime", (_, args) =>
                DoSpeedAtTime(engine, exp, (float)args[0].AsNumber())));
            ctx.Set("propertyIndex", new JsNumber(exp.property.ix));
            ctx.Set("numKeys", new JsNumber(exp.property.FrameCnt()));

            ctx.Set("wiggle", MakeVariadicFunc(engine, "wiggle", args =>
                DoWiggle(engine, frameNo, exp, args)));
            ctx.Set("temporalWiggle", MakeVariadicFunc(engine, "temporalWiggle", args =>
                DoTemporalWiggle(engine, frameNo, exp, args)));
            ctx.Set("loopIn", MakeVariadicFunc(engine, "loopIn", args =>
                DoLoopIn(engine, frameNo, exp, args)));
            ctx.Set("loopOut", MakeVariadicFunc(engine, "loopOut", args =>
                DoLoopOut(engine, frameNo, exp, args)));
            ctx.Set("loopInDuration", MakeVariadicFunc(engine, "loopInDuration", args =>
                DoLoopInDuration(engine, frameNo, exp, args)));
            ctx.Set("loopOutDuration", MakeVariadicFunc(engine, "loopOutDuration", args =>
                DoLoopOutDuration(engine, frameNo, exp, args)));
            ctx.Set("key", new ClrFunction(engine, "key", (_, args) =>
                DoKey(engine, exp, (int)args[0].AsNumber())));
            ctx.Set("nearestKey", new ClrFunction(engine, "nearestKey", (_, args) =>
            {
                var time = (float)args[0].AsNumber();
                var fn = exp.comp!.FrameAtTime(time);
                var idx = exp.property.Nearest(fn);
                var obj = new Jint.Native.JsObject(engine);
                obj.Set(EXP_INDEX, new JsNumber(idx));
                return obj;
            }));

            // propertyGroup
            ctx.Set("propertyGroup", new ClrFunction(engine, "propertyGroup", (_, args) =>
                DoPropertyGroup(engine, frameNo, exp, (int)args[0].AsNumber())));

            // content(name) -- look for named property from layer
            if (exp.layer != null)
            {
                ctx.Set(EXP_CONTENT, new ClrFunction(engine, EXP_CONTENT, (_, args) =>
                    DoContent(engine, frameNo, exp.layer, args[0])));
                ctx.Set(EXP_EFFECT, new ClrFunction(engine, EXP_EFFECT, (_, args) =>
                    DoEffect(engine, frameNo, exp, exp.layer, args[0])));
            }

            if (exp.property.type == LottieProperty.PropertyType.PathSet)
            {
                BuildPathExpansionOnObj(engine, ctx, frameNo, exp.property);
            }
        }

        // ================================================================
        //  Layer builder
        // ================================================================

        private void SetLayerProps(Engine engine, Jint.Native.Object.ObjectInstance ctx, float frameNo, LottieLayer layer, LottieLayer? comp, LottieExpression exp)
        {
            ctx.Set(EXP_WIDTH, new JsNumber(layer.w));
            ctx.Set(EXP_HEIGHT, new JsNumber(layer.h));
            ctx.Set(EXP_INDEX, new JsNumber(layer.ix));

            ctx.Set("hasParent", layer.parent != null ? JsBoolean.True : JsBoolean.False);
            ctx.Set("inPoint", new JsNumber(layer.inFrame));
            ctx.Set("outPoint", new JsNumber(layer.outFrame));
            ctx.Set("startTime", new JsNumber(exp.comp != null ? exp.comp.TimeAtFrame(layer.startFrame) : 0));
            ctx.Set("hasVideo", JsBoolean.False);
            ctx.Set("hasAudio", JsBoolean.False);
            ctx.Set("enabled", layer.hidden ? JsBoolean.False : JsBoolean.True);
            ctx.Set("audioActive", JsBoolean.False);

            // parent
            if (layer.parent != null)
            {
                var parentObj = new Jint.Native.JsObject(engine);
                parentObj.Set("_nativeRef", JsValue.FromObject(engine, layer.parent));
                ctx.Set("parent", parentObj);
            }

            // toComp(point) -- transform point to composition space
            ctx.Set("toComp", new ClrFunction(engine, "toComp", (_, args) =>
            {
                var pt = JsToPoint(args[0]);
                pt = TvgMath.Transform(pt, layer.cacheMatrix);
                return MakePoint2d(engine, pt);
            }));

            // transform
            if (layer.transform != null)
            {
                var transformObj = new Jint.Native.JsObject(engine);
                transformObj.Set("anchorPoint", MakePointWithValue(engine, layer.transform.anchor.Evaluate(frameNo)));
                transformObj.Set("position", MakePointWithValue(engine, layer.transform.position.Evaluate(frameNo)));
                transformObj.Set("scale", MakePointWithValue(engine, layer.transform.scale.Evaluate(frameNo)));
                transformObj.Set("rotation", new JsNumber(layer.transform.rotation.Evaluate(frameNo)));
                transformObj.Set("opacity", new JsNumber(layer.transform.opacity.Evaluate(frameNo)));
                ctx.Set("transform", transformObj);
            }

            // timeRemap
            var timeRemapObj = new Jint.Native.JsObject(engine);
            timeRemapObj.Set("_nativeRef", JsValue.FromObject(engine, new LottiePropertyRef(layer.timeRemap)));
            ctx.Set("timeRemap", timeRemapObj);

            // content(name)
            ctx.Set(EXP_CONTENT, new ClrFunction(engine, EXP_CONTENT, (_, args) =>
                DoContent(engine, frameNo, layer, args[0])));

            // effect(name/index)
            ctx.Set(EXP_EFFECT, new ClrFunction(engine, EXP_EFFECT, (_, args) =>
                DoEffect(engine, frameNo, exp, layer, args[0])));
        }

        // ================================================================
        //  Content lookup (mirrors C++ _content)
        // ================================================================

        private JsValue DoContent(Engine engine, float frameNo, LottieGroup group, JsValue nameArg)
        {
            var name = nameArg.AsString();
            var id = TvgCompressor.Djb2Encode(name);
            var target = group.Content(id);
            if (target == null) return JsValue.Undefined;

            switch (target.type)
            {
                case LottieObject.ObjectType.Group:
                {
                    var grp = (LottieGroup)target;
                    var obj = new Jint.Native.JsObject(engine);
                    // Attach transform
                    foreach (var child in grp.children)
                    {
                        if (child.type == LottieObject.ObjectType.Transform)
                        {
                            var transformObj = new Jint.Native.JsObject(engine);
                            var tr = (LottieTransform)child;
                            transformObj.Set("anchorPoint", MakePointWithValue(engine, tr.anchor.Evaluate(frameNo)));
                            transformObj.Set("position", MakePointWithValue(engine, tr.position.Evaluate(frameNo)));
                            transformObj.Set("scale", MakePointWithValue(engine, tr.scale.Evaluate(frameNo)));
                            transformObj.Set("rotation", new JsNumber(tr.rotation.Evaluate(frameNo)));
                            transformObj.Set("opacity", new JsNumber(tr.opacity.Evaluate(frameNo)));
                            obj.Set("transform", transformObj);
                            break;
                        }
                    }
                    obj.Set(EXP_CONTENT, new ClrFunction(engine, EXP_CONTENT, (_, args) =>
                        DoContent(engine, frameNo, grp, args[0])));
                    return obj;
                }
                case LottieObject.ObjectType.Path:
                {
                    var path = (LottiePath)target;
                    var obj = new Jint.Native.JsObject(engine);
                    var nativeRef = JsValue.FromObject(engine, new LottiePropertyRef(path.pathset));
                    obj.Set("_nativeRef", nativeRef);
                    // C++ does obj.path = obj (self-reference for content('X').path access).
                    // Jint detects cyclic refs, so point "path" at a wrapper with the same native ref.
                    var pathObj = new Jint.Native.JsObject(engine);
                    pathObj.Set("_nativeRef", nativeRef);
                    obj.Set("path", pathObj);
                    return obj;
                }
                case LottieObject.ObjectType.Polystar:
                {
                    var polystar = (LottiePolyStar)target;
                    var obj = new Jint.Native.JsObject(engine);
                    obj.Set("_nativeRef", JsValue.FromObject(engine, new LottiePropertyRef(polystar.position)));
                    obj.Set("position", MakeNativeRef(engine, polystar.position));
                    obj.Set("innerRadius", new JsNumber(polystar.innerRadius.Evaluate(frameNo)));
                    obj.Set("outerRadius", new JsNumber(polystar.outerRadius.Evaluate(frameNo)));
                    obj.Set("innerRoundness", new JsNumber(polystar.innerRoundness.Evaluate(frameNo)));
                    obj.Set("outerRoundness", new JsNumber(polystar.outerRoundness.Evaluate(frameNo)));
                    obj.Set("rotation", new JsNumber(polystar.rotation.Evaluate(frameNo)));
                    obj.Set("points", new JsNumber(polystar.ptsCnt.Evaluate(frameNo)));
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
                effect = layer.EffectById(TvgCompressor.Djb2Encode(nameArg.AsString()));

            if (effect == null) return JsValue.Undefined;

            // Return a function that looks up effect properties by name
            return new ClrFunction(engine, "effect", (_, args) =>
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
            engine.SetValue("points", new ClrFunction(engine, "points", (_, args) => MakeNativeRef(engine, pathset)));

            // pointOnPath(progress) -- evaluate path at progress and return point
            engine.SetValue("pointOnPath", new ClrFunction(engine, "pointOnPath", (_, args) =>
                DoPointOnPath(engine, frameNo, pathset, args[0])));

            // tangentOnPath(progress) -- evaluate tangent at progress and return normalized direction
            engine.SetValue("tangentOnPath", new ClrFunction(engine, "tangentOnPath", (_, args) =>
                DoTangentOnPath(engine, frameNo, pathset, args[0])));
        }

        private void BuildPathExpansionOnObj(Engine engine, Jint.Native.JsObject ctx, float frameNo, LottieProperty pathset)
        {
            ctx.Set("points", new ClrFunction(engine, "points", (_, args) => MakeNativeRef(engine, pathset)));
            ctx.Set("pointOnPath", new ClrFunction(engine, "pointOnPath", (_, args) =>
                DoPointOnPath(engine, frameNo, pathset, args[0])));
            ctx.Set("tangentOnPath", new ClrFunction(engine, "tangentOnPath", (_, args) =>
                DoTangentOnPath(engine, frameNo, pathset, args[0])));
        }

        private JsValue DoPointOnPath(Engine engine, float frameNo, LottieProperty pathset, JsValue progressArg)
        {
            if (pathset is LottiePathSet ps)
            {
                var progress = JsToNumber(progressArg);
                var renderPath = new RenderPath();
                unsafe { ps.DefaultPath(frameNo, renderPath, null); }
                var pt = RenderPathPoint(renderPath, progress);
                return MakePoint2d(engine, pt);
            }
            return JsValue.Undefined;
        }

        private JsValue DoTangentOnPath(Engine engine, float frameNo, LottieProperty pathset, JsValue progressArg)
        {
            if (pathset is LottiePathSet ps)
            {
                var progress = JsToNumber(progressArg);
                var renderPath = new RenderPath();
                unsafe { ps.DefaultPath(frameNo, renderPath, null); }
                var a = RenderPathPoint(renderPath, Math.Max(0.0f, progress - 0.001f));
                var b = RenderPathPoint(renderPath, Math.Min(1.0f, progress + 0.001f));
                var t = new Point(b.x - a.x, b.y - a.y);
                var len = TvgMath.PointLength(t);
                if (len > 0.0f) { t.x /= len; t.y /= len; }
                return MakePoint2d(engine, t);
            }
            return JsValue.Undefined;
        }

        /// <summary>
        /// Evaluate a point at a given progress along a RenderPath.
        /// Mirrors C++ RenderPath::point(float progress).
        /// </summary>
        private static unsafe Point RenderPathPoint(RenderPath path, float progress)
        {
            if (path.pts.count == 0) return default;
            if (progress <= 0.0f) return path.pts.First();
            if (progress >= 1.0f) return path.pts.Last();

            // Calculate total path length
            var totalLength = 0.0f;
            Point curr = default, start = default;
            var ptsPtr = path.pts.Begin();
            var cmdsPtr = path.cmds.Begin();
            var cmdsEnd = path.cmds.End();
            var p = ptsPtr;
            var c = cmdsPtr;

            // First pass: compute total length
            while (c < cmdsEnd)
            {
                switch (*c)
                {
                    case PathCommand.MoveTo:
                        curr = start = *p++;
                        break;
                    case PathCommand.LineTo:
                        totalLength += TvgMath.LineLength(curr, *p);
                        curr = *p++;
                        break;
                    case PathCommand.CubicTo:
                    {
                        var bz = new Bezier(curr, *p, *(p + 1), *(p + 2));
                        totalLength += bz.Length();
                        curr = *(p + 2);
                        p += 3;
                        break;
                    }
                    case PathCommand.Close:
                        totalLength += TvgMath.LineLength(curr, start);
                        curr = start;
                        break;
                }
                ++c;
            }

            // Second pass: find point at target length
            var targetLen = totalLength * progress;
            var runningLen = 0.0f;
            p = ptsPtr;
            c = cmdsPtr;

            while (c < cmdsEnd)
            {
                switch (*c)
                {
                    case PathCommand.MoveTo:
                        curr = start = *p++;
                        break;
                    case PathCommand.LineTo:
                    {
                        var next = *p;
                        var segLen = TvgMath.LineLength(curr, next);
                        if (runningLen + segLen >= targetLen)
                        {
                            var t = (targetLen - runningLen) / segLen;
                            return new Point(curr.x + (next.x - curr.x) * t, curr.y + (next.y - curr.y) * t);
                        }
                        runningLen += segLen;
                        curr = *p++;
                        break;
                    }
                    case PathCommand.CubicTo:
                    {
                        var bz = new Bezier(curr, *p, *(p + 1), *(p + 2));
                        var segLen = bz.Length();
                        if (runningLen + segLen >= targetLen)
                        {
                            // Match C++: use arc-length fraction directly as bezier parameter
                            return bz.At((targetLen - runningLen) / segLen);
                        }
                        runningLen += segLen;
                        curr = *(p + 2);
                        p += 3;
                        break;
                    }
                    case PathCommand.Close:
                    {
                        var segLen = TvgMath.LineLength(curr, start);
                        if (runningLen + segLen >= targetLen)
                        {
                            var t = (targetLen - runningLen) / segLen;
                            return new Point(curr.x + (start.x - curr.x) * t, curr.y + (start.y - curr.y) * t);
                        }
                        runningLen += segLen;
                        curr = start;
                        break;
                    }
                }
                ++c;
            }

            return path.pts.Last();
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
                var u = fx * fx * fx * (fx * (fx * 6.0f - 15.0f) + 10.0f);

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
                result.x += Perlin1D(repeat, 1000000 + o) * amp;
                result.y += Perlin1D(repeat, 2000000 + o) * amp;
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
                    wiggleTime += (Rand() * 2.0f - 1.0f) * amp * frac;
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
            if (MathF.Abs(elapsed) < MathConstants.FLOAT_EPSILON) return new JsNumber(0);

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
            if (MathF.Abs(elapsed) < MathConstants.FLOAT_EPSILON) return new JsNumber(0);
            var speed = MathF.Sqrt(MathF.Pow(cur.x - prv.x, 2) + MathF.Pow(cur.y - prv.y, 2)) / elapsed;
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
                return new ClrFunction(engine, "propertyGroup", (_, args) =>
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
