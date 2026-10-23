// Ported from ThorVG/src/loaders/lottie/tvgLottieModel.h and tvgLottieModel.cpp

using System;
using System.Collections.Generic;

namespace ThorVG
{
    // Stroke properties shared by solid and gradient strokes
    public class LottieStroke
    {
        public class DashAttr
        {
            public LottieFloat offset = new();
            public List<LottieFloat> values = new();
        }

        public LottieFloat width = new();
        public DashAttr? dashattr;
        public float miterLimit;
        public StrokeCap cap = StrokeCap.Round;
        public StrokeJoin join = StrokeJoin.Round;

        public LottieFloat DashValue()
        {
            dashattr ??= new DashAttr();
            var v = new LottieFloat();
            dashattr.values.Add(v);
            return v;
        }

        public LottieFloat DashOffset()
        {
            dashattr ??= new DashAttr();
            return dashattr.offset;
        }
    }

    // Effects hierarchy
    public class LottieEffect
    {
        public enum EffectType : byte { Custom = 5, Tint = 20, Fill, Stroke, Tritone, DropShadow = 25, GaussianBlur = 29 }

        public ulong nm;    // encoded by djb2
        public ulong mn;    // encoded by djb2
        public short ix;
        public EffectType type;
        public bool enable;
    }

    public class LottieFxCustom : LottieEffect
    {
        public class Property
        {
            public LottieProperty? property;
            public ulong nm;
            public ulong mn;
        }

        public string? name;
        public List<Property> props = new();

        public LottieFxCustom() { type = EffectType.Custom; }

        public Property? CreateProperty(int propType)
        {
            LottieProperty? prop = null;
            switch (propType)
            {
                case 0: // slider
                case 1: prop = new LottieFloat(); break; // angle
                case 2: prop = new LottieColor(); break; // color
                case 3: prop = new LottieVector(); break; // point
                case 4: // checkbox
                case 7: // dropdown
                case 10: prop = new LottieInteger(); break; // effect layer
                default: return null;
            }
            var p = new Property { property = prop };
            props.Add(p);
            return p;
        }

        public LottieProperty? FindProperty(string? name)
        {
            if (name == null) return null;
            var id = TvgCompressor.Djb2EncodeNative(name);
            foreach (var p in props)
            {
                if (p.mn == id || p.nm == id) return p.property;
            }
            return null;
        }
    }

    public class LottieFxFill : LottieEffect
    {
        public LottieColor color = new();
        public LottieFloat opacity = new();
        public LottieFxFill() { type = EffectType.Fill; }
    }

    public class LottieFxStroke : LottieEffect
    {
        public LottieInteger mask = new();
        public LottieInteger allMask = new();
        public LottieColor color = new();
        public LottieFloat size = new();
        public LottieFloat opacity = new();
        public LottieFloat begin = new();
        public LottieFloat end = new();
        public LottieFxStroke() { type = EffectType.Stroke; }
    }

    public class LottieFxTint : LottieEffect
    {
        public LottieColor black = new();
        public LottieColor white = new();
        public LottieFloat intensity = new();
        public LottieFxTint() { type = EffectType.Tint; }
    }

    public class LottieFxTritone : LottieEffect
    {
        public LottieColor bright = new();
        public LottieColor midtone = new();
        public LottieColor dark = new();
        public LottieOpacity blend = new();
        public LottieFxTritone() { type = EffectType.Tritone; }
    }

    public class LottieFxDropShadow : LottieEffect
    {
        public LottieColor color = new();
        public LottieFloat opacity = new();
        public LottieFloat angle = new();
        public LottieFloat distance = new();
        public LottieFloat blurness = new();
        public LottieFxDropShadow() { type = EffectType.DropShadow; }
    }

    public class LottieFxGaussianBlur : LottieEffect
    {
        public LottieFloat blurness = new();
        public LottieInteger direction = new();
        public LottieInteger wrap = new();
        public LottieFxGaussianBlur() { type = EffectType.GaussianBlur; }
    }

    public class LottieMask
    {
        public LottiePathSet pathset = new();
        public LottieFloat expand = new();
        public LottieOpacity opacity = new(255);
        public MaskMethod method;
        public bool inverse;
    }

    // Base for all Lottie model objects
    public class LottieObject
    {
        public enum ObjectType : byte
        {
            Composition = 0, Layer, Group, Transform, SolidFill, SolidStroke,
            GradientFill, GradientStroke, Rect, Ellipse, Path, Polystar,
            Image, Trimpath, Text, Repeater, RoundedCorner, OffsetPath, PuckerBloat, ZigZag, TextRange, Audio
        }

        public ulong id;        // unique id by name, generated by djb2 encoding
        public ObjectType type;
        public bool hidden;

        public virtual LottieProperty? Override(LottieProperty prop, bool release) => null;
        public virtual bool Mergeable() => false;
        public virtual LottieProperty? FindProperty(ushort ix) => null;
    }

    public class LottieGlyph
    {
        public List<LottieObject> children = new();
        public float width;
        public string? code;
        public string? family;
        public string? style;
        public ushort size;
        public byte len;
        private string? cachedCode;
        private byte[] cachedUtf8 = System.Array.Empty<byte>();
        internal byte[] Utf8Code
        {
            get
            {
                if (!ReferenceEquals(cachedCode, code))
                {
                    cachedUtf8 = LottieDataHelper.Utf8CString(code);
                    cachedCode = code;
                }
                return cachedUtf8;
            }
        }

        public bool Prepare()
        {
            len = (byte)Utf8Code.Length;
            return len > 0;
        }
    }

    public class LottieTextRange : LottieObject
    {
        public enum Based : byte { Chars = 1, CharsExcludingSpaces, Words, Lines }
        public enum RangeShape : byte { Square = 1, RampUp, RampDown, Triangle, Round, Smooth }
        public enum Unit : byte { Percent = 1, Index }

        public class StyleData
        {
            public LottieColor fillColor = new(new RGB32(255, 255, 255));
            public LottieColor strokeColor = new(new RGB32(255, 255, 255));
            public LottieVector position = new();
            public LottieScalar scale = new(new Point(100, 100));
            public LottieFloat letterSpace = new();
            public LottieFloat lineSpace = new();
            public LottieFloat strokeWidth = new();
            public LottieFloat rotation = new();
            public LottieOpacity fillOpacity = new(255);
            public LottieOpacity strokeOpacity = new(255);
            public LottieOpacity opacity = new(255);
            public bool flagFillColor;
            public bool flagStrokeColor;
            public bool flagStrokeWidth;
        }

        public StyleData style = new();
        public LottieFloat offset = new();
        public LottieFloat maxEase = new();
        public LottieFloat minEase = new();
        public LottieFloat maxAmount = new();
        public LottieFloat smoothness = new(100.0f);
        public LottieFloat start = new();
        public LottieFloat end = new(100.0f);
        public LottieInterpolator? interpolator;
        public Based based = Based.Chars;
        public RangeShape shape = RangeShape.Square;
        public Unit rangeUnit = Unit.Percent;
        public byte random;
        public bool expressible;

