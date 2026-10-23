using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit;

namespace ThorVG.Tests;

public class tvgLottiePoint3UpdateTests
{
    [Fact]
    public void ThreeComponentPropertiesAndTweensMatchThePinnedNativeBits()
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "point3-native-golden.json")));
        bool arm = OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        foreach (var row in data.RootElement.GetProperty("cases").EnumerateArray())
        {
            var start = Point(row.GetProperty("start"));
            var end = Point(row.GetProperty("end"));
            float t = BitConverter.UInt32BitsToSingle(row.GetProperty("position").GetUInt32());
            var expected = row.GetProperty(arm ? "expectedLinuxArm64" : "expectedX86");
            var property = new LottieScalar3();
            var first = property.NewFrame(); first.no = 0; first.value = start;
            var second = property.NewFrame(); second.no = 1; second.value = end;
            Check(expected, property.Evaluate(t));
            using var tween = new LottieTween();
            tween.On(1); tween.Capture(property, 0, start); tween.progress = t;
            Check(expected, tween.Run(property, 0, end));
        }
    }

    private static Point3 Point(JsonElement value) => new(BitConverter.UInt32BitsToSingle(value[0].GetUInt32()),
        BitConverter.UInt32BitsToSingle(value[1].GetUInt32()), BitConverter.UInt32BitsToSingle(value[2].GetUInt32()));

    private static void Check(JsonElement expected, Point3 actual)
    {
        Assert.Equal(expected[0].GetUInt32(), BitConverter.SingleToUInt32Bits(actual.x));
        Assert.Equal(expected[1].GetUInt32(), BitConverter.SingleToUInt32Bits(actual.y));
        Assert.Equal(expected[2].GetUInt32(), BitConverter.SingleToUInt32Bits(actual.z));
    }
}
