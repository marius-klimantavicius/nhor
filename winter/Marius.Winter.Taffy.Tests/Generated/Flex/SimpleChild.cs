using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class SimpleChild
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node000 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(10f)),
        });
        var node00 = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(10f)),
        }, new NodeId[] { node000 });
        var node010 = taffy.NewLeaf(new Style
        {
            AlignSelfValue = AlignItems.Center,
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(10f)),
        });
        var node011 = taffy.NewLeaf(new Style
        {
            AlignSelfValue = AlignItems.Center,
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(10f)),
        });
        var node01 = taffy.NewWithChildren(new Style(), new NodeId[] { node010, node011 });
        var node0 = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(1f)),
        }, new NodeId[] { node00, node01 });
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(100f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(10f, layout_node00.Size.Width);
        Assert.Equal(10f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node000 = taffy.GetLayout(node000);
        Assert.Equal(10f, layout_node000.Size.Width);
        Assert.Equal(10f, layout_node000.Size.Height);
        Assert.Equal(0f, layout_node000.Location.X);
        Assert.Equal(0f, layout_node000.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(20f, layout_node01.Size.Width);
        Assert.Equal(100f, layout_node01.Size.Height);
        Assert.Equal(10f, layout_node01.Location.X);
        Assert.Equal(0f, layout_node01.Location.Y);
        var layout_node010 = taffy.GetLayout(node010);
        Assert.Equal(10f, layout_node010.Size.Width);
        Assert.Equal(10f, layout_node010.Size.Height);
        Assert.Equal(0f, layout_node010.Location.X);
        Assert.Equal(45f, layout_node010.Location.Y);
        var layout_node011 = taffy.GetLayout(node011);
        Assert.Equal(10f, layout_node011.Size.Width);
        Assert.Equal(10f, layout_node011.Size.Height);
        Assert.Equal(10f, layout_node011.Location.X);
        Assert.Equal(45f, layout_node011.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node000 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(10f)),
        });
        var node00 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(10f)),
        }, new NodeId[] { node000 });
        var node010 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignSelfValue = AlignItems.Center,
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(10f)),
        });
        var node011 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignSelfValue = AlignItems.Center,
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(10f)),
        });
        var node01 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        }, new NodeId[] { node010, node011 });
        var node0 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(1f)),
        }, new NodeId[] { node00, node01 });
        var node = taffy.NewWithChildren(new Style
        {
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
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(100f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(10f, layout_node00.Size.Width);
        Assert.Equal(10f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node000 = taffy.GetLayout(node000);
        Assert.Equal(10f, layout_node000.Size.Width);
        Assert.Equal(10f, layout_node000.Size.Height);
        Assert.Equal(0f, layout_node000.Location.X);
        Assert.Equal(0f, layout_node000.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(20f, layout_node01.Size.Width);
        Assert.Equal(100f, layout_node01.Size.Height);
        Assert.Equal(10f, layout_node01.Location.X);
        Assert.Equal(0f, layout_node01.Location.Y);
        var layout_node010 = taffy.GetLayout(node010);
        Assert.Equal(10f, layout_node010.Size.Width);
        Assert.Equal(10f, layout_node010.Size.Height);
        Assert.Equal(0f, layout_node010.Location.X);
        Assert.Equal(45f, layout_node010.Location.Y);
        var layout_node011 = taffy.GetLayout(node011);
        Assert.Equal(10f, layout_node011.Size.Width);
        Assert.Equal(10f, layout_node011.Size.Height);
        Assert.Equal(10f, layout_node011.Location.X);
        Assert.Equal(45f, layout_node011.Location.Y);
    }
}
