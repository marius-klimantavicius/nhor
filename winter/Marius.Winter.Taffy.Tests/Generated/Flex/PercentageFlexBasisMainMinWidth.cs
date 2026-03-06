using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class PercentageFlexBasisMainMinWidth
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            FlexBasisValue = Dimension.FromPercent(0.15f),
            MinSizeValue = new Size<Dimension>(Dimension.FromPercent(0.6f), Dimension.Auto()),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 4f,
            FlexBasisValue = Dimension.FromPercent(0.1f),
            MinSizeValue = new Size<Dimension>(Dimension.FromPercent(0.2f), Dimension.Auto()),
        });
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(400f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(400f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(120f, layout_node0.Size.Width);
        Assert.Equal(400f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(80f, layout_node1.Size.Width);
        Assert.Equal(400f, layout_node1.Size.Height);
        Assert.Equal(120f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 1f,
            FlexBasisValue = Dimension.FromPercent(0.15f),
            MinSizeValue = new Size<Dimension>(Dimension.FromPercent(0.6f), Dimension.Auto()),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 4f,
            FlexBasisValue = Dimension.FromPercent(0.1f),
            MinSizeValue = new Size<Dimension>(Dimension.FromPercent(0.2f), Dimension.Auto()),
        });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(400f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(400f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(120f, layout_node0.Size.Width);
        Assert.Equal(400f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(80f, layout_node1.Size.Width);
        Assert.Equal(400f, layout_node1.Size.Height);
        Assert.Equal(120f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
    }
}
