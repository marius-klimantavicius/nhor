using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class BevyIssue10343Flex
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node000 = taffy.NewLeaf(new Style());
        var node00 = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(200f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f)),
        }, new NodeId[] { node000 });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
        }, new NodeId[] { node00 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            FlexDirectionValue = FlexDirection.Column,
            JustifyContentValue = AlignContent.SpaceAround,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(210f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(90f, layout_node00.Size.Width);
        Assert.Equal(200f, layout_node00.Size.Height);
        Assert.Equal(5f, layout_node00.Location.X);
        Assert.Equal(5f, layout_node00.Location.Y);
        var layout_node000 = taffy.GetLayout(node000);
        Assert.Equal(0f, layout_node000.Size.Width);
        Assert.Equal(200f, layout_node000.Size.Height);
        Assert.Equal(0f, layout_node000.Location.X);
        Assert.Equal(0f, layout_node000.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node000 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node00 = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(200f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f)),
        }, new NodeId[] { node000 });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
        }, new NodeId[] { node00 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            JustifyContentValue = AlignContent.SpaceAround,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(210f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(90f, layout_node00.Size.Width);
        Assert.Equal(200f, layout_node00.Size.Height);
        Assert.Equal(5f, layout_node00.Location.X);
        Assert.Equal(5f, layout_node00.Location.Y);
        var layout_node000 = taffy.GetLayout(node000);
        Assert.Equal(0f, layout_node000.Size.Width);
        Assert.Equal(200f, layout_node000.Size.Height);
        Assert.Equal(0f, layout_node000.Location.X);
        Assert.Equal(0f, layout_node000.Location.Y);
    }
}
