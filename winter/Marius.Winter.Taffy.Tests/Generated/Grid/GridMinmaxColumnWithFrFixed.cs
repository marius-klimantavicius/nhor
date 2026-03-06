using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridMinmaxColumnWithFrFixed
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style());
        var node1 = taffy.NewLeaf(new Style());
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(40f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromSingle(new MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>(MinTrackSizingFunction.Length(20f), MaxTrackSizingFunction.Length(40f))), GridTemplateComponent.FromFr(1f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.Auto()),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(60f, layout_node.Size.Width);
        Assert.Equal(40f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(40f, layout_node0.Size.Width);
        Assert.Equal(40f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(20f, layout_node1.Size.Width);
        Assert.Equal(40f, layout_node1.Size.Height);
        Assert.Equal(40f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(40f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromSingle(new MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>(MinTrackSizingFunction.Length(20f), MaxTrackSizingFunction.Length(40f))), GridTemplateComponent.FromFr(1f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.Auto()),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(60f, layout_node.Size.Width);
        Assert.Equal(40f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(40f, layout_node0.Size.Width);
        Assert.Equal(40f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(20f, layout_node1.Size.Width);
        Assert.Equal(40f, layout_node1.Size.Height);
        Assert.Equal(40f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
    }
}
