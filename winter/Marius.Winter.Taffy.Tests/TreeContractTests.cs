using System;
using System.Runtime.CompilerServices;
using Marius.Winter.Taffy;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

public class TreeContractTests
{
    [Fact]
    public void RemoveMarksParentAndAncestorsDirtyAndShrinksLayout()
    {
        var tree = new TaffyTree<object>();
        var child = tree.NewLeaf(new Style { BorderValue = new Rect<LengthPercentage>(
            LengthPercentage.ZERO, LengthPercentage.ZERO, LengthPercentage.ZERO, LengthPercentage.Length(1)) });
        var parent = tree.NewWithChildren(new Style(), new[] { child });
        var root = tree.NewWithChildren(new Style(), new[] { parent });
        var available = new Size<AvailableSpace>(AvailableSpace.MinContent, AvailableSpace.MinContent);
        tree.ComputeLayout(root, available);
        Assert.Equal(1f, tree.GetLayout(root).Size.Height);
        Assert.False(tree.Dirty(parent));
        Assert.False(tree.Dirty(root));
        tree.Remove(child);
        Assert.True(tree.Dirty(parent));
        Assert.True(tree.Dirty(root));
        tree.ComputeLayout(root, available);
        Assert.Equal(0f, tree.GetLayout(root).Size.Height);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (NodeId, WeakReference) AddContext(TaffyTree<object> tree)
    {
        object context = new object();
        return (tree.NewLeafWithContext(new Style(), context), new WeakReference(context));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovedAndClearedNodeContextsAreReleased(bool clear)
    {
        var tree = new TaffyTree<object>();
        var references = new WeakReference[8];
        for (int i = 0; i < references.Length; i++)
        {
            var (node, reference) = AddContext(tree);
            references[i] = reference;
            if (!clear) tree.Remove(node);
        }
        if (clear) tree.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        foreach (var reference in references) Assert.False(reference.IsAlive);
        GC.KeepAlive(tree);
    }

    [Fact]
    public void LayoutOutputCallbackPreservesCustomBaselineForFlexAlignment()
    {
        var tree = new TaffyTree<float>();
        var first = tree.NewLeafWithContext(new Style(), 4f);
        var second = tree.NewLeafWithContext(new Style(), 12f);
        var root = tree.NewWithChildren(new Style { AlignItemsValue = AlignItems.Baseline }, new[] { first, second });
        tree.ComputeLayoutWithMeasure(root,
            new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent),
            (input, id, context, style) => LayoutOutput.FromSizesAndBaselines(
                new Size<float>(20, 20), new Size<float>(20, 20), new Point<float?>(null, context)));
        Assert.Equal(8f, tree.GetLayout(first).Location.Y);
        Assert.Equal(0f, tree.GetLayout(second).Location.Y);
        Assert.Equal(28f, tree.GetLayout(root).Size.Height);
    }

    [Fact]
    public void TransposingInputsPreservesConstraintsAndResetsMarginCollapsing()
    {
        var input = new LayoutInput
        {
            Axis = RequestedAxis.Horizontal,
            RunMode = RunMode.ComputeSize,
            SizingMode = SizingMode.ContentSize,
            KnownDimensions = new Size<float?>(10, null),
            KnownDimensionsAreDefinite = new Size<bool>(false, true),
            ParentSize = new Size<float?>(50, 70),
            AvailableSpace = new Size<AvailableSpace>(AvailableSpace.MinContent, AvailableSpace.Definite(80)),
            VerticalMarginsAreCollapsible = new Line<bool>(true, true),
        };
        var transposed = input.Transpose();
        Assert.Equal(RequestedAxis.Vertical, transposed.Axis);
        Assert.Equal(input.RunMode, transposed.RunMode);
        Assert.Equal(input.SizingMode, transposed.SizingMode);
        Assert.Equal(new Size<float?>(null, 10), transposed.KnownDimensions);
        Assert.Equal(new Size<bool>(true, false), (Size<bool>)transposed.KnownDimensionsAreDefinite);
        Assert.Equal(new Size<float?>(70, 50), transposed.ParentSize);
        Assert.Equal(new Size<AvailableSpace>(AvailableSpace.Definite(80), AvailableSpace.MinContent), transposed.AvailableSpace);
        Assert.Equal(new Line<bool>(false, false), transposed.VerticalMarginsAreCollapsible);
    }
}
