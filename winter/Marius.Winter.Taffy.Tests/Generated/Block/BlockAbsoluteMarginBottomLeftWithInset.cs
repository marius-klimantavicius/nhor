using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Block;

public class BlockAbsoluteMarginBottomLeftWithInset
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
            PositionValue = Position.Absolute,
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(10f), LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(10f)),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(20f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(20f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(10f, layout_node0.Size.Width);
        Assert.Equal(10f, layout_node0.Size.Height);
        Assert.Equal(30f, layout_node0.Location.X);
        Assert.Equal(10f, layout_node0.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            PositionValue = Position.Absolute,
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(10f), LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(10f)),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(20f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(20f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(10f, layout_node0.Size.Width);
        Assert.Equal(10f, layout_node0.Size.Height);
        Assert.Equal(30f, layout_node0.Location.X);
        Assert.Equal(10f, layout_node0.Location.Y);
    }
}
