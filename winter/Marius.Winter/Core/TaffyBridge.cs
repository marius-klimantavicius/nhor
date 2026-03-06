using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Marius.Winter.Taffy;

namespace Marius.Winter;

/// <summary>
/// Manages a TaffyTree that mirrors the Element tree for CSS layout computation.
/// Each Element gets a NodeId in the tree. Layout containers (Panel, ScrollPanel)
/// configure Taffy styles via their ILayout property. Leaf elements use MeasureCore
/// as the measure function.
///
/// Layout containers call ComputeLayout(element, w, h) from their ArrangeCore/OnSizeChanged.
/// A re-entrancy guard prevents nested containers from redundantly re-computing
/// (when Taffy arranges a child container, that child's ArrangeCore will try to call
/// ComputeLayout again, but the guard skips it since the parent already computed it).
/// </summary>
internal class TaffyLayoutEngine
{
    private readonly TaffyTree<Element> _tree = new();
    private bool _computing; // re-entrancy guard

    /// <summary>
    /// Compute layout for the element tree rooted at 'root' with the given available space,
    /// then apply results (set Bounds) on all descendants.
    /// No-ops if already inside a ComputeLayout call (re-entrancy guard).
    /// </summary>
    public void ComputeLayout(Element root, float availableWidth, float availableHeight)
    {
        if (_computing) return;
        _computing = true;
        try
        {
            EnsureNode(root);
            SyncChildren(root);

            // Set root to exact given size so it fills the space allocated by its parent
            {
                var style = BuildStyle(root);
                style.SizeValue = new Size<Dimension>(
                    Dimension.FromLength(availableWidth),
                    Dimension.FromLength(availableHeight));
                _tree.SetStyle(root._taffyNode, style);
                root._taffyStyleDirty = false;
            }

            // Clear caches on all descendants so Taffy recomputes with the new
            // available space. MarkDirty only propagates upward, but when the
            // root's available size changes, children must also be recomputed.
            ClearDescendantCaches(root);

            var avail = new Size<AvailableSpace>(
                AvailableSpace.Definite(availableWidth),
                AvailableSpace.Definite(availableHeight));

            _tree.ComputeLayoutWithMeasure(root._taffyNode, avail, MeasureElement);

            // The SizeValue override we set on the root must not persist —
            // when this node is later queried as a *child* in a parent's flex
            // computation, it must report Size = Auto so that stretch works.
            root._taffyStyleDirty = true;

            // Done computing — clear guard so nested Panels (inside ManagesOwnChildLayout
            // containers like ScrollPanel) can trigger their own ComputeLayout from ArrangeCore.
            _computing = false;

            // Apply layout results to children (skip root — its bounds are set by the caller)
            ApplyLayout(root, 0, 0);
        }
        finally { _computing = false; }
    }

    /// <summary>
    /// Whether we're currently inside a ComputeLayout call.
    /// Used by controls to know if Taffy is driving layout.
    /// </summary>
    public bool IsComputing => _computing;

    private void ClearDescendantCaches(Element element)
    {
        foreach (var child in element.ChildrenMutable)
        {
            if (!child._taffyNodeValid) continue;
            _tree.MarkDirty(child._taffyNode);
            if (!child.ManagesOwnChildLayout)
                ClearDescendantCaches(child);
        }
    }

    /// <summary>
    /// Remove a node and all its descendants from the tree.
    /// </summary>
    public void RemoveNode(Element element)
    {
        if (!element._taffyNodeValid) return;
        foreach (var child in element.Children)
            RemoveNode(child);
        _tree.Remove(element._taffyNode);
        element._taffyNodeValid = false;
    }

    /// <summary>
    /// Clear the entire tree.
    /// </summary>
    public void Clear()
    {
        _tree.Clear();
    }

    private void EnsureNode(Element element)
    {
        if (!element._taffyNodeValid)
        {
            element._taffyNode = _tree.NewLeafWithContext(new Taffy.Style(), element);
            element._taffyNodeValid = true;
            element._taffyStyleDirty = true;
        }
    }

    private void SyncChildren(Element element)
    {
        // If this element manages its own child layout (ScrollPanel, DialogWindow, TabWidget, Button),
        // don't add children to the Taffy tree — treat it as a leaf.
        if (element.ManagesOwnChildLayout)
        {
            // Ensure no stale children remain in Taffy
            var existing = _tree.Children(element._taffyNode);
            if (existing.Count > 0)
                _tree.SetChildren(element._taffyNode, ReadOnlySpan<NodeId>.Empty);

            if (element._taffyStyleDirty)
            {
                var style = BuildStyle(element);
                _tree.SetStyle(element._taffyNode, style);
                element._taffyStyleDirty = false;
            }
            return;
        }

        var children = element.ChildrenMutable;
        var taffyChildren = _tree.Children(element._taffyNode);

        // Check if children match — fast path: same count and same nodes in order
        bool childrenMatch = children.Count == taffyChildren.Count;
        if (childrenMatch)
        {
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                EnsureNode(child);
                if (child._taffyNode != taffyChildren[i])
                {
                    childrenMatch = false;
                    break;
                }
            }
        }
        else
        {
            for (int i = 0; i < children.Count; i++)
                EnsureNode(children[i]);
        }

