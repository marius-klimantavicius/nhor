using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Leaf;

public class LeafWithContentAndBorder
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node = taffy.NewLeafWithContext(new Style
        {
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(8f), LengthPercentage.Length(4f), LengthPercentage.Length(2f), LengthPercentage.Length(6f)),
        }, TestNodeContext.AhemText("HHHH", WritingMode.Horizontal));
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(52f, layout_node.Size.Width);
        Assert.Equal(18f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node = taffy.NewLeafWithContext(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(8f), LengthPercentage.Length(4f), LengthPercentage.Length(2f), LengthPercentage.Length(6f)),
        }, TestNodeContext.AhemText("HHHH", WritingMode.Horizontal));
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(52f, layout_node.Size.Width);
        Assert.Equal(18f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
    }
}
