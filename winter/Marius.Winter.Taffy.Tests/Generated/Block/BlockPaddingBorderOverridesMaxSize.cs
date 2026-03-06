using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Block;

public class BlockPaddingBorderOverridesMaxSize
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style());
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            MaxSizeValue = new Size<Dimension>(Dimension.FromLength(12f), Dimension.FromLength(12f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(8f), LengthPercentage.Length(4f), LengthPercentage.Length(2f), LengthPercentage.Length(6f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(7f), LengthPercentage.Length(3f), LengthPercentage.Length(1f), LengthPercentage.Length(5f)),
        }, new NodeId[] { node00 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(22f, layout_node.Size.Width);
        Assert.Equal(14f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(22f, layout_node0.Size.Width);
        Assert.Equal(14f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(0f, layout_node00.Size.Width);
        Assert.Equal(0f, layout_node00.Size.Height);
        Assert.Equal(15f, layout_node00.Location.X);
        Assert.Equal(3f, layout_node00.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            MaxSizeValue = new Size<Dimension>(Dimension.FromLength(12f), Dimension.FromLength(12f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(8f), LengthPercentage.Length(4f), LengthPercentage.Length(2f), LengthPercentage.Length(6f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(7f), LengthPercentage.Length(3f), LengthPercentage.Length(1f), LengthPercentage.Length(5f)),
        }, new NodeId[] { node00 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(22f, layout_node.Size.Width);
        Assert.Equal(14f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(22f, layout_node0.Size.Width);
        Assert.Equal(14f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(0f, layout_node00.Size.Width);
        Assert.Equal(0f, layout_node00.Size.Height);
        Assert.Equal(15f, layout_node00.Location.X);
        Assert.Equal(3f, layout_node00.Location.Y);
    }
}
