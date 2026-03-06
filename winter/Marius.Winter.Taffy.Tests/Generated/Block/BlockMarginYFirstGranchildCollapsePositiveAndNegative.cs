using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Block;

public class BlockMarginYFirstGranchildCollapsePositiveAndNegative
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0000 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
        });
        var node000 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-10f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node0000 });
        var node00 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(10f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node000 });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-10f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node00 });
        var node1000 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
        });
        var node100 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-2f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node1000 });
        var node10 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(5f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node100 });
        var node1 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-10f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node10 });
        var node2000 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
        });
        var node200 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(15f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node2000 });
        var node20 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(5f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node200 });
        var node2 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-10f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node20 });
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
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(50f, layout_node00.Size.Width);
        Assert.Equal(10f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node000 = taffy.GetLayout(node000);
        Assert.Equal(50f, layout_node000.Size.Width);
        Assert.Equal(10f, layout_node000.Size.Height);
        Assert.Equal(0f, layout_node000.Location.X);
        Assert.Equal(0f, layout_node000.Location.Y);
        var layout_node0000 = taffy.GetLayout(node0000);
        Assert.Equal(50f, layout_node0000.Size.Width);
        Assert.Equal(10f, layout_node0000.Size.Height);
        Assert.Equal(0f, layout_node0000.Location.X);
        Assert.Equal(0f, layout_node0000.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(50f, layout_node1.Size.Width);
        Assert.Equal(10f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(5f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(50f, layout_node10.Size.Width);
        Assert.Equal(10f, layout_node10.Size.Height);
        Assert.Equal(0f, layout_node10.Location.X);
        Assert.Equal(0f, layout_node10.Location.Y);
        var layout_node100 = taffy.GetLayout(node100);
        Assert.Equal(50f, layout_node100.Size.Width);
        Assert.Equal(10f, layout_node100.Size.Height);
        Assert.Equal(0f, layout_node100.Location.X);
        Assert.Equal(0f, layout_node100.Location.Y);
        var layout_node1000 = taffy.GetLayout(node1000);
        Assert.Equal(50f, layout_node1000.Size.Width);
        Assert.Equal(10f, layout_node1000.Size.Height);
        Assert.Equal(0f, layout_node1000.Location.X);
        Assert.Equal(0f, layout_node1000.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(50f, layout_node2.Size.Width);
        Assert.Equal(10f, layout_node2.Size.Height);
        Assert.Equal(0f, layout_node2.Location.X);
        Assert.Equal(20f, layout_node2.Location.Y);
        var layout_node20 = taffy.GetLayout(node20);
        Assert.Equal(50f, layout_node20.Size.Width);
        Assert.Equal(10f, layout_node20.Size.Height);
        Assert.Equal(0f, layout_node20.Location.X);
        Assert.Equal(0f, layout_node20.Location.Y);
        var layout_node200 = taffy.GetLayout(node200);
        Assert.Equal(50f, layout_node200.Size.Width);
        Assert.Equal(10f, layout_node200.Size.Height);
        Assert.Equal(0f, layout_node200.Location.X);
        Assert.Equal(0f, layout_node200.Location.Y);
        var layout_node2000 = taffy.GetLayout(node2000);
        Assert.Equal(50f, layout_node2000.Size.Width);
        Assert.Equal(10f, layout_node2000.Size.Height);
        Assert.Equal(0f, layout_node2000.Location.X);
        Assert.Equal(0f, layout_node2000.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0000 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
        });
        var node000 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-10f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node0000 });
        var node00 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(10f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node000 });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-10f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node00 });
        var node1000 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
        });
        var node100 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-2f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node1000 });
        var node10 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(5f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node100 });
        var node1 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-10f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node10 });
        var node2000 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
        });
        var node200 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(15f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node2000 });
        var node20 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(5f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node200 });
        var node2 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-10f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node20 });
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
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(50f, layout_node00.Size.Width);
        Assert.Equal(10f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node000 = taffy.GetLayout(node000);
        Assert.Equal(50f, layout_node000.Size.Width);
        Assert.Equal(10f, layout_node000.Size.Height);
        Assert.Equal(0f, layout_node000.Location.X);
        Assert.Equal(0f, layout_node000.Location.Y);
        var layout_node0000 = taffy.GetLayout(node0000);
        Assert.Equal(50f, layout_node0000.Size.Width);
        Assert.Equal(10f, layout_node0000.Size.Height);
        Assert.Equal(0f, layout_node0000.Location.X);
        Assert.Equal(0f, layout_node0000.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(50f, layout_node1.Size.Width);
        Assert.Equal(10f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(5f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(50f, layout_node10.Size.Width);
        Assert.Equal(10f, layout_node10.Size.Height);
        Assert.Equal(0f, layout_node10.Location.X);
        Assert.Equal(0f, layout_node10.Location.Y);
        var layout_node100 = taffy.GetLayout(node100);
        Assert.Equal(50f, layout_node100.Size.Width);
        Assert.Equal(10f, layout_node100.Size.Height);
        Assert.Equal(0f, layout_node100.Location.X);
        Assert.Equal(0f, layout_node100.Location.Y);
        var layout_node1000 = taffy.GetLayout(node1000);
        Assert.Equal(50f, layout_node1000.Size.Width);
        Assert.Equal(10f, layout_node1000.Size.Height);
        Assert.Equal(0f, layout_node1000.Location.X);
        Assert.Equal(0f, layout_node1000.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(50f, layout_node2.Size.Width);
        Assert.Equal(10f, layout_node2.Size.Height);
        Assert.Equal(0f, layout_node2.Location.X);
        Assert.Equal(20f, layout_node2.Location.Y);
        var layout_node20 = taffy.GetLayout(node20);
        Assert.Equal(50f, layout_node20.Size.Width);
        Assert.Equal(10f, layout_node20.Size.Height);
        Assert.Equal(0f, layout_node20.Location.X);
        Assert.Equal(0f, layout_node20.Location.Y);
        var layout_node200 = taffy.GetLayout(node200);
        Assert.Equal(50f, layout_node200.Size.Width);
        Assert.Equal(10f, layout_node200.Size.Height);
        Assert.Equal(0f, layout_node200.Location.X);
        Assert.Equal(0f, layout_node200.Location.Y);
        var layout_node2000 = taffy.GetLayout(node2000);
        Assert.Equal(50f, layout_node2000.Size.Width);
        Assert.Equal(10f, layout_node2000.Size.Height);
        Assert.Equal(0f, layout_node2000.Location.X);
        Assert.Equal(0f, layout_node2000.Location.Y);
    }
}
