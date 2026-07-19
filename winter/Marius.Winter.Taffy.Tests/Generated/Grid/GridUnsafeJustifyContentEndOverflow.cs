using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridUnsafeJustifyContentEndOverflow
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Ltr,
        });
        var node1 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Ltr,
        });
        var node2 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Ltr,
        });
        var node3 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Ltr,
        });
        var node4 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Ltr,
        });
        var node5 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Ltr,
        });
        var node6 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Ltr,
        });
        var node7 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Ltr,
        });
        var node8 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Ltr,
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            DirectionValue = Direction.Ltr,
            JustifyContentValue = AlignContent.End,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0, node1, node2, node3, node4, node5, node6, node7, node8 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(40f, layout_node0.Size.Width);
        Assert.Equal(40f, layout_node0.Size.Height);
        Assert.Equal(-20f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(40f, layout_node1.Size.Width);
        Assert.Equal(40f, layout_node1.Size.Height);
        Assert.Equal(20f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(40f, layout_node2.Size.Width);
        Assert.Equal(40f, layout_node2.Size.Height);
        Assert.Equal(60f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(40f, layout_node3.Size.Width);
        Assert.Equal(40f, layout_node3.Size.Height);
        Assert.Equal(-20f, layout_node3.Location.X);
        Assert.Equal(40f, layout_node3.Location.Y);
        var layout_node4 = taffy.GetLayout(node4);
        Assert.Equal(40f, layout_node4.Size.Width);
        Assert.Equal(40f, layout_node4.Size.Height);
        Assert.Equal(20f, layout_node4.Location.X);
        Assert.Equal(40f, layout_node4.Location.Y);
        var layout_node5 = taffy.GetLayout(node5);
        Assert.Equal(40f, layout_node5.Size.Width);
        Assert.Equal(40f, layout_node5.Size.Height);
        Assert.Equal(60f, layout_node5.Location.X);
        Assert.Equal(40f, layout_node5.Location.Y);
        var layout_node6 = taffy.GetLayout(node6);
        Assert.Equal(40f, layout_node6.Size.Width);
        Assert.Equal(40f, layout_node6.Size.Height);
        Assert.Equal(-20f, layout_node6.Location.X);
        Assert.Equal(80f, layout_node6.Location.Y);
        var layout_node7 = taffy.GetLayout(node7);
        Assert.Equal(40f, layout_node7.Size.Width);
        Assert.Equal(40f, layout_node7.Size.Height);
        Assert.Equal(20f, layout_node7.Location.X);
        Assert.Equal(80f, layout_node7.Location.Y);
        var layout_node8 = taffy.GetLayout(node8);
        Assert.Equal(40f, layout_node8.Size.Width);
        Assert.Equal(40f, layout_node8.Size.Height);
        Assert.Equal(60f, layout_node8.Location.X);
        Assert.Equal(80f, layout_node8.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
        });
        var node2 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
        });
        var node3 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
        });
        var node4 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
        });
        var node5 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
        });
        var node6 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
        });
        var node7 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
        });
        var node8 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
            JustifyContentValue = AlignContent.End,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0, node1, node2, node3, node4, node5, node6, node7, node8 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(40f, layout_node0.Size.Width);
        Assert.Equal(40f, layout_node0.Size.Height);
        Assert.Equal(-20f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(40f, layout_node1.Size.Width);
        Assert.Equal(40f, layout_node1.Size.Height);
        Assert.Equal(20f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(40f, layout_node2.Size.Width);
        Assert.Equal(40f, layout_node2.Size.Height);
        Assert.Equal(60f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(40f, layout_node3.Size.Width);
        Assert.Equal(40f, layout_node3.Size.Height);
        Assert.Equal(-20f, layout_node3.Location.X);
        Assert.Equal(40f, layout_node3.Location.Y);
        var layout_node4 = taffy.GetLayout(node4);
        Assert.Equal(40f, layout_node4.Size.Width);
        Assert.Equal(40f, layout_node4.Size.Height);
        Assert.Equal(20f, layout_node4.Location.X);
        Assert.Equal(40f, layout_node4.Location.Y);
        var layout_node5 = taffy.GetLayout(node5);
        Assert.Equal(40f, layout_node5.Size.Width);
        Assert.Equal(40f, layout_node5.Size.Height);
        Assert.Equal(60f, layout_node5.Location.X);
        Assert.Equal(40f, layout_node5.Location.Y);
        var layout_node6 = taffy.GetLayout(node6);
        Assert.Equal(40f, layout_node6.Size.Width);
        Assert.Equal(40f, layout_node6.Size.Height);
        Assert.Equal(-20f, layout_node6.Location.X);
        Assert.Equal(80f, layout_node6.Location.Y);
        var layout_node7 = taffy.GetLayout(node7);
        Assert.Equal(40f, layout_node7.Size.Width);
        Assert.Equal(40f, layout_node7.Size.Height);
        Assert.Equal(20f, layout_node7.Location.X);
        Assert.Equal(80f, layout_node7.Location.Y);
        var layout_node8 = taffy.GetLayout(node8);
        Assert.Equal(40f, layout_node8.Size.Width);
        Assert.Equal(40f, layout_node8.Size.Height);
        Assert.Equal(60f, layout_node8.Location.X);
        Assert.Equal(80f, layout_node8.Location.Y);
    }

    [Fact]
    public void BorderBoxRtl()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Rtl,
        });
        var node1 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Rtl,
        });
        var node2 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Rtl,
        });
        var node3 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Rtl,
        });
        var node4 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Rtl,
        });
        var node5 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Rtl,
        });
        var node6 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Rtl,
        });
        var node7 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Rtl,
        });
        var node8 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Rtl,
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            DirectionValue = Direction.Rtl,
            JustifyContentValue = AlignContent.End,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0, node1, node2, node3, node4, node5, node6, node7, node8 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(40f, layout_node0.Size.Width);
        Assert.Equal(40f, layout_node0.Size.Height);
        Assert.Equal(80f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(40f, layout_node1.Size.Width);
        Assert.Equal(40f, layout_node1.Size.Height);
        Assert.Equal(40f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(40f, layout_node2.Size.Width);
        Assert.Equal(40f, layout_node2.Size.Height);
        Assert.Equal(0f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(40f, layout_node3.Size.Width);
        Assert.Equal(40f, layout_node3.Size.Height);
        Assert.Equal(80f, layout_node3.Location.X);
        Assert.Equal(40f, layout_node3.Location.Y);
        var layout_node4 = taffy.GetLayout(node4);
        Assert.Equal(40f, layout_node4.Size.Width);
        Assert.Equal(40f, layout_node4.Size.Height);
        Assert.Equal(40f, layout_node4.Location.X);
        Assert.Equal(40f, layout_node4.Location.Y);
        var layout_node5 = taffy.GetLayout(node5);
        Assert.Equal(40f, layout_node5.Size.Width);
        Assert.Equal(40f, layout_node5.Size.Height);
        Assert.Equal(0f, layout_node5.Location.X);
        Assert.Equal(40f, layout_node5.Location.Y);
        var layout_node6 = taffy.GetLayout(node6);
        Assert.Equal(40f, layout_node6.Size.Width);
        Assert.Equal(40f, layout_node6.Size.Height);
        Assert.Equal(80f, layout_node6.Location.X);
        Assert.Equal(80f, layout_node6.Location.Y);
        var layout_node7 = taffy.GetLayout(node7);
        Assert.Equal(40f, layout_node7.Size.Width);
        Assert.Equal(40f, layout_node7.Size.Height);
        Assert.Equal(40f, layout_node7.Location.X);
        Assert.Equal(80f, layout_node7.Location.Y);
        var layout_node8 = taffy.GetLayout(node8);
        Assert.Equal(40f, layout_node8.Size.Width);
        Assert.Equal(40f, layout_node8.Size.Height);
        Assert.Equal(0f, layout_node8.Location.X);
        Assert.Equal(80f, layout_node8.Location.Y);
    }

    [Fact]
    public void ContentBoxRtl()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
        });
        var node2 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
        });
        var node3 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
        });
        var node4 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
        });
        var node5 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
        });
        var node6 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
        });
        var node7 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
        });
        var node8 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
            JustifyContentValue = AlignContent.End,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0, node1, node2, node3, node4, node5, node6, node7, node8 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(40f, layout_node0.Size.Width);
        Assert.Equal(40f, layout_node0.Size.Height);
        Assert.Equal(80f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(40f, layout_node1.Size.Width);
        Assert.Equal(40f, layout_node1.Size.Height);
        Assert.Equal(40f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(40f, layout_node2.Size.Width);
        Assert.Equal(40f, layout_node2.Size.Height);
        Assert.Equal(0f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(40f, layout_node3.Size.Width);
        Assert.Equal(40f, layout_node3.Size.Height);
        Assert.Equal(80f, layout_node3.Location.X);
        Assert.Equal(40f, layout_node3.Location.Y);
        var layout_node4 = taffy.GetLayout(node4);
        Assert.Equal(40f, layout_node4.Size.Width);
        Assert.Equal(40f, layout_node4.Size.Height);
        Assert.Equal(40f, layout_node4.Location.X);
        Assert.Equal(40f, layout_node4.Location.Y);
        var layout_node5 = taffy.GetLayout(node5);
        Assert.Equal(40f, layout_node5.Size.Width);
        Assert.Equal(40f, layout_node5.Size.Height);
        Assert.Equal(0f, layout_node5.Location.X);
        Assert.Equal(40f, layout_node5.Location.Y);
        var layout_node6 = taffy.GetLayout(node6);
        Assert.Equal(40f, layout_node6.Size.Width);
        Assert.Equal(40f, layout_node6.Size.Height);
        Assert.Equal(80f, layout_node6.Location.X);
        Assert.Equal(80f, layout_node6.Location.Y);
        var layout_node7 = taffy.GetLayout(node7);
        Assert.Equal(40f, layout_node7.Size.Width);
        Assert.Equal(40f, layout_node7.Size.Height);
        Assert.Equal(40f, layout_node7.Location.X);
        Assert.Equal(80f, layout_node7.Location.Y);
        var layout_node8 = taffy.GetLayout(node8);
        Assert.Equal(40f, layout_node8.Size.Width);
        Assert.Equal(40f, layout_node8.Size.Height);
        Assert.Equal(0f, layout_node8.Location.X);
        Assert.Equal(80f, layout_node8.Location.Y);
    }
}
