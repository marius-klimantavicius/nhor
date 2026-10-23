using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

public class HelperNativeTests
{
    private static readonly JsonDocument Corpus = Load();
    private static JsonDocument Load()
    {
        using var raw = typeof(HelperNativeTests).Assembly.GetManifestResourceStream("Marius.Winter.Taffy.Tests.Native.helpers-corpus.json.gz")!;
        using var gzip = new GZipStream(raw, CompressionMode.Decompress); return JsonDocument.Parse(gzip);
    }
    [Fact]
    public void BalancedDynamicProgramMatchesNativePartitions()
    {
        foreach (var test in Corpus.RootElement.GetProperty("balance").EnumerateArray())
        {
            var items = test.GetProperty("items").EnumerateArray().Select(v => BitConverter.UInt32BitsToSingle(v.GetUInt32())).ToArray();
            var actual = FlexBalance.BalancedLineItemCounts(items, BitConverter.UInt32BitsToSingle(test.GetProperty("limit").GetUInt32()), BitConverter.UInt32BitsToSingle(test.GetProperty("gap").GetUInt32()), test.GetProperty("minimum").GetInt32());
            Assert.Equal(test.GetProperty("expected").EnumerateArray().Select(v => v.GetInt32()), actual);
        }
    }
    [Fact]
    public void SubnormalFreeSpaceIsNotDistributedAsNormalFreeSpace()
    {
        var tree = new TaffyTree<object>(); tree.DisableRounding();
        var child = tree.NewLeaf(new Style { FlexGrowValue = 1 });
        var root = tree.NewWithChildren(new Style { SizeValue = new(Dimension.Length(1e-40f), Dimension.Length(10)) }, [child]);
        tree.ComputeLayout(root, new(AvailableSpace.MaxContent, AvailableSpace.MaxContent));
        Assert.Equal(Corpus.RootElement.GetProperty("tiny_width").GetUInt32(), BitConverter.SingleToUInt32Bits(tree.GetLayout(child).Size.Width));
    }
    private sealed class FailingWriter : StringWriter
    {
        public override void Write(string? value) => throw new IOException("writer failed");
    }
    [Fact]
    public void TreeDebugOutputAndWriterErrorsMatchNativeContract()
    {
        var tree = new TaffyTree<object>();
        var child = tree.NewLeaf(new Style { SizeValue = new(Dimension.Length(10.25f), Dimension.Length(7.5f)) });
        var root = tree.NewWithChildren(new Style { Display = Display.FlowRoot, DirectionValue = Direction.Rtl, SizeValue = new(Dimension.Length(30), Dimension.Length(20)) }, [child]);
        tree.ComputeLayout(root, new(AvailableSpace.MaxContent, AvailableSpace.MaxContent));
        using var writer = new StringWriter(); tree.WriteTree(writer, root);
        Assert.Equal(Corpus.RootElement.GetProperty("printed").GetString(), writer.ToString());
        Assert.Throws<IOException>(() => tree.WriteTree(new FailingWriter(), root));
    }
    [Fact]
    public void RoughAvailableSpaceEqualityUsesFloatMachineEpsilon()
    {
        var zero = AvailableSpace.Definite(0); float epsilon = 1.1920928955078125e-7f;
        Assert.True(zero.IsRoughlyEqual(AvailableSpace.Definite(epsilon / 2)));
        Assert.False(zero.IsRoughlyEqual(AvailableSpace.Definite(epsilon)));
        Assert.True(AvailableSpace.MinContent.IsRoughlyEqual(AvailableSpace.MinContent));
        Assert.False(AvailableSpace.MinContent.IsRoughlyEqual(AvailableSpace.MaxContent));
        Assert.False(AvailableSpace.Definite(float.PositiveInfinity).IsRoughlyEqual(AvailableSpace.Definite(float.PositiveInfinity)));
    }
    [Fact]
    public void ExpandedGridTrackValuesRoundTripEveryNativeVariant()
    {
        foreach (var value in new[] { MaxTrackSizingFunction.Length(12), MaxTrackSizingFunction.Percent(.5f), MaxTrackSizingFunction.AUTO, MaxTrackSizingFunction.MIN_CONTENT, MaxTrackSizingFunction.MAX_CONTENT, MaxTrackSizingFunction.FitContentPx(30), MaxTrackSizingFunction.FitContentPercent(.75f), MaxTrackSizingFunction.Fr(2), MaxTrackSizingFunction.Calc(new(0x1000)), MaxTrackSizingFunction.FitContentCalc(new(0x1000)) })
        { Assert.Equal(value, (MaxTrackSizingFunction)value.Expand()); Assert.Equal(value.Expand(), ((ExpandedMaxTrackSizingFunction)value)); }
        foreach (var value in new[] { MinTrackSizingFunction.Length(12), MinTrackSizingFunction.Percent(.5f), MinTrackSizingFunction.AUTO, MinTrackSizingFunction.MIN_CONTENT, MinTrackSizingFunction.MAX_CONTENT, MinTrackSizingFunction.Calc(new(0x1000)) })
        { Assert.Equal(value, (MinTrackSizingFunction)value.Expand()); Assert.Equal(value.Expand(), ((ExpandedMinTrackSizingFunction)value)); }
        var calc = MaxTrackSizingFunction.FitContentCalc(new(0x1000));
        Assert.Null(calc.FitContentLimit(null, (_, basis) => basis + 5));
        Assert.Equal(25f, calc.FitContentLimit(20, (_, basis) => basis + 5));
        Assert.Equal(25f, calc.DefiniteLimit(20, (_, basis) => basis + 5));
        Assert.True(MinTrackSizingFunction.Percent(.5f).BehavesAsAuto(null)); Assert.False(MinTrackSizingFunction.Percent(.5f).BehavesAsAuto(10));
    }
    [Fact]
    public void StableOrderingPreservesAllTiedItemsAcrossInlineAndSpilledStorage()
    {
        foreach (int count in new[] { 0, 1, 7, 8, 9, 32, 257 })
        {
            var values = new ValueList<(int Key, int Original)>();
            for (int i = 0; i < count; i++) values.Add(((count - i) % 5, i));
            values.StableSort((a, b) => a.Key.CompareTo(b.Key));
            var expected = Enumerable.Range(0, count).OrderBy(i => (count - i) % 5);
            Assert.Equal(expected, values.AsSpan().ToArray().Select(v => v.Original));
        }
    }
    [Fact]
    public void CalcTagsPreserveNativeAddressBitsAndClassifyThroughMasks()
    {
        foreach (long handle in new long[] { 8, 16, 248, 0x1000, 0x1018, 0x7fff_ffff_fff8 })
        {
            if (IntPtr.Size == 4 && handle > int.MaxValue) continue;
            var pointer = new IntPtr(handle); var plain = CompactLength.Calc(pointer); var fit = CompactLength.FitContent(LengthPercentage.Calc(pointer));
            Assert.Equal(IntPtr.Size == 8 ? (byte)(handle | CompactLength.CALC_TAG) : CompactLength.CALC_TAG, plain.Tag);
            Assert.True(plain.IsPlainCalc()); Assert.False(plain.IsSizingKeyword()); Assert.False(plain.IsIntrinsic());
            Assert.True(fit.IsFitContentCalc()); Assert.True(fit.IsSizingKeyword()); Assert.True(fit.IsIntrinsic());
            Assert.Equal(pointer, plain.CalcValue); Assert.Equal(pointer, fit.CalcValue);
            Assert.Throws<JsonException>(() => TaffyStyleJson.Serialize(plain));
            Assert.Throws<JsonException>(() => TaffyStyleJson.Serialize(fit));
        }
    }
}
