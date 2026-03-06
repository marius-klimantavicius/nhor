using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Block;

public class BlockMarginYTotalCollapse
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(10f)),
        });
        var node01 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f)),
        });
        var node02 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(10f), LengthPercentageAuto.ZERO),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f)),
        }, new NodeId[] { node00, node01, node02 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.Auto()),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(50f, layout_node.Size.Width);
        Assert.Equal(30f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(50f, layout_node0.Size.Width);
        Assert.Equal(10f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(10f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(50f, layout_node00.Size.Width);
        Assert.Equal(10f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(50f, layout_node01.Size.Width);
        Assert.Equal(0f, layout_node01.Size.Height);
        Assert.Equal(0f, layout_node01.Location.X);
        Assert.Equal(20f, layout_node01.Location.Y);
        var layout_node02 = taffy.GetLayout(node02);
        Assert.Equal(50f, layout_node02.Size.Width);
        Assert.Equal(10f, layout_node02.Size.Height);
        Assert.Equal(0f, layout_node02.Location.X);
        Assert.Equal(20f, layout_node02.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(10f)),
        });
        var node01 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f)),
        });
        var node02 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(10f), LengthPercentageAuto.ZERO),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f)),
        }, new NodeId[] { node00, node01, node02 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.Auto()),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(50f, layout_node.Size.Width);
        Assert.Equal(30f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(50f, layout_node0.Size.Width);
        Assert.Equal(10f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(10f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(50f, layout_node00.Size.Width);
        Assert.Equal(10f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(50f, layout_node01.Size.Width);
        Assert.Equal(0f, layout_node01.Size.Height);
        Assert.Equal(0f, layout_node01.Location.X);
        Assert.Equal(20f, layout_node01.Location.Y);
        var layout_node02 = taffy.GetLayout(node02);
        Assert.Equal(50f, layout_node02.Size.Width);
        Assert.Equal(10f, layout_node02.Size.Height);
        Assert.Equal(0f, layout_node02.Location.X);
        Assert.Equal(20f, layout_node02.Location.Y);
    }
}
