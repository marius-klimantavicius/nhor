using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class MeasureChildConstraintPaddingParent
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeafWithContext(new Style(), TestNodeContext.AhemText("HHHHHHHHHH\u200bHHHHHHHHHH\u200bHHHHHHHHHH\u200bHHHHHHHHHH\u200bHHHHHHHHHH\u200bHHHHHHHHHH\u200bHHHHHHHHHH\u200bHHHHHHHHHH\u200bHHHHHHHHHH\u200bHHHHHHHHHH", WritingMode.Horizontal));
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.AUTO),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(50f, layout_node.Size.Width);
        Assert.Equal(120f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(100f, layout_node0.Size.Height);
        Assert.Equal(10f, layout_node0.Location.X);
        Assert.Equal(10f, layout_node0.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeafWithContext(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        }, TestNodeContext.AhemText("HHHHHHHHHH\u200bHHHHHHHHHH\u200bHHHHHHHHHH\u200bHHHHHHHHHH\u200bHHHHHHHHHH\u200bHHHHHHHHHH\u200bHHHHHHHHHH\u200bHHHHHHHHHH\u200bHHHHHHHHHH\u200bHHHHHHHHHH", WritingMode.Horizontal));
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.AUTO),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(70f, layout_node.Size.Width);
        Assert.Equal(120f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(100f, layout_node0.Size.Width);
        Assert.Equal(100f, layout_node0.Size.Height);
        Assert.Equal(10f, layout_node0.Location.X);
        Assert.Equal(10f, layout_node0.Location.Y);
    }
}
