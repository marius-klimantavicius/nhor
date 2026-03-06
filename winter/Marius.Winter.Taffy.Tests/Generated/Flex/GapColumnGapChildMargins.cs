using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class GapColumnGapChildMargins
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            FlexShrinkValue = 1f,
            FlexBasisValue = Dimension.FromPercent(0f),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(2f), LengthPercentageAuto.Length(2f), LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            FlexShrinkValue = 1f,
            FlexBasisValue = Dimension.FromPercent(0f),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            FlexShrinkValue = 1f,
            FlexBasisValue = Dimension.FromPercent(0f),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(15f), LengthPercentageAuto.Length(15f), LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO),
        });
        var node = taffy.NewWithChildren(new Style
        {
            GapValue = new Size<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.ZERO),
            SizeValue = new Size<Dimension>(Dimension.FromLength(80f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(80f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(2f, layout_node0.Size.Width);
        Assert.Equal(100f, layout_node0.Size.Height);
        Assert.Equal(2f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(2f, layout_node1.Size.Width);
        Assert.Equal(100f, layout_node1.Size.Height);
        Assert.Equal(26f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(2f, layout_node2.Size.Width);
        Assert.Equal(100f, layout_node2.Size.Height);
        Assert.Equal(63f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 1f,
            FlexShrinkValue = 1f,
            FlexBasisValue = Dimension.FromPercent(0f),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(2f), LengthPercentageAuto.Length(2f), LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 1f,
            FlexShrinkValue = 1f,
            FlexBasisValue = Dimension.FromPercent(0f),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 1f,
            FlexShrinkValue = 1f,
            FlexBasisValue = Dimension.FromPercent(0f),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(15f), LengthPercentageAuto.Length(15f), LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO),
        });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            GapValue = new Size<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.ZERO),
            SizeValue = new Size<Dimension>(Dimension.FromLength(80f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(80f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(2f, layout_node0.Size.Width);
        Assert.Equal(100f, layout_node0.Size.Height);
        Assert.Equal(2f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(2f, layout_node1.Size.Width);
        Assert.Equal(100f, layout_node1.Size.Height);
        Assert.Equal(26f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(2f, layout_node2.Size.Width);
        Assert.Equal(100f, layout_node2.Size.Height);
        Assert.Equal(63f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
    }
}
