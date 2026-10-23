using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using ThorVG;
using Xunit;

namespace ThorVG.Tests;

public unsafe class tvgTexmapNativeUpdateTests
{
    private static uint Color(uint i, uint k)
    {
        uint a = (i * 13 + k * 47) & 255;
        uint r = (i * 31 + 17) & 255, g = (i * 59 + 79) & 255, b = (i * 73 + 137) & 255;
        return SwHelper.JOIN((byte)a, (byte)(r * a / 255), (byte)(g * a / 255), (byte)(b * a / 255));
    }

    [Fact]
    public void OrdinaryAffineImagesAndNestedRenderingMatchNativePixels()
    {
        Matrix[] matrices =
        {
            new(1, 0, 7.25f, 0, 1, 9.5f, 0, 0, 1),
            new(.8660254f, -.5f, 21.5f, .5f, .8660254f, 3.25f, 0, 0, 1),
            new(0, -1, 42, 1, 0, 7, 0, 0, 1),
            new(.75f, .25f, 9, -.125f, 1.25f, 11, 0, 0, 1),
            new(-1.25f, .125f, 45, .25f, 1, 8, 0, 0, 1),
            new(1, 0, -2.5f, 0, 1, -3.5f, 0, 0, 1),
            new(1.6f, -.3f, 3.125f, .2f, .7f, 12.75f, 0, 0, 1)
        };
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "fixtures", "thorvg-update", "texmap-native-golden.json")));
        string arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        var cases = expected.RootElement.GetProperty("architectures").GetProperty(arch).GetProperty("cases");
        int index = 0;
        byte[] bytes = new byte[4 + 4096 * 4];

        foreach (uint dim in new uint[] { 1, 7, 23 })
        foreach (var matrix in matrices)
        foreach (var filter in new[] { FilterMethod.Bilinear, FilterMethod.Nearest })
        foreach (byte opacity in new byte[] { 127, 255 })
        for (int mode = 0; mode < 8; ++mode)
        {
            var source = new uint[(dim + 3) * (dim + 2)];
            var dest = new uint[4096];
            var mask = new uint[4096];
            for (uint i = 0; i < source.Length; ++i) source[i] = Color(i, 0);
            for (uint i = 0; i < 4096; ++i)
            {
                dest[i] = Color(i, 1);
                mask[i] = Color(i, 2);
            }
            using var sourcePin = new RenderSurface();
            sourcePin.Setup(source, dim + 3, dim, dim + 2, 4, ColorSpace.ABGR8888);
            using var maskPin = new RenderSurface();
            maskPin.Setup(mask, 64, 64, 64, 4, ColorSpace.ABGR8888);
            using var surface = new SwSurface();
            surface.Setup(dest, 64, 64, 64, 4, ColorSpace.ABGR8888);
            Assert.Equal(Result.Success, SwRaster.rasterCompositor(surface));
            var image = new SwImage
            {
                buf32 = sourcePin.buf32, w = dim, h = dim + 2, stride = dim + 3,
                channelSize = 4, filter = filter
            };
            if (mode == 1) surface.channelSize = 1;
            if (mode == 2) surface.blender = SwBlendOp.BlendMultiply;
            if (mode >= 3 && mode < 7)
            {
                var cmp = new SwCompositor { method = (MaskMethod)(mode - 2) };
                surface.compositor = cmp;
                cmp.image.buf32 = maskPin.buf32;
                cmp.image.w = cmp.image.h = cmp.image.stride = 64;
                cmp.image.channelSize = (byte)(mode < 5 ? 1 : 4);
                if (mode < 5)
                    for (uint i = 0; i < 4096; ++i) maskPin.buf8[i] = (byte)(i * 29 + 17);
            }
            using var inner = new SwSurface();
            inner.Setup(new uint[4096], 64, 64, 64, 4, ColorSpace.ABGR8888);
            Assert.Equal(Result.Success, SwRaster.rasterCompositor(inner));
            bool nested = false;
            if (mode == 7)
            {
                surface.blender = (s, src, dst) =>
                {
                    if (!nested)
                    {
                        nested = true;
                        var nestedMatrix = new Matrix(1.25f, .125f, 7.5f, -.25f, .75f, 11.25f, 0, 0, 1);
                        var nestedBox = new RenderRegion(2, 3, 62, 61);
                        Assert.True(SwRaster.rasterTexmapPolygon(inner, image, nestedMatrix, nestedBox, 127));
                    }
                    return SwBlendOp.BlendMultiply(s, src, dst);
                };
            }
            var bbox = new RenderRegion(2, 3, 62, 61);
            bool result = SwRaster.rasterTexmapPolygon(surface, image, matrix, bbox, opacity);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, result ? 1u : 0u);
            new ReadOnlySpan<byte>(surface.buf32, 4096 * 4).CopyTo(bytes.AsSpan(4));
            var row = cases[index];
            Assert.Equal(index, row.GetProperty("index").GetInt32());
            Assert.Equal(row.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            ++index;
        }
        Assert.Equal(672, index);
        Assert.Equal(index, cases.GetArrayLength());
    }
}
