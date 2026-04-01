// Ported from ThorVG/src/loaders/svg/tvgSvgLoader.cpp
// Types, constants, color table, unit parsing, transform parsing, tag lookup tables.

using System;

namespace ThorVG
{
    // Delegate types matching C++ function-pointer typedefs.
    public delegate bool ParseAttributesFunc(string buf, int bufOffset, int bufLength, XmlAttributeCb func, SvgParserContext data);
    public delegate SvgNode? FactoryMethod(SvgParserContext ctx, SvgNode? parent, string buf, int bufOffset, int bufLength, ParseAttributesFunc? func);
    public delegate SvgStyleGradient? GradientFactoryMethod(SvgParserContext ctx, string buf, int bufOffset, int bufLength);
    public delegate void StyleMethodDelegate(SvgParserContext ctx, SvgNode node, string value);

    public enum MatrixState
    {
        Unknown,
        Matrix,
        Translate,
        Rotate,
        Scale,
        SkewX,
        SkewY
    }

    public static partial class SvgLoader
    {
        // --- Unit conversion constants ---
        private const float PX_PER_IN = 96;
        private const float PX_PER_PC = 16;
        private const float PX_PER_PT = 1.333333f;
        private const float PX_PER_MM = 3.779528f;
        private const float PX_PER_CM = 37.79528f;

