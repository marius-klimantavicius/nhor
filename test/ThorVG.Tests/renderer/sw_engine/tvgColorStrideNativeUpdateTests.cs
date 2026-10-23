using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace ThorVG.Tests;

public class tvgColorStrideNativeUpdateTests
{
    [Fact]
    public unsafe void PublicColorConversionMatchesNativePaddedSurfaceRowsInBothDirections()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "cpu-color-stride-native-golden.json");
        using var data = JsonDocument.Parse(File.ReadAllText(fixture));
        int count = 0;
        foreach (var row in data.RootElement.GetProperty("cases").EnumerateArray())
        {
            uint stride = row.GetProperty("stride").GetUInt32();
            uint width = row.GetProperty("width").GetUInt32();
            uint height = row.GetProperty("height").GetUInt32();
            uint offset = row.GetProperty("offset").GetUInt32();
            var pixels = new uint[stride * height + 8];
            for (uint i = 0; i < pixels.Length; ++i) pixels[i] = 0x80000000u | ((i * 2654435761u) & 0x7f7f7fu);
            var original = (uint[])pixels.Clone();
            using var surface = new RenderSurface();
            surface.Setup(pixels, stride, width, height, 4, ColorSpace.ABGR8888);
            surface.buf32 += offset;
            Assert.True(SwRaster.rasterConvertCS(surface, ColorSpace.ARGB8888));
            Assert.Equal(ColorSpace.ARGB8888, surface.cs);
            int index = 0;
            foreach (var value in row.GetProperty("expected").EnumerateArray()) Assert.Equal(value.GetUInt32(), pixels[index++]);
            Assert.Equal(pixels.Length, index);
            Assert.True(SwRaster.rasterConvertCS(surface, ColorSpace.ABGR8888));
            Assert.Equal(original, pixels);
            ++count;
        }
        Assert.Equal(80, count);
    }
}
