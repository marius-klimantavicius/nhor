using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class PaddingAlignEndChild
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(20f), LengthPercentage.Length(20f), LengthPercentage.Length(20f), LengthPercentage.Length(20f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            AlignItemsValue = AlignItems.FlexEnd,
            JustifyContentValue = AlignContent.FlexEnd,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(200f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(100f, layout_node0.Size.Height);
        Assert.Equal(100f, layout_node0.Location.X);
        Assert.Equal(100f, layout_node0.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(20f), LengthPercentage.Length(20f), LengthPercentage.Length(20f), LengthPercentage.Length(20f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignItemsValue = AlignItems.FlexEnd,
            JustifyContentValue = AlignContent.FlexEnd,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(200f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(140f, layout_node0.Size.Width);
        Assert.Equal(140f, layout_node0.Size.Height);
        Assert.Equal(60f, layout_node0.Location.X);
        Assert.Equal(60f, layout_node0.Location.Y);
    }
}
