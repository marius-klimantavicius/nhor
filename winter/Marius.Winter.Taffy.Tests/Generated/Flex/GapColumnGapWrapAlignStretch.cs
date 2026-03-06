using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class GapColumnGapWrapAlignStretch
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.Auto()),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.Auto()),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.Auto()),
        });
        var node3 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.Auto()),
        });
        var node4 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 1f,
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.Auto()),
        });
        var node = taffy.NewWithChildren(new Style
        {
            FlexWrapValue = FlexWrap.Wrap,
            AlignContentValue = AlignContent.Stretch,
            GapValue = new Size<LengthPercentage>(LengthPercentage.Length(5f), LengthPercentage.ZERO),
            SizeValue = new Size<Dimension>(Dimension.FromLength(300f), Dimension.FromLength(300f)),
        }, new NodeId[] { node0, node1, node2, node3, node4 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(300f, layout_node.Size.Width);
        Assert.Equal(300f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(71f, layout_node0.Size.Width);
        Assert.Equal(150f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(72f, layout_node1.Size.Width);
        Assert.Equal(150f, layout_node1.Size.Height);
        Assert.Equal(76f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(71f, layout_node2.Size.Width);
        Assert.Equal(150f, layout_node2.Size.Height);
        Assert.Equal(153f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(71f, layout_node3.Size.Width);
        Assert.Equal(150f, layout_node3.Size.Height);
        Assert.Equal(229f, layout_node3.Location.X);
        Assert.Equal(0f, layout_node3.Location.Y);
        var layout_node4 = taffy.GetLayout(node4);
        Assert.Equal(300f, layout_node4.Size.Width);
        Assert.Equal(150f, layout_node4.Size.Height);
        Assert.Equal(0f, layout_node4.Location.X);
        Assert.Equal(150f, layout_node4.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 1f,
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.Auto()),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 1f,
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.Auto()),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 1f,
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.Auto()),
        });
        var node3 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 1f,
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.Auto()),
        });
        var node4 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 1f,
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.Auto()),
        });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexWrapValue = FlexWrap.Wrap,
            AlignContentValue = AlignContent.Stretch,
            GapValue = new Size<LengthPercentage>(LengthPercentage.Length(5f), LengthPercentage.ZERO),
            SizeValue = new Size<Dimension>(Dimension.FromLength(300f), Dimension.FromLength(300f)),
        }, new NodeId[] { node0, node1, node2, node3, node4 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(300f, layout_node.Size.Width);
        Assert.Equal(300f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(71f, layout_node0.Size.Width);
        Assert.Equal(150f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(72f, layout_node1.Size.Width);
        Assert.Equal(150f, layout_node1.Size.Height);
        Assert.Equal(76f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(71f, layout_node2.Size.Width);
        Assert.Equal(150f, layout_node2.Size.Height);
        Assert.Equal(153f, layout_node2.Location.X);
        Assert.Equal(0f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(71f, layout_node3.Size.Width);
        Assert.Equal(150f, layout_node3.Size.Height);
        Assert.Equal(229f, layout_node3.Location.X);
        Assert.Equal(0f, layout_node3.Location.Y);
        var layout_node4 = taffy.GetLayout(node4);
        Assert.Equal(300f, layout_node4.Size.Width);
        Assert.Equal(150f, layout_node4.Size.Height);
        Assert.Equal(0f, layout_node4.Location.X);
        Assert.Equal(150f, layout_node4.Location.Y);
    }
}
