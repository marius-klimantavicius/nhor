using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class FlexGrowWithinConstrainedMaxColumn
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 1f,
            FlexBasisValue = Dimension.FromLength(100f),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(50f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.Auto()),
            MaxSizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(100f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(67f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(100f, layout_node1.Size.Width);
        Assert.Equal(33f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(67f, layout_node1.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 1f,
            FlexBasisValue = Dimension.FromLength(100f),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(50f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.Auto()),
            MaxSizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(100f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(67f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(100f, layout_node1.Size.Width);
        Assert.Equal(33f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(67f, layout_node1.Location.Y);
    }
}