        // --- Named CSS colors (148 entries, sorted by name) ---
        private static readonly (string name, uint value)[] Colors = {
            ("aliceblue", 0xfff0f8ff),
            ("antiquewhite", 0xfffaebd7),
            ("aqua", 0xff00ffff),
            ("aquamarine", 0xff7fffd4),
            ("azure", 0xfff0ffff),
            ("beige", 0xfff5f5dc),
            ("bisque", 0xffffe4c4),
            ("black", 0xff000000),
            ("blanchedalmond", 0xffffebcd),
            ("blue", 0xff0000ff),
            ("blueviolet", 0xff8a2be2),
            ("brown", 0xffa52a2a),
            ("burlywood", 0xffdeb887),
            ("cadetblue", 0xff5f9ea0),
            ("chartreuse", 0xff7fff00),
            ("chocolate", 0xffd2691e),
            ("coral", 0xffff7f50),
            ("cornflowerblue", 0xff6495ed),
            ("cornsilk", 0xfffff8dc),
            ("crimson", 0xffdc143c),
            ("cyan", 0xff00ffff),
            ("darkblue", 0xff00008b),
            ("darkcyan", 0xff008b8b),
            ("darkgoldenrod", 0xffb8860b),
            ("darkgray", 0xffa9a9a9),
            ("darkgrey", 0xffa9a9a9),
            ("darkgreen", 0xff006400),
            ("darkkhaki", 0xffbdb76b),
            ("darkmagenta", 0xff8b008b),
            ("darkolivegreen", 0xff556b2f),
            ("darkorange", 0xffff8c00),
            ("darkorchid", 0xff9932cc),
            ("darkred", 0xff8b0000),
            ("darksalmon", 0xffe9967a),
            ("darkseagreen", 0xff8fbc8f),
            ("darkslateblue", 0xff483d8b),
            ("darkslategray", 0xff2f4f4f),
            ("darkslategrey", 0xff2f4f4f),
            ("darkturquoise", 0xff00ced1),
            ("darkviolet", 0xff9400d3),
            ("deeppink", 0xffff1493),
            ("deepskyblue", 0xff00bfff),
            ("dimgray", 0xff696969),
            ("dimgrey", 0xff696969),
            ("dodgerblue", 0xff1e90ff),
            ("firebrick", 0xffb22222),
            ("floralwhite", 0xfffffaf0),
            ("forestgreen", 0xff228b22),
            ("fuchsia", 0xffff00ff),
            ("gainsboro", 0xffdcdcdc),
            ("ghostwhite", 0xfff8f8ff),
            ("gold", 0xffffd700),
            ("goldenrod", 0xffdaa520),
            ("gray", 0xff808080),
            ("grey", 0xff808080),
            ("green", 0xff008000),
            ("greenyellow", 0xffadff2f),
            ("honeydew", 0xfff0fff0),
            ("hotpink", 0xffff69b4),
            ("indianred", 0xffcd5c5c),
            ("indigo", 0xff4b0082),
            ("ivory", 0xfffffff0),
            ("khaki", 0xfff0e68c),
            ("lavender", 0xffe6e6fa),
            ("lavenderblush", 0xfffff0f5),
            ("lawngreen", 0xff7cfc00),
            ("lemonchiffon", 0xfffffacd),
            ("lightblue", 0xffadd8e6),
            ("lightcoral", 0xfff08080),
            ("lightcyan", 0xffe0ffff),
            ("lightgoldenrodyellow", 0xfffafad2),
            ("lightgray", 0xffd3d3d3),
            ("lightgrey", 0xffd3d3d3),
            ("lightgreen", 0xff90ee90),
            ("lightpink", 0xffffb6c1),
            ("lightsalmon", 0xffffa07a),
            ("lightseagreen", 0xff20b2aa),
            ("lightskyblue", 0xff87cefa),
            ("lightslategray", 0xff778899),
            ("lightslategrey", 0xff778899),
            ("lightsteelblue", 0xffb0c4de),
            ("lightyellow", 0xffffffe0),
            ("lime", 0xff00ff00),
            ("limegreen", 0xff32cd32),
            ("linen", 0xfffaf0e6),
            ("magenta", 0xffff00ff),
            ("maroon", 0xff800000),
            ("mediumaquamarine", 0xff66cdaa),
            ("mediumblue", 0xff0000cd),
            ("mediumorchid", 0xffba55d3),
            ("mediumpurple", 0xff9370d8),
            ("mediumseagreen", 0xff3cb371),
            ("mediumslateblue", 0xff7b68ee),
            ("mediumspringgreen", 0xff00fa9a),
            ("mediumturquoise", 0xff48d1cc),
            ("mediumvioletred", 0xffc71585),
            ("midnightblue", 0xff191970),
            ("mintcream", 0xfff5fffa),
            ("mistyrose", 0xffffe4e1),
            ("moccasin", 0xffffe4b5),
            ("navajowhite", 0xffffdead),
            ("navy", 0xff000080),
            ("oldlace", 0xfffdf5e6),
            ("olive", 0xff808000),
            ("olivedrab", 0xff6b8e23),
            ("orange", 0xffffa500),
            ("orangered", 0xffff4500),
            ("orchid", 0xffda70d6),
            ("palegoldenrod", 0xffeee8aa),
            ("palegreen", 0xff98fb98),
            ("paleturquoise", 0xffafeeee),
            ("palevioletred", 0xffd87093),
            ("papayawhip", 0xffffefd5),
            ("peachpuff", 0xffffdab9),
            ("peru", 0xffcd853f),
            ("pink", 0xffffc0cb),
            ("plum", 0xffdda0dd),
            ("powderblue", 0xffb0e0e6),
            ("purple", 0xff800080),
            ("red", 0xffff0000),
            ("rosybrown", 0xffbc8f8f),
            ("royalblue", 0xff4169e1),
            ("saddlebrown", 0xff8b4513),
            ("salmon", 0xfffa8072),
            ("sandybrown", 0xfff4a460),
            ("seagreen", 0xff2e8b57),
            ("seashell", 0xfffff5ee),
            ("sienna", 0xffa0522d),
            ("silver", 0xffc0c0c0),
            ("skyblue", 0xff87ceeb),
            ("slateblue", 0xff6a5acd),
            ("slategray", 0xff708090),
            ("slategrey", 0xff708090),
            ("snow", 0xfffffafa),
            ("springgreen", 0xff00ff7f),
            ("steelblue", 0xff4682b4),
            ("tan", 0xffd2b48c),
            ("teal", 0xff008080),
            ("thistle", 0xffd8bfd8),
            ("tomato", 0xffff6347),
            ("turquoise", 0xff40e0d0),
            ("violet", 0xffee82ee),
            ("wheat", 0xfff5deb3),
            ("white", 0xffffffff),
            ("whitesmoke", 0xfff5f5f5),
            ("yellow", 0xffffff00),
            ("yellowgreen", 0xff9acd32)
        };

        // --- Alignment tags ---
        private static readonly (AspectRatioAlign align, string tag)[] AlignTags = {
            (AspectRatioAlign.XMinYMin, "xMinYMin"),
            (AspectRatioAlign.XMidYMin, "xMidYMin"),
            (AspectRatioAlign.XMaxYMin, "xMaxYMin"),
            (AspectRatioAlign.XMinYMid, "xMinYMid"),
            (AspectRatioAlign.XMidYMid, "xMidYMid"),
            (AspectRatioAlign.XMaxYMid, "xMaxYMid"),
            (AspectRatioAlign.XMinYMax, "xMinYMax"),
            (AspectRatioAlign.XMidYMax, "xMidYMax"),
            (AspectRatioAlign.XMaxYMax, "xMaxYMax")
        };

