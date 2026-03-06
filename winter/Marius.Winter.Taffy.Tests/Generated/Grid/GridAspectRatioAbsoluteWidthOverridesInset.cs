using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridAspectRatioAbsoluteWidthOverridesInset
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            PositionValue = Position.Absolute,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.4f), Dimension.Auto()),
            AspectRatioValue = 3f,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(0.1f), LengthPercentageAuto.Percent(0.1f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.AUTO),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            SizeValue = new Size<Dimension>(Dimension.FromLength(400f), Dimension.FromLength(300f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(400f, layout_node.Size.Width);
        Assert.Equal(300f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(160f, layout_node0.Size.Width);
        Assert.Equal(53f, layout_node0.Size.Height);
        Assert.Equal(40f, layout_node0.Location.X);
        Assert.Equal(15f, layout_node0.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            PositionValue = Position.Absolute,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.4f), Dimension.Auto()),
            AspectRatioValue = 3f,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(0.1f), LengthPercentageAuto.Percent(0.1f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.AUTO),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(400f), Dimension.FromLength(300f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(400f, layout_node.Size.Width);
        Assert.Equal(300f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(160f, layout_node0.Size.Width);
        Assert.Equal(53f, layout_node0.Size.Height);
        Assert.Equal(40f, layout_node0.Location.X);
        Assert.Equal(15f, layout_node0.Location.Y);
    }
}
