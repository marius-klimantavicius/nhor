using Xunit;

namespace ThorVG.Tests;

public class tvgLottieColorRangeUpdateTests
{
    [Theory]
    [InlineData(0.5f, 140, 89, 153, 102, 153, 128)]
    [InlineData(2f, 255, 0, 0, 0, 0, 255)]
    [InlineData(-1f, 0, 255, 255, 255, 255, 0)]
    public void TextRangeColorsUseNativeTruncationAndClamp(float factor, int fr, int fg, int fb, int sr, int sg, int sb)
    {
        var range = new LottieTextRange();
        range.style.flagFillColor = range.style.flagStrokeColor = true;
        range.style.fillColor.value = new RGB32(230, 26, 76);
        range.style.strokeColor.value = new RGB32(26, 76, 230);
        var fill = new RGB32(51, 153, 230);
        var stroke = new RGB32(178, 230, 26);
        using var tween = new LottieTween();
        range.Color(0, ref fill, ref stroke, factor, tween, null);
        Assert.Equal((fr, fg, fb), (fill.r, fill.g, fill.b));
        Assert.Equal((sr, sg, sb), (stroke.r, stroke.g, stroke.b));
    }
}
