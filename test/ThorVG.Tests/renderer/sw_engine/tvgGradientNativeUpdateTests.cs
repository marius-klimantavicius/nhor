using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace ThorVG.Tests;

public class tvgGradientNativeUpdateTests
{
    private static uint Join(byte r, byte g, byte b, byte a) => SwHelper.JOIN(a, r, g, b);
    private static byte Mask(byte s, byte d, byte a) => (byte)(s + SwHelper.MULTIPLY(d, a));
    private static unsafe byte Alpha(byte* data) => *data;

    [Fact]
    public unsafe void FiniteGradientsMatchNativePreparationTablesAndEveryRowMode()
    {
        string architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        string fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", $"cpu-gradient-{architecture}-native-golden.bin");
        using var expected = new BinaryReader(File.OpenRead(fixture));
        void Word(uint value) => Assert.Equal(expected.ReadUInt32(), value);
        void Number(float value) => Word(BitConverter.SingleToUInt32Bits(value));
        Matrix[] matrices =
        {
            new(1, 0, 0, 0, 1, 0, 0, 0, 1),
            new(.75f, .125f, 2.5f, -.25f, 1.25f, -1.75f, 0, 0, 1),
            new(1.3f, -.4f, -3.25f, .2f, .9f, 4.125f, 0, 0, 1)
        };
        Fill.ColorStop[] stops =
        {
            new(0, 245, 31, 67, 255), new(.31f, 24, 213, 91, 137),
            new(.72f, 63, 89, 241, 221), new(1, 193, 151, 17, 255)
        };
        using var surface = new SwSurface { join = Join };
        int count = 0;
        for (int kind = 0; kind < 3; ++kind)
        for (int geometry = 0; geometry < 3; ++geometry)
        foreach (var matrix in matrices)
        foreach (var spread in new[] { FillSpread.Pad, FillSpread.Repeat, FillSpread.Reflect })
        foreach (byte opacity in new byte[] { 127, 255 })
        {
            Fill gradient;
            if (kind == 0)
            {
                var linear = LinearGradient.Gen();
                Assert.Equal(Result.Success, linear.Linear(-3.25f + geometry, 2.125f, 21.75f, 17.375f + geometry * 7));
                gradient = linear;
            }
            else if (kind == 1)
            {
                var radial = RadialGradient.Gen();
                Assert.Equal(Result.Success, radial.Radial(16.25f, 14.5f, 18 + geometry * 7, 13.125f, 12.75f, 2.25f + geometry));
                gradient = radial;
            }
            else
            {
                var conic = ConicGradient.Gen();
                Assert.Equal(Result.Success, conic.Conic(15.25f, 13.75f, 23.5f + geometry * 47));
                gradient = conic;
            }
            Assert.Equal(Result.Success, gradient.SetColorStops(stops, 4));
            Assert.Equal(Result.Success, gradient.SetSpread(spread));
            var fill = new SwFill();
            Word(SwFillOps.fillGenColorTable(fill, gradient, matrix, surface, opacity, true) ? 1u : 0u);
            Word(fill.solid ? 1u : 0u);
            Word(fill.translucent ? 1u : 0u);
            Number(fill.extent);
            if (kind == 0)
            {
                Number(fill.linear.dx); Number(fill.linear.dy); Number(fill.linear.offset);
            }
            else if (kind == 1)
            {
                var r = fill.radial;
                foreach (float value in new[] { r.a11, r.a12, r.a13, r.a21, r.a22, r.a23, r.fx, r.fy, r.fr, r.dx, r.dy, r.dr, r.invA, r.a }) Number(value);
            }
            else
            {
                var c = fill.conic;
                foreach (float value in new[] { c.a11, c.a12, c.a13, c.a21, c.a22, c.a23, c.cx, c.cy, c.offset, c.normal.x, c.normal.y, c.invFwidth, c.distanceDx, c.seamProjectionDx }) Number(value);
            }
            for (int i = 0; i < SwConstants.SW_COLOR_TABLE; ++i) Word(fill.ctable.data[i]);
            foreach (uint y in new uint[] { 0, 7, 14, 23 })
            foreach (uint x in new uint[] { 0, 5 })
            for (int mode = 0; mode < 5; ++mode)
            {
                byte[] actual = Row(kind, fill, surface, x, y, opacity, mode);
                Assert.Equal(expected.ReadBytes(actual.Length), actual);
            }
            SwFillOps.fillReset(fill);
            Word(fill.solid ? 1u : 0u);
            Word(fill.translucent ? 1u : 0u);
            ++count;
        }
        Assert.Equal(162, count);
        Assert.Equal(expected.BaseStream.Length, expected.BaseStream.Position);
    }

