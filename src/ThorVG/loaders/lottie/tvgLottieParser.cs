// Ported from ThorVG/src/loaders/lottie/tvgLottieParser.h and tvgLottieParser.cpp
// Uses System.Text.Json instead of RapidJSON.

using System;
using System.Collections.Generic;

namespace ThorVG
{
    public class LottieParser : LookaheadParserHandler
    {
        public LottieComposition? comp;
        public string? dirName;
        public string? slots;
        public bool expressions;

        private struct Context
        {
            public LottieLayer? layer;
            public LottieObject? parent;
        }

        private Context context;

        public LottieParser(string json, string? dirName, bool expressions) : base(json)
        {
            this.dirName = dirName;
            this.expressions = expressions;
        }

        #region Utility helpers

        private static ulong Int2Str(int num)
        {
            return TvgCompressor.Djb2Encode(num.ToString());
        }

        private RGB32 GetColor(string? str)
        {
            var color = new RGB32(0, 0, 0);
            if (str == null) return color;
            if (str.Length != 7 || str[0] != '#') return color;
            color.r = Convert.ToInt32(str.Substring(1, 2), 16);
            color.g = Convert.ToInt32(str.Substring(3, 2), 16);
            color.b = Convert.ToInt32(str.Substring(5, 2), 16);
            return color;
        }

        private FillRule GetFillRule()
        {
            return (GetInt() == 1) ? FillRule.NonZero : FillRule.EvenOdd;
        }

        private MaskMethod GetMaskMethod(bool inversed)
        {
            var mode = GetString();
            if (mode == null || mode.Length == 0) return MaskMethod.None;
            return mode[0] switch
            {
                'a' => inversed ? MaskMethod.InvAlpha : MaskMethod.Add,
                's' => inversed ? MaskMethod.Intersect : MaskMethod.Subtract,
                'i' => inversed ? MaskMethod.Difference : MaskMethod.Intersect,
                'f' => inversed ? MaskMethod.Intersect : MaskMethod.Difference,
                'l' => MaskMethod.Lighten,
                'd' => MaskMethod.Darken,
                _ => MaskMethod.None
            };
        }

        private LottieInterpolator GetInterpolator(string? key, Point @in, Point @out)
        {
            string buf;
            if (key == null)
            {
                buf = $"{@in.x:F2}_{@in.y:F2}_{@out.x:F2}_{@out.y:F2}";
                // Match C++ char buf[20] + snprintf(buf, sizeof(buf), ...) truncation
                if (buf.Length > 19) buf = buf.Substring(0, 19);
                key = buf;
            }

            // get a cached interpolator if it has any.
            // Match C++ strncmp((*p)->key, key, sizeof(buf)) where sizeof(buf) = 20
            const int BufSize = 20;
            if (comp != null)
            {
                LottieInterpolator? cached = null;
                foreach (var p in comp.interpolators)
                {
                    if (string.Compare(p.key, 0, key, 0, BufSize, StringComparison.Ordinal) == 0)
                        cached = p;
                }
                if (cached != null) return cached;
            }

            // new interpolator
            var interpolator = new LottieInterpolator();
            interpolator.Set(key, @in, @out);
            comp?.interpolators.Add(interpolator);
            return interpolator;
        }

        private LottieEffect? GetEffect(int type)
        {
            return type switch
            {
                (int)LottieEffect.EffectType.Custom => new LottieFxCustom(),
                (int)LottieEffect.EffectType.Tint => new LottieFxTint(),
                (int)LottieEffect.EffectType.Fill => new LottieFxFill(),
                (int)LottieEffect.EffectType.Stroke => new LottieFxStroke(),
                (int)LottieEffect.EffectType.Tritone => new LottieFxTritone(),
                (int)LottieEffect.EffectType.DropShadow => new LottieFxDropShadow(),
                (int)LottieEffect.EffectType.GaussianBlur => new LottieFxGaussianBlur(),
                _ => null
            };
        }

        private void GetExpression(string? code, LottieComposition? comp, LottieLayer? layer, LottieObject? obj, LottieProperty? property)
        {
            if (comp != null) comp.expressions = true;
            if (property == null || code == null) return;

            var inst = new LottieExpression();
            inst.code = code;
            inst.comp = comp;
            inst.layer = layer;
            inst.obj = obj;
            inst.property = property;
            property.exp = inst;
        }

        #endregion

        #region getValue overloads

