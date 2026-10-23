using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit;

namespace ThorVG.Tests;

public class tvgFlattenNativeUpdateTests
{
    [Fact]
    public void BezierFlattenMatchesEveryNativeUlpBoundaryPredicate()
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "flatten-boundary-native-golden.json")));
        string arch = OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "LinuxArm64" : "X86";
        using var input = new BinaryReader(new MemoryStream(data.RootElement.GetProperty("input" + arch + "Base64").GetBytesFromBase64()));
        byte[] expected = data.RootElement.GetProperty("expected" + arch + "Base64").GetBytesFromBase64();
        int index = 0;
        while (input.BaseStream.Position < input.BaseStream.Length)
        {
            var start = new Point(input.ReadSingle(), input.ReadSingle());
            var first = new Point(input.ReadSingle(), input.ReadSingle());
            var second = new Point(input.ReadSingle(), input.ReadSingle());
            var end = new Point(input.ReadSingle(), input.ReadSingle());
            var bezier = new Bezier(start, first, second, end);
            float metric = input.ReadSingle();
            foreach (float tolerance in new[] { MathF.BitDecrement(metric), metric, MathF.BitIncrement(metric) })
                Assert.Equal(expected[index++] != 0, bezier.Flatten(tolerance));
        }
        Assert.Equal(expected.Length, index);
        Assert.Equal(data.RootElement.GetProperty("cases").GetInt32(), index);
    }
}
