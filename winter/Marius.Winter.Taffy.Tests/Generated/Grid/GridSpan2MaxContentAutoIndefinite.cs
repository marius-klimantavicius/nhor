using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridSpan2MaxContentAutoIndefinite
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            GridRow = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.Auto),
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.Auto),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            GridRow = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.Auto),
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)2), GridPlacement.Auto),
        });
        var node2 = taffy.NewLeafWithContext(new Style
        {
            GridRow = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.Auto),
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.FromSpanCount((ushort)2)),
        }, TestNodeContext.AhemText("HHHH\u200bHHHH", WritingMode.Horizontal));
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(40f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.MaxContentComponent(), GridTemplateComponent.AutoComponent()),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(80f, layout_node.Size.Width);
        Assert.Equal(40f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(60f, layout_node0.Size.Width);
        Assert.Equal(40f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(20f, layout_node1.Size.Width);
        Assert.Equal(40f, layout_node1.Size.Height);
        Assert.Equal(60f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(80f, layout_node2.Size.Width);
        Assert.Equal(40f, layout_node2.Size.Height);
        Assert.Equal(0f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            GridRow = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.Auto),
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.Auto),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            GridRow = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.Auto),
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)2), GridPlacement.Auto),
        });
        var node2 = taffy.NewLeafWithContext(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            GridRow = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.Auto),
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.FromSpanCount((ushort)2)),
        }, TestNodeContext.AhemText("HHHH\u200bHHHH", WritingMode.Horizontal));
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(40f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.MaxContentComponent(), GridTemplateComponent.AutoComponent()),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(80f, layout_node.Size.Width);
        Assert.Equal(40f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(60f, layout_node0.Size.Width);
        Assert.Equal(40f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(20f, layout_node1.Size.Width);
        Assert.Equal(40f, layout_node1.Size.Height);
        Assert.Equal(60f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(80f, layout_node2.Size.Width);
        Assert.Equal(40f, layout_node2.Size.Height);
        Assert.Equal(0f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
    }
}
