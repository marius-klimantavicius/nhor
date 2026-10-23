using Xunit;

namespace ThorVG.Tests;

public class tvgLottieBuilderOwnershipUpdateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TextTemporaryReleasesUnattachedScenesAndPreservesTransferredScenes(bool transferred)
    {
        var parent = Scene.Gen();
        using var context = new RenderText(new LottieText(), new TextDocument { text = "A", size = 1 });
        var glyph = Shape.Gen();
        glyph.AppendRect(0, 0, 12, 8);
        var pointCount = glyph.rs.path.pts.count;
        Assert.NotEqual(0u, pointCount);
        context.lineScene.Add(glyph);
        context.textScene.Add(context.lineScene);
        if (transferred) parent.Add(context.textScene);
        context.Dispose();

        Assert.Equal(transferred ? 1 : 0, glyph.RefCnt());
        Assert.Equal(transferred ? pointCount : 0u, glyph.rs.path.pts.count);
        Paint.Rel(parent);
        Assert.Equal(0, glyph.RefCnt());
        Assert.Equal(0u, glyph.rs.path.pts.count);
    }

    [Fact]
    public void RecycledContextReleasesItsPropagatorExactlyOncePerLifetime()
    {
        var shape = Shape.Gen();
        using var context = new RenderContext();
        for (int i = 0; i < 3; ++i)
        {
            context.Init(shape);
            Assert.Equal(1, shape.RefCnt());
            context.Dispose();
            Assert.Equal(0, shape.RefCnt());
            context.Dispose();
            Assert.Equal(0, shape.RefCnt());
        }
        Paint.Rel(shape);
    }
}
