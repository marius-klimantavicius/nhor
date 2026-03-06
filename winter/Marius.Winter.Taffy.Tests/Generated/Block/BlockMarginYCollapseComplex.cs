using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Block;

public class BlockMarginYCollapseComplex
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-10f), LengthPercentageAuto.Length(-10f)),
        });
        var node10 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-5f), LengthPercentageAuto.Length(-5f)),
        });
        var node11 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(7f), LengthPercentageAuto.Length(3f)),
        });
        var node12 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-6f), LengthPercentageAuto.Length(9f)),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-10f), LengthPercentageAuto.Length(-10f)),
        }, new NodeId[] { node10, node11, node12 });
        var node2 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-5f), LengthPercentageAuto.Length(-5f)),
        });
        var node3 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-10f), LengthPercentageAuto.Length(-10f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.Auto()),
        }, new NodeId[] { node0, node1, node2, node3 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(50f, layout_node.Size.Width);
        Assert.Equal(0f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(50f, layout_node0.Size.Width);
        Assert.Equal(10f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(-10f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(50f, layout_node1.Size.Width);
        Assert.Equal(10f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(-10f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(0f, layout_node10.Size.Width);
        Assert.Equal(10f, layout_node10.Size.Height);
        Assert.Equal(0f, layout_node10.Location.X);
        Assert.Equal(-5f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(0f, layout_node11.Size.Width);
        Assert.Equal(10f, layout_node11.Size.Height);
        Assert.Equal(0f, layout_node11.Location.X);
        Assert.Equal(7f, layout_node11.Location.Y);
        var layout_node12 = taffy.GetLayout(node12);
        Assert.Equal(0f, layout_node12.Size.Width);
        Assert.Equal(10f, layout_node12.Size.Height);
        Assert.Equal(0f, layout_node12.Location.X);
        Assert.Equal(-6f, layout_node12.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(50f, layout_node2.Size.Width);
        Assert.Equal(10f, layout_node2.Size.Height);
        Assert.Equal(0f, layout_node2.Location.X);
        Assert.Equal(-10f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(50f, layout_node3.Size.Width);
        Assert.Equal(10f, layout_node3.Size.Height);
        Assert.Equal(0f, layout_node3.Location.X);
        Assert.Equal(-10f, layout_node3.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-10f), LengthPercentageAuto.Length(-10f)),
        });
        var node10 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-5f), LengthPercentageAuto.Length(-5f)),
        });
        var node11 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(7f), LengthPercentageAuto.Length(3f)),
        });
        var node12 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-6f), LengthPercentageAuto.Length(9f)),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-10f), LengthPercentageAuto.Length(-10f)),
        }, new NodeId[] { node10, node11, node12 });
        var node2 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-5f), LengthPercentageAuto.Length(-5f)),
        });
        var node3 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(-10f), LengthPercentageAuto.Length(-10f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.Auto()),
        }, new NodeId[] { node0, node1, node2, node3 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(50f, layout_node.Size.Width);
        Assert.Equal(0f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(50f, layout_node0.Size.Width);
        Assert.Equal(10f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(-10f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(50f, layout_node1.Size.Width);
        Assert.Equal(10f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(-10f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(0f, layout_node10.Size.Width);
        Assert.Equal(10f, layout_node10.Size.Height);
        Assert.Equal(0f, layout_node10.Location.X);
        Assert.Equal(-5f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(0f, layout_node11.Size.Width);
        Assert.Equal(10f, layout_node11.Size.Height);
        Assert.Equal(0f, layout_node11.Location.X);
        Assert.Equal(7f, layout_node11.Location.Y);
        var layout_node12 = taffy.GetLayout(node12);
        Assert.Equal(0f, layout_node12.Size.Width);
        Assert.Equal(10f, layout_node12.Size.Height);
        Assert.Equal(0f, layout_node12.Location.X);
        Assert.Equal(-6f, layout_node12.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(50f, layout_node2.Size.Width);
        Assert.Equal(10f, layout_node2.Size.Height);
        Assert.Equal(0f, layout_node2.Location.X);
        Assert.Equal(-10f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(50f, layout_node3.Size.Width);
        Assert.Equal(10f, layout_node3.Size.Height);
        Assert.Equal(0f, layout_node3.Location.X);
        Assert.Equal(-10f, layout_node3.Location.Y);
    }
}
