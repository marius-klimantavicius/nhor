using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace ThorVG.Tests;

public class tvgClampNativeUpdateTests
{
    [Fact]
    public void GenericFloatingClampPreservesEveryNativeOrderedOperandAndBit()
    {
        using var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "fixtures", "thorvg-update", "math-clamp-native-golden.json")));
        var root = golden.RootElement;
        using var input = new BinaryReader(new MemoryStream(root.GetProperty("inputBase64").GetBytesFromBase64()));
        using var expected = new BinaryReader(new MemoryStream(root.GetProperty("nativeBase64").GetBytesFromBase64()));
        for (int i = 0; i < root.GetProperty("cases").GetInt32(); ++i)
        {
            ulong kind = input.ReadUInt64(), value = input.ReadUInt64(), lower = input.ReadUInt64(), upper = input.ReadUInt64();
            ulong actual;
            if (kind == 0)
            {
                float result = TvgMath.Clamp<float>(BitConverter.UInt32BitsToSingle((uint)value),
                    BitConverter.UInt32BitsToSingle((uint)lower), BitConverter.UInt32BitsToSingle((uint)upper));
                actual = BitConverter.SingleToUInt32Bits(result);
            }
            else
            {
                double result = TvgMath.Clamp<double>(BitConverter.UInt64BitsToDouble(value),
                    BitConverter.UInt64BitsToDouble(lower), BitConverter.UInt64BitsToDouble(upper));
                actual = BitConverter.DoubleToUInt64Bits(result);
            }
            Assert.Equal(expected.ReadUInt64(), actual);
        }
        Assert.Equal(input.BaseStream.Length, input.BaseStream.Position);
        Assert.Equal(expected.BaseStream.Length, expected.BaseStream.Position);

        long before = GC.GetAllocatedBytesForCurrentThread();
        double checksum = 0;
        for (int i = 0; i < 10000; ++i)
            checksum += TvgMath.Clamp<float>(i, -1, 1) + TvgMath.Clamp<double>(i, -1, 1);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(19998, checksum);
        Assert.Equal(0, allocated);
    }
}
