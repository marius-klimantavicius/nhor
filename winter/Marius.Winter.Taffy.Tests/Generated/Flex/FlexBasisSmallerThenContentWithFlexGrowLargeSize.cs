using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class FlexBasisSmallerThenContentWithFlexGrowLargeSize
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(70f), Dimension.FromLength(100f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            FlexGrowValue = 1f,
            FlexBasisValue = Dimension.FromLength(0f),
        }, new NodeId[] { node00 });
        var node10 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(100f)),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            FlexGrowValue = 1f,
            FlexBasisValue = Dimension.FromLength(0f),
        }, new NodeId[] { node10 });
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.Auto()),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(70f, layout_node0.Size.Width);
        Assert.Equal(100f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(70f, layout_node00.Size.Width);
        Assert.Equal(100f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(30f, layout_node1.Size.Width);
        Assert.Equal(100f, layout_node1.Size.Height);
        Assert.Equal(70f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(20f, layout_node10.Size.Width);
        Assert.Equal(100f, layout_node10.Size.Height);
        Assert.Equal(0f, layout_node10.Location.X);
        Assert.Equal(0f, layout_node10.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(70f), Dimension.FromLength(100f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            FlexGrowValue = 1f,
            FlexBasisValue = Dimension.FromLength(0f),
        }, new NodeId[] { node00 });
        var node10 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(100f)),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            FlexGrowValue = 1f,
            FlexBasisValue = Dimension.FromLength(0f),
        }, new NodeId[] { node10 });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.Auto()),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(70f, layout_node0.Size.Width);
        Assert.Equal(100f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(70f, layout_node00.Size.Width);
        Assert.Equal(100f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(30f, layout_node1.Size.Width);
        Assert.Equal(100f, layout_node1.Size.Height);
        Assert.Equal(70f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(20f, layout_node10.Size.Width);
        Assert.Equal(100f, layout_node10.Size.Height);
        Assert.Equal(0f, layout_node10.Location.X);
        Assert.Equal(0f, layout_node10.Location.Y);
    }
}
