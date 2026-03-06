using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Block;

public class BlockAbsoluteMarginAutoMultipleChildrenWithInset
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            PositionValue = Position.Absolute,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(50f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.AUTO, LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(20f), LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            PositionValue = Position.Absolute,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.AUTO, LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(20f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            AlignItemsValue = AlignItems.Center,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(200f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(50f, layout_node0.Size.Height);
        Assert.Equal(10f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(50f, layout_node1.Size.Width);
        Assert.Equal(50f, layout_node1.Size.Height);
        Assert.Equal(20f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            PositionValue = Position.Absolute,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(50f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.AUTO, LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(20f), LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            PositionValue = Position.Absolute,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.AUTO, LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(20f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            AlignItemsValue = AlignItems.Center,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(200f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(50f, layout_node0.Size.Height);
        Assert.Equal(10f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(50f, layout_node1.Size.Width);
        Assert.Equal(50f, layout_node1.Size.Height);
        Assert.Equal(20f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
    }
}
