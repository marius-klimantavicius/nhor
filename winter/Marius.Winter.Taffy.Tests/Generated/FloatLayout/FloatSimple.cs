using System.Collections.Generic;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests.Generated.FloatLayout;

public class FloatSimple
{
    [Fact]
    public void BorderBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            FloatValue = Float.Right,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            FloatValue = Float.Right,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            FloatValue = Float.Right,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
        });
        var node3 = taffy.NewLeaf(new Style
        {
            FloatValue = Float.Right,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
        });
        var node4 = taffy.NewLeaf(new Style
        {
            FloatValue = Float.Right,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(300f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(2f), LengthPercentage.Length(2f), LengthPercentage.Length(2f), LengthPercentage.Length(2f)),
        }, new NodeId[] { node0, node1, node2, node3, node4 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(254f, layout_node.Size.Width);
        Assert.Equal(300f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(50f, layout_node0.Size.Width);
        Assert.Equal(50f, layout_node0.Size.Height);
        Assert.Equal(202f, layout_node0.Location.X);
        Assert.Equal(2f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(50f, layout_node1.Size.Width);
        Assert.Equal(50f, layout_node1.Size.Height);
        Assert.Equal(152f, layout_node1.Location.X);
        Assert.Equal(2f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(50f, layout_node2.Size.Width);
        Assert.Equal(50f, layout_node2.Size.Height);
        Assert.Equal(102f, layout_node2.Location.X);
        Assert.Equal(2f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(50f, layout_node3.Size.Width);
        Assert.Equal(50f, layout_node3.Size.Height);
        Assert.Equal(52f, layout_node3.Location.X);
        Assert.Equal(2f, layout_node3.Location.Y);
        var layout_node4 = taffy.GetLayout(node4);
        Assert.Equal(50f, layout_node4.Size.Width);
        Assert.Equal(50f, layout_node4.Size.Height);
        Assert.Equal(2f, layout_node4.Location.X);
        Assert.Equal(2f, layout_node4.Location.Y);
    }

    [Fact]
    public void ContentBox()
    {
        var taffy = NewTestTree();
        var node0 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FloatValue = Float.Right,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
        });
        var node1 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FloatValue = Float.Right,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
        });
        var node2 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FloatValue = Float.Right,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
        });
        var node3 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FloatValue = Float.Right,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
        });
        var node4 = taffy.NewLeaf(new Style
        {
            BoxSizingValue = BoxSizing.ContentBox,
            FloatValue = Float.Right,
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
        });
        var node = taffy.NewWithChildren(new Style
        {
            Display = Display.Block,
            BoxSizingValue = BoxSizing.ContentBox,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(300f)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(2f), LengthPercentage.Length(2f), LengthPercentage.Length(2f), LengthPercentage.Length(2f)),
        }, new NodeId[] { node0, node1, node2, node3, node4 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);
        var layout_node = taffy.GetLayout(node);
        Assert.Equal(254f, layout_node.Size.Width);
        Assert.Equal(304f, layout_node.Size.Height);
        Assert.Equal(0f, layout_node.Location.X);
        Assert.Equal(0f, layout_node.Location.Y);
        var layout_node0 = taffy.GetLayout(node0);
        Assert.Equal(50f, layout_node0.Size.Width);
        Assert.Equal(50f, layout_node0.Size.Height);
        Assert.Equal(202f, layout_node0.Location.X);
        Assert.Equal(2f, layout_node0.Location.Y);
        var layout_node1 = taffy.GetLayout(node1);
        Assert.Equal(50f, layout_node1.Size.Width);
        Assert.Equal(50f, layout_node1.Size.Height);
        Assert.Equal(152f, layout_node1.Location.X);
        Assert.Equal(2f, layout_node1.Location.Y);
        var layout_node2 = taffy.GetLayout(node2);
        Assert.Equal(50f, layout_node2.Size.Width);
        Assert.Equal(50f, layout_node2.Size.Height);
        Assert.Equal(102f, layout_node2.Location.X);
        Assert.Equal(2f, layout_node2.Location.Y);
        var layout_node3 = taffy.GetLayout(node3);
        Assert.Equal(50f, layout_node3.Size.Width);
        Assert.Equal(50f, layout_node3.Size.Height);
        Assert.Equal(52f, layout_node3.Location.X);
        Assert.Equal(2f, layout_node3.Location.Y);
        var layout_node4 = taffy.GetLayout(node4);
        Assert.Equal(50f, layout_node4.Size.Width);
        Assert.Equal(50f, layout_node4.Size.Height);
        Assert.Equal(2f, layout_node4.Location.X);
        Assert.Equal(2f, layout_node4.Location.Y);
    }
}
