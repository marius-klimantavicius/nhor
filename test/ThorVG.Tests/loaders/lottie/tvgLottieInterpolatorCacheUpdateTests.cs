using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using Xunit;

namespace ThorVG.Tests;

public class tvgLottieInterpolatorCacheUpdateTests
{
    public static IEnumerable<object[]> CachedKeys()
    {
        yield return [new string('Ž', 10) + "A", new string('Ž', 10) + "B", true];
        yield return ["😀😀😀😀😀A", "😀😀😀😀😀B", true];
        yield return [new string('a', 19) + "Ž", new string('a', 19) + "ž", true];
        yield return ["same\0A", "same\0B", true];
        yield return [new string('a', 19) + "A", new string('a', 19) + "B", false];
    }

    [Theory]
    [MemberData(nameof(CachedKeys))]
    public void ParsedEasingCacheUsesTwentyCStringBytes(string first, string second, bool reused)
    {
        var document = Fixture();
        var shapes = document["layers"]![0]!["shapes"]!;
        shapes[0]!["p"]!["k"]![0]!["n"] = first;
        shapes[1]!["p"]!["k"]![0]!["n"] = second;
        var parser = new LottieParser(document.ToJsonString(), null, false);
        Assert.True(parser.Parse());
        var comp = parser.comp!;
        try
        {
            var layer = Assert.IsType<LottieLayer>(Assert.Single(comp.root!.children));
            var a = Assert.IsType<LottieRect>(layer.children[0]);
            var b = Assert.IsType<LottieRect>(layer.children[1]);
            Assert.Equal(reused, ReferenceEquals(a.position.frames![0].interpolator, b.position.frames![0].interpolator));
            Assert.Equal(reused ? 1 : 2, comp.interpolators.Count);
            if (reused)
            {
                Assert.Equal(a.position.Evaluate(30).x, b.position.Evaluate(30).x);
                Assert.Equal(20, a.position.Evaluate(30).y);
                Assert.Equal(44, b.position.Evaluate(30).y);
            }
        }
        finally { comp.Release(); }
    }

    [Fact]
    public void GeneratedEasingNamesKeepNativeCDecimalFormattingUnderOtherCultures()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            var commaCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            commaCulture.NumberFormat.NumberDecimalSeparator = ",";
            CultureInfo.CurrentCulture = commaCulture;
            var document = Fixture();
            ((JsonObject)document["layers"]![0]!["shapes"]![0]!["p"]!["k"]![0]!).Remove("n");
            var parser = new LottieParser(document.ToJsonString(), null, false);
            Assert.True(parser.Parse());
            try { Assert.Equal("0.35_0.80_0.20_0.10", parser.comp!.interpolators[0].key); }
            finally { parser.comp!.Release(); }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    private static JsonNode Fixture() => JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "fixtures", "thorvg-update", "lottie-easing-key-utf8.lot")))!;
}
