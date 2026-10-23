using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace ThorVG.Tests;

public class tvgPngOwnershipUpdateTests
{
    [Fact]
    public void ReleasingPngLoaderReleasesDecodedPaletteAndPixelOwnership()
    {
        var loader = new PngLoader();
        Assert.True(loader.Open(Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "palette-owned.png")));
        Assert.True(loader.Read());
        var surface = Assert.IsType<RenderSurface>(loader.Bitmap());
        Assert.Equal(new uint[] { 0x001e140a, 0xff3c3228 }, surface.data);
        var state = typeof(PngLoader).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(loader)!;
        var info = state.GetType().GetField("info_png")!.GetValue(state)!;
        var color = info.GetType().GetField("color")!.GetValue(info)!;
        var palette = color.GetType().GetField("palette")!;
        Assert.NotNull(palette.GetValue(color));
        Assert.True(LoaderMgr.Retrieve(loader));
        Assert.Null(surface.data);
        Assert.Null(palette.GetValue(color));
        Assert.Null(info.GetType().GetField("iccp_name")!.GetValue(info));
        Assert.Null(info.GetType().GetField("iccp_profile")!.GetValue(info));
        GC.KeepAlive(loader); // The disposed loader itself remains reachable.
    }
}
