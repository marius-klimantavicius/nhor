using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit;

namespace ThorVG.Tests;

public class tvgLottieIntegerTweenUpdateTests
{
    [Fact]
    public void IntegerAndOpacityPropertiesAndTweensMatchEveryNativeBoundaryVector()
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "integer-tween-native-golden.json")));
        var root = data.RootElement;
        byte[] inputs = root.GetProperty("inputBase64").GetBytesFromBase64();
        bool arm = OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        byte[] expected = root.GetProperty(arm ? "expectedLinuxArm64Base64" : "expectedX86Base64").GetBytesFromBase64();
        Assert.Equal(root.GetProperty("cases").GetInt32(), expected.Length);
        using var reader = new BinaryReader(new MemoryStream(inputs));
        for (int i = 0; i < expected.Length; i++)
        {
            byte kind = reader.ReadByte();
            short start = reader.ReadInt16(), end = reader.ReadInt16();
            float progress = reader.ReadSingle();
            using var tween = new LottieTween();
            tween.On(1); tween.progress = progress;
            if (kind == 0)
            {
                var property = new LottieInteger();
                var first = property.NewFrame(); first.no = 0; first.value = (sbyte)start;
                var last = property.NewFrame(); last.no = 1; last.value = (sbyte)end;
                tween.Capture(property, 0, (sbyte)start);
                Assert.Equal(unchecked((sbyte)expected[i]), tween.Run(property, 0, (sbyte)end));
                Assert.Equal(unchecked((sbyte)expected[i]), property.Evaluate(progress));
            }
            else
            {
                var property = new LottieOpacity();
                var first = property.NewFrame(); first.no = 0; first.value = (byte)start;
                var last = property.NewFrame(); last.no = 1; last.value = (byte)end;
                tween.Capture(property, 0, (byte)start);
                Assert.Equal(expected[i], tween.Run(property, 0, (byte)end));
                Assert.Equal(expected[i], property.Evaluate(progress));
            }
        }
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
    }
}
