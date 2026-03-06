using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.GridFlex;

public class GridflexRowIntegration
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeafWithContext(new Style(), TestNodeContext.AhemText("HH", WritingMode.Horizontal));
        var node01 = taffy.NewLeafWithContext(new Style(), TestNodeContext.AhemText("HH", WritingMode.Horizontal));
        var node02 = taffy.NewLeafWithContext(new Style(), TestNodeContext.AhemText("HH", WritingMode.Horizontal));
        var node03 = taffy.NewLeafWithContext(new Style(), TestNodeContext.AhemText("HH", WritingMode.Horizontal));
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromFr(1f), GridTemplateComponent.FromFr(1f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromFr(1f), GridTemplateComponent.FromFr(1f)),
        }, new NodeId[] { node00, node01, node02, node03 });
        var node = taffy.NewWithChildren(new Style(), new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(40f, layout_node.Size.Width);
        Assert.Equal(20f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(40f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(20f, layout_node00.Size.Width);
        Assert.Equal(10f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(20f, layout_node01.Size.Width);
        Assert.Equal(10f, layout_node01.Size.Height);
        Assert.Equal(20f, layout_node01.Location.X);
        Assert.Equal(0f, layout_node01.Location.Y);
        var layout_node02 = taffy.GetLayout(node02);
        Assert.Equal(20f, layout_node02.Size.Width);
        Assert.Equal(10f, layout_node02.Size.Height);
        Assert.Equal(0f, layout_node02.Location.X);
        Assert.Equal(10f, layout_node02.Location.Y);
        var layout_node03 = taffy.GetLayout(node03);
        Assert.Equal(20f, layout_node03.Size.Width);
        Assert.Equal(10f, layout_node03.Size.Height);
        Assert.Equal(20f, layout_node03.Location.X);
        Assert.Equal(10f, layout_node03.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeafWithContext(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        }, TestNodeContext.AhemText("HH", WritingMode.Horizontal));
        var node01 = taffy.NewLeafWithContext(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        }, TestNodeContext.AhemText("HH", WritingMode.Horizontal));
        var node02 = taffy.NewLeafWithContext(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        }, TestNodeContext.AhemText("HH", WritingMode.Horizontal));
        var node03 = taffy.NewLeafWithContext(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        }, TestNodeContext.AhemText("HH", WritingMode.Horizontal));
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromFr(1f), GridTemplateComponent.FromFr(1f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromFr(1f), GridTemplateComponent.FromFr(1f)),
        }, new NodeId[] { node00, node01, node02, node03 });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(40f, layout_node.Size.Width);
        Assert.Equal(20f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(40f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(20f, layout_node00.Size.Width);
        Assert.Equal(10f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(20f, layout_node01.Size.Width);
        Assert.Equal(10f, layout_node01.Size.Height);
        Assert.Equal(20f, layout_node01.Location.X);
        Assert.Equal(0f, layout_node01.Location.Y);
        var layout_node02 = taffy.GetLayout(node02);
        Assert.Equal(20f, layout_node02.Size.Width);
        Assert.Equal(10f, layout_node02.Size.Height);
        Assert.Equal(0f, layout_node02.Location.X);
        Assert.Equal(10f, layout_node02.Location.Y);
        var layout_node03 = taffy.GetLayout(node03);
        Assert.Equal(20f, layout_node03.Size.Width);
        Assert.Equal(10f, layout_node03.Size.Height);
        Assert.Equal(20f, layout_node03.Location.X);
        Assert.Equal(10f, layout_node03.Location.Y);
    }
}
