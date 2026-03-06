using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class IntrinsicSizingMainSizeMinSize
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            Display = Display.Flex,
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            PositionValue = Position.Absolute,
            AlignItemsValue = AlignItems.Center,
            JustifyContentValue = AlignContent.Center,
            MaxSizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(0f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Length(0f), LengthPercentageAuto.AUTO),
        }, new NodeId[] { node00 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            SizeValue = new Size<Dimension>(Dimension.FromLength(300f), Dimension.FromLength(200f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(300f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(70f, layout_node0.Size.Width);
        Assert.Equal(70f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(50f, layout_node00.Size.Width);
        Assert.Equal(50f, layout_node00.Size.Height);
        Assert.Equal(10f, layout_node00.Location.X);
        Assert.Equal(10f, layout_node00.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            PositionValue = Position.Absolute,
            AlignItemsValue = AlignItems.Center,
            JustifyContentValue = AlignContent.Center,
            MaxSizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(0f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Length(0f), LengthPercentageAuto.AUTO),
        }, new NodeId[] { node00 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(300f), Dimension.FromLength(200f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(300f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(90f, layout_node0.Size.Width);
        Assert.Equal(90f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(70f, layout_node00.Size.Width);
        Assert.Equal(70f, layout_node00.Size.Height);
        Assert.Equal(10f, layout_node00.Location.X);
        Assert.Equal(10f, layout_node00.Location.Y);
    }
}
