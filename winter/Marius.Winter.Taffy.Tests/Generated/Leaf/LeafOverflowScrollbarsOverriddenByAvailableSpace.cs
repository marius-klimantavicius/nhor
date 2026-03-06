using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Leaf;

public class LeafOverflowScrollbarsOverriddenByAvailableSpace
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            OverflowValue = new Point<Overflow>(Overflow.Scroll, Overflow.Scroll),
            ScrollbarWidthValue = 15f,
            FlexGrowValue = 1f,
        });
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(2f), Dimension.FromLength(4f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(2f, layout_node.Size.Width);
        Assert.Equal(4f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(2f, layout_node0.Size.Width);
        Assert.Equal(4f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        Assert.Equal(0f, layout_node0.ScrollWidth());
        Assert.Equal(0f, layout_node0.ScrollHeight());
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            OverflowValue = new Point<Overflow>(Overflow.Scroll, Overflow.Scroll),
            ScrollbarWidthValue = 15f,
            FlexGrowValue = 1f,
        });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(2f), Dimension.FromLength(4f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(2f, layout_node.Size.Width);
        Assert.Equal(4f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(2f, layout_node0.Size.Width);
        Assert.Equal(4f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        Assert.Equal(0f, layout_node0.ScrollWidth());
        Assert.Equal(0f, layout_node0.ScrollHeight());
    }
}
