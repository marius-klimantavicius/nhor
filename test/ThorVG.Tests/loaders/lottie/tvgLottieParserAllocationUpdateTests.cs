using System;
using System.Text;
using Xunit;

namespace ThorVG.Tests;

public class tvgLottieParserAllocationUpdateTests
{
    [Fact]
    public void SkippingOrdinaryUnusedStringsRetainsTheFollowingValueWithoutPerTokenStrings()
    {
        var source = new StringBuilder("{\"ignored\":[");
        for (int i = 0; i < 2000; ++i)
        {
            if (i != 0) source.Append(',');
            source.Append("{\"name\":\"unused shape\",\"type\":\"rectangle\",\"position\":[10,20]}");
        }
        source.Append("],\"name\":\"Žalias 🙂\"}");
        string json = source.ToString();
        static string? Parse(string json)
        {
            var parser = new LookaheadParserHandler(json);
            if (!parser.ParseNext() || !parser.EnterObject() || parser.NextObjectKey() != "ignored") return null;
            parser.Skip();
            if (parser.NextObjectKey() != "name") return null;
            string? result = parser.GetString();
            if (parser.NextObjectKey() != null || parser.Invalid()) return null;
            return result;
        }
        for (int i = 0; i < 5; ++i) Assert.Equal("Žalias 🙂", Parse(json));
        long before = GC.GetAllocatedBytesForCurrentThread();
        string? actual = Parse(json);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal("Žalias 🙂", actual);
        // One UTF8 source buffer and bounded parser/key metadata are expected.
        // A copied string per ignored token exceeds this input-sized budget.
        Assert.True(allocated < Encoding.UTF8.GetByteCount(json) * 2L + 4096, "Allocated " + allocated);
    }
}
