using System;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

public class GenerationAndNumericTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StaleKeysCannotAccessReusedSlots(bool clear)
    {
        var tree = new TaffyTree<object>();
        var old = tree.NewLeafWithContext(new Style(), new object());
        if (clear) tree.Clear(); else tree.Remove(old);
        var current = tree.NewLeaf(new Style());
        Assert.NotEqual(old, current);
        Assert.Throws<TaffyInvalidInputNodeError>(() => tree.GetStyle(old));
        Assert.Throws<TaffyInvalidInputNodeError>(() => tree.SetStyle(old, new Style()));
        Assert.Null(tree.GetNodeContext(old)); // Native SecondaryMap::get returns None for a stale key.
        Assert.False(tree.TryGetNodeContext(old, out _));
        Assert.Null(tree.GetNodeContext(current));
        Assert.Equal(1, tree.TotalNodeCount());
    }

    [Fact]
    public void AllLayoutInputConstructionPathsDefaultToDefinite()
    {
        var inputs = new[] { default(LayoutInput), new LayoutInput(), new LayoutInput { KnownDimensions = new(20, 30) }, (new LayoutInput[1])[0] };
        foreach (var input in inputs)
        {
            Assert.True(input.KnownDimensionsAreDefinite.Width);
            Assert.True(input.KnownDimensionsAreDefinite.Height);
        }
        var changed = default(LayoutInput); changed.KnownDimensionsAreDefinite.Width = false;
        Assert.False(changed.KnownDimensionsAreDefinite.Width);
        Assert.True(changed.KnownDimensionsAreDefinite.Height);
        Assert.False(changed.Transpose().KnownDimensionsAreDefinite.Height);
    }

    [Theory]
    [InlineData(-1.5f, -1f)]
    [InlineData(-0.5f, 0f)]
    [InlineData(0.5f, 1f)]
    [InlineData(1.5f, 2f)]
    public void RoundingUsesPinnedNativeStdSemantics(float value, float expected) => Assert.Equal(expected, TaffySys.Round(value));

    [Fact]
    public void CompactEqualityAndHashesPreserveNaNPayloadsAndSignedZero()
    {
        foreach (uint bits in new uint[] { 0, 0x80000000, 0x7fc00001, 0x7fc00002, 0xffc00001 })
        {
            var first = CompactLength.Length(BitConverter.UInt32BitsToSingle(bits));
            var copy = CompactLength.Length(BitConverter.UInt32BitsToSingle(bits));
            Assert.Equal(first, copy); Assert.Equal(first.GetHashCode(), copy.GetHashCode());
        }
        Assert.NotEqual(CompactLength.Length(0), CompactLength.Length(-0f));
        Assert.NotEqual(CompactLength.Length(BitConverter.UInt32BitsToSingle(0x7fc00001)), CompactLength.Length(BitConverter.UInt32BitsToSingle(0x7fc00002)));
        Assert.Equal((float)(1.0 / 3), Dimension.FromLength(1.0 / 3).GetValue());
    }

    [Fact]
    public void ClonedOutputsOwnTheirCandidateLists()
    {
        var output = LayoutOutput.FromOuterSize(default);
        output.OofCandidates.Push(new OofCandidate { Node = new NodeId(1), StaticPosition = new(AxisStaticPosition.FromEdge(2, AxisStaticEdge.Start), default) });
        var copy = output.Clone(); copy.OofCandidates.Translate(new(10, 0));
        Assert.Equal(2f, output.OofCandidates.AsSpan()[0].StaticPosition.X.Area.Start);
        Assert.Equal(12f, copy.OofCandidates.AsSpan()[0].StaticPosition.X.Area.Start);
    }

    [Fact]
    public void LargeNamedSpansAndMissingIndicesClampWithoutSignedOverflow()
    {
        var resolver = new NamedLineResolver(new Style(), 0, 0); resolver.SetExplicitColumnCount(10000);
        foreach (var (index, expected) in new[] { (short.MaxValue, (short)10001), (short.MinValue, (short)-20001) })
            Assert.Equal(expected, resolver.ResolveColumnNames(new(GridPlacement.FromNamedLine("missing", index), GridPlacement.Auto)).Start.LineValue.AsI16());
        var resolved = resolver.ResolveColumnNames(new(GridPlacement.FromLine(1), GridPlacement.FromNamedSpan("missing", ushort.MaxValue)));
        Assert.Equal(10001, resolved.End.LineValue.AsI16());
        Assert.Equal(10000, new Line<GridPlacement>(GridPlacement.FromSpanCount(ushort.MaxValue), GridPlacement.Auto).IntoOriginZeroIgnoringNamed(0).IndefiniteSpan());
        Assert.Equal(1, new Line<GridPlacement>(GridPlacement.FromSpanCount(0), GridPlacement.Auto).IntoOriginZeroIgnoringNamed(0).IndefiniteSpan());
    }

    [Fact]
    public void ReplacedElementsAreAlsoCompressible()
    {
        ICoreStyle replaced = new Style { ItemIsReplaced = true };
        Assert.True(replaced.IsReplaced()); Assert.True(replaced.IsCompressibleReplaced());
        ICoreStyle compressible = new Style { ItemIsCompressibleReplaced = true };
        Assert.False(compressible.IsReplaced()); Assert.True(compressible.IsCompressibleReplaced());
    }
}
