// Ported from pinned ThorVG/src/loaders/svg/tvgSvgLoader.cpp and tvgSvgCommon.h.
using System;
using System.Globalization;

namespace ThorVG
{
    public enum SvgFontWeight : short
    {
        Invalid = 0, Inherit = -1, Bolder = -2, Lighter = -3,
        Weight100 = 100, Weight200 = 200, Weight300 = 300, Normal = 400,
        Weight500 = 500, Weight600 = 600, Bold = 700, Weight800 = 800, Weight900 = 900
    }

    public static partial class SvgLoader
    {
        private static SvgFontWeight ToFontWeight(string value)
        {
            switch (value)
            {
                case "normal": return SvgFontWeight.Normal;
                case "bold": return SvgFontWeight.Bold;
                case "inherit": return SvgFontWeight.Inherit;
                case "bolder": return SvgFontWeight.Bolder;
                case "lighter": return SvgFontWeight.Lighter;
            }
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int weight) && weight >= 100 && weight <= 900 && weight % 100 == 0)
                return (SvgFontWeight)weight;
            return SvgFontWeight.Invalid;
        }
        private static void HandleFontWeightAttr(SvgParserContext ctx, SvgNode node, string value)
        {
            node.style!.fontWeight = ToFontWeight(value); node.style.flags |= SvgStyleFlags.FontWeight;
        }
        private static void HandleDominantBaselineAttr(SvgParserContext ctx, SvgNode node, string value)
        {
            node.style!.dominantBaseline = ToBaseline(value); node.style.flags |= SvgStyleFlags.DominantBaseline;
        }

        private static float ParseSpacing(string value, out bool relative)
        {
            relative = false;
            if (value == "normal") return 0;
            int pos = 0; float parsed = TvgStr.ToFloat(value, ref pos);
            bool percentage = IsPercentage(value, pos);
            int end = value.Length;
            while (end > 0 && SvgUtil.IsWhiteSpace(value[end - 1])) --end;
            int start = end;
            while (start > 0 && ((value[start - 1] >= 'a' && value[start - 1] <= 'z') || (value[start - 1] >= 'A' && value[start - 1] <= 'Z'))) --start;
            relative = percentage || (end - start == 2 && (value.AsSpan(start, 2).SequenceEqual("em") || value.AsSpan(start, 2).SequenceEqual("ex")));
            return parsed * (percentage ? .01f : UnitScale(value, 1));
        }
        private static void HandleLetterSpacingAttr(SvgParserContext ctx, SvgNode node, string value)
        {
            var style = node.style!; style.flags |= SvgStyleFlags.LetterSpacing;
            style.letterSpacing = ParseSpacing(value, out style.letterSpacingRelative);
        }
        private static void HandleWordSpacingAttr(SvgParserContext ctx, SvgNode node, string value)
        {
            var style = node.style!; style.flags |= SvgStyleFlags.WordSpacing;
            style.wordSpacing = ParseSpacing(value, out style.wordSpacingRelative);
        }
        private static bool ValidTypographyStyle(SvgStyleFlags flag, string value)
        {
            if (flag == SvgStyleFlags.FontWeight) return ToFontWeight(value) != SvgFontWeight.Invalid;
            if (flag == SvgStyleFlags.DominantBaseline)
                return value != "text-top" && value != "text-bottom" && (ToBaseline(value) != SvgBaseline.Auto || value == "auto");
            if (flag == SvgStyleFlags.LetterSpacing || flag == SvgStyleFlags.WordSpacing)
            {
                int end = 0; TvgStr.ToFloat(value, ref end);
                return end != 0 || value == "normal";
            }
            return true;
        }
    }
}
