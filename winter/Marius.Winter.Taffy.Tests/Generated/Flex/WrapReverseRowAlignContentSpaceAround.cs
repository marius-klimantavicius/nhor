using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class WrapReverseRowAlignContentSpaceAround
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(30f), Dimension.FromLength(10f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(30f), Dimension.FromLength(20f)),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(30f), Dimension.FromLength(30f)),
        });
        var node3 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(30f), Dimension.FromLength(40f)),
        });
        var node4 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(30f), Dimension.FromLength(50f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            FlexWrapValue = FlexWrap.WrapReverse,
            AlignContentValue = AlignContent.SpaceAround,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.Auto()),
        }, new NodeId[] { node0, node1, node2, node3, node4 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(80f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(30f, layout_node0.Size.Width);
        Assert.Equal(10f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(70f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(30f, layout_node1.Size.Width);
        Assert.Equal(20f, layout_node1.Size.Height);
        Assert.Equal(30f, layout_node1.Location.X);
        Assert.Equal(60f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(30f, layout_node2.Size.Width);
        Assert.Equal(30f, layout_node2.Size.Height);
        Assert.Equal(60f, layout_node2.Location.X);
        Assert.Equal(50f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(30f, layout_node3.Size.Width);
        Assert.Equal(40f, layout_node3.Size.Height);
        Assert.Equal(0f, layout_node3.Location.X);
        Assert.Equal(10f, layout_node3.Location.Y);
        var layout_node4 = taffy.GetLayout(node4);
        Assert.Equal(30f, layout_node4.Size.Width);
        Assert.Equal(50f, layout_node4.Size.Height);
        Assert.Equal(30f, layout_node4.Location.X);
        Assert.Equal(0f, layout_node4.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(30f), Dimension.FromLength(10f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(30f), Dimension.FromLength(20f)),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(30f), Dimension.FromLength(30f)),
        });
        var node3 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(30f), Dimension.FromLength(40f)),
        });
        var node4 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(30f), Dimension.FromLength(50f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexWrapValue = FlexWrap.WrapReverse,
            AlignContentValue = AlignContent.SpaceAround,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.Auto()),
        }, new NodeId[] { node0, node1, node2, node3, node4 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(80f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(30f, layout_node0.Size.Width);
        Assert.Equal(10f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(70f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(30f, layout_node1.Size.Width);
        Assert.Equal(20f, layout_node1.Size.Height);
        Assert.Equal(30f, layout_node1.Location.X);
        Assert.Equal(60f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(30f, layout_node2.Size.Width);
        Assert.Equal(30f, layout_node2.Size.Height);
        Assert.Equal(60f, layout_node2.Location.X);
        Assert.Equal(50f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(30f, layout_node3.Size.Width);
        Assert.Equal(40f, layout_node3.Size.Height);
        Assert.Equal(0f, layout_node3.Location.X);
        Assert.Equal(10f, layout_node3.Location.Y);
        var layout_node4 = taffy.GetLayout(node4);
        Assert.Equal(30f, layout_node4.Size.Width);
        Assert.Equal(50f, layout_node4.Size.Height);
        Assert.Equal(30f, layout_node4.Location.X);
        Assert.Equal(0f, layout_node4.Location.Y);
    }
}
