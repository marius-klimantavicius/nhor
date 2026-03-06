using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class IntrinsicSizingMainSizeRowWrap
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeafWithContext(new Style(), TestNodeContext.AhemText("HH\u200bHH", WritingMode.Horizontal));
        var node1 = taffy.NewLeafWithContext(new Style(), TestNodeContext.AhemText("HH\u200bHH", WritingMode.Horizontal));
        var node = taffy.NewWithChildren(new Style
        {
            FlexWrapValue = FlexWrap.Wrap,
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(80f, layout_node.Size.Width);
        Assert.Equal(10f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(40f, layout_node0.Size.Width);
        Assert.Equal(10f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(40f, layout_node1.Size.Width);
        Assert.Equal(10f, layout_node1.Size.Height);
        Assert.Equal(40f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeafWithContext(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        }, TestNodeContext.AhemText("HH\u200bHH", WritingMode.Horizontal));
        var node1 = taffy.NewLeafWithContext(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        }, TestNodeContext.AhemText("HH\u200bHH", WritingMode.Horizontal));
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexWrapValue = FlexWrap.Wrap,
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(80f, layout_node.Size.Width);
        Assert.Equal(10f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(40f, layout_node0.Size.Width);
        Assert.Equal(10f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(40f, layout_node1.Size.Width);
        Assert.Equal(10f, layout_node1.Size.Height);
        Assert.Equal(40f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
    }
}
