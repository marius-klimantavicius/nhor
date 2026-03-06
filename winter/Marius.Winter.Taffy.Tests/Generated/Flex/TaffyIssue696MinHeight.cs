using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class TaffyIssue696MinHeight
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            Display = Display.Flex,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(200f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            FlexDirectionValue = FlexDirection.Column,
            FlexBasisValue = Dimension.FromLength(0f),
            MinSizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(100f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(20f), LengthPercentage.Length(20f), LengthPercentage.Length(20f), LengthPercentage.Length(20f)),
        }, new NodeId[] { node00 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.Auto()),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(200f, layout_node0.Size.Width);
        Assert.Equal(100f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(160f, layout_node00.Size.Width);
        Assert.Equal(200f, layout_node00.Size.Height);
        Assert.Equal(20f, layout_node00.Location.X);
        Assert.Equal(20f, layout_node00.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(200f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            FlexBasisValue = Dimension.FromLength(0f),
            MinSizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(100f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(20f), LengthPercentage.Length(20f), LengthPercentage.Length(20f), LengthPercentage.Length(20f)),
        }, new NodeId[] { node00 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.Auto()),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(140f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(200f, layout_node0.Size.Width);
        Assert.Equal(140f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(160f, layout_node00.Size.Width);
        Assert.Equal(200f, layout_node00.Size.Height);
        Assert.Equal(20f, layout_node00.Location.X);
        Assert.Equal(20f, layout_node00.Location.Y);
    }
}
