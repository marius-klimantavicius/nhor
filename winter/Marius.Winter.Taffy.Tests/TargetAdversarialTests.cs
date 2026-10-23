using System;
using System.Collections.Immutable;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

// Direct translations of tests/hand_written/adversarial_styles.rs.
public class TargetAdversarialTests
{
    private static readonly Size<AvailableSpace> Definite = new(AvailableSpace.Definite(100), AvailableSpace.Definite(100));
    private static GridTemplateComponent Repeat(ushort count, params MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>[] tracks) => GridTemplateComponent.FromRepeat(new(RepetitionCount.Count(count), tracks.ToImmutableList(), ImmutableList<ImmutableList<string>>.Empty));
    private static GridTemplateComponent Px(float value) => GridTemplateComponent.FromLength(value);

    [Fact]
    public void NonFiniteFlexFactorsTerminate()
    {
        foreach (float factor in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            var tree = new TaffyTree<object>();
            var child = tree.NewLeaf(new Style { SizeValue = new(Dimension.Length(10), Dimension.Length(10)) });
            var root = tree.NewWithChildren(new Style { Display = Display.Grid, GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromFr(factor)) }, new[] { child });
            tree.ComputeLayout(root, Definite);
        }
    }
    [Fact]
    public void NonFiniteGridSizesTerminate()
    {
        foreach (float width in new[] { float.NaN, float.PositiveInfinity })
        {
            var tree = new TaffyTree<object>(); var child = tree.NewLeaf(new Style());
            var root = tree.NewWithChildren(new Style { Display = Display.Grid, SizeValue = new(Dimension.Length(width), Dimension.Length(100)), GridTemplateColumns = ImmutableList.Create(Repeat(2, TrackSizingFunctionExtensions.FromFr(1))) }, new[] { child });
            tree.ComputeLayout(root, Definite);
        }
    }
    [Fact]
    public void RepetitionWithoutNamesStillAdvancesLineNumbers()
    {
        var tree = new TaffyTree<object>();
        var child = tree.NewLeaf(new Style { SizeValue = new(Dimension.Length(5), Dimension.Length(5)), GridColumn = new(GridPlacement.FromNamedLine("c", 1), GridPlacement.Auto) });
        var root = tree.NewWithChildren(new Style { Display = Display.Grid, SizeValue = new(Dimension.Length(100), Dimension.Length(100)),
            GridTemplateColumns = ImmutableList.Create(Repeat(2, TrackSizingFunctionExtensions.FromLength(10)), Px(10)),
            GridTemplateColumnNames = ImmutableList.Create(ImmutableList<string>.Empty, ImmutableList<string>.Empty, ImmutableList.Create("c")) }, new[] { child });
        tree.ComputeLayout(root, Definite); Assert.Equal(30f, tree.GetLayout(child).Location.X);
    }
    [Fact]
    public void ZeroSpanAndMissingNamedSpanTerminate()
    {
        foreach (var direction in new[] { Direction.Ltr, Direction.Rtl })
        {
            var tree = new TaffyTree<object>();
            var child = tree.NewLeaf(new Style { GridColumn = new(GridPlacement.FromNamedSpan("does-not-exist", 1), GridPlacement.FromSpanCount(0)) });
            var root = tree.NewWithChildren(new Style { Display = Display.Grid, DirectionValue = direction }, new[] { child });
            tree.ComputeLayout(root, Definite);
        }
    }
    [Fact]
    public void WrongRepetitionNameSetCountThrows()
    {
        var tree = new TaffyTree<object>(); var child = tree.NewLeaf(new Style());
        var root = tree.NewWithChildren(new Style { Display = Display.Grid,
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromRepeat(new(RepetitionCount.Count(2), ImmutableList.Create(TrackSizingFunctionExtensions.FromLength(10)), ImmutableList.Create(ImmutableList.Create("a"))))),
            GridTemplateColumnNames = ImmutableList.Create(ImmutableList<string>.Empty, ImmutableList<string>.Empty) }, new[] { child });
        Assert.Throws<ArgumentException>(() => tree.ComputeLayout(root, Definite));
    }
    [Fact]
    public void SmallItemJumpsPastMaximumOccupiedGrid()
    {
        var tree = new TaffyTree<object>();
        var big = tree.NewLeaf(new Style { GridRow = new(GridPlacement.FromSpanCount(10000), GridPlacement.Auto), GridColumn = new(GridPlacement.FromSpanCount(10000), GridPlacement.Auto) });
        var small = tree.NewLeaf(new Style { GridRow = new(GridPlacement.FromSpanCount(10000), GridPlacement.Auto), GridColumn = new(GridPlacement.FromSpanCount(1), GridPlacement.Auto) });
        var root = tree.NewWithChildren(new Style { Display = Display.Grid, SizeValue = new(Dimension.Length(100), Dimension.Length(100)) }, new[] { big, small });
        tree.ComputeLayout(root, Definite);
    }
    [Fact]
    public void ExtremePlacementsAndSpansTerminate()
    {
        var tree = new TaffyTree<object>();
        var a = new Line<GridPlacement>(GridPlacement.Auto, GridPlacement.FromSpanCount(2000));
        var b = new Line<GridPlacement>(GridPlacement.FromLine(-2000), GridPlacement.FromLine(2000));
        var children = new[] { tree.NewLeaf(new Style { GridRow = a, GridColumn = a }), tree.NewLeaf(new Style { GridRow = b, GridColumn = b }) };
        var root = tree.NewWithChildren(new Style { Display = Display.Grid, SizeValue = new(Dimension.Length(100), Dimension.Length(100)) }, children);
        tree.ComputeLayout(root, Definite);
    }
}
