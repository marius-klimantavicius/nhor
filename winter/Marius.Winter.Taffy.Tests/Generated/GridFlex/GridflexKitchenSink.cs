using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.GridFlex;

public class GridflexKitchenSink
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node000 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.Auto()),
        });
        var node00100 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.Auto()),
        });
        var node00101 = taffy.NewLeaf(new Style());
        var node00102 = taffy.NewLeaf(new Style());
        var node00103 = taffy.NewLeaf(new Style());
        var node0010 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromPercent(0.3f), GridTemplateComponent.FromPercent(0.1f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.AutoComponent(), GridTemplateComponent.FromPercent(0.1f)),
        }, new NodeId[] { node00100, node00101, node00102, node00103 });
        var node001 = taffy.NewWithChildren(new Style
        {
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.Auto()),
        }, new NodeId[] { node0010 });
        var node00 = taffy.NewWithChildren(new Style(), new NodeId[] { node000, node001 });
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
        Assert.Equal(140f, layout_node.Size.Width);
        Assert.Equal(20f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(140f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(70f, layout_node00.Size.Width);
        Assert.Equal(10f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node000 = taffy.GetLayout(node000);
        Assert.Equal(20f, layout_node000.Size.Width);
        Assert.Equal(10f, layout_node000.Size.Height);
        Assert.Equal(0f, layout_node000.Location.X);
        Assert.Equal(0f, layout_node000.Location.Y);
        var layout_node001 = taffy.GetLayout(node001);
        Assert.Equal(50f, layout_node001.Size.Width);
        Assert.Equal(10f, layout_node001.Size.Height);
        Assert.Equal(20f, layout_node001.Location.X);
        Assert.Equal(0f, layout_node001.Location.Y);
        var layout_node0010 = taffy.GetLayout(node0010);
        Assert.Equal(20f, layout_node0010.Size.Width);
        Assert.Equal(10f, layout_node0010.Size.Height);
        Assert.Equal(0f, layout_node0010.Location.X);
        Assert.Equal(0f, layout_node0010.Location.Y);
        var layout_node00100 = taffy.GetLayout(node00100);
        Assert.Equal(20f, layout_node00100.Size.Width);
        Assert.Equal(3f, layout_node00100.Size.Height);
        Assert.Equal(0f, layout_node00100.Location.X);
        Assert.Equal(0f, layout_node00100.Location.Y);
        var layout_node00101 = taffy.GetLayout(node00101);
        Assert.Equal(2f, layout_node00101.Size.Width);
        Assert.Equal(3f, layout_node00101.Size.Height);
        Assert.Equal(20f, layout_node00101.Location.X);
        Assert.Equal(0f, layout_node00101.Location.Y);
        var layout_node00102 = taffy.GetLayout(node00102);
        Assert.Equal(20f, layout_node00102.Size.Width);
        Assert.Equal(1f, layout_node00102.Size.Height);
        Assert.Equal(0f, layout_node00102.Location.X);
        Assert.Equal(3f, layout_node00102.Location.Y);
        var layout_node00103 = taffy.GetLayout(node00103);
        Assert.Equal(2f, layout_node00103.Size.Width);
        Assert.Equal(1f, layout_node00103.Size.Height);
        Assert.Equal(20f, layout_node00103.Location.X);
        Assert.Equal(3f, layout_node00103.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(70f, layout_node01.Size.Width);
        Assert.Equal(10f, layout_node01.Size.Height);
        Assert.Equal(70f, layout_node01.Location.X);
        Assert.Equal(0f, layout_node01.Location.Y);
        var layout_node02 = taffy.GetLayout(node02);
        Assert.Equal(70f, layout_node02.Size.Width);
        Assert.Equal(10f, layout_node02.Size.Height);
        Assert.Equal(0f, layout_node02.Location.X);
        Assert.Equal(10f, layout_node02.Location.Y);
        var layout_node03 = taffy.GetLayout(node03);
        Assert.Equal(70f, layout_node03.Size.Width);
        Assert.Equal(10f, layout_node03.Size.Height);
        Assert.Equal(70f, layout_node03.Location.X);
        Assert.Equal(10f, layout_node03.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node000 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.Auto()),
        });
        var node00100 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.Auto()),
        });
        var node00101 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node00102 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node00103 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node0010 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromPercent(0.3f), GridTemplateComponent.FromPercent(0.1f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.AutoComponent(), GridTemplateComponent.FromPercent(0.1f)),
        }, new NodeId[] { node00100, node00101, node00102, node00103 });
        var node001 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.Auto()),
        }, new NodeId[] { node0010 });
        var node00 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        }, new NodeId[] { node000, node001 });
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
        Assert.Equal(140f, layout_node.Size.Width);
        Assert.Equal(20f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(140f, layout_node0.Size.Width);
        Assert.Equal(20f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(70f, layout_node00.Size.Width);
        Assert.Equal(10f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node000 = taffy.GetLayout(node000);
        Assert.Equal(20f, layout_node000.Size.Width);
        Assert.Equal(10f, layout_node000.Size.Height);
        Assert.Equal(0f, layout_node000.Location.X);
        Assert.Equal(0f, layout_node000.Location.Y);
        var layout_node001 = taffy.GetLayout(node001);
        Assert.Equal(50f, layout_node001.Size.Width);
        Assert.Equal(10f, layout_node001.Size.Height);
        Assert.Equal(20f, layout_node001.Location.X);
        Assert.Equal(0f, layout_node001.Location.Y);
        var layout_node0010 = taffy.GetLayout(node0010);
        Assert.Equal(20f, layout_node0010.Size.Width);
        Assert.Equal(10f, layout_node0010.Size.Height);
        Assert.Equal(0f, layout_node0010.Location.X);
        Assert.Equal(0f, layout_node0010.Location.Y);
        var layout_node00100 = taffy.GetLayout(node00100);
        Assert.Equal(20f, layout_node00100.Size.Width);
        Assert.Equal(3f, layout_node00100.Size.Height);
        Assert.Equal(0f, layout_node00100.Location.X);
        Assert.Equal(0f, layout_node00100.Location.Y);
        var layout_node00101 = taffy.GetLayout(node00101);
        Assert.Equal(2f, layout_node00101.Size.Width);
        Assert.Equal(3f, layout_node00101.Size.Height);
        Assert.Equal(20f, layout_node00101.Location.X);
        Assert.Equal(0f, layout_node00101.Location.Y);
        var layout_node00102 = taffy.GetLayout(node00102);
        Assert.Equal(20f, layout_node00102.Size.Width);
        Assert.Equal(1f, layout_node00102.Size.Height);
        Assert.Equal(0f, layout_node00102.Location.X);
        Assert.Equal(3f, layout_node00102.Location.Y);
        var layout_node00103 = taffy.GetLayout(node00103);
        Assert.Equal(2f, layout_node00103.Size.Width);
        Assert.Equal(1f, layout_node00103.Size.Height);
        Assert.Equal(20f, layout_node00103.Location.X);
        Assert.Equal(3f, layout_node00103.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(70f, layout_node01.Size.Width);
        Assert.Equal(10f, layout_node01.Size.Height);
        Assert.Equal(70f, layout_node01.Location.X);
        Assert.Equal(0f, layout_node01.Location.Y);
        var layout_node02 = taffy.GetLayout(node02);
        Assert.Equal(70f, layout_node02.Size.Width);
        Assert.Equal(10f, layout_node02.Size.Height);
        Assert.Equal(0f, layout_node02.Location.X);
        Assert.Equal(10f, layout_node02.Location.Y);
        var layout_node03 = taffy.GetLayout(node03);
        Assert.Equal(70f, layout_node03.Size.Width);
        Assert.Equal(10f, layout_node03.Size.Height);
        Assert.Equal(70f, layout_node03.Location.X);
        Assert.Equal(10f, layout_node03.Location.Y);
    }
}