        // --- Line cap tags ---
        private static readonly (StrokeCap lineCap, string tag)[] LineCapTags = {
            (StrokeCap.Butt, "butt"),
            (StrokeCap.Round, "round"),
            (StrokeCap.Square, "square")
        };

        // --- Line join tags ---
        private static readonly (StrokeJoin lineJoin, string tag)[] LineJoinTags = {
            (StrokeJoin.Miter, "miter"),
            (StrokeJoin.Round, "round"),
            (StrokeJoin.Bevel, "bevel")
        };

        // --- Fill rule tags ---
        private static readonly (FillRule fillRule, string tag)[] FillRuleTags = {
            (FillRule.EvenOdd, "evenodd")
        };

        // --- Matrix tags ---
        private static readonly (string tag, MatrixState state)[] MatrixTags = {
            ("matrix", MatrixState.Matrix),
            ("translate", MatrixState.Translate),
            ("rotate", MatrixState.Rotate),
            ("scale", MatrixState.Scale),
            ("skewX", MatrixState.SkewX),
            ("skewY", MatrixState.SkewY)
        };

        // --- Style tag table ---
        private static readonly (string tag, StyleMethodDelegate handler, SvgStyleFlags flag)[] StyleTags = {
            ("color", HandleColorAttr, SvgStyleFlags.Color),
            ("fill", HandleFillAttr, SvgStyleFlags.Fill),
            ("fill-rule", HandleFillRuleAttr, SvgStyleFlags.FillRule),
            ("fill-opacity", HandleFillOpacityAttr, SvgStyleFlags.FillOpacity),
            ("opacity", HandleOpacityAttr, SvgStyleFlags.Opacity),
            ("stroke", HandleStrokeAttr, SvgStyleFlags.Stroke),
            ("stroke-width", HandleStrokeWidthAttr, SvgStyleFlags.StrokeWidth),
            ("stroke-linejoin", HandleStrokeLineJoinAttr, SvgStyleFlags.StrokeLineJoin),
            ("stroke-miterlimit", HandleStrokeMiterlimitAttr, SvgStyleFlags.StrokeMiterlimit),
            ("stroke-linecap", HandleStrokeLineCapAttr, SvgStyleFlags.StrokeLineCap),
            ("stroke-opacity", HandleStrokeOpacityAttr, SvgStyleFlags.StrokeOpacity),
            ("stroke-dasharray", HandleStrokeDashArrayAttr, SvgStyleFlags.StrokeDashArray),
            ("stroke-dashoffset", HandleStrokeDashOffsetAttr, SvgStyleFlags.StrokeDashOffset),
            ("transform", HandleTransformAttr, SvgStyleFlags.Transform),
            ("clip-path", HandleClipPathAttr, SvgStyleFlags.ClipPath),
            ("mask", HandleMaskAttr, SvgStyleFlags.Mask),
            ("mask-type", HandleMaskTypeAttr, SvgStyleFlags.MaskType),
            ("display", HandleDisplayAttr, SvgStyleFlags.Display),
            ("paint-order", HandlePaintOrderAttr, SvgStyleFlags.PaintOrder),
            ("filter", HandleFilterAttr, SvgStyleFlags.Filter),
            ("mix-blend-mode", HandleMixBlendModeAttr, SvgStyleFlags.BlendMode)
        };

        // --- Blend mode tags ---
        private static readonly (BlendMethod blendMode, string tag)[] BlendModeTags = {
            (BlendMethod.Multiply, "multiply"),
            (BlendMethod.Screen, "screen"),
            (BlendMethod.Overlay, "overlay"),
            (BlendMethod.Darken, "darken"),
            (BlendMethod.Lighten, "lighten"),
            (BlendMethod.ColorDodge, "color-dodge"),
            (BlendMethod.ColorBurn, "color-burn"),
            (BlendMethod.HardLight, "hard-light"),
            (BlendMethod.SoftLight, "soft-light"),
            (BlendMethod.Difference, "difference"),
            (BlendMethod.Exclusion, "exclusion"),
            (BlendMethod.Hue, "hue"),
            (BlendMethod.Saturation, "saturation"),
            (BlendMethod.Color, "color"),
            (BlendMethod.Luminosity, "luminosity")
        };

        private static readonly string[] IgnoreUnsupportedLogElements = {
            "feOffset", "feColorMatrix", "feFlood", "feComposite", "feMerge", "feMergeNode",
            "feTurbulence", "feBlend", "feImage", "feTile",
            "switch", "foreignObject", "desc", "title", "metadata", "marker"
        };

