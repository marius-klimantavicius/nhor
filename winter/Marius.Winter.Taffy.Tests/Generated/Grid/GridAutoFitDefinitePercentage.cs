using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Grid;

public class GridAutoFitDefinitePercentage
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
        });
        var node01 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
        });
        var node02 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
        });
        var node03 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
        });
        var node04 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
        });
        var node05 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
        });
        var node06 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
        });
        var node07 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            GapValue = new Size<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromRepeat(new GridTemplateRepetition(RepetitionCount.AutoFill, ImmutableList.Create(new MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>(MinTrackSizingFunction.Length(150f), MaxTrackSizingFunction.Fr(1f)))))),
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(1f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
        }, new NodeId[] { node00, node01, node02, node03, node04, node05, node06, node07 });
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(730f), Dimension.FromLength(300f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(730f, layout_node.Size.Width);
        Assert.Equal(300f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(730f, layout_node0.Size.Width);
        Assert.Equal(300f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(170f, layout_node00.Size.Width);
        Assert.Equal(135f, layout_node00.Size.Height);
        Assert.Equal(10f, layout_node00.Location.X);
        Assert.Equal(10f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(170f, layout_node01.Size.Width);
        Assert.Equal(135f, layout_node01.Size.Height);
        Assert.Equal(190f, layout_node01.Location.X);
        Assert.Equal(10f, layout_node01.Location.Y);
        var layout_node02 = taffy.GetLayout(node02);
        Assert.Equal(170f, layout_node02.Size.Width);
        Assert.Equal(135f, layout_node02.Size.Height);
        Assert.Equal(370f, layout_node02.Location.X);
        Assert.Equal(10f, layout_node02.Location.Y);
        var layout_node03 = taffy.GetLayout(node03);
        Assert.Equal(170f, layout_node03.Size.Width);
        Assert.Equal(135f, layout_node03.Size.Height);
        Assert.Equal(550f, layout_node03.Location.X);
        Assert.Equal(10f, layout_node03.Location.Y);
        var layout_node04 = taffy.GetLayout(node04);
        Assert.Equal(170f, layout_node04.Size.Width);
        Assert.Equal(135f, layout_node04.Size.Height);
        Assert.Equal(10f, layout_node04.Location.X);
        Assert.Equal(155f, layout_node04.Location.Y);
        var layout_node05 = taffy.GetLayout(node05);
        Assert.Equal(170f, layout_node05.Size.Width);
        Assert.Equal(135f, layout_node05.Size.Height);
        Assert.Equal(190f, layout_node05.Location.X);
        Assert.Equal(155f, layout_node05.Location.Y);
        var layout_node06 = taffy.GetLayout(node06);
        Assert.Equal(170f, layout_node06.Size.Width);
        Assert.Equal(135f, layout_node06.Size.Height);
        Assert.Equal(370f, layout_node06.Location.X);
        Assert.Equal(155f, layout_node06.Location.Y);
        var layout_node07 = taffy.GetLayout(node07);
        Assert.Equal(170f, layout_node07.Size.Width);
        Assert.Equal(135f, layout_node07.Size.Height);
        Assert.Equal(550f, layout_node07.Location.X);
        Assert.Equal(155f, layout_node07.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node00 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node01 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node02 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node03 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node04 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node05 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node06 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node07 = taffy.NewLeaf(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            BoxSizingValue = BoxSizing.ContentBox,
            GapValue = new Size<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromRepeat(new GridTemplateRepetition(RepetitionCount.AutoFill, ImmutableList.Create(new MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>(MinTrackSizingFunction.Length(150f), MaxTrackSizingFunction.Fr(1f)))))),
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(1f)),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f), LengthPercentage.Length(10f)),
        }, new NodeId[] { node00, node01, node02, node03, node04, node05, node06, node07 });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.FromLength(730f), Dimension.FromLength(300f)),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(730f, layout_node.Size.Width);
        Assert.Equal(300f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(730f, layout_node0.Size.Width);
        Assert.Equal(320f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(170f, layout_node00.Size.Width);
        Assert.Equal(145f, layout_node00.Size.Height);
        Assert.Equal(10f, layout_node00.Location.X);
        Assert.Equal(10f, layout_node00.Location.Y);
        var layout_node01 = taffy.GetLayout(node01);
        Assert.Equal(170f, layout_node01.Size.Width);
        Assert.Equal(145f, layout_node01.Size.Height);
        Assert.Equal(190f, layout_node01.Location.X);
        Assert.Equal(10f, layout_node01.Location.Y);
        var layout_node02 = taffy.GetLayout(node02);
        Assert.Equal(170f, layout_node02.Size.Width);
        Assert.Equal(145f, layout_node02.Size.Height);
        Assert.Equal(370f, layout_node02.Location.X);
        Assert.Equal(10f, layout_node02.Location.Y);
        var layout_node03 = taffy.GetLayout(node03);
        Assert.Equal(170f, layout_node03.Size.Width);
        Assert.Equal(145f, layout_node03.Size.Height);
        Assert.Equal(550f, layout_node03.Location.X);
        Assert.Equal(10f, layout_node03.Location.Y);
        var layout_node04 = taffy.GetLayout(node04);
        Assert.Equal(170f, layout_node04.Size.Width);
        Assert.Equal(145f, layout_node04.Size.Height);
        Assert.Equal(10f, layout_node04.Location.X);
        Assert.Equal(165f, layout_node04.Location.Y);
        var layout_node05 = taffy.GetLayout(node05);
        Assert.Equal(170f, layout_node05.Size.Width);
        Assert.Equal(145f, layout_node05.Size.Height);
        Assert.Equal(190f, layout_node05.Location.X);
        Assert.Equal(165f, layout_node05.Location.Y);
        var layout_node06 = taffy.GetLayout(node06);
        Assert.Equal(170f, layout_node06.Size.Width);
        Assert.Equal(145f, layout_node06.Size.Height);
        Assert.Equal(370f, layout_node06.Location.X);
        Assert.Equal(165f, layout_node06.Location.Y);
        var layout_node07 = taffy.GetLayout(node07);
        Assert.Equal(170f, layout_node07.Size.Width);
        Assert.Equal(145f, layout_node07.Size.Height);
        Assert.Equal(550f, layout_node07.Location.X);
        Assert.Equal(165f, layout_node07.Location.Y);
    }
}
