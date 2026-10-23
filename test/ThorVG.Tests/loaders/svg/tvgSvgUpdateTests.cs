using System;
using System.Collections.Generic;
using Xunit;

namespace ThorVG.Tests
{
    public class tvgSvgUpdateTests
    {
        private static SvgNode Find(SvgNode node, string id)
        {
            if (node.id == id) return node;
            foreach (var child in node.child)
            {
                var found = FindOrNull(child, id); if (found != null) return found;
            }
            throw new InvalidOperationException("Missing fixture node " + id);
        }
        private static SvgNode? FindOrNull(SvgNode node, string id)
        {
            if (node.id == id) return node;
            foreach (var child in node.child) { var found = FindOrNull(child, id); if (found != null) return found; }
            return null;
        }

        [Fact]
        public void CssRepeatedRules_PreserveImportanceAndClearDashArray()
        {
            var root = SvgLoader.Parse("<svg width='128' height='128'><style>rect { fill: red !important; } .a { stroke-dasharray:4,2; }</style><style>rect { fill: yellow; } .a { stroke-dasharray:none; }</style><rect id='r' class='a' width='20' height='20' style='fill:green'/></svg>")!;
            var style = Find(root, "r").style!;
            Assert.Equal((byte)255, style.fill.paint.color.r); Assert.Equal((byte)0, style.fill.paint.color.g);
            Assert.True((style.flagsImportance & SvgStyleFlags.Fill) != 0);
            Assert.True((style.stroke.flags & SvgStrokeFlags.Dash) != 0); Assert.Empty(style.stroke.dash.array);
        }

        [Fact]
        public void TextFontSizePrescan_MakesRelativeCoordinatesOrderIndependent()
        {
            var root = SvgLoader.Parse("<svg width='128' height='128'><text font-size='20'><tspan id='a' dx='.5em' font-size='150%'>A</tspan><tspan id='b' font-size='150%' dx='.5em'>B</tspan></text></svg>")!;
            Assert.Equal(30, Find(root, "a").text.fontSize); Assert.Equal(15, Find(root, "a").text.dx);
            Assert.Equal(Find(root, "a").text.dx, Find(root, "b").text.dx);
        }

        [Fact]
        public void Typography_ResolvesInheritedWeightsAndFontRelativeSpacing()
        {
            var root = SvgLoader.Parse("<svg width='128' height='128'><g font-weight='bold' dominant-baseline='central'><text id='t' font-size='20' letter-spacing='.25em' word-spacing='50%' font-weight='lighter'>A B</text></g></svg>")!;
            var style = Find(root, "t").style!;
            Assert.Equal(SvgFontWeight.Normal, style.fontWeight); Assert.Equal(SvgBaseline.Central, style.dominantBaseline);
            Assert.Equal(5, style.letterSpacing); Assert.Equal(10, style.wordSpacing);
            Assert.False(style.letterSpacingRelative); Assert.False(style.wordSpacingRelative);
        }

        [Theory]
        [InlineData("rgba(240,20,80,0.5)", 240, 20, 80, 128)]
        [InlineData("mediumpurple", 147, 112, 219, 255)]
        [InlineData("palevioletred", 219, 112, 147, 255)]
        [InlineData("#ggg", 255, 0, 0, 255)]
        [InlineData("rgb(20,30)", 255, 0, 0, 255)]
        public void ColorParsing_PreservesAlphaAndRejectsInvalidReplacement(string value, byte r, byte g, byte b, byte a)
        {
            var root = SvgLoader.Parse("<svg><rect id='r' width='20' height='20' style='fill:red;fill:" + value + "'/></svg>")!;
            var color = Find(root, "r").style!.fill.paint.color;
            Assert.Equal((r, g, b, a), (color.r, color.g, color.b, color.a));
        }

        [Fact]
        public void XmlEntities_DecodeAndRetainNonAsciiWhitespace()
        {
            var root = SvgLoader.Parse("<svg><text id='t'>A&amp;B&#035;C\u00a0D</text></svg>")!;
            Assert.Equal("A&B#C\u00a0D", Find(root, "t").text.text);
            var data = new SvgParserContext();
            var attributes = new List<string>(); const string input = "id='\u00a0A&amp;B\u00a0'";
            Assert.True(XmlParser.ParseAttributes(input, 0, input.Length, (ctx, key, value) => { attributes.Add(value); return true; }, data));
            Assert.Equal("\u00a0A&B\u00a0", Assert.Single(attributes));
        }

        [Theory]
        [InlineData("inf", -1)]
        [InlineData("infinity", -1)]
        [InlineData("-inf", 0)]
        [InlineData("nan", 0)]
        public void NonFiniteOpacity_PreservesNativeLongThenIntConversion(string value, int linuxArm64)
        {
            var root = SvgLoader.Parse("<svg><rect id='r' fill-opacity='" + value + "'/></svg>")!;
            // arm-opacity-native-oracle.json: GNU AArch64 lrint saturates the
            // C long before narrowing; x86 returns LONG_MIN for nonfinite input.
            int expected = OperatingSystem.IsWindows() ? int.MinValue : 0;
            if (OperatingSystem.IsLinux() && System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture ==
                System.Runtime.InteropServices.Architecture.Arm64) expected = linuxArm64;
            Assert.Equal(expected, Find(root, "r").style!.fill.opacity);
        }
    }
}
