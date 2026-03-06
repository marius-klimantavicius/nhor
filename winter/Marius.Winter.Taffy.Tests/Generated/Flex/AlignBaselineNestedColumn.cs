using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class AlignBaselineNestedColumn
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(60f)),
        });
        var node100 = taffy.NewLeaf(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(30f)),
        });
        var node101 = taffy.NewLeaf(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(40f)),
        });
        var node10 = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(80f)),
        }, new NodeId[] { node100, node101 });
        var node1 = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
        }, new NodeId[] { node10 });
        var node = taffy.NewWithChildren(new Style
        {
            AlignItemsValue = AlignItems.Baseline,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(50f, layout_node0.Size.Width);
        Assert.Equal(60f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(50f, layout_node1.Size.Width);
        Assert.Equal(80f, layout_node1.Size.Height);
        Assert.Equal(50f, layout_node1.Location.X);
        Assert.Equal(30f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(50f, layout_node10.Size.Width);
        Assert.Equal(80f, layout_node10.Size.Height);
        Assert.Equal(0f, layout_node10.Location.X);
        Assert.Equal(0f, layout_node10.Location.Y);
        var layout_node100 = taffy.GetLayout(node100);
        Assert.Equal(50f, layout_node100.Size.Width);
        Assert.Equal(30f, layout_node100.Size.Height);
        Assert.Equal(0f, layout_node100.Location.X);
        Assert.Equal(0f, layout_node100.Location.Y);
        var layout_node101 = taffy.GetLayout(node101);
        Assert.Equal(50f, layout_node101.Size.Width);
        Assert.Equal(40f, layout_node101.Size.Height);
        Assert.Equal(0f, layout_node101.Location.X);
        Assert.Equal(30f, layout_node101.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(60f)),
        });
        var node100 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(30f)),
        });
        var node101 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(40f)),
        });
        var node10 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(80f)),
        }, new NodeId[] { node100, node101 });
        var node1 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
        }, new NodeId[] { node10 });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignItemsValue = AlignItems.Baseline,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(50f, layout_node0.Size.Width);
        Assert.Equal(60f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(50f, layout_node1.Size.Width);
        Assert.Equal(80f, layout_node1.Size.Height);
        Assert.Equal(50f, layout_node1.Location.X);
        Assert.Equal(30f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(50f, layout_node10.Size.Width);
        Assert.Equal(80f, layout_node10.Size.Height);
        Assert.Equal(0f, layout_node10.Location.X);
        Assert.Equal(0f, layout_node10.Location.Y);
        var layout_node100 = taffy.GetLayout(node100);
        Assert.Equal(50f, layout_node100.Size.Width);
        Assert.Equal(30f, layout_node100.Size.Height);
        Assert.Equal(0f, layout_node100.Location.X);
        Assert.Equal(0f, layout_node100.Location.Y);
        var layout_node101 = taffy.GetLayout(node101);
        Assert.Equal(50f, layout_node101.Size.Width);
        Assert.Equal(40f, layout_node101.Size.Height);
        Assert.Equal(0f, layout_node101.Location.X);
        Assert.Equal(30f, layout_node101.Location.Y);
    }
}
