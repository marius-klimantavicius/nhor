using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class AbsoluteLayoutAlignItemsAndJustifyContentFlexEnd
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            PositionValue = Position.Absolute,
            SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.FromLength(40f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            AlignItemsValue = AlignItems.FlexEnd,
            JustifyContentValue = AlignContent.FlexEnd,
            SizeValue = new Size<Dimension>(Dimension.FromLength(110f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(110f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(60f, layout_node0.Size.Width);
        Assert.Equal(40f, layout_node0.Size.Height);
        Assert.Equal(50f, layout_node0.Location.X);
        Assert.Equal(60f, layout_node0.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            PositionValue = Position.Absolute,
            SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.FromLength(40f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignItemsValue = AlignItems.FlexEnd,
            JustifyContentValue = AlignContent.FlexEnd,
            SizeValue = new Size<Dimension>(Dimension.FromLength(110f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(110f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(60f, layout_node0.Size.Width);
        Assert.Equal(40f, layout_node0.Size.Height);
        Assert.Equal(50f, layout_node0.Location.X);
        Assert.Equal(60f, layout_node0.Location.Y);
    }
}
