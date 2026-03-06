using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Block;

public class BlockMarginXPercentageIntrinsicSizeSelfNegative
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(-0.2f), LengthPercentageAuto.Percent(-0.1f), LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(10f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(10f, layout_node0.Size.Height);
        Assert.Equal(-20f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(-0.2f), LengthPercentageAuto.Percent(-0.1f), LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(10f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(10f, layout_node0.Size.Height);
        Assert.Equal(-20f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
    }
}
