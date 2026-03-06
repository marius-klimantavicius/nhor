using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class PercentagePositionBottomRight
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.55f), Dimension.FromPercent(0.15f)),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(0.2f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(0.1f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(500f), Dimension.FromLength(500f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(500f, layout_node.Size.Width);
        Assert.Equal(500f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(275f, layout_node0.Size.Width);
        Assert.Equal(75f, layout_node0.Size.Height);
        Assert.Equal(-100f, layout_node0.Location.X);
        Assert.Equal(-50f, layout_node0.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.55f), Dimension.FromPercent(0.15f)),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(0.2f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(0.1f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(500f), Dimension.FromLength(500f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(500f, layout_node.Size.Width);
        Assert.Equal(500f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(275f, layout_node0.Size.Width);
        Assert.Equal(75f, layout_node0.Size.Height);
        Assert.Equal(-100f, layout_node0.Location.X);
        Assert.Equal(-50f, layout_node0.Location.Y);
    }
}
