using System.Collections.Generic;
using System.Collections.Immutable;
using System;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridPercentItemsWidthAndMargin2Col
{
    private static void AssertClose(float expected, float actual) =>
        Assert.True(MathF.Abs(actual - expected) < 0.1f);

    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        taffy.DisableRounding();
        var node0 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Ltr,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.45f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            DirectionValue = Direction.Ltr,
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.AutoComponent(), GridTemplateComponent.AutoComponent()),
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.Auto()),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        AssertClose(200f, layout_node.Size.Width);
        AssertClose(16.28125f, layout_node.Size.Height);
        AssertClose(0f, layout_node.Location.X);
        AssertClose(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        AssertClose(46.34375f, layout_node0.Size.Width);
        AssertClose(6f, layout_node0.Size.Height);
        AssertClose(5.140625f, layout_node0.Location.X);
        AssertClose(5.140625f, layout_node0.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        taffy.DisableRounding();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.45f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Ltr,
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.AutoComponent(), GridTemplateComponent.AutoComponent()),
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.Auto()),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        AssertClose(200f, layout_node.Size.Width);
        AssertClose(16.28125f, layout_node.Size.Height);
        AssertClose(0f, layout_node.Location.X);
        AssertClose(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        AssertClose(52.34375f, layout_node0.Size.Width);
        AssertClose(6f, layout_node0.Size.Height);
        AssertClose(5.140625f, layout_node0.Location.X);
        AssertClose(5.140625f, layout_node0.Location.Y);
    }

    [Fact]
    public void BorderBoxRtl()
    {
        var taffy = NewTestTree();
        taffy.DisableRounding();
        var node0 = taffy.NewLeaf(new Style
        {
            DirectionValue = Direction.Rtl,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.45f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            DirectionValue = Direction.Rtl,
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.AutoComponent(), GridTemplateComponent.AutoComponent()),
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.Auto()),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        AssertClose(200f, layout_node.Size.Width);
        AssertClose(16.28125f, layout_node.Size.Height);
        AssertClose(0f, layout_node.Location.X);
        AssertClose(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        AssertClose(46.34375f, layout_node0.Size.Width);
        AssertClose(6f, layout_node0.Size.Height);
        AssertClose(148.51563f, layout_node0.Location.X);
        AssertClose(5.140625f, layout_node0.Location.Y);
    }

    [Fact]
    public void ContentBoxRtl()
    {
        var taffy = NewTestTree();
        taffy.DisableRounding();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(0.45f), Dimension.Auto()),
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f), LengthPercentageAuto.Percent(0.05f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f), LengthPercentage.Length(3f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            DirectionValue = Direction.Rtl,
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.AutoComponent(), GridTemplateComponent.AutoComponent()),
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.Auto()),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent), MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        AssertClose(200f, layout_node.Size.Width);
        AssertClose(16.28125f, layout_node.Size.Height);
        AssertClose(0f, layout_node.Location.X);
        AssertClose(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        AssertClose(52.34375f, layout_node0.Size.Width);
        AssertClose(6f, layout_node0.Size.Height);
        AssertClose(142.51563f, layout_node0.Location.X);
        AssertClose(5.140625f, layout_node0.Location.Y);
    }
}
