using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

// Direct translations of tests/hand_written/fit_content_calc.rs, using a managed opaque-handle resolver.
public class CalcIntegrationTests
{
    internal sealed class CalcTree : ILayoutContainingBlock, ICacheTree, ILayoutBlockContainer, ILayoutGridContainer
    {
        public IGridContainerStyle? ContainerStyleOverride;
        public IGridItemStyle? ItemStyleOverride;
        private sealed class Node(Style style, NodeId[] children, float min, float max, float height)
        {
            public Style Style = style; public NodeId[] Children = children; public float Min = min, Max = max, Height = height;
            public Cache Cache = Cache.New(); public Layout Layout; public DetailedGridInfo? Detailed;
        }
        private readonly List<Node> _nodes = new();
        private readonly List<(float Px, float Percent)> _calc = new();
        public NodeId Add(Style style, NodeId[]? children = null, float min = 0, float max = 0, float height = 0)
        { var id = new NodeId((ulong)_nodes.Count); _nodes.Add(new(style, children ?? [], min, max, height)); return id; }
        private Node Get(NodeId id) => _nodes[(int)id.Value];
        public LengthPercentage Calc(float px, float percent) { _calc.Add((px, percent)); return LengthPercentage.Calc(new IntPtr(_calc.Count * 8)); }
        public float ResolveCalcValue(IntPtr handle, float basis) { var (px, percent) = _calc[handle.ToInt32() / 8 - 1]; return percent * basis + px; }
        public Layout Layout(NodeId node) => Get(node).Layout;
        public void Compute(NodeId root, AvailableSpace width) => ComputeLayout.ComputeRootLayout(this, root, new(width, AvailableSpace.MaxContent));
        public IEnumerable<NodeId> ChildIds(NodeId node) => Get(node).Children;
        public int ChildCount(NodeId node) => Get(node).Children.Length;
        public NodeId GetChildId(NodeId node, int index) => Get(node).Children[index];
        public ICoreStyle GetCoreContainerStyle(NodeId node) => Get(node).Style;
        public IBlockContainerStyle GetBlockContainerStyle(NodeId node) => Get(node).Style;
        public IBlockItemStyle GetBlockChildStyle(NodeId node) => Get(node).Style;
        public IGridContainerStyle GetGridContainerStyle(NodeId node) => ContainerStyleOverride ?? Get(node).Style;
        public IGridItemStyle GetGridChildStyle(NodeId node) => ItemStyleOverride ?? Get(node).Style;
        public IOofItemStyle GetOofItemStyle(NodeId node) => Get(node).Style;
        public void SetUnroundedLayout(NodeId node, Layout layout) => Get(node).Layout = layout;
        public void SetDetailedGridInfo(NodeId node, DetailedGridInfo info) => Get(node).Detailed = info;
        public DetailedGridInfo? GetDetailedGridInfo(NodeId node) => Get(node).Detailed;
        public void ClearHoistedChildren(NodeId node) { }
        public void AddHoistedChildren(NodeId node, ReadOnlySpan<NodeId> children) => Assert.True(children.IsEmpty);
        public LayoutOutput? CacheGet(NodeId node, in LayoutInput input) => Get(node).Cache.Get(input);
        public void CacheStore(NodeId node, in LayoutInput input, LayoutOutput output) => Get(node).Cache.Store(input, output);
        public void CacheClear(NodeId node) => Get(node).Cache.Clear();
        public LayoutOutput ComputeChildLayout(NodeId id, LayoutInput input) => ComputeLayout.ComputeCachedLayout(this, id, input, (tree, nodeId, inputs) =>
        {
            var node = Get(nodeId);
            if (node.Children.Length != 0) return node.Style.Display == Display.Grid ? GridAlgorithm.ComputeGridLayout(tree, nodeId, inputs) : BlockAlgorithm.ComputeBlockLayout(tree, nodeId, inputs, null);
            return LeafCompute.ComputeLeafLayout(inputs, node.Style, ResolveCalcValue, (known, space) => new(known.Width ?? (space.Width == AvailableSpace.MinContent ? node.Min : space.Width == AvailableSpace.MaxContent ? node.Max : Math.Clamp(space.Width.Unwrap(), node.Min, node.Max)), known.Height ?? node.Height));
        });
    }
    [Fact]
    public void GridTrackFitContentCalcLimitsGrowth()
    {
        var tree = new CalcTree(); var child = tree.Add(new Style { MinSizeValue = new(Dimension.AUTO, Dimension.ZERO) }, height: 400);
        var root = tree.Add(new Style { Display = Display.Grid, SizeValue = new(Dimension.Length(100), Dimension.Length(300)), GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromSingle(TrackSizingFunctionExtensions.FitContent(tree.Calc(5, .1f)))) }, [child]);
        tree.Compute(root, AvailableSpace.MaxContent); Assert.Equal(35f, tree.Layout(child).Size.Height);
    }
    [Fact]
    public void BlockItemFitContentCalcResolvesAgainstParent()
    {
        var tree = new CalcTree(); var child = tree.Add(new Style { SizeValue = new(Dimension.FitContentCalc(tree.Calc(70, .1f).IntoRaw().CalcValue), Dimension.AUTO) }, min: 50, max: 400, height: 10);
        var root = tree.Add(new Style { Display = Display.Block, SizeValue = new(Dimension.Length(300), Dimension.AUTO) }, [child]);
        tree.Compute(root, AvailableSpace.MaxContent); Assert.Equal(100f, tree.Layout(child).Size.Width);
    }
    private static (float Container, float Item) SingleColumn(bool calc, float px, float percent, Dimension width, AvailableSpace available)
    {
        var tree = new CalcTree(); var child = tree.Add(new Style(), min: 50, max: 150, height: 10);
        var limit = calc ? tree.Calc(px, percent) : px != 0 ? LengthPercentage.Length(px) : LengthPercentage.Percent(percent);
        var root = tree.Add(new Style { Display = Display.Grid, SizeValue = new(width, Dimension.Length(40)), GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromSingle(TrackSizingFunctionExtensions.FitContent(limit))) }, [child]);
        tree.Compute(root, available); return (tree.Layout(root).Size.Width, tree.Layout(child).Size.Width);
    }
    [Fact]
    public void FitContentCalcMatchesEquivalentLengthAndPercentage()
    {
        var actual = SingleColumn(true, 10, .2f, Dimension.Length(200), AvailableSpace.MaxContent);
        Assert.Equal(SingleColumn(false, 50, 0, Dimension.Length(200), AvailableSpace.MaxContent), actual); Assert.Equal(50f, actual.Item);
        foreach (var available in new[] { AvailableSpace.Definite(200), AvailableSpace.MaxContent, AvailableSpace.MinContent })
            Assert.Equal(SingleColumn(false, 0, .5f, Dimension.AUTO, available), SingleColumn(true, 0, .5f, Dimension.AUTO, available));
        Assert.Equal((150f, 75f), SingleColumn(true, 0, .5f, Dimension.AUTO, AvailableSpace.MaxContent));
    }
}
