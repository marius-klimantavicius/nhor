using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class RoundingInnerNodeControversyCombined
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromPercent(1f)),
        });
        var node10 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.Auto()),
        });
        var node110 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.Auto()),
        });
        var node11 = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.Auto()),
        }, new NodeId[] { node110 });
        var node12 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.Auto()),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromPercent(1f)),
        }, new NodeId[] { node10, node11, node12 });
        var node2 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromPercent(1f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(640f), Dimension.FromLength(320f)),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(640f, layout_node.Size.Width);
        Assert.Equal(320f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(213f, layout_node0.Size.Width);
        Assert.Equal(320f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(214f, layout_node1.Size.Width);
        Assert.Equal(320f, layout_node1.Size.Height);
        Assert.Equal(213f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(214f, layout_node10.Size.Width);
        Assert.Equal(107f, layout_node10.Size.Height);
        Assert.Equal(0f, layout_node10.Location.X);
        Assert.Equal(0f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(214f, layout_node11.Size.Width);
        Assert.Equal(106f, layout_node11.Size.Height);
        Assert.Equal(0f, layout_node11.Location.X);
        Assert.Equal(107f, layout_node11.Location.Y);
        var layout_node110 = taffy.GetLayout(node110);
        Assert.Equal(214f, layout_node110.Size.Width);
        Assert.Equal(106f, layout_node110.Size.Height);
        Assert.Equal(0f, layout_node110.Location.X);
        Assert.Equal(0f, layout_node110.Location.Y);
        var layout_node12 = taffy.GetLayout(node12);
        Assert.Equal(214f, layout_node12.Size.Width);
        Assert.Equal(107f, layout_node12.Size.Height);
        Assert.Equal(0f, layout_node12.Location.X);
        Assert.Equal(213f, layout_node12.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(213f, layout_node2.Size.Width);
        Assert.Equal(320f, layout_node2.Size.Height);
        Assert.Equal(427f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromPercent(1f)),
        });
        var node10 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.Auto()),
        });
        var node110 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.Auto()),
        });
        var node11 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.Auto()),
        }, new NodeId[] { node110 });
        var node12 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.Auto()),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromPercent(1f)),
        }, new NodeId[] { node10, node11, node12 });
        var node2 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromPercent(1f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(640f), Dimension.FromLength(320f)),
        }, new NodeId[] { node0, node1, node2 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(640f, layout_node.Size.Width);
        Assert.Equal(320f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(213f, layout_node0.Size.Width);
        Assert.Equal(320f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(214f, layout_node1.Size.Width);
        Assert.Equal(320f, layout_node1.Size.Height);
        Assert.Equal(213f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(214f, layout_node10.Size.Width);
        Assert.Equal(107f, layout_node10.Size.Height);
        Assert.Equal(0f, layout_node10.Location.X);
        Assert.Equal(0f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(214f, layout_node11.Size.Width);
        Assert.Equal(106f, layout_node11.Size.Height);
        Assert.Equal(0f, layout_node11.Location.X);
        Assert.Equal(107f, layout_node11.Location.Y);
        var layout_node110 = taffy.GetLayout(node110);
        Assert.Equal(214f, layout_node110.Size.Width);
        Assert.Equal(106f, layout_node110.Size.Height);
        Assert.Equal(0f, layout_node110.Location.X);
        Assert.Equal(0f, layout_node110.Location.Y);
        var layout_node12 = taffy.GetLayout(node12);
        Assert.Equal(214f, layout_node12.Size.Width);
        Assert.Equal(107f, layout_node12.Size.Height);
        Assert.Equal(0f, layout_node12.Location.X);
        Assert.Equal(213f, layout_node12.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(213f, layout_node2.Size.Width);
        Assert.Equal(320f, layout_node2.Size.Height);
        Assert.Equal(427f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
    }
}
