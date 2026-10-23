using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

public class CorrectionRound4Tests
{
    // Fresh native target outputs, preserving the review's 134 targeted cases and
    // scenes 51/100/274, plus adjacent placement and fractional flex branches.
    private static readonly JsonDocument Corpus = LoadCorpus();
    private static JsonDocument LoadCorpus()
    {
        using var stream = typeof(CorrectionRound4Tests).Assembly.GetManifestResourceStream(
            "Marius.Winter.Taffy.Tests.Native.correction-round4-corpus.json.gz")!;
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        return JsonDocument.Parse(gzip);
    }
    public static IEnumerable<object[]> NativeCases() => Corpus.RootElement.GetProperty("names")
        .EnumerateArray().Select((name, i) => new object[] { i, name.GetString()! });

    private static AvailableSpace Space(JsonElement v) => v.ValueKind == JsonValueKind.String
        ? v.GetString() == "MinContent" ? AvailableSpace.MinContent : AvailableSpace.MaxContent
        : AvailableSpace.Definite(v.GetProperty("Definite").GetSingle());

    [Theory]
    [MemberData(nameof(NativeCases))]
    public void NativePlacementAndFractionalFlexBits(int index, string name)
    {
        var scene = Corpus.RootElement.GetProperty("scenes")[index];
        var expected = Corpus.RootElement.GetProperty("expected")[index];
        var tree = new TaffyTree<TestNodeContext>();
        if (!scene.GetProperty("round").GetBoolean()) tree.DisableRounding();
        var ids = new List<NodeId>();
        foreach (var n in scene.GetProperty("nodes").EnumerateArray())
        {
            var style = TaffyStyleJson.Deserialize<Style>(n.GetProperty("style"));
            var children = n.GetProperty("children").EnumerateArray().Select(v => ids[v.GetInt32()]).ToArray();
            ids.Add(children.Length > 0 ? tree.NewWithChildren(style, children)
                : tree.NewLeafWithContext(style, TestNodeContext.AhemText(n.GetProperty("text").GetString()!,
                    n.GetProperty("vertical").GetBoolean() ? WritingMode.Vertical : WritingMode.Horizontal)));
        }
        var space = scene.GetProperty("space");
        for (int phase = 0; phase < expected.GetArrayLength(); phase++)
        {
            if (phase == 2)
            {
                var style = tree.GetStyle(ids[0]); style.SizeValue.Width = Dimension.Percent(.375f);
                tree.SetStyle(ids[0], style);
            }
            if (phase == 3)
            {
                var style = tree.GetStyle(ids[7]); style.PositionValue = Position.Relative;
                tree.SetStyle(ids[7], style);
            }
            if (phase == 4) tree.Remove(ids[0]);
            tree.ComputeLayoutWithMeasure(ids[^1], new(Space(space.GetProperty("width")), Space(space.GetProperty("height"))), TestHelpers.MeasureFunction);
            int cursor = 0;
            for (int i = phase == 4 ? 1 : 0; i < ids.Count; i++)
            {
                var native = expected[phase][cursor++]; var id = ids[i]; var l = tree.GetLayout(id);
                var actual = new[] { l.Location.X, l.Location.Y, l.Size.Width, l.Size.Height, l.ScrollWidth(), l.ScrollHeight(),
                    l.Border.Left, l.Border.Right, l.Border.Top, l.Border.Bottom, l.Padding.Left, l.Padding.Right, l.Padding.Top, l.Padding.Bottom,
                    l.Margin.Left, l.Margin.Right, l.Margin.Top, l.Margin.Bottom, l.ScrollableOverflowRect.Left, l.ScrollableOverflowRect.Right,
                    l.ScrollableOverflowRect.Top, l.ScrollableOverflowRect.Bottom, l.ScrollbarSize.Width, l.ScrollbarSize.Height };
                Assert.Equal(i, native.GetProperty("index").GetInt32());
                var bits = native.GetProperty("layout").EnumerateArray().Select(v => v.GetUInt32()).ToArray();
                for (int scalar = 0; scalar < actual.Length; scalar++)
                    Assert.True(bits[scalar] == BitConverter.SingleToUInt32Bits(actual[scalar]),
                        $"{name}, phase {phase}, node {i}, scalar {scalar}: expected {bits[scalar]}, actual {BitConverter.SingleToUInt32Bits(actual[scalar])}");
                Assert.Equal(native.GetProperty("hoisted").EnumerateArray().Select(v => v.GetInt32()), tree.HoistedChildren(id).ToArray().Select(n => ids.IndexOf(n)));
                var detail = native.GetProperty("detail"); var grid = tree.GetDetailedGridInfo(id);
                if (detail.ValueKind == JsonValueKind.Null) Assert.Null(grid);
                else Assert.Equal(detail.EnumerateArray().Select(v => v.GetString()), new[] { grid!.GridTemplateRows(), grid.GridTemplateColumns() });
            }
            Assert.Equal(cursor, expected[phase].GetArrayLength());
        }
    }

