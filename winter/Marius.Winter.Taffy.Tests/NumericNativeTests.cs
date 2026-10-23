using System;
using System.IO.Compression;
using System.Text.Json;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

public class NumericNativeTests
{
    [Fact]
    public void HelpersMatchPinnedRustBitsAndDisplayFormatting()
    {
        using var raw = typeof(NumericNativeTests).Assembly.GetManifestResourceStream("Marius.Winter.Taffy.Tests.Native.numeric-corpus.json.gz")!;
        using var gzip = new GZipStream(raw, CompressionMode.Decompress); using var document = JsonDocument.Parse(gzip);
        foreach (var test in document.RootElement.EnumerateArray())
        {
            float a = BitConverter.UInt32BitsToSingle(test.GetProperty("a").GetUInt32()), b = BitConverter.UInt32BitsToSingle(test.GetProperty("b").GetUInt32());
            Assert.Equal(test.GetProperty("minimum").GetUInt32(), BitConverter.SingleToUInt32Bits(TaffySys.F32Min(a, b)));
            Assert.Equal(test.GetProperty("maximum").GetUInt32(), BitConverter.SingleToUInt32Bits(TaffySys.F32Max(a, b)));
            Assert.Equal(test.GetProperty("rounded").GetUInt32(), BitConverter.SingleToUInt32Bits(TaffySys.Round(a)));
            Assert.Equal(test.GetProperty("formatted").GetString(), TaffySys.FormatFloat(a));
        }
    }
}
