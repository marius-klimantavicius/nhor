using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridPercentItemsNestedModerate
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.45f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.5f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f)),
        }, new NodeId[] { node00 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.Auto()),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.45f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.5f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f)),
        }, new NodeId[] { node00 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.Auto()),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
    }
}
