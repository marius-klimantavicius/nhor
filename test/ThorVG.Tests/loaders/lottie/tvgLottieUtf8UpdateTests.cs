using Xunit;

namespace ThorVG.Tests;

public class tvgLottieUtf8UpdateTests
{
    [Theory]
    [InlineData("Ž", 2)]
    [InlineData("🙂", 4)]
    [InlineData("A", 1)]
    [InlineData("a\0b", 1)]
    public void GlyphLengthUsesNativeUtf8Bytes(string code, int expected)
    {
        var glyph = new LottieGlyph { code = code };
        Assert.True(glyph.Prepare());
        Assert.Equal(expected, glyph.len);
    }

    [Fact]
    public void TextAnimatorLengthUsesTheNativeCStringByteCountAndRefreshesOnReplacement()
    {
        var doc = new TextDocument { text = "Ž🙂A" };
        Assert.Equal(7, new RenderText(new LottieText(), doc).nChars);
        doc.text = "a\0b";
        Assert.Equal(1, new RenderText(new LottieText(), doc).nChars);
        doc.text = "🙂";
        Assert.Equal(4, new RenderText(new LottieText(), doc).nChars);
    }
}
