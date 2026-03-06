using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class JustifyContentRowMaxWidthAndMargin
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(100f), LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO),
        });
        var node = taffy.NewWithChildren(new Style
        {
            JustifyContentValue = AlignContent.Center,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.Auto()),
            MaxSizeValue = new Size<Dimension>(Dimension.FromLength(80f), Dimension.Auto()),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(80f, layout_node.Size.Width);
        Assert.Equal(20f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(0f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(90f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(100f), LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO),
        });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            JustifyContentValue = AlignContent.Center,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.Auto()),
            MaxSizeValue = new Size<Dimension>(Dimension.FromLength(80f), Dimension.Auto()),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(80f, layout_node.Size.Width);
        Assert.Equal(20f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(0f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(90f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
    }
}
