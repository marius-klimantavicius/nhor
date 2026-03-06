using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridAlignItemsBaselineChildMultilineOverride
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            Display = Display.Grid,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(60f)),
        });
        var node10 = taffy.NewLeaf(new Style
        {
            Display = Display.Grid,
            SizeValue = new Size<Dimension>(Dimension.FromLength(25f), Dimension.FromLength(20f)),
        });
        var node11 = taffy.NewLeaf(new Style
        {
            Display = Display.Grid,
            AlignSelfValue = AlignItems.Baseline,
            SizeValue = new Size<Dimension>(Dimension.FromLength(25f), Dimension.FromLength(10f)),
        });
        var node12 = taffy.NewLeaf(new Style
        {
            Display = Display.Grid,
            SizeValue = new Size<Dimension>(Dimension.FromLength(25f), Dimension.FromLength(20f)),
        });
        var node13 = taffy.NewLeaf(new Style
        {
            Display = Display.Grid,
            AlignSelfValue = AlignItems.Baseline,
            SizeValue = new Size<Dimension>(Dimension.FromLength(25f), Dimension.FromLength(10f)),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.AutoComponent(), GridTemplateComponent.AutoComponent()),
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(25f)),
        }, new NodeId[] { node10, node11, node12, node13 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            AlignItemsValue = AlignItems.Baseline,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(50f, layout_node0.Size.Width);
        Assert.Equal(60f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(50f, layout_node1.Size.Width);
        Assert.Equal(25f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(68f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(25f, layout_node10.Size.Width);
        Assert.Equal(20f, layout_node10.Size.Height);
        Assert.Equal(0f, layout_node10.Location.X);
        Assert.Equal(0f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(25f, layout_node11.Size.Width);
        Assert.Equal(10f, layout_node11.Size.Height);
        Assert.Equal(25f, layout_node11.Location.X);
        Assert.Equal(0f, layout_node11.Location.Y);
        var layout_node12 = taffy.GetLayout(node12);
        Assert.Equal(25f, layout_node12.Size.Width);
        Assert.Equal(20f, layout_node12.Size.Height);
        Assert.Equal(0f, layout_node12.Location.X);
        Assert.Equal(20f, layout_node12.Location.Y);
        var layout_node13 = taffy.GetLayout(node13);
        Assert.Equal(25f, layout_node13.Size.Width);
        Assert.Equal(10f, layout_node13.Size.Height);
        Assert.Equal(25f, layout_node13.Location.X);
        Assert.Equal(20f, layout_node13.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(60f)),
        });
        var node10 = taffy.NewLeaf(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(25f), Dimension.FromLength(20f)),
        });
        var node11 = taffy.NewLeaf(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            AlignSelfValue = AlignItems.Baseline,
            SizeValue = new Size<Dimension>(Dimension.FromLength(25f), Dimension.FromLength(10f)),
        });
        var node12 = taffy.NewLeaf(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(25f), Dimension.FromLength(20f)),
        });
        var node13 = taffy.NewLeaf(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            AlignSelfValue = AlignItems.Baseline,
            SizeValue = new Size<Dimension>(Dimension.FromLength(25f), Dimension.FromLength(10f)),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.AutoComponent(), GridTemplateComponent.AutoComponent()),
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(25f)),
        }, new NodeId[] { node10, node11, node12, node13 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            AlignItemsValue = AlignItems.Baseline,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(100f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(50f, layout_node0.Size.Width);
        Assert.Equal(60f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(50f, layout_node1.Size.Width);
        Assert.Equal(25f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(68f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(25f, layout_node10.Size.Width);
        Assert.Equal(20f, layout_node10.Size.Height);
        Assert.Equal(0f, layout_node10.Location.X);
        Assert.Equal(0f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(25f, layout_node11.Size.Width);
        Assert.Equal(10f, layout_node11.Size.Height);
        Assert.Equal(25f, layout_node11.Location.X);
        Assert.Equal(0f, layout_node11.Location.Y);
        var layout_node12 = taffy.GetLayout(node12);
        Assert.Equal(25f, layout_node12.Size.Width);
        Assert.Equal(20f, layout_node12.Size.Height);
        Assert.Equal(0f, layout_node12.Location.X);
        Assert.Equal(20f, layout_node12.Location.Y);
        var layout_node13 = taffy.GetLayout(node13);
        Assert.Equal(25f, layout_node13.Size.Width);
        Assert.Equal(10f, layout_node13.Size.Height);
        Assert.Equal(25f, layout_node13.Location.X);
        Assert.Equal(20f, layout_node13.Location.Y);
    }
}
