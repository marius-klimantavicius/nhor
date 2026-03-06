using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class FlexGrowFlexBasisPercentMinMax
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            FlexShrinkValue = 0f,
            FlexBasisValue = Dimension.FromLength(0f),
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(20f)),
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.Auto()),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            FlexShrinkValue = 0f,
            FlexBasisValue = Dimension.FromPercent(0.5f),
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
            MaxSizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.Auto()),
        });
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(120f), Dimension.Auto()),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(120f, layout_node.Size.Width);
        Assert.Equal(20f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(20f, layout_node1.Size.Width);
        Assert.Equal(20f, layout_node1.Size.Height);
        Assert.Equal(100f, layout_node1.Location.X);
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
            FlexShrinkValue = 0f,
            FlexBasisValue = Dimension.FromLength(0f),
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(20f)),
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.Auto()),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 1f,
            FlexShrinkValue = 0f,
            FlexBasisValue = Dimension.FromPercent(0.5f),
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
            MaxSizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.Auto()),
        });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(120f), Dimension.Auto()),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(120f, layout_node.Size.Width);
        Assert.Equal(20f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(20f, layout_node1.Size.Width);
        Assert.Equal(20f, layout_node1.Size.Height);
        Assert.Equal(100f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
    }
}
