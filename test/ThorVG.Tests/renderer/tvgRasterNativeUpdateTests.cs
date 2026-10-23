using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using ThorVG;
using Xunit;

namespace ThorVG.Tests;
public unsafe class tvgRasterNativeUpdateTests
{
    private static uint Color(uint i, uint k, bool opaque = false)
    {
        uint a = opaque ? 255 : (i * 13 + k * 47) & 255, r = (i * 31 + 17) & 255, g = (i * 59 + 79) & 255, b = (i * 73 + 137) & 255;
        return SwHelper.JOIN((byte)a, (byte)(r * a / 255), (byte)(g * a / 255), (byte)(b * a / 255));
    }

    [Fact]
    public void OrdinaryPaddedShapeGradientMaskAndImageDispatchMatchesNative()
    {
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "raster-native-golden.json")));
        string arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        if (!System.Runtime.Intrinsics.X86.Sse2.IsSupported && !System.Runtime.Intrinsics.Arm.AdvSimd.IsSupported)
            arch += "-scalar";
        var cases = expected.RootElement.GetProperty("architectures").GetProperty(arch).GetProperty("cases");
        using var memory = new MemoryStream(65540);
        using var output = new BinaryWriter(memory);
        uint count = 0;
        void Check()
        {
            output.Flush();
            Assert.Equal(65540, memory.Length);
            var row = cases[(int)count];
            Assert.Equal(count, row.GetProperty("index").GetUInt32());
            string actual = Convert.ToHexString(SHA256.HashData(memory.GetBuffer().AsSpan(0, (int)memory.Length))).ToLowerInvariant();
            Assert.True(row.GetProperty("sha256").GetString() == actual, $"Native raster case {count}: {actual}");
            memory.SetLength(0);
        }

        void Execute(int geometry, int kind, int mode, byte opacity, int path, int filter, bool opaque)
        {
            var source = new uint[71 * 64];
            var dest = new uint[8192];
            var mask = new uint[8192];
            for (uint i = 0; i < source.Length; ++i)
                source[i] = Color(i, 0, opaque);
            for (uint i = 0; i < 8192; ++i)
            {
                dest[i] = Color(i, 1);
                mask[i] = Color(i, 2);
            }

            using var sourcePin = new RenderSurface();
            sourcePin.Setup(source, 71, 64, 64, 4, ColorSpace.ABGR8888);
            using var maskPin = new RenderSurface();
            maskPin.Setup(mask, 69, 64, 64, 4, ColorSpace.ABGR8888);
            using var surface = new SwSurface();
            surface.Setup(dest, 67, 64, 64, 4, ColorSpace.ABGR8888);
            SwRaster.rasterCompositor(surface);
            var cmp = new SwCompositor();
            cmp.image.buf32 = maskPin.buf32;
            cmp.image.w = cmp.image.h = 64;
            cmp.image.stride = 69;
            cmp.image.channelSize = 4;
            cmp.bbox = new(2, 3, 62, 61);
            bool gray = mode == 1 || mode == 3 || mode >= 8;
            if (gray)
                surface.channelSize = 1;
            if (mode == 2 || mode == 3)
                surface.blender = SwBlendOp.BlendMultiply;
            if (mode >= 4)
            {
                surface.compositor = cmp;
                cmp.method = (MaskMethod)(mode < 8 ? mode - 3 : mode < 12 ? mode - 7 : mode - 7);
                cmp.image.channelSize = (byte)(cmp.method == MaskMethod.Luma || cmp.method == MaskMethod.InvLuma ? 4 : 1);
                if (cmp.image.channelSize == 1)
                    for (uint i = 0; i < 69 * 64; ++i)
                        maskPin.buf8[i] = (byte)(i * 29 + 17);
            }

            SwRle rle = default;
            rle.spans.Push(new SwSpan { x = 4, y = 5, len = 12, coverage = 31 });
            rle.spans.Push(new SwSpan { x = 9, y = 8, len = 17, coverage = 127 });
            rle.spans.Push(new SwSpan { x = 6, y = 11, len = 11, coverage = 255 });
            var stroke = new SwStroke();
            var shape = new SwShape
            {
                fastTrack = geometry == 0,
                rle = rle,
                hasRle = true,
                strokeRle = rle,
                hasStrokeRle = true,
                stroke = stroke
            };
            var bbox = new RenderRegion(2, 3, 62, 61);
            bool result;
            if (path < 0)
            {
                if (kind >= 0)
                {
                    Fill gradient;
                    if (kind == 0)
                    {
                        var f = LinearGradient.Gen();
                        f.Linear(3.25f, 7.5f, 51.75f, 42.125f);
                        gradient = f;
                    }
                    else if (kind == 1)
                    {
                        var f = RadialGradient.Gen();
                        f.Radial(31.25f, 29.5f, 27.75f, 28.125f, 26.75f, 2.25f);
                        gradient = f;
                    }
                    else
                    {
                        var f = ConicGradient.Gen();
                        f.Conic(29.25f, 31.75f, 23.5f);
                        gradient = f;
                    }

                    Fill.ColorStop[] stops =
                    {
                        new(0, 245, 31, 67, 255),
                        new(.31f, 24, 213, 91, (byte)(opacity == 255 ? 255 : 137)),
                        new(.72f, 63, 89, 241, (byte)(opacity == 255 ? 255 : 221)),
                        new(1, 193, 151, 17, 255)
                    };
                    gradient.SetColorStops(stops, 4);
                    var identity = TvgMath.Identity();
                    var fill = new SwFill();
                    SwFillOps.fillGenColorTable(fill, gradient, identity, surface, opacity, true);
                    shape.fill = stroke.fill = fill;
                    result = geometry == 2 ? SwRaster.rasterGradientStroke(surface, shape, bbox, gradient, opacity) : SwRaster.rasterGradientShape(surface, shape, bbox, gradient, opacity);
                }
                else
                {
                    var c = new RenderColor(97, 173, 229, opacity);
                    result = geometry == 2 ? SwRaster.rasterStroke(surface, shape, bbox, c) : SwRaster.rasterShape(surface, shape, bbox, c);
                }
            }
            else
            {
                var image = new SwImage
                {
                    buf32 = sourcePin.buf32,
                    w = 64,
                    h = 64,
                    stride = 71,
                    channelSize = 4,
                    filter = filter != 0 ? FilterMethod.Nearest : FilterMethod.Bilinear,
                    rle = rle,
                    hasRle = true,
                    ox = -2,
                    oy = -3,
                    alphaIgnored = opaque
                };
                float[] scales =
                {
                    .375f,
                    .8f,
                    1.5f
                };
                image.scale = path < 2 ? 1 : scales[(path - 2) / 2];
                var m = new Matrix(image.scale, 0, 3.125f, 0, image.scale, 4.75f, 0, 0, 1);
                if (path == 0)
                    result = SwRaster.rasterDirectImage(surface, image, bbox, opacity);
                else if (path == 1)
                    result = SwRaster.rasterDirectRleImage(surface, image, bbox, opacity);
                else
                    result = path % 2 == 0 ? SwRaster.rasterScaledImage(surface, image, m, bbox, opacity) : SwRaster.rasterScaledRleImage(surface, image, m, bbox, opacity);
            }

            output.Write(result ? 1u : 0u);
            output.Write(new ReadOnlySpan<byte>(surface.buf32, 8192 * 4));
            output.Write(new ReadOnlySpan<byte>(maskPin.buf32, 8192 * 4));
            Check();
            rle.spans.Dispose();
            ++count;
        }

        for (int g = 0; g < 3; ++g)
            for (int k = -1; k < 3; ++k)
                foreach (byte o in new byte[]
                {
                    127,
                    255
                }

                )
                    for (int m = 0; m < 18; ++m)
                    {
                        if (k >= 0 && m >= 8 && m < 12)
                            continue;
                        Execute(g, k, m, o, -1, 0, false);
                    }

        for (int path = 0; path < 8; ++path)
            for (int filter = 0; filter < 2; ++filter)
                foreach (byte o in new byte[]
                {
                    127,
                    255
                }

                )
                    foreach (bool opaque in new[]
                    {
                        false,
                        true
                    }

                    )
                        for (int m = 0; m < 12; ++m)
                        {
                            bool gray = m == 1 || m == 3 || m >= 8;
                            if (path == 1 && gray)
                                continue;
                            if (path >= 2 && (m == 3 || m >= 8))
                                continue;
                            Execute(0, -1, m, o, path, filter, opaque);
                        }

        for (int kind = 0; kind < 2; ++kind)
            for (int j = 0; j < 3; ++j)
            {
                var dest = new uint[8192];
                var mask = new uint[8192];
                for (uint i = 0; i < 8192; ++i)
                {
                    dest[i] = Color(i, 1);
                    mask[i] = Color(i, 2);
                }

                using var surface = new SwSurface();
                surface.Setup(dest, 67, (uint)(9 + j * 8), (uint)(7 + j * 5), 4, ColorSpace.ABGR8888);
                using var maskPin = new RenderSurface();
                maskPin.Setup(mask, 69, 64, 64, 4, ColorSpace.ABGR8888);
                surface.premultiplied = kind == 0;
                if (kind == 0)
                    SwRaster.rasterUnpremultiply(surface);
                else
                    SwRaster.rasterPremultiply(surface);
                output.Write(surface.premultiplied ? 1u : 0u);
                output.Write(new ReadOnlySpan<byte>(surface.buf32, 8192 * 4));
                output.Write(new ReadOnlySpan<byte>(maskPin.buf32, 8192 * 4));
                Check();
                ++count;
            }

        Assert.Equal(846u, count);
        Assert.Equal(846, cases.GetArrayLength());
    }
}
