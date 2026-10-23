using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace ThorVG.Tests;

public class tvgPngNativeUpdateTests
{
    private static string Fixtures => Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update");
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static JsonDocument Provenance() => JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "normal-png", "provenance.json")));

    [Fact]
    public void AllStandardColorDepthAndAdam7ModesMatchNativePixels()
    {
        using var provenance = Provenance();
        int count = 0;
        foreach (var row in provenance.RootElement.GetProperty("cases").EnumerateArray())
        {
            string file = row.GetProperty("file").GetString()!;
            byte[] input = File.ReadAllBytes(Path.Combine(Fixtures, "normal-png", file));
            Assert.Equal(row.GetProperty("inputSha256").GetString(), Hash(input));
            var state = LodePng.StateInit();
            Assert.Equal(0u, LodePng.DecodeBytes(input, input.Length, state, out var pixels, out uint width, out uint height));
            Assert.Equal(row.GetProperty("width").GetUInt32(), width);
            Assert.Equal(row.GetProperty("height").GetUInt32(), height);
            Assert.Equal(row.GetProperty("color").GetInt32(), (int)state.info_png.color.colortype);
            Assert.Equal(row.GetProperty("bitdepth").GetUInt32(), state.info_png.color.bitdepth);
            Assert.Equal(row.GetProperty("interlace").GetUInt32(), state.info_png.interlace_method);
            Assert.NotNull(pixels);
            Assert.True(row.GetProperty("nativeRgbaSha256").GetString() == Hash(pixels!), file);
            LodePng.StateCleanup(state);
            ++count;
        }
        Assert.Equal(90, count);
    }

    private sealed class Context { internal int Zlib, Inflate; }

    private static uint Decompress(ref byte[]? output, ref int size, byte[] input, int offset,
        int length, LodePNGDecompressSettings settings, bool zlib)
    {
        var context = (Context)settings.custom_context!;
        if (zlib) ++context.Zlib; else ++context.Inflate;
        using var source = new MemoryStream(input, offset, length, false);
        using Stream decoder = zlib ? new ZLibStream(source, CompressionMode.Decompress) : new DeflateStream(source, CompressionMode.Decompress);
        using var result = new MemoryStream();
        if (size != 0) result.Write(output!, 0, size);
        decoder.CopyTo(result);
        output = result.ToArray();
        size = output.Length;
        return 0;
    }

    [Fact]
    public void CustomDecompressorsPreserveNativePrecedenceContextAndPixels()
    {
        byte[] input = File.ReadAllBytes(Path.Combine(Fixtures, "normal-png", "c6-b8-17x19-adam0-stored.png"));
        byte[]? expected = null;
        for (int mode = 0; mode < 4; ++mode)
        {
            var state = LodePng.StateInit();
            var settings = state.decoder.zlibsettings;
            Assert.Null(settings.custom_zlib);
            Assert.Null(settings.custom_inflate);
            Assert.Null(settings.custom_context);
            var context = new Context();
            settings.custom_context = context;
            if ((mode & 1) != 0) settings.custom_zlib = (ref byte[]? output, ref int size, byte[] bytes, int offset, int length, LodePNGDecompressSettings config)
                => Decompress(ref output, ref size, bytes, offset, length, config, true);
            if ((mode & 2) != 0) settings.custom_inflate = (ref byte[]? output, ref int size, byte[] bytes, int offset, int length, LodePNGDecompressSettings config)
                => Decompress(ref output, ref size, bytes, offset, length, config, false);
            Assert.Equal(0u, LodePng.DecodeBytes(input, input.Length, state, out var pixels, out uint width, out uint height));
            Assert.Equal(17u, width);
            Assert.Equal(19u, height);
            Assert.Same(context, settings.custom_context);
            Assert.Equal((mode & 1) != 0 ? 1 : 0, context.Zlib);
            Assert.Equal(mode == 2 ? 1 : 0, context.Inflate);
            if (mode == 0) expected = pixels;
            Assert.Equal(expected, pixels);
            LodePng.StateCleanup(state);
        }
    }

    [Fact]
    public void OriginalBitReaderHelperMatchesNativeOrdinarySteps()
    {
        var bytes = new byte[96];
        for (int i = 0; i < bytes.Length; ++i) bytes[i] = (byte)(i * 17 + 5);
        int[] steps = { 1, 3, 8, 9, 10, 17, 18, 25, 26, 31, 4, 2 };
        var result = new uint[steps.Length];
        Assert.True(LodePng.TestBitReader(bytes, steps, result));
        using var provenance = Provenance();
        int index = 0;
        foreach (var expected in provenance.RootElement.GetProperty("bitReaderExpected").EnumerateArray())
            Assert.Equal(expected.GetUInt32(), result[index++]);
        Assert.Equal(steps.Length, index);
    }

    [Fact]
    public void ValidIccProfilesMatchNativeFullColorCubeOnThisArchitecture()
    {
        using var provenance = Provenance();
        string arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x86";
        byte[] input = new byte[4913 * 4];
        for (int i = 0; i < 4913; ++i)
        {
            input[i * 4] = (byte)Math.Min(255, (i % 17) * 16);
            input[i * 4 + 1] = (byte)Math.Min(255, ((i / 17) % 17) * 16);
            input[i * 4 + 2] = (byte)Math.Min(255, (i / 289) * 16);
            input[i * 4 + 3] = (byte)(i * 37);
        }
        foreach (var row in provenance.RootElement.GetProperty("iccCube").GetProperty(arch).EnumerateArray())
        {
            byte[] png = File.ReadAllBytes(Path.Combine(Fixtures, row.GetProperty("file").GetString()!));
            Assert.Equal(row.GetProperty("inputSha256").GetString(), Hash(png));
            var state = LodePng.StateInit();
            Assert.Equal(0u, LodePng.DecodeBytes(png, png.Length, state, out _, out _, out _));
            var output = new byte[input.Length];
            Assert.Equal(0u, LodePng.ToSrgb(output, input, 4913, 1, state));
            Assert.Equal(row.GetProperty("nativeRgbaSha256").GetString(), Hash(output));
            LodePng.StateCleanup(state);
        }
    }
}
