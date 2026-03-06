using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class BevyIssue8082
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f)),
        });
        var node01 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f)),
        });
        var node02 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f)),
        });
        var node03 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            FlexWrapValue = FlexWrap.Wrap,
            AlignItemsValue = AlignItems.FlexStart,
            AlignContentValue = AlignContent.Center,
            JustifyContentValue = AlignContent.Center,
        }, new NodeId[] { node00, node01, node02, node03 });
        var node = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            AlignItemsValue = AlignItems.Stretch,
            AlignContentValue = AlignContent.Center,
            JustifyContentValue = AlignContent.FlexStart,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(400f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(400f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(200f, layout_node0.Size.Width);
        Assert.Equal(140f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(50f, layout_node00.Size.Width);
        Assert.Equal(50f, layout_node00.Size.Height);
        Assert.Equal(40f, layout_node00.Location.X);
        Assert.Equal(10f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(50f, layout_node01.Size.Width);
        Assert.Equal(50f, layout_node01.Size.Height);
        Assert.Equal(110f, layout_node01.Location.X);
        Assert.Equal(10f, layout_node01.Location.Y);
        var layout_node02 = taffy.GetLayout(node02);
        Assert.Equal(50f, layout_node02.Size.Width);
        Assert.Equal(50f, layout_node02.Size.Height);
        Assert.Equal(40f, layout_node02.Location.X);
        Assert.Equal(80f, layout_node02.Location.Y);
        var layout_node03 = taffy.GetLayout(node03);
        Assert.Equal(50f, layout_node03.Size.Width);
        Assert.Equal(50f, layout_node03.Size.Height);
        Assert.Equal(110f, layout_node03.Location.X);
        Assert.Equal(80f, layout_node03.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f)),
        });
        var node01 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f)),
        });
        var node02 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f)),
        });
        var node03 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f), LengthPercentageAuto.Length(10f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexWrapValue = FlexWrap.Wrap,
            AlignItemsValue = AlignItems.FlexStart,
            AlignContentValue = AlignContent.Center,
            JustifyContentValue = AlignContent.Center,
        }, new NodeId[] { node00, node01, node02, node03 });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            AlignItemsValue = AlignItems.Stretch,
            AlignContentValue = AlignContent.Center,
            JustifyContentValue = AlignContent.FlexStart,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(400f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(400f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(200f, layout_node0.Size.Width);
        Assert.Equal(140f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(50f, layout_node00.Size.Width);
        Assert.Equal(50f, layout_node00.Size.Height);
        Assert.Equal(40f, layout_node00.Location.X);
        Assert.Equal(10f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(50f, layout_node01.Size.Width);
        Assert.Equal(50f, layout_node01.Size.Height);
        Assert.Equal(110f, layout_node01.Location.X);
        Assert.Equal(10f, layout_node01.Location.Y);
        var layout_node02 = taffy.GetLayout(node02);
        Assert.Equal(50f, layout_node02.Size.Width);
        Assert.Equal(50f, layout_node02.Size.Height);
        Assert.Equal(40f, layout_node02.Location.X);
        Assert.Equal(80f, layout_node02.Location.Y);
        var layout_node03 = taffy.GetLayout(node03);
        Assert.Equal(50f, layout_node03.Size.Width);
        Assert.Equal(50f, layout_node03.Size.Height);
        Assert.Equal(110f, layout_node03.Location.X);
        Assert.Equal(80f, layout_node03.Location.Y);
    }
}
