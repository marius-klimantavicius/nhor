using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Block;

public class BlockInsetPercentage
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(0.02f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(0.04f), LengthPercentageAuto.AUTO),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(0.06f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(0.08f)),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(0.02f), LengthPercentageAuto.Percent(0.06f), LengthPercentageAuto.Percent(0.04f), LengthPercentageAuto.Percent(0.08f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.Auto()),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(50f, layout_node.Size.Width);
        Assert.Equal(30f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(50f, layout_node0.Size.Width);
        Assert.Equal(10f, layout_node0.Size.Height);
        Assert.Equal(1f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(50f, layout_node1.Size.Width);
        Assert.Equal(10f, layout_node1.Size.Height);
        Assert.Equal(-3f, layout_node1.Location.X);
        Assert.Equal(10f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(50f, layout_node2.Size.Width);
        Assert.Equal(10f, layout_node2.Size.Height);
        Assert.Equal(1f, layout_node2.Location.X);
        Assert.Equal(20f, layout_node2.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(0.02f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(0.04f), LengthPercentageAuto.AUTO),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(0.06f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(0.08f)),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(0.02f), LengthPercentageAuto.Percent(0.06f), LengthPercentageAuto.Percent(0.04f), LengthPercentageAuto.Percent(0.08f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.Auto()),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(50f, layout_node.Size.Width);
        Assert.Equal(30f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(50f, layout_node0.Size.Width);
        Assert.Equal(10f, layout_node0.Size.Height);
        Assert.Equal(1f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(50f, layout_node1.Size.Width);
        Assert.Equal(10f, layout_node1.Size.Height);
        Assert.Equal(-3f, layout_node1.Location.X);
        Assert.Equal(10f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(50f, layout_node2.Size.Width);
        Assert.Equal(10f, layout_node2.Size.Height);
        Assert.Equal(1f, layout_node2.Location.X);
        Assert.Equal(20f, layout_node2.Location.Y);
    }
}
