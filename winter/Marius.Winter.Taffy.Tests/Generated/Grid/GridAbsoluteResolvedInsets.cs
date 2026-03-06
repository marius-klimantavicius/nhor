using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridAbsoluteResolvedInsets
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            PositionValue = Position.Absolute,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO),
        });
        var node01 = taffy.NewLeaf(new Style
        {
            PositionValue = Position.Absolute,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(0f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Length(0f), LengthPercentageAuto.AUTO),
        });
        var node02 = taffy.NewLeaf(new Style
        {
            PositionValue = Position.Absolute,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(1f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(1f), LengthPercentageAuto.AUTO),
        });
        var node03 = taffy.NewLeaf(new Style
        {
            PositionValue = Position.Absolute,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(1f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(1f)),
        });
        var node04 = taffy.NewLeaf(new Style
        {
            PositionValue = Position.Absolute,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(30f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Length(30f), LengthPercentageAuto.AUTO),
        });
        var node05 = taffy.NewLeaf(new Style
        {
            PositionValue = Position.Absolute,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(1f)),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(0f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Length(0f), LengthPercentageAuto.AUTO),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(200f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(15f), LengthPercentage.Length(15f), LengthPercentage.Length(15f), LengthPercentage.Length(15f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(20f), LengthPercentage.Length(20f), LengthPercentage.Length(20f), LengthPercentage.Length(20f)),
        }, new NodeId[] { node00, node01, node02, node03, node04, node05 });
        var node10 = taffy.NewLeaf(new Style
        {
            PositionValue = Position.Absolute,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO),
        });
        var node11 = taffy.NewLeaf(new Style
        {
            PositionValue = Position.Absolute,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(0f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Length(0f), LengthPercentageAuto.AUTO),
        });
        var node12 = taffy.NewLeaf(new Style
        {
            PositionValue = Position.Absolute,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(1f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(1f), LengthPercentageAuto.AUTO),
        });
        var node13 = taffy.NewLeaf(new Style
        {
            PositionValue = Position.Absolute,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(1f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(1f)),
        });
        var node14 = taffy.NewLeaf(new Style
        {
            PositionValue = Position.Absolute,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(30f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Length(30f), LengthPercentageAuto.AUTO),
        });
        var node15 = taffy.NewLeaf(new Style
        {
            PositionValue = Position.Absolute,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(1f)),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(0f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Length(0f), LengthPercentageAuto.AUTO),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            OverflowValue = new Point<Overflow>(Overflow.Scroll, Overflow.Scroll),
            ScrollbarWidthValue = 15f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(200f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(15f), LengthPercentage.Length(15f), LengthPercentage.Length(15f), LengthPercentage.Length(15f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(20f), LengthPercentage.Length(20f), LengthPercentage.Length(20f), LengthPercentage.Length(20f)),
        }, new NodeId[] { node10, node11, node12, node13, node14, node15 });
        var node = taffy.NewWithChildren(new Style(), new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(400f, layout_node.Size.Width);
        Assert.Equal(200f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(200f, layout_node0.Size.Width);
        Assert.Equal(200f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(0f, layout_node00.Size.Width);
        Assert.Equal(0f, layout_node00.Size.Height);
        Assert.Equal(20f, layout_node00.Location.X);
        Assert.Equal(20f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(0f, layout_node01.Size.Width);
        Assert.Equal(0f, layout_node01.Size.Height);
        Assert.Equal(20f, layout_node01.Location.X);
        Assert.Equal(20f, layout_node01.Location.Y);
        var layout_node02 = taffy.GetLayout(node02);
        Assert.Equal(0f, layout_node02.Size.Width);
        Assert.Equal(0f, layout_node02.Size.Height);
        Assert.Equal(180f, layout_node02.Location.X);
        Assert.Equal(180f, layout_node02.Location.Y);
        var layout_node03 = taffy.GetLayout(node03);
        Assert.Equal(0f, layout_node03.Size.Width);
        Assert.Equal(0f, layout_node03.Size.Height);
        Assert.Equal(20f, layout_node03.Location.X);
        Assert.Equal(20f, layout_node03.Location.Y);
        var layout_node04 = taffy.GetLayout(node04);
        Assert.Equal(0f, layout_node04.Size.Width);
        Assert.Equal(0f, layout_node04.Size.Height);
        Assert.Equal(50f, layout_node04.Location.X);
        Assert.Equal(50f, layout_node04.Location.Y);
        var layout_node05 = taffy.GetLayout(node05);
        Assert.Equal(160f, layout_node05.Size.Width);
        Assert.Equal(160f, layout_node05.Size.Height);
        Assert.Equal(20f, layout_node05.Location.X);
        Assert.Equal(20f, layout_node05.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(200f, layout_node1.Size.Width);
        Assert.Equal(200f, layout_node1.Size.Height);
        Assert.Equal(200f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        Assert.Equal(0f, layout_node1.ScrollWidth());
        Assert.Equal(0f, layout_node1.ScrollHeight());
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(0f, layout_node10.Size.Width);
        Assert.Equal(0f, layout_node10.Size.Height);
        Assert.Equal(20f, layout_node10.Location.X);
        Assert.Equal(20f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(0f, layout_node11.Size.Width);
        Assert.Equal(0f, layout_node11.Size.Height);
        Assert.Equal(20f, layout_node11.Location.X);
        Assert.Equal(20f, layout_node11.Location.Y);
        var layout_node12 = taffy.GetLayout(node12);
        Assert.Equal(0f, layout_node12.Size.Width);
        Assert.Equal(0f, layout_node12.Size.Height);
        Assert.Equal(165f, layout_node12.Location.X);
        Assert.Equal(165f, layout_node12.Location.Y);
        var layout_node13 = taffy.GetLayout(node13);
        Assert.Equal(0f, layout_node13.Size.Width);
        Assert.Equal(0f, layout_node13.Size.Height);
        Assert.Equal(20f, layout_node13.Location.X);
        Assert.Equal(20f, layout_node13.Location.Y);
        var layout_node14 = taffy.GetLayout(node14);
        Assert.Equal(0f, layout_node14.Size.Width);
        Assert.Equal(0f, layout_node14.Size.Height);
        Assert.Equal(50f, layout_node14.Location.X);
        Assert.Equal(50f, layout_node14.Location.Y);
        var layout_node15 = taffy.GetLayout(node15);
        Assert.Equal(145f, layout_node15.Size.Width);
        Assert.Equal(145f, layout_node15.Size.Height);
        Assert.Equal(20f, layout_node15.Location.X);
        Assert.Equal(20f, layout_node15.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            PositionValue = Position.Absolute,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO),
        });
        var node01 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            PositionValue = Position.Absolute,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(0f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Length(0f), LengthPercentageAuto.AUTO),
        });
        var node02 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            PositionValue = Position.Absolute,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(1f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(1f), LengthPercentageAuto.AUTO),
        });
        var node03 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            PositionValue = Position.Absolute,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(1f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(1f)),
        });
        var node04 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            PositionValue = Position.Absolute,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(30f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Length(30f), LengthPercentageAuto.AUTO),
        });
        var node05 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            PositionValue = Position.Absolute,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(1f)),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(0f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Length(0f), LengthPercentageAuto.AUTO),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(200f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(15f), LengthPercentage.Length(15f), LengthPercentage.Length(15f), LengthPercentage.Length(15f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(20f), LengthPercentage.Length(20f), LengthPercentage.Length(20f), LengthPercentage.Length(20f)),
        }, new NodeId[] { node00, node01, node02, node03, node04, node05 });
        var node10 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            PositionValue = Position.Absolute,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO),
        });
        var node11 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            PositionValue = Position.Absolute,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(0f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Length(0f), LengthPercentageAuto.AUTO),
        });
        var node12 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            PositionValue = Position.Absolute,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Percent(1f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(1f), LengthPercentageAuto.AUTO),
        });
        var node13 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            PositionValue = Position.Absolute,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(1f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Percent(1f)),
        });
        var node14 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            PositionValue = Position.Absolute,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(30f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Length(30f), LengthPercentageAuto.AUTO),
        });
        var node15 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            PositionValue = Position.Absolute,
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(1f)),
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(0f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Length(0f), LengthPercentageAuto.AUTO),
        });
        var node1 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            OverflowValue = new Point<Overflow>(Overflow.Scroll, Overflow.Scroll),
            ScrollbarWidthValue = 15f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(200f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(15f), LengthPercentage.Length(15f), LengthPercentage.Length(15f), LengthPercentage.Length(15f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(20f), LengthPercentage.Length(20f), LengthPercentage.Length(20f), LengthPercentage.Length(20f)),
        }, new NodeId[] { node10, node11, node12, node13, node14, node15 });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
        }, new NodeId[] { node0, node1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(540f, layout_node.Size.Width);
        Assert.Equal(270f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(270f, layout_node0.Size.Width);
        Assert.Equal(270f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(0f, layout_node00.Size.Width);
        Assert.Equal(0f, layout_node00.Size.Height);
        Assert.Equal(20f, layout_node00.Location.X);
        Assert.Equal(20f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(0f, layout_node01.Size.Width);
        Assert.Equal(0f, layout_node01.Size.Height);
        Assert.Equal(20f, layout_node01.Location.X);
        Assert.Equal(20f, layout_node01.Location.Y);
        var layout_node02 = taffy.GetLayout(node02);
        Assert.Equal(0f, layout_node02.Size.Width);
        Assert.Equal(0f, layout_node02.Size.Height);
        Assert.Equal(250f, layout_node02.Location.X);
        Assert.Equal(250f, layout_node02.Location.Y);
        var layout_node03 = taffy.GetLayout(node03);
        Assert.Equal(0f, layout_node03.Size.Width);
        Assert.Equal(0f, layout_node03.Size.Height);
        Assert.Equal(20f, layout_node03.Location.X);
        Assert.Equal(20f, layout_node03.Location.Y);
        var layout_node04 = taffy.GetLayout(node04);
        Assert.Equal(0f, layout_node04.Size.Width);
        Assert.Equal(0f, layout_node04.Size.Height);
        Assert.Equal(50f, layout_node04.Location.X);
        Assert.Equal(50f, layout_node04.Location.Y);
        var layout_node05 = taffy.GetLayout(node05);
        Assert.Equal(230f, layout_node05.Size.Width);
        Assert.Equal(230f, layout_node05.Size.Height);
        Assert.Equal(20f, layout_node05.Location.X);
        Assert.Equal(20f, layout_node05.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(270f, layout_node1.Size.Width);
        Assert.Equal(270f, layout_node1.Size.Height);
        Assert.Equal(270f, layout_node1.Location.X);
        Assert.Equal(0f, layout_node1.Location.Y);
        Assert.Equal(0f, layout_node1.ScrollWidth());
        Assert.Equal(0f, layout_node1.ScrollHeight());
        var layout_node10 = taffy.GetLayout(node10);
        Assert.Equal(0f, layout_node10.Size.Width);
        Assert.Equal(0f, layout_node10.Size.Height);
        Assert.Equal(20f, layout_node10.Location.X);
        Assert.Equal(20f, layout_node10.Location.Y);
        var layout_node11 = taffy.GetLayout(node11);
        Assert.Equal(0f, layout_node11.Size.Width);
        Assert.Equal(0f, layout_node11.Size.Height);
        Assert.Equal(20f, layout_node11.Location.X);
        Assert.Equal(20f, layout_node11.Location.Y);
        var layout_node12 = taffy.GetLayout(node12);
        Assert.Equal(0f, layout_node12.Size.Width);
        Assert.Equal(0f, layout_node12.Size.Height);
        Assert.Equal(235f, layout_node12.Location.X);
        Assert.Equal(235f, layout_node12.Location.Y);
        var layout_node13 = taffy.GetLayout(node13);
        Assert.Equal(0f, layout_node13.Size.Width);
        Assert.Equal(0f, layout_node13.Size.Height);
        Assert.Equal(20f, layout_node13.Location.X);
        Assert.Equal(20f, layout_node13.Location.Y);
        var layout_node14 = taffy.GetLayout(node14);
        Assert.Equal(0f, layout_node14.Size.Width);
        Assert.Equal(0f, layout_node14.Size.Height);
        Assert.Equal(50f, layout_node14.Location.X);
        Assert.Equal(50f, layout_node14.Location.Y);
        var layout_node15 = taffy.GetLayout(node15);
        Assert.Equal(215f, layout_node15.Size.Width);
        Assert.Equal(215f, layout_node15.Size.Height);
        Assert.Equal(20f, layout_node15.Location.X);
        Assert.Equal(20f, layout_node15.Location.Y);
    }
}
