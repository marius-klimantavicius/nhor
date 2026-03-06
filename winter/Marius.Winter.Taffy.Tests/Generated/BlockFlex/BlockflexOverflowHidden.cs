using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.BlockFlex;

public class BlockflexOverflowHidden
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeafWithContext(new Style
        {
            Display = Display.Block,
            OverflowValue = new Point<Overflow>(Overflow.Hidden, Overflow.Hidden),
            ScrollbarWidthValue = 15f,
            FlexGrowValue = 1f,
        }, TestNodeContext.AhemText("HHHH\u200bHH", WritingMode.Horizontal));
        var node1 = taffy.NewLeafWithContext(new Style
        {
            Display = Display.Block,
            FlexGrowValue = 1f,
        }, TestNodeContext.AhemText("HHHH\u200bHH", WritingMode.Horizontal));
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(50f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(20f, layout_node.Size.Width);
        Assert.Equal(50f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(0f, layout_node0.Size.Width);
        Assert.Equal(50f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        Assert.Equal(40f, layout_node0.ScrollWidth());
        Assert.Equal(0f, layout_node0.ScrollHeight());
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(40f, layout_node1.Size.Width);
        Assert.Equal(50f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeafWithContext(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            OverflowValue = new Point<Overflow>(Overflow.Hidden, Overflow.Hidden),
            ScrollbarWidthValue = 15f,
            FlexGrowValue = 1f,
        }, TestNodeContext.AhemText("HHHH\u200bHH", WritingMode.Horizontal));
        var node1 = taffy.NewLeafWithContext(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 1f,
        }, TestNodeContext.AhemText("HHHH\u200bHH", WritingMode.Horizontal));
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(50f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(20f, layout_node.Size.Width);
        Assert.Equal(50f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(0f, layout_node0.Size.Width);
        Assert.Equal(50f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        Assert.Equal(40f, layout_node0.ScrollWidth());
        Assert.Equal(0f, layout_node0.ScrollHeight());
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(40f, layout_node1.Size.Width);
        Assert.Equal(50f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
    }
}
