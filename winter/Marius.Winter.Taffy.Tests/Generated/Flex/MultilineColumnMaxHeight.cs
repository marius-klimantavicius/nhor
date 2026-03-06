using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class MultilineColumnMaxHeight
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node3 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node4 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node5 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node6 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node7 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node8 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node9 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node10 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node11 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node12 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node13 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node14 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node15 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node16 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node17 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node18 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node19 = taffy.NewLeaf(new Style
        {
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            FlexDirectionValue = FlexDirection.Column,
            FlexWrapValue = FlexWrap.Wrap,
            MaxSizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(200f)),
        }, new NodeId[] { node0, node1, node2, node3, node4, node5, node6, node7, node8, node9, node10, node11, node12, node13, node14, node15, node16, node17, node18, node19 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(80f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(40f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(40f, layout_node1.Size.Width);
        Assert.Equal(20f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(20f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(40f, layout_node2.Size.Width);
        Assert.Equal(20f, layout_node2.Size.Height);
        Assert.Equal(0f, layout_node2.Location.X);
        Assert.Equal(40f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(40f, layout_node3.Size.Width);
        Assert.Equal(20f, layout_node3.Size.Height);
        Assert.Equal(0f, layout_node3.Location.X);
        Assert.Equal(60f, layout_node3.Location.Y);
        var layout_node4 = taffy.GetLayout(node4);
        Assert.Equal(40f, layout_node4.Size.Width);
        Assert.Equal(20f, layout_node4.Size.Height);
        Assert.Equal(0f, layout_node4.Location.X);
        Assert.Equal(80f, layout_node4.Location.Y);
        var layout_node5 = taffy.GetLayout(node5);
        Assert.Equal(40f, layout_node5.Size.Width);
        Assert.Equal(20f, layout_node5.Size.Height);
        Assert.Equal(0f, layout_node5.Location.X);
        Assert.Equal(100f, layout_node5.Location.Y);
        var layout_node6 = taffy.GetLayout(node6);
        Assert.Equal(40f, layout_node6.Size.Width);
        Assert.Equal(20f, layout_node6.Size.Height);
        Assert.Equal(0f, layout_node6.Location.X);
        Assert.Equal(120f, layout_node6.Location.Y);
        var layout_node7 = taffy.GetLayout(node7);
        Assert.Equal(40f, layout_node7.Size.Width);
        Assert.Equal(20f, layout_node7.Size.Height);
        Assert.Equal(0f, layout_node7.Location.X);
        Assert.Equal(140f, layout_node7.Location.Y);
        var layout_node8 = taffy.GetLayout(node8);
        Assert.Equal(40f, layout_node8.Size.Width);
        Assert.Equal(20f, layout_node8.Size.Height);
        Assert.Equal(0f, layout_node8.Location.X);
        Assert.Equal(160f, layout_node8.Location.Y);
        var layout_node9 = taffy.GetLayout(node9);
        Assert.Equal(40f, layout_node9.Size.Width);
        Assert.Equal(20f, layout_node9.Size.Height);
        Assert.Equal(0f, layout_node9.Location.X);
        Assert.Equal(180f, layout_node9.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(40f, layout_node10.Size.Width);
        Assert.Equal(20f, layout_node10.Size.Height);
        Assert.Equal(40f, layout_node10.Location.X);
        Assert.Equal(0f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(40f, layout_node11.Size.Width);
        Assert.Equal(20f, layout_node11.Size.Height);
        Assert.Equal(40f, layout_node11.Location.X);
        Assert.Equal(20f, layout_node11.Location.Y);
        var layout_node12 = taffy.GetLayout(node12);
        Assert.Equal(40f, layout_node12.Size.Width);
        Assert.Equal(20f, layout_node12.Size.Height);
        Assert.Equal(40f, layout_node12.Location.X);
        Assert.Equal(40f, layout_node12.Location.Y);
        var layout_node13 = taffy.GetLayout(node13);
        Assert.Equal(40f, layout_node13.Size.Width);
        Assert.Equal(20f, layout_node13.Size.Height);
        Assert.Equal(40f, layout_node13.Location.X);
        Assert.Equal(60f, layout_node13.Location.Y);
        var layout_node14 = taffy.GetLayout(node14);
        Assert.Equal(40f, layout_node14.Size.Width);
        Assert.Equal(20f, layout_node14.Size.Height);
        Assert.Equal(40f, layout_node14.Location.X);
        Assert.Equal(80f, layout_node14.Location.Y);
        var layout_node15 = taffy.GetLayout(node15);
        Assert.Equal(40f, layout_node15.Size.Width);
        Assert.Equal(20f, layout_node15.Size.Height);
        Assert.Equal(40f, layout_node15.Location.X);
        Assert.Equal(100f, layout_node15.Location.Y);
        var layout_node16 = taffy.GetLayout(node16);
        Assert.Equal(40f, layout_node16.Size.Width);
        Assert.Equal(20f, layout_node16.Size.Height);
        Assert.Equal(40f, layout_node16.Location.X);
        Assert.Equal(120f, layout_node16.Location.Y);
        var layout_node17 = taffy.GetLayout(node17);
        Assert.Equal(40f, layout_node17.Size.Width);
        Assert.Equal(20f, layout_node17.Size.Height);
        Assert.Equal(40f, layout_node17.Location.X);
        Assert.Equal(140f, layout_node17.Location.Y);
        var layout_node18 = taffy.GetLayout(node18);
        Assert.Equal(40f, layout_node18.Size.Width);
        Assert.Equal(20f, layout_node18.Size.Height);
        Assert.Equal(40f, layout_node18.Location.X);
        Assert.Equal(160f, layout_node18.Location.Y);
        var layout_node19 = taffy.GetLayout(node19);
        Assert.Equal(40f, layout_node19.Size.Width);
        Assert.Equal(20f, layout_node19.Size.Height);
        Assert.Equal(40f, layout_node19.Location.X);
        Assert.Equal(180f, layout_node19.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node3 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node4 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node5 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node6 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node7 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node8 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node9 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node10 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node11 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node12 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node13 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node14 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node15 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node16 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node17 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node18 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node19 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(20f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            FlexWrapValue = FlexWrap.Wrap,
            MaxSizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(200f)),
        }, new NodeId[] { node0, node1, node2, node3, node4, node5, node6, node7, node8, node9, node10, node11, node12, node13, node14, node15, node16, node17, node18, node19 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(80f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(40f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(40f, layout_node1.Size.Width);
        Assert.Equal(20f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(20f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(40f, layout_node2.Size.Width);
        Assert.Equal(20f, layout_node2.Size.Height);
        Assert.Equal(0f, layout_node2.Location.X);
        Assert.Equal(40f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(40f, layout_node3.Size.Width);
        Assert.Equal(20f, layout_node3.Size.Height);
        Assert.Equal(0f, layout_node3.Location.X);
        Assert.Equal(60f, layout_node3.Location.Y);
        var layout_node4 = taffy.GetLayout(node4);
        Assert.Equal(40f, layout_node4.Size.Width);
        Assert.Equal(20f, layout_node4.Size.Height);
        Assert.Equal(0f, layout_node4.Location.X);
        Assert.Equal(80f, layout_node4.Location.Y);
        var layout_node5 = taffy.GetLayout(node5);
        Assert.Equal(40f, layout_node5.Size.Width);
        Assert.Equal(20f, layout_node5.Size.Height);
        Assert.Equal(0f, layout_node5.Location.X);
        Assert.Equal(100f, layout_node5.Location.Y);
        var layout_node6 = taffy.GetLayout(node6);
        Assert.Equal(40f, layout_node6.Size.Width);
        Assert.Equal(20f, layout_node6.Size.Height);
        Assert.Equal(0f, layout_node6.Location.X);
        Assert.Equal(120f, layout_node6.Location.Y);
        var layout_node7 = taffy.GetLayout(node7);
        Assert.Equal(40f, layout_node7.Size.Width);
        Assert.Equal(20f, layout_node7.Size.Height);
        Assert.Equal(0f, layout_node7.Location.X);
        Assert.Equal(140f, layout_node7.Location.Y);
        var layout_node8 = taffy.GetLayout(node8);
        Assert.Equal(40f, layout_node8.Size.Width);
        Assert.Equal(20f, layout_node8.Size.Height);
        Assert.Equal(0f, layout_node8.Location.X);
        Assert.Equal(160f, layout_node8.Location.Y);
        var layout_node9 = taffy.GetLayout(node9);
        Assert.Equal(40f, layout_node9.Size.Width);
        Assert.Equal(20f, layout_node9.Size.Height);
        Assert.Equal(0f, layout_node9.Location.X);
        Assert.Equal(180f, layout_node9.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(40f, layout_node10.Size.Width);
        Assert.Equal(20f, layout_node10.Size.Height);
        Assert.Equal(40f, layout_node10.Location.X);
        Assert.Equal(0f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(40f, layout_node11.Size.Width);
        Assert.Equal(20f, layout_node11.Size.Height);
        Assert.Equal(40f, layout_node11.Location.X);
        Assert.Equal(20f, layout_node11.Location.Y);
        var layout_node12 = taffy.GetLayout(node12);
        Assert.Equal(40f, layout_node12.Size.Width);
        Assert.Equal(20f, layout_node12.Size.Height);
        Assert.Equal(40f, layout_node12.Location.X);
        Assert.Equal(40f, layout_node12.Location.Y);
        var layout_node13 = taffy.GetLayout(node13);
        Assert.Equal(40f, layout_node13.Size.Width);
        Assert.Equal(20f, layout_node13.Size.Height);
        Assert.Equal(40f, layout_node13.Location.X);
        Assert.Equal(60f, layout_node13.Location.Y);
        var layout_node14 = taffy.GetLayout(node14);
        Assert.Equal(40f, layout_node14.Size.Width);
        Assert.Equal(20f, layout_node14.Size.Height);
        Assert.Equal(40f, layout_node14.Location.X);
        Assert.Equal(80f, layout_node14.Location.Y);
        var layout_node15 = taffy.GetLayout(node15);
        Assert.Equal(40f, layout_node15.Size.Width);
        Assert.Equal(20f, layout_node15.Size.Height);
        Assert.Equal(40f, layout_node15.Location.X);
        Assert.Equal(100f, layout_node15.Location.Y);
        var layout_node16 = taffy.GetLayout(node16);
        Assert.Equal(40f, layout_node16.Size.Width);
        Assert.Equal(20f, layout_node16.Size.Height);
        Assert.Equal(40f, layout_node16.Location.X);
        Assert.Equal(120f, layout_node16.Location.Y);
        var layout_node17 = taffy.GetLayout(node17);
        Assert.Equal(40f, layout_node17.Size.Width);
        Assert.Equal(20f, layout_node17.Size.Height);
        Assert.Equal(40f, layout_node17.Location.X);
        Assert.Equal(140f, layout_node17.Location.Y);
        var layout_node18 = taffy.GetLayout(node18);
        Assert.Equal(40f, layout_node18.Size.Width);
        Assert.Equal(20f, layout_node18.Size.Height);
        Assert.Equal(40f, layout_node18.Location.X);
        Assert.Equal(160f, layout_node18.Location.Y);
        var layout_node19 = taffy.GetLayout(node19);
        Assert.Equal(40f, layout_node19.Size.Width);
        Assert.Equal(20f, layout_node19.Size.Height);
        Assert.Equal(40f, layout_node19.Location.X);
        Assert.Equal(180f, layout_node19.Location.Y);
    }
}
