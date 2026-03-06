using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridPercentItemsWidthAndPadding
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.5f), Dimension.Auto()),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.Auto()),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(12f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(12f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.5f), Dimension.Auto()),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.Auto()),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(12f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(112f, layout_node0.Size.Width);
        Assert.Equal(12f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
    }
}
