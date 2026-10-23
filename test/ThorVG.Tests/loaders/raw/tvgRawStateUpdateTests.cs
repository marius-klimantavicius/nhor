using Xunit;

namespace ThorVG.Tests;

public class tvgRawStateUpdateTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void NativeFailedOpenConsumesTheRawLoader(int invalid)
    {
        var loader = new RawLoader();
        var pixels = new uint[4];
        Assert.False(loader.Open(invalid == 0 ? null! : pixels,
            invalid == 1 ? 0u : 2u, invalid == 2 ? 0u : 2u, ColorSpace.ABGR8888, false));
        Assert.True(loader.readied);
        Assert.True(loader.Open(pixels, 2, 2, ColorSpace.ABGR8888, false));
        Assert.Equal(0, loader.w);
        Assert.Equal(0, loader.h);
        Assert.Null(loader.surface.data);
    }

    [Fact]
    public void RepeatedRawOpenKeepsTheOriginalSurfaceWithoutReadingNewArguments()
    {
        var loader = new RawLoader();
        var pixels = new uint[4];
        Assert.True(loader.Open(pixels, 2, 2, ColorSpace.ABGR8888, false));
        try
        {
            Assert.True(loader.Open(null!, 0, 0, ColorSpace.Unknown, false));
            Assert.Same(pixels, loader.surface.data);
            Assert.Equal(2, loader.w);
            Assert.Equal(2, loader.h);
        }
        finally { loader.surface.Dispose(); }
    }
}
