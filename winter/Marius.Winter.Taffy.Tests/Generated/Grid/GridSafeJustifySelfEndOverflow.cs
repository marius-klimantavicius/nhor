using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridSafeJustifySelfEndOverflow
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Ltr,
            JustifySelfValue = AlignItems.SafeEnd,
            SizeValue = new Size<Dimension>(Dimension.FromLength(150f), Dimension.FromLength(50f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            DirectionValue = Direction.Ltr,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(100f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(100f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(150f, layout_node0.Size.Width);
        Assert.Equal(50f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
            JustifySelfValue = AlignItems.SafeEnd,
            SizeValue = new Size<Dimension>(Dimension.FromLength(150f), Dimension.FromLength(50f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(100f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(100f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(150f, layout_node0.Size.Width);
        Assert.Equal(50f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
    }

    [Fact]
    public void BorderBoxRtl()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Rtl,
            JustifySelfValue = AlignItems.SafeEnd,
            SizeValue = new Size<Dimension>(Dimension.FromLength(150f), Dimension.FromLength(50f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            DirectionValue = Direction.Rtl,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(100f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(100f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(150f, layout_node0.Size.Width);
        Assert.Equal(50f, layout_node0.Size.Height);
        Assert.Equal(-50f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
    }

    [Fact]
    public void ContentBoxRtl()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
            JustifySelfValue = AlignItems.SafeEnd,
            SizeValue = new Size<Dimension>(Dimension.FromLength(150f), Dimension.FromLength(50f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(100f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(100f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(150f, layout_node0.Size.Width);
        Assert.Equal(50f, layout_node0.Size.Height);
        Assert.Equal(-50f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
    }
}
