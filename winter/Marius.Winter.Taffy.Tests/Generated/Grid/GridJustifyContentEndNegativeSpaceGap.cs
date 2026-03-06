using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridJustifyContentEndNegativeSpaceGap
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style());
        var node01 = taffy.NewLeaf(new Style());
        var node02 = taffy.NewLeaf(new Style());
        var node03 = taffy.NewLeaf(new Style());
        var node04 = taffy.NewLeaf(new Style());
        var node05 = taffy.NewLeaf(new Style());
        var node06 = taffy.NewLeaf(new Style());
        var node07 = taffy.NewLeaf(new Style());
        var node08 = taffy.NewLeaf(new Style());
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            AlignContentValue = AlignContent.Center,
            JustifyContentValue = AlignContent.End,
            GapValue = new Size<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(20f), GridTemplateComponent.FromLength(20f), GridTemplateComponent.FromLength(20f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(120f), Dimension.FromLength(120f)),
        }, new NodeId[] { node00, node01, node02, node03, node04, node05, node06, node07, node08 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            SizeValue = new Size<Dimension>(Dimension.FromLength(240f), Dimension.FromLength(240f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(60f), LengthPercentage.Length(60f), LengthPercentage.Length(60f), LengthPercentage.Length(60f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(240f, layout_node.Size.Width);
        Assert.Equal(240f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(120f, layout_node0.Size.Width);
        Assert.Equal(120f, layout_node0.Size.Height);
        Assert.Equal(60f, layout_node0.Location.X);
        Assert.Equal(60f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(40f, layout_node00.Size.Width);
        Assert.Equal(20f, layout_node00.Size.Height);
        Assert.Equal(-20f, layout_node00.Location.X);
        Assert.Equal(20f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(40f, layout_node01.Size.Width);
        Assert.Equal(20f, layout_node01.Size.Height);
        Assert.Equal(30f, layout_node01.Location.X);
        Assert.Equal(20f, layout_node01.Location.Y);
        var layout_node02 = taffy.GetLayout(node02);
        Assert.Equal(40f, layout_node02.Size.Width);
        Assert.Equal(20f, layout_node02.Size.Height);
        Assert.Equal(80f, layout_node02.Location.X);
        Assert.Equal(20f, layout_node02.Location.Y);
        var layout_node03 = taffy.GetLayout(node03);
        Assert.Equal(40f, layout_node03.Size.Width);
        Assert.Equal(20f, layout_node03.Size.Height);
        Assert.Equal(-20f, layout_node03.Location.X);
        Assert.Equal(50f, layout_node03.Location.Y);
        var layout_node04 = taffy.GetLayout(node04);
        Assert.Equal(40f, layout_node04.Size.Width);
        Assert.Equal(20f, layout_node04.Size.Height);
        Assert.Equal(30f, layout_node04.Location.X);
        Assert.Equal(50f, layout_node04.Location.Y);
        var layout_node05 = taffy.GetLayout(node05);
        Assert.Equal(40f, layout_node05.Size.Width);
        Assert.Equal(20f, layout_node05.Size.Height);
        Assert.Equal(80f, layout_node05.Location.X);
        Assert.Equal(50f, layout_node05.Location.Y);
        var layout_node06 = taffy.GetLayout(node06);
        Assert.Equal(40f, layout_node06.Size.Width);
        Assert.Equal(20f, layout_node06.Size.Height);
        Assert.Equal(-20f, layout_node06.Location.X);
        Assert.Equal(80f, layout_node06.Location.Y);
        var layout_node07 = taffy.GetLayout(node07);
        Assert.Equal(40f, layout_node07.Size.Width);
        Assert.Equal(20f, layout_node07.Size.Height);
        Assert.Equal(30f, layout_node07.Location.X);
        Assert.Equal(80f, layout_node07.Location.Y);
        var layout_node08 = taffy.GetLayout(node08);
        Assert.Equal(40f, layout_node08.Size.Width);
        Assert.Equal(20f, layout_node08.Size.Height);
        Assert.Equal(80f, layout_node08.Location.X);
        Assert.Equal(80f, layout_node08.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node01 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node02 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node03 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node04 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node05 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node06 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node07 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node08 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            AlignContentValue = AlignContent.Center,
            JustifyContentValue = AlignContent.End,
            GapValue = new Size<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(20f), GridTemplateComponent.FromLength(20f), GridTemplateComponent.FromLength(20f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f), GridTemplateComponent.FromLength(40f)),
            SizeValue = new Size<Dimension>(Dimension.FromLength(120f), Dimension.FromLength(120f)),
        }, new NodeId[] { node00, node01, node02, node03, node04, node05, node06, node07, node08 });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(240f), Dimension.FromLength(240f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(60f), LengthPercentage.Length(60f), LengthPercentage.Length(60f), LengthPercentage.Length(60f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(360f, layout_node.Size.Width);
        Assert.Equal(360f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(120f, layout_node0.Size.Width);
        Assert.Equal(120f, layout_node0.Size.Height);
        Assert.Equal(60f, layout_node0.Location.X);
        Assert.Equal(60f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(40f, layout_node00.Size.Width);
        Assert.Equal(20f, layout_node00.Size.Height);
        Assert.Equal(-20f, layout_node00.Location.X);
        Assert.Equal(20f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(40f, layout_node01.Size.Width);
        Assert.Equal(20f, layout_node01.Size.Height);
        Assert.Equal(30f, layout_node01.Location.X);
        Assert.Equal(20f, layout_node01.Location.Y);
        var layout_node02 = taffy.GetLayout(node02);
        Assert.Equal(40f, layout_node02.Size.Width);
        Assert.Equal(20f, layout_node02.Size.Height);
        Assert.Equal(80f, layout_node02.Location.X);
        Assert.Equal(20f, layout_node02.Location.Y);
        var layout_node03 = taffy.GetLayout(node03);
        Assert.Equal(40f, layout_node03.Size.Width);
        Assert.Equal(20f, layout_node03.Size.Height);
        Assert.Equal(-20f, layout_node03.Location.X);
        Assert.Equal(50f, layout_node03.Location.Y);
        var layout_node04 = taffy.GetLayout(node04);
        Assert.Equal(40f, layout_node04.Size.Width);
        Assert.Equal(20f, layout_node04.Size.Height);
        Assert.Equal(30f, layout_node04.Location.X);
        Assert.Equal(50f, layout_node04.Location.Y);
        var layout_node05 = taffy.GetLayout(node05);
        Assert.Equal(40f, layout_node05.Size.Width);
        Assert.Equal(20f, layout_node05.Size.Height);
        Assert.Equal(80f, layout_node05.Location.X);
        Assert.Equal(50f, layout_node05.Location.Y);
        var layout_node06 = taffy.GetLayout(node06);
        Assert.Equal(40f, layout_node06.Size.Width);
        Assert.Equal(20f, layout_node06.Size.Height);
        Assert.Equal(-20f, layout_node06.Location.X);
        Assert.Equal(80f, layout_node06.Location.Y);
        var layout_node07 = taffy.GetLayout(node07);
        Assert.Equal(40f, layout_node07.Size.Width);
        Assert.Equal(20f, layout_node07.Size.Height);
        Assert.Equal(30f, layout_node07.Location.X);
        Assert.Equal(80f, layout_node07.Location.Y);
        var layout_node08 = taffy.GetLayout(node08);
        Assert.Equal(40f, layout_node08.Size.Width);
        Assert.Equal(20f, layout_node08.Size.Height);
        Assert.Equal(80f, layout_node08.Location.X);
        Assert.Equal(80f, layout_node08.Location.Y);
    }
}
