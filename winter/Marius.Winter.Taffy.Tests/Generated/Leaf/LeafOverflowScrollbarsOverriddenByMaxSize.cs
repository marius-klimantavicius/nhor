using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Leaf;

public class LeafOverflowScrollbarsOverriddenByMaxSize
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node = taffy.NewLeaf(new Style
        {
            OverflowValue = new Point<Overflow>(Overflow.Scroll, Overflow.Scroll),
            ScrollbarWidthValue = 15f,
            MaxSizeValue = new Size<Dimension>(Dimension.FromLength(2f), Dimension.FromLength(4f)),
        });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(2f, layout_node.Size.Width);
        Assert.Equal(4f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        Assert.Equal(0f, layout_node.ScrollWidth());
        Assert.Equal(0f, layout_node.ScrollHeight());
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            OverflowValue = new Point<Overflow>(Overflow.Scroll, Overflow.Scroll),
            ScrollbarWidthValue = 15f,
            MaxSizeValue = new Size<Dimension>(Dimension.FromLength(2f), Dimension.FromLength(4f)),
        });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(2f, layout_node.Size.Width);
        Assert.Equal(4f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        Assert.Equal(0f, layout_node.ScrollWidth());
        Assert.Equal(0f, layout_node.ScrollHeight());
    }
}
