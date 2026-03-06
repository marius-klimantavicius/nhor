using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class RoundingFractialInput7
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(1f), Dimension.FromLength(10f)),
        });
        var node01 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(1f), Dimension.FromLength(10f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            FlexWrapValue = FlexWrap.Wrap,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.25f), Dimension.Auto()),
        }, new NodeId[] { node00, node01 });
        var node10 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(1f), Dimension.FromLength(10f)),
        });
        var node11 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(1f), Dimension.FromLength(10f)),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            FlexWrapValue = FlexWrap.Wrap,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.25f), Dimension.Auto()),
        }, new NodeId[] { node10, node11 });
        var node20 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(1f), Dimension.FromLength(10f)),
        });
        var node21 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(1f), Dimension.FromLength(10f)),
        });
        var node2 = taffy.NewWithChildren(new Style
        {
            FlexWrapValue = FlexWrap.Wrap,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.25f), Dimension.Auto()),
        }, new NodeId[] { node20, node21 });
        var node30 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(1f), Dimension.FromLength(10f)),
        });
        var node31 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(1f), Dimension.FromLength(10f)),
        });
        var node3 = taffy.NewWithChildren(new Style
        {
            FlexWrapValue = FlexWrap.Wrap,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.25f), Dimension.Auto()),
        }, new NodeId[] { node30, node31 });
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(7f), Dimension.Auto()),
        }, new NodeId[] { node0, node1, node2, node3 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(7f, layout_node.Size.Width);
        Assert.Equal(20f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(2f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(1f, layout_node00.Size.Width);
        Assert.Equal(10f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(1f, layout_node01.Size.Width);
        Assert.Equal(10f, layout_node01.Size.Height);
        Assert.Equal(0f, layout_node01.Location.X);
        Assert.Equal(10f, layout_node01.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(2f, layout_node1.Size.Width);
        Assert.Equal(20f, layout_node1.Size.Height);
        Assert.Equal(2f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(1f, layout_node10.Size.Width);
        Assert.Equal(10f, layout_node10.Size.Height);
        Assert.Equal(0f, layout_node10.Location.X);
        Assert.Equal(0f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(1f, layout_node11.Size.Width);
        Assert.Equal(10f, layout_node11.Size.Height);
        Assert.Equal(0f, layout_node11.Location.X);
        Assert.Equal(10f, layout_node11.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(1f, layout_node2.Size.Width);
        Assert.Equal(20f, layout_node2.Size.Height);
        Assert.Equal(4f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
        var layout_node20 = taffy.GetLayout(node20);
        Assert.Equal(1f, layout_node20.Size.Width);
        Assert.Equal(10f, layout_node20.Size.Height);
        Assert.Equal(0f, layout_node20.Location.X);
        Assert.Equal(0f, layout_node20.Location.Y);
        var layout_node21 = taffy.GetLayout(node21);
        Assert.Equal(1f, layout_node21.Size.Width);
        Assert.Equal(10f, layout_node21.Size.Height);
        Assert.Equal(0f, layout_node21.Location.X);
        Assert.Equal(10f, layout_node21.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(2f, layout_node3.Size.Width);
        Assert.Equal(20f, layout_node3.Size.Height);
        Assert.Equal(5f, layout_node3.Location.X);
        Assert.Equal(0f, layout_node3.Location.Y);
        var layout_node30 = taffy.GetLayout(node30);
        Assert.Equal(1f, layout_node30.Size.Width);
        Assert.Equal(10f, layout_node30.Size.Height);
        Assert.Equal(0f, layout_node30.Location.X);
        Assert.Equal(0f, layout_node30.Location.Y);
        var layout_node31 = taffy.GetLayout(node31);
        Assert.Equal(1f, layout_node31.Size.Width);
        Assert.Equal(10f, layout_node31.Size.Height);
        Assert.Equal(0f, layout_node31.Location.X);
        Assert.Equal(10f, layout_node31.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(1f), Dimension.FromLength(10f)),
        });
        var node01 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(1f), Dimension.FromLength(10f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexWrapValue = FlexWrap.Wrap,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.25f), Dimension.Auto()),
        }, new NodeId[] { node00, node01 });
        var node10 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(1f), Dimension.FromLength(10f)),
        });
        var node11 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(1f), Dimension.FromLength(10f)),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexWrapValue = FlexWrap.Wrap,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.25f), Dimension.Auto()),
        }, new NodeId[] { node10, node11 });
        var node20 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(1f), Dimension.FromLength(10f)),
        });
        var node21 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(1f), Dimension.FromLength(10f)),
        });
        var node2 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexWrapValue = FlexWrap.Wrap,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.25f), Dimension.Auto()),
        }, new NodeId[] { node20, node21 });
        var node30 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(1f), Dimension.FromLength(10f)),
        });
        var node31 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(1f), Dimension.FromLength(10f)),
        });
        var node3 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexWrapValue = FlexWrap.Wrap,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.25f), Dimension.Auto()),
        }, new NodeId[] { node30, node31 });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(7f), Dimension.Auto()),
        }, new NodeId[] { node0, node1, node2, node3 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(7f, layout_node.Size.Width);
        Assert.Equal(20f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(2f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(1f, layout_node00.Size.Width);
        Assert.Equal(10f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(1f, layout_node01.Size.Width);
        Assert.Equal(10f, layout_node01.Size.Height);
        Assert.Equal(0f, layout_node01.Location.X);
        Assert.Equal(10f, layout_node01.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(2f, layout_node1.Size.Width);
        Assert.Equal(20f, layout_node1.Size.Height);
        Assert.Equal(2f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(1f, layout_node10.Size.Width);
        Assert.Equal(10f, layout_node10.Size.Height);
        Assert.Equal(0f, layout_node10.Location.X);
        Assert.Equal(0f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(1f, layout_node11.Size.Width);
        Assert.Equal(10f, layout_node11.Size.Height);
        Assert.Equal(0f, layout_node11.Location.X);
        Assert.Equal(10f, layout_node11.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(1f, layout_node2.Size.Width);
        Assert.Equal(20f, layout_node2.Size.Height);
        Assert.Equal(4f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
        var layout_node20 = taffy.GetLayout(node20);
        Assert.Equal(1f, layout_node20.Size.Width);
        Assert.Equal(10f, layout_node20.Size.Height);
        Assert.Equal(0f, layout_node20.Location.X);
        Assert.Equal(0f, layout_node20.Location.Y);
        var layout_node21 = taffy.GetLayout(node21);
        Assert.Equal(1f, layout_node21.Size.Width);
        Assert.Equal(10f, layout_node21.Size.Height);
        Assert.Equal(0f, layout_node21.Location.X);
        Assert.Equal(10f, layout_node21.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(2f, layout_node3.Size.Width);
        Assert.Equal(20f, layout_node3.Size.Height);
        Assert.Equal(5f, layout_node3.Location.X);
        Assert.Equal(0f, layout_node3.Location.Y);
        var layout_node30 = taffy.GetLayout(node30);
        Assert.Equal(1f, layout_node30.Size.Width);
        Assert.Equal(10f, layout_node30.Size.Height);
        Assert.Equal(0f, layout_node30.Location.X);
        Assert.Equal(0f, layout_node30.Location.Y);
        var layout_node31 = taffy.GetLayout(node31);
        Assert.Equal(1f, layout_node31.Size.Width);
        Assert.Equal(10f, layout_node31.Size.Height);
        Assert.Equal(0f, layout_node31.Location.X);
        Assert.Equal(10f, layout_node31.Location.Y);
    }
}
