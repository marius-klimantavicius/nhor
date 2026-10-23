using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ThorVG.Tests;

public class tvgLottieJsonNativeUpdateTests
{
    [Fact]
    public void DecimalConversionMatchesEveryPinnedNativeMidpointAndExponentVector()
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "json-number-native-golden.json")));
        int expectedIndex = OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? 2 : 1;
        foreach (var row in data.RootElement.GetProperty("cases").EnumerateArray())
        {
            string input = row[0].GetString()!;
            var handler = new LookaheadParserHandler("{\"value\":" + input + "}");
            Assert.True(handler.ParseNext());
            Assert.True(handler.EnterObject());
            Assert.Equal("value", handler.NextObjectKey());
            string actual = handler.PeekType() == LookaheadParserHandler.kNumberType
                ? BitConverter.SingleToUInt32Bits(handler.GetFloat()).ToString("x8")
                : "not-number";
            actual += "," + (handler.Invalid() ? 1 : 0);
            Assert.True(row[expectedIndex].GetString() == actual, input + ": " + actual + " != " + row[expectedIndex].GetString());
        }
    }

    public static IEnumerable<object[]> Cases()
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "json-handler-native-golden.json")));
        bool arm = OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        foreach (var row in data.RootElement.GetProperty("cases").EnumerateArray())
            yield return new object[] { row.GetProperty("name").GetString()!, row.GetProperty("mode").GetString()!, row.GetProperty("input").GetString()!, row.GetProperty(arm ? "expectedLinuxArm64" : "expectedX86").GetString()! };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void HandlerMatchesTheCompletePinnedNativeProtocol(string name, string mode, string json, string expected)
    {
        var handler = new LookaheadParserHandler(json);
        bool parsed = handler.ParseNext();
        var output = new StringBuilder("parse=" + (parsed ? 1 : 0));
        if (parsed && handler.EnterObject())
        {
            int steps = 0;
            string? key;
            while ((key = handler.NextObjectKey()) != null)
            {
                Assert.True(++steps <= 32, name + ": unexpected parser loop");
                if (key != "value") { handler.Skip(); continue; }
                output.Append(",value=");
                if (mode == "bool") output.Append(handler.GetBool() ? 1 : 0);
                else if (mode == "int") output.Append(handler.GetInt());
                else if (mode == "float")
                {
                    float value = handler.GetFloat();
                    output.Append(float.IsNaN(value) ? "nan" : BitConverter.SingleToUInt32Bits(value).ToString("x8"));
                }
                else if (mode == "copy")
                {
                    string? value = handler.GetStringCopy();
                    output.Append(value == null ? "null" : Convert.ToHexString(Encoding.UTF8.GetBytes(value)).ToLowerInvariant());
                }
                else { output.Append("skipped"); handler.Skip(); }
            }
        }
        output.Append(",invalid=").Append(handler.Invalid() ? 1 : 0).Append('\n');
        Assert.Equal(expected, output.ToString());
    }
}
