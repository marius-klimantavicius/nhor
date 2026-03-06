using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridTaffyIssue624
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            GridRow = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.FromSpanCount((ushort)2)),
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.Auto),
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(50f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            GridRow = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.FromSpanCount((ushort)2)),
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)2), GridPlacement.FromSpanCount((ushort)2)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(30f)),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            GridRow = new Line<GridPlacement>(GridPlacement.FromLine((short)3), GridPlacement.FromSpanCount((ushort)1)),
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.FromSpanCount((ushort)2)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(120f), Dimension.FromLength(20f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            JustifyItemsValue = AlignItems.Start,
            JustifyContentValue = AlignContent.Start,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.AutoComponent(), GridTemplateComponent.AutoComponent(), GridTemplateComponent.AutoComponent(), GridTemplateComponent.FromFr(1f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.AutoComponent(), GridTemplateComponent.AutoComponent(), GridTemplateComponent.FromFr(1f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(320f), Dimension.FromLength(640f)),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(320f, layout_node.Size.Width);
        Assert.Equal(640f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(50f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(40f, layout_node1.Size.Width);
        Assert.Equal(30f, layout_node1.Size.Height);
        Assert.Equal(100f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(120f, layout_node2.Size.Width);
        Assert.Equal(20f, layout_node2.Size.Height);
        Assert.Equal(0f, layout_node2.Location.X);
        Assert.Equal(50f, layout_node2.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            GridRow = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.FromSpanCount((ushort)2)),
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.Auto),
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(50f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            GridRow = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.FromSpanCount((ushort)2)),
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)2), GridPlacement.FromSpanCount((ushort)2)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(30f)),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            GridRow = new Line<GridPlacement>(GridPlacement.FromLine((short)3), GridPlacement.FromSpanCount((ushort)1)),
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.FromSpanCount((ushort)2)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(120f), Dimension.FromLength(20f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            JustifyItemsValue = AlignItems.Start,
            JustifyContentValue = AlignContent.Start,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.AutoComponent(), GridTemplateComponent.AutoComponent(), GridTemplateComponent.AutoComponent(), GridTemplateComponent.FromFr(1f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.AutoComponent(), GridTemplateComponent.AutoComponent(), GridTemplateComponent.FromFr(1f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(320f), Dimension.FromLength(640f)),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(320f, layout_node.Size.Width);
        Assert.Equal(640f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(50f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(40f, layout_node1.Size.Width);
        Assert.Equal(30f, layout_node1.Size.Height);
        Assert.Equal(100f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(120f, layout_node2.Size.Width);
        Assert.Equal(20f, layout_node2.Size.Height);
        Assert.Equal(0f, layout_node2.Location.X);
        Assert.Equal(50f, layout_node2.Location.Y);
    }
}
