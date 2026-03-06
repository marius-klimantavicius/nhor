using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.GridFlex;

public class GridflexKitchenSinkMinimise2
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.AutoComponent()),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.AutoComponent()),
        }, new NodeId[] { node00 });
        var node = taffy.NewWithChildren(new Style
        {
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.Auto()),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(50f, layout_node.Size.Width);
        Assert.Equal(20f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(20f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(20f, layout_node00.Size.Width);
        Assert.Equal(20f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.AutoComponent()),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.AutoComponent()),
        }, new NodeId[] { node00 });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.Auto()),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(50f, layout_node.Size.Width);
        Assert.Equal(20f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(20f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(20f, layout_node00.Size.Width);
        Assert.Equal(20f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
    }
}
