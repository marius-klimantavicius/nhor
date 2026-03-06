using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Leaf;

public class LeafOverflowScrollbarsTakeUpSpaceYAxis
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node = taffy.NewLeafWithContext(new Style
        {
            OverflowValue = new Point<Overflow>(Overflow.Visible, Overflow.Scroll),
            ScrollbarWidthValue = 15f,
        }, TestNodeContext.AhemText("HH", WritingMode.Horizontal));
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(35f, layout_node.Size.Width);
        Assert.Equal(10f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        Assert.Equal(0f, layout_node.ScrollWidth());
        Assert.Equal(0f, layout_node.ScrollHeight());
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node = taffy.NewLeafWithContext(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            OverflowValue = new Point<Overflow>(Overflow.Visible, Overflow.Scroll),
            ScrollbarWidthValue = 15f,
        }, TestNodeContext.AhemText("HH", WritingMode.Horizontal));
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(35f, layout_node.Size.Width);
        Assert.Equal(10f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        Assert.Equal(0f, layout_node.ScrollWidth());
        Assert.Equal(0f, layout_node.ScrollHeight());
    }
}
