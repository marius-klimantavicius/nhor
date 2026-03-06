using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class FlexGrow0MinSize
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeafWithContext(new Style
        {
            FlexGrowValue = 0f,
            FlexShrinkValue = 0f,
            FlexBasisValue = Dimension.FromPercent(0f),
        }, TestNodeContext.AhemText("one", WritingMode.Horizontal));
        var node1 = taffy.NewLeafWithContext(new Style
        {
            FlexGrowValue = 0f,
            FlexShrinkValue = 0f,
            FlexBasisValue = Dimension.FromPercent(0f),
        }, TestNodeContext.AhemText("two", WritingMode.Horizontal));
        var node2 = taffy.NewLeafWithContext(new Style
        {
            FlexGrowValue = 0f,
            FlexShrinkValue = 0f,
            FlexBasisValue = Dimension.FromPercent(0f),
        }, TestNodeContext.AhemText("three", WritingMode.Horizontal));
        var node3 = taffy.NewLeafWithContext(new Style
        {
            FlexGrowValue = 0f,
            FlexShrinkValue = 0f,
            FlexBasisValue = Dimension.FromPercent(0f),
        }, TestNodeContext.AhemText("four", WritingMode.Horizontal));
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            SizeValue = new Size<Dimension>(Dimension.FromLength(400f), Dimension.FromLength(100f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(1f), LengthPercentage.Length(1f), LengthPercentage.Length(1f), LengthPercentage.Length(1f)),
        }, new NodeId[] { node0, node1, node2, node3 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(400f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(30f, layout_node0.Size.Width);
        Assert.Equal(98f, layout_node0.Size.Height);
        Assert.Equal(1f, layout_node0.Location.X);
        Assert.Equal(1f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(30f, layout_node1.Size.Width);
        Assert.Equal(98f, layout_node1.Size.Height);
        Assert.Equal(31f, layout_node1.Location.X);
        Assert.Equal(1f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(50f, layout_node2.Size.Width);
        Assert.Equal(98f, layout_node2.Size.Height);
        Assert.Equal(61f, layout_node2.Location.X);
        Assert.Equal(1f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(40f, layout_node3.Size.Width);
        Assert.Equal(98f, layout_node3.Size.Height);
        Assert.Equal(111f, layout_node3.Location.X);
        Assert.Equal(1f, layout_node3.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeafWithContext(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 0f,
            FlexShrinkValue = 0f,
            FlexBasisValue = Dimension.FromPercent(0f),
        }, TestNodeContext.AhemText("one", WritingMode.Horizontal));
        var node1 = taffy.NewLeafWithContext(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 0f,
            FlexShrinkValue = 0f,
            FlexBasisValue = Dimension.FromPercent(0f),
        }, TestNodeContext.AhemText("two", WritingMode.Horizontal));
        var node2 = taffy.NewLeafWithContext(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 0f,
            FlexShrinkValue = 0f,
            FlexBasisValue = Dimension.FromPercent(0f),
        }, TestNodeContext.AhemText("three", WritingMode.Horizontal));
        var node3 = taffy.NewLeafWithContext(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 0f,
            FlexShrinkValue = 0f,
            FlexBasisValue = Dimension.FromPercent(0f),
        }, TestNodeContext.AhemText("four", WritingMode.Horizontal));
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(400f), Dimension.FromLength(100f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(1f), LengthPercentage.Length(1f), LengthPercentage.Length(1f), LengthPercentage.Length(1f)),
        }, new NodeId[] { node0, node1, node2, node3 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(402f, layout_node.Size.Width);
        Assert.Equal(102f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(30f, layout_node0.Size.Width);
        Assert.Equal(100f, layout_node0.Size.Height);
        Assert.Equal(1f, layout_node0.Location.X);
        Assert.Equal(1f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(30f, layout_node1.Size.Width);
        Assert.Equal(100f, layout_node1.Size.Height);
        Assert.Equal(31f, layout_node1.Location.X);
        Assert.Equal(1f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(50f, layout_node2.Size.Width);
        Assert.Equal(100f, layout_node2.Size.Height);
        Assert.Equal(61f, layout_node2.Location.X);
        Assert.Equal(1f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(40f, layout_node3.Size.Width);
        Assert.Equal(100f, layout_node3.Size.Height);
        Assert.Equal(111f, layout_node3.Location.X);
        Assert.Equal(1f, layout_node3.Location.Y);
    }
}
