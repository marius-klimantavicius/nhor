using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class JustifyContentMinWidthWithPaddingChildWidthGreaterThanParent
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node000 = taffy.NewLeaf(new Style
        {
            AlignContentValue = AlignContent.Stretch,
            SizeValue = new Size<Dimension>(Dimension.FromLength(300f), Dimension.FromLength(100f)),
        });
        var node00 = taffy.NewWithChildren(new Style
        {
            AlignContentValue = AlignContent.Stretch,
            JustifyContentValue = AlignContent.Center,
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(400f), Dimension.Auto()),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(100f), LengthPercentage.Length(100f), LengthPercentage.ZERO, LengthPercentage.ZERO),
        }, new NodeId[] { node000 });
        var node0 = taffy.NewWithChildren(new Style
        {
            AlignContentValue = AlignContent.Stretch,
        }, new NodeId[] { node00 });
        var node = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            AlignContentValue = AlignContent.Stretch,
            SizeValue = new Size<Dimension>(Dimension.FromLength(1000f), Dimension.FromLength(1584f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(1000f, layout_node.Size.Width);
        Assert.Equal(1584f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(1000f, layout_node0.Size.Width);
        Assert.Equal(100f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(500f, layout_node00.Size.Width);
        Assert.Equal(100f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node000 = taffy.GetLayout(node000);
        Assert.Equal(300f, layout_node000.Size.Width);
        Assert.Equal(100f, layout_node000.Size.Height);
        Assert.Equal(100f, layout_node000.Location.X);
        Assert.Equal(0f, layout_node000.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node000 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignContentValue = AlignContent.Stretch,
            SizeValue = new Size<Dimension>(Dimension.FromLength(300f), Dimension.FromLength(100f)),
        });
        var node00 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignContentValue = AlignContent.Stretch,
            JustifyContentValue = AlignContent.Center,
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(400f), Dimension.Auto()),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(100f), LengthPercentage.Length(100f), LengthPercentage.ZERO, LengthPercentage.ZERO),
        }, new NodeId[] { node000 });
        var node0 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignContentValue = AlignContent.Stretch,
        }, new NodeId[] { node00 });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            AlignContentValue = AlignContent.Stretch,
            SizeValue = new Size<Dimension>(Dimension.FromLength(1000f), Dimension.FromLength(1584f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(1000f, layout_node.Size.Width);
        Assert.Equal(1584f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(1000f, layout_node0.Size.Width);
        Assert.Equal(100f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(600f, layout_node00.Size.Width);
        Assert.Equal(100f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node000 = taffy.GetLayout(node000);
        Assert.Equal(300f, layout_node000.Size.Width);
        Assert.Equal(100f, layout_node000.Size.Height);
        Assert.Equal(150f, layout_node000.Location.X);
        Assert.Equal(0f, layout_node000.Location.Y);
    }
}
