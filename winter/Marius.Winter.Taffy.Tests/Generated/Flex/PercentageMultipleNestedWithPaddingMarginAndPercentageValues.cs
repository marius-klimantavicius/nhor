using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class PercentageMultipleNestedWithPaddingMarginAndPercentageValues
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node000 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.45f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f)),
        });
        var node00 = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.5f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f)),
        }, new NodeId[] { node000 });
        var node0 = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            FlexGrowValue = 1f,
            FlexBasisValue = Dimension.FromPercent(0.1f),
            MinSizeValue = new Size<Dimension>(Dimension.FromPercent(0.6f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f)),
        }, new NodeId[] { node00 });
        var node1 = taffy.NewLeaf(new Style
        {
            FlexGrowValue = 4f,
            FlexBasisValue = Dimension.FromPercent(0.15f),
            MinSizeValue = new Size<Dimension>(Dimension.FromPercent(0.2f), Dimension.Auto()),
        });
        var node = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(200f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(190f, layout_node0.Size.Width);
        Assert.Equal(48f, layout_node0.Size.Height);
        Assert.Equal(5f, layout_node0.Location.X);
        Assert.Equal(5f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(92f, layout_node00.Size.Width);
        Assert.Equal(25f, layout_node00.Size.Height);
        Assert.Equal(8f, layout_node00.Location.X);
        Assert.Equal(8f, layout_node00.Location.Y);
        var layout_node000 = taffy.GetLayout(node000);
        Assert.Equal(36f, layout_node000.Size.Width);
        Assert.Equal(6f, layout_node000.Size.Height);
        Assert.Equal(10f, layout_node000.Location.X);
        Assert.Equal(10f, layout_node000.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(200f, layout_node1.Size.Width);
        Assert.Equal(142f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(58f, layout_node1.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node000 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.45f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f)),
        });
        var node00 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.5f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f)),
        }, new NodeId[] { node000 });
        var node0 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            FlexGrowValue = 1f,
            FlexBasisValue = Dimension.FromPercent(0.1f),
            MinSizeValue = new Size<Dimension>(Dimension.FromPercent(0.6f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f)),
        }, new NodeId[] { node00 });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexGrowValue = 4f,
            FlexBasisValue = Dimension.FromPercent(0.15f),
            MinSizeValue = new Size<Dimension>(Dimension.FromPercent(0.2f), Dimension.Auto()),
        });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(200f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(190f, layout_node0.Size.Width);
        Assert.Equal(53f, layout_node0.Size.Height);
        Assert.Equal(5f, layout_node0.Location.X);
        Assert.Equal(5f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(103f, layout_node00.Size.Width);
        Assert.Equal(26f, layout_node00.Size.Height);
        Assert.Equal(8f, layout_node00.Location.X);
        Assert.Equal(8f, layout_node00.Location.Y);
        var layout_node000 = taffy.GetLayout(node000);
        Assert.Equal(48f, layout_node000.Size.Width);
        Assert.Equal(6f, layout_node000.Size.Height);
        Assert.Equal(10f, layout_node000.Location.X);
        Assert.Equal(10f, layout_node000.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(200f, layout_node1.Size.Width);
        Assert.Equal(137f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(63f, layout_node1.Location.Y);
    }
}
