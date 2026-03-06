using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class PercentageContainerInWrappingContainer
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node000 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
        });
        var node001 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
        });
        var node00 = taffy.NewWithChildren(new Style
        {
            JustifyContentValue = AlignContent.Center,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.Auto()),
        }, new NodeId[] { node000, node001 });
        var node0 = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
        }, new NodeId[] { node00 });
        var node = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            AlignItemsValue = AlignItems.Center,
            JustifyContentValue = AlignContent.Center,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(200f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(50f, layout_node0.Size.Height);
        Assert.Equal(50f, layout_node0.Location.X);
        Assert.Equal(75f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(100f, layout_node00.Size.Width);
        Assert.Equal(50f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node000 = taffy.GetLayout(node000);
        Assert.Equal(50f, layout_node000.Size.Width);
        Assert.Equal(50f, layout_node000.Size.Height);
        Assert.Equal(0f, layout_node000.Location.X);
        Assert.Equal(0f, layout_node000.Location.Y);
        var layout_node001 = taffy.GetLayout(node001);
        Assert.Equal(50f, layout_node001.Size.Width);
        Assert.Equal(50f, layout_node001.Size.Height);
        Assert.Equal(50f, layout_node001.Location.X);
        Assert.Equal(0f, layout_node001.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node000 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
        });
        var node001 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
        });
        var node00 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            JustifyContentValue = AlignContent.Center,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.Auto()),
        }, new NodeId[] { node000, node001 });
        var node0 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
        }, new NodeId[] { node00 });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            AlignItemsValue = AlignItems.Center,
            JustifyContentValue = AlignContent.Center,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(200f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(50f, layout_node0.Size.Height);
        Assert.Equal(50f, layout_node0.Location.X);
        Assert.Equal(75f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(100f, layout_node00.Size.Width);
        Assert.Equal(50f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node000 = taffy.GetLayout(node000);
        Assert.Equal(50f, layout_node000.Size.Width);
        Assert.Equal(50f, layout_node000.Size.Height);
        Assert.Equal(0f, layout_node000.Location.X);
        Assert.Equal(0f, layout_node000.Location.Y);
        var layout_node001 = taffy.GetLayout(node001);
        Assert.Equal(50f, layout_node001.Size.Width);
        Assert.Equal(50f, layout_node001.Size.Height);
        Assert.Equal(50f, layout_node001.Location.X);
        Assert.Equal(0f, layout_node001.Location.Y);
    }
}
