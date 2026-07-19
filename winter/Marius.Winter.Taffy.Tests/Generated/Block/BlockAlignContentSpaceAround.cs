using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Block;

public class BlockAlignContentSpaceAround
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Ltr,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(20f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Ltr,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(20f)),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Ltr,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(20f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            DirectionValue = Direction.Ltr,
            AlignContentValue = AlignContent.SpaceAround,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(120f)),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(120f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(50f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(30f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(50f, layout_node1.Size.Width);
        Assert.Equal(20f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(50f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(50f, layout_node2.Size.Width);
        Assert.Equal(20f, layout_node2.Size.Height);
        Assert.Equal(0f, layout_node2.Location.X);
        Assert.Equal(70f, layout_node2.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(20f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(20f)),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(20f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
            AlignContentValue = AlignContent.SpaceAround,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(120f)),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(120f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(50f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(30f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(50f, layout_node1.Size.Width);
        Assert.Equal(20f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(50f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(50f, layout_node2.Size.Width);
        Assert.Equal(20f, layout_node2.Size.Height);
        Assert.Equal(0f, layout_node2.Location.X);
        Assert.Equal(70f, layout_node2.Location.Y);
    }

    [Fact]
    public void BorderBoxRtl()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Rtl,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(20f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Rtl,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(20f)),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Rtl,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(20f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            DirectionValue = Direction.Rtl,
            AlignContentValue = AlignContent.SpaceAround,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(120f)),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(120f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(50f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(50f, layout_node0.Location.X);
        Assert.Equal(30f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(50f, layout_node1.Size.Width);
        Assert.Equal(20f, layout_node1.Size.Height);
        Assert.Equal(50f, layout_node1.Location.X);
        Assert.Equal(50f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(50f, layout_node2.Size.Width);
        Assert.Equal(20f, layout_node2.Size.Height);
        Assert.Equal(50f, layout_node2.Location.X);
        Assert.Equal(70f, layout_node2.Location.Y);
    }

    [Fact]
    public void ContentBoxRtl()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(20f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(20f)),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(20f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
            AlignContentValue = AlignContent.SpaceAround,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(120f)),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(120f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(50f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(50f, layout_node0.Location.X);
        Assert.Equal(30f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(50f, layout_node1.Size.Width);
        Assert.Equal(20f, layout_node1.Size.Height);
        Assert.Equal(50f, layout_node1.Location.X);
        Assert.Equal(50f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(50f, layout_node2.Size.Width);
        Assert.Equal(20f, layout_node2.Size.Height);
        Assert.Equal(50f, layout_node2.Location.X);
        Assert.Equal(70f, layout_node2.Location.Y);
    }
}
