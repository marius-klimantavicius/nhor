using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Block;

public class BlockPaddingBorderIntrinsicSize
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(10f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(7f), LengthPercentage.Length(3f), LengthPercentage.Length(1f), LengthPercentage.Length(5f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(8f), LengthPercentage.Length(4f), LengthPercentage.Length(2f), LengthPercentage.Length(6f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(72f, layout_node.Size.Width);
        Assert.Equal(34f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(50f, layout_node0.Size.Width);
        Assert.Equal(10f, layout_node0.Size.Height);
        Assert.Equal(15f, layout_node0.Location.X);
        Assert.Equal(3f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(50f, layout_node1.Size.Width);
        Assert.Equal(10f, layout_node1.Size.Height);
        Assert.Equal(15f, layout_node1.Location.X);
        Assert.Equal(13f, layout_node1.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(10f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(10f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(7f), LengthPercentage.Length(3f), LengthPercentage.Length(1f), LengthPercentage.Length(5f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(8f), LengthPercentage.Length(4f), LengthPercentage.Length(2f), LengthPercentage.Length(6f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(72f, layout_node.Size.Width);
        Assert.Equal(34f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(50f, layout_node0.Size.Width);
        Assert.Equal(10f, layout_node0.Size.Height);
        Assert.Equal(15f, layout_node0.Location.X);
        Assert.Equal(3f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(50f, layout_node1.Size.Width);
        Assert.Equal(10f, layout_node1.Size.Height);
        Assert.Equal(15f, layout_node1.Location.X);
        Assert.Equal(13f, layout_node1.Location.Y);
    }
}
