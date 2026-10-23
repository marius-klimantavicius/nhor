using Marius.Winter.Taffy;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

public class BaselineContractTests
{
    [Fact]
    public void AChildWithAutoCrossMarginsDoesNotEnlargeTheBaselineAlignmentGroup()
    {
        var tree = new TaffyTree<float>();
        var first = tree.NewLeafWithContext(new Style(), 4f);
        var autoMargin = tree.NewLeafWithContext(new Style
        {
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO,
                LengthPercentageAuto.AUTO, LengthPercentageAuto.ZERO),
        }, 30f);
        var root = tree.NewWithChildren(new Style { AlignItemsValue = AlignItems.Baseline }, new[] { first, autoMargin });
        tree.ComputeLayoutWithMeasure(root, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent),
            (input, _, context, style) => LayoutOutput.FromSizesAndBaselines(
                new Size<float>(input.KnownDimensions.Width ?? 20, input.KnownDimensions.Height ?? (context > 10 ? 40 : 20)),
                SizeExtensions.ZeroF32, new Point<float?>(null, context)));
        Assert.Equal(0f, tree.GetLayout(first).Location.Y);
        Assert.Equal(40f, tree.GetLayout(root).Size.Height);
    }

    [Fact]
    public void ReversedColumnBaselineComesFromTheVisuallyStartmostItem()
    {
        var tree = new TaffyTree<float>();
        var first = tree.NewLeafWithContext(new Style(), 4f);
        var second = tree.NewLeafWithContext(new Style(), 12f);
        var column = tree.NewWithChildren(new Style { FlexDirectionValue = FlexDirection.ColumnReverse }, new[] { first, second });
        var sibling = tree.NewLeafWithContext(new Style(), 20f);
        var root = tree.NewWithChildren(new Style { AlignItemsValue = AlignItems.Baseline }, new[] { column, sibling });
        tree.ComputeLayoutWithMeasure(root, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent),
            (input, _, context, style) => LayoutOutput.FromSizesAndBaselines(
                new Size<float>(input.KnownDimensions.Width ?? 20, input.KnownDimensions.Height ?? 20),
                SizeExtensions.ZeroF32, new Point<float?>(null, context)));
        Assert.Equal(8f, tree.GetLayout(column).Location.Y);
        Assert.Equal(0f, tree.GetLayout(sibling).Location.Y);
        Assert.Equal(0f, tree.GetLayout(second).Location.Y);
        Assert.Equal(20f, tree.GetLayout(first).Location.Y);
        Assert.Equal(48f, tree.GetLayout(root).Size.Height);
    }

    [Theory]
    [InlineData(Overflow.Hidden, -5f, 0f)]
    [InlineData(Overflow.Hidden, 30f, 20f)]
    [InlineData(Overflow.Scroll, null, 20f)]
    [InlineData(Overflow.Clip, -5f, -5f)]
    [InlineData(Overflow.Visible, 30f, 30f)]
    [InlineData(Overflow.Visible, null, null)]
    public void ScrollBaselinesAreClampedAndSynthesized(Overflow overflow, float? reported, float? expected) =>
        Assert.Equal(expected, AlignmentUtils.ChildFirstBaseline(reported, 20, overflow));

    [Theory]
    [InlineData(0f)]
    [InlineData(7f)]
    public void BlockPropagatesItsFirstChildBaselineWithoutRelativeOffsets(float inset)
    {
        var tree = new TaffyTree<float>();
        var first = tree.NewLeafWithContext(new Style
        {
            PositionValue = Position.Relative,
            Inset = new Rect<LengthPercentageAuto>(LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO,
                LengthPercentageAuto.Length(inset), LengthPercentageAuto.AUTO),
        }, 4f);
        var block = tree.NewWithChildren(new Style
        {
            Display = Display.Block,
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.ZERO, LengthPercentage.ZERO,
                LengthPercentage.Length(3), LengthPercentage.ZERO),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.ZERO, LengthPercentage.ZERO,
                LengthPercentage.Length(1), LengthPercentage.ZERO),
        }, new[] { first });
        var second = tree.NewLeafWithContext(new Style(), 12f);
        var root = tree.NewWithChildren(new Style { AlignItemsValue = AlignItems.Baseline }, new[] { block, second });
        tree.ComputeLayoutWithMeasure(root, new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent),
            (input, _, context, style) => LayoutOutput.FromSizesAndBaselines(
                new Size<float>(20, 20), new Size<float>(20, 20), new Point<float?>(null, context)));
        Assert.Equal(4f, tree.GetLayout(block).Location.Y);
        Assert.Equal(0f, tree.GetLayout(second).Location.Y);
        Assert.Equal(4f + inset, tree.GetLayout(first).Location.Y);
        Assert.Equal(28f, tree.GetLayout(root).Size.Height);
    }
}
