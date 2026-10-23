using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

public class PositioningContractTests
{
    private static readonly Size<AvailableSpace> Viewport = new(AvailableSpace.Definite(200), AvailableSpace.Definite(150));
    private static Style Box(Position position, float width = 20, float height = 10) => new()
    {
        PositionValue = position,
        SizeValue = new(Dimension.Length(width), Dimension.Length(height)),
        Inset = new(LengthPercentageAuto.Length(3.25f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Length(4.5f), LengthPercentageAuto.AUTO),
    };

    [Theory]
    [InlineData(Display.Block)]
    [InlineData(Display.Flex)]
    [InlineData(Display.Grid)]
    public void FixedDescendantsHoistThroughPositionedAncestorsAndCachedRuns(Display display)
    {
        var tree = new TaffyTree<object>();
        var absolute = tree.NewLeaf(Box(Position.Absolute));
        var fixedNode = tree.NewLeaf(Box(Position.Fixed));
        var parentStyle = Box(Position.Relative, 80, 60); parentStyle.Display = display;
        var parent = tree.NewWithChildren(parentStyle, new[] { absolute, fixedNode });
        var root = tree.NewWithChildren(new Style { SizeValue = new(Dimension.Length(200), Dimension.Length(150)) }, new[] { parent });
        for (int pass = 0; pass < 4; pass++)
        {
            tree.ComputeLayout(root, Viewport);
            Assert.Equal(new[] { absolute }, tree.HoistedChildren(parent).ToArray());
            Assert.Equal(new[] { fixedNode }, tree.HoistedChildren(root).ToArray());
            Assert.Equal(new Point<float>(3, 5), tree.GetLayout(fixedNode).Location);
            Assert.Equal(new Point<float>(4, 4), tree.GetLayout(absolute).Location);
            Assert.Equal(new Point<float>(3.25f, 4.5f), tree.GetUnroundedLayout(absolute).Location);
        }
        parentStyle.ContainingBlockValue = ContainingBlockClaims.All;
        tree.SetStyle(parent, parentStyle);
        tree.ComputeLayout(root, Viewport);
        Assert.Equal(new[] { absolute, fixedNode }, tree.HoistedChildren(parent).ToArray());
        Assert.Empty(tree.HoistedChildren(root).ToArray());
    }

    [Theory]
    [InlineData(Position.Static, 0f, 0f)]
    [InlineData(Position.Sticky, 0f, 0f)]
    [InlineData(Position.Relative, 3f, 5f)]
    public void OnlyRelativeInFlowBoxesApplyInsets(Position position, float x, float y)
    {
        Assert.Equal(position != Position.Static, position.IsPositioned());
        Assert.False(position.IsOutOfFlow());
        var tree = new TaffyTree<object>();
        var child = tree.NewLeaf(Box(position));
        var root = tree.NewWithChildren(new Style(), new[] { child });
        tree.ComputeLayout(root, Viewport);
        Assert.Equal(new Point<float>(x, y), tree.GetLayout(child).Location);
    }

    [Fact]
    public void HiddenOutOfFlowChildrenReceiveRoundedHiddenLayouts()
    {
        var tree = new TaffyTree<object>(); var style = Box(Position.Absolute);
        var child = tree.NewLeaf(style); var root = tree.NewWithChildren(new Style(), new[] { child });
        tree.ComputeLayout(root, Viewport); Assert.Equal(20f, tree.GetLayout(child).Size.Width);
        style.Display = Display.None; tree.SetStyle(child, style); tree.ComputeLayout(root, Viewport);
        Assert.Equal(default(Size<float>), tree.GetLayout(child).Size);
        Assert.Equal(default(Point<float>), tree.GetLayout(child).Location);
        Assert.Empty(tree.HoistedChildren(root).ToArray());
    }

    private sealed class EmptyFlexTree : ILayoutFlexboxContainer
    {
        public Style Style = new() { PaddingValue = new(LengthPercentage.Length(2), LengthPercentage.Length(3), LengthPercentage.Length(4), LengthPercentage.Length(5)) };
        public IEnumerable<NodeId> ChildIds(NodeId node) => Array.Empty<NodeId>();
        public int ChildCount(NodeId node) => 0;
        public NodeId GetChildId(NodeId node, int index) => throw new InvalidOperationException();
        public ICoreStyle GetCoreContainerStyle(NodeId node) => Style;
        public IFlexboxContainerStyle GetFlexboxContainerStyle(NodeId node) => Style;
        public IFlexboxItemStyle GetFlexboxChildStyle(NodeId node) => throw new InvalidOperationException();
        public void SetUnroundedLayout(NodeId node, Layout layout) => throw new InvalidOperationException();
        public LayoutOutput ComputeChildLayout(NodeId node, LayoutInput input) => throw new InvalidOperationException();
    }

    [Fact]
    public void EmptyFlexContainerComputesInsetsAndReturnsAnEmptyPositioningArea()
    {
        var tree = new EmptyFlexTree();
        var output = FlexboxAlgorithm.ComputeFlexboxLayout(tree, default, new LayoutInput
        {
            RunMode = RunMode.PerformLayout, SizingMode = SizingMode.InherentSize, Axis = RequestedAxis.Both,
            AvailableSpace = new(AvailableSpace.MaxContent, AvailableSpace.MaxContent),
        });
        Assert.Equal(new Size<float>(5, 9), output.Size);
        Assert.Equal(new OofPositioningArea(default, default), output.OofPositioningArea);
        Assert.True(output.OofCandidates.IsEmpty);
        Assert.Equal(Baselines.None, output.Baselines);
    }
}
