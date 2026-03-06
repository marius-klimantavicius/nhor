using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class GapRowGapRowWrapChildMargins
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(2f), LengthPercentageAuto.Length(2f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f)),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(15f), LengthPercentageAuto.Length(15f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            FlexWrapValue = FlexWrap.Wrap,
            GapValue = new Size<LengthPercentage>(LengthPercentage.ZERO, LengthPercentage.Length(10f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(200f)),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(60f, layout_node0.Size.Width);
        Assert.Equal(42f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(2f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(60f, layout_node1.Size.Width);
        Assert.Equal(42f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(66f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(60f, layout_node2.Size.Width);
        Assert.Equal(42f, layout_node2.Size.Height);
        Assert.Equal(0f, layout_node2.Location.X);
        Assert.Equal(143f, layout_node2.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(2f), LengthPercentageAuto.Length(2f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f)),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(15f), LengthPercentageAuto.Length(15f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexWrapValue = FlexWrap.Wrap,
            GapValue = new Size<LengthPercentage>(LengthPercentage.ZERO, LengthPercentage.Length(10f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(200f)),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(60f, layout_node0.Size.Width);
        Assert.Equal(42f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(2f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(60f, layout_node1.Size.Width);
        Assert.Equal(42f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(66f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(60f, layout_node2.Size.Width);
        Assert.Equal(42f, layout_node2.Size.Height);
        Assert.Equal(0f, layout_node2.Location.X);
        Assert.Equal(143f, layout_node2.Location.Y);
    }
}