        public LottieTextRange()
        {
            type = ObjectType.TextRange;
            style.flagFillColor = false;
            style.flagStrokeColor = false;
            style.flagStrokeWidth = false;
        }

        public float Factor(float frameNo, float totalLen, float idx)
        {
            var offsetVal = offset.Evaluate(frameNo);
            var startVal = start.Evaluate(frameNo) + offsetVal;
            var endVal = end.Evaluate(frameNo) + offsetVal;

            if (random > 0)
            {
                var range = endVal - startVal;
                var len = (rangeUnit == Unit.Percent) ? 100.0f : totalLen;
                startVal = (float)(random % (int)(len - range));
                endVal = startVal + range;
            }

            var divisor = (rangeUnit == Unit.Percent) ? (100.0f / totalLen) : 1.0f;
            startVal /= divisor;
            endVal /= divisor;

            var f = 0.0f;

            switch (shape)
            {
                case RangeShape.Square:
                {
                    var smoothnessVal = smoothness.Evaluate(frameNo);
                    if (TvgMath.Zero(smoothnessVal))
                    {
                        f = idx >= MathF.Round(startVal) && idx < MathF.Round(endVal) ? 1.0f : 0.0f;
                    }
                    else
                    {
                        if (idx >= MathF.Floor(startVal))
                        {
                            var diff = idx - startVal;
                            f = diff < 0.0f ? MathF.Min(endVal, 1.0f) + diff : endVal - idx;
                        }
                        smoothnessVal *= 0.01f;
                        f = (f - (1.0f - smoothnessVal) * 0.5f) / smoothnessVal;
                    }
                    break;
                }
                case RangeShape.RampUp:
                {
                    f = TvgMath.Equal(startVal, endVal) ? (idx >= endVal ? 1.0f : 0.0f) : (0.5f + idx - startVal) / (endVal - startVal);
                    break;
                }
                case RangeShape.RampDown:
                {
                    f = TvgMath.Equal(startVal, endVal) ? (idx >= endVal ? 0.0f : 1.0f) : 1.0f - (0.5f + idx - startVal) / (endVal - startVal);
                    break;
                }
                case RangeShape.Triangle:
                {
                    f = TvgMath.Equal(startVal, endVal) ? 0.0f : 2.0f * (0.5f + idx - startVal) / (endVal - startVal);
                    f = f < 1.0f ? f : 2.0f - f;
                    break;
                }
                case RangeShape.Round:
                {
                    idx = TvgMath.Clamp(idx + (0.5f - startVal), 0.0f, endVal - startVal);
                    var range = 0.5f * (endVal - startVal);
                    var t = idx - range;
                    f = TvgMath.Equal(startVal, endVal) ? 0.0f : MathF.Sqrt(1.0f - t * t / (range * range));
                    break;
                }
                case RangeShape.Smooth:
                {
                    idx = TvgMath.Clamp(idx + (0.5f - startVal), 0.0f, endVal - startVal);
                    f = TvgMath.Equal(startVal, endVal) ? 0.0f : 0.5f * (1.0f + MathF.Cos(MathConstants.MATH_PI * (1.0f + 2.0f * idx / (endVal - startVal))));
                    break;
                }
            }
            f = TvgMath.Clamp(f, 0.0f, 1.0f);

            // apply easing
            var minEaseVal = TvgMath.Clamp(minEase.Evaluate(frameNo), -100.0f, 100.0f);
            var maxEaseVal = TvgMath.Clamp(maxEase.Evaluate(frameNo), -100.0f, 100.0f);
            if (!TvgMath.Zero(minEaseVal) || !TvgMath.Zero(maxEaseVal))
            {
                var inPt = new Point(1.0f, 1.0f);
                var outPt = new Point(0.0f, 0.0f);

                if (maxEaseVal > 0.0f) inPt.x = 1.0f - maxEaseVal * 0.01f;
                else inPt.y = 1.0f + maxEaseVal * 0.01f;
                if (minEaseVal > 0.0f) outPt.x = minEaseVal * 0.01f;
                else outPt.y = -minEaseVal * 0.01f;

                interpolator ??= new LottieInterpolator();
                interpolator.Set(null, inPt, outPt);
                f = interpolator.Progress(f);
            }
            f = TvgMath.Clamp(f, 0.0f, 1.0f);

            return f * maxAmount.Evaluate(frameNo) * 0.01f;
        }

        public void Color(float frameNo, ref RGB32 fillColor, ref RGB32 strokeColor, float factor, LottieTween tween, LottieExpressions? exps)
        {
            if (style.flagFillColor)
            {
                var color = style.fillColor.Evaluate(frameNo, tween, exps);
                fillColor = RGB32.Lerp(fillColor, color, factor);
            }
            if (style.flagStrokeColor)
            {
                var color = style.strokeColor.Evaluate(frameNo, tween, exps);
                strokeColor = RGB32.Lerp(strokeColor, color, factor);
            }
        }

