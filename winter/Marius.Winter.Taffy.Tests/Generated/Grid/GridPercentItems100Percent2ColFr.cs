using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridPercentItems100Percent2ColFr
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Ltr,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromLength(50f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Ltr,
            GridRow = new Line<GridPlacement>(GridPlacement.FromLine((short)2), GridPlacement.Auto),
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)2), GridPlacement.Auto),
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromLength(50f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            DirectionValue = Direction.Ltr,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(50f), GridTemplateComponent.FromLength(50f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.AutoComponent(), GridTemplateComponent.AutoComponent()),
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.Auto()),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(50f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(100f, layout_node1.Size.Width);
        Assert.Equal(50f, layout_node1.Size.Height);
        Assert.Equal(100f, layout_node1.Location.X);
        Assert.Equal(50f, layout_node1.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromLength(50f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
            GridRow = new Line<GridPlacement>(GridPlacement.FromLine((short)2), GridPlacement.Auto),
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)2), GridPlacement.Auto),
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromLength(50f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(50f), GridTemplateComponent.FromLength(50f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.AutoComponent(), GridTemplateComponent.AutoComponent()),
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.Auto()),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(50f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(100f, layout_node1.Size.Width);
        Assert.Equal(50f, layout_node1.Size.Height);
        Assert.Equal(100f, layout_node1.Location.X);
        Assert.Equal(50f, layout_node1.Location.Y);
    }

    [Fact]
    public void BorderBoxRtl()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Rtl,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromLength(50f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Rtl,
            GridRow = new Line<GridPlacement>(GridPlacement.FromLine((short)2), GridPlacement.Auto),
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)2), GridPlacement.Auto),
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromLength(50f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            DirectionValue = Direction.Rtl,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(50f), GridTemplateComponent.FromLength(50f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.AutoComponent(), GridTemplateComponent.AutoComponent()),
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.Auto()),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(50f, layout_node0.Size.Height);
        Assert.Equal(100f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(100f, layout_node1.Size.Width);
        Assert.Equal(50f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(50f, layout_node1.Location.Y);
    }

    [Fact]
    public void ContentBoxRtl()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromLength(50f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
            GridRow = new Line<GridPlacement>(GridPlacement.FromLine((short)2), GridPlacement.Auto),
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)2), GridPlacement.Auto),
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromLength(50f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(50f), GridTemplateComponent.FromLength(50f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.AutoComponent(), GridTemplateComponent.AutoComponent()),
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.Auto()),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(50f, layout_node0.Size.Height);
        Assert.Equal(100f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(100f, layout_node1.Size.Width);
        Assert.Equal(50f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(50f, layout_node1.Location.Y);
    }
}
