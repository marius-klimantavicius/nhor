using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class WrapNodesWithContentSizingMarginCross
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node000 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(40f)),
        });
        var node00 = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
        }, new NodeId[] { node000 });
        var node010 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(40f)),
        });
        var node01 = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(10f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node010 });
        var node0 = taffy.NewWithChildren(new Style
        {
            FlexWrapValue = FlexWrap.Wrap,
            SizeValue = new Size<Dimension>(Dimension.FromLength(70f), Dimension.Auto()),
        }, new NodeId[] { node00, node01 });
        var node = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromLength(500f), Dimension.FromLength(500f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(500f, layout_node.Size.Width);
        Assert.Equal(500f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(70f, layout_node0.Size.Width);
        Assert.Equal(90f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(40f, layout_node00.Size.Width);
        Assert.Equal(40f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node000 = taffy.GetLayout(node000);
        Assert.Equal(40f, layout_node000.Size.Width);
        Assert.Equal(40f, layout_node000.Size.Height);
        Assert.Equal(0f, layout_node000.Location.X);
        Assert.Equal(0f, layout_node000.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(40f, layout_node01.Size.Width);
        Assert.Equal(40f, layout_node01.Size.Height);
        Assert.Equal(0f, layout_node01.Location.X);
        Assert.Equal(50f, layout_node01.Location.Y);
        var layout_node010 = taffy.GetLayout(node010);
        Assert.Equal(40f, layout_node010.Size.Width);
        Assert.Equal(40f, layout_node010.Size.Height);
        Assert.Equal(0f, layout_node010.Location.X);
        Assert.Equal(0f, layout_node010.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node000 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(40f)),
        });
        var node00 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
        }, new NodeId[] { node000 });
        var node010 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(40f)),
        });
        var node01 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(10f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node010 });
        var node0 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexWrapValue = FlexWrap.Wrap,
            SizeValue = new Size<Dimension>(Dimension.FromLength(70f), Dimension.Auto()),
        }, new NodeId[] { node00, node01 });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromLength(500f), Dimension.FromLength(500f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(500f, layout_node.Size.Width);
        Assert.Equal(500f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(70f, layout_node0.Size.Width);
        Assert.Equal(90f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(40f, layout_node00.Size.Width);
        Assert.Equal(40f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node000 = taffy.GetLayout(node000);
        Assert.Equal(40f, layout_node000.Size.Width);
        Assert.Equal(40f, layout_node000.Size.Height);
        Assert.Equal(0f, layout_node000.Location.X);
        Assert.Equal(0f, layout_node000.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(40f, layout_node01.Size.Width);
        Assert.Equal(40f, layout_node01.Size.Height);
        Assert.Equal(0f, layout_node01.Location.X);
        Assert.Equal(50f, layout_node01.Location.Y);
        var layout_node010 = taffy.GetLayout(node010);
        Assert.Equal(40f, layout_node010.Size.Width);
        Assert.Equal(40f, layout_node010.Size.Height);
        Assert.Equal(0f, layout_node010.Location.X);
        Assert.Equal(0f, layout_node010.Location.Y);
    }
}