        // --- Helper: is this an unsupported element we ignore? ---
        private static bool IsIgnoreUnsupportedLogElements(string tag)
        {
            for (int i = 0; i < IgnoreUnsupportedLogElements.Length; i++)
            {
                if (SvgHelper.StrAs(tag, IgnoreUnsupportedLogElements[i])) return true;
            }
            return false;
        }

        // --- CopyId ---
        private static string? CopyId(string? str)
        {
            if (string.IsNullOrEmpty(str)) return null;
            return str;
        }

        // --- ParseNumber ---
        private static bool ParseNumber(string content, ref int pos, out float number)
        {
            int startPos = pos;
            pos = SvgUtil.SkipWhiteSpace(content, pos, -1);
            number = TvgStr.ToFloat(content, ref pos);
            if (pos == startPos) return false;
            pos = SvgUtil.SkipWhiteSpaceAndComma(content, pos);
            return true;
        }

        // --- ToFloat (unit-aware) ---
        private static float ToFloat(SvgParser? svgParse, string str, SvgParserLengthType type)
        {
            int pos = 0;
            float parsedValue = TvgStr.ToFloat(str, ref pos);

            if (str.Contains("cm")) parsedValue *= PX_PER_CM;
            else if (str.Contains("mm")) parsedValue *= PX_PER_MM;
            else if (str.Contains("pt")) parsedValue *= PX_PER_PT;
            else if (str.Contains("pc")) parsedValue *= PX_PER_PC;
            else if (str.Contains("in")) parsedValue *= PX_PER_IN;
            else if (str.Contains("%") && svgParse != null)
            {
                if (type == SvgParserLengthType.Vertical) parsedValue = (parsedValue / 100.0f) * svgParse.global.h;
                else if (type == SvgParserLengthType.Horizontal) parsedValue = (parsedValue / 100.0f) * svgParse.global.w;
                else if (type == SvgParserLengthType.Diagonal)
                    parsedValue = (MathF.Sqrt(svgParse.global.w * svgParse.global.w + svgParse.global.h * svgParse.global.h) / MathF.Sqrt(2.0f)) * (parsedValue / 100.0f);
                else
                {
                    float max = svgParse.global.w > svgParse.global.h ? svgParse.global.w : svgParse.global.h;
                    parsedValue = (parsedValue / 100.0f) * max;
                }
            }

            return parsedValue;
        }

        // --- GradientToFloat ---
        private static float GradientToFloat(SvgParser? svgParse, string str, ref bool isPercentage)
        {
            int pos = 0;
            var parsedValue = TvgStr.ToFloat(str, ref pos);
            isPercentage = false;

            if (str.Contains("%"))
            {
                parsedValue = parsedValue / 100.0f;
                isPercentage = true;
            }
            else if (str.Contains("cm")) parsedValue *= PX_PER_CM;
            else if (str.Contains("mm")) parsedValue *= PX_PER_MM;
            else if (str.Contains("pt")) parsedValue *= PX_PER_PT;
            else if (str.Contains("pc")) parsedValue *= PX_PER_PC;
            else if (str.Contains("in")) parsedValue *= PX_PER_IN;

            return parsedValue;
        }

        // --- ToOffset ---
        private static float ToOffset(string str)
        {
            int pos = 0;
            var parsedValue = TvgStr.ToFloat(str, ref pos);

            pos = SvgUtil.SkipWhiteSpace(str, pos, -1);

            if (str.Contains("%"))
            {
                parsedValue = parsedValue / 100.0f;
            }

            return parsedValue;
        }

        // --- ToOpacity ---
        private static int ToOpacity(string str)
        {
            int pos = 0;
            var opacity = TvgStr.ToFloat(str, ref pos);

            if (pos < str.Length)
            {
                if (str[pos] == '%') return (int)Math.Round(opacity * 2.55f);
                return (int)Math.Round(opacity * 255);
            }
            return (int)Math.Round(opacity * 255);
        }

        // --- ToMaskType ---
        private static SvgMaskType ToMaskType(string str)
        {
            return SvgHelper.StrAs(str, "Alpha") ? SvgMaskType.Alpha : SvgMaskType.Luminance;
        }

        // --- ToBlendMode ---
        private static BlendMethod ToBlendMode(string str)
        {
            for (int i = 0; i < BlendModeTags.Length; i++)
            {
                if (SvgHelper.StrAs(str, BlendModeTags[i].tag)) return BlendModeTags[i].blendMode;
            }
            return BlendMethod.Normal;
        }

