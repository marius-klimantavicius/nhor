using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests;

public class MeasureTests
{
    [Fact]
    public void MeasureRoot()
    {
        var taffy = NewTestTree();
        var node = taffy.NewLeafWithContext(new Style(), TestNodeContext.Fixed(100, 100));
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);

        Assert.Equal(100f, taffy.GetLayout(node).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(node).Size.Height);
    }

    [Fact]
    public void MeasureChild()
    {
        var taffy = NewTestTree();
        var child = taffy.NewLeafWithContext(new Style(), TestNodeContext.Fixed(100, 100));
        var node = taffy.NewWithChildren(new Style(), new[] { child });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);

        Assert.Equal(100f, taffy.GetLayout(node).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(node).Size.Height);
        Assert.Equal(100f, taffy.GetLayout(child).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(child).Size.Height);
    }

    [Fact]
    public void MeasureChildConstraint()
    {
        var taffy = NewTestTree();
        var child = taffy.NewLeafWithContext(new Style(), TestNodeContext.Fixed(100, 100));
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.Auto()),
        }, new[] { child });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);

        Assert.Equal(50f, taffy.GetLayout(node).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(node).Size.Height);
        Assert.Equal(100f, taffy.GetLayout(child).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(child).Size.Height);
    }

    [Fact]
    public void MeasureChildConstraintPaddingParent()
    {
        var taffy = NewTestTree();
        var child = taffy.NewLeafWithContext(new Style(), TestNodeContext.Fixed(100, 100));
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.Auto()),
            PaddingValue = new Rect<LengthPercentage>(
                LengthPercentage.FromLength(10f), LengthPercentage.FromLength(10f),
                LengthPercentage.FromLength(10f), LengthPercentage.FromLength(10f)),
        }, new[] { child });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);

        Assert.Equal(0f, taffy.GetLayout(node).Location.X);
        Assert.Equal(0f, taffy.GetLayout(node).Location.Y);
        Assert.Equal(50f, taffy.GetLayout(node).Size.Width);
        Assert.Equal(120f, taffy.GetLayout(node).Size.Height);

        Assert.Equal(10f, taffy.GetLayout(child).Location.X);
        Assert.Equal(10f, taffy.GetLayout(child).Location.Y);
        Assert.Equal(100f, taffy.GetLayout(child).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(child).Size.Height);
    }

    [Fact]
    public void MeasureChildWithFlexGrow()
    {
        var taffy = NewTestTree();
        var child0 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
        });
        var child1 = taffy.NewLeafWithContext(new Style { FlexGrowValue = 1f }, TestNodeContext.Fixed(50, 50));
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.Auto()),
        }, new[] { child0, child1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);

        Assert.Equal(50f, taffy.GetLayout(child1).Size.Width);
        Assert.Equal(50f, taffy.GetLayout(child1).Size.Height);
    }

    [Fact]
    public void MeasureChildWithFlexShrink()
    {
        var taffy = NewTestTree();
        var child0 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
            FlexShrinkValue = 0f,
        });
        var child1 = taffy.NewLeafWithContext(new Style(), TestNodeContext.Fixed(100, 50));
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.Auto()),
        }, new[] { child0, child1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);

        Assert.Equal(100f, taffy.GetLayout(child1).Size.Width);
        Assert.Equal(50f, taffy.GetLayout(child1).Size.Height);
    }

    [Fact]
    public void RemeasureChildAfterGrowing()
    {
        var taffy = NewTestTree();
        var child0 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
        });
        var child1 = taffy.NewLeafWithContext(new Style { FlexGrowValue = 1f },
            TestNodeContext.AspectRatio(10f, 2f));
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.Auto()),
            AlignItemsValue = AlignItems.Start,
        }, new[] { child0, child1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);

        Assert.Equal(50f, taffy.GetLayout(child1).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(child1).Size.Height);
    }

    [Fact]
    public void RemeasureChildAfterShrinking()
    {
        var taffy = NewTestTree();
        var child0 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
            FlexShrinkValue = 0f,
        });
        var child1 = taffy.NewLeafWithContext(new Style(), TestNodeContext.AspectRatio(100f, 2f));
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.Auto()),
            AlignItemsValue = AlignItems.Start,
        }, new[] { child0, child1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);

        Assert.Equal(100f, taffy.GetLayout(child1).Size.Width);
        Assert.Equal(200f, taffy.GetLayout(child1).Size.Height);
    }

    [Fact]
    public void RemeasureChildAfterStretching()
    {
        var taffy = new TaffyTree<TestNodeContext>();
        var child = taffy.NewLeafWithContext(new Style(), TestNodeContext.Zero());
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new[] { child });

        // Custom measure: width = known_width ?? height, height = known_height ?? 50
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize,
            (known, avail, nid, ctx, style) =>
            {
                var height = known.Height ?? 50f;
                var width = known.Width ?? height;
                return new Size<float>(width, height);
            });

        Assert.Equal(100f, taffy.GetLayout(child).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(child).Size.Height);
    }

    [Fact]
    public void WidthOverridesMeasure()
    {
        var taffy = NewTestTree();
        var child = taffy.NewLeafWithContext(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.Auto()),
        }, TestNodeContext.Fixed(100, 100));
        var node = taffy.NewWithChildren(new Style(), new[] { child });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);

        Assert.Equal(50f, taffy.GetLayout(child).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(child).Size.Height);
    }

    [Fact]
    public void HeightOverridesMeasure()
    {
        var taffy = NewTestTree();
        var child = taffy.NewLeafWithContext(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.FromLength(50f)),
        }, TestNodeContext.Fixed(100, 100));
        var node = taffy.NewWithChildren(new Style(), new[] { child });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);

        Assert.Equal(100f, taffy.GetLayout(child).Size.Width);
        Assert.Equal(50f, taffy.GetLayout(child).Size.Height);
    }

    [Fact]
    public void FlexBasisOverridesMeasure()
    {
        var taffy = NewTestTree();
        var child0 = taffy.NewLeaf(new Style
        {
            FlexBasisValue = Dimension.FromLength(50f),
            FlexGrowValue = 1f,
        });
        var child1 = taffy.NewLeafWithContext(new Style
        {
            FlexBasisValue = Dimension.FromLength(50f),
            FlexGrowValue = 1f,
        }, TestNodeContext.Fixed(100, 100));
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(100f)),
        }, new[] { child0, child1 });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);

        Assert.Equal(100f, taffy.GetLayout(child0).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(child0).Size.Height);
        Assert.Equal(100f, taffy.GetLayout(child1).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(child1).Size.Height);
    }

    [Fact]
    public void StretchOverridesMeasure()
    {
        var taffy = NewTestTree();
        var child = taffy.NewLeafWithContext(new Style(), TestNodeContext.Fixed(50, 50));
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new[] { child });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);

        Assert.Equal(50f, taffy.GetLayout(child).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(child).Size.Height);
    }

    [Fact]
    public void MeasureAbsoluteChild()
    {
        var taffy = NewTestTree();
        var child = taffy.NewLeafWithContext(new Style
        {
            PositionValue = Position.Absolute,
        }, TestNodeContext.Fixed(50, 50));
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new[] { child });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);

        Assert.Equal(50f, taffy.GetLayout(child).Size.Width);
        Assert.Equal(50f, taffy.GetLayout(child).Size.Height);
    }

    [Fact]
    public void IgnoreInvalidMeasure()
    {
        var taffy = NewTestTree();
        var child = taffy.NewLeaf(new Style { FlexGrowValue = 1f });
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new[] { child });
        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);

        Assert.Equal(100f, taffy.GetLayout(child).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(child).Size.Height);
    }
}
