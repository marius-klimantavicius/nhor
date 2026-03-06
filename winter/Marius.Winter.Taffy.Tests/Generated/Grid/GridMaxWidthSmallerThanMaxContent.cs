using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridMaxWidthSmallerThanMaxContent
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeafWithContext(new Style(), TestNodeContext.AhemText("HH\u200bHH\u200bHH\u200bHH", WritingMode.Horizontal));
        var node01 = taffy.NewLeafWithContext(new Style(), TestNodeContext.AhemText("HH\u200bHH\u200bHH\u200bHH", WritingMode.Horizontal));
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.AutoComponent(), GridTemplateComponent.AutoComponent()),
            MaxSizeValue = new Size<Dimension>(Dimension.FromLength(80f), Dimension.Auto()),
        }, new NodeId[] { node00, node01 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.MaxContentComponent()),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(80f, layout_node.Size.Width);
        Assert.Equal(20f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(80f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(40f, layout_node00.Size.Width);
        Assert.Equal(20f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(40f, layout_node01.Size.Width);
        Assert.Equal(20f, layout_node01.Size.Height);
        Assert.Equal(40f, layout_node01.Location.X);
        Assert.Equal(0f, layout_node01.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeafWithContext(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        }, TestNodeContext.AhemText("HH\u200bHH\u200bHH\u200bHH", WritingMode.Horizontal));
        var node01 = taffy.NewLeafWithContext(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        }, TestNodeContext.AhemText("HH\u200bHH\u200bHH\u200bHH", WritingMode.Horizontal));
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.AutoComponent(), GridTemplateComponent.AutoComponent()),
            MaxSizeValue = new Size<Dimension>(Dimension.FromLength(80f), Dimension.Auto()),
        }, new NodeId[] { node00, node01 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.MaxContentComponent()),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(80f, layout_node.Size.Width);
        Assert.Equal(20f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(80f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(40f, layout_node00.Size.Width);
        Assert.Equal(20f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(40f, layout_node01.Size.Width);
        Assert.Equal(20f, layout_node01.Size.Height);
        Assert.Equal(40f, layout_node01.Location.X);
        Assert.Equal(0f, layout_node01.Location.Y);
    }
}