        // --- ToPaintOrder ---
        private static bool ToPaintOrder(string str)
        {
            var position = 1;
            var strokePosition = 0;
            var fillPosition = 0;
            int i = 0;

            while (i < str.Length)
            {
                i = SvgUtil.SkipWhiteSpace(str, i, -1);
                if (i >= str.Length) break;

                if (str.Length - i >= 4 && str.Substring(i, 4) == "fill")
                {
                    fillPosition = position++;
                    i += 4;
                }
                else if (str.Length - i >= 6 && str.Substring(i, 6) == "stroke")
                {
                    strokePosition = position++;
                    i += 6;
                }
                else if (str.Length - i >= 7 && str.Substring(i, 7) == "markers")
                {
                    i += 7;
                }
                else
                {
                    return ToPaintOrder("fill stroke");
                }
            }

            if (fillPosition == 0) fillPosition = position++;
            if (strokePosition == 0) strokePosition = position++;

            return fillPosition < strokePosition;
        }

        // --- ToLineCap ---
        private static StrokeCap ToLineCap(string str)
        {
            for (int i = 0; i < LineCapTags.Length; i++)
            {
                if (SvgHelper.StrAs(str, LineCapTags[i].tag)) return LineCapTags[i].lineCap;
            }
            return StrokeCap.Butt;
        }

        // --- ToLineJoin ---
        private static StrokeJoin ToLineJoin(string str)
        {
            for (int i = 0; i < LineJoinTags.Length; i++)
            {
                if (SvgHelper.StrAs(str, LineJoinTags[i].tag)) return LineJoinTags[i].lineJoin;
            }
            return StrokeJoin.Miter;
        }

        // --- ToFillRule ---
        private static FillRule ToFillRule(string str)
        {
            for (int i = 0; i < FillRuleTags.Length; i++)
            {
                if (SvgHelper.StrAs(str, FillRuleTags[i].tag)) return FillRuleTags[i].fillRule;
            }
            return FillRule.NonZero;
        }

        // --- ParseAspectRatio ---
        private static void ParseAspectRatio(string content, ref int pos, ref AspectRatioAlign align, ref AspectRatioMeetOrSlice meetOrSlice)
        {
            var remaining = content.Substring(pos).Trim();
            if (SvgHelper.StrAs(remaining, "none"))
            {
                align = AspectRatioAlign.None;
                return;
            }

            for (int i = 0; i < AlignTags.Length; i++)
            {
                if (remaining.StartsWith(AlignTags[i].tag, StringComparison.Ordinal))
                {
                    align = AlignTags[i].align;
                    remaining = remaining.Substring(8).TrimStart();
                    break;
                }
            }

            if (SvgHelper.StrAs(remaining, "meet"))
            {
                meetOrSlice = AspectRatioMeetOrSlice.Meet;
            }
            else if (SvgHelper.StrAs(remaining, "slice"))
            {
                meetOrSlice = AspectRatioMeetOrSlice.Slice;
            }
        }

        // --- ParseDashArray ---
        private static void ParseDashArray(SvgParserContext ctx, string str, SvgDash dash)
        {
            if (str.StartsWith("none", StringComparison.Ordinal)) return;

            int pos = 0;
            while (pos < str.Length)
            {
                pos = SvgUtil.SkipWhiteSpaceAndComma(str, pos);
                if (pos >= str.Length) break;
                int startPos = pos;
                var parsedValue = TvgStr.ToFloat(str, ref pos);
                if (pos == startPos) break;
                if (parsedValue < 0.0f)
                {
                    dash.array.Clear();
                    return;
                }
                if (pos < str.Length && str[pos] == '%')
                {
                    pos++;
                    var gw = ctx.svgParse!.global.w;
                    var gh = ctx.svgParse.global.h;
                    parsedValue = (MathF.Sqrt(gw * gw + gh * gh) / MathF.Sqrt(2.0f)) * (parsedValue / 100.0f);
                }
                dash.array.Add(parsedValue);
            }
        }

