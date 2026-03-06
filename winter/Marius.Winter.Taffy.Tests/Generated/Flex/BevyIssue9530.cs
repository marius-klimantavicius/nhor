using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class BevyIssue9530
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromLength(20f)),
        });
        var node10 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromLength(50f)),
        });
        var node11 = taffy.NewLeafWithContext(new Style
        {
            AlignItemsValue = AlignItems.Center,
            AlignContentValue = AlignContent.Center,
            JustifyContentValue = AlignContent.Center,
            FlexGrowValue = 1f,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(20f), LengthPercentageAuto.Length(20f), LengthPercentageAuto.Length(20f), LengthPercentageAuto.Length(20f)),
        }, TestNodeContext.AhemText("HHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH", WritingMode.Horizontal));
        var node12 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromLength(50f)),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(20f), LengthPercentageAuto.Length(20f), LengthPercentageAuto.Length(20f), LengthPercentageAuto.Length(20f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(20f), LengthPercentage.Length(20f), LengthPercentage.Length(20f), LengthPercentage.Length(20f)),
        }, new NodeId[] { node10, node11, node12 });
        var node = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            AlignItemsValue = AlignItems.Center,
            AlignContentValue = AlignContent.Center,
            SizeValue = new Size<Dimension>(Dimension.FromLength(300f), Dimension.FromLength(300f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(300f, layout_node.Size.Width);
        Assert.Equal(300f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(300f, layout_node0.Size.Width);
        Assert.Equal(0f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(300f, layout_node1.Size.Width);
        Assert.Equal(420f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(20f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(260f, layout_node10.Size.Width);
        Assert.Equal(50f, layout_node10.Size.Height);
        Assert.Equal(20f, layout_node10.Location.X);
        Assert.Equal(20f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(220f, layout_node11.Size.Width);
        Assert.Equal(240f, layout_node11.Size.Height);
        Assert.Equal(40f, layout_node11.Location.X);
        Assert.Equal(90f, layout_node11.Location.Y);
        var layout_node12 = taffy.GetLayout(node12);
        Assert.Equal(260f, layout_node12.Size.Width);
        Assert.Equal(50f, layout_node12.Size.Height);
        Assert.Equal(20f, layout_node12.Location.X);
        Assert.Equal(350f, layout_node12.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromLength(20f)),
        });
        var node10 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromLength(50f)),
        });
        var node11 = taffy.NewLeafWithContext(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignItemsValue = AlignItems.Center,
            AlignContentValue = AlignContent.Center,
            JustifyContentValue = AlignContent.Center,
            FlexGrowValue = 1f,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(20f), LengthPercentageAuto.Length(20f), LengthPercentageAuto.Length(20f), LengthPercentageAuto.Length(20f)),
        }, TestNodeContext.AhemText("HHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH\u200bHHHH", WritingMode.Horizontal));
        var node12 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromLength(50f)),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            FlexGrowValue = 1f,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(20f), LengthPercentageAuto.Length(20f), LengthPercentageAuto.Length(20f), LengthPercentageAuto.Length(20f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(20f), LengthPercentage.Length(20f), LengthPercentage.Length(20f), LengthPercentage.Length(20f)),
        }, new NodeId[] { node10, node11, node12 });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            AlignItemsValue = AlignItems.Center,
            AlignContentValue = AlignContent.Center,
            SizeValue = new Size<Dimension>(Dimension.FromLength(300f), Dimension.FromLength(300f)),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(300f, layout_node.Size.Width);
        Assert.Equal(300f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(300f, layout_node0.Size.Width);
        Assert.Equal(0f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(340f, layout_node1.Size.Width);
        Assert.Equal(380f, layout_node1.Size.Height);
        Assert.Equal(-20f, layout_node1.Location.X);
        Assert.Equal(20f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(300f, layout_node10.Size.Width);
        Assert.Equal(50f, layout_node10.Size.Height);
        Assert.Equal(20f, layout_node10.Location.X);
        Assert.Equal(20f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(260f, layout_node11.Size.Width);
        Assert.Equal(200f, layout_node11.Size.Height);
        Assert.Equal(40f, layout_node11.Location.X);
        Assert.Equal(90f, layout_node11.Location.Y);
        var layout_node12 = taffy.GetLayout(node12);
        Assert.Equal(300f, layout_node12.Size.Width);
        Assert.Equal(50f, layout_node12.Size.Height);
        Assert.Equal(20f, layout_node12.Location.X);
        Assert.Equal(310f, layout_node12.Location.Y);
    }
}
