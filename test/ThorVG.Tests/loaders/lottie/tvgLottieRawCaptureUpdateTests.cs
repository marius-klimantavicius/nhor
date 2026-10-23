using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ThorVG.Tests;

public class tvgLottieRawCaptureUpdateTests
{
    [Fact]
    public void TypeAndSlotLookaheadMatchEveryPinnedNativeSourceProfile()
    {
        using var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "fixtures", "thorvg-update", "lottie-raw-capture-native-golden.json")));
        var parseObject = typeof(LottieParser).GetMethod("ParseObject", BindingFlags.Instance | BindingFlags.NonPublic,
            null, new[] { typeof(List<LottieObject>) }, null)!;
        var captureSlots = typeof(LottieParser).GetMethod("CaptureSlots", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var row in golden.RootElement.GetProperty("cases").EnumerateArray())
        {
            string name = row.GetProperty("name").GetString()!;
            var parser = new LottieParser(row.GetProperty("input").GetString()!, null, false);
            Assert.True(parser.ParseNext());
            string result;
            if (row.GetProperty("mode").GetString() == "slots")
            {
                Assert.True(parser.EnterObject());
                var key = parser.NextObjectKey();
                captureSlots.Invoke(parser, new object?[] { key });
                result = "slots=" + (parser.slots == null ? "null" : Convert.ToHexString(Encoding.UTF8.GetBytes(parser.slots)).ToLowerInvariant());
            }
            else
            {
                var objects = new List<LottieObject>();
                parseObject.Invoke(parser, new object[] { objects });
                result = "count=" + objects.Count;
                foreach (var obj in objects) result += ",type=" + (int)obj.type + ",id=" + obj.id;
            }
            result += ",invalid=" + (parser.Invalid() ? 1 : 0) + "\n";
            Assert.Equal(name + ":" + row.GetProperty("expected").GetString(), name + ":" + result);
        }
    }
}
