using System;
using System.IO;
using Xunit;

namespace ThorVG.Tests;

public class tvgBlendNativeUpdateTests
{
    private static uint Color(uint i, uint alpha)
    {
        uint r = (i * 17) & 255, g = (i * 47 + 31) & 255, b = (i * 73 + 79) & 255;
        return SwHelper.JOIN((byte)alpha, (byte)(r * alpha / 255), (byte)(g * alpha / 255), (byte)(b * alpha / 255));
    }

    [Fact]
    public void NormalPremultipliedColorSwatchesMatchAllNativeBlendMethods()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "cpu-blend-native-golden.bin");
        using var expected = new BinaryReader(File.OpenRead(fixture));
        using var surface = new SwSurface();
        Func<SwSurface, uint, uint, uint>[] methods = { SwBlendOp.BlendDifference, SwBlendOp.BlendExclusion, SwBlendOp.BlendAdd, SwBlendOp.BlendScreen, SwBlendOp.BlendMultiply, SwBlendOp.BlendOverlay, SwBlendOp.BlendDarken, SwBlendOp.BlendLighten, SwBlendOp.BlendColorDodge, SwBlendOp.BlendColorBurn, SwBlendOp.BlendHardLight, SwBlendOp.BlendSoftLight, SwBlendOp.BlendHue, SwBlendOp.BlendSaturation, SwBlendOp.BlendColor, SwBlendOp.BlendLuminosity };
        int count = 0;
        for (int cs = 0; cs < 2; ++cs)
        {
            surface.cs = cs == 0 ? ColorSpace.ABGR8888 : ColorSpace.ARGB8888;
            Assert.Equal(Result.Success, SwRaster.rasterCompositor(surface));
            foreach (var method in methods)
            for (uint s = 0; s < 16; ++s)
            for (uint d = 0; d < 16; ++d)
            foreach (uint alpha in new uint[] { 0, 1, 64, 128, 254, 255 })
            {
                Assert.Equal(expected.ReadUInt32(), method(surface, Color(s, 255), Color(d, alpha)));
                ++count;
            }
            for (uint d = 0; d < 256; ++d)
            {
                Assert.Equal(expected.ReadUInt32(), SwBlendOp.BlendSoftLight(surface,
                    SwHelper.JOIN(255, 192, 192, 192), SwHelper.JOIN(255, (byte)d, (byte)d, (byte)d)));
                ++count;
            }
        }
        Assert.Equal(49664, count);
        Assert.Equal(expected.BaseStream.Length, expected.BaseStream.Position);
    }
}
