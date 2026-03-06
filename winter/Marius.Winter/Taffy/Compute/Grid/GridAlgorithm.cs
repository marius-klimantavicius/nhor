// Ported from taffy/src/compute/grid/mod.rs
// This module is a partial implementation of the CSS Grid Level 1 specification
// https://www.w3.org/TR/css-grid-1

using System;
using System.Collections.Generic;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// Grid layout algorithm
    /// This consists of a few phases:
    ///   - Resolving the explicit grid
    ///   - Placing items (which also resolves the implicit grid)
    ///   - Track (row/column) sizing
    ///   - Alignment and Final item placement
    /// </summary>
    public static class GridAlgorithm
    {
        /// <summary>
        /// Grid layout algorithm entry point
        /// </summary>
        public static LayoutOutput ComputeGridLayout<TTree>(TTree tree, NodeId node, LayoutInput inputs)
            where TTree : ILayoutGridContainer
        {
            var knownDimensions = inputs.KnownDimensions;
            var parentSize = inputs.ParentSize;
            var availableSpace = inputs.AvailableSpace;
            var runMode = inputs.RunMode;

            var gridStyle = tree.GetGridContainerStyle(node);
            var style = (Style)gridStyle;

            // 1. Compute "available grid space"
            // https://www.w3.org/TR/css-grid-1/#available-grid-space
            var aspectRatio = gridStyle.AspectRatio();
            var padding = gridStyle.Padding().ResolveOrZero(parentSize.Width, (val, basis) => tree.Calc(val, basis));
            var border = gridStyle.Border().ResolveOrZero(parentSize.Width, (val, basis) => tree.Calc(val, basis));
            var paddingBorder = padding.Add(border);
            var paddingBorderSize = paddingBorder.SumAxes();
            var boxSizingAdjustment = gridStyle.BoxSizing() == BoxSizing.ContentBox ? paddingBorderSize : SizeExtensions.ZeroF32;
            var boxSizingAdj = boxSizingAdjustment.Map<float?>(v => v);

            var minSize = gridStyle.MinSize()
                .MaybeResolve(parentSize, (val, basis) => tree.Calc(val, basis))
                .MaybeApplyAspectRatio(aspectRatio)
                .MaybeAdd(boxSizingAdj);
            var maxSize = gridStyle.MaxSize()
                .MaybeResolve(parentSize, (val, basis) => tree.Calc(val, basis))
                .MaybeApplyAspectRatio(aspectRatio)
                .MaybeAdd(boxSizingAdj);

            Size<float?> preferredSize;
            if (inputs.SizingMode == SizingMode.InherentSize)
            {
                preferredSize = gridStyle.Size()
                    .MaybeResolve(parentSize, (val, basis) => tree.Calc(val, basis))
                    .MaybeApplyAspectRatio(gridStyle.AspectRatio())
                    .MaybeAdd(boxSizingAdj);
            }
            else
            {
                preferredSize = SizeExtensions.NoneF32;
            }

            // Scrollbar gutters are reserved when the overflow property is set to Overflow.Scroll.
            // However, the axes are switched (transposed) because a node that scrolls vertically needs
            // *horizontal* space to be reserved for a scrollbar
            var overflowVal = gridStyle.Overflow();
            var scrollbarGutter = new Size<float>
            {
                Width = overflowVal.Y == Overflow.Scroll ? gridStyle.ScrollbarWidth() : 0f,
                Height = overflowVal.X == Overflow.Scroll ? gridStyle.ScrollbarWidth() : 0f,
            };

            // TODO: make side configurable based on the `direction` property
            var contentBoxInset = paddingBorder;
            contentBoxInset.Right += scrollbarGutter.Width;
            contentBoxInset.Bottom += scrollbarGutter.Height;

            var alignContent = gridStyle.AlignContent() ?? AlignContent.Stretch;
            var justifyContent = gridStyle.JustifyContent() ?? AlignContent.Stretch;
            var alignItems = gridStyle.AlignItems();
            var justifyItems = gridStyle.JustifyItems();

            var kdOrPref = knownDimensions.Or(preferredSize);
            var constrainedAvailableSpace = new Size<AvailableSpace>
            {
                Width = kdOrPref.Width.HasValue ? Taffy.AvailableSpace.Definite(kdOrPref.Width.Value) : availableSpace.Width,
                Height = kdOrPref.Height.HasValue ? Taffy.AvailableSpace.Definite(kdOrPref.Height.Value) : availableSpace.Height,
            }.MaybeClamp(minSize, maxSize)
             .MaybeMax(paddingBorderSize);

            var availableGridSpace = new Size<AvailableSpace>
            {
                Width = constrainedAvailableSpace.Width.MapDefiniteValue(space => space - contentBoxInset.HorizontalAxisSum()),
                Height = constrainedAvailableSpace.Height.MapDefiniteValue(space => space - contentBoxInset.VerticalAxisSum()),
            };

            var outerNodeSize = knownDimensions.Or(preferredSize).MaybeClamp(minSize, maxSize).MaybeMax(paddingBorderSize.Map<float?>(v => v));
            var innerNodeSize = new Size<float?>
            {
                Width = outerNodeSize.Width.HasValue ? outerNodeSize.Width.Value - contentBoxInset.HorizontalAxisSum() : null,
                Height = outerNodeSize.Height.HasValue ? outerNodeSize.Height.Value - contentBoxInset.VerticalAxisSum() : null,
            };

            if (runMode == RunMode.ComputeSize && outerNodeSize.Width.HasValue && outerNodeSize.Height.HasValue)
            {
                return LayoutOutput.FromOuterSize(new Size<float> { Width = outerNodeSize.Width.Value, Height = outerNodeSize.Height.Value });
            }

            // 2. Resolve the explicit grid
            var autoFitContainerSize = outerNodeSize
                .Or(maxSize)
                .Or(minSize)
                .MaybeClamp(minSize, maxSize)
                .MaybeMax(paddingBorderSize.Map<float?>(v => v))
                .MaybeSub(contentBoxInset.SumAxes().Map<float?>(v => v));

            var autoRepeatFitStrategy = new Size<AutoRepeatStrategy>
            {
                Width = outerNodeSize.Or(maxSize).Width.HasValue
                    ? AutoRepeatStrategy.MaxRepetitionsThatDoNotOverflow
                    : AutoRepeatStrategy.MinRepetitionsThatDoOverflow,
                Height = outerNodeSize.Or(maxSize).Height.HasValue
                    ? AutoRepeatStrategy.MaxRepetitionsThatDoNotOverflow
                    : AutoRepeatStrategy.MinRepetitionsThatDoOverflow,
            };

            // Compute the number of rows and columns in the explicit grid *template*
            var (colAutoRepetitionCount, gridTemplateColCount) = ExplicitGridUtils.ComputeExplicitGridSizeInAxis(
                style, autoFitContainerSize.Width, autoRepeatFitStrategy.Width,
                (val, basis) => tree.Calc(val, basis), AbsoluteAxis.Horizontal);
            var (rowAutoRepetitionCount, gridTemplateRowCount) = ExplicitGridUtils.ComputeExplicitGridSizeInAxis(
                style, autoFitContainerSize.Height, autoRepeatFitStrategy.Height,
                (val, basis) => tree.Calc(val, basis), AbsoluteAxis.Vertical);

            var nameResolver = new NamedLineResolver(style, colAutoRepetitionCount, rowAutoRepetitionCount);

            var explicitColCount = Math.Max(gridTemplateColCount, nameResolver.AreaColumnCount);
            var explicitRowCount = Math.Max(gridTemplateRowCount, nameResolver.AreaRowCount);

            nameResolver.SetExplicitColumnCount(explicitColCount);
            nameResolver.SetExplicitRowCount(explicitRowCount);

            // 3. Implicit Grid: Estimate Track Counts
            IEnumerable<Style> ChildStylesIter()
            {
                foreach (var childNode in tree.ChildIds(node))
                    yield return (Style)tree.GetGridChildStyle(childNode);
            }
            var (estColCounts, estRowCounts) = ImplicitGridUtils.ComputeGridSizeEstimate(explicitColCount, explicitRowCount, ChildStylesIter());

            // 4. Grid Item Placement
            var items = new List<GridItem>(tree.ChildCount(node));
            var cellOccupancyMatrix = CellOccupancyMatrix.WithTrackCounts(estColCounts, estRowCounts);

            IEnumerable<(int index, NodeId node, Style style)> InFlowChildrenIter()
            {
                int idx = 0;
                foreach (var childNode in tree.ChildIds(node))
                {
                    var childStyleI = tree.GetGridChildStyle(childNode);
                    var childStyle = (Style)childStyleI;
                    if (childStyleI.BoxGenerationMode() != BoxGenerationMode.None && ((ICoreStyle)childStyle).Position() != Position.Absolute)
                    {
                        yield return (idx, childNode, childStyle);
                    }
                    idx++;
                }
            }

            PlacementUtils.PlaceGridItems(
                cellOccupancyMatrix,
                items,
                () => InFlowChildrenIter(),
                gridStyle.GridAutoFlow(),
                alignItems ?? AlignItems.Stretch,
                justifyItems ?? AlignItems.Stretch,
                nameResolver);

            // Extract track counts from previous step (auto-placement can expand the number of tracks)
            var finalColCounts = cellOccupancyMatrix.GetTrackCounts(AbsoluteAxis.Horizontal);
            var finalRowCounts = cellOccupancyMatrix.GetTrackCounts(AbsoluteAxis.Vertical);

            // 5. Initialize Tracks
            var columns = new List<GridTrack>();
            var rows = new List<GridTrack>();
            ExplicitGridUtils.InitializeGridTracks(columns, finalColCounts, style, AbsoluteAxis.Horizontal,
                columnIndex => cellOccupancyMatrix.ColumnIsOccupied(columnIndex));
            ExplicitGridUtils.InitializeGridTracks(rows, finalRowCounts, style, AbsoluteAxis.Vertical,
                rowIndex => cellOccupancyMatrix.RowIsOccupied(rowIndex));

            // 6. Track Sizing

            // Convert grid placements in origin-zero coordinates to indexes into the GridTrack (rows and columns) vectors
            TrackSizingUtils.ResolveItemTrackIndexes(items, finalColCounts, finalRowCounts);

            // For each item, and in each axis, determine whether the item crosses any flexible (fr) tracks
            TrackSizingUtils.DetermineIfItemCrossesFlexibleOrIntrinsicTracks(items, columns, rows);

            // Determine if the grid has any baseline aligned items
            bool hasBaselineAlignedItem = false;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].AlignSelfStyle == AlignItems.Baseline) { hasBaselineAlignedItem = true; break; }
            }

            // Run track sizing algorithm for Inline axis
            TrackSizingUtils.TrackSizingAlgorithm(
                tree,
                AbstractAxis.Inline,
                minSize.Get(AbstractAxis.Inline),
                maxSize.Get(AbstractAxis.Inline),
                justifyContent,
                alignContent,
                availableGridSpace,
                innerNodeSize,
                columns,
                rows,
                items,
                (GridTrack track, float? pSize, TTree t) =>
                    track.MaxTrackSizingFunction.DefiniteValue(pSize, (val, basis) => t.Calc(val, basis)),
                hasBaselineAlignedItem);

            float initialColumnSum = 0f;
            for (int i = 0; i < columns.Count; i++)
                initialColumnSum += columns[i].BaseSize;
            innerNodeSize.Width ??= initialColumnSum;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                item.AvailableSpaceCache = null;
                items[i] = item;
            }

            // Run track sizing algorithm for Block axis
            TrackSizingUtils.TrackSizingAlgorithm(
                tree,
                AbstractAxis.Block,
                minSize.Get(AbstractAxis.Block),
                maxSize.Get(AbstractAxis.Block),
                alignContent,
                justifyContent,
                availableGridSpace,
                innerNodeSize,
                rows,
                columns,
                items,
                (GridTrack track, float? _, TTree _2) => (float?)track.BaseSize,
                false); // TODO: Support baseline alignment in the vertical axis

            float initialRowSum = 0f;
            for (int i = 0; i < rows.Count; i++)
                initialRowSum += rows[i].BaseSize;
            innerNodeSize.Height ??= initialRowSum;

            // 6. Compute container size
            var resolvedStyleSize = knownDimensions.Or(preferredSize);
            var containerBorderBox = new Size<float>
            {
                Width = MathF.Max(
                    (resolvedStyleSize.Get(AbstractAxis.Inline) ?? (initialColumnSum + contentBoxInset.HorizontalAxisSum()))
                        .MaybeClamp(minSize.Width, maxSize.Width),
                    paddingBorderSize.Width),
                Height = MathF.Max(
                    (resolvedStyleSize.Get(AbstractAxis.Block) ?? (initialRowSum + contentBoxInset.VerticalAxisSum()))
                        .MaybeClamp(minSize.Height, maxSize.Height),
                    paddingBorderSize.Height),
            };
            var containerContentBox = new Size<float>
            {
                Width = MathF.Max(0f, containerBorderBox.Width - contentBoxInset.HorizontalAxisSum()),
                Height = MathF.Max(0f, containerBorderBox.Height - contentBoxInset.VerticalAxisSum()),
            };

            // If only the container's size has been requested
            if (runMode == RunMode.ComputeSize)
                return LayoutOutput.FromOuterSize(containerBorderBox);

            // 7. Resolve percentage track base sizes
            if (!availableGridSpace.Width.IsDefinite())
            {
                for (int i = 0; i < columns.Count; i++)
                {
                    var column = columns[i];
                    var min = column.MinTrackSizingFunction.ResolvedPercentageSize(containerContentBox.Width, (val, basis) => tree.Calc(val, basis));
                    var max = column.MaxTrackSizingFunction.ResolvedPercentageSize(containerContentBox.Width, (val, basis) => tree.Calc(val, basis));
                    column.BaseSize = column.BaseSize.MaybeClamp(min, max);
                    columns[i] = column;
                }
            }
            if (!availableGridSpace.Height.IsDefinite())
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    var min = row.MinTrackSizingFunction.ResolvedPercentageSize(containerContentBox.Height, (val, basis) => tree.Calc(val, basis));
                    var max = row.MaxTrackSizingFunction.ResolvedPercentageSize(containerContentBox.Height, (val, basis) => tree.Calc(val, basis));
                    row.BaseSize = row.BaseSize.MaybeClamp(min, max);
                    rows[i] = row;
                }
            }

            // Column sizing must be re-run (once) if needed
            bool rerunColumnSizing;
            bool hasPercentageColumn = false;
            for (int i = 0; i < columns.Count; i++) { if (columns[i].UsesPercentage()) { hasPercentageColumn = true; break; } }
            bool parentWidthIndefinite = !availableSpace.Width.IsDefinite();
            rerunColumnSizing = parentWidthIndefinite && hasPercentageColumn;

            if (!rerunColumnSizing)
            {
                bool minContentContributionChanged = false;
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (!item.CrossesIntrinsicColumn) continue;

                    var avail = item.AvailableSpaceCached(
                        AbstractAxis.Inline, rows, innerNodeSize.Height,
                        (GridTrack t, float? _) => (float?)t.BaseSize);
                    var newMinContentContribution = item.MinContentContributionCached(AbstractAxis.Inline, tree, avail, innerNodeSize);

                    bool hasChanged = item.MinContentContributionCache.Width != newMinContentContribution;

                    item.AvailableSpaceCache = avail;
                    item.MinContentContributionCache = new Size<float?> { Width = newMinContentContribution, Height = item.MinContentContributionCache.Height };
                    item.MaxContentContributionCache = new Size<float?> { Width = null, Height = item.MaxContentContributionCache.Height };
                    item.MinimumContributionCache = new Size<float?> { Width = null, Height = item.MinimumContributionCache.Height };
                    items[i] = item;

                    if (hasChanged)
                        minContentContributionChanged = true;
                }
                rerunColumnSizing = minContentContributionChanged;
            }
            else
            {
                // Clear intrinsic width caches
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    item.AvailableSpaceCache = null;
                    item.MinContentContributionCache = new Size<float?> { Width = null, Height = item.MinContentContributionCache.Height };
                    item.MaxContentContributionCache = new Size<float?> { Width = null, Height = item.MaxContentContributionCache.Height };
                    item.MinimumContributionCache = new Size<float?> { Width = null, Height = item.MinimumContributionCache.Height };
                    items[i] = item;
                }
            }

            if (rerunColumnSizing)
            {
                // Re-run track sizing algorithm for Inline axis
                TrackSizingUtils.TrackSizingAlgorithm(
                    tree,
                    AbstractAxis.Inline,
                    minSize.Get(AbstractAxis.Inline),
                    maxSize.Get(AbstractAxis.Inline),
                    justifyContent,
                    alignContent,
                    availableGridSpace,
                    innerNodeSize,
                    columns,
                    rows,
                    items,
                    (GridTrack track, float? _, TTree _2) => (float?)track.BaseSize,
                    hasBaselineAlignedItem);

                // Row sizing re-run check
                bool rerunRowSizing;
                bool hasPercentageRow = false;
                for (int ri = 0; ri < rows.Count; ri++) { if (rows[ri].UsesPercentage()) { hasPercentageRow = true; break; } }
                bool parentHeightIndefinite = !availableSpace.Height.IsDefinite();
                rerunRowSizing = parentHeightIndefinite && hasPercentageRow;

                if (!rerunRowSizing)
                {
                    bool minContentContributionChanged = false;
                    for (int i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        if (!item.CrossesIntrinsicColumn) continue;

                        var avail = item.AvailableSpaceCached(
                            AbstractAxis.Block, columns, innerNodeSize.Width,
                            (GridTrack t, float? _) => (float?)t.BaseSize);
                        var newMinContentContribution = item.MinContentContributionCached(AbstractAxis.Block, tree, avail, innerNodeSize);

                        bool hasChanged = item.MinContentContributionCache.Height != newMinContentContribution;

                        item.AvailableSpaceCache = avail;
                        item.MinContentContributionCache = new Size<float?> { Width = item.MinContentContributionCache.Width, Height = newMinContentContribution };
                        item.MaxContentContributionCache = new Size<float?> { Width = item.MaxContentContributionCache.Width, Height = null };
                        item.MinimumContributionCache = new Size<float?> { Width = item.MinimumContributionCache.Width, Height = null };
                        items[i] = item;

                        if (hasChanged)
                            minContentContributionChanged = true;
                    }
                    rerunRowSizing = minContentContributionChanged;
                }
                else
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        item.AvailableSpaceCache = null;
                        item.MinContentContributionCache = new Size<float?> { Width = item.MinContentContributionCache.Width, Height = null };
                        item.MaxContentContributionCache = new Size<float?> { Width = item.MaxContentContributionCache.Width, Height = null };
                        item.MinimumContributionCache = new Size<float?> { Width = item.MinimumContributionCache.Width, Height = null };
                        items[i] = item;
                    }
                }

                if (rerunRowSizing)
                {
                    // Re-run track sizing algorithm for Block axis
                    TrackSizingUtils.TrackSizingAlgorithm(
                        tree,
                        AbstractAxis.Block,
                        minSize.Get(AbstractAxis.Block),
                        maxSize.Get(AbstractAxis.Block),
                        alignContent,
                        justifyContent,
                        availableGridSpace,
                        innerNodeSize,
                        rows,
                        columns,
                        items,
                        (GridTrack track, float? _, TTree _2) => (float?)track.BaseSize,
                        false);
                }
            }

            // 8. Track Alignment

            // Align columns
            GridAlignmentUtils.AlignTracks(
                containerContentBox.Get(AbstractAxis.Inline),
                new Line<float> { Start = padding.Left, End = padding.Right },
                new Line<float> { Start = border.Left, End = border.Right },
                columns,
                justifyContent);
            // Align rows
            GridAlignmentUtils.AlignTracks(
                containerContentBox.Get(AbstractAxis.Block),
                new Line<float> { Start = padding.Top, End = padding.Bottom },
                new Line<float> { Start = border.Top, End = border.Bottom },
                rows,
                alignContent);

            // 9. Size, Align, and Position Grid Items
            var itemContentSizeContribution = SizeExtensions.ZeroF32;

            // Sort items back into original order to allow them to be matched up with styles
            items.Sort((a, b) => a.SourceOrder.CompareTo(b.SourceOrder));

            var containerAlignmentStyles = new InBothAbsAxis<AlignItems?>
            {
                Horizontal = justifyItems,
                Vertical = alignItems,
            };

            // Position in-flow children (stored in items vector)
            for (int index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var gridAreaRect = new Rect<float>
                {
                    Top = rows[item.RowIndexes.Start + 1].Offset,
                    Bottom = rows[item.RowIndexes.End].Offset,
                    Left = columns[item.ColumnIndexes.Start + 1].Offset,
                    Right = columns[item.ColumnIndexes.End].Offset,
                };

                var (contentSizeContrib, yPosition, height) = GridAlignmentUtils.AlignAndPositionItem(
                    tree,
                    item.Node,
                    (uint)index,
                    gridAreaRect,
                    containerAlignmentStyles,
                    item.BaselineShim);

                item.YPosition = yPosition;
                item.Height = height;
                items[index] = item;

                itemContentSizeContribution = itemContentSizeContribution.F32Max(contentSizeContrib);
            }

            // Position hidden and absolutely positioned children
            uint orderVal = (uint)items.Count;
            int childCount = tree.ChildCount(node);
            for (int idx = 0; idx < childCount; idx++)
            {
                var child = tree.GetChildId(node, idx);
                var childStyleInterface = tree.GetGridChildStyle(child);
                var childStyle = (Style)childStyleInterface;

                // Position hidden child
                if (childStyleInterface.BoxGenerationMode() == BoxGenerationMode.None)
                {
                    tree.SetUnroundedLayout(child, Layout.WithOrder(orderVal));
                    tree.PerformChildLayout(
                        child,
                        SizeExtensions.NoneF32,
                        SizeExtensions.NoneF32,
                        new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent),
                        SizingMode.InherentSize,
                        LineExtensions.FalseLine);
                    orderVal++;
                    continue;
                }

                // Position absolutely positioned child
                if (childStyleInterface.Position() == Position.Absolute)
                {
                    // Convert grid-col-{start/end} into Option's of indexes into the columns vector
                    var maybeColIndexes = nameResolver
                        .ResolveColumnNames(childStyle.GetGridColumn())
                        .IntoOriginZero(finalColCounts.Explicit)
                        .ResolveAbsolutelyPositionedGridTracks()
                        .Map(maybeGridLine =>
                        {
                            if (maybeGridLine.HasValue)
                            {
                                var idx2 = maybeGridLine.Value.TryIntoTrackVecIndex(finalColCounts);
                                return idx2 >= 0 ? (int?)idx2 : null;
                            }
                            return null;
                        });

                    var maybeRowIndexes = nameResolver
                        .ResolveRowNames(childStyle.GetGridRow())
                        .IntoOriginZero(finalRowCounts.Explicit)
                        .ResolveAbsolutelyPositionedGridTracks()
                        .Map(maybeGridLine =>
                        {
                            if (maybeGridLine.HasValue)
                            {
                                var idx2 = maybeGridLine.Value.TryIntoTrackVecIndex(finalRowCounts);
                                return idx2 >= 0 ? (int?)idx2 : null;
                            }
                            return null;
                        });

                    var gridAreaRect = new Rect<float>
                    {
                        Top = maybeRowIndexes.Start.HasValue ? rows[maybeRowIndexes.Start.Value].Offset : border.Top,
                        Bottom = maybeRowIndexes.End.HasValue
                            ? rows[maybeRowIndexes.End.Value].Offset
                            : containerBorderBox.Height - border.Bottom - scrollbarGutter.Height,
                        Left = maybeColIndexes.Start.HasValue ? columns[maybeColIndexes.Start.Value].Offset : border.Left,
                        Right = maybeColIndexes.End.HasValue
                            ? columns[maybeColIndexes.End.Value].Offset
                            : containerBorderBox.Width - border.Right - scrollbarGutter.Width,
                    };

                    var (contentSizeContrib, _, _) = GridAlignmentUtils.AlignAndPositionItem(
                        tree, child, orderVal, gridAreaRect, containerAlignmentStyles, 0f);
                    itemContentSizeContribution = itemContentSizeContribution.F32Max(contentSizeContrib);
                    orderVal++;
                }
            }

            // If there are no items then return just the container size (no baseline)
            if (items.Count == 0)
                return LayoutOutput.FromOuterSize(containerBorderBox);

            // Determine the grid container baseline(s) (currently we only compute the first baseline)
            float gridContainerBaseline;
            {
                // Sort items by row start position
                items.Sort((a, b) => a.RowIndexes.Start.CompareTo(b.RowIndexes.Start));

                // Get the row index of the first row containing items
                var firstRow = items[0].RowIndexes.Start;

                // Create a slice of all of the items that start in this row
                int firstRowEndIdx = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].RowIndexes.Start != firstRow) break;
                    firstRowEndIdx = i + 1;
                }

                // Check if any items in *this row* are baseline aligned
                bool rowHasBaselineItem = false;
                for (int i = 0; i < firstRowEndIdx; i++)
                {
                    if (items[i].AlignSelfStyle == AlignItems.Baseline)
                    {
                        rowHasBaselineItem = true;
                        break;
                    }
                }

                GridItem baselineItem;
                if (rowHasBaselineItem)
                {
                    baselineItem = items[0];
                    for (int i = 0; i < firstRowEndIdx; i++)
                    {
                        if (items[i].AlignSelfStyle == AlignItems.Baseline)
                        {
                            baselineItem = items[i];
                            break;
                        }
                    }
                }
                else
                {
                    baselineItem = items[0];
                }

                gridContainerBaseline = baselineItem.YPosition + (baselineItem.Baseline ?? baselineItem.Height);
            }

            return LayoutOutput.FromSizesAndBaselines(
                containerBorderBox,
                itemContentSizeContribution,
                new Point<float?> { X = null, Y = gridContainerBaseline });
        }
    }
}