        // --- IdFromUrl ---
        private static string? IdFromUrl(string url)
        {
            int openParen = url.IndexOf('(');
            int closeParen = url.IndexOf(')');
            if (openParen < 0 || closeParen < 0 || openParen >= closeParen) return null;

            int hash = url.IndexOf('#', openParen);
            if (hash < 0 || hash >= closeParen) return null;

            int start = hash + 1;
            int end = closeParen - 1;

            // Trim trailing spaces and quotes
            while (end > start && (url[end] == ' ' || url[end] == '\'' || url[end] == '"')) end--;

            // Verify: no spaces or quotes in the middle
            for (int i = start; i <= end; i++)
            {
                if (url[i] == ' ' || url[i] == '\'') return null;
            }

            return url.Substring(start, end - start + 1);
        }

        // --- IdFromHref ---
        private static string? IdFromHref(string href)
        {
            int pos = SvgUtil.SkipWhiteSpace(href, 0, -1);
            if (pos < href.Length && href[pos] == '#') pos++;
            return href.Substring(pos);
        }

        // --- ParseColor: parse a single component from rgb() ---
        private static byte ParseColorComponent(string value, ref int pos)
        {
            var r = TvgStr.ToFloat(value, ref pos);
            pos = SvgUtil.SkipWhiteSpace(value, pos, -1);
            if (pos < value.Length && value[pos] == '%')
            {
                r = 255 * r / 100;
                pos++;
            }
            pos = SvgUtil.SkipWhiteSpace(value, pos, -1);

            if (r < 0 || r > 255) return 0;
            return (byte)Math.Round(r);
        }

        private static bool IsHexDigit(char c) =>
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

        private static byte HexToByte(char high, char low)
        {
            return (byte)(HexVal(high) * 16 + HexVal(low));
        }

        private static int HexVal(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return 0;
        }

        // --- ToColor ---
        private static bool ToColor(string str, ref byte r, ref byte g, ref byte b, ref string? paintUrl)
        {
            var len = str.Length;

            if (len == 4 && str[0] == '#')
            {
                if (IsHexDigit(str[1]) && IsHexDigit(str[2]) && IsHexDigit(str[3]))
                {
                    r = HexToByte(str[1], str[1]);
                    g = HexToByte(str[2], str[2]);
                    b = HexToByte(str[3], str[3]);
                }
                return true;
            }
            else if (len == 7 && str[0] == '#')
            {
                if (IsHexDigit(str[1]) && IsHexDigit(str[2]) && IsHexDigit(str[3]) &&
                    IsHexDigit(str[4]) && IsHexDigit(str[5]) && IsHexDigit(str[6]))
                {
                    r = HexToByte(str[1], str[2]);
                    g = HexToByte(str[3], str[4]);
                    b = HexToByte(str[5], str[6]);
                }
                return true;
            }
            else if (len >= 10 && str.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) && str[len - 1] == ')')
            {
                int pos = 4;
                var tr = ParseColorComponent(str, ref pos);
                if (pos < str.Length && str[pos] == ',')
                {
                    pos++;
                    var tg = ParseColorComponent(str, ref pos);
                    if (pos < str.Length && str[pos] == ',')
                    {
                        pos++;
                        var tb = ParseColorComponent(str, ref pos);
                        if (pos < str.Length && str[pos] == ')')
                        {
                            r = tr;
                            g = tg;
                            b = tb;
                        }
                    }
                }
                return true;
            }
            else if (len >= 3 && str.StartsWith("url", StringComparison.Ordinal))
            {
                paintUrl = IdFromUrl(str.Substring(3));
                return true;
            }
            else if (len >= 10 && str.StartsWith("hsl(", StringComparison.OrdinalIgnoreCase) && str[len - 1] == ')')
            {
                // Parse HSL
                int pos = 4;
                pos = SvgUtil.SkipWhiteSpace(str, pos, -1);
                float h_val = TvgStr.ToFloat(str, ref pos);
                pos = SvgUtil.SkipWhiteSpaceAndComma(str, pos);
                pos = SvgUtil.SkipWhiteSpace(str, pos, -1);
                float s_val = TvgStr.ToFloat(str, ref pos);
                if (pos < str.Length && str[pos] == '%')
                {
                    s_val /= 100.0f;
                    pos++;
                }
                pos = SvgUtil.SkipWhiteSpaceAndComma(str, pos);
                pos = SvgUtil.SkipWhiteSpace(str, pos, -1);
                float l_val = TvgStr.ToFloat(str, ref pos);
                if (pos < str.Length && str[pos] == '%')
                {
                    l_val /= 100.0f;
                    pos++;
                }
                s_val = Math.Clamp(s_val, 0.0f, 1.0f);
                l_val = Math.Clamp(l_val, 0.0f, 1.0f);
                TvgColor.Hsl2Rgb(h_val, s_val, l_val, out r, out g, out b);
                return true;
            }
            else
            {
                // Handle named color
                for (int i = 0; i < Colors.Length; i++)
                {
                    if (string.Equals(Colors[i].name, str, StringComparison.OrdinalIgnoreCase))
                    {
                        var val = Colors[i].value;
                        r = (byte)((val >> 16) & 0xFF);
                        g = (byte)((val >> 8) & 0xFF);
                        b = (byte)(val & 0xFF);
                        return true;
                    }
                }
            }
            return false;
        }

