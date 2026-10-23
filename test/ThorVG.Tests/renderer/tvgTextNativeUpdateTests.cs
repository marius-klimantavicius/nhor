using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace ThorVG.Tests;

public class tvgTextNativeUpdateTests
{
    [Theory]
    [InlineData("conic")]
    [InlineData("linear")]
    [InlineData("radial")]
    public void EveryTextGradientScaleMatchesTheCompleteNativePixels(string type)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "text-gradients-native-golden.json")));
        var root = document.RootElement;
        var font = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "legacy-upstream", "resources", "DMSans.otf"));
        Assert.Equal(root.GetProperty("fontSha256").GetString(), Convert.ToHexString(SHA256.HashData(font)).ToLowerInvariant());
        string name = "text-gradient-native-" + Guid.NewGuid().ToString("N");
        Assert.Equal(Result.Success, Initializer.Init(2));
        try
        {
            Assert.Equal(Result.Success, Text.LoadFont(name, font, (uint)font.Length, "otf", true));
            var text = Text.Gen();
            Assert.Equal(Result.Success, text.SetFont(name));
            text.SetHinting(false); text.SetFontSize(24); text.SetText("Ag"); text.Translate(4, 6);
            Fill fill;
            if (type == "conic") { var gradient = ConicGradient.Gen(); gradient.Conic(32, 32, 0); fill = gradient; }
            else if (type == "radial") { var gradient = RadialGradient.Gen(); gradient.Radial(32, 32, 8, 32, 32, 0); fill = gradient; }
            else { var gradient = LinearGradient.Gen(); gradient.Linear(0, 0, 8, 0); fill = gradient; }
            fill.SetColorStops(new Fill.ColorStop[] { new(0, 255, 0, 0, 255), new(.35f, 0, 255, 0, 160), new(1, 0, 0, 255, 255) }, 3);
            fill.SetSpread(FillSpread.Repeat); text.SetFill(fill);
            using var canvas = SwCanvas.Gen(EngineOption.None)!;
            var pixels = new uint[96 * 64];
            Assert.Equal(Result.Success, canvas.Target(pixels, 96, 96, 64, ColorSpace.ABGR8888));
            Assert.Equal(Result.Success, canvas.Add(text));
            JsonElement row = default;
            foreach (var entry in root.GetProperty("cases").EnumerateArray())
                if (entry.GetProperty("type").GetString() == type) row = entry;
            bool arm = OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
            var expected = row.GetProperty(arm ? "expectedLinuxArm64" : "expectedX86");
            int index = 0;
            foreach (float scale in new[] { 1f, 2f, 4f, .5f })
            {
                text.Scale(scale);
                Assert.Equal(Result.Success, canvas.Update());
                Assert.Equal(Result.Success, canvas.Draw(true));
                Assert.Equal(Result.Success, canvas.Sync());
                string hash = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(pixels.AsSpan()))).ToLowerInvariant();
                Assert.Equal(expected[index++].GetString(), hash);
            }
            Assert.Equal(Result.Success, canvas.Remove());
        }
        finally { Assert.Equal(Result.Success, Initializer.Term()); }
    }

    [Theory]
    [InlineData("A\0B", "A")]
    [InlineData("é😀\0ignored", "é😀")]
    [InlineData("\0ignored", "")]
    [InlineData(null, null)]
    public void TextValuePreservesTheNativeCStringBoundary(string? input, string? expected)
    {
        var text = Text.Gen();
        try
        {
            Assert.Equal(Result.Success, text.SetText(input));
            Assert.Equal(expected, text.GetText());
        }
        finally { Paint.Rel(text); }
    }
}