    [Theory]
    [InlineData(WritingMode.Horizontal)]
    [InlineData(WritingMode.Vertical)]
    public void KnownInlineSizeFloorsMeasurementBeforeWrapping(WritingMode mode)
    {
        var known = mode == WritingMode.Horizontal ? new Size<float?>(1.25f, null) : new(null, 1.25f);
        var context = TestNodeContext.AhemText("XXX\u200bX\u200bXX", mode);
        foreach (var available in new[] { AvailableSpace.MinContent, AvailableSpace.MaxContent, AvailableSpace.Definite(.5f) })
        {
            var measured = TestHelpers.MeasureFunction(known, new(available, available), new(0), context, new Style());
            Assert.Equal(mode == WritingMode.Horizontal ? new Size<float>(1.25f, 20) : new(20, 1.25f), measured);
            var input = new LayoutInput { KnownDimensions = known, AvailableSpace = new(available, available),
                RunMode = RunMode.ComputeSize, SizingMode = SizingMode.ContentSize, Axis = RequestedAxis.Both };
            var output = LeafCompute.ComputeLeafLayout(input, new Style(), (_, _) => 0,
                (k, a) => TestHelpers.MeasureFunction(k, a, new(0), context, new Style()));
            Assert.Equal(measured, output.Size);
        }
    }

    [Theory]
    [InlineData(WritingMode.Horizontal)]
    [InlineData(WritingMode.Vertical)]
    public void AhemLengthsUseNativeUtf8Bytes(WritingMode mode)
    {
        // Native str::len counts bytes: 4 + 2 + 4, wrapping to 3 lines at width 40.
        var context = TestNodeContext.AhemText("éé\u200bé\u200b😀", mode);
        var size = TestHelpers.MeasureFunction(new(null, null), new(AvailableSpace.MinContent, AvailableSpace.MinContent), new(0), context, new Style());
        Assert.Equal(mode == WritingMode.Horizontal ? new Size<float>(40, 30) : new(30, 40), size);
    }

    [Theory]
    [InlineData(float.PositiveInfinity, 10f)]
    [InlineData(float.MaxValue, 10f)]
    [InlineData(float.NaN, 20f)]
    public void AhemKnownInlineExtremesUseNativeFloorAndSaturatingCast(float inline, float block)
    {
        var context = TestNodeContext.AhemText("XXX\u200bX\u200bXX", WritingMode.Horizontal);
        var size = TestHelpers.MeasureFunction(new(inline, null), TestHelpers.MaxContentSize, new(0), context, new Style());
        Assert.Equal(block, size.Height);
    }

