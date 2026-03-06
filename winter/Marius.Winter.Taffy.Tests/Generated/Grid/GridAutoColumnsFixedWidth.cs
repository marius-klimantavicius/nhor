using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridAutoColumnsFixedWidth
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
        var node6 = taffy.NewLeaf(new Style());
        var node7 = taffy.NewLeaf(new Style());
        var node8 = taffy.NewLeaf(new Style());
        var node9 = taffy.NewLeaf(new Style());
        var node10 = taffy.NewLeaf(new Style());
        var node11 = taffy.NewLeaf(new Style());
        var node12 = taffy.NewLeaf(new Style());
        var node13 = taffy.NewLeaf(new Style());
        var node14 = taffy.NewLeaf(new Style());
        var node15 = taffy.NewLeaf(new Style());
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.AutoComponent(), GridTemplateComponent.FromLength(40f), GridTemplateComponent.AutoComponent()),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.AutoComponent(), GridTemplateComponent.FromLength(40f), GridTemplateComponent.AutoComponent()),
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(200f)),
        }, new NodeId[] { node0, node1, node2, node3, node4, node5, node6, node7, node8, node9, node10, node11, node12, node13, node14, node15 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(40f, layout_node0.Size.Width);
        Assert.Equal(40f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(60f, layout_node1.Size.Width);
        Assert.Equal(40f, layout_node1.Size.Height);
        Assert.Equal(40f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(40f, layout_node2.Size.Width);
        Assert.Equal(40f, layout_node2.Size.Height);
        Assert.Equal(100f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(60f, layout_node3.Size.Width);
        Assert.Equal(40f, layout_node3.Size.Height);
        Assert.Equal(140f, layout_node3.Location.X);
        Assert.Equal(0f, layout_node3.Location.Y);
        var layout_node4 = taffy.GetLayout(node4);
        Assert.Equal(40f, layout_node4.Size.Width);
        Assert.Equal(60f, layout_node4.Size.Height);
        Assert.Equal(0f, layout_node4.Location.X);
        Assert.Equal(40f, layout_node4.Location.Y);
        var layout_node5 = taffy.GetLayout(node5);
        Assert.Equal(60f, layout_node5.Size.Width);
        Assert.Equal(60f, layout_node5.Size.Height);
        Assert.Equal(40f, layout_node5.Location.X);
        Assert.Equal(40f, layout_node5.Location.Y);
        var layout_node6 = taffy.GetLayout(node6);
        Assert.Equal(40f, layout_node6.Size.Width);
        Assert.Equal(60f, layout_node6.Size.Height);
        Assert.Equal(100f, layout_node6.Location.X);
        Assert.Equal(40f, layout_node6.Location.Y);
        var layout_node7 = taffy.GetLayout(node7);
        Assert.Equal(60f, layout_node7.Size.Width);
        Assert.Equal(60f, layout_node7.Size.Height);
        Assert.Equal(140f, layout_node7.Location.X);
        Assert.Equal(40f, layout_node7.Location.Y);
        var layout_node8 = taffy.GetLayout(node8);
        Assert.Equal(40f, layout_node8.Size.Width);
        Assert.Equal(40f, layout_node8.Size.Height);
        Assert.Equal(0f, layout_node8.Location.X);
        Assert.Equal(100f, layout_node8.Location.Y);
        var layout_node9 = taffy.GetLayout(node9);
        Assert.Equal(60f, layout_node9.Size.Width);
        Assert.Equal(40f, layout_node9.Size.Height);
        Assert.Equal(40f, layout_node9.Location.X);
        Assert.Equal(100f, layout_node9.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(40f, layout_node10.Size.Width);
        Assert.Equal(40f, layout_node10.Size.Height);
        Assert.Equal(100f, layout_node10.Location.X);
        Assert.Equal(100f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(60f, layout_node11.Size.Width);
        Assert.Equal(40f, layout_node11.Size.Height);
        Assert.Equal(140f, layout_node11.Location.X);
        Assert.Equal(100f, layout_node11.Location.Y);
        var layout_node12 = taffy.GetLayout(node12);
        Assert.Equal(40f, layout_node12.Size.Width);
        Assert.Equal(60f, layout_node12.Size.Height);
        Assert.Equal(0f, layout_node12.Location.X);
        Assert.Equal(140f, layout_node12.Location.Y);
        var layout_node13 = taffy.GetLayout(node13);
        Assert.Equal(60f, layout_node13.Size.Width);
        Assert.Equal(60f, layout_node13.Size.Height);
        Assert.Equal(40f, layout_node13.Location.X);
        Assert.Equal(140f, layout_node13.Location.Y);
        var layout_node14 = taffy.GetLayout(node14);
        Assert.Equal(40f, layout_node14.Size.Width);
        Assert.Equal(60f, layout_node14.Size.Height);
        Assert.Equal(100f, layout_node14.Location.X);
        Assert.Equal(140f, layout_node14.Location.Y);
        var layout_node15 = taffy.GetLayout(node15);
        Assert.Equal(60f, layout_node15.Size.Width);
        Assert.Equal(60f, layout_node15.Size.Height);
        Assert.Equal(140f, layout_node15.Location.X);
        Assert.Equal(140f, layout_node15.Location.Y);
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
        var node14 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node15 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.AutoComponent(), GridTemplateComponent.FromLength(40f), GridTemplateComponent.AutoComponent()),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.AutoComponent(), GridTemplateComponent.FromLength(40f), GridTemplateComponent.AutoComponent()),
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(200f)),
        }, new NodeId[] { node0, node1, node2, node3, node4, node5, node6, node7, node8, node9, node10, node11, node12, node13, node14, node15 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(40f, layout_node0.Size.Width);
        Assert.Equal(40f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(60f, layout_node1.Size.Width);
        Assert.Equal(40f, layout_node1.Size.Height);
        Assert.Equal(40f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(40f, layout_node2.Size.Width);
        Assert.Equal(40f, layout_node2.Size.Height);
        Assert.Equal(100f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(60f, layout_node3.Size.Width);
        Assert.Equal(40f, layout_node3.Size.Height);
        Assert.Equal(140f, layout_node3.Location.X);
        Assert.Equal(0f, layout_node3.Location.Y);
        var layout_node4 = taffy.GetLayout(node4);
        Assert.Equal(40f, layout_node4.Size.Width);
        Assert.Equal(60f, layout_node4.Size.Height);
        Assert.Equal(0f, layout_node4.Location.X);
        Assert.Equal(40f, layout_node4.Location.Y);
        var layout_node5 = taffy.GetLayout(node5);
        Assert.Equal(60f, layout_node5.Size.Width);
        Assert.Equal(60f, layout_node5.Size.Height);
        Assert.Equal(40f, layout_node5.Location.X);
        Assert.Equal(40f, layout_node5.Location.Y);
        var layout_node6 = taffy.GetLayout(node6);
        Assert.Equal(40f, layout_node6.Size.Width);
        Assert.Equal(60f, layout_node6.Size.Height);
        Assert.Equal(100f, layout_node6.Location.X);
        Assert.Equal(40f, layout_node6.Location.Y);
        var layout_node7 = taffy.GetLayout(node7);
        Assert.Equal(60f, layout_node7.Size.Width);
        Assert.Equal(60f, layout_node7.Size.Height);
        Assert.Equal(140f, layout_node7.Location.X);
        Assert.Equal(40f, layout_node7.Location.Y);
        var layout_node8 = taffy.GetLayout(node8);
        Assert.Equal(40f, layout_node8.Size.Width);
        Assert.Equal(40f, layout_node8.Size.Height);
        Assert.Equal(0f, layout_node8.Location.X);
        Assert.Equal(100f, layout_node8.Location.Y);
        var layout_node9 = taffy.GetLayout(node9);
        Assert.Equal(60f, layout_node9.Size.Width);
        Assert.Equal(40f, layout_node9.Size.Height);
        Assert.Equal(40f, layout_node9.Location.X);
        Assert.Equal(100f, layout_node9.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(40f, layout_node10.Size.Width);
        Assert.Equal(40f, layout_node10.Size.Height);
        Assert.Equal(100f, layout_node10.Location.X);
        Assert.Equal(100f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(60f, layout_node11.Size.Width);
        Assert.Equal(40f, layout_node11.Size.Height);
        Assert.Equal(140f, layout_node11.Location.X);
        Assert.Equal(100f, layout_node11.Location.Y);
        var layout_node12 = taffy.GetLayout(node12);
        Assert.Equal(40f, layout_node12.Size.Width);
        Assert.Equal(60f, layout_node12.Size.Height);
        Assert.Equal(0f, layout_node12.Location.X);
        Assert.Equal(140f, layout_node12.Location.Y);
        var layout_node13 = taffy.GetLayout(node13);
        Assert.Equal(60f, layout_node13.Size.Width);
        Assert.Equal(60f, layout_node13.Size.Height);
        Assert.Equal(40f, layout_node13.Location.X);
        Assert.Equal(140f, layout_node13.Location.Y);
        var layout_node14 = taffy.GetLayout(node14);
        Assert.Equal(40f, layout_node14.Size.Width);
        Assert.Equal(60f, layout_node14.Size.Height);
        Assert.Equal(100f, layout_node14.Location.X);
        Assert.Equal(140f, layout_node14.Location.Y);
        var layout_node15 = taffy.GetLayout(node15);
        Assert.Equal(60f, layout_node15.Size.Width);
        Assert.Equal(60f, layout_node15.Size.Height);
        Assert.Equal(140f, layout_node15.Location.X);
        Assert.Equal(140f, layout_node15.Location.Y);
    }
}
