using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class AlignItemsCenterWithMaxHeightWithPaddingBorder
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(10f)),
        });
        var node01 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(150f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            AlignItemsValue = AlignItems.Center,
            AlignContentValue = AlignContent.FlexStart,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.Auto()),
            MaxSizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(100f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
        }, new NodeId[] { node00, node01 });
        var node10 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(10f)),
        });
        var node11 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(150f)),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            FlexWrapValue = FlexWrap.Wrap,
            AlignItemsValue = AlignItems.Center,
            AlignContentValue = AlignContent.FlexStart,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.Auto()),
            MaxSizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(100f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
        }, new NodeId[] { node10, node11 });
        var node = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(100f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(10f, layout_node00.Size.Width);
        Assert.Equal(10f, layout_node00.Size.Height);
        Assert.Equal(20f, layout_node00.Location.X);
        Assert.Equal(45f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(10f, layout_node01.Size.Width);
        Assert.Equal(150f, layout_node01.Size.Height);
        Assert.Equal(30f, layout_node01.Location.X);
        Assert.Equal(-25f, layout_node01.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(100f, layout_node1.Size.Width);
        Assert.Equal(100f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(100f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(10f, layout_node10.Size.Width);
        Assert.Equal(10f, layout_node10.Size.Height);
        Assert.Equal(20f, layout_node10.Location.X);
        Assert.Equal(90f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(10f, layout_node11.Size.Width);
        Assert.Equal(150f, layout_node11.Size.Height);
        Assert.Equal(30f, layout_node11.Location.X);
        Assert.Equal(20f, layout_node11.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(10f)),
        });
        var node01 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(150f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            AlignItemsValue = AlignItems.Center,
            AlignContentValue = AlignContent.FlexStart,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.Auto()),
            MaxSizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(100f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
        }, new NodeId[] { node00, node01 });
        var node10 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(10f)),
        });
        var node11 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(150f)),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            FlexWrapValue = FlexWrap.Wrap,
            AlignItemsValue = AlignItems.Center,
            AlignContentValue = AlignContent.FlexStart,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.Auto()),
            MaxSizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(100f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
        }, new NodeId[] { node10, node11 });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(140f, layout_node.Size.Width);
        Assert.Equal(280f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(140f, layout_node0.Size.Width);
        Assert.Equal(140f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(10f, layout_node00.Size.Width);
        Assert.Equal(10f, layout_node00.Size.Height);
        Assert.Equal(20f, layout_node00.Location.X);
        Assert.Equal(65f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(10f, layout_node01.Size.Width);
        Assert.Equal(150f, layout_node01.Size.Height);
        Assert.Equal(30f, layout_node01.Location.X);
        Assert.Equal(-5f, layout_node01.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(140f, layout_node1.Size.Width);
        Assert.Equal(140f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(140f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(10f, layout_node10.Size.Width);
        Assert.Equal(10f, layout_node10.Size.Height);
        Assert.Equal(20f, layout_node10.Location.X);
        Assert.Equal(90f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(10f, layout_node11.Size.Width);
        Assert.Equal(150f, layout_node11.Size.Height);
        Assert.Equal(30f, layout_node11.Location.X);
        Assert.Equal(20f, layout_node11.Location.Y);
    }
}