    private sealed class Container : IBlockContainerStyle
    {
        public bool IsBlock() => true;
        public Size<Dimension> Size() => new(Dimension.Length(100), Dimension.Length(100));
        public AlignItems? JustifyItems() => AlignItems.End;
        public Direction Direction() => Taffy.Direction.Rtl;
    }
    private sealed class Item(Position position) : IBlockItemStyle, IOofItemStyle
    {
        public Position Position() => position;
        public Size<Dimension> Size() => new(Dimension.Length(10), Dimension.Length(10));
        public Direction Direction() => Taffy.Direction.Ltr;
        public AlignItems? AlignSelf() => AlignItems.End;
    }
    private sealed class BlockTree(bool customContainer, bool customItem, Position position) : ILayoutBlockContainer, ILayoutContainingBlock
    {
        private readonly IBlockContainerStyle _container = customContainer ? new Container() : new Style { Display = Display.Block,
            SizeValue = new(Dimension.Length(100), Dimension.Length(100)), JustifyItemsValue = AlignItems.End, DirectionValue = Direction.Rtl };
        private readonly IBlockItemStyle _item = customItem ? new Item(position) : new Style { PositionValue = position,
            SizeValue = new(Dimension.Length(10), Dimension.Length(10)), AlignSelfValue = AlignItems.End };
        public Layout ChildLayout;
        public ICoreStyle GetCoreContainerStyle(NodeId n) => n.Value == 1 ? _container : _item;
        public IBlockContainerStyle GetBlockContainerStyle(NodeId n) => _container;
        public IBlockItemStyle GetBlockChildStyle(NodeId n) => _item;
        public IOofItemStyle GetOofItemStyle(NodeId n) => (IOofItemStyle)_item;
        public IEnumerable<NodeId> ChildIds(NodeId n) => n.Value == 1 ? [new(2)] : [];
        public int ChildCount(NodeId n) => n.Value == 1 ? 1 : 0;
        public NodeId GetChildId(NodeId n, int i) => new(2);
        public void SetUnroundedLayout(NodeId n, Layout l) { if (n.Value == 2) ChildLayout = l; }
        public LayoutOutput ComputeChildLayout(NodeId n, LayoutInput i) => LeafCompute.ComputeLeafLayout(i, _item, (_, _) => 0, (k, a) => new(k.Width ?? 10, k.Height ?? 10));
        public void ClearHoistedChildren(NodeId n) { }
        public void AddHoistedChildren(NodeId n, ReadOnlySpan<NodeId> children) { }
        public DetailedGridInfo? GetDetailedGridInfo(NodeId n) => null;
    }
    public static IEnumerable<object[]> CustomBlockCases()
    {
        foreach (bool container in new[] { false, true })
        foreach (bool item in new[] { false, true })
        foreach (var position in new[] { Position.Absolute, Position.Fixed }) yield return [container, item, position];
    }
    [Theory]
    [MemberData(nameof(CustomBlockCases))]
    public void BlockAndOutOfFlowConsumeIndependentStyleTraits(bool customContainer, bool customItem, Position position)
    {
        var tree = new BlockTree(customContainer, customItem, position);
        var output = BlockAlgorithm.ComputeBlockLayout(tree, new(1), new LayoutInput { RunMode = RunMode.PerformLayout,
            SizingMode = SizingMode.InherentSize, Axis = RequestedAxis.Both, KnownDimensions = new(100, 100),
            AvailableSpace = new(AvailableSpace.Definite(100), AvailableSpace.Definite(100)) }, null);
        Assert.Equal(new Size<float>(100, 100), output.Size);
        var candidate = Assert.Single(output.OofCandidates.AsSpan().ToArray());
        Assert.Equal(position, candidate.Position);
        Assert.Equal(new Line<float>(0, 100), candidate.StaticPosition.X.Area);
        Assert.Equal(AxisStaticEdge.Start, candidate.StaticPosition.X.Align.Keyword); // RTL justify-items: end
        Assert.Equal(AxisStaticEdge.End, candidate.StaticPosition.Y.Align.Keyword);
        var hoisted = new List<NodeId>(); var unclaimed = new OofCandidates();
        OutOfFlow.PerformOofLayout(tree, new(1), output.OofCandidates.AsSpan(), new(100, 100), default, Direction.Rtl,
            ContainingBlockClaims.All, new(Overflow.Visible, Overflow.Visible), hoisted, ref unclaimed);
        Assert.True(unclaimed.IsEmpty);
        Assert.Equal(new Size<float>(10, 10), tree.ChildLayout.Size);
        Assert.Equal(new Point<float>(0, -10), tree.ChildLayout.Location);
    }
}
