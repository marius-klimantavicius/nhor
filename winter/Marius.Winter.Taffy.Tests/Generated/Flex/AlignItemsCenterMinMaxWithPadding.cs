using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class AlignItemsCenterMinMaxWithPadding
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(62f), Dimension.FromLength(62f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            AlignItemsValue = AlignItems.Center,
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(320f), Dimension.FromLength(72f)),
            MaxSizeValue = new Size<Dimension>(Dimension.FromLength(320f), Dimension.FromLength(504f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.ZERO, LengthPercentage.ZERO, LengthPercentage.Length(8f), LengthPercentage.Length(8f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(320f, layout_node.Size.Width);
        Assert.Equal(78f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(62f, layout_node0.Size.Width);
        Assert.Equal(62f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(8f, layout_node0.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(62f), Dimension.FromLength(62f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignItemsValue = AlignItems.Center,
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(320f), Dimension.FromLength(72f)),
            MaxSizeValue = new Size<Dimension>(Dimension.FromLength(320f), Dimension.FromLength(504f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.ZERO, LengthPercentage.ZERO, LengthPercentage.Length(8f), LengthPercentage.Length(8f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(320f, layout_node.Size.Width);
        Assert.Equal(88f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(62f, layout_node0.Size.Width);
        Assert.Equal(62f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(13f, layout_node0.Location.Y);
    }
}