        // Overload without ref paintUrl
        private static bool ToColorNoPaint(string str, ref byte r, ref byte g, ref byte b)
        {
            string? dummy = null;
            return ToColor(str, ref r, ref g, ref b, ref dummy);
        }

        // --- ParseNumbersArray ---
        private static int ParseNumbersArray(string str, ref int pos, float[] points, int maxLen)
        {
            int count = 0;
            pos = SvgUtil.SkipWhiteSpace(str, pos, -1);
            while (count < maxLen && pos < str.Length &&
                   (char.IsDigit(str[pos]) || str[pos] == '-' || str[pos] == '+' || str[pos] == '.'))
            {
                int startPos = pos;
                points[count] = TvgStr.ToFloat(str, ref pos);
                if (pos == startPos) break;
                count++;
                pos = SvgUtil.SkipWhiteSpaceAndComma(str, pos);
                pos = SvgUtil.SkipWhiteSpace(str, pos, -1);
            }
            return count;
        }

        // --- ParseTransformationMatrix ---
        private static Matrix? ParseTransformationMatrix(string value)
        {
            var matrix = new Matrix(1, 0, 0, 0, 1, 0, 0, 0, 1); // identity

            float[] points = new float[8];
            int pos = 0;
            int end = value.Length;

            while (pos < end)
            {
                if (char.IsWhiteSpace(value[pos]) || value[pos] == ',')
                {
                    pos++;
                    continue;
                }

                var state = MatrixState.Unknown;
                for (int i = 0; i < MatrixTags.Length; i++)
                {
                    var tag = MatrixTags[i].tag;
                    if (pos + tag.Length <= end && value.Substring(pos, tag.Length) == tag)
                    {
                        state = MatrixTags[i].state;
                        pos += tag.Length;
                        break;
                    }
                }
                if (state == MatrixState.Unknown) return null;

                pos = SvgUtil.SkipWhiteSpace(value, pos, end);
                if (pos >= end || value[pos] != '(') return null;
                pos++;

                int ptCount = ParseNumbersArray(value, ref pos, points, 8);
                if (pos >= end || value[pos] != ')') return null;
                pos++;

                if (state == MatrixState.Matrix)
                {
                    if (ptCount != 6) return null;
                    var tmp = new Matrix(points[0], points[2], points[4], points[1], points[3], points[5], 0, 0, 1);
                    matrix = MatrixMultiply(matrix, tmp);
                }
                else if (state == MatrixState.Translate)
                {
                    if (ptCount == 1)
                    {
                        var tmp = new Matrix(1, 0, points[0], 0, 1, 0, 0, 0, 1);
                        matrix = MatrixMultiply(matrix, tmp);
                    }
                    else if (ptCount == 2)
                    {
                        var tmp = new Matrix(1, 0, points[0], 0, 1, points[1], 0, 0, 1);
                        matrix = MatrixMultiply(matrix, tmp);
                    }
                    else return null;
                }
                else if (state == MatrixState.Rotate)
                {
                    points[0] = points[0] % 360.0f;
                    if (points[0] < 0) points[0] += 360.0f;
                    var c = MathF.Cos(TvgMath.Deg2Rad(points[0]));
                    var s = MathF.Sin(TvgMath.Deg2Rad(points[0]));
                    if (ptCount == 1)
                    {
                        var tmp = new Matrix(c, -s, 0, s, c, 0, 0, 0, 1);
                        matrix = MatrixMultiply(matrix, tmp);
                    }
                    else if (ptCount == 3)
                    {
                        var tmp = new Matrix(1, 0, points[1], 0, 1, points[2], 0, 0, 1);
                        matrix = MatrixMultiply(matrix, tmp);
                        tmp = new Matrix(c, -s, 0, s, c, 0, 0, 0, 1);
                        matrix = MatrixMultiply(matrix, tmp);
                        tmp = new Matrix(1, 0, -points[1], 0, 1, -points[2], 0, 0, 1);
                        matrix = MatrixMultiply(matrix, tmp);
                    }
                    else return null;
                }
                else if (state == MatrixState.Scale)
                {
                    if (ptCount < 1 || ptCount > 2) return null;
                    var sx = points[0];
                    var sy = sx;
                    if (ptCount == 2) sy = points[1];
                    var tmp = new Matrix(sx, 0, 0, 0, sy, 0, 0, 0, 1);
                    matrix = MatrixMultiply(matrix, tmp);
                }
                else if (state == MatrixState.SkewX)
                {
                    if (ptCount != 1) return null;
                    var deg = MathF.Tan(TvgMath.Deg2Rad(points[0]));
                    var tmp = new Matrix(1, deg, 0, 0, 1, 0, 0, 0, 1);
                    matrix = MatrixMultiply(matrix, tmp);
                }
                else if (state == MatrixState.SkewY)
                {
                    if (ptCount != 1) return null;
                    var deg = MathF.Tan(TvgMath.Deg2Rad(points[0]));
                    var tmp = new Matrix(1, 0, 0, deg, 1, 0, 0, 0, 1);
                    matrix = MatrixMultiply(matrix, tmp);
                }
            }
            return matrix;
        }

