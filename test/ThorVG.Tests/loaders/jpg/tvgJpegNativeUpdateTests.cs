using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace ThorVG.Tests;

public class tvgJpegNativeUpdateTests
{
    private static string Fixtures => Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update");

    [Fact]
    public void AllSparseIdctLengthsMatchTheCompleteNativeBlocks()
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "jpeg-idct-native-golden.json")));
        byte[] expected = File.ReadAllBytes(Path.Combine(Fixtures, "jpeg-idct-native-golden.bin"));
        var input = new short[64];
        var output = new byte[64];
        int index = 0;
        foreach (var row in data.RootElement.GetProperty("cases").EnumerateArray())
        {
            int n = 0;
            foreach (var v in row.GetProperty("coefficients").EnumerateArray()) input[n++] = v.GetInt16();
            JpgdIdct.idct(input, output, row.GetProperty("maxZag").GetInt32());
            Assert.True(expected.AsSpan(index * 64, 64).SequenceEqual(output), "Native IDCT block " + index);
            ++index;
        }
        Assert.Equal(expected.Length, index * 64);
    }

    [Fact]
    public void ValidSequentialProgressiveSubsampledAndRestartImagesMatchNativePixels()
    {
        string folder = Path.Combine(Fixtures, "normal-jpeg");
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "provenance.json")));
        foreach (var row in data.RootElement.GetProperty("cases").EnumerateArray())
        {
            string file = row.GetProperty("file").GetString()!;
            string path = Path.Combine(folder, file);
            byte[] bytes = File.ReadAllBytes(path);
            Assert.Equal(row.GetProperty("inputSha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            for (int source = 0; source < 2; ++source)
            {
                int width, height;
                using var decoder = source == 0 ? JpegDecoder.FromData(bytes, bytes.Length, out width, out height)
                    : JpegDecoder.FromFile(path, out width, out height);
                Assert.NotNull(decoder);
                Assert.Equal(row.GetProperty("width").GetInt32(), width);
                Assert.Equal(row.GetProperty("height").GetInt32(), height);
                uint[]? pixels = decoder!.Decompress();
                Assert.NotNull(pixels);
                string hash = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(pixels.AsSpan()))).ToLowerInvariant();
                Assert.True(row.GetProperty("decodedSha256").GetString() == hash, file + ": source=" + source + ",hash=" + hash);
            }
        }
    }

    [Fact]
    public void FileHeadersStreamAndDecompressionAllocatesOneFullImage()
    {
        string path = Path.Combine(Fixtures, "normal-jpeg", "stream-large.jpg");
        for (int i = 0; i < 3; ++i)
        {
            using var warm = JpegDecoder.FromFile(path, out _, out _);
            Assert.NotNull(warm);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        using var decoder = JpegDecoder.FromFile(path, out int width, out int height);
        long headerBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.NotNull(decoder);
        Assert.Equal(511, width);
        Assert.Equal(513, height);
        Assert.True(headerBytes < new FileInfo(path).Length / 2, "Header allocated " + headerBytes);
        before = GC.GetAllocatedBytesForCurrentThread();
        uint[]? pixels = decoder!.Decompress();
        long decodeBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.NotNull(pixels);
        Assert.Equal(width * height, pixels!.Length);
        Assert.True(decodeBytes < width * height * 6L, "Decode allocated " + decodeBytes);
        decoder.Dispose();
        using var reopened = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.True(reopened.CanRead);
    }
}