        if (!childrenMatch)
        {
            Span<NodeId> childNodes = children.Count <= 16
                ? stackalloc NodeId[children.Count]
                : new NodeId[children.Count];

            for (int i = 0; i < children.Count; i++)
                childNodes[i] = children[i]._taffyNode;

            _tree.SetChildren(element._taffyNode, childNodes);
        }

        // Sync style if dirty
        if (element._taffyStyleDirty)
        {
            var style = BuildStyle(element);
            _tree.SetStyle(element._taffyNode, style);
            element._taffyStyleDirty = false;
        }

        // Recurse into children
        for (int i = 0; i < children.Count; i++)
            SyncChildren(children[i]);
    }

    private static Taffy.Style BuildStyle(Element element)
    {
        var s = new Taffy.Style();

        // Override CSS default min-size: auto → 0. Our elements don't text-wrap and
        // should be allowed to size below content width (matching old FlexLayout behavior).
        s.MinSizeValue = new Size<Dimension>(Dimension.FromLength(0), Dimension.FromLength(0));

        // Apply explicit min/max constraints from Element
        if (element.MinWidth.HasValue)
            s.MinSizeValue = new Size<Dimension>(Dimension.FromLength(element.MinWidth.Value), s.MinSizeValue.Height);
        if (element.MinHeight.HasValue)
            s.MinSizeValue = new Size<Dimension>(s.MinSizeValue.Width, Dimension.FromLength(element.MinHeight.Value));
        if (element.MaxWidth.HasValue)
            s.MaxSizeValue = new Size<Dimension>(Dimension.FromLength(element.MaxWidth.Value), s.MaxSizeValue.Height);
        if (element.MaxHeight.HasValue)
            s.MaxSizeValue = new Size<Dimension>(s.MaxSizeValue.Width, Dimension.FromLength(element.MaxHeight.Value));

        // Check if parent has a layout — apply per-child layout data
        var parent = element.Parent;
        if (parent is ILayoutContainer lc && lc.Layout != null)
        {
            var layout = lc.Layout;
            if (layout is FlexLayout)
                ApplyFlexChildStyle(s, element);
            else if (layout is GridLayout)
                ApplyGridChildStyle(s, element);
        }

        // If this element is a layout container, apply container style
        if (element is ILayoutContainer container && container.Layout != null)
        {
            var layout = container.Layout;
            if (layout is FlexLayout flex)
                ApplyFlexContainerStyle(s, flex, element);
            else if (layout is GridLayout grid)
                ApplyGridContainerStyle(s, grid, element);
            else if (layout is StackLayout stack)
                ApplyStackContainerStyle(s, stack, element);
        }

        return s;
    }

    private static void ApplyFlexChildStyle(Taffy.Style s, Element element)
    {
        var hint = element.LayoutData as FlexItem;
        if (hint == null) return;

        s.FlexGrowValue = hint.Grow;
        s.FlexShrinkValue = hint.Shrink;
        if (!float.IsNaN(hint.Basis))
            s.FlexBasisValue = Dimension.FromLength(hint.Basis);

        if (hint.AlignSelf.HasValue)
            s.AlignSelfValue = ToTaffyAlignItems(hint.AlignSelf.Value);
    }

    private static void ApplyGridChildStyle(Taffy.Style s, Element element)
    {
        var hint = element.LayoutData as GridItem;
        if (hint == null) return;

        s.GridColumn = new Line<GridPlacement>(
            GridPlacement.FromLine((short)(hint.Column + 1)),
            GridPlacement.FromSpanCount((ushort)Math.Max(1, hint.ColumnSpan)));
        s.GridRow = new Line<GridPlacement>(
            GridPlacement.FromLine((short)(hint.Row + 1)),
            GridPlacement.FromSpanCount((ushort)Math.Max(1, hint.RowSpan)));

        s.AlignSelfValue = ToTaffyAlignItems(hint.VerticalAlignment);
        s.JustifySelfValue = ToTaffyAlignItems(hint.HorizontalAlignment);
    }

    private static void ApplyFlexContainerStyle(Taffy.Style s, FlexLayout flex, Element element)
    {
        s.Display = Display.Flex;
        s.FlexDirectionValue = flex.Direction == Orientation.Horizontal
            ? FlexDirection.Row : FlexDirection.Column;
        s.FlexWrapValue = flex.Wrap == FlexWrap.Wrap
            ? Taffy.FlexWrap.Wrap : Taffy.FlexWrap.NoWrap;
        s.JustifyContentValue = ToTaffyJustify(flex.JustifyContent);
        s.AlignItemsValue = ToTaffyAlignItems(flex.AlignItems);
        s.GapValue = new Size<LengthPercentage>(
            LengthPercentage.FromLength(flex.Gap),
            LengthPercentage.FromLength(flex.Gap));

        ApplyPadding(s, element);
    }

    private static void ApplyGridContainerStyle(Taffy.Style s, GridLayout grid, Element element)
    {
        s.Display = Display.Grid;

        var colsBuilder = ImmutableList.CreateBuilder<GridTemplateComponent>();
        foreach (var t in grid.Columns)
        {
            colsBuilder.Add(t.Type switch
            {
                TrackSize.TrackType.Fixed => GridTemplateComponent.FromLength(t.Value),
                TrackSize.TrackType.Auto => GridTemplateComponent.AutoComponent(),
                TrackSize.TrackType.Fraction => GridTemplateComponent.FromFr(t.Value),
                _ => GridTemplateComponent.AutoComponent()
            });
        }
        s.GridTemplateColumns = colsBuilder.ToImmutable();

        var rowsBuilder = ImmutableList.CreateBuilder<GridTemplateComponent>();
        foreach (var t in grid.Rows)
        {
            rowsBuilder.Add(t.Type switch
            {
                TrackSize.TrackType.Fixed => GridTemplateComponent.FromLength(t.Value),
                TrackSize.TrackType.Auto => GridTemplateComponent.AutoComponent(),
                TrackSize.TrackType.Fraction => GridTemplateComponent.FromFr(t.Value),
                _ => GridTemplateComponent.AutoComponent()
            });
        }
        s.GridTemplateRows = rowsBuilder.ToImmutable();

        s.GapValue = new Size<LengthPercentage>(
            LengthPercentage.FromLength(grid.ColumnGap),
            LengthPercentage.FromLength(grid.RowGap));

        ApplyPadding(s, element);
    }

    private static void ApplyStackContainerStyle(Taffy.Style s, StackLayout stack, Element element)
    {
        s.Display = Display.Flex;
        s.FlexDirectionValue = stack.Orientation == Orientation.Horizontal
            ? FlexDirection.Row : FlexDirection.Column;
        s.FlexWrapValue = Taffy.FlexWrap.NoWrap;
        s.AlignItemsValue = ToTaffyAlignItems(stack.CrossAlignment);
        s.GapValue = new Size<LengthPercentage>(
            LengthPercentage.FromLength(stack.Spacing),
            LengthPercentage.FromLength(stack.Spacing));

        ApplyPadding(s, element);
    }

    private static void ApplyPadding(Taffy.Style s, Element element)
    {
        var p = element.Style.Padding;
        s.PaddingValue = new Rect<LengthPercentage>(
            LengthPercentage.FromLength(p.Left),
            LengthPercentage.FromLength(p.Right),
            LengthPercentage.FromLength(p.Top),
            LengthPercentage.FromLength(p.Bottom));
    }

    private void ApplyLayout(Element element, float offsetX, float offsetY)
    {
        // Don't apply Taffy layout to children of elements that manage their own layout
        if (element.ManagesOwnChildLayout) return;

        var children = element.ChildrenMutable;
        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (!child._taffyNodeValid) continue;

            var layout = _tree.GetLayout(child._taffyNode);
            float x = offsetX + layout.Location.X;
            float y = offsetY + layout.Location.Y;
            float w = layout.Size.Width;
            float h = layout.Size.Height;

            child.Arrange(new RectF(x, y, w, h));

            // If the child is a layout container (Panel with Layout), its ArrangeCore
            // already ran its own ComputeLayout + ApplyLayout. Don't recurse into it
            // again — that would override children with stale positions from this computation.
            if (child is ILayoutContainer lc && lc.Layout != null)
                continue;

            // Recurse — children positions are relative to parent
            ApplyLayout(child, 0, 0);
        }
    }


    private static Size<float> MeasureElement(
        Size<float?> knownDimensions,
        Size<AvailableSpace> availableSpace,
        NodeId nodeId,
        Element? element,
        Taffy.Style style)
    {
        if (element == null) return SizeExtensions.ZeroF32;

        float availW = knownDimensions.Width ?? availableSpace.Width.UnwrapOr(float.PositiveInfinity);
        float availH = knownDimensions.Height ?? availableSpace.Height.UnwrapOr(float.PositiveInfinity);

        var sz = element.Measure(availW, availH);

        return new Size<float>(
            knownDimensions.Width ?? sz.X,
            knownDimensions.Height ?? sz.Y);
    }

    // --- Enum conversions ---

    private static AlignItems ToTaffyAlignItems(Alignment a) => a switch
    {
        Alignment.Start => AlignItems.Start,
        Alignment.Center => AlignItems.Center,
        Alignment.End => AlignItems.End,
        Alignment.Stretch => AlignItems.Stretch,
        _ => AlignItems.Stretch,
    };

    private static AlignContent ToTaffyJustify(JustifyContent jc) => jc switch
    {
        JustifyContent.Start => AlignContent.Start,
        JustifyContent.Center => AlignContent.Center,
        JustifyContent.End => AlignContent.End,
        JustifyContent.SpaceBetween => AlignContent.SpaceBetween,
        JustifyContent.SpaceAround => AlignContent.SpaceAround,
        JustifyContent.SpaceEvenly => AlignContent.SpaceEvenly,
        _ => AlignContent.Start,
    };
}
