using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridAlignItemsBaselineComplex
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        });
        var node10 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        }, new NodeId[] { node10 });
        var node20 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
        });
        var node2 = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        }, new NodeId[] { node20 });
        var node3 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        });
        var node4 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        });
        var node5 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        });
        var node60 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
        });
        var node6 = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        }, new NodeId[] { node60 });
        var node70 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(5f)),
        });
        var node7 = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        }, new NodeId[] { node70 });
        var node8 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            AlignItemsValue = AlignItems.Baseline,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(120f), Dimension.FromLength(120f)),
        }, new NodeId[] { node0, node1, node2, node3, node4, node5, node6, node7, node8 });
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
        Assert.Equal(20f, layout_node1.Size.Width);
        Assert.Equal(20f, layout_node1.Size.Height);
        Assert.Equal(40f, layout_node1.Location.X);
        Assert.Equal(10f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(0f, layout_node10.Size.Width);
        Assert.Equal(10f, layout_node10.Size.Height);
        Assert.Equal(0f, layout_node10.Location.X);
        Assert.Equal(0f, layout_node10.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(20f, layout_node2.Size.Width);
        Assert.Equal(20f, layout_node2.Size.Height);
        Assert.Equal(80f, layout_node2.Location.X);
        Assert.Equal(10f, layout_node2.Location.Y);
        var layout_node20 = taffy.GetLayout(node20);
        Assert.Equal(0f, layout_node20.Size.Width);
        Assert.Equal(10f, layout_node20.Size.Height);
        Assert.Equal(0f, layout_node20.Location.X);
        Assert.Equal(0f, layout_node20.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(20f, layout_node3.Size.Width);
        Assert.Equal(20f, layout_node3.Size.Height);
        Assert.Equal(0f, layout_node3.Location.X);
        Assert.Equal(40f, layout_node3.Location.Y);
        var layout_node4 = taffy.GetLayout(node4);
        Assert.Equal(20f, layout_node4.Size.Width);
        Assert.Equal(20f, layout_node4.Size.Height);
        Assert.Equal(40f, layout_node4.Location.X);
        Assert.Equal(40f, layout_node4.Location.Y);
        var layout_node5 = taffy.GetLayout(node5);
        Assert.Equal(20f, layout_node5.Size.Width);
        Assert.Equal(20f, layout_node5.Size.Height);
        Assert.Equal(80f, layout_node5.Location.X);
        Assert.Equal(40f, layout_node5.Location.Y);
        var layout_node6 = taffy.GetLayout(node6);
        Assert.Equal(20f, layout_node6.Size.Width);
        Assert.Equal(20f, layout_node6.Size.Height);
        Assert.Equal(0f, layout_node6.Location.X);
        Assert.Equal(90f, layout_node6.Location.Y);
        var layout_node60 = taffy.GetLayout(node60);
        Assert.Equal(0f, layout_node60.Size.Width);
        Assert.Equal(10f, layout_node60.Size.Height);
        Assert.Equal(0f, layout_node60.Location.X);
        Assert.Equal(0f, layout_node60.Location.Y);
        var layout_node7 = taffy.GetLayout(node7);
        Assert.Equal(20f, layout_node7.Size.Width);
        Assert.Equal(20f, layout_node7.Size.Height);
        Assert.Equal(40f, layout_node7.Location.X);
        Assert.Equal(95f, layout_node7.Location.Y);
        var layout_node70 = taffy.GetLayout(node70);
        Assert.Equal(0f, layout_node70.Size.Width);
        Assert.Equal(5f, layout_node70.Size.Height);
        Assert.Equal(0f, layout_node70.Location.X);
        Assert.Equal(0f, layout_node70.Location.Y);
        var layout_node8 = taffy.GetLayout(node8);
        Assert.Equal(20f, layout_node8.Size.Width);
        Assert.Equal(20f, layout_node8.Size.Height);
        Assert.Equal(80f, layout_node8.Location.X);
        Assert.Equal(80f, layout_node8.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        });
        var node10 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        }, new NodeId[] { node10 });
        var node20 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
        });
        var node2 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        }, new NodeId[] { node20 });
        var node3 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        });
        var node4 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        });
        var node5 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        });
        var node60 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
        });
        var node6 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        }, new NodeId[] { node60 });
        var node70 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(5f)),
        });
        var node7 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        }, new NodeId[] { node70 });
        var node8 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(20f), Dimension.FromLength(20f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            AlignItemsValue = AlignItems.Baseline,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(120f), Dimension.FromLength(120f)),
        }, new NodeId[] { node0, node1, node2, node3, node4, node5, node6, node7, node8 });
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
        Assert.Equal(20f, layout_node1.Size.Width);
        Assert.Equal(20f, layout_node1.Size.Height);
        Assert.Equal(40f, layout_node1.Location.X);
        Assert.Equal(10f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(0f, layout_node10.Size.Width);
        Assert.Equal(10f, layout_node10.Size.Height);
        Assert.Equal(0f, layout_node10.Location.X);
        Assert.Equal(0f, layout_node10.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(20f, layout_node2.Size.Width);
        Assert.Equal(20f, layout_node2.Size.Height);
        Assert.Equal(80f, layout_node2.Location.X);
        Assert.Equal(10f, layout_node2.Location.Y);
        var layout_node20 = taffy.GetLayout(node20);
        Assert.Equal(0f, layout_node20.Size.Width);
        Assert.Equal(10f, layout_node20.Size.Height);
        Assert.Equal(0f, layout_node20.Location.X);
        Assert.Equal(0f, layout_node20.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(20f, layout_node3.Size.Width);
        Assert.Equal(20f, layout_node3.Size.Height);
        Assert.Equal(0f, layout_node3.Location.X);
        Assert.Equal(40f, layout_node3.Location.Y);
        var layout_node4 = taffy.GetLayout(node4);
        Assert.Equal(20f, layout_node4.Size.Width);
        Assert.Equal(20f, layout_node4.Size.Height);
        Assert.Equal(40f, layout_node4.Location.X);
        Assert.Equal(40f, layout_node4.Location.Y);
        var layout_node5 = taffy.GetLayout(node5);
        Assert.Equal(20f, layout_node5.Size.Width);
        Assert.Equal(20f, layout_node5.Size.Height);
        Assert.Equal(80f, layout_node5.Location.X);
        Assert.Equal(40f, layout_node5.Location.Y);
        var layout_node6 = taffy.GetLayout(node6);
        Assert.Equal(20f, layout_node6.Size.Width);
        Assert.Equal(20f, layout_node6.Size.Height);
        Assert.Equal(0f, layout_node6.Location.X);
        Assert.Equal(90f, layout_node6.Location.Y);
        var layout_node60 = taffy.GetLayout(node60);
        Assert.Equal(0f, layout_node60.Size.Width);
        Assert.Equal(10f, layout_node60.Size.Height);
        Assert.Equal(0f, layout_node60.Location.X);
        Assert.Equal(0f, layout_node60.Location.Y);
        var layout_node7 = taffy.GetLayout(node7);
        Assert.Equal(20f, layout_node7.Size.Width);
        Assert.Equal(20f, layout_node7.Size.Height);
        Assert.Equal(40f, layout_node7.Location.X);
        Assert.Equal(95f, layout_node7.Location.Y);
        var layout_node70 = taffy.GetLayout(node70);
        Assert.Equal(0f, layout_node70.Size.Width);
        Assert.Equal(5f, layout_node70.Size.Height);
        Assert.Equal(0f, layout_node70.Location.X);
        Assert.Equal(0f, layout_node70.Location.Y);
        var layout_node8 = taffy.GetLayout(node8);
        Assert.Equal(20f, layout_node8.Size.Width);
        Assert.Equal(20f, layout_node8.Size.Height);
        Assert.Equal(80f, layout_node8.Location.X);
        Assert.Equal(80f, layout_node8.Location.Y);
    }
}
