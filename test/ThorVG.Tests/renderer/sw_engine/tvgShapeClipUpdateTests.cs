using System;
using System.IO;
using Xunit;

namespace ThorVG.Tests;

public class tvgShapeClipUpdateTests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(2u)]
    public void ClippingRetainsAVisibleStrokeOrFillWhenTheOtherCoverageIsEmpty(uint workers)
    {
        Assert.Equal(Result.Success, Initializer.Init(workers));
        try
        {
            using var canvas = SwCanvas.Gen()!;
            var pixels = new uint[96 * 64];
            Assert.Equal(Result.Success, canvas.Target(pixels, 96, 96, 64, ColorSpace.ABGR8888));
            var picture = Picture.Gen();
            Assert.Equal(Result.Success, picture.Load(Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "svg-fill-stroke-clip.svg")));
            Assert.Equal(Result.Success, canvas.Add(picture));
            Assert.Equal(Result.Success, canvas.Draw(true));
            Assert.Equal(Result.Success, canvas.Sync());
            Assert.Equal(0xffd04020u, pixels[24 * 96 + 14]);
            Assert.Equal(0xff50b020u, pixels[28 * 96 + 74]);
            Assert.Equal(0u, pixels[40 * 96 + 74]);
        }
        finally { Assert.Equal(Result.Success, Initializer.Term()); }
    }
}