        // --- Matrix multiply helper ---
        private static Matrix MatrixMultiply(in Matrix a, in Matrix b)
        {
            return new Matrix(
                a.e11 * b.e11 + a.e12 * b.e21 + a.e13 * b.e31,
                a.e11 * b.e12 + a.e12 * b.e22 + a.e13 * b.e32,
                a.e11 * b.e13 + a.e12 * b.e23 + a.e13 * b.e33,
                a.e21 * b.e11 + a.e22 * b.e21 + a.e23 * b.e31,
                a.e21 * b.e12 + a.e22 * b.e22 + a.e23 * b.e32,
                a.e21 * b.e13 + a.e22 * b.e23 + a.e23 * b.e33,
                a.e31 * b.e11 + a.e32 * b.e21 + a.e33 * b.e31,
                a.e31 * b.e12 + a.e32 * b.e22 + a.e33 * b.e32,
                a.e31 * b.e13 + a.e32 * b.e23 + a.e33 * b.e33
            );
        }

        // --- ToXmlSpace ---
        private static SvgXmlSpace ToXmlSpace(string str)
        {
            if (SvgHelper.StrAs(str, "default")) return SvgXmlSpace.Default;
            if (SvgHelper.StrAs(str, "preserve")) return SvgXmlSpace.Preserve;
            return SvgXmlSpace.None;
        }

        // --- ParseSpreadValue ---
        private static FillSpread ParseSpreadValue(string value)
        {
            if (SvgHelper.StrAs(value, "reflect")) return FillSpread.Reflect;
            if (SvgHelper.StrAs(value, "repeat")) return FillSpread.Repeat;
            return FillSpread.Pad;
        }

        // --- Unquote ---
        private static string Unquote(string str)
        {
            if (str.Length >= 2 && str[0] == '\'' && str[str.Length - 1] == '\'')
                return str.Substring(1, str.Length - 2);
            return str;
        }

        // --- SrcFromUrl ---
        private static (string? src, int len) SrcFromUrl(string url)
        {
            int open = url.IndexOf('(');
            int close = url.IndexOf(')');
            if (open < 0 || close < 0 || open >= close) return (null, 0);

            int sq = url.IndexOf('\'', open);
            if (sq < 0 || sq >= close) return (null, 0);
            int start = sq + 1;

            int sq2 = url.IndexOf('\'', start);
            if (sq2 < 0 || sq2 == start) return (null, 0);
            int end = sq2 - 1;

            while (start < end && url[start] == ' ') start++;
            while (start < end && url[end] == ' ') end--;

            var len = end - start + 1;
            return (url.Substring(start, len), len);
        }

        // --- ParseGaussianBlurStdDeviation ---
        private static void ParseGaussianBlurStdDeviation(string content, ref float x, ref float y)
        {
            int pos = 0;
            float[] deviation = { 0, 0 };
            int n = 0;

            while (pos < content.Length && n < 2)
            {
                pos = SvgUtil.SkipWhiteSpaceAndComma(content, pos);
                int startPos = pos;
                var parsedValue = TvgStr.ToFloat(content, ref pos);
                if (pos == startPos) break;
                if (parsedValue < 0.0f) break;
                deviation[n++] = parsedValue;
            }

            x = deviation[0];
            y = n == 1 ? deviation[0] : deviation[1];
        }
    }
}
