using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridSpan13MostNonFlexWithMinmaxIndefiniteHidden
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeafWithContext(new Style
        {
            OverflowValue = new Point<Overflow>(Overflow.Hidden, Overflow.Hidden),
            ScrollbarWidthValue = 15f,
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.FromSpanCount((ushort)13)),
        }, TestNodeContext.AhemText("HHHHHHHHHHHHHHHH\u200bHHHHHHHHHHHHHHHH", WritingMode.Horizontal));
        var node1 = taffy.NewLeaf(new Style());
        var node2 = taffy.NewLeaf(new Style());
        var node3 = taffy.NewLeaf(new Style());
        var node4 = taffy.NewLeaf(new Style());
        var node5 = taffy.NewLeaf(new Style());
        var node6 = taffy.NewLeaf(new Style());
        var node7 = taffy.NewLeaf(new Style());
        var node8 = taffy.NewLeaf(new Style());
        var node9 = taffy.NewLeaf(new Style());
        var node10 = taffy.NewLeaf(new Style());
        var node11 = taffy.NewLeaf(new Style());
        var node12 = taffy.NewLeaf(new Style());
        var node13 = taffy.NewLeaf(new Style());
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.MinContentComponent(), GridTemplateComponent.MaxContentComponent(), GridTemplateComponent.FitContentComponent(LengthPercentage.Length(20f)), GridTemplateComponent.AutoComponent(), GridTemplateComponent.FromLength(10f), GridTemplateComponent.FromPercent(0.2f), GridTemplateComponent.FromSingle(new MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>(MinTrackSizingFunction.Length(2f), MaxTrackSizingFunction.AUTO)), GridTemplateComponent.FromSingle(new MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>(MinTrackSizingFunction.Length(2f), MaxTrackSizingFunction.Length(4f))), GridTemplateComponent.FromSingle(new MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>(MinTrackSizingFunction.Length(2f), MaxTrackSizingFunction.MIN_CONTENT)), GridTemplateComponent.FromSingle(new MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>(MinTrackSizingFunction.Length(2f), MaxTrackSizingFunction.MAX_CONTENT)), GridTemplateComponent.FromSingle(new MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>(MinTrackSizingFunction.MIN_CONTENT, MaxTrackSizingFunction.MAX_CONTENT)), GridTemplateComponent.FromSingle(new MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>(MinTrackSizingFunction.MIN_CONTENT, MaxTrackSizingFunction.AUTO)), GridTemplateComponent.FromSingle(new MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>(MinTrackSizingFunction.MAX_CONTENT, MaxTrackSizingFunction.AUTO))),
        }, new NodeId[] { node0, node1, node2, node3, node4, node5, node6, node7, node8, node9, node10, node11, node12, node13 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(322f, layout_node.Size.Width);
        Assert.Equal(80f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(322f, layout_node0.Size.Width);
        Assert.Equal(40f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        Assert.Equal(0f, layout_node0.ScrollWidth());
        Assert.Equal(0f, layout_node0.ScrollHeight());
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(16f, layout_node1.Size.Width);
        Assert.Equal(40f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(40f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(95f, layout_node2.Size.Width);
        Assert.Equal(40f, layout_node2.Size.Height);
        Assert.Equal(16f, layout_node2.Location.X);
        Assert.Equal(40f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(0f, layout_node3.Size.Width);
        Assert.Equal(40f, layout_node3.Size.Height);
        Assert.Equal(111f, layout_node3.Location.X);
        Assert.Equal(40f, layout_node3.Location.Y);
        var layout_node4 = taffy.GetLayout(node4);
        Assert.Equal(0f, layout_node4.Size.Width);
        Assert.Equal(40f, layout_node4.Size.Height);
        Assert.Equal(111f, layout_node4.Location.X);
        Assert.Equal(40f, layout_node4.Location.Y);
        var layout_node5 = taffy.GetLayout(node5);
        Assert.Equal(10f, layout_node5.Size.Width);
        Assert.Equal(40f, layout_node5.Size.Height);
        Assert.Equal(111f, layout_node5.Location.X);
        Assert.Equal(40f, layout_node5.Location.Y);
        var layout_node6 = taffy.GetLayout(node6);
        Assert.Equal(64f, layout_node6.Size.Width);
        Assert.Equal(40f, layout_node6.Size.Height);
        Assert.Equal(121f, layout_node6.Location.X);
        Assert.Equal(40f, layout_node6.Location.Y);
        var layout_node7 = taffy.GetLayout(node7);
        Assert.Equal(2f, layout_node7.Size.Width);
        Assert.Equal(40f, layout_node7.Size.Height);
        Assert.Equal(185f, layout_node7.Location.X);
        Assert.Equal(40f, layout_node7.Location.Y);
        var layout_node8 = taffy.GetLayout(node8);
        Assert.Equal(4f, layout_node8.Size.Width);
        Assert.Equal(40f, layout_node8.Size.Height);
        Assert.Equal(187f, layout_node8.Location.X);
        Assert.Equal(40f, layout_node8.Location.Y);
        var layout_node9 = taffy.GetLayout(node9);
        Assert.Equal(2f, layout_node9.Size.Width);
        Assert.Equal(40f, layout_node9.Size.Height);
        Assert.Equal(191f, layout_node9.Location.X);
        Assert.Equal(40f, layout_node9.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(2f, layout_node10.Size.Width);
        Assert.Equal(40f, layout_node10.Size.Height);
        Assert.Equal(193f, layout_node10.Location.X);
        Assert.Equal(40f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(16f, layout_node11.Size.Width);
        Assert.Equal(40f, layout_node11.Size.Height);
        Assert.Equal(195f, layout_node11.Location.X);
        Assert.Equal(40f, layout_node11.Location.Y);
        var layout_node12 = taffy.GetLayout(node12);
        Assert.Equal(15f, layout_node12.Size.Width);
        Assert.Equal(40f, layout_node12.Size.Height);
        Assert.Equal(211f, layout_node12.Location.X);
        Assert.Equal(40f, layout_node12.Location.Y);
        var layout_node13 = taffy.GetLayout(node13);
        Assert.Equal(96f, layout_node13.Size.Width);
        Assert.Equal(40f, layout_node13.Size.Height);
        Assert.Equal(226f, layout_node13.Location.X);
        Assert.Equal(40f, layout_node13.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeafWithContext(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            OverflowValue = new Point<Overflow>(Overflow.Hidden, Overflow.Hidden),
            ScrollbarWidthValue = 15f,
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.FromSpanCount((ushort)13)),
        }, TestNodeContext.AhemText("HHHHHHHHHHHHHHHH\u200bHHHHHHHHHHHHHHHH", WritingMode.Horizontal));
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node2 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node3 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node4 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node5 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node6 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node7 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node8 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node9 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node10 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node11 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node12 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node13 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.MinContentComponent(), GridTemplateComponent.MaxContentComponent(), GridTemplateComponent.FitContentComponent(LengthPercentage.Length(20f)), GridTemplateComponent.AutoComponent(), GridTemplateComponent.FromLength(10f), GridTemplateComponent.FromPercent(0.2f), GridTemplateComponent.FromSingle(new MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>(MinTrackSizingFunction.Length(2f), MaxTrackSizingFunction.AUTO)), GridTemplateComponent.FromSingle(new MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>(MinTrackSizingFunction.Length(2f), MaxTrackSizingFunction.Length(4f))), GridTemplateComponent.FromSingle(new MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>(MinTrackSizingFunction.Length(2f), MaxTrackSizingFunction.MIN_CONTENT)), GridTemplateComponent.FromSingle(new MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>(MinTrackSizingFunction.Length(2f), MaxTrackSizingFunction.MAX_CONTENT)), GridTemplateComponent.FromSingle(new MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>(MinTrackSizingFunction.MIN_CONTENT, MaxTrackSizingFunction.MAX_CONTENT)), GridTemplateComponent.FromSingle(new MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>(MinTrackSizingFunction.MIN_CONTENT, MaxTrackSizingFunction.AUTO)), GridTemplateComponent.FromSingle(new MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>(MinTrackSizingFunction.MAX_CONTENT, MaxTrackSizingFunction.AUTO))),
        }, new NodeId[] { node0, node1, node2, node3, node4, node5, node6, node7, node8, node9, node10, node11, node12, node13 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(322f, layout_node.Size.Width);
        Assert.Equal(80f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(322f, layout_node0.Size.Width);
        Assert.Equal(40f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        Assert.Equal(0f, layout_node0.ScrollWidth());
        Assert.Equal(0f, layout_node0.ScrollHeight());
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(16f, layout_node1.Size.Width);
        Assert.Equal(40f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(40f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(95f, layout_node2.Size.Width);
        Assert.Equal(40f, layout_node2.Size.Height);
        Assert.Equal(16f, layout_node2.Location.X);
        Assert.Equal(40f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(0f, layout_node3.Size.Width);
        Assert.Equal(40f, layout_node3.Size.Height);
        Assert.Equal(111f, layout_node3.Location.X);
        Assert.Equal(40f, layout_node3.Location.Y);
        var layout_node4 = taffy.GetLayout(node4);
        Assert.Equal(0f, layout_node4.Size.Width);
        Assert.Equal(40f, layout_node4.Size.Height);
        Assert.Equal(111f, layout_node4.Location.X);
        Assert.Equal(40f, layout_node4.Location.Y);
        var layout_node5 = taffy.GetLayout(node5);
        Assert.Equal(10f, layout_node5.Size.Width);
        Assert.Equal(40f, layout_node5.Size.Height);
        Assert.Equal(111f, layout_node5.Location.X);
        Assert.Equal(40f, layout_node5.Location.Y);
        var layout_node6 = taffy.GetLayout(node6);
        Assert.Equal(64f, layout_node6.Size.Width);
        Assert.Equal(40f, layout_node6.Size.Height);
        Assert.Equal(121f, layout_node6.Location.X);
        Assert.Equal(40f, layout_node6.Location.Y);
        var layout_node7 = taffy.GetLayout(node7);
        Assert.Equal(2f, layout_node7.Size.Width);
        Assert.Equal(40f, layout_node7.Size.Height);
        Assert.Equal(185f, layout_node7.Location.X);
        Assert.Equal(40f, layout_node7.Location.Y);
        var layout_node8 = taffy.GetLayout(node8);
        Assert.Equal(4f, layout_node8.Size.Width);
        Assert.Equal(40f, layout_node8.Size.Height);
        Assert.Equal(187f, layout_node8.Location.X);
        Assert.Equal(40f, layout_node8.Location.Y);
        var layout_node9 = taffy.GetLayout(node9);
        Assert.Equal(2f, layout_node9.Size.Width);
        Assert.Equal(40f, layout_node9.Size.Height);
        Assert.Equal(191f, layout_node9.Location.X);
        Assert.Equal(40f, layout_node9.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(2f, layout_node10.Size.Width);
        Assert.Equal(40f, layout_node10.Size.Height);
        Assert.Equal(193f, layout_node10.Location.X);
        Assert.Equal(40f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(16f, layout_node11.Size.Width);
        Assert.Equal(40f, layout_node11.Size.Height);
        Assert.Equal(195f, layout_node11.Location.X);
        Assert.Equal(40f, layout_node11.Location.Y);
        var layout_node12 = taffy.GetLayout(node12);
        Assert.Equal(15f, layout_node12.Size.Width);
        Assert.Equal(40f, layout_node12.Size.Height);
        Assert.Equal(211f, layout_node12.Location.X);
        Assert.Equal(40f, layout_node12.Location.Y);
        var layout_node13 = taffy.GetLayout(node13);
        Assert.Equal(96f, layout_node13.Size.Width);
        Assert.Equal(40f, layout_node13.Size.Height);
        Assert.Equal(226f, layout_node13.Location.X);
        Assert.Equal(40f, layout_node13.Location.Y);
    }
}
