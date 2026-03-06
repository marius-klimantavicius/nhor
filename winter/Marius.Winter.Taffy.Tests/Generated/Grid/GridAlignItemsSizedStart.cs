using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridAlignItemsSizedStart
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            GridRow = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.Auto),
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.Auto),
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            GridRow = new Line<GridPlacement>(GridPlacement.FromLine((short)3), GridPlacement.Auto),
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)3), GridPlacement.Auto),
            SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.FromLength(60f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            AlignItemsValue = AlignItems.Start,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(120f), Dimension.FromLength(120f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(120f, layout_node.Size.Width);
        Assert.Equal(120f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(20f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(60f, layout_node1.Size.Width);
        Assert.Equal(60f, layout_node1.Size.Height);
        Assert.Equal(80f, layout_node1.Location.X);
        Assert.Equal(80f, layout_node1.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            GridRow = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.Auto),
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)1), GridPlacement.Auto),
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            GridRow = new Line<GridPlacement>(GridPlacement.FromLine((short)3), GridPlacement.Auto),
            GridColumn = new Line<GridPlacement>(GridPlacement.FromLine((short)3), GridPlacement.Auto),
            SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.FromLength(60f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            AlignItemsValue = AlignItems.Start,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(120f), Dimension.FromLength(120f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(120f, layout_node.Size.Width);
        Assert.Equal(120f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(20f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(60f, layout_node1.Size.Width);
        Assert.Equal(60f, layout_node1.Size.Height);
        Assert.Equal(80f, layout_node1.Location.X);
        Assert.Equal(80f, layout_node1.Location.Y);
    }
}
