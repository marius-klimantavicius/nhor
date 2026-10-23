using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ThorVG.Tests;

public class tvgTextStateNativeUpdateTests
{
    private static void Number(StringBuilder output, float value) => output.Append(float.IsNaN(value) ? "nan" : BitConverter.SingleToUInt32Bits(value).ToString("x8")).Append(',');
    private static void CString(StringBuilder output, string? value) => output.Append(value == null ? "null" : Convert.ToHexString(Encoding.UTF8.GetBytes(TvgStr.Duplicate(value)!)).ToLowerInvariant());

    private static void State(StringBuilder output, Text text, string? value)
    {
        var result = text.GetMetrics(out TextMetrics metrics);
        output.Append((int)result).Append(',');
        if (result == Result.Success)
            foreach (float number in new[] { metrics.ascent, metrics.descent, metrics.linegap, metrics.advance }) Number(output, number);
        result = text.GetMetrics(value, out GlyphMetrics glyph, out int next);
        output.Append((int)result).Append(',');
        if (result == Result.Success)
        {
            foreach (float number in new[] { glyph.advance, glyph.bearing, glyph.min.x, glyph.min.y, glyph.max.x, glyph.max.y }) Number(output, number);
            CString(output, value![next..]);
        }
        result = text.Bounds(out float x, out float y, out float width, out float height);
        output.Append(',').Append((int)result).Append(',');
        if (result == Result.Success)
            foreach (float number in new[] { x, y, width, height }) Number(output, number);
        output.Append(text.Lines()).Append(',');
        CString(output, text.GetText());
    }

    [Fact]
    public void LayoutMetricsAndDuplicatesMatchEveryPinnedNativeState()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "text-states-native-golden.json")));
        byte[] font = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "legacy-upstream", "resources", "DMSans.otf"));
        Assert.Equal(document.RootElement.GetProperty("fontSha256").GetString(), Convert.ToHexString(SHA256.HashData(font)).ToLowerInvariant());
        string name = "text-state-native-" + Guid.NewGuid().ToString("N");
        bool arm = OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        Assert.Equal(Result.Success, Initializer.Init(0));
        try
        {
            Assert.Equal(Result.Success, Text.LoadFont(name, font, (uint)font.Length, "otf", true));
            foreach (var row in document.RootElement.GetProperty("cases").EnumerateArray())
            {
                var text = Text.Gen();
                Text? duplicate = null;
                try
                {
                    Assert.Equal(Result.Success, text.SetFont(name));
                    text.SetHinting(false); text.SetFontSize(24);
                    float size = BitConverter.ToSingle(Convert.FromHexString(row.GetProperty("sizeBits").GetString()!));
                    var result = text.SetFontSize(size);
                    string? value = row.GetProperty("text").GetString();
                    int wrap = row.GetProperty("wrap").GetInt32();
                    text.SetText(value); text.SetWrapping((TextWrap)wrap);
                    text.SetLayout(wrap != 0 ? 64f : 0f, wrap != 0 ? 40f : 0f);
                    text.SetAlign(.5f, 1f); text.SetSpacing(1.25f, 1.5f); text.SetItalic(.2f); text.SetOutline(.5f, 10, 20, 30);
                    var output = new StringBuilder().Append((int)result).Append(',');
                    State(output, text, value);
                    duplicate = (Text)text.Duplicate();
                    output.Append('|');
                    State(output, duplicate, value);
                    Assert.Equal(row.GetProperty(arm ? "expectedLinuxArm64" : "expectedX86").GetString(), output.ToString());
                }
                finally { Paint.Rel(duplicate); Paint.Rel(text); }
            }
        }
        finally { Assert.Equal(Result.Success, Initializer.Term()); }
    }
}
