using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class FlexGrowLessThanFactorOne
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 0.2f,
            FlexShrinkValue = 0f,
            FlexBasisValue = Dimension.FromLength(40f),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 0.2f,
            FlexShrinkValue = 0f,
        });
        var node2 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 0.4f,
            FlexShrinkValue = 0f,
        });
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(500f), Dimension.FromLength(200f)),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(500f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(132f, layout_node0.Size.Width);
        Assert.Equal(200f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(92f, layout_node1.Size.Width);
        Assert.Equal(200f, layout_node1.Size.Height);
        Assert.Equal(132f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(184f, layout_node2.Size.Width);
        Assert.Equal(200f, layout_node2.Size.Height);
        Assert.Equal(224f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 0.2f,
            FlexShrinkValue = 0f,
            FlexBasisValue = Dimension.FromLength(40f),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 0.2f,
            FlexShrinkValue = 0f,
        });
        var node2 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 0.4f,
            FlexShrinkValue = 0f,
        });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(500f), Dimension.FromLength(200f)),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(500f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(132f, layout_node0.Size.Width);
        Assert.Equal(200f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(92f, layout_node1.Size.Width);
        Assert.Equal(200f, layout_node1.Size.Height);
        Assert.Equal(132f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(184f, layout_node2.Size.Width);
        Assert.Equal(200f, layout_node2.Size.Height);
        Assert.Equal(224f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
    }
}
