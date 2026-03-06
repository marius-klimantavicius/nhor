using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class BevyIssue8017
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(1f)),
        });
        var node01 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(1f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            GapValue = new Size<LengthPercentage>(LengthPercentage.Length(8f), LengthPercentage.Length(8f)),
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(0.5f)),
        }, new NodeId[] { node00, node01 });
        var node10 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(1f)),
        });
        var node11 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(1f)),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            GapValue = new Size<LengthPercentage>(LengthPercentage.Length(8f), LengthPercentage.Length(8f)),
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(0.5f)),
        }, new NodeId[] { node10, node11 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            FlexDirectionValue = FlexDirection.Column,
            GapValue = new Size<LengthPercentage>(LengthPercentage.Length(8f), LengthPercentage.Length(8f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(400f), Dimension.FromLength(400f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(8f), LengthPercentage.Length(8f), LengthPercentage.Length(8f), LengthPercentage.Length(8f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(400f, layout_node.Size.Width);
        Assert.Equal(400f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(384f, layout_node0.Size.Width);
        Assert.Equal(188f, layout_node0.Size.Height);
        Assert.Equal(8f, layout_node0.Location.X);
        Assert.Equal(8f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(188f, layout_node00.Size.Width);
        Assert.Equal(188f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(188f, layout_node01.Size.Width);
        Assert.Equal(188f, layout_node01.Size.Height);
        Assert.Equal(196f, layout_node01.Location.X);
        Assert.Equal(0f, layout_node01.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(384f, layout_node1.Size.Width);
        Assert.Equal(188f, layout_node1.Size.Height);
        Assert.Equal(8f, layout_node1.Location.X);
        Assert.Equal(204f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(188f, layout_node10.Size.Width);
        Assert.Equal(188f, layout_node10.Size.Height);
        Assert.Equal(0f, layout_node10.Location.X);
        Assert.Equal(0f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(188f, layout_node11.Size.Width);
        Assert.Equal(188f, layout_node11.Size.Height);
        Assert.Equal(196f, layout_node11.Location.X);
        Assert.Equal(0f, layout_node11.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(1f)),
        });
        var node01 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(1f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            GapValue = new Size<LengthPercentage>(LengthPercentage.Length(8f), LengthPercentage.Length(8f)),
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(0.5f)),
        }, new NodeId[] { node00, node01 });
        var node10 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(1f)),
        });
        var node11 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(1f)),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            GapValue = new Size<LengthPercentage>(LengthPercentage.Length(8f), LengthPercentage.Length(8f)),
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(0.5f)),
        }, new NodeId[] { node10, node11 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            GapValue = new Size<LengthPercentage>(LengthPercentage.Length(8f), LengthPercentage.Length(8f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(400f), Dimension.FromLength(400f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(8f), LengthPercentage.Length(8f), LengthPercentage.Length(8f), LengthPercentage.Length(8f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(416f, layout_node.Size.Width);
        Assert.Equal(416f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(400f, layout_node0.Size.Width);
        Assert.Equal(196f, layout_node0.Size.Height);
        Assert.Equal(8f, layout_node0.Location.X);
        Assert.Equal(8f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(196f, layout_node00.Size.Width);
        Assert.Equal(196f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(196f, layout_node01.Size.Width);
        Assert.Equal(196f, layout_node01.Size.Height);
        Assert.Equal(204f, layout_node01.Location.X);
        Assert.Equal(0f, layout_node01.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(400f, layout_node1.Size.Width);
        Assert.Equal(196f, layout_node1.Size.Height);
        Assert.Equal(8f, layout_node1.Location.X);
        Assert.Equal(212f, layout_node1.Location.Y);
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(196f, layout_node10.Size.Width);
        Assert.Equal(196f, layout_node10.Size.Height);
        Assert.Equal(0f, layout_node10.Location.X);
        Assert.Equal(0f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(196f, layout_node11.Size.Width);
        Assert.Equal(196f, layout_node11.Size.Height);
        Assert.Equal(204f, layout_node11.Location.X);
        Assert.Equal(0f, layout_node11.Location.Y);
    }
}
