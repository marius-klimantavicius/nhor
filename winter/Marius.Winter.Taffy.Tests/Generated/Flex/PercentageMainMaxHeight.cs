using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class PercentageMainMaxHeight
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            FlexBasisValue = Dimension.FromLength(15f),
        });
        var node01 = taffy.NewLeaf(new Style
        {
            FlexBasisValue = Dimension.FromLength(48f),
            MaxSizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromPercent(0.33f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            AlignItemsValue = AlignItems.FlexStart,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(151f)),
        }, new NodeId[] { node00, node01 });
        var node = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromLength(71f), Dimension.Auto()),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(71f, layout_node.Size.Width);
        Assert.Equal(151f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(71f, layout_node0.Size.Width);
        Assert.Equal(151f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(0f, layout_node00.Size.Width);
        Assert.Equal(15f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(0f, layout_node01.Size.Width);
        Assert.Equal(48f, layout_node01.Size.Height);
        Assert.Equal(0f, layout_node01.Location.X);
        Assert.Equal(15f, layout_node01.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexBasisValue = Dimension.FromLength(15f),
        });
        var node01 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexBasisValue = Dimension.FromLength(48f),
            MaxSizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromPercent(0.33f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            AlignItemsValue = AlignItems.FlexStart,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(151f)),
        }, new NodeId[] { node00, node01 });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromLength(71f), Dimension.Auto()),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(71f, layout_node.Size.Width);
        Assert.Equal(151f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(71f, layout_node0.Size.Width);
        Assert.Equal(151f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(0f, layout_node00.Size.Width);
        Assert.Equal(15f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(0f, layout_node01.Size.Width);
        Assert.Equal(48f, layout_node01.Size.Height);
        Assert.Equal(0f, layout_node01.Location.X);
        Assert.Equal(15f, layout_node01.Location.Y);
    }
}
