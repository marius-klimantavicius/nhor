using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridPercentItemsNestedWithPaddingMargin
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node000 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.45f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f)),
        });
        var node00 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.5f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f)),
        }, new NodeId[] { node000 });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            MinSizeValue = new Size<Dimension>(Dimension.FromPercent(0.6f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f)),
        }, new NodeId[] { node00 });
        var node1 = taffy.NewLeaf(new Style());
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromFr(1f), GridTemplateComponent.FromFr(4f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(200f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(190f, layout_node0.Size.Width);
        Assert.Equal(41f, layout_node0.Size.Height);
        Assert.Equal(5f, layout_node0.Location.X);
        Assert.Equal(5f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(92f, layout_node00.Size.Width);
        Assert.Equal(25f, layout_node00.Size.Height);
        Assert.Equal(8f, layout_node00.Location.X);
        Assert.Equal(8f, layout_node00.Location.Y);
        var layout_node000 = taffy.GetLayout(node000);
        Assert.Equal(36f, layout_node000.Size.Width);
        Assert.Equal(6f, layout_node000.Size.Height);
        Assert.Equal(10f, layout_node000.Location.X);
        Assert.Equal(10f, layout_node000.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(200f, layout_node1.Size.Width);
        Assert.Equal(149f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(51f, layout_node1.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node000 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.45f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f)),
        });
        var node00 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.5f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f), LengthPercentage.Percent(0.03f)),
        }, new NodeId[] { node000 });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            MinSizeValue = new Size<Dimension>(Dimension.FromPercent(0.6f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f), LengthPercentageAuto.Length(5f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f)),
        }, new NodeId[] { node00 });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromFr(1f), GridTemplateComponent.FromFr(4f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(200f)),
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(200f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(190f, layout_node0.Size.Width);
        Assert.Equal(42f, layout_node0.Size.Height);
        Assert.Equal(5f, layout_node0.Location.X);
        Assert.Equal(5f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(103f, layout_node00.Size.Width);
        Assert.Equal(26f, layout_node00.Size.Height);
        Assert.Equal(8f, layout_node00.Location.X);
        Assert.Equal(8f, layout_node00.Location.Y);
        var layout_node000 = taffy.GetLayout(node000);
        Assert.Equal(48f, layout_node000.Size.Width);
        Assert.Equal(6f, layout_node000.Size.Height);
        Assert.Equal(10f, layout_node000.Location.X);
        Assert.Equal(10f, layout_node000.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(200f, layout_node1.Size.Width);
        Assert.Equal(148f, layout_node1.Size.Height);
        Assert.Equal(0f, layout_node1.Location.X);
        Assert.Equal(52f, layout_node1.Location.Y);
    }
}
