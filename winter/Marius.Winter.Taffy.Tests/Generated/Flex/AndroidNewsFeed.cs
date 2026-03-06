using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.Flex;

public class AndroidNewsFeed
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node000000 = taffy.NewLeaf(new Style
        {
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(120f), Dimension.FromLength(120f)),
        });
        var node00000 = taffy.NewWithChildren(new Style
        {
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 0f,
        }, new NodeId[] { node000000 });
        var node000010 = taffy.NewLeaf(new Style
        {
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 1f,
        });
        var node000011 = taffy.NewLeaf(new Style
        {
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 1f,
        });
        var node00001 = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 1f,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(36f), LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(36f), LengthPercentage.Length(36f), LengthPercentage.Length(21f), LengthPercentage.Length(18f)),
        }, new NodeId[] { node000010, node000011 });
        var node0000 = taffy.NewWithChildren(new Style
        {
            AlignItemsValue = AlignItems.FlexStart,
            AlignContentValue = AlignContent.Stretch,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(36f), LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(24f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node00000, node00001 });
        var node000 = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            AlignContentValue = AlignContent.Stretch,
        }, new NodeId[] { node0000 });
        var node001000 = taffy.NewLeaf(new Style
        {
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(72f), Dimension.FromLength(72f)),
        });
        var node00100 = taffy.NewWithChildren(new Style
        {
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 0f,
        }, new NodeId[] { node001000 });
        var node001010 = taffy.NewLeaf(new Style
        {
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 1f,
        });
        var node001011 = taffy.NewLeaf(new Style
        {
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 1f,
        });
        var node00101 = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 1f,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(36f), LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(36f), LengthPercentage.Length(36f), LengthPercentage.Length(21f), LengthPercentage.Length(18f)),
        }, new NodeId[] { node001010, node001011 });
        var node0010 = taffy.NewWithChildren(new Style
        {
            AlignItemsValue = AlignItems.FlexStart,
            AlignContentValue = AlignContent.Stretch,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(174f), LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(24f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node00100, node00101 });
        var node001 = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            AlignContentValue = AlignContent.Stretch,
        }, new NodeId[] { node0010 });
        var node00 = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            AlignContentValue = AlignContent.Stretch,
        }, new NodeId[] { node000, node001 });
        var node0 = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            FlexShrinkValue = 0f,
        }, new NodeId[] { node00 });
        var node = taffy.NewWithChildren(new Style
        {
            FlexDirectionValue = FlexDirection.Column,
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(1080f), Dimension.Auto()),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(1080f, layout_node.Size.Width);
        Assert.Equal(240f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(1080f, layout_node0.Size.Width);
        Assert.Equal(240f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(1080f, layout_node00.Size.Width);
        Assert.Equal(240f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node000 = taffy.GetLayout(node000);
        Assert.Equal(1080f, layout_node000.Size.Width);
        Assert.Equal(144f, layout_node000.Size.Height);
        Assert.Equal(0f, layout_node000.Location.X);
        Assert.Equal(0f, layout_node000.Location.Y);
        var layout_node0000 = taffy.GetLayout(node0000);
        Assert.Equal(1044f, layout_node0000.Size.Width);
        Assert.Equal(120f, layout_node0000.Size.Height);
        Assert.Equal(36f, layout_node0000.Location.X);
        Assert.Equal(24f, layout_node0000.Location.Y);
        var layout_node00000 = taffy.GetLayout(node00000);
        Assert.Equal(120f, layout_node00000.Size.Width);
        Assert.Equal(120f, layout_node00000.Size.Height);
        Assert.Equal(0f, layout_node00000.Location.X);
        Assert.Equal(0f, layout_node00000.Location.Y);
        var layout_node000000 = taffy.GetLayout(node000000);
        Assert.Equal(120f, layout_node000000.Size.Width);
        Assert.Equal(120f, layout_node000000.Size.Height);
        Assert.Equal(0f, layout_node000000.Location.X);
        Assert.Equal(0f, layout_node000000.Location.Y);
        var layout_node00001 = taffy.GetLayout(node00001);
        Assert.Equal(72f, layout_node00001.Size.Width);
        Assert.Equal(39f, layout_node00001.Size.Height);
        Assert.Equal(120f, layout_node00001.Location.X);
        Assert.Equal(0f, layout_node00001.Location.Y);
        var layout_node000010 = taffy.GetLayout(node000010);
        Assert.Equal(0f, layout_node000010.Size.Width);
        Assert.Equal(0f, layout_node000010.Size.Height);
        Assert.Equal(36f, layout_node000010.Location.X);
        Assert.Equal(21f, layout_node000010.Location.Y);
        var layout_node000011 = taffy.GetLayout(node000011);
        Assert.Equal(0f, layout_node000011.Size.Width);
        Assert.Equal(0f, layout_node000011.Size.Height);
        Assert.Equal(36f, layout_node000011.Location.X);
        Assert.Equal(21f, layout_node000011.Location.Y);
        var layout_node001 = taffy.GetLayout(node001);
        Assert.Equal(1080f, layout_node001.Size.Width);
        Assert.Equal(96f, layout_node001.Size.Height);
        Assert.Equal(0f, layout_node001.Location.X);
        Assert.Equal(144f, layout_node001.Location.Y);
        var layout_node0010 = taffy.GetLayout(node0010);
        Assert.Equal(906f, layout_node0010.Size.Width);
        Assert.Equal(72f, layout_node0010.Size.Height);
        Assert.Equal(174f, layout_node0010.Location.X);
        Assert.Equal(24f, layout_node0010.Location.Y);
        var layout_node00100 = taffy.GetLayout(node00100);
        Assert.Equal(72f, layout_node00100.Size.Width);
        Assert.Equal(72f, layout_node00100.Size.Height);
        Assert.Equal(0f, layout_node00100.Location.X);
        Assert.Equal(0f, layout_node00100.Location.Y);
        var layout_node001000 = taffy.GetLayout(node001000);
        Assert.Equal(72f, layout_node001000.Size.Width);
        Assert.Equal(72f, layout_node001000.Size.Height);
        Assert.Equal(0f, layout_node001000.Location.X);
        Assert.Equal(0f, layout_node001000.Location.Y);
        var layout_node00101 = taffy.GetLayout(node00101);
        Assert.Equal(72f, layout_node00101.Size.Width);
        Assert.Equal(39f, layout_node00101.Size.Height);
        Assert.Equal(72f, layout_node00101.Location.X);
        Assert.Equal(0f, layout_node00101.Location.Y);
        var layout_node001010 = taffy.GetLayout(node001010);
        Assert.Equal(0f, layout_node001010.Size.Width);
        Assert.Equal(0f, layout_node001010.Size.Height);
        Assert.Equal(36f, layout_node001010.Location.X);
        Assert.Equal(21f, layout_node001010.Location.Y);
        var layout_node001011 = taffy.GetLayout(node001011);
        Assert.Equal(0f, layout_node001011.Size.Width);
        Assert.Equal(0f, layout_node001011.Size.Height);
        Assert.Equal(36f, layout_node001011.Location.X);
        Assert.Equal(21f, layout_node001011.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node000000 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(120f), Dimension.FromLength(120f)),
        });
        var node00000 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 0f,
        }, new NodeId[] { node000000 });
        var node000010 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 1f,
        });
        var node000011 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 1f,
        });
        var node00001 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 1f,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(36f), LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(36f), LengthPercentage.Length(36f), LengthPercentage.Length(21f), LengthPercentage.Length(18f)),
        }, new NodeId[] { node000010, node000011 });
        var node0000 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignItemsValue = AlignItems.FlexStart,
            AlignContentValue = AlignContent.Stretch,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(36f), LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(24f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node00000, node00001 });
        var node000 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            AlignContentValue = AlignContent.Stretch,
        }, new NodeId[] { node0000 });
        var node001000 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(72f), Dimension.FromLength(72f)),
        });
        var node00100 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 0f,
        }, new NodeId[] { node001000 });
        var node001010 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 1f,
        });
        var node001011 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 1f,
        });
        var node00101 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 1f,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(36f), LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO),
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(36f), LengthPercentage.Length(36f), LengthPercentage.Length(21f), LengthPercentage.Length(18f)),
        }, new NodeId[] { node001010, node001011 });
        var node0010 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            AlignItemsValue = AlignItems.FlexStart,
            AlignContentValue = AlignContent.Stretch,
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(174f), LengthPercentageAuto.ZERO, LengthPercentageAuto.Length(24f), LengthPercentageAuto.ZERO),
        }, new NodeId[] { node00100, node00101 });
        var node001 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            AlignContentValue = AlignContent.Stretch,
        }, new NodeId[] { node0010 });
        var node00 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            AlignContentValue = AlignContent.Stretch,
        }, new NodeId[] { node000, node001 });
        var node0 = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            FlexShrinkValue = 0f,
        }, new NodeId[] { node00 });
        var node = taffy.NewWithChildren(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FlexDirectionValue = FlexDirection.Column,
            AlignContentValue = AlignContent.Stretch,
            FlexShrinkValue = 0f,
            SizeValue = new Size<Dimension>(Dimension.FromLength(1080f), Dimension.Auto()),
        }, new NodeId[] { node0 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(1080f, layout_node.Size.Width);
        Assert.Equal(240f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(1080f, layout_node0.Size.Width);
        Assert.Equal(240f, layout_node0.Size.Height);
        Assert.Equal(0f, layout_node0.Location.X);
        Assert.Equal(0f, layout_node0.Location.Y);
        var layout_node00 = taffy.GetLayout(node00);
        Assert.Equal(1080f, layout_node00.Size.Width);
        Assert.Equal(240f, layout_node00.Size.Height);
        Assert.Equal(0f, layout_node00.Location.X);
        Assert.Equal(0f, layout_node00.Location.Y);
        var layout_node000 = taffy.GetLayout(node000);
        Assert.Equal(1080f, layout_node000.Size.Width);
        Assert.Equal(144f, layout_node000.Size.Height);
        Assert.Equal(0f, layout_node000.Location.X);
        Assert.Equal(0f, layout_node000.Location.Y);
        var layout_node0000 = taffy.GetLayout(node0000);
        Assert.Equal(1044f, layout_node0000.Size.Width);
        Assert.Equal(120f, layout_node0000.Size.Height);
        Assert.Equal(36f, layout_node0000.Location.X);
        Assert.Equal(24f, layout_node0000.Location.Y);
        var layout_node00000 = taffy.GetLayout(node00000);
        Assert.Equal(120f, layout_node00000.Size.Width);
        Assert.Equal(120f, layout_node00000.Size.Height);
        Assert.Equal(0f, layout_node00000.Location.X);
        Assert.Equal(0f, layout_node00000.Location.Y);
        var layout_node000000 = taffy.GetLayout(node000000);
        Assert.Equal(120f, layout_node000000.Size.Width);
        Assert.Equal(120f, layout_node000000.Size.Height);
        Assert.Equal(0f, layout_node000000.Location.X);
        Assert.Equal(0f, layout_node000000.Location.Y);
        var layout_node00001 = taffy.GetLayout(node00001);
        Assert.Equal(72f, layout_node00001.Size.Width);
        Assert.Equal(39f, layout_node00001.Size.Height);
        Assert.Equal(120f, layout_node00001.Location.X);
        Assert.Equal(0f, layout_node00001.Location.Y);
        var layout_node000010 = taffy.GetLayout(node000010);
        Assert.Equal(0f, layout_node000010.Size.Width);
        Assert.Equal(0f, layout_node000010.Size.Height);
        Assert.Equal(36f, layout_node000010.Location.X);
        Assert.Equal(21f, layout_node000010.Location.Y);
        var layout_node000011 = taffy.GetLayout(node000011);
        Assert.Equal(0f, layout_node000011.Size.Width);
        Assert.Equal(0f, layout_node000011.Size.Height);
        Assert.Equal(36f, layout_node000011.Location.X);
        Assert.Equal(21f, layout_node000011.Location.Y);
        var layout_node001 = taffy.GetLayout(node001);
        Assert.Equal(1080f, layout_node001.Size.Width);
        Assert.Equal(96f, layout_node001.Size.Height);
        Assert.Equal(0f, layout_node001.Location.X);
        Assert.Equal(144f, layout_node001.Location.Y);
        var layout_node0010 = taffy.GetLayout(node0010);
        Assert.Equal(906f, layout_node0010.Size.Width);
        Assert.Equal(72f, layout_node0010.Size.Height);
        Assert.Equal(174f, layout_node0010.Location.X);
        Assert.Equal(24f, layout_node0010.Location.Y);
        var layout_node00100 = taffy.GetLayout(node00100);
        Assert.Equal(72f, layout_node00100.Size.Width);
        Assert.Equal(72f, layout_node00100.Size.Height);
        Assert.Equal(0f, layout_node00100.Location.X);
        Assert.Equal(0f, layout_node00100.Location.Y);
        var layout_node001000 = taffy.GetLayout(node001000);
        Assert.Equal(72f, layout_node001000.Size.Width);
        Assert.Equal(72f, layout_node001000.Size.Height);
        Assert.Equal(0f, layout_node001000.Location.X);
        Assert.Equal(0f, layout_node001000.Location.Y);
        var layout_node00101 = taffy.GetLayout(node00101);
        Assert.Equal(72f, layout_node00101.Size.Width);
        Assert.Equal(39f, layout_node00101.Size.Height);
        Assert.Equal(72f, layout_node00101.Location.X);
        Assert.Equal(0f, layout_node00101.Location.Y);
        var layout_node001010 = taffy.GetLayout(node001010);
        Assert.Equal(0f, layout_node001010.Size.Width);
        Assert.Equal(0f, layout_node001010.Size.Height);
        Assert.Equal(36f, layout_node001010.Location.X);
        Assert.Equal(21f, layout_node001010.Location.Y);
        var layout_node001011 = taffy.GetLayout(node001011);
        Assert.Equal(0f, layout_node001011.Size.Width);
        Assert.Equal(0f, layout_node001011.Size.Height);
        Assert.Equal(36f, layout_node001011.Location.X);
        Assert.Equal(21f, layout_node001011.Location.Y);
    }
}
