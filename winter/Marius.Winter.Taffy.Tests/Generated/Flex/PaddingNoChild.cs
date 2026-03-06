using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class PaddingNoChild
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node = taffy.NewLeaf(new Style
        {
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
        });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(20f, layout_node.Size.Width);
        Assert.Equal(20f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
        });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(20f, layout_node.Size.Width);
        Assert.Equal(20f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
    }
}
