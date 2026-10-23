using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace ThorVG.Tests;

public class tvgStringMidpointUpdateTests
{
    [Fact]
    public void IntegerAndFractionConversionsMatchEveryNativeMidpointAndEndPosition()
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "fixtures", "thorvg-update", "string-midpoint-native-golden.json")));
        foreach (var row in data.RootElement.GetProperty("cases").EnumerateArray())
        {
            string input = row[0].GetString()!;
            int end = 0;
            float value = TvgStr.ToFloat(input, ref end);
            string actual = BitConverter.SingleToUInt32Bits(value).ToString("x8") + "," + end;
            Assert.True(row[1].GetString() == actual, input + ": " + actual + " != " + row[1].GetString());
        }
    }
}
