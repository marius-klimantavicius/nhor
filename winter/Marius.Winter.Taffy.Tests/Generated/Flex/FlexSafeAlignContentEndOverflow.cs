using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class FlexSafeAlignContentEndOverflow
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Ltr,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.FromLength(80f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Ltr,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.FromLength(80f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            DirectionValue = Direction.Ltr,
            FlexWrapValue = FlexWrap.Wrap,
            AlignContentValue = AlignContent.SafeEnd,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(60f, layout_node0.Size.Width);
        Assert.Equal(80f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(60f, layout_node1.Size.Width);
        Assert.Equal(80f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(80f, layout_node1.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.FromLength(80f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.FromLength(80f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
            FlexWrapValue = FlexWrap.Wrap,
            AlignContentValue = AlignContent.SafeEnd,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(60f, layout_node0.Size.Width);
        Assert.Equal(80f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(60f, layout_node1.Size.Width);
        Assert.Equal(80f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(80f, layout_node1.Location.Y);
    }

    [Fact]
    public void BorderBoxRtl()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Rtl,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.FromLength(80f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Rtl,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.FromLength(80f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            DirectionValue = Direction.Rtl,
            FlexWrapValue = FlexWrap.Wrap,
            AlignContentValue = AlignContent.SafeEnd,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(60f, layout_node0.Size.Width);
        Assert.Equal(80f, layout_node0.Size.Height);
        Assert.Equal(40f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(60f, layout_node1.Size.Width);
        Assert.Equal(80f, layout_node1.Size.Height);
        Assert.Equal(40f, layout_node1.Location.X);
        Assert.Equal(80f, layout_node1.Location.Y);
    }

    [Fact]
    public void ContentBoxRtl()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.FromLength(80f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.FromLength(80f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
            FlexWrapValue = FlexWrap.Wrap,
            AlignContentValue = AlignContent.SafeEnd,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(60f, layout_node0.Size.Width);
        Assert.Equal(80f, layout_node0.Size.Height);
        Assert.Equal(40f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(60f, layout_node1.Size.Width);
        Assert.Equal(80f, layout_node1.Size.Height);
        Assert.Equal(40f, layout_node1.Location.X);
        Assert.Equal(80f, layout_node1.Location.Y);
    }
}
