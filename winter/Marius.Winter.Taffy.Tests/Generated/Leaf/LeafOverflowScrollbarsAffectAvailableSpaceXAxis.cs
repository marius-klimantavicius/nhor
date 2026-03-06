using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Leaf;

public class LeafOverflowScrollbarsAffectAvailableSpaceXAxis
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node = taffy.NewLeafWithContext(new Style
        {
            OverflowValue = new Point<Overflow>(Overflow.Scroll, Overflow.Visible),
            ScrollbarWidthValue = 15f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(45f), Dimension.FromLength(45f)),
        }, TestNodeContext.AhemText("HHHHHHHHHHHHHHHHHHHHH", WritingMode.Horizontal));
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(45f, layout_node.Size.Width);
        Assert.Equal(45f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        Assert.Equal(165f, layout_node.ScrollWidth());
        Assert.Equal(0f, layout_node.ScrollHeight());
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node = taffy.NewLeafWithContext(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            OverflowValue = new Point<Overflow>(Overflow.Scroll, Overflow.Visible),
            ScrollbarWidthValue = 15f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(45f), Dimension.FromLength(45f)),
        }, TestNodeContext.AhemText("HHHHHHHHHHHHHHHHHHHHH", WritingMode.Horizontal));
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(45f, layout_node.Size.Width);
        Assert.Equal(45f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        Assert.Equal(165f, layout_node.ScrollWidth());
        Assert.Equal(0f, layout_node.ScrollHeight());
    }
}
