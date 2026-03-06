using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridPercentTracksDefiniteOverflow
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style());
        var node1 = taffy.NewLeaf(new Style());
        var node2 = taffy.NewLeaf(new Style());
        var node3 = taffy.NewLeaf(new Style());
        var node4 = taffy.NewLeaf(new Style());
        var node5 = taffy.NewLeaf(new Style());
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromPercent(0.5f), GridTemplateComponent.FromPercent(0.8f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromPercent(0.4f), GridTemplateComponent.FromPercent(0.4f), GridTemplateComponent.FromPercent(0.4f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(120f), Dimension.FromLength(60f)),
        }, new NodeId[] { node0, node1, node2, node3, node4, node5 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(120f, layout_node.Size.Width);
        Assert.Equal(60f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(48f, layout_node0.Size.Width);
        Assert.Equal(30f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(48f, layout_node1.Size.Width);
        Assert.Equal(30f, layout_node1.Size.Height);
        Assert.Equal(48f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(48f, layout_node2.Size.Width);
        Assert.Equal(30f, layout_node2.Size.Height);
        Assert.Equal(96f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(48f, layout_node3.Size.Width);
        Assert.Equal(48f, layout_node3.Size.Height);
        Assert.Equal(0f, layout_node3.Location.X);
        Assert.Equal(30f, layout_node3.Location.Y);
        var layout_node4 = taffy.GetLayout(node4);
        Assert.Equal(48f, layout_node4.Size.Width);
        Assert.Equal(48f, layout_node4.Size.Height);
        Assert.Equal(48f, layout_node4.Location.X);
        Assert.Equal(30f, layout_node4.Location.Y);
        var layout_node5 = taffy.GetLayout(node5);
        Assert.Equal(48f, layout_node5.Size.Width);
        Assert.Equal(48f, layout_node5.Size.Height);
        Assert.Equal(96f, layout_node5.Location.X);
        Assert.Equal(30f, layout_node5.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
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
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromPercent(0.5f), GridTemplateComponent.FromPercent(0.8f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromPercent(0.4f), GridTemplateComponent.FromPercent(0.4f), GridTemplateComponent.FromPercent(0.4f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(120f), Dimension.FromLength(60f)),
        }, new NodeId[] { node0, node1, node2, node3, node4, node5 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(120f, layout_node.Size.Width);
        Assert.Equal(60f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(48f, layout_node0.Size.Width);
        Assert.Equal(30f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(48f, layout_node1.Size.Width);
        Assert.Equal(30f, layout_node1.Size.Height);
        Assert.Equal(48f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(48f, layout_node2.Size.Width);
        Assert.Equal(30f, layout_node2.Size.Height);
        Assert.Equal(96f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(48f, layout_node3.Size.Width);
        Assert.Equal(48f, layout_node3.Size.Height);
        Assert.Equal(0f, layout_node3.Location.X);
        Assert.Equal(30f, layout_node3.Location.Y);
        var layout_node4 = taffy.GetLayout(node4);
        Assert.Equal(48f, layout_node4.Size.Width);
        Assert.Equal(48f, layout_node4.Size.Height);
        Assert.Equal(48f, layout_node4.Location.X);
        Assert.Equal(30f, layout_node4.Location.Y);
        var layout_node5 = taffy.GetLayout(node5);
        Assert.Equal(48f, layout_node5.Size.Width);
        Assert.Equal(48f, layout_node5.Size.Height);
        Assert.Equal(96f, layout_node5.Location.X);
        Assert.Equal(30f, layout_node5.Location.Y);
    }
}
