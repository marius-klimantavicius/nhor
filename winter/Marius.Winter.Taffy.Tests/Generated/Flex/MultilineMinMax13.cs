using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class MultilineMinMax13
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            FlexShrinkValue = 1f,
            FlexBasisValue = Dimension.FromLength(600f),
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MaxSizeValue = new Size<Dimension>(Dimension.FromLength(300f), Dimension.Auto()),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.ZERO, LengthPercentage.ZERO, LengthPercentage.ZERO),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            FlexShrinkValue = 1f,
            FlexBasisValue = Dimension.AUTO,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(10f)),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            FlexShrinkValue = 1f,
            FlexBasisValue = Dimension.AUTO,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(10f)),
        });
        var node3 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            FlexShrinkValue = 1f,
            FlexBasisValue = Dimension.AUTO,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(10f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            FlexWrapValue = FlexWrap.Wrap,
            SizeValue = new Size<Dimension>(Dimension.FromLength(600f), Dimension.FromLength(20f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(5f), LengthPercentage.Length(5f), LengthPercentage.Length(5f), LengthPercentage.Length(5f)),
        }, new NodeId[] { node0, node1, node2, node3 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(610f, layout_node.Size.Width);
        Assert.Equal(30f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(300f, layout_node0.Size.Width);
        Assert.Equal(10f, layout_node0.Size.Height);
        Assert.Equal(5f, layout_node0.Location.X);
        Assert.Equal(5f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(100f, layout_node1.Size.Width);
        Assert.Equal(10f, layout_node1.Size.Height);
        Assert.Equal(305f, layout_node1.Location.X);
        Assert.Equal(5f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(100f, layout_node2.Size.Width);
        Assert.Equal(10f, layout_node2.Size.Height);
        Assert.Equal(405f, layout_node2.Location.X);
        Assert.Equal(5f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(100f, layout_node3.Size.Width);
        Assert.Equal(10f, layout_node3.Size.Height);
        Assert.Equal(505f, layout_node3.Location.X);
        Assert.Equal(5f, layout_node3.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            FlexShrinkValue = 1f,
            FlexBasisValue = Dimension.FromLength(600f),
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
            MaxSizeValue = new Size<Dimension>(Dimension.FromLength(300f), Dimension.Auto()),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.ZERO, LengthPercentage.ZERO, LengthPercentage.ZERO),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            FlexShrinkValue = 1f,
            FlexBasisValue = Dimension.AUTO,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(10f)),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            FlexShrinkValue = 1f,
            FlexBasisValue = Dimension.AUTO,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(10f)),
        });
        var node3 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            FlexShrinkValue = 1f,
            FlexBasisValue = Dimension.AUTO,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(10f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            FlexWrapValue = FlexWrap.Wrap,
            SizeValue = new Size<Dimension>(Dimension.FromLength(600f), Dimension.FromLength(20f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(5f), LengthPercentage.Length(5f), LengthPercentage.Length(5f), LengthPercentage.Length(5f)),
        }, new NodeId[] { node0, node1, node2, node3 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(610f, layout_node.Size.Width);
        Assert.Equal(30f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(300f, layout_node0.Size.Width);
        Assert.Equal(10f, layout_node0.Size.Height);
        Assert.Equal(5f, layout_node0.Location.X);
        Assert.Equal(5f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(100f, layout_node1.Size.Width);
        Assert.Equal(10f, layout_node1.Size.Height);
        Assert.Equal(305f, layout_node1.Location.X);
        Assert.Equal(5f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(100f, layout_node2.Size.Width);
        Assert.Equal(10f, layout_node2.Size.Height);
        Assert.Equal(405f, layout_node2.Location.X);
        Assert.Equal(5f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(100f, layout_node3.Size.Width);
        Assert.Equal(10f, layout_node3.Size.Height);
        Assert.Equal(505f, layout_node3.Location.X);
        Assert.Equal(5f, layout_node3.Location.Y);
    }
}
