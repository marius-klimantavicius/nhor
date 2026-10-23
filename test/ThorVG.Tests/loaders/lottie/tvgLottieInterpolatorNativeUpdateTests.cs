using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit;

namespace ThorVG.Tests;

public class tvgLottieInterpolatorNativeUpdateTests
{
    [Fact]
    public void FullMantissaEasingMatchesDefaultNativeCompilerBits()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "fixtures", "thorvg-update", "interpolator-native-golden.json")));
        var root = json.RootElement;
        byte[] inputs = Convert.FromBase64String(root.GetProperty("inputs").GetString()!);
        string platform = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "expectedLinuxArm64" : "expectedLinuxX64";
        byte[] expected = Convert.FromBase64String(root.GetProperty(platform).GetString()!);
        int count = root.GetProperty("cases").GetInt32();
        Assert.Equal(count * 20, inputs.Length);
        Assert.Equal(count * 4, expected.Length);
        for (int i = 0; i < count; ++i)
        {
            int at = i * 20;
            var curve = new LottieInterpolator();
            curve.Set(null, new Point(Read(at), Read(at + 4)), new Point(Read(at + 8), Read(at + 12)));
            uint actual = BitConverter.SingleToUInt32Bits(curve.Progress(Read(at + 16)));
            uint wanted = BitConverter.ToUInt32(expected, i * 4);
            Assert.True(actual == wanted, $"Native easing vector {i}: {actual:x8} expected {wanted:x8}");
        }
        float Read(int offset) => BitConverter.ToSingle(inputs, offset);
    }
}
