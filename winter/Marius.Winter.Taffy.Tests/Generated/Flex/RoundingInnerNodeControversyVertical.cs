using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class RoundingInnerNodeControversyVertical
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.Auto()),
        });
        var node10 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.Auto()),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.Auto()),
        }, new NodeId[] { node10 });
        var node2 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.Auto()),
        });
        var node = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(320f)),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(10f, layout_node.Size.Width);
        Assert.Equal(320f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(10f, layout_node0.Size.Width);
        Assert.Equal(107f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(10f, layout_node1.Size.Width);
        Assert.Equal(106f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(107f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(10f, layout_node10.Size.Width);
        Assert.Equal(106f, layout_node10.Size.Height);
        Assert.Equal(0f, layout_node10.Location.X);
        Assert.Equal(0f, layout_node10.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(10f, layout_node2.Size.Width);
        Assert.Equal(107f, layout_node2.Size.Height);
        Assert.Equal(0f, layout_node2.Location.X);
        Assert.Equal(213f, layout_node2.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.Auto()),
        });
        var node10 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.Auto()),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.Auto()),
        }, new NodeId[] { node10 });
        var node2 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.Auto()),
        });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(320f)),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(10f, layout_node.Size.Width);
        Assert.Equal(320f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(10f, layout_node0.Size.Width);
        Assert.Equal(107f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(10f, layout_node1.Size.Width);
        Assert.Equal(106f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(107f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(10f, layout_node10.Size.Width);
        Assert.Equal(106f, layout_node10.Size.Height);
        Assert.Equal(0f, layout_node10.Location.X);
        Assert.Equal(0f, layout_node10.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(10f, layout_node2.Size.Width);
        Assert.Equal(107f, layout_node2.Size.Height);
        Assert.Equal(0f, layout_node2.Location.X);
        Assert.Equal(213f, layout_node2.Location.Y);
    }
}
