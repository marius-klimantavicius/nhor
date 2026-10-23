using System.Text.Json;
using Xunit;

namespace ThorVG.Tests;

public class tvgLottieUnicodeTextContractTests
{
    [Theory]
    [InlineData("Hi", "Ž", "H", true)]
    [InlineData("Hi", "Ža", "Ž", true)]
    [InlineData("Hi", "aŽ", "ai", true)]
    [InlineData("Hi", "🙂", "Hi", true)]
    [InlineData("Hi", "Ž🙂A", "Ž", true)]
    [InlineData("ABCD", "漢字", "AB", true)]
    [InlineData("", "éé", "é", true)]
    [InlineData("", "ŽA", "Ž", true)]
    [InlineData("", "🙂", "", true)]
    [InlineData("", "a🙂", "", false)]
    [InlineData(null, "🙂", null, false)]
    [InlineData("Hi", "ASCII replacement", "ASCII replacement", true)]
    public void ValidUnicodeReturnsUseBoundedInitializedNativeBytes(string? initial, string returned, string? expected, bool converted)
    {
        // Direct TextDocument returns, not an ASCII serialization of JS code units.
        // Expected values follow the pinned UTF16-capacity adapter and the managed
        // refusal to read an unwritten, uninitialized grown allocation tail.
        var root = new LottieRootLayer { inFrame = 0, outFrame = 90 };
        var comp = new LottieComposition { root = root, w = 128, h = 64, frameRate = 30 };
        var layer = new LottieLayer { comp = root, transform = new LottieTransform() };
        root.children.Add(layer);
        var rect = new LottieRect();
        layer.children.Add(rect);
        var property = new LottieTextDoc();
        property.value.text = initial;
        var expression = new LottieExpression
        {
            code = "$bm_rt=" + JsonSerializer.Serialize(returned) + ";",
            comp = comp, layer = layer, obj = rect, property = property
        };
        var expressions = LottieExpressions.Instance()!;
        try
        {
            Assert.Equal(converted, expressions.ResultTextDoc(0, property.value, expression));
            Assert.Equal(expected, property.value.text);
            Assert.False(expression.disabled);
            // A retained document uses its previous initialized bytes on each call.
            Assert.True(expressions.ResultTextDoc(0, property.value, new LottieExpression
            {
                code = "$bm_rt='Next';", comp = comp, layer = layer, obj = rect, property = property
            }));
            Assert.Equal("Next", property.value.text);
        }
        finally { LottieExpressions.Retrieve(expressions); comp.Release(); }
    }
}
