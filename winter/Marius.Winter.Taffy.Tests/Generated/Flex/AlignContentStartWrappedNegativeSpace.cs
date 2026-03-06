using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class AlignContentStartWrappedNegativeSpace
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.8f), Dimension.FromLength(20f)),
        });
        var node01 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.8f), Dimension.FromLength(20f)),
        });
        var node02 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.8f), Dimension.FromLength(20f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            FlexWrapValue = FlexWrap.Wrap,
            AlignContentValue = AlignContent.Start,
            JustifyContentValue = AlignContent.Center,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
        }, new NodeId[] { node00, node01, node02 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromLength(320f), Dimension.FromLength(320f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(60f), LengthPercentage.Length(60f), LengthPercentage.Length(60f), LengthPercentage.Length(60f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(320f, layout_node.Size.Width);
        Assert.Equal(320f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(200f, layout_node0.Size.Width);
        Assert.Equal(10f, layout_node0.Size.Height);
        Assert.Equal(60f, layout_node0.Location.X);
        Assert.Equal(60f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(160f, layout_node00.Size.Width);
        Assert.Equal(20f, layout_node00.Size.Height);
        Assert.Equal(20f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(160f, layout_node01.Size.Width);
        Assert.Equal(20f, layout_node01.Size.Height);
        Assert.Equal(20f, layout_node01.Location.X);
        Assert.Equal(20f, layout_node01.Location.Y);
        var layout_node02 = taffy.GetLayout(node02);
        Assert.Equal(160f, layout_node02.Size.Width);
        Assert.Equal(20f, layout_node02.Size.Height);
        Assert.Equal(20f, layout_node02.Location.X);
        Assert.Equal(40f, layout_node02.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.8f), Dimension.FromLength(20f)),
        });
        var node01 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.8f), Dimension.FromLength(20f)),
        });
        var node02 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.8f), Dimension.FromLength(20f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            FlexWrapValue = FlexWrap.Wrap,
            AlignContentValue = AlignContent.Start,
            JustifyContentValue = AlignContent.Center,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
        }, new NodeId[] { node00, node01, node02 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromLength(320f), Dimension.FromLength(320f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(60f), LengthPercentage.Length(60f), LengthPercentage.Length(60f), LengthPercentage.Length(60f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(440f, layout_node.Size.Width);
        Assert.Equal(440f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(320f, layout_node0.Size.Width);
        Assert.Equal(10f, layout_node0.Size.Height);
        Assert.Equal(60f, layout_node0.Location.X);
        Assert.Equal(60f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(256f, layout_node00.Size.Width);
        Assert.Equal(20f, layout_node00.Size.Height);
        Assert.Equal(32f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(256f, layout_node01.Size.Width);
        Assert.Equal(20f, layout_node01.Size.Height);
        Assert.Equal(32f, layout_node01.Location.X);
        Assert.Equal(20f, layout_node01.Location.Y);
        var layout_node02 = taffy.GetLayout(node02);
        Assert.Equal(256f, layout_node02.Size.Width);
        Assert.Equal(20f, layout_node02.Size.Height);
        Assert.Equal(32f, layout_node02.Location.X);
        Assert.Equal(40f, layout_node02.Location.Y);
    }
}
