using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class AbsoluteAspectRatioFillWidth
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            PositionValue = Position.Absolute,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromPercent(0.2f)),
            AspectRatioValue = 3f,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.AUTO),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            SizeValue = new Size<Dimension>(Dimension.FromLength(400f), Dimension.FromLength(300f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(400f, layout_node.Size.Width);
        Assert.Equal(300f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(180f, layout_node0.Size.Width);
        Assert.Equal(60f, layout_node0.Size.Height);
        Assert.Equal(20f, layout_node0.Location.X);
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
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromPercent(0.2f)),
            AspectRatioValue = 3f,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.AUTO),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
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
        Assert.Equal(180f, layout_node0.Size.Width);
        Assert.Equal(60f, layout_node0.Size.Height);
        Assert.Equal(20f, layout_node0.Location.X);
        Assert.Equal(15f, layout_node0.Location.Y);
    }
}