        public override LottieProperty? Override(LottieProperty prop, bool release)
        {
            LottieProperty? backup = null;
            if (LottieOverride.Apply(style.fillColor, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(style.strokeColor, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(style.position, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(style.scale, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(style.rotation, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(style.letterSpace, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(style.lineSpace, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(style.strokeWidth, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(style.fillOpacity, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(style.strokeOpacity, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(style.opacity, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(offset, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(maxEase, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(minEase, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(maxAmount, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(smoothness, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(start, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(end, prop, ref backup, release)) return backup;
            return base.Override(prop, release);
        }
    }

    public class LottieFont
    {
        public enum Origin : byte { Local = 0, CssURL, ScriptURL, FontURL }

        public string? b64src; // union with path in C++ — base64 encoded font data or external path
        public string? path { get => b64src; set => b64src = value; }
        public List<LottieGlyph> chars = new();
        public string? name;
        public string? family;
        public string? style;
        public string? mime;
        public uint size;      // decoded font byte count after Prepare
        public float ascent;
        public Origin origin = Origin.Local;
        public bool external;
        private bool released;

        public void Prepare()
        {
            released = false;
            if (external) { Text.LoadFont(b64src); return; }
            if (b64src == null)
            {
                Text.LoadFont(name!, null, size, mime, Ownership.Transfer);
                return;
            }
            // Native parseFontData uses the shared permissive decoder, and
            // prepare transfers the decoded allocation to the font loader.
            var decoded = TvgCompressor.B64Decode(b64src);
            size = (uint)decoded.Length;
            Text.LoadFont(name!, decoded, size, mime, Ownership.Transfer);
        }

        ~LottieFont()
        {
            Release();
        }

        public void Release()
        {
            if (released) return;
            released = true;
            if (external) Text.UnloadFont(name!);
            else if (name != null) Text.LoadFont(name, null, size, mime, false);
            b64src = null;
        }
    }

    public class LottieMarker
    {
        public string? name;
        public float time;
        public float duration;
    }

    public unsafe class LottieTextFollowPath : IDisposable
    {
        private RenderPath path = new();
        private PathCommand* cmds;
        private uint cmdsCnt;
        private Point* pts;
        private Point* start;
        private float totalLen;
        private float currentLen;

        private void Rewind()
        {
            pts = path.pts.data;
            cmds = path.cmds.data;
            cmdsCnt = path.cmds.count;
            currentLen = 0.0f;
        }

        public LottieFloat firstMargin = new(0.0f);
        public LottieInteger perpendicular = new(0);
        public LottieMask? mask;
        public sbyte maskIdx = -1;

        ~LottieTextFollowPath() => Release();

        public void Dispose()
        {
            Release();
            GC.SuppressFinalize(this);
        }

        public void Release()
        {
            path.cmds.Dispose();
            path.pts.Dispose();
            cmds = null;
            pts = start = null;
            cmdsCnt = 0;
        }

        private Point Split(float dLen, float lenSearched, ref float angle)
        {
            switch (*cmds)
            {
                case PathCommand.MoveTo:
                {
                    angle = 0.0f;
                    break;
                }
                case PathCommand.LineTo:
                {
                    var dp = new Point(pts->x - (pts - 1)->x, pts->y - (pts - 1)->y);
                    angle = TvgMath.Atan2(dp.y, dp.x);
                    break;
                }
                case PathCommand.CubicTo:
                {
                    var bz = new Bezier(*(pts - 1), *pts, *(pts + 1), *(pts + 2));
                    float t = bz.At(lenSearched - currentLen, dLen);
                    angle = TvgMath.Deg2Rad(bz.Angle(t));
                    return bz.At(t);
                }
                case PathCommand.Close:
                {
                    var dp = new Point(start->x - (pts - 1)->x, start->y - (pts - 1)->y);
                    angle = TvgMath.Atan2(dp.y, dp.x);
                    break;
                }
            }
            return default;
        }

        public float Prepare(LottieMask? mask, float frameNo, float scale, LottieTween tween, LottieExpressions? exps)
        {
            this.mask = mask;
            var m = new Matrix(1.0f / scale, 0.0f, 0.0f, 0.0f, 1.0f / scale, 0.0f, 0.0f, 0.0f, 1.0f);
            path.Clear();
            mask!.pathset.Evaluate(frameNo, path, &m, tween, exps);

            Rewind();
            totalLen = TvgMath.Length(cmds, cmdsCnt, pts, path.pts.count);
            start = pts;

            return firstMargin.Evaluate(frameNo, tween, exps) / scale;
        }

        public Point Position(float lenSearched, ref float angle)
        {
            // position before the start of the curve
            if (lenSearched <= 0.0f)
            {
                // shape is closed -> wrapping
                if (path.cmds.Last() == PathCommand.Close)
                {
                    while (lenSearched < 0.0f) lenSearched += totalLen;
                }
                // linear interpolation
                else
                {
                    if (cmds >= path.cmds.data + path.cmds.count - 1) return *start;
                    switch (*(cmds + 1))
                    {
                        case PathCommand.LineTo:
                        {
                            var dp = new Point((pts + 1)->x - pts->x, (pts + 1)->y - pts->y);
                            angle = TvgMath.Atan2(dp.y, dp.x);
                            return new Point(TvgMath.MultiplyAdd(lenSearched, MathF.Cos(angle), pts->x), TvgMath.MultiplyAdd(lenSearched, MathF.Sin(angle), pts->y));
                        }
                        case PathCommand.CubicTo:
                        {
                            angle = TvgMath.Deg2Rad(new Bezier(*pts, *(pts + 1), *(pts + 2), *(pts + 3)).AngleAtConstant(0.0001f));
                            return new Point(TvgMath.MultiplyAdd(lenSearched, MathF.Cos(angle), pts->x), TvgMath.MultiplyAdd(lenSearched, MathF.Sin(angle), pts->y));
                        }
                        default:
                            angle = 0.0f;
                            return *start;
                    }
                }
            }

            void Shift()
            {
                switch (*cmds)
                {
                    case PathCommand.MoveTo: start = pts; ++pts; break;
                    case PathCommand.LineTo: ++pts; break;
                    case PathCommand.CubicTo: pts += 3; break;
                    case PathCommand.Close: break;
                }
                ++cmds;
                --cmdsCnt;
            }

            // position beyond the end of the curve
            if (lenSearched >= totalLen)
            {
                // shape is closed -> wrapping
                if (path.cmds.Last() == PathCommand.Close)
                {
                    while (lenSearched > totalLen) lenSearched -= totalLen;
                }
                // linear interpolation
                else
                {
                    while (cmdsCnt > 1) Shift();
                    switch (*cmds)
                    {
                        case PathCommand.MoveTo:
                            angle = 0.0f;
                            return *pts;
                        case PathCommand.LineTo:
                        {
                            var len = lenSearched - totalLen;
                            var dp = new Point(pts->x - (pts - 1)->x, pts->y - (pts - 1)->y);
                            angle = TvgMath.Atan2(dp.y, dp.x);
                            return new Point(TvgMath.MultiplyAdd(len, MathF.Cos(angle), pts->x), TvgMath.MultiplyAdd(len, MathF.Sin(angle), pts->y));
                        }
                        case PathCommand.CubicTo:
                        {
                            var len = lenSearched - totalLen;
                            angle = TvgMath.Deg2Rad(new Bezier(*(pts - 1), *pts, *(pts + 1), *(pts + 2)).AngleAtConstant(0.999f));
                            return new Point(TvgMath.MultiplyAdd(len, MathF.Cos(angle), (pts + 2)->x), TvgMath.MultiplyAdd(len, MathF.Sin(angle), (pts + 2)->y));
                        }
                        case PathCommand.Close:
                        {
                            var len = lenSearched - totalLen;
                            var dp = new Point(start->x - (pts - 1)->x, start->y - (pts - 1)->y);
                            angle = TvgMath.Atan2(dp.y, dp.x);
                            return new Point(TvgMath.MultiplyAdd(len, MathF.Cos(angle), (pts - 1)->x), TvgMath.MultiplyAdd(len, MathF.Sin(angle), (pts - 1)->y));
                        }
                    }
                }
            }

            // reset required if text partially crosses curve start
            if (lenSearched < currentLen) Rewind();

            float SegLength()
            {
                switch (*cmds)
                {
                    case PathCommand.MoveTo: return 0.0f;
                    case PathCommand.LineTo: return TvgMath.PointLength(*(pts - 1), *pts);
                    case PathCommand.CubicTo: return new Bezier(*(pts - 1), *pts, *(pts + 1), *(pts + 2)).Length();
                    case PathCommand.Close: return TvgMath.PointLength(*(pts - 1), *start);
                    default: return 0.0f;
                }
            }

            while (cmdsCnt > 0)
            {
                var dLen = SegLength();
                if (currentLen + dLen < lenSearched)
                {
                    Shift();
                    currentLen += dLen;
                    continue;
                }
                return Split(dLen, lenSearched, ref angle);
            }
            return default;
        }
    }

    public class LottieText : LottieObject
    {
        public class AlignOption
        {
            public enum Group : byte { Chars = 1, Word = 2, Line = 3, All = 4 }
            public Group group = Group.Chars;
            public LottieScalar anchor = new();
        }

        public LottieRenderPooler<Shape> renderPooler = new();
        public AlignOption alignOp = new();
        public LottieTextDoc doc = new();
        public LottieFont? font;
        public List<LottieTextRange> ranges = new();
        public LottieTextFollowPath? follow;

        public LottieText() { type = ObjectType.Text; }

        public override LottieProperty? Override(LottieProperty prop, bool release)
        {
            LottieProperty? backup = null;
            if (LottieOverride.Apply(doc, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(alignOp.anchor, prop, ref backup, release)) return backup;
            return base.Override(prop, release);
        }

        public override LottieProperty? FindProperty(ushort ix)
        {
            if (doc.ix == ix) return doc;
            return null;
        }
    }

    public class LottieTrimpath : LottieObject
    {
        public enum TrimType : byte { Simultaneous = 1, Individual = 2 }

        public LottieFloat start = new();
        public LottieFloat end = new(100.0f);
        public LottieFloat offset = new();
        public TrimType trimType = TrimType.Simultaneous;

        public LottieTrimpath() { type = ObjectType.Trimpath; }

        public override bool Mergeable()
        {
            if (start.frames == null && start.value == 0.0f && end.frames == null && end.value == 100.0f && offset.frames == null && offset.value == 0.0f) return true;
            return false;
        }

        public override LottieProperty? FindProperty(ushort ix)
        {
            if (start.ix == ix) return start;
            if (end.ix == ix) return end;
            if (offset.ix == ix) return offset;
            return null;
        }

        public void Segment(float frameNo, out float startVal, out float endVal, LottieTween tween, LottieExpressions? exps)
        {
            startVal = TvgMath.Clamp(start.Evaluate(frameNo, tween, exps) * 0.01f, 0.0f, 1.0f);
            endVal = TvgMath.Clamp(end.Evaluate(frameNo, tween, exps) * 0.01f, 0.0f, 1.0f);

            var diff = MathF.Abs(startVal - endVal);
            if (TvgMath.Zero(diff))
            {
                startVal = 0.0f;
                endVal = 0.0f;
                return;
            }

            // Even if the start and end values do not cause trimming, an offset > 0 can still affect dashing starting point
            var o = (offset.Evaluate(frameNo, tween, exps) % 360.0f) / 360.0f;
            if (TvgMath.Zero(o) && diff >= 1.0f)
            {
                startVal = 0.0f;
                endVal = 1.0f;
                return;
            }

            if (startVal > endVal) { var tmp = startVal; startVal = endVal; endVal = tmp; }
            startVal += o;
            endVal += o;
        }
        public override LottieProperty? Override(LottieProperty prop, bool release)
        {
            LottieProperty? backup = null;
            if (LottieOverride.Apply(start, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(end, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(offset, prop, ref backup, release)) return backup;
            return base.Override(prop, release);
        }
    }

    public class LottieShape : LottieObject
    {
        public LottieRenderPooler<Shape> renderPooler = new();
        public bool clockwise = true;

        public override bool Mergeable() => true;

        public LottieShape(ObjectType type) { this.type = type; }
    }

    public class LottieRoundedCorner : LottieObject
    {
        public LottieFloat radius = new();
        public LottieRoundedCorner() { type = ObjectType.RoundedCorner; }

        public override LottieProperty? FindProperty(ushort ix)
        {
            if (radius.ix == ix) return radius;
            return null;
        }
        public override LottieProperty? Override(LottieProperty prop, bool release)
        {
            LottieProperty? backup = null;
            if (LottieOverride.Apply(radius, prop, ref backup, release)) return backup;
            return base.Override(prop, release);
        }
    }

    public class LottiePath : LottieShape
    {
        public LottiePathSet pathset = new();
        public LottiePath() : base(ObjectType.Path) { }

        public override LottieProperty? FindProperty(ushort ix)
        {
            if (pathset.ix == ix) return pathset;
            return null;
        }
        public override LottieProperty? Override(LottieProperty prop, bool release)
        {
            LottieProperty? backup = null;
            if (LottieOverride.Apply(pathset, prop, ref backup, release)) return backup;
            return base.Override(prop, release);
        }
    }

    public class LottieRect : LottieShape
    {
        public LottieVector position = new();
        public LottieScalar size = new();
        public LottieFloat radius = new();
        public LottieRect() : base(ObjectType.Rect) { }

        public override LottieProperty? FindProperty(ushort ix)
        {
            if (position.ix == ix) return position;
            if (size.ix == ix) return size;
            if (radius.ix == ix) return radius;
            return null;
        }
        public override LottieProperty? Override(LottieProperty prop, bool release)
        {
            LottieProperty? backup = null;
            if (LottieOverride.Apply(position, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(size, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(radius, prop, ref backup, release)) return backup;
            return base.Override(prop, release);
        }
    }

    public class LottiePolyStar : LottieShape
    {
        public enum StarType : byte { Star = 1, Polygon }

        public LottieVector position = new();
        public LottieFloat innerRadius = new();
        public LottieFloat outerRadius = new();
        public LottieFloat innerRoundness = new();
        public LottieFloat outerRoundness = new();
        public LottieFloat rotation = new();
        public LottieFloat ptsCnt = new();
        public StarType starType = StarType.Polygon;

        public LottiePolyStar() : base(ObjectType.Polystar) { }

        public override LottieProperty? FindProperty(ushort ix)
        {
            if (position.ix == ix) return position;
            if (innerRadius.ix == ix) return innerRadius;
            if (outerRadius.ix == ix) return outerRadius;
            if (innerRoundness.ix == ix) return innerRoundness;
            if (outerRoundness.ix == ix) return outerRoundness;
            if (rotation.ix == ix) return rotation;
            if (ptsCnt.ix == ix) return ptsCnt;
            return null;
        }
        public override LottieProperty? Override(LottieProperty prop, bool release)
        {
            LottieProperty? backup = null;
            if (LottieOverride.Apply(position, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(innerRadius, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(outerRadius, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(innerRoundness, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(outerRoundness, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(rotation, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(ptsCnt, prop, ref backup, release)) return backup;
            return base.Override(prop, release);
        }
    }

    public class LottieEllipse : LottieShape
    {
        public LottieVector position = new();
        public LottieScalar size = new();
        public LottieEllipse() : base(ObjectType.Ellipse) { }

        public override LottieProperty? FindProperty(ushort ix)
        {
            if (position.ix == ix) return position;
            if (size.ix == ix) return size;
            return null;
        }
        public override LottieProperty? Override(LottieProperty prop, bool release)
        {
            LottieProperty? backup = null;
            if (LottieOverride.Apply(position, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(size, prop, ref backup, release)) return backup;
            return base.Override(prop, release);
        }
    }

    public class LottieTransform : LottieObject
    {
        public class SeparateCoord
        {
            public LottieFloat x = new();
            public LottieFloat y = new();
        }

        public class Dimension3
        {
            public LottieFloat rx = new();
            public LottieFloat ry = new();
            public LottieScalar3 orient = new(new Point3(0.0f, 0.0f, 0.0f));
        }

        public LottieVector position = new();
        public LottieFloat rotation = new();     // z rotation
        public LottieScalar scale = new(new Point(100.0f, 100.0f));
        public LottieScalar anchor = new();
        public LottieOpacity opacity = new(255);
        public LottieFloat skewAngle = new();
        public LottieFloat skewAxis = new();
        public SeparateCoord? coords;
        public Dimension3? ddd;

        public LottieTransform() { type = ObjectType.Transform; }
        public override bool Mergeable() => true;

        public SeparateCoord GetSeparateCoord()
        {
            coords ??= new SeparateCoord();
            return coords;
        }

        public override LottieProperty? FindProperty(ushort ix)
        {
            if (position.ix == ix) return position;
            if (rotation.ix == ix) return rotation;
            if (scale.ix == ix) return scale;
            if (anchor.ix == ix) return anchor;
            if (opacity.ix == ix) return opacity;
            if (skewAngle.ix == ix) return skewAngle;
            if (skewAxis.ix == ix) return skewAxis;
            if (coords != null)
            {
                if (coords.x.ix == ix) return coords.x;
                if (coords.y.ix == ix) return coords.y;
            }
            return null;
        }

        public override LottieProperty? Override(LottieProperty prop, bool release)
        {
            LottieProperty? backup = null;
            if (LottieOverride.Apply(rotation, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(scale, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(position, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(anchor, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(opacity, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(skewAngle, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(skewAxis, prop, ref backup, release)) return backup;
            return base.Override(prop, release);
        }
    }

    public class LottieSolid : LottieObject
    {
        public LottieColor color = new(new RGB32(255, 255, 255));
        public LottieOpacity opacity = new(255);

        public override LottieProperty? FindProperty(ushort ix)
        {
            if (color.ix == ix) return color;
            if (opacity.ix == ix) return opacity;
            return null;
        }

        public override LottieProperty? Override(LottieProperty prop, bool release)
        {
            LottieProperty? backup = null;
            if (LottieOverride.Apply(color, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(opacity, prop, ref backup, release)) return backup;
            return base.Override(prop, release);
        }
    }

    public class LottieSolidStroke : LottieSolid
    {
        public LottieStroke stroke = new();

        public LottieSolidStroke() { type = ObjectType.SolidStroke; }

        public override LottieProperty? FindProperty(ushort ix)
        {
            if (stroke.width.ix == ix) return stroke.width;
            if (stroke.dashattr != null)
            {
                foreach (var v in stroke.dashattr.values)
                    if (v.ix == ix) return v;
            }
            return base.FindProperty(ix);
        }

        public override LottieProperty? Override(LottieProperty prop, bool release)
        {
            LottieProperty? backup = null;
            if (LottieOverride.Apply(stroke.width, prop, ref backup, release)) return backup;
            return base.Override(prop, release);
        }
    }

    public class LottieSolidFill : LottieSolid
    {
        public FillRule rule = FillRule.NonZero;
        public LottieSolidFill() { type = ObjectType.SolidFill; }
    }

    public class LottieGradient : LottieObject
    {
        public LottieScalar start = new();
        public LottieScalar end = new();
        public LottieFloat height = new();
        public LottieFloat angle = new();
        public LottieOpacity opacity = new(255);
        public LottieColorStop colorStops = new();
        public byte gradientId;  // 1: linear, 2: radial
        public bool opaque = true;

        public bool Prepare()
        {
            if (!colorStops.populated)
            {
                var cnt = colorStops.count;
                if (colorStops.frames != null)
                {
                    foreach (var v in colorStops.frames)
                    {
                        colorStops.count = (ushort)Populate(v.value, cnt);
                    }
                }
                else
                {
                    colorStops.count = (ushort)Populate(colorStops.value, cnt);
                }
                colorStops.populated = true;
            }
            return start.frames != null || end.frames != null || height.frames != null ||
                   angle.frames != null || opacity.frames != null || colorStops.frames != null;
        }

        public override LottieProperty? FindProperty(ushort ix)
        {
            if (start.ix == ix) return start;
            if (end.ix == ix) return end;
            if (height.ix == ix) return height;
            if (angle.ix == ix) return angle;
            if (opacity.ix == ix) return opacity;
            if (colorStops.ix == ix) return colorStops;
            return null;
        }

        public override LottieProperty? Override(LottieProperty prop, bool release)
        {
            LottieProperty? backup = null;
            if (LottieOverride.Apply(colorStops, prop, ref backup, release)) Prepare();
            else if (LottieOverride.Apply(opacity, prop, ref backup, release)) return backup;
            else if (LottieOverride.Apply(start, prop, ref backup, release)) return backup;
            else if (LottieOverride.Apply(end, prop, ref backup, release)) return backup;
            else if (LottieOverride.Apply(height, prop, ref backup, release)) return backup;
            else if (LottieOverride.Apply(angle, prop, ref backup, release)) return backup;
            return backup;
        }

        public unsafe uint Populate(ColorStop color, uint count)
        {
            if (color.input == null) return 0;

            var input = color.input.Value;
            ulong colorLast = (ulong)count * 4;
            if (input.count < colorLast)
            {
                TvgCommon.TVGERR("LOTTIE", "Malformed asset!");
                return 0;
            }
            uint clast = (uint)colorLast;
            uint inputLast = input.count - ((input.count - clast) % 2);
            uint alphaCnt = (inputLast - clast) / 2;
            var output = new List<Fill.ColorStop>((int)(count + alphaCnt));
            uint cidx = 0;
            uint aidx = clast;
            Fill.ColorStop cs = default;

            // merge color stops
            for (uint i = 0; i < inputLast; ++i)
            {
                if (cidx == clast || aidx == inputLast) break;
                if (input[cidx] == input[aidx])
                {
                    cs.offset = input[cidx];
                    cs.r = (byte)MathF.Round(input[cidx + 1] * 255.0f);
                    cs.g = (byte)MathF.Round(input[cidx + 2] * 255.0f);
                    cs.b = (byte)MathF.Round(input[cidx + 3] * 255.0f);
                    cs.a = (byte)MathF.Round(input[aidx + 1] * 255.0f);
                    cidx += 4;
                    aidx += 2;
                }
                else if (input[cidx] < input[aidx])
                {
                    cs.offset = input[cidx];
                    cs.r = (byte)MathF.Round(input[cidx + 1] * 255.0f);
                    cs.g = (byte)MathF.Round(input[cidx + 2] * 255.0f);
                    cs.b = (byte)MathF.Round(input[cidx + 3] * 255.0f);
                    if (output.Count > 0)
                    {
                        var last = output[^1];
                        var p = (input[cidx] - last.offset) / (input[aidx] - last.offset);
                        cs.a = LottieDataHelper.LerpByte(last.a, (byte)MathF.Round(input[aidx + 1] * 255.0f), p);
                    }
                    else cs.a = (byte)MathF.Round(input[aidx + 1] * 255.0f);
                    cidx += 4;
                }
                else
                {
                    cs.offset = input[aidx];
                    cs.a = (byte)MathF.Round(input[aidx + 1] * 255.0f);
                    if (output.Count > 0)
                    {
                        var last = output[^1];
                        var p = (input[aidx] - last.offset) / (input[cidx] - last.offset);
                        cs.r = LottieDataHelper.LerpByte(last.r, (byte)MathF.Round(input[cidx + 1] * 255.0f), p);
                        cs.g = LottieDataHelper.LerpByte(last.g, (byte)MathF.Round(input[cidx + 2] * 255.0f), p);
                        cs.b = LottieDataHelper.LerpByte(last.b, (byte)MathF.Round(input[cidx + 3] * 255.0f), p);
                    }
                    else
                    {
                        cs.r = (byte)MathF.Round(input[cidx + 1] * 255.0f);
                        cs.g = (byte)MathF.Round(input[cidx + 2] * 255.0f);
                        cs.b = (byte)MathF.Round(input[cidx + 3] * 255.0f);
                    }
                    aidx += 2;
                }
                if (cs.a < 255) opaque = false;
                output.Add(cs);
            }

            // color remains
            while (cidx + 3 < clast)
            {
                cs.offset = input[cidx];
                cs.r = (byte)MathF.Round(input[cidx + 1] * 255.0f);
                cs.g = (byte)MathF.Round(input[cidx + 2] * 255.0f);
                cs.b = (byte)MathF.Round(input[cidx + 3] * 255.0f);
                cs.a = (output.Count > 0) ? output[^1].a : (byte)255;
                if (cs.a < 255) opaque = false;
                output.Add(cs);
                cidx += 4;
            }

            // alpha remains
            while (aidx < inputLast)
            {
                cs.offset = input[aidx];
                cs.a = (byte)MathF.Round(input[aidx + 1] * 255.0f);
                if (cs.a < 255) opaque = false;
                if (output.Count > 0)
                {
                    var last = output[^1];
                    cs.r = last.r;
                    cs.g = last.g;
                    cs.b = last.b;
                }
                else { cs.r = cs.g = cs.b = 255; }
                output.Add(cs);
                aidx += 2;
            }

            color.data = output.ToArray();
            color.input?.Dispose();
            color.input = null;

            return (uint)output.Count;
        }

        public Fill? CreateFill(float frameNo, byte opacity, LottieTween tween, LottieExpressions? exps)
        {
            if (opacity == 0) return null;

            Fill fill;
            var s = start.Evaluate(frameNo, tween, exps);
            var e = end.Evaluate(frameNo, tween, exps);

            // Linear Gradient
            if (gradientId == 1)
            {
                fill = LinearGradient.Gen();
                ((LinearGradient)fill).Linear(s.x, s.y, e.x, e.y);
            }
            // Radial Gradient
            else
            {
                fill = RadialGradient.Gen();
                var w = MathF.Abs(e.x - s.x);
                var h = MathF.Abs(e.y - s.y);
                var r = (w > h) ? TvgMath.MultiplyAdd(0.375f, h, w) : TvgMath.MultiplyAdd(0.375f, w, h);
                var progress = height.Evaluate(frameNo, tween, exps) * 0.01f;

                if (TvgMath.Zero(progress))
                {
                    ((RadialGradient)fill).Radial(s.x, s.y, r, s.x, s.y, 0.0f);
                }
                else
                {
                    var angleDeg = TvgMath.MultiplyAdd(TvgMath.Atan2(e.y - s.y, e.x - s.x), 180.0f / MathConstants.MATH_PI, angle.Evaluate(frameNo, tween, exps));
                    var angleRad = TvgMath.Deg2Rad(angleDeg);
                    var fx = TvgMath.MultiplyAdd(MathF.Cos(angleRad) * progress, r, s.x);
                    var fy = TvgMath.MultiplyAdd(MathF.Sin(angleRad) * progress, r, s.y);
                    ((RadialGradient)fill).Radial(s.x, s.y, r, fx, fy, 0.0f);
                }
            }

            colorStops.Evaluate(frameNo, fill, tween, exps);

            // multiply the current opacity with the fill
            if (opacity < 255)
            {
                fill.GetColorStops(out var stops);
                if (stops != null)
                {
                    for (int i = 0; i < stops.Length; ++i)
                    {
                        stops[i].a = RenderHelper.Multiply(stops[i].a, opacity);
                    }
                }
            }

            return fill;
        }
    }

    public class LottieGradientFill : LottieGradient
    {
        public FillRule rule = FillRule.NonZero;
        public LottieGradientFill() { type = ObjectType.GradientFill; }
    }

    public class LottieGradientStroke : LottieGradient
    {
        public LottieStroke stroke = new();

        public LottieGradientStroke() { type = ObjectType.GradientStroke; }

        public override LottieProperty? FindProperty(ushort ix)
        {
            if (stroke.width.ix == ix) return stroke.width;
            if (stroke.dashattr != null)
            {
                foreach (var v in stroke.dashattr.values)
                    if (v.ix == ix) return v;
            }
            return base.FindProperty(ix);
        }
        public override LottieProperty? Override(LottieProperty prop, bool release)
        {
            LottieProperty? backup = null;
            if (LottieOverride.Apply(stroke.width, prop, ref backup, release)) return backup;
            return base.Override(prop, release);
        }
    }

    public class LottieImage : LottieObject
    {
        public LottieBitmap asset = new();
        public LottieBitmap bitmap => asset;
        public Picture? picture { get => asset.picture; set => asset.picture = value; }
        public bool valid;
        public bool resolved { get => valid; set => valid = value; }

        public LottieImage() { type = ObjectType.Image; }

        public void Release()
        {
            if (picture != null) { picture.Unref(); picture = null; }
        }

        public override LottieProperty? Override(LottieProperty prop, bool release)
        {
            Release();
            LottieProperty? backup = null;
            LottieOverride.Apply(asset, prop, ref backup, release);
            valid = false;
            return backup;
        }

        public Picture Get()
        {
            if (picture != null) return picture;
            picture = Picture.Gen();
            picture.Ref();
            var result = Result.Unknown;
            if (asset.size > 0 && asset.b64Data != null) result = picture.Load(asset.b64Data, asset.size, asset.mimeType);
            else if (asset.external) result = picture.Load(asset.path);
            picture.SetSize(asset.width, asset.height);
            valid = result == Result.Success;
            return picture;
        }

        // Retained compatibility entry point; decoding now happens on first Get.
        public void Prepare(bool external = false) { asset.external = external; }
    }

    public class LottieAudio : LottieObject
    {
        public object? data;
        public string? mimeType;
        public uint size;
        public bool external;

        public LottieAudio() { type = ObjectType.Audio; }
    }

    public class LottieRepeater : LottieObject
    {
        public LottieFloat copies = new();
        public LottieFloat offset = new();
        public LottieVector position = new();
        public LottieFloat rotation = new();
        public LottieScalar scale = new(new Point(100.0f, 100.0f));
        public LottieScalar anchor = new();
        public LottieOpacity startOpacity = new(255);
        public LottieOpacity endOpacity = new(255);
        public bool inorder = true;

        public LottieRepeater() { type = ObjectType.Repeater; }

        public override LottieProperty? FindProperty(ushort ix)
        {
            if (copies.ix == ix) return copies;
            if (offset.ix == ix) return offset;
            if (position.ix == ix) return position;
            if (rotation.ix == ix) return rotation;
            if (scale.ix == ix) return scale;
            if (anchor.ix == ix) return anchor;
            if (startOpacity.ix == ix) return startOpacity;
            if (endOpacity.ix == ix) return endOpacity;
            return null;
        }
        public override LottieProperty? Override(LottieProperty prop, bool release)
        {
            LottieProperty? backup = null;
            if (LottieOverride.Apply(copies, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(offset, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(position, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(rotation, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(scale, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(anchor, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(startOpacity, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(endOpacity, prop, ref backup, release)) return backup;
            return base.Override(prop, release);
        }
    }

    public class LottieOffsetPath : LottieObject
    {
        public LottieFloat offset = new();
        public LottieFloat miterLimit = new(4.0f);
        public StrokeJoin join = StrokeJoin.Miter;

        public LottieOffsetPath() { type = ObjectType.OffsetPath; }
        public override LottieProperty? Override(LottieProperty prop, bool release)
        {
            LottieProperty? backup = null;
            if (LottieOverride.Apply(offset, prop, ref backup, release)) return backup;
            if (LottieOverride.Apply(miterLimit, prop, ref backup, release)) return backup;
            return base.Override(prop, release);
        }
    }

    public class LottiePuckerBloat : LottieObject
    {
        public LottieFloat amount = new();

        public LottiePuckerBloat() { type = ObjectType.PuckerBloat; }
        public override LottieProperty? Override(LottieProperty prop, bool release)
        {
            LottieProperty? backup = null;
            if (LottieOverride.Apply(amount, prop, ref backup, release)) return backup;
            return base.Override(prop, release);
        }
    }

    public class LottieZigZag : LottieObject
    {
        public LottieFloat amplitude = new();
        public LottieInteger frequency = new();
        public LottieInteger point = new(1);

        public LottieZigZag() { type = ObjectType.ZigZag; }
        public override LottieProperty? Override(LottieProperty prop, bool release)
        {
            LottieProperty? backup = null;
            if (LottieOverride.Apply(amplitude, prop, ref backup, release)) return backup;
            return base.Override(prop, release);
        }
    }

    public class LottieGroup : LottieObject
    {
        public LottieRenderPooler<Shape> renderPooler = new();
        public Scene? scene;
        public List<LottieObject> children = new();
        public BlendMethod blendMethod = BlendMethod.Normal;

        public bool reqFragment;
        public bool buildDone;
        public bool trimpath;
        public bool visible;
        public bool allowMerge;

        public LottieGroup(ObjectType type = ObjectType.Group)
        {
            this.type = type;
            reqFragment = false;
            buildDone = false;
            trimpath = false;
            visible = false;
            allowMerge = true;
        }

        public override bool Mergeable() => allowMerge;

        public override LottieProperty? FindProperty(ushort ix)
        {
            foreach (var child in children)
            {
                var p = child.FindProperty(ix);
                if (p != null) return p;
            }
            return null;
        }

        public void Prepare(ObjectType? objType = null)
        {
            if (objType.HasValue) type = objType.Value;

            if (children.Count == 0) return;

            int strokeCnt = 0;
            int fillCnt = 0;

            for (int ci = children.Count - 1; ci >= 0; --ci)
            {
                var child = children[ci];

                if (child.type == ObjectType.Trimpath) trimpath = true;

                // Figure out if this group is a simple path drawing.
                if (allowMerge && !child.Mergeable()) allowMerge = false;

                // Figure out this group has visible contents
                switch (child.type)
                {
                    case ObjectType.Group:
                    {
                        visible |= ((LottieGroup)child).visible;
                        break;
                    }
                    case ObjectType.Rect:
                    case ObjectType.Ellipse:
                    case ObjectType.Path:
                    case ObjectType.Polystar:
                    case ObjectType.Image:
                    case ObjectType.Text:
                    {
                        visible = true;
                        break;
                    }
                    default: break;
                }

                if (reqFragment) continue;

                // Figure out if the rendering context should be fragmented.
                if (child.type == ObjectType.Group && !child.Mergeable())
                {
                    if (strokeCnt > 0 || fillCnt > 0) reqFragment = true;
                }
                else if (child.type == ObjectType.SolidStroke || child.type == ObjectType.GradientStroke)
                {
                    if (strokeCnt > 0) reqFragment = true;
                    else ++strokeCnt;
                }
                else if (child.type == ObjectType.SolidFill || child.type == ObjectType.GradientFill)
                {
                    if (fillCnt > 0) reqFragment = true;
                    else ++fillCnt;
                }
            }

            // Reverse the drawing order if this group has a trimpath.
            if (!trimpath) return;

            for (int i = 0; i < children.Count - 1; )
            {
                var child2 = children[i + 1];
                if (!child2.Mergeable() || child2.type == ObjectType.Transform)
                {
                    i += 2;
                    continue;
                }
                var child = children[i];
                if (!child.Mergeable() || child.type == ObjectType.Transform)
                {
                    i++;
                    continue;
                }
                children[i] = child2;
                children[i + 1] = child;
                i++;
            }
        }

        public LottieObject? Content(ulong id)
        {
            if (this.id == id) return this;
            foreach (var child in children)
            {
                if (child.type == ObjectType.Group || child.type == ObjectType.Layer)
                {
                    if (child is LottieGroup grp)
                    {
                        var ret = grp.Content(id);
                        if (ret != null) return ret;
                    }
                }
                else if (child.id == id) return child;
            }
            return null;
        }
    }

    public class LottieRootLayer : LottieGroup
    {
        public LottieFloat timeRemap = new(-1.0f);
        public float timeStretch = 1.0f;
        public float w, h;
        public float inPoint, outPoint;
        public float startFrame;
        public bool effect;

        // Retain the existing managed names for callers of the model API.
        public float inFrame { get => inPoint; set => inPoint = value; }
        public float outFrame { get => outPoint; set => outPoint = value; }

        public LottieRootLayer(ObjectType type = ObjectType.Composition) : base(type) { }

        public float Remap(LottieComposition comp, float frameNo, LottieExpressions? exp)
        {
            if (timeRemap.frames != null || timeRemap.value >= 0.0f)
            {
                return comp.FrameAtTime(timeRemap.Evaluate(frameNo, exp));
            }
            return (frameNo - startFrame) / timeStretch;
        }

        public LottieLayer? LayerById(ulong id)
        {
            foreach (var child in children)
            {
                if (child.type != ObjectType.Layer) continue;
                var layer = (LottieLayer)child;
                if (layer.id == id) return layer;
            }
            return null;
        }

        public LottieLayer? LayerByIdx(short idx)
        {
            foreach (var child in children)
            {
                if (child.type != ObjectType.Layer) continue;
                var layer = (LottieLayer)child;
                if (layer.ix == idx) return layer;
            }
            return null;
        }

        public override LottieProperty? Override(LottieProperty prop, bool release)
        {
            LottieProperty? backup = null;
            LottieOverride.Apply(timeRemap, prop, ref backup, release);
            return backup;
        }
    }

    public class LottieLayer : LottieRootLayer
    {
        public enum LayerType : byte { Precomp = 0, Solid, Image, Null, Shape, Text, Audio }

        public class AudioControl
        {
            public LottieFloat volume = new(100.0f);
            public float prevVolume = -1.0f;
            public bool prevActive;
        }

        public string? name;
        public LottieLayer? parent;
        public LottieRootLayer? precomp;
        public LottieRootLayer? comp { get => precomp; set => precomp = value; }
        public LottieTransform? transform;
        public List<LottieMask> masks = new();
        public List<LottieEffect> effects = new();
        public LottieLayer? matteTarget;
        public LottieRenderPooler<Shape> statical = new();

        public ulong rid;         // pre-composition reference id
        public short mix = -1;    // index of the matte layer
        public short pix = -1;    // index of the parent layer
        public short ix = -1;     // index of the current layer

        public float cacheFrameNo = -1.0f;
        public Matrix cacheMatrix;
        public byte cacheOpacity;

        public MaskMethod matteType = MaskMethod.None;
        public LayerType layerType = LayerType.Null;
        public bool autoOrient;
        public bool matteSrc;
        private AudioControl? audioCtrl;

        public LottieLayer() : base(ObjectType.Layer) { }

        public override bool Mergeable() => false;

        public void Prepare(RGB32? color = null)
        {
            // if layer is hidden, only useful data is its transform matrix.
            // so force it to be a Null Layer and release all resource.
            if (hidden)
            {
                layerType = LayerType.Null;
                children.Clear();
                return;
            }

            // prepare the viewport clipper
            if (layerType == LayerType.Precomp)
            {
                var clipper = Shape.Gen();
                clipper.AppendRect(0, 0, w, h);
                clipper.Ref();
                statical.pooler.Add(clipper);
            }
            // prepare solid fill in advance if it is a layer type.
            else if (color.HasValue && layerType == LayerType.Solid)
            {
                var solidFill = Shape.Gen();
                solidFill.AppendRect(0, 0, w, h);
                solidFill.SetFill((byte)color.Value.r, (byte)color.Value.g, (byte)color.Value.b);
                solidFill.Ref();
                statical.pooler.Add(solidFill);
            }

            base.Prepare();
        }

        public override LottieProperty? FindProperty(ushort ix)
        {
            if (transform != null)
            {
                var property = transform.FindProperty(ix);
                if (property != null) return property;
            }
            return base.FindProperty(ix);
        }

        public AudioControl Audio() => audioCtrl ??= new AudioControl();

        public LottieEffect? EffectById(ulong id)
        {
            foreach (var e in effects)
            {
                if (id == e.nm || id == e.mn) return e;
            }
            return null;
        }

        public LottieEffect? EffectByIdx(short idx)
        {
            foreach (var e in effects)
            {
                if (idx == e.ix) return e;
            }
            return null;
        }
    }

    public class LottieSlot
    {
        public class Pair
        {
            public LottieObject? obj;
            public LottieProperty? prop;
        }

        public struct Context
        {
            public LottieLayer? layer;
            public LottieObject? parent;
        }

        public Context context;
        public ulong sid;
        public List<Pair> pairs = new();
        public LottieProperty.PropertyType propType;
        public bool overridden;

        public LottieSlot(LottieLayer? layer, LottieObject? parent, ulong sid, LottieObject obj, LottieProperty.PropertyType type)
        {
            context = new Context { layer = layer, parent = parent };
            this.sid = sid;
            propType = type;
            pairs.Add(new Pair { obj = obj });
        }

        public void Reset()
        {
            if (!overridden) return;

            foreach (var pair in pairs)
            {
                pair.obj!.Override(pair.prop!, true);
                pair.prop?.Release();
                pair.prop = null;
            }
            overridden = false;
        }

        public void Apply(LottieProperty prop, bool byDefault = false)
        {
            var release = overridden || byDefault;

            // apply slot object to all targets
            foreach (var pair in pairs)
            {
                var backup = pair.obj!.Override(prop, release);
                if (!release) pair.prop = backup;
            }

            if (!byDefault) overridden = true;
        }
    }

    public class LottieComposition
    {
        public LottieRootLayer? root;
        public string? version;
        public string? name;
        public float w, h;
        public float frameRate;
        public List<LottieObject> assets = new();
        public List<LottieInterpolator> interpolators = new();
        public List<LottieFont> fonts = new();
        public List<LottieSlot> slots = new();
        public List<LottieMarker> markers = new();
        public bool expressions;
        public bool initiated;
        public byte quality = 50;

        public float Duration()
        {
            return FrameCnt() / frameRate;
        }

        public float FrameAtTime(float timeInSec)
        {
            var p = timeInSec / Duration();
            if (p < 0.0f) p = 0.0f;
            return p * FrameCnt();
        }

        public float TimeAtFrame(float frameNo)
        {
            return (frameNo - (root?.inFrame ?? 0)) / frameRate;
        }

        public float FrameCnt()
        {
            if (root == null) return 0;
            return root.outFrame - root.inFrame;
        }

        public LottieLayer? Asset(ulong id)
        {
            foreach (var a in assets)
            {
                if (a.type == LottieObject.ObjectType.Layer && a is LottieLayer layer && layer.id == id) return layer;
            }
            return null;
        }

        public void Clamp(ref float frameNo)
        {
            if (root == null) return;
            frameNo += root.inFrame;
            if (frameNo < root.inFrame) frameNo = root.inFrame;
            if (frameNo > root.outFrame - 1.0f) frameNo = root.outFrame - 1.0f;
        }

        public void Clear()
        {
            if (root?.scene != null) root.scene.Remove();
        }

        public void Release()
        {
            if (!initiated && root?.scene != null) Paint.Rel(root.scene);
            var released = new HashSet<LottieObject>();
            ReleaseObject(root);
            foreach (var asset in assets) ReleaseObject(asset);
            foreach (var font in fonts)
            {
                foreach (var glyph in font.chars) foreach (var child in glyph.children) ReleaseObject(child);
                font.chars.Clear();
                font.Release();
            }
            foreach (var slot in slots)
            {
                foreach (var pair in slot.pairs)
                {
                    pair.prop?.Release();
                    pair.prop = null;
                }
                slot.pairs.Clear();
            }
            assets.Clear(); fonts.Clear(); slots.Clear(); interpolators.Clear(); markers.Clear();
            root = null;

            void ReleaseObject(LottieObject? obj)
            {
                if (obj == null || !released.Add(obj)) return;
                if (obj is LottieGroup group)
                {
                    foreach (var child in group.children) ReleaseObject(child);
                    group.children.Clear();
                    group.renderPooler.Dispose();
                    group.scene = null;
                }
                if (obj is LottieLayer layer)
                {
                    layer.statical.Dispose();
                    layer.masks.Clear();
                    layer.effects.Clear();
                }
                if (obj is LottieShape shape) shape.renderPooler.Dispose();
                if (obj is LottieImage image) { image.Release(); image.asset.Release(); }
                if (obj is LottieGradient gradient) gradient.colorStops.Release();
                if (obj is LottieText text) { text.renderPooler.Dispose(); text.follow?.Release(); text.ranges.Clear(); }
            }
        }
    }
}