        private void GetInterpolatorPoint(ref Point pt)
        {
            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "x") GetValue(ref pt.x);
                else if (key == "y") GetValue(ref pt.y);
                else Skip();
            }
        }

        private bool GetValue(ref TextDocument doc)
        {
            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                switch (key)
                {
                    case "s": doc.size = GetFloat() * 0.01f; break;
                    case "f": doc.name = GetStringCopy(); break;
                    case "t": doc.text = GetStringCopy(); break;
                    case "j":
                        var val = GetInt();
                        if (val == 1) doc.justify = 1.0f;
                        else if (val == 2) doc.justify = 0.5f;
                        break;
                    case "ca": doc.caps = (byte)GetInt(); break;
                    case "tr": doc.tracking = GetFloat() * 0.1f; break;
                    case "lh": doc.height = GetFloat(); break;
                    case "ls": doc.shift = GetFloat(); break;
                    case "fc": GetValue(ref doc.color); break;
                    case "ps": GetValue(ref doc.bboxPos); break;
                    case "sz": GetValue(ref doc.bboxSize); break;
                    case "sc": GetValue(ref doc.strokeColor); break;
                    case "sw": doc.strokeWidth = GetFloat(); break;
                    case "of": doc.strokeBelow = !GetBool(); break;
                    default: Skip(); break;
                }
            }
            return false;
        }

        private bool GetValue(ref PathSet path)
        {
            var outs = new List<Point>();
            var ins = new List<Point>();
            var pts = new List<Point>();
            bool closed = false;

            var arrayWrapper = (PeekType() == kArrayType);
            if (arrayWrapper) { EnterArray(); NextArrayValue(); }

            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "i") GetValuePoints(ins);
                else if (key == "o") GetValuePoints(outs);
                else if (key == "v") GetValuePoints(pts);
                else if (key == "c") closed = GetBool();
                else Skip();
            }

            if (arrayWrapper) NextArrayValue();

            if (ins.Count == 0 || outs.Count == 0 || pts.Count == 0) return false;
            if (ins.Count != outs.Count || outs.Count != pts.Count) return false;

            // convert path
            var tempCmds = new List<PathCommand>();
            var tempPts = new List<Point>();

            // MoveTo first point
            tempCmds.Add(PathCommand.MoveTo);
            tempPts.Add(pts[0]);

            for (int i = 1; i < pts.Count; ++i)
            {
                tempCmds.Add(PathCommand.CubicTo);
                tempPts.Add(new Point(pts[i - 1].x + outs[i - 1].x, pts[i - 1].y + outs[i - 1].y));
                tempPts.Add(new Point(pts[i].x + ins[i].x, pts[i].y + ins[i].y));
                tempPts.Add(pts[i]);
            }

            if (closed)
            {
                tempCmds.Add(PathCommand.CubicTo);
                var last = pts[^1];
                var first = pts[0];
                tempPts.Add(new Point(last.x + outs[^1].x, last.y + outs[^1].y));
                tempPts.Add(new Point(first.x + ins[0].x, first.y + ins[0].y));
                tempPts.Add(first);
                tempCmds.Add(PathCommand.Close);
            }

            path.pts = tempPts.ToArray();
            path.cmds = tempCmds.ToArray();
            path.ptsCnt = (ushort)tempPts.Count;
            path.cmdsCnt = (ushort)tempCmds.Count;

            return false;
        }

        private bool GetValue(ref ColorStop color)
        {
            if (PeekType() == kArrayType)
            {
                EnterArray();
                if (!NextArrayValue()) return true;
            }

            if (color.input == null)
            {
                color.input = new Array<float>();
            }
            else
            {
                var tmp = color.input.Value;
                tmp.Clear();
                color.input = tmp;
            }

            do
            {
                var tmp = color.input.Value;
                tmp.Push(GetFloat());
                color.input = tmp;
            } while (NextArrayValue());

            return true;
        }

        private void GetValuePoints(List<Point> pts)
        {
            var pt = new Point();
            EnterArray();
            while (NextArrayValue())
            {
                // Don't call EnterArray() here - let GetValue(ref Point)
                // handle entering the inner array. In C++, enterArray()
                // auto-advances to the first element, but our DOM-based
                // EnterArray() does not, so GetValue handles it correctly
                // by checking PeekType() == kArrayType.
                GetValue(ref pt);
                pts.Add(pt);
            }
        }

        private bool GetValue(ref sbyte val)
        {
            if (PeekType() == kArrayType)
            {
                EnterArray();
                if (NextArrayValue()) val = (sbyte)GetInt();
                while (NextArrayValue()) GetInt();
            }
            else
            {
                val = (sbyte)GetFloat();
            }
            return false;
        }

        private bool GetValue(ref byte val)
        {
            if (PeekType() == kArrayType)
            {
                EnterArray();
                if (NextArrayValue()) val = (byte)(GetFloat() * 2.55f);
                while (NextArrayValue()) GetFloat();
            }
            else
            {
                val = (byte)(GetFloat() * 2.55f);
            }
            return false;
        }

        private bool GetValue(ref float val)
        {
            if (PeekType() == kArrayType)
            {
                EnterArray();
                if (NextArrayValue()) val = GetFloat();
                while (NextArrayValue()) GetFloat();
            }
            else
            {
                val = GetFloat();
            }
            return false;
        }

        private bool GetValue(ref Point pt)
        {
            if (PeekType() == kNullType) return false;
            if (PeekType() == kArrayType)
            {
                EnterArray();
                if (!NextArrayValue()) return false;
            }
            pt.x = GetFloat();
            pt.y = GetFloat();
            while (NextArrayValue()) GetFloat(); // drop
            return true;
        }

        private bool GetValue(ref RGB32 color)
        {
            if (PeekType() == kArrayType)
            {
                EnterArray();
                if (!NextArrayValue()) return false;
            }
            color.r = LottieDataHelper.REMAP255(GetFloat());
            color.g = LottieDataHelper.REMAP255(GetFloat());
            color.b = LottieDataHelper.REMAP255(GetFloat());
            while (NextArrayValue()) GetFloat(); // drop
            return true;
        }

        #endregion

        #region parseKeyFrame / parsePropertyInternal / parseProperty for each property type

        // Helper: parse tangent for vector frames (LottieVector uses LottieVectorFrame<Point>)
        private bool ParseTangent(string key, LottieVectorFrame<Point> value)
        {
            if (key == "ti") { var pt = new Point(); GetValue(ref pt); value.inTangent = pt; value.hasTangent = true; return true; }
            if (key == "to") { var pt = new Point(); GetValue(ref pt); value.outTangent = pt; value.hasTangent = true; return true; }
            return false;
        }

        // parseTangent for scalar frames always returns false
        private bool ParseTangent<T>(string key, LottieScalarFrame<T> value) => false;

        // --- LottieFloat ---
        private void ParseKeyFrame(LottieFloat prop)
        {
            Point inTangent = default, outTangent = default;
            string? interpolatorKey = null;
            var frame = prop.NewFrame();
            var interpolator = false;

            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                switch (key)
                {
                    case "i": interpolator = true; GetInterpolatorPoint(ref inTangent); break;
                    case "o": GetInterpolatorPoint(ref outTangent); break;
                    case "n":
                        if (PeekType() == kStringType) interpolatorKey = GetString();
                        else { EnterArray(); while (NextArrayValue()) { if (interpolatorKey == null) interpolatorKey = GetString(); else Skip(); } }
                        break;
                    case "t": frame.no = GetFloat(); break;
                    case "s": GetValue(ref frame.value); break;
                    case "e": var frame2 = prop.NextFrame(); GetValue(ref frame2.value); break;
                    case "h": frame.hold = GetInt() != 0; break;
                    default: Skip(); break;
                }
            }
            if (interpolator) frame.interpolator = GetInterpolator(interpolatorKey, inTangent, outTangent);
        }

        private void ParsePropertyInternal(LottieFloat prop)
        {
            if (PeekType() == kNumberType) { GetValue(ref prop.value); }
            else
            {
                EnterArray();
                while (NextArrayValue())
                {
                    if (PeekType() == kObjectType) ParseKeyFrame(prop);
                    else if (GetValue(ref prop.value)) break;
                }
                prop.Prepare();
            }
        }

        private void ParseProperty(LottieFloat prop, LottieObject? obj = null)
        {
            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "k") ParsePropertyInternal(prop);
                else if (ParseCommon(obj, prop, key)) continue;
                else Skip();
            }
        }

        // --- LottieInteger ---
        private void ParseKeyFrame(LottieInteger prop)
        {
            Point inTangent = default, outTangent = default;
            string? interpolatorKey = null;
            var frame = prop.NewFrame();
            var interpolator = false;

            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                switch (key)
                {
                    case "i": interpolator = true; GetInterpolatorPoint(ref inTangent); break;
                    case "o": GetInterpolatorPoint(ref outTangent); break;
                    case "n":
                        if (PeekType() == kStringType) interpolatorKey = GetString();
                        else { EnterArray(); while (NextArrayValue()) { if (interpolatorKey == null) interpolatorKey = GetString(); else Skip(); } }
                        break;
                    case "t": frame.no = GetFloat(); break;
                    case "s": GetValue(ref frame.value); break;
                    case "e": var frame2 = prop.NextFrame(); GetValue(ref frame2.value); break;
                    case "h": frame.hold = GetInt() != 0; break;
                    default: Skip(); break;
                }
            }
            if (interpolator) frame.interpolator = GetInterpolator(interpolatorKey, inTangent, outTangent);
        }

        private void ParsePropertyInternal(LottieInteger prop)
        {
            if (PeekType() == kNumberType) { GetValue(ref prop.value); }
            else
            {
                EnterArray();
                while (NextArrayValue())
                {
                    if (PeekType() == kObjectType) ParseKeyFrame(prop);
                    else if (GetValue(ref prop.value)) break;
                }
                prop.Prepare();
            }
        }

        private void ParseProperty(LottieInteger prop, LottieObject? obj = null)
        {
            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "k") ParsePropertyInternal(prop);
                else if (ParseCommon(obj, prop, key)) continue;
                else Skip();
            }
        }

        // --- LottieScalar ---
        private void ParseKeyFrame(LottieScalar prop)
        {
            Point inTangent = default, outTangent = default;
            string? interpolatorKey = null;
            var frame = prop.NewFrame();
            var interpolator = false;

            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                switch (key)
                {
                    case "i": interpolator = true; GetInterpolatorPoint(ref inTangent); break;
                    case "o": GetInterpolatorPoint(ref outTangent); break;
                    case "n":
                        if (PeekType() == kStringType) interpolatorKey = GetString();
                        else { EnterArray(); while (NextArrayValue()) { if (interpolatorKey == null) interpolatorKey = GetString(); else Skip(); } }
                        break;
                    case "t": frame.no = GetFloat(); break;
                    case "s": GetValue(ref frame.value); break;
                    case "e": var frame2 = prop.NextFrame(); GetValue(ref frame2.value); break;
                    case "h": frame.hold = GetInt() != 0; break;
                    default: Skip(); break;
                }
            }
            if (interpolator) frame.interpolator = GetInterpolator(interpolatorKey, inTangent, outTangent);
        }

        private void ParsePropertyInternal(LottieScalar prop)
        {
            if (PeekType() == kNumberType) { GetValue(ref prop.value); }
            else
            {
                EnterArray();
                while (NextArrayValue())
                {
                    if (PeekType() == kObjectType) ParseKeyFrame(prop);
                    else if (GetValue(ref prop.value)) break;
                }
                prop.Prepare();
            }
        }

        private void ParseProperty(LottieScalar prop, LottieObject? obj = null)
        {
            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "k") ParsePropertyInternal(prop);
                else if (ParseCommon(obj, prop, key)) continue;
                else Skip();
            }
        }

        // --- LottieVector ---
        private void ParseKeyFrame(LottieVector prop)
        {
            Point inTangent = default, outTangent = default;
            string? interpolatorKey = null;
            var frame = prop.NewFrame();
            var interpolator = false;

            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "i") { interpolator = true; GetInterpolatorPoint(ref inTangent); }
                else if (key == "o") { GetInterpolatorPoint(ref outTangent); }
                else if (key == "n")
                {
                    if (PeekType() == kStringType) interpolatorKey = GetString();
                    else { EnterArray(); while (NextArrayValue()) { if (interpolatorKey == null) interpolatorKey = GetString(); else Skip(); } }
                }
                else if (key == "t") { frame.no = GetFloat(); }
                else if (key == "s") { GetValue(ref frame.value); }
                else if (key == "e") { var frame2 = prop.NextFrame(); GetValue(ref frame2.value); }
                else if (ParseTangent(key, frame)) { continue; }
                else if (key == "h") { frame.hold = GetInt() != 0; }
                else Skip();
            }
            if (interpolator) frame.interpolator = GetInterpolator(interpolatorKey, inTangent, outTangent);
        }

        private void ParsePropertyInternal(LottieVector prop)
        {
            if (PeekType() == kNumberType) { GetValue(ref prop.value); }
            else
            {
                EnterArray();
                while (NextArrayValue())
                {
                    if (PeekType() == kObjectType) ParseKeyFrame(prop);
                    else if (GetValue(ref prop.value)) break;
                }
                prop.Prepare();
            }
        }

        private void ParseProperty(LottieVector prop, LottieObject? obj = null)
        {
            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "k") ParsePropertyInternal(prop);
                else if (ParseCommon(obj, prop, key)) continue;
                else Skip();
            }
        }

        // --- LottieColor ---
        private void ParseKeyFrame(LottieColor prop)
        {
            Point inTangent = default, outTangent = default;
            string? interpolatorKey = null;
            var frame = prop.NewFrame();
            var interpolator = false;

            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                switch (key)
                {
                    case "i": interpolator = true; GetInterpolatorPoint(ref inTangent); break;
                    case "o": GetInterpolatorPoint(ref outTangent); break;
                    case "n":
                        if (PeekType() == kStringType) interpolatorKey = GetString();
                        else { EnterArray(); while (NextArrayValue()) { if (interpolatorKey == null) interpolatorKey = GetString(); else Skip(); } }
                        break;
                    case "t": frame.no = GetFloat(); break;
                    case "s": GetValue(ref frame.value); break;
                    case "e": var frame2 = prop.NextFrame(); GetValue(ref frame2.value); break;
                    case "h": frame.hold = GetInt() != 0; break;
                    default: Skip(); break;
                }
            }
            if (interpolator) frame.interpolator = GetInterpolator(interpolatorKey, inTangent, outTangent);
        }

        private void ParsePropertyInternal(LottieColor prop)
        {
            if (PeekType() == kNumberType) { GetValue(ref prop.value); }
            else
            {
                EnterArray();
                while (NextArrayValue())
                {
                    if (PeekType() == kObjectType) ParseKeyFrame(prop);
                    else if (GetValue(ref prop.value)) break;
                }
                prop.Prepare();
            }
        }

        private void ParseProperty(LottieColor prop, LottieObject? obj = null)
        {
            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "k") ParsePropertyInternal(prop);
                else if (ParseCommon(obj, prop, key)) continue;
                else Skip();
            }
        }

        // --- LottieOpacity ---
        private void ParseKeyFrame(LottieOpacity prop)
        {
            Point inTangent = default, outTangent = default;
            string? interpolatorKey = null;
            var frame = prop.NewFrame();
            var interpolator = false;

            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                switch (key)
                {
                    case "i": interpolator = true; GetInterpolatorPoint(ref inTangent); break;
                    case "o": GetInterpolatorPoint(ref outTangent); break;
                    case "n":
                        if (PeekType() == kStringType) interpolatorKey = GetString();
                        else { EnterArray(); while (NextArrayValue()) { if (interpolatorKey == null) interpolatorKey = GetString(); else Skip(); } }
                        break;
                    case "t": frame.no = GetFloat(); break;
                    case "s": GetValue(ref frame.value); break;
                    case "e": var frame2 = prop.NextFrame(); GetValue(ref frame2.value); break;
                    case "h": frame.hold = GetInt() != 0; break;
                    default: Skip(); break;
                }
            }
            if (interpolator) frame.interpolator = GetInterpolator(interpolatorKey, inTangent, outTangent);
        }

        private void ParsePropertyInternal(LottieOpacity prop)
        {
            if (PeekType() == kNumberType) { GetValue(ref prop.value); }
            else
            {
                EnterArray();
                while (NextArrayValue())
                {
                    if (PeekType() == kObjectType) ParseKeyFrame(prop);
                    else if (GetValue(ref prop.value)) break;
                }
                prop.Prepare();
            }
        }

        private void ParseProperty(LottieOpacity prop, LottieObject? obj = null)
        {
            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "k") ParsePropertyInternal(prop);
                else if (ParseCommon(obj, prop, key)) continue;
                else Skip();
            }
        }

        // --- LottieColorStop ---
        private void ParseKeyFrame(LottieColorStop prop)
        {
            Point inTangent = default, outTangent = default;
            string? interpolatorKey = null;
            var frame = prop.NewFrame();
            var interpolator = false;

            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                switch (key)
                {
                    case "i": interpolator = true; GetInterpolatorPoint(ref inTangent); break;
                    case "o": GetInterpolatorPoint(ref outTangent); break;
                    case "n":
                        if (PeekType() == kStringType) interpolatorKey = GetString();
                        else { EnterArray(); while (NextArrayValue()) { if (interpolatorKey == null) interpolatorKey = GetString(); else Skip(); } }
                        break;
                    case "t": frame.no = GetFloat(); break;
                    case "s": GetValue(ref frame.value); break;
                    case "e": var frame2 = prop.NextFrame(); GetValue(ref frame2.value); break;
                    case "h": frame.hold = GetInt() != 0; break;
                    default: Skip(); break;
                }
            }
            if (interpolator) frame.interpolator = GetInterpolator(interpolatorKey, inTangent, outTangent);
        }

        private void ParseProperty(LottieColorStop prop, LottieObject? obj = null)
        {
            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "k") ParsePropertyInternal(prop);
                else if (ParseCommon(obj, prop, key)) continue;
                else Skip();
            }
        }

        private void ParsePropertyInternal(LottieColorStop prop)
        {
            if (PeekType() == kNumberType) { GetValue(ref prop.value); }
            else
            {
                EnterArray();
                while (NextArrayValue())
                {
                    if (PeekType() == kObjectType) ParseKeyFrame(prop);
                    else if (GetValue(ref prop.value)) break;
                }
                prop.Prepare();
            }
        }

        // --- LottieTextDoc ---
        private void ParseKeyFrame(LottieTextDoc prop)
        {
            Point inTangent = default, outTangent = default;
            string? interpolatorKey = null;
            var frame = prop.NewFrame();
            var interpolator = false;

            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                switch (key)
                {
                    case "i": interpolator = true; GetInterpolatorPoint(ref inTangent); break;
                    case "o": GetInterpolatorPoint(ref outTangent); break;
                    case "n":
                        if (PeekType() == kStringType) interpolatorKey = GetString();
                        else { EnterArray(); while (NextArrayValue()) { if (interpolatorKey == null) interpolatorKey = GetString(); else Skip(); } }
                        break;
                    case "t": frame.no = GetFloat(); break;
                    case "s": GetValue(ref frame.value); break;
                    case "e": var frame2 = prop.NextFrame(); GetValue(ref frame2.value); break;
                    case "h": frame.hold = GetInt() != 0; break;
                    default: Skip(); break;
                }
            }
            if (interpolator) frame.interpolator = GetInterpolator(interpolatorKey, inTangent, outTangent);
        }

        private void ParsePropertyInternal(LottieTextDoc prop)
        {
            EnterArray();
            while (NextArrayValue())
            {
                if (PeekType() == kObjectType) ParseKeyFrame(prop);
                else Skip();
            }
            prop.Prepare();
        }

        private void ParseProperty(LottieTextDoc prop, LottieObject? obj = null)
        {
            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "k") ParsePropertyInternal(prop);
                else if (ParseCommon(obj, prop, key)) continue;
                else Skip();
            }
        }

        // --- ParseSlotProperty ---
        private void ParseSlotProperty(LottieFloat prop)
        {
            string? key; while ((key = NextObjectKey()) != null) { if (key == "p") ParseProperty(prop); else Skip(); }
        }
        private void ParseSlotProperty(LottieScalar prop)
        {
            string? key; while ((key = NextObjectKey()) != null) { if (key == "p") ParseProperty(prop); else Skip(); }
        }
        private void ParseSlotProperty(LottieVector prop)
        {
            string? key; while ((key = NextObjectKey()) != null) { if (key == "p") ParseProperty(prop); else Skip(); }
        }
        private void ParseSlotProperty(LottieOpacity prop)
        {
            string? key; while ((key = NextObjectKey()) != null) { if (key == "p") ParseProperty(prop); else Skip(); }
        }
        private void ParseSlotProperty(LottieColor prop)
        {
            string? key; while ((key = NextObjectKey()) != null) { if (key == "p") ParseProperty(prop); else Skip(); }
        }
        private void ParseSlotProperty(LottieTextDoc prop)
        {
            string? key; while ((key = NextObjectKey()) != null) { if (key == "p") ParseProperty(prop); else Skip(); }
        }

        #endregion

        #region Common parse helpers

        private void RegisterSlot(LottieObject obj, string? sid, LottieProperty prop)
        {
            if (sid == null || comp == null) return;
            var val = TvgCompressor.Djb2Encode(sid);

            foreach (var s in comp.slots)
            {
                if (s.sid != val) continue;
                s.pairs.Add(new LottieSlot.Pair { obj = obj });
                prop.sid = val;
                return;
            }
            comp.slots.Add(new LottieSlot(context.layer, context.parent, val, obj, prop.type));
            prop.sid = val;
        }

        private bool ParseCommon(LottieObject? obj, string key)
        {
            if (obj == null) return false;
            if (key == "nm") { obj.id = TvgCompressor.Djb2Encode(GetString()); return true; }
            if (key == "hd") { obj.hidden = GetBool(); return true; }
            return false;
        }

        private bool ParseCommon(LottieObject? obj, LottieProperty prop, string key)
        {
            if (key == "ix") { prop.ix = (byte)GetInt(); return true; }
            if (key == "x" && expressions) { GetExpression(GetStringCopy(), comp, context.layer, context.parent, prop); return true; }
            if (key == "sid") { RegisterSlot(obj!, GetString(), prop); return true; }
            return false;
        }

        private bool ParseDirection(LottieShape? shape, string key)
        {
            if (shape == null) return false;
            if (key == "d") { if (GetInt() == 3) shape.clockwise = false; return true; }
            return false;
        }

        #endregion

        #region Shape parsers

        private LottieRect ParseRect()
        {
            var rect = new LottieRect();
            context.parent = rect;

            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (ParseCommon(rect, key)) continue;
                else if (key == "s") ParseProperty(rect.size);
                else if (key == "p") ParseProperty(rect.position);
                else if (key == "r") ParseProperty(rect.radius);
                else if (ParseDirection(rect, key)) continue;
                else Skip();
            }
            return rect;
        }

        private LottieEllipse ParseEllipse()
        {
            var ellipse = new LottieEllipse();
            context.parent = ellipse;

            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (ParseCommon(ellipse, key)) continue;
                else if (key == "p") ParseProperty(ellipse.position);
                else if (key == "s") ParseProperty(ellipse.size);
                else if (ParseDirection(ellipse, key)) continue;
                else Skip();
            }
            return ellipse;
        }

        private LottieTransform ParseTransform(bool ddd = false)
        {
            var transform = new LottieTransform();
            context.parent = transform;

            if (ddd)
            {
                transform.rotationEx = new LottieTransform.RotationEx();
                TvgCommon.TVGLOG("LOTTIE", "3D transform(ddd) is not totally compatible.");
            }

            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (ParseCommon(transform, key)) continue;
                else if (key == "p")
                {
                    EnterObject();
                    string? k2;
                    while ((k2 = NextObjectKey()) != null)
                    {
                        if (k2 == "k") ParsePropertyInternal(transform.position);
                        else if (k2 == "x")
                        {
                            if (PeekType() == kStringType)
                            {
                                if (expressions) GetExpression(GetStringCopy(), comp, context.layer, context.parent, transform.position);
                                else Skip();
                            }
                            else ParseProperty(transform.GetSeparateCoord().x);
                        }
                        else if (k2 == "y") ParseProperty(transform.GetSeparateCoord().y);
                        else if (ParseCommon(transform, transform.position, k2)) continue;
                        else Skip();
                    }
                }
                else if (key == "a") ParseProperty(transform.anchor);
                else if (key == "s") ParseProperty(transform.scale, transform);
                else if (key == "r") ParseProperty(transform.rotation, transform);
                else if (key == "o") ParseProperty(transform.opacity, transform);
                else if (transform.rotationEx != null && key == "rx") ParseProperty(transform.rotationEx.x);
                else if (transform.rotationEx != null && key == "ry") ParseProperty(transform.rotationEx.y);
                else if (transform.rotationEx != null && key == "rz") ParseProperty(transform.rotation);
                else if (key == "sk") ParseProperty(transform.skewAngle, transform);
                else if (key == "sa") ParseProperty(transform.skewAxis, transform);
                else Skip();
            }
            return transform;
        }

        private LottieSolidFill ParseSolidFill()
        {
            var fill = new LottieSolidFill();
            context.parent = fill;

            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (ParseCommon(fill, key)) continue;
                else if (key == "c") ParseProperty(fill.color, fill);
                else if (key == "o") ParseProperty(fill.opacity, fill);
                else if (key == "fillEnabled") fill.hidden |= !GetBool();
                else if (key == "r") fill.rule = (GetInt() == 1) ? FillRule.NonZero : FillRule.EvenOdd;
                else Skip();
            }
            return fill;
        }

        private void ParseStrokeDash(LottieStroke stroke)
        {
            EnterArray();
            while (NextArrayValue())
            {
                EnterObject();
                string? style = null;
                string? key;
                while ((key = NextObjectKey()) != null)
                {
                    if (key == "n") style = GetString();
                    else if (key == "v")
                    {
                        if (style != null && style == "o") ParseProperty(stroke.DashOffset());
                        else ParseProperty(stroke.DashValue());
                    }
                    else Skip();
                }
            }
        }

        private LottieSolidStroke ParseSolidStroke()
        {
            var stroke = new LottieSolidStroke();
            context.parent = stroke;

            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (ParseCommon(stroke, key)) continue;
                else if (key == "c") ParseProperty(stroke.color, stroke);
                else if (key == "o") ParseProperty(stroke.opacity, stroke);
                else if (key == "w") ParseProperty(stroke.stroke.width, stroke);
                else if (key == "lc") stroke.stroke.cap = (StrokeCap)(GetInt() - 1);
                else if (key == "lj") stroke.stroke.join = (StrokeJoin)(GetInt() - 1);
                else if (key == "ml") stroke.stroke.miterLimit = GetFloat();
                else if (key == "fillEnabled") stroke.hidden |= !GetBool();
                else if (key == "d") ParseStrokeDash(stroke.stroke);
                else Skip();
            }
            return stroke;
        }

        private void GetPathSet(LottiePath? obj, LottiePathSet path)
        {
            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "k")
                {
                    if (PeekType() == kArrayType)
                    {
                        EnterArray();
                        while (NextArrayValue()) ParseKeyFrame(path);
                    }
                    else
                    {
                        GetValue(ref path.value);
                    }
                }
                else if (obj != null && ParseCommon(obj, path, key)) continue;
                else Skip();
            }
        }

        // --- LottiePathSet keyframe parsing ---
        private void ParseKeyFrame(LottiePathSet prop)
        {
            Point inTangent = default, outTangent = default;
            string? interpolatorKey = null;
            var frame = prop.NewFrame();
            var interpolator = false;

            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                switch (key)
                {
                    case "i": interpolator = true; GetInterpolatorPoint(ref inTangent); break;
                    case "o": GetInterpolatorPoint(ref outTangent); break;
                    case "n":
                        if (PeekType() == kStringType) interpolatorKey = GetString();
                        else { EnterArray(); while (NextArrayValue()) { if (interpolatorKey == null) interpolatorKey = GetString(); else Skip(); } }
                        break;
                    case "t": frame.no = GetFloat(); break;
                    case "s": GetValue(ref frame.value); break;
                    case "e": var frame2 = prop.NextFrame(); GetValue(ref frame2.value); break;
                    case "h": frame.hold = GetInt() != 0; break;
                    default: Skip(); break;
                }
            }
            if (interpolator) frame.interpolator = GetInterpolator(interpolatorKey, inTangent, outTangent);
        }

        private LottiePath ParsePath()
        {
            var path = new LottiePath();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (ParseCommon(path, key)) continue;
                else if (key == "ks") GetPathSet(path, path.pathset);
                else if (ParseDirection(path, key)) continue;
                else Skip();
            }
            return path;
        }

        private LottiePolyStar ParsePolyStar()
        {
            var star = new LottiePolyStar();
            context.parent = star;

            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (ParseCommon(star, key)) continue;
                else if (key == "p") ParseProperty(star.position);
                else if (key == "pt") ParseProperty(star.ptsCnt);
                else if (key == "ir") ParseProperty(star.innerRadius);
                else if (key == "is") ParseProperty(star.innerRoundness);
                else if (key == "or") ParseProperty(star.outerRadius);
                else if (key == "os") ParseProperty(star.outerRoundness);
                else if (key == "r") ParseProperty(star.rotation);
                else if (key == "sy") star.starType = (LottiePolyStar.StarType)GetInt();
                else if (ParseDirection(star, key)) continue;
                else Skip();
            }
            return star;
        }

        private LottieRoundedCorner ParseRoundedCorner()
        {
            var corner = new LottieRoundedCorner();
            context.parent = corner;

            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (ParseCommon(corner, key)) continue;
                else if (key == "r") ParseProperty(corner.radius);
                else Skip();
            }
            return corner;
        }

        private void ParseColorStop(LottieGradient gradient)
        {
            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "p") gradient.colorStops.count = (ushort)GetInt();
                else if (key == "k") ParseProperty(gradient.colorStops, gradient);
                else if (ParseCommon(gradient, gradient.colorStops, key)) continue;
                else Skip();
            }
        }

        private void ParseGradient(LottieGradient gradient, string key)
        {
            if (key == "t") gradient.gradientId = (byte)GetInt();
            else if (key == "o") ParseProperty(gradient.opacity, gradient);
            else if (key == "g") ParseColorStop(gradient);
            else if (key == "s") ParseProperty(gradient.start, gradient);
            else if (key == "e") ParseProperty(gradient.end, gradient);
            else if (key == "h") ParseProperty(gradient.height, gradient);
            else if (key == "a") ParseProperty(gradient.angle, gradient);
            else Skip();
        }

        private LottieGradientFill ParseGradientFill()
        {
            var fill = new LottieGradientFill();
            context.parent = fill;

            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (ParseCommon(fill, key)) continue;
                else if (key == "r") fill.rule = (GetInt() == 1) ? FillRule.NonZero : FillRule.EvenOdd;
                else ParseGradient(fill, key);
            }
            fill.Prepare();
            return fill;
        }

        private LottieGradientStroke ParseGradientStroke()
        {
            var stroke = new LottieGradientStroke();
            context.parent = stroke;

            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (ParseCommon(stroke, key)) continue;
                else if (key == "lc") stroke.stroke.cap = (StrokeCap)(GetInt() - 1);
                else if (key == "lj") stroke.stroke.join = (StrokeJoin)(GetInt() - 1);
                else if (key == "ml") stroke.stroke.miterLimit = GetFloat();
                else if (key == "w") ParseProperty(stroke.stroke.width);
                else if (key == "d") ParseStrokeDash(stroke.stroke);
                else ParseGradient(stroke, key);
            }
            stroke.Prepare();
            return stroke;
        }

        private LottieTrimpath ParseTrimpath()
        {
            var trim = new LottieTrimpath();
            context.parent = trim;

            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (ParseCommon(trim, key)) continue;
                else if (key == "s") ParseProperty(trim.start);
                else if (key == "e") ParseProperty(trim.end);
                else if (key == "o") ParseProperty(trim.offset);
                else if (key == "m") trim.trimType = (LottieTrimpath.TrimType)(byte)GetInt();
                else Skip();
            }
            return trim;
        }

        private LottieRepeater ParseRepeater()
        {
            var repeater = new LottieRepeater();
            context.parent = repeater;

            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (ParseCommon(repeater, key)) continue;
                else if (key == "c") ParseProperty(repeater.copies);
                else if (key == "o") ParseProperty(repeater.offset);
                else if (key == "m") repeater.inorder = GetInt() == 2;
                else if (key == "tr")
                {
                    EnterObject();
                    string? k2;
                    while ((k2 = NextObjectKey()) != null)
                    {
                        if (k2 == "a") ParseProperty(repeater.anchor);
                        else if (k2 == "p") ParseProperty(repeater.position);
                        else if (k2 == "r") ParseProperty(repeater.rotation);
                        else if (k2 == "s") ParseProperty(repeater.scale);
                        else if (k2 == "so") ParseProperty(repeater.startOpacity);
                        else if (k2 == "eo") ParseProperty(repeater.endOpacity);
                        else Skip();
                    }
                }
                else Skip();
            }
            return repeater;
        }

        private LottieOffsetPath ParseOffsetPath()
        {
            var offsetPath = new LottieOffsetPath();
            context.parent = offsetPath;

            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (ParseCommon(offsetPath, key)) continue;
                else if (key == "a") ParseProperty(offsetPath.offset);
                else if (key == "lj") offsetPath.join = (StrokeJoin)(GetInt() - 1);
                else if (key == "ml") ParseProperty(offsetPath.miterLimit);
                else Skip();
            }
            return offsetPath;
        }

        private LottiePuckerBloat ParsePuckerBloat()
        {
            var puckerBloat = new LottiePuckerBloat();
            context.parent = puckerBloat;

            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (ParseCommon(puckerBloat, key)) continue;
                else if (key == "a") ParseProperty(puckerBloat.amount);
                else Skip();
            }
            return puckerBloat;
        }

        #endregion

        #region Object/asset parsing

        private LottieObject? ParseObject(string type)
        {
            switch (type)
            {
                case "gr": return ParseGroup();
                case "rc": return ParseRect();
                case "el": return ParseEllipse();
                case "tr": return ParseTransform();
                case "fl": return ParseSolidFill();
                case "st": return ParseSolidStroke();
                case "sh": return ParsePath();
                case "sr": return ParsePolyStar();
                case "rd": return ParseRoundedCorner();
                case "gf": return ParseGradientFill();
                case "gs": return ParseGradientStroke();
                case "tm": return ParseTrimpath();
                case "rp": return ParseRepeater();
                case "pb": return ParsePuckerBloat();
                case "op": return ParseOffsetPath();
                case "mm": TvgCommon.TVGLOG("LOTTIE", "MergePath(mm) is not supported yet"); break;
                case "tw": TvgCommon.TVGLOG("LOTTIE", "Twist(tw) is not supported yet"); break;
                case "zz": TvgCommon.TVGLOG("LOTTIE", "ZigZag(zz) is not supported yet"); break;
            }
            return null;
        }

        private void ParseObject(List<LottieObject> parent)
        {
            // DOM-based lookahead: find "ty" before entering the object,
            // so sub-parsers see all keys. This mirrors C++ captureType().
            var type = PeekStringProperty("ty");

            EnterObject();

            // If lookahead didn't find it (shouldn't happen in well-formed Lottie),
            // fall back to scanning for "ty" as the first key.
            if (type == null)
            {
                var key = NextObjectKey();
                if (key != null)
                {
                    if (key == "ty") type = GetString();
                    else Skip();
                }
            }

            if (type != null)
            {
                var child = ParseObject(type);
                if (child != null)
                {
                    if (child.hidden) { /* discard */ }
                    else parent.Add(child);
                }
            }

            // Skip remaining keys
            while (NextObjectKey() != null) Skip();
        }

        private void ParseImage(LottieImage image, string? data, string? subPath, bool embedded, float width, float height)
        {
            if (data == null || data.Length == 0) return;

            var external = false;

            if (embedded && data.StartsWith("data:"))
            {
                var mimeTypeStart = 11;
                var semicolonIdx = data.IndexOf(';', mimeTypeStart);
                if (semicolonIdx > 0) image.bitmap.mimeType = data.Substring(mimeTypeStart, semicolonIdx - mimeTypeStart);

                var commaIdx = data.IndexOf(',');
                if (commaIdx > 0)
                {
                    var b64Str = data.Substring(commaIdx + 1);
                    image.bitmap.b64Data = TvgCompressor.B64Decode(b64Str);
                    image.bitmap.size = (uint)image.bitmap.b64Data.Length;
                }
            }
            //remote image resource (https:// or http://)
            else if (data.StartsWith("https://") || data.StartsWith("http://"))
            {
                image.bitmap.path = data;
            }
            //external image resource
            else
            {
                image.bitmap.path = $"{dirName}/{subPath ?? ""}{data}";
                external = true;
            }

            image.bitmap.width = width;
            image.bitmap.height = height;
            image.Prepare(external);
        }

        private LottieObject? ParseAsset()
        {
            EnterObject();

            LottieObject? obj = null;
            ulong id = 0;
            string? sid = null;
            string? data = null;
            string? subPath = null;
            float width = 0, height = 0;
            bool embedded = false;

            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "id")
                {
                    if (PeekType() == kStringType) id = TvgCompressor.Djb2Encode(GetString());
                    else id = Int2Str(GetInt());
                }
                else if (key == "layers") obj = ParseLayers(comp!.root!);
                else if (key == "u") subPath = GetString();
                else if (key == "p") data = GetString();
                else if (key == "w") width = GetFloat();
                else if (key == "h") height = GetFloat();
                else if (key == "e") embedded = GetInt() != 0;
                else if (key == "sid") sid = GetString();
                else Skip();
            }

            if (data != null)
            {
                obj = new LottieImage();
                ParseImage((LottieImage)obj, data, subPath, embedded, width, height);
                if (sid != null) RegisterSlot(obj, sid, ((LottieImage)obj).bitmap);
            }
            if (obj != null) obj.id = id;
            return obj;
        }

        private void ParseFontData(LottieFont font, string? data)
        {
            if (data == null) return;

            if (data.StartsWith("data:font/"))
            {
                var fontData = data.Substring("data:font/".Length);
                if (fontData.StartsWith("ttf"))
                {
                    fontData = fontData.Substring(3);
                }
                else
                {
                    TvgCommon.TVGLOG("LOTTIE", "TODO: Support a new font type!");
                    return;
                }
                var b64Start = ";base64,".Length;
                if (fontData.Length > b64Start)
                {
                    var b64Str = fontData.Substring(b64Start);
                    font.b64src = b64Str;
                    font.size = (uint)b64Str.Length;
                }
            }
            else
            {
                font.path = data;
            }
        }

        private LottieFont ParseFont()
        {
            EnterObject();
            var font = new LottieFont();

            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "fName") font.name = GetStringCopy();
                else if (key == "fFamily") font.family = GetStringCopy();
                else if (key == "fStyle") font.style = GetStringCopy();
                else if (key == "fPath") ParseFontData(font, GetString());
                else if (key == "ascent") font.ascent = GetFloat();
                else if (key == "origin") font.origin = (LottieFont.Origin)GetInt();
                else Skip();
            }
            font.Prepare();
            return font;
        }

        private void ParseAssets()
        {
            EnterArray();
            while (NextArrayValue())
            {
                var asset = ParseAsset();
                if (asset != null) comp!.assets.Add(asset);
                else TvgCommon.TVGERR("LOTTIE", "Invalid Asset!");
            }
        }

        private LottieMarker ParseMarker()
        {
            EnterObject();
            var marker = new LottieMarker();

            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "cm") marker.name = GetStringCopy();
                else if (key == "tm") marker.time = GetFloat();
                else if (key == "dr") marker.duration = GetFloat();
                else Skip();
            }
            return marker;
        }

        private void ParseMarkers()
        {
            EnterArray();
            while (NextArrayValue())
            {
                comp!.markers.Add(ParseMarker());
            }
        }

        private void ParseChars(List<LottieGlyph> glyphs)
        {
            EnterArray();
            while (NextArrayValue())
            {
                EnterObject();
                var glyph = new LottieGlyph();
                string? key;
                while ((key = NextObjectKey()) != null)
                {
                    if (key == "ch") glyph.code = GetStringCopy();
                    else if (key == "size") glyph.size = (ushort)GetFloat();
                    else if (key == "style") glyph.style = GetStringCopy();
                    else if (key == "w") glyph.width = GetFloat();
                    else if (key == "fFamily") glyph.family = GetStringCopy();
                    else if (key == "data")
                    {
                        EnterObject();
                        string? k2;
                        while ((k2 = NextObjectKey()) != null)
                        {
                            if (k2 == "shapes") ParseShapes(glyph.children);
                            else Skip();
                        }
                    }
                    else Skip();
                }
                glyph.Prepare();
                glyphs.Add(glyph);
            }
        }

        private void ParseFonts()
        {
            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "list")
                {
                    EnterArray();
                    while (NextArrayValue()) comp!.fonts.Add(ParseFont());
                }
                else Skip();
            }
        }

        private LottieObject ParseGroup()
        {
            var group = new LottieGroup();

            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (ParseCommon(group, key)) continue;
                else if (key == "it")
                {
                    EnterArray();
                    while (NextArrayValue()) ParseObject(group.children);
                }
                else if (key == "bm") group.blendMethod = (BlendMethod)GetInt();
                else Skip();
            }
            group.Prepare();
            return group;
        }

        private void ParseTimeRemap(LottieLayer layer)
        {
            ParseProperty(layer.timeRemap);
        }

        private void ParseShapes(List<LottieObject> parent)
        {
            EnterArray();
            while (NextArrayValue()) ParseObject(parent);
        }

        #endregion

        #region Text parsing

        private void ParseTextAlignmentOption(LottieText text)
        {
            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "g") text.alignOp.group = (LottieText.AlignOption.Group)GetInt();
                else if (key == "a") ParseProperty(text.alignOp.anchor);
                else Skip();
            }
        }

        private void ParseTextRange(LottieText text)
        {
            EnterArray();
            while (NextArrayValue())
            {
                EnterObject();
                var selector = new LottieTextRange();
                context.parent = selector;

                string? key;
                while ((key = NextObjectKey()) != null)
                {
                    if (key == "s")
                    {
                        EnterObject();
                        string? k2;
                        while ((k2 = NextObjectKey()) != null)
                        {
                            if (k2 == "t") selector.expressible = GetInt() != 0;
                            else if (k2 == "xe")
                            {
                                ParseProperty(selector.maxEase);
                                selector.interpolator = new LottieInterpolator();
                            }
                            else if (k2 == "ne") ParseProperty(selector.minEase);
                            else if (k2 == "a") ParseProperty(selector.maxAmount);
                            else if (k2 == "b") selector.based = (LottieTextRange.Based)GetInt();
                            else if (k2 == "rn") selector.random = GetInt() != 0 ? (byte)(new Random().Next() & 0xFF) : (byte)0;
                            else if (k2 == "sh") selector.shape = (LottieTextRange.RangeShape)GetInt();
                            else if (k2 == "o") ParseProperty(selector.offset);
                            else if (k2 == "r") selector.rangeUnit = (LottieTextRange.Unit)GetInt();
                            else if (k2 == "sm") ParseProperty(selector.smoothness);
                            else if (k2 == "s") ParseProperty(selector.start);
                            else if (k2 == "e") ParseProperty(selector.end);
                            else Skip();
                        }
                    }
                    else if (key == "a")
                    {
                        EnterObject();
                        string? k2;
                        while ((k2 = NextObjectKey()) != null)
                        {
                            if (k2 == "t") ParseProperty(selector.style.letterSpace, selector);
                            else if (k2 == "ls") ParseProperty(selector.style.lineSpace, selector);
                            else if (k2 == "fc") { ParseProperty(selector.style.fillColor, selector); selector.style.flagFillColor = true; }
                            else if (k2 == "fo") ParseProperty(selector.style.fillOpacity, selector);
                            else if (k2 == "sw") { ParseProperty(selector.style.strokeWidth, selector); selector.style.flagStrokeWidth = true; }
                            else if (k2 == "sc") { ParseProperty(selector.style.strokeColor, selector); selector.style.flagStrokeColor = true; }
                            else if (k2 == "so") ParseProperty(selector.style.strokeOpacity, selector);
                            else if (k2 == "o") ParseProperty(selector.style.opacity, selector);
                            else if (k2 == "p") ParseProperty(selector.style.position, selector);
                            else if (k2 == "s") ParseProperty(selector.style.scale, selector);
                            else if (k2 == "r") ParseProperty(selector.style.rotation, selector);
                            else Skip();
                        }
                    }
                    else Skip();
                }
                text.ranges.Add(selector);
            }
        }

        private void ParseTextFollowPath(LottieText text)
        {
            EnterObject();
            var key = NextObjectKey();
            if (key == null) return;
            text.follow ??= new LottieTextFollowPath();
            do
            {
                if (key == "m") text.follow.maskIdx = (sbyte)GetInt();
                else if (key == "f") ParseProperty(text.follow.firstMargin);
                else Skip();
            } while ((key = NextObjectKey()) != null);
        }

        private void ParseText(List<LottieObject> parent)
        {
            EnterObject();
            var text = new LottieText();

            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "d") ParseProperty(text.doc, text);
                else if (key == "a") ParseTextRange(text);
                else if (key == "m") ParseTextAlignmentOption(text);
                else if (key == "p") ParseTextFollowPath(text);
                else Skip();
            }
            parent.Add(text);
        }

        #endregion

        #region Mask and effect parsing

        private void GetLayerSize(ref float val)
        {
            if (val == 0.0f) val = GetFloat();
            else
            {
                var w = GetFloat();
                if (w < val) val = w;
            }
        }

        private LottieMask ParseMask()
        {
            var mask = new LottieMask();
            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "inv") mask.inverse = GetBool();
                else if (key == "mode") mask.method = GetMaskMethod(mask.inverse);
                else if (key == "pt") GetPathSet(null, mask.pathset);
                else if (key == "o") ParseProperty(mask.opacity);
                else if (key == "x") ParseProperty(mask.expand);
                else Skip();
            }
            return mask;
        }

        private void ParseMasks(LottieLayer layer)
        {
            EnterArray();
            while (NextArrayValue())
            {
                layer.masks.Add(ParseMask());
            }
        }

        private bool ParseEffect(LottieEffect effect, Action<LottieEffect, int> func)
        {
            var custom = (effect.type == LottieEffect.EffectType.Custom);
            LottieFxCustom.Property? property = null;

            EnterArray();
            int idx = 0;
            while (NextArrayValue())
            {
                EnterObject();
                string? key;
                while ((key = NextObjectKey()) != null)
                {
                    if (custom && key == "ty") property = ((LottieFxCustom)effect).CreateProperty(GetInt());
                    else if (key == "v")
                    {
                        if (PeekType() == kObjectType)
                        {
                            EnterObject();
                            string? k2;
                            while ((k2 = NextObjectKey()) != null)
                            {
                                if (k2 == "k") func(effect, idx++);
                                else Skip();
                            }
                        }
                        else func(effect, idx++);
                    }
                    else if (property != null && key == "nm") property.nm = TvgCompressor.Djb2Encode(GetString());
                    else if (property != null && key == "mn") property.mn = TvgCompressor.Djb2Encode(GetString());
                    else Skip();
                }
            }
            return true;
        }

        private void ParseCustom(LottieEffect effect, int idx)
        {
            var fx = (LottieFxCustom)effect;
            if (idx >= fx.props.Count) { TvgCommon.TVGERR("LOTTIE", "Parsing error in Custom effect!"); Skip(); return; }
            var prop = fx.props[idx].property;
            if (prop == null) { Skip(); return; }

            switch (prop.type)
            {
                case LottieProperty.PropertyType.Integer: ParsePropertyInternal((LottieInteger)prop); break;
                case LottieProperty.PropertyType.Float: ParsePropertyInternal((LottieFloat)prop); break;
                case LottieProperty.PropertyType.Vector: ParsePropertyInternal((LottieVector)prop); break;
                case LottieProperty.PropertyType.Color: ParsePropertyInternal((LottieColor)prop); break;
                default: TvgCommon.TVGLOG("LOTTIE", $"Missing Property Type? = {(int)prop.type}"); Skip(); break;
            }
        }

        private void ParseTint(LottieEffect effect, int idx)
        {
            var tint = (LottieFxTint)effect;
            if (idx == 0) ParsePropertyInternal(tint.black);
            else if (idx == 1) ParsePropertyInternal(tint.white);
            else if (idx == 2) ParsePropertyInternal(tint.intensity);
            else Skip();
        }

        private void ParseTritone(LottieEffect effect, int idx)
        {
            var tritone = (LottieFxTritone)effect;
            if (idx == 0) ParsePropertyInternal(tritone.bright);
            else if (idx == 1) ParsePropertyInternal(tritone.midtone);
            else if (idx == 2) ParsePropertyInternal(tritone.dark);
            else if (idx == 3) ParsePropertyInternal(tritone.blend);
            else Skip();
        }

        private void ParseFill(LottieEffect effect, int idx)
        {
            var fill = (LottieFxFill)effect;
            if (idx == 2) ParsePropertyInternal(fill.color);
            else if (idx == 6) ParsePropertyInternal(fill.opacity);
            else Skip();
        }

        private void ParseGaussianBlur(LottieEffect effect, int idx)
        {
            var blur = (LottieFxGaussianBlur)effect;
            if (idx == 0) ParsePropertyInternal(blur.blurness);
            else if (idx == 1) ParsePropertyInternal(blur.direction);
            else if (idx == 2) ParsePropertyInternal(blur.wrap);
            else Skip();
        }

        private void ParseDropShadow(LottieEffect effect, int idx)
        {
            var shadow = (LottieFxDropShadow)effect;
            if (idx == 0) ParsePropertyInternal(shadow.color);
            else if (idx == 1) ParsePropertyInternal(shadow.opacity);
            else if (idx == 2) ParsePropertyInternal(shadow.angle);
            else if (idx == 3) ParsePropertyInternal(shadow.distance);
            else if (idx == 4) ParsePropertyInternal(shadow.blurness);
            else Skip();
        }

        private void ParseStroke(LottieEffect effect, int idx)
        {
            var stroke = (LottieFxStroke)effect;
            if (idx == 0) ParsePropertyInternal(stroke.mask);
            else if (idx == 1) ParsePropertyInternal(stroke.allMask);
            else if (idx == 3) ParsePropertyInternal(stroke.color);
            else if (idx == 4) ParsePropertyInternal(stroke.size);
            else if (idx == 6) ParsePropertyInternal(stroke.opacity);
            else if (idx == 7) ParsePropertyInternal(stroke.begin);
            else if (idx == 8) ParsePropertyInternal(stroke.end);
            else Skip();
        }

        private bool ParseEffect(LottieEffect effect)
        {
            return effect.type switch
            {
                LottieEffect.EffectType.Custom => ParseEffect(effect, ParseCustom),
                LottieEffect.EffectType.Tint => ParseEffect(effect, ParseTint),
                LottieEffect.EffectType.Fill => ParseEffect(effect, ParseFill),
                LottieEffect.EffectType.Stroke => ParseEffect(effect, ParseStroke),
                LottieEffect.EffectType.Tritone => ParseEffect(effect, ParseTritone),
                LottieEffect.EffectType.DropShadow => ParseEffect(effect, ParseDropShadow),
                LottieEffect.EffectType.GaussianBlur => ParseEffect(effect, ParseGaussianBlur),
                _ => false
            };
        }

        private void ParseEffects(LottieLayer layer)
        {
            EnterArray();
            while (NextArrayValue())
            {
                LottieEffect? effect = null;
                var invalid = true;
                EnterObject();
                string? key;
                while ((key = NextObjectKey()) != null)
                {
                    if (key == "ty")
                    {
                        effect = GetEffect(GetInt());
                        if (effect == null) break;
                        invalid = false;
                    }
                    else if (effect != null && key == "nm") effect.nm = TvgCompressor.Djb2Encode(GetString());
                    else if (effect != null && key == "mn") effect.mn = TvgCompressor.Djb2Encode(GetString());
                    else if (effect != null && key == "ix") effect.ix = (short)GetInt();
                    else if (effect != null && key == "en") effect.enable = GetInt() != 0;
                    else if (effect != null && key == "ef") ParseEffect(effect);
                    else Skip();
                }
                if (invalid)
                {
                    TvgCommon.TVGLOG("LOTTIE", $"Not supported Layer Effect = {(effect != null ? (int)effect.type : -1)}");
                    while (NextObjectKey() != null) Skip();
                }
                else if (effect != null)
                {
                    layer.effects.Add(effect);
                }
            }
        }

        #endregion

        #region Layer parsing

        private LottieLayer ParseLayer(LottieLayer? precomp)
        {
            var layer = new LottieLayer();
            layer.comp = precomp;
            context.layer = layer;

            var ddd = false;
            RGB32 color = default;

            EnterObject();
            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "nm") { layer.name = GetStringCopy(); layer.id = TvgCompressor.Djb2Encode(layer.name); }
                else if (key == "ddd") ddd = GetInt() != 0;
                else if (key == "ind") layer.ix = (short)GetInt();
                else if (key == "ty") layer.layerType = (LottieLayer.LayerType)GetInt();
                else if (key == "sr") layer.timeStretch = GetFloat();
                else if (key == "ks") { EnterObject(); layer.transform = ParseTransform(ddd); }
                else if (key == "ao") layer.autoOrient = GetInt() != 0;
                else if (key == "shapes") ParseShapes(layer.children);
                else if (key == "ip") layer.inFrame = GetFloat();
                else if (key == "op") layer.outFrame = GetFloat();
                else if (key == "st") layer.startFrame = GetFloat();
                else if (key == "bm") layer.blendMethod = (BlendMethod)GetInt();
                else if (key == "parent") layer.pix = (short)GetInt();
                else if (key == "tm") ParseTimeRemap(layer);
                else if (key == "w" || key == "sw") GetLayerSize(ref layer.w);
                else if (key == "h" || key == "sh") GetLayerSize(ref layer.h);
                else if (key == "sc") color = GetColor(GetString());
                else if (key == "tt") layer.matteType = (MaskMethod)GetInt();
                else if (key == "tp") layer.mix = (short)GetInt();
                else if (key == "masksProperties") ParseMasks(layer);
                else if (key == "hd") layer.hidden = GetBool();
                else if (key == "refId") layer.rid = TvgCompressor.Djb2Encode(GetString());
                else if (key == "td") layer.matteSrc = GetInt() != 0;
                else if (key == "t") ParseText(layer.children);
                else if (key == "ef") ParseEffects(layer);
                else Skip();
            }

            layer.Prepare(color);
            return layer;
        }

        private LottieLayer ParseLayers(LottieLayer root)
        {
            var precomp = new LottieLayer();
            precomp.layerType = LottieLayer.LayerType.Precomp;
            precomp.comp = root;

            EnterArray();
            while (NextArrayValue())
            {
                var layer = ParseLayer(precomp);
                precomp.children.Add(layer);
            }
            precomp.Prepare();
            return precomp;
        }

        private void PostProcess(List<LottieGlyph> glyphs)
        {
            foreach (var glyph in glyphs)
            {
                foreach (var font in comp!.fonts)
                {
                    if (font.family == glyph.family && font.style == glyph.style)
                    {
                        font.chars.Add(glyph);
                        break;
                    }
                }
            }
        }

        #endregion

        #region Slot parsing

        private void CaptureSlots(string key)
        {
            // In the DOM-based approach, we just capture the raw JSON text of the slots object
            slots = GetCurrentValueRawText();
            Skip();
        }

        #endregion

        #region Public interface

        public string? Sid(bool first = false)
        {
            if (first)
            {
                if (!ParseNext()) return null;
                EnterObject();
            }
            return NextObjectKey();
        }

        public LottieProperty? Parse(LottieSlot slot)
        {
            EnterObject();

            LottieProperty? prop = null;
            context = new Context { layer = slot.context.layer, parent = slot.context.parent };

            switch (slot.propType)
            {
                case LottieProperty.PropertyType.Float:
                {
                    var p = new LottieFloat();
                    ParseSlotProperty(p);
                    prop = p;
                    break;
                }
                case LottieProperty.PropertyType.Scalar:
                {
                    var p = new LottieScalar();
                    ParseSlotProperty(p);
                    prop = p;
                    break;
                }
                case LottieProperty.PropertyType.Vector:
                {
                    var p = new LottieVector();
                    ParseSlotProperty(p);
                    prop = p;
                    break;
                }
                case LottieProperty.PropertyType.Opacity:
                {
                    var p = new LottieOpacity();
                    ParseSlotProperty(p);
                    prop = p;
                    break;
                }
                case LottieProperty.PropertyType.Color:
                {
                    var p = new LottieColor();
                    ParseSlotProperty(p);
                    prop = p;
                    break;
                }
                case LottieProperty.PropertyType.ColorStop:
                {
                    var obj = new LottieGradient();
                    string? k;
                    while ((k = NextObjectKey()) != null)
                    {
                        if (k == "p") ParseColorStop(obj);
                        else Skip();
                    }
                    obj.Prepare();
                    prop = new LottieColorStop(obj.colorStops);
                    break;
                }
                case LottieProperty.PropertyType.TextDoc:
                {
                    var p = new LottieTextDoc();
                    ParseSlotProperty(p);
                    prop = p;
                    break;
                }
                case LottieProperty.PropertyType.Image:
                {
                    LottieObject? obj = null;
                    string? k;
                    while ((k = NextObjectKey()) != null)
                    {
                        if (k == "p") obj = ParseAsset();
                        else Skip();
                    }
                    if (obj == null) return null;
                    prop = new LottieBitmap(((LottieImage)obj).bitmap);
                    break;
                }
            }

            if (prop != null) prop.sid = slot.sid;
            return prop;
        }

        public bool Parse()
        {
            if (!ParseNext()) return false;
            EnterObject();

            if (comp != null) comp = null;
            comp = new LottieComposition();

            var glyphs = new List<LottieGlyph>();
            var startFrame = 0.0f;
            var endFrame = 0.0f;

            string? key;
            while ((key = NextObjectKey()) != null)
            {
                if (key == "v") comp.version = GetStringCopy();
                else if (key == "fr") comp.frameRate = GetFloat();
                else if (key == "ip") startFrame = GetFloat();
                else if (key == "op") endFrame = GetFloat();
                else if (key == "w") comp.w = GetFloat();
                else if (key == "h") comp.h = GetFloat();
                else if (key == "nm") comp.name = GetStringCopy();
                else if (key == "assets") ParseAssets();
                else if (key == "layers") comp.root = ParseLayers(comp.root!);
                else if (key == "fonts") ParseFonts();
                else if (key == "chars") ParseChars(glyphs);
                else if (key == "markers") ParseMarkers();
                else if (key == "slots") CaptureSlots(key);
                else Skip();
            }

            if (Invalid() || comp.root == null)
            {
                comp = null;
                return false;
            }

            comp.root.inFrame = startFrame;
            comp.root.outFrame = endFrame;

            PostProcess(glyphs);
            return true;
        }

        #endregion
    }
}
