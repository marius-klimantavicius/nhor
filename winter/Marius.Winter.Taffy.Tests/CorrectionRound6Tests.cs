using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

public class CorrectionRound6Tests
{
    public static IEnumerable<object[]> ChunkBoundaries()
    {
        foreach (var mode in new[] { WritingMode.Horizontal, WritingMode.Vertical })
        foreach (int offset in new[] { -1, 0, 1 })
        foreach (var glyph in new[] { "\u0800", "😀", "\U0010ffff" })
            yield return [mode, offset, glyph];
    }

    [Theory]
    [MemberData(nameof(ChunkBoundaries))]
    public void RealLeafMeasurementCountsAcrossUtf8Chunks(WritingMode mode, int offset, string glyph)
    {
        int prefix = TestHelpers.Utf8CountChunkLength + offset;
        string segment = new string('X', prefix) + glyph + new string('\u0800', TestHelpers.Utf8CountChunkLength);
        // Includes leading/trailing/adjacent empty segments and a second
        // multi-byte segment. The native Ahem sum excludes separator bytes.
        string text = "\u200b" + segment + "\u200b\u200bé\u200b";
        nuint bytes = (nuint)(prefix + (glyph.Length == 1 ? 3 : 4) + TestHelpers.Utf8CountChunkLength * 3);
        Assert.Equal(bytes, TestHelpers.CountUtf8Bytes(segment));
        AssertLeaf(mode, text, AvailableSpace.MaxContent, (float)(bytes + 2) * 10f, 10f);
        AssertLeaf(mode, text, AvailableSpace.MinContent, (float)bytes * 10f, 20f);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\u200b")]
    [InlineData("\u200b\u200b")]
    public void EmptySegmentsRetainOneLine(string text)
    {
        Assert.Equal((nuint)0, TestHelpers.CountUtf8Bytes(ReadOnlySpan<char>.Empty));
        AssertLeaf(WritingMode.Horizontal, text, AvailableSpace.MaxContent, 0, 10);
        AssertLeaf(WritingMode.Vertical, text, AvailableSpace.MinContent, 0, 10);
    }

    [Theory]
    [InlineData(WritingMode.Horizontal)]
    [InlineData(WritingMode.Vertical)]
    public void ByteAccumulationWrapsOnlyAfterExceedingInlineLength(WritingMode mode)
    {
        // UTF-8 lengths 3 + 3 exactly fill the first line; the next byte wraps.
        AssertLeaf(mode, "\u200b\u0800\u200b\u200b\u0800\u200bX\u200b", AvailableSpace.Definite(60), 60, 20);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ChunkingPreservesEncodingReplacementBehavior(int scenario)
    {
        // Build malformed UTF-16 here: attribute serialization can normalize
        // lone surrogates and collapse distinct xUnit case identities.
        string suffix = scenario switch { 0 => "\ud800", 1 => "\udc00", 2 => "\ud800X\udc00", _ => "\ud800\ud800\udc00" };
        var text = new string('X', TestHelpers.Utf8CountChunkLength - 1) + suffix;
        // Invalid UTF-16 has no Rust str equivalent; retain normal CLR UTF-8
        // fallback behavior instead of treating a valid boundary pair as invalid.
        nuint expected = (nuint)Encoding.UTF8.GetByteCount(text);
        Assert.Equal(expected, TestHelpers.CountUtf8Bytes(text));
        AssertLeaf(WritingMode.Horizontal, text, AvailableSpace.MaxContent, (float)expected * 10f, 10f);
    }

    private static void AssertLeaf(WritingMode mode, string text, AvailableSpace available, float inline, float block)
    {
        var style = new Style();
        var context = TestNodeContext.AhemText(text, mode);
        var input = new LayoutInput { KnownDimensions = new(null, null), AvailableSpace = new(available, available),
            RunMode = RunMode.ComputeSize, SizingMode = SizingMode.ContentSize, Axis = RequestedAxis.Both };
        var output = LeafCompute.ComputeLeafLayout(input, style, (_, _) => 0,
            (known, space) => TestHelpers.MeasureFunction(known, space, new(0), context, style));
        var expected = mode == WritingMode.Horizontal ? new Size<float>(inline, block) : new(block, inline);
        Assert.Equal(BitConverter.SingleToUInt32Bits(expected.Width), BitConverter.SingleToUInt32Bits(output.Size.Width));
        Assert.Equal(BitConverter.SingleToUInt32Bits(expected.Height), BitConverter.SingleToUInt32Bits(output.Size.Height));
        Assert.True(context.Count > 0);
    }
}
