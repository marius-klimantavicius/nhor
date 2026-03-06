using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class PercentagePaddingShouldCalculateBasedOnlyOnWidth
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(10f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            FlexGrowValue = 1f,
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Percent(0.1f), LengthPercentage.Percent(0.1f), LengthPercentage.Percent(0.1f), LengthPercentage.Percent(0.1f)),
        }, new NodeId[] { node00 });
        var node = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(200f, layout_node0.Size.Width);
        Assert.Equal(100f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(10f, layout_node00.Size.Width);
        Assert.Equal(10f, layout_node00.Size.Height);
        Assert.Equal(20f, layout_node00.Location.X);
        Assert.Equal(20f, layout_node00.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(10f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            FlexGrowValue = 1f,
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Percent(0.1f), LengthPercentage.Percent(0.1f), LengthPercentage.Percent(0.1f), LengthPercentage.Percent(0.1f)),
        }, new NodeId[] { node00 });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(100f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(100f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(200f, layout_node0.Size.Width);
        Assert.Equal(100f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(10f, layout_node00.Size.Width);
        Assert.Equal(10f, layout_node00.Size.Height);
        Assert.Equal(20f, layout_node00.Location.X);
        Assert.Equal(20f, layout_node00.Location.Y);
    }
}