    private static unsafe byte[] Row(int kind, SwFill fill, SwSurface surface, uint x, uint y, byte opacity, int mode)
    {
        const uint length = 33;
        uint* pixels = stackalloc uint[(int)length];
        uint* generic = stackalloc uint[(int)length];
        byte* gray = stackalloc byte[(int)length];
        byte* genericGray = stackalloc byte[(int)length];
        byte* mask = stackalloc byte[(int)length * 2];
        for (uint i = 0; i < length; ++i)
        {
            generic[i] = pixels[i] = 0x80402010;
            genericGray[i] = gray[i] = (byte)(i * 17 + 43);
            mask[i * 2] = (byte)(i * 31 + 17);
            mask[i * 2 + 1] = 99;
        }
        if (kind == 0)
        {
            if (mode == 0) { SwFillOps.fillLinear(fill, pixels, y, x, length, SwBlendOps.opBlendNormal, opacity); SwFillOps.fillLinear<BlendNormal>(fill, generic, y, x, length, opacity); }
            if (mode == 1) SwFillOps.fillLinear(fill, pixels, y, x, length, mask, Alpha, 2, opacity);
            if (mode == 2) { SwFillOps.fillLinear(fill, gray, y, x, length, Mask, opacity); SwFillOps.fillLinear<MaskAdd>(fill, genericGray, y, x, length, opacity); }
            if (mode == 3) SwFillOps.fillLinear(fill, gray, y, x, length, mask, Mask, opacity);
            if (mode == 4) SwFillOps.fillLinear(surface, fill, pixels, y, x, length, SwBlendOps.opBlendNormal, (_, s, d) => s ^ d, opacity);
        }
        else if (kind == 1)
        {
            if (mode == 0) { SwFillOps.fillRadial(fill, pixels, y, x, length, SwBlendOps.opBlendNormal, opacity); SwFillOps.fillRadial<BlendNormal>(fill, generic, y, x, length, opacity); }
            if (mode == 1) SwFillOps.fillRadial(fill, pixels, y, x, length, mask, Alpha, 2, opacity);
            if (mode == 2) { SwFillOps.fillRadial(fill, gray, y, x, length, Mask, opacity); SwFillOps.fillRadial<MaskAdd>(fill, genericGray, y, x, length, opacity); }
            if (mode == 3) SwFillOps.fillRadial(fill, gray, y, x, length, mask, Mask, opacity);
            if (mode == 4) SwFillOps.fillRadial(surface, fill, pixels, y, x, length, SwBlendOps.opBlendNormal, (_, s, d) => s ^ d, opacity);
        }
        else
        {
            if (mode == 0) { SwFillOps.fillConic(fill, pixels, y, x, length, SwBlendOps.opBlendNormal, opacity); SwFillOps.fillConic<BlendNormal>(fill, generic, y, x, length, opacity); }
            if (mode == 1) SwFillOps.fillConic(fill, pixels, y, x, length, mask, Alpha, 2, opacity);
            if (mode == 2) { SwFillOps.fillConic(fill, gray, y, x, length, Mask, opacity); SwFillOps.fillConic<MaskAdd>(fill, genericGray, y, x, length, opacity); }
            if (mode == 3) SwFillOps.fillConic(fill, gray, y, x, length, mask, Mask, opacity);
            if (mode == 4) SwFillOps.fillConic(surface, fill, pixels, y, x, length, SwBlendOps.opBlendNormal, (_, s, d) => s ^ d, opacity);
        }
        if (mode == 0) Assert.True(new ReadOnlySpan<uint>(pixels, (int)length).SequenceEqual(new ReadOnlySpan<uint>(generic, (int)length)));
        if (mode == 2) Assert.True(new ReadOnlySpan<byte>(gray, (int)length).SequenceEqual(new ReadOnlySpan<byte>(genericGray, (int)length)));
        return mode == 2 || mode == 3 ? new ReadOnlySpan<byte>(gray, (int)length).ToArray() : new ReadOnlySpan<byte>(pixels, (int)length * 4).ToArray();
    }
}
