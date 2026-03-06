using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class AbsoluteChildWithCrossMargin
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            AlignContentValue = AlignContent.Stretch,
            SizeValue = new Size<Dimension>(Dimension.FromLength(28f), Dimension.FromLength(27f)),
        });
        var node1 = taffy.NewLeafWithContext(new Style
        {
            PositionValue = Position.Absolute,
            AlignContentValue = AlignContent.Stretch,
            FlexGrowValue = 0f,
            FlexShrinkValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromLength(15f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(4f), LengthPercentageAuto.ZERO),
        }, TestNodeContext.AhemText("", WritingMode.Horizontal));
        var node2 = taffy.NewLeaf(new Style
        {
            AlignContentValue = AlignContent.Stretch,
            SizeValue = new Size<Dimension>(Dimension.FromLength(25f), Dimension.FromLength(27f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            JustifyContentValue = AlignContent.SpaceBetween,
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(311f), Dimension.FromLength(0f)),
            MaxSizeValue = new Size<Dimension>(Dimension.FromLength(311f), Dimension.FromLength(36893500000000000000f)),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(311f, layout_node.Size.Width);
        Assert.Equal(27f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(28f, layout_node0.Size.Width);
        Assert.Equal(27f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(311f, layout_node1.Size.Width);
        Assert.Equal(15f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(4f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(25f, layout_node2.Size.Width);
        Assert.Equal(27f, layout_node2.Size.Height);
        Assert.Equal(286f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignContentValue = AlignContent.Stretch,
            SizeValue = new Size<Dimension>(Dimension.FromLength(28f), Dimension.FromLength(27f)),
        });
        var node1 = taffy.NewLeafWithContext(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            PositionValue = Position.Absolute,
            AlignContentValue = AlignContent.Stretch,
            FlexGrowValue = 0f,
            FlexShrinkValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromLength(15f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(4f), LengthPercentageAuto.ZERO),
        }, TestNodeContext.AhemText("", WritingMode.Horizontal));
        var node2 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignContentValue = AlignContent.Stretch,
            SizeValue = new Size<Dimension>(Dimension.FromLength(25f), Dimension.FromLength(27f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            JustifyContentValue = AlignContent.SpaceBetween,
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(311f), Dimension.FromLength(0f)),
            MaxSizeValue = new Size<Dimension>(Dimension.FromLength(311f), Dimension.FromLength(36893500000000000000f)),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(311f, layout_node.Size.Width);
        Assert.Equal(27f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(28f, layout_node0.Size.Width);
        Assert.Equal(27f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(311f, layout_node1.Size.Width);
        Assert.Equal(15f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(4f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(25f, layout_node2.Size.Width);
        Assert.Equal(27f, layout_node2.Size.Height);
        Assert.Equal(286f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
    }
}
