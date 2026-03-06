using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridAbsoluteColumnEnd
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            PositionValue = Position.Absolute,
            GridColumn = new Line<GridPlacement>(GridPlacement.Auto, GridPlacement.FromLine((short)1)),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(4f), LengthPercentageAuto.Length(3f), LengthPercentageAuto.Length(1f), LengthPercentageAuto.Length(2f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(40f), LengthPercentage.Length(20f), LengthPercentage.Length(10f), LengthPercentage.Length(30f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(180f, layout_node.Size.Width);
        Assert.Equal(160f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(33f, layout_node0.Size.Width);
        Assert.Equal(157f, layout_node0.Size.Height);
        Assert.Equal(4f, layout_node0.Location.X);
        Assert.Equal(1f, layout_node0.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            PositionValue = Position.Absolute,
            GridColumn = new Line<GridPlacement>(GridPlacement.Auto, GridPlacement.FromLine((short)1)),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(4f), LengthPercentageAuto.Length(3f), LengthPercentageAuto.Length(1f), LengthPercentageAuto.Length(2f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(40f), LengthPercentage.Length(20f), LengthPercentage.Length(10f), LengthPercentage.Length(30f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(180f, layout_node.Size.Width);
        Assert.Equal(160f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(33f, layout_node0.Size.Width);
        Assert.Equal(157f, layout_node0.Size.Height);
        Assert.Equal(4f, layout_node0.Location.X);
        Assert.Equal(1f, layout_node0.Location.Y);
    }
}
