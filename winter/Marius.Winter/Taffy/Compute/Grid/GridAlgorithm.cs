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
            var style = gridStyle;
            var direction = gridStyle.Direction();

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

            var contentBoxInset = paddingBorder;
            contentBoxInset.Bottom += scrollbarGutter.Height;
            if (direction.IsRtl())
                contentBoxInset.Left += scrollbarGutter.Width;
            else
                contentBoxInset.Right += scrollbarGutter.Width;

            var alignContent = gridStyle.AlignContent() ?? AlignContent.Normal;
            if (alignContent.Keyword == AlignContentKeyword.Normal) alignContent = AlignContent.Stretch;
            var justifyContent = gridStyle.JustifyContent() ?? AlignContent.Normal;
            if (justifyContent.Keyword == AlignContentKeyword.Normal) justifyContent = AlignContent.Stretch;
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

            var innerMinSize = minSize.MaybeSub(contentBoxInset.SumAxes().Map<float?>(v => v));
            var innerMaxSize = maxSize.MaybeSub(contentBoxInset.SumAxes().Map<float?>(v => v));

            // Short-circuit layout if the container's size is fully determined by the container's size and the run mode
            // is ComputeSize (and thus the container's size is all that we're interested in)
            if (runMode == RunMode.ComputeSize)
            {
                if (outerNodeSize.Width.HasValue && outerNodeSize.Height.HasValue)
                {
                    return LayoutOutput.FromOuterSize(new Size<float> { Width = outerNodeSize.Width.Value, Height = outerNodeSize.Height.Value });
                }

                // We can also short-circuit if the width is known and only the width has been requested.
                if (inputs.Axis == RequestedAxis.Horizontal && outerNodeSize.Width.HasValue)
                {
                    return LayoutOutput.FromOuterSize(new Size<float> { Width = outerNodeSize.Width.Value, Height = 0f });
                }
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

            var explicitColCount = (ushort)Math.Min(Math.Max(gridTemplateColCount, nameResolver.AreaColumnCount), (ushort)10000);
            var explicitRowCount = (ushort)Math.Min(Math.Max(gridTemplateRowCount, nameResolver.AreaRowCount), (ushort)10000);

            nameResolver.SetExplicitColumnCount(explicitColCount);
            nameResolver.SetExplicitRowCount(explicitRowCount);

            // Resolve the styles once, before estimating the implicit bounds and placing items.
            var items = new ValueList<GridItem>();
            var placements = new List<InBothAbsAxis<Line<GenericGridPlacement<OriginZeroLine>>>>();
            int sourceOrder = 0;
            foreach (var childNode in tree.ChildIds(node))
            {
                var child = tree.GetGridChildStyle(childNode);
                if (((ICoreStyle)child).BoxGenerationMode() != BoxGenerationMode.None && !child.Position().IsOutOfFlow())
                {
                    placements.Add(new() { Horizontal = nameResolver.ResolveColumnNames(child.GetGridColumn()).IntoOriginZero(explicitColCount),
                        Vertical = nameResolver.ResolveRowNames(child.GetGridRow()).IntoOriginZero(explicitRowCount) });
                    items.Add(GridItem.NewWithPlacementStyleAndOrder(childNode, default, default, child,
                        alignItems ?? AlignItems.Normal, justifyItems ?? AlignItems.Normal, unchecked((ushort)sourceOrder)));
                }
                sourceOrder++;
            }
            var placementSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(placements);
            var (estColCounts, estRowCounts) = ImplicitGridUtils.ComputeGridSizeEstimate(explicitColCount, explicitRowCount, placementSpan);
            var cellOccupancyMatrix = CellOccupancyMatrix.WithTrackCounts(estColCounts, estRowCounts);
            PlacementUtils.PlaceGridItems(cellOccupancyMatrix, ref items, placementSpan, gridStyle.GridAutoFlow());

            for (int i = 0; i < items.Count; i++)
            {
                var itemStyle = tree.GetGridChildStyle(items[i].Node);
                items[i].AlignSelfStyle = (itemStyle.AlignSelf() ?? alignItems ?? AlignItems.Normal).ResolveSelfRelative(itemStyle.Direction(), direction, false);
                items[i].JustifySelfStyle = (itemStyle.JustifySelf() ?? justifyItems ?? AlignItems.Normal).ResolveSelfRelative(itemStyle.Direction(), direction, true);
            }

            // Extract track counts from previous step (auto-placement can expand the number of tracks)
            var finalColCounts = cellOccupancyMatrix.GetTrackCounts(AbsoluteAxis.Horizontal);
            var finalRowCounts = cellOccupancyMatrix.GetTrackCounts(AbsoluteAxis.Vertical);

            // 5. Initialize Tracks
            var columns = new ValueList<GridTrack>();
            var rows = new ValueList<GridTrack>();
            ExplicitGridUtils.InitializeGridTracks(ref columns, finalColCounts, style, AbsoluteAxis.Horizontal, colAutoRepetitionCount,
                columnIndex => cellOccupancyMatrix.ColumnIsOccupied(columnIndex));
            ExplicitGridUtils.InitializeGridTracks(ref rows, finalRowCounts, style, AbsoluteAxis.Vertical, rowAutoRepetitionCount,
                rowIndex => cellOccupancyMatrix.RowIsOccupied(rowIndex));

            // 6. Track Sizing

            // Convert grid placements in origin-zero coordinates to indexes into the GridTrack (rows and columns) vectors
            TrackSizingUtils.ResolveItemTrackIndexes(ref items, finalColCounts, finalRowCounts);

            // For each item, and in each axis, determine whether the item crosses any flexible (fr) tracks
            TrackSizingUtils.DetermineIfItemCrossesFlexibleOrIntrinsicTracks(ref items, ref columns, ref rows);

            // Determine if the grid has any baseline aligned items
            bool hasBaselineAlignedItem = false;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].ParticipatesInBaselineAlignment) { hasBaselineAlignedItem = true; break; }
            }

            // Run track sizing algorithm for Inline axis
            TrackSizingUtils.TrackSizingAlgorithm(
                tree,
                AbstractAxis.Inline,
                innerMinSize.Get(AbstractAxis.Inline),
                innerMaxSize.Get(AbstractAxis.Inline),
                justifyContent,
                alignContent,
                availableGridSpace,
                innerNodeSize,
                ref columns,
                ref rows,
                ref items,
                (GridTrack track, float? pSize, TTree t) =>
                    track.MaxTrackSizingFunction.DefiniteValue(pSize, (val, basis) => t.Calc(val, basis)),
                hasBaselineAlignedItem);

            float initialColumnSum = 0f;
            for (int i = 0; i < columns.Count; i++)
                initialColumnSum += columns[i].BaseSize;
            innerNodeSize.Width ??= initialColumnSum;

            if (runMode == RunMode.ComputeSize && inputs.Axis == RequestedAxis.Horizontal)
                return LayoutOutput.FromOuterSize(new(TaffySys.F32Max(
                    (initialColumnSum + contentBoxInset.HorizontalAxisSum()).MaybeClamp(minSize.Width, maxSize.Width), paddingBorderSize.Width), 0));

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                item.GridAreaSizeCache = null;
                items[i] = item;
            }

            // Run track sizing algorithm for Block axis
            TrackSizingUtils.TrackSizingAlgorithm(
                tree,
                AbstractAxis.Block,
                innerMinSize.Get(AbstractAxis.Block),
                innerMaxSize.Get(AbstractAxis.Block),
                alignContent,
                justifyContent,
                availableGridSpace,
                innerNodeSize,
                ref rows,
                ref columns,
                ref items,
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
                Width = TaffySys.F32Max(
                    (resolvedStyleSize.Get(AbstractAxis.Inline) ?? (initialColumnSum + contentBoxInset.HorizontalAxisSum()))
                        .MaybeClamp(minSize.Width, maxSize.Width),
                    paddingBorderSize.Width),
                Height = TaffySys.F32Max(
                    (resolvedStyleSize.Get(AbstractAxis.Block) ?? (initialRowSum + contentBoxInset.VerticalAxisSum()))
                        .MaybeClamp(minSize.Height, maxSize.Height),
                    paddingBorderSize.Height),
            };
            var containerContentBox = new Size<float>
            {
                Width = TaffySys.F32Max(0f, containerBorderBox.Width - contentBoxInset.HorizontalAxisSum()),
                Height = TaffySys.F32Max(0f, containerBorderBox.Height - contentBoxInset.VerticalAxisSum()),
            };

            // If only the container's size has been requested
            if (runMode == RunMode.ComputeSize)
                return LayoutOutput.FromOuterSize(containerBorderBox);

            // 7. Resolve percentage track base sizes
            innerNodeSize = containerContentBox.Map<float?>(v => v);

            // Column sizing must be re-run (once) if needed
            bool rerunColumnSizing;
            bool intrinsicColumnContributionChanged = false;
            bool hasPercentageColumn = false;
            for (int i = 0; i < columns.Count; i++) { if (columns[i].UsesPercentage()) { hasPercentageColumn = true; break; } }
            bool hasPercentageRow = false;
            for (int i = 0; i < rows.Count; i++) { if (rows[i].UsesPercentage()) { hasPercentageRow = true; break; } }
            bool parentWidthIndefinite = !outerNodeSize.Width.HasValue;
            rerunColumnSizing = parentWidthIndefinite && hasPercentageColumn;

            if (!rerunColumnSizing)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (!item.CrossesIntrinsicColumn) continue;

                    var gridAreaSize = item.GridAreaSize(
                        AbstractAxis.Inline, ref columns, ref rows, innerNodeSize,
                        (GridTrack t, float? _) => (float?)t.BaseSize,
                        (val, basis) => tree.Calc(val, basis));
                    var intrinsicAvailableSpace = gridAreaSize;
                    intrinsicAvailableSpace.Set(AbstractAxis.Inline, null);
                    var newMinContentContribution = item.MinContentContribution(
                        AbstractAxis.Inline, tree, gridAreaSize, intrinsicAvailableSpace);

                    bool hasChanged = item.MinContentContributionCache.Width != newMinContentContribution;

                    item.GridAreaSizeCache = gridAreaSize;
                    item.MinContentContributionCache = new Size<float?> { Width = newMinContentContribution, Height = item.MinContentContributionCache.Height };
                    item.MaxContentContributionCache = new Size<float?> { Width = null, Height = item.MaxContentContributionCache.Height };
                    item.MinimumContributionCache = new Size<float?> { Width = null, Height = item.MinimumContributionCache.Height };
                    items[i] = item;

                    if (hasChanged)
                    {
                        intrinsicColumnContributionChanged = true;
                    }
                }
                rerunColumnSizing = intrinsicColumnContributionChanged;
            }
            else
            {
                // Clear intrinsic width caches
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    item.GridAreaSizeCache = null;
                    item.MinContentContributionCache = new Size<float?> { Width = null, Height = item.MinContentContributionCache.Height };
                    item.MaxContentContributionCache = new Size<float?> { Width = null, Height = item.MaxContentContributionCache.Height };
                    item.MinimumContributionCache = new Size<float?> { Width = null, Height = item.MinimumContributionCache.Height };
                    items[i] = item;
                }
            }

            bool intrinsicRowContributionChanged = false;
            bool rerunRowSizing = !outerNodeSize.Height.HasValue && hasPercentageRow;
            if (rerunRowSizing)
                foreach (var item in items)
                {
                    item.GridAreaSizeCache = null;
                    item.MinContentContributionCache.Height = null; item.MaxContentContributionCache.Height = null; item.MinimumContributionCache.Height = null;
                }
            if (rerunColumnSizing)
            {
                TrackSizingUtils.TrackSizingAlgorithm(tree, AbstractAxis.Inline, innerMinSize.Width, innerMaxSize.Width,
                    justifyContent, alignContent, availableGridSpace, innerNodeSize, ref columns, ref rows, ref items,
                    (GridTrack track, float? _, TTree _2) => track.BaseSize, hasBaselineAlignedItem);
                if (!rerunRowSizing)
                {
                    foreach (var item in items)
                    {
                        if (!item.CrossesIntrinsicRow) continue;
                        var area = item.GridAreaSize(AbstractAxis.Block, ref rows, ref columns, innerNodeSize,
                            (GridTrack track, float? _) => track.BaseSize, tree.Calc);
                        var available = area; available.Height = null;
                        float contribution = item.MinContentContribution(AbstractAxis.Block, tree, area, available);
                        intrinsicRowContributionChanged |= item.MinContentContributionCache.Height != contribution;
                        item.GridAreaSizeCache = area;
                        item.MinContentContributionCache.Height = contribution; item.MaxContentContributionCache.Height = null; item.MinimumContributionCache.Height = null;
                    }
                    rerunRowSizing = intrinsicRowContributionChanged;
                }
            }
            if (rerunRowSizing)
                TrackSizingUtils.TrackSizingAlgorithm(tree, AbstractAxis.Block, innerMinSize.Height, innerMaxSize.Height,
                    alignContent, justifyContent, availableGridSpace, innerNodeSize, ref rows, ref columns, ref items,
                    (GridTrack track, float? _, TTree _2) => track.BaseSize, false);

            if ((intrinsicColumnContributionChanged && !hasPercentageColumn)
                || (intrinsicRowContributionChanged && !hasPercentageRow))
            {
                float finalColumnSum = 0f;
                for (int i = 0; i < columns.Count; i++)
                    finalColumnSum += columns[i].BaseSize;
                float finalRowSum = 0f;
                for (int i = 0; i < rows.Count; i++)
                    finalRowSum += rows[i].BaseSize;

                if (intrinsicColumnContributionChanged && !hasPercentageColumn)
                {
                    containerBorderBox.Width = TaffySys.F32Max(
                        (resolvedStyleSize.Get(AbstractAxis.Inline) ?? (finalColumnSum + contentBoxInset.HorizontalAxisSum()))
                            .MaybeClamp(minSize.Width, maxSize.Width),
                        paddingBorderSize.Width);
                    containerContentBox.Width = TaffySys.F32Max(
                        0f, containerBorderBox.Width - contentBoxInset.HorizontalAxisSum());
                }

                if (intrinsicRowContributionChanged && !hasPercentageRow)
                {
                    containerBorderBox.Height = TaffySys.F32Max(
                        (resolvedStyleSize.Get(AbstractAxis.Block) ?? (finalRowSum + contentBoxInset.VerticalAxisSum()))
                            .MaybeClamp(minSize.Height, maxSize.Height),
                        paddingBorderSize.Height);
                    containerContentBox.Height = TaffySys.F32Max(
                        0f, containerBorderBox.Height - contentBoxInset.VerticalAxisSum());
                }
            }

            // If only the container's size has been requested
            if (runMode == RunMode.ComputeSize)
                return LayoutOutput.FromOuterSize(containerBorderBox);

            // 8. Track Alignment

            // Align columns
            var inlineSizeWithoutScrollbar = TaffySys.F32Max(containerBorderBox.Width - (padding.Left + padding.Right + border.Left + border.Right), 0f);
            var inlineScrollbarGutterForAlignment = TaffySys.F32Min(scrollbarGutter.Width, inlineSizeWithoutScrollbar);
            GridAlignmentUtils.AlignTracks(
                containerContentBox.Get(AbstractAxis.Inline),
                new Line<float>
                {
                    Start = padding.Left + (direction.IsRtl() ? inlineScrollbarGutterForAlignment : 0f),
                    End = padding.Right + (direction.IsRtl() ? 0f : inlineScrollbarGutterForAlignment),
                },
                new Line<float> { Start = border.Left, End = border.Right },
                ref columns,
                justifyContent,
                direction.IsRtl());
            // Align rows
            GridAlignmentUtils.AlignTracks(
                containerContentBox.Get(AbstractAxis.Block),
                new Line<float> { Start = padding.Top, End = padding.Bottom },
                new Line<float> { Start = border.Top, End = border.Bottom },
                ref rows,
                alignContent,
                false);

            // 9. Size, Align, and Position Grid Items
            Rect<float> itemContentSizeContribution = default;
            var candidates = new OofCandidates();
            bool parentIsScroll = style.Overflow().X.IsScrollContainer() || style.Overflow().Y.IsScrollContainer();

            // Sort items back into original order to allow them to be matched up with styles
            items.StableSort((a, b) => a.SourceOrder.CompareTo(b.SourceOrder));

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
                    Left = direction.IsRtl() ? columns[item.ColumnIndexes.End - 1].Offset : columns[item.ColumnIndexes.Start + 1].Offset,
                    Right = direction.IsRtl() ? columns[item.ColumnIndexes.Start].Offset : columns[item.ColumnIndexes.End].Offset,
                };

                var itemAlignmentStyles = containerAlignmentStyles;
                if (item.HasCyclicBlockSizeDependency && item.AlignSelfStyle.Keyword == AlignItemsKeyword.Baseline)
                    itemAlignmentStyles.Vertical = AlignItems.Start;

                var (contentSizeContrib, yPosition, height, baseline) = GridAlignmentUtils.AlignAndPositionItem(
                    tree,
                    item.Node,
                    (uint)index,
                    gridAreaRect,
                    itemAlignmentStyles,
                    item.HasCyclicBlockSizeDependency ? 0 : item.BaselineShim,
                    direction, containerBorderBox.Width, border, parentIsScroll, ref item.OofCandidates);

                item.YPosition = yPosition;
                item.Height = height;
                item.Baseline = baseline;
                items[index] = item;

                itemContentSizeContribution = itemContentSizeContribution.Union(contentSizeContrib);
            }

            DetailedGridInfo MakeDetailedInfo() => new(new(finalRowCounts, rows, nameResolver.DetailedLineNames(false)),
                new(finalColCounts, columns, nameResolver.DetailedLineNames(true)), items, nameResolver);
            var detailed = MakeDetailedInfo();

            // Position hidden and absolutely positioned children
            uint orderVal = (uint)items.Count;
            int childCount = tree.ChildCount(node), inflowCursor = 0;
            for (int idx = 0; idx < childCount; idx++)
            {
                var child = tree.GetChildId(node, idx);
                var childStyleInterface = tree.GetGridChildStyle(child);
                var childStyle = childStyleInterface;

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

                if (!childStyleInterface.Position().IsOutOfFlow())
                {
                    if (inflowCursor < items.Count) candidates.Append(ref items[inflowCursor++].OofCandidates);
                    continue;
                }
                var justify = (childStyle.JustifySelf() ?? justifyItems ?? AlignItems.Normal).ResolveSelfRelative(childStyle.Direction(), direction, true);
                var align = (childStyle.AlignSelf() ?? alignItems ?? AlignItems.Normal).ResolveSelfRelative(childStyle.Direction(), direction, false);
                var contentArea = new Rect<float>(border.Left + padding.Left + (direction.IsRtl() ? scrollbarGutter.Width : 0),
                    containerBorderBox.Width - border.Right - padding.Right - (direction.IsRtl() ? 0 : scrollbarGutter.Width),
                    border.Top + padding.Top, containerBorderBox.Height - border.Bottom - padding.Bottom - scrollbarGutter.Height);
                if (((ICoreStyle)style).IsContainingBlock().ForPosition(childStyle.Position()))
                {
                    contentArea = ResolveStaticPositionGridArea(childStyle, nameResolver, finalRowCounts, finalColCounts,
                        rows, columns, direction, border, scrollbarGutter, containerBorderBox);
                }
                candidates.Push(new OofCandidate { Node = child, Order = orderVal++, Position = childStyle.Position(),
                    StaticPosition = new(AxisStaticPosition.FromAlignment(justify, new(contentArea.Left, contentArea.Right), direction.IsRtl()),
                        AxisStaticPosition.FromAlignment(align, new(contentArea.Top, contentArea.Bottom), false)) });
            }

            tree.SetDetailedGridInfo(node, detailed);
            LayoutOutput FinishOutput(LayoutOutput output)
            {
                output.OofCandidates = candidates;
                var inset = border.Add(new Rect<float>(direction.IsRtl() ? scrollbarGutter.Width : 0,
                    direction.IsRtl() ? 0 : scrollbarGutter.Width, 0, scrollbarGutter.Height));
                output.OofPositioningArea = new(containerBorderBox.Sub(inset.SumAxes()), new(inset.Left, inset.Top));
                var rect = itemContentSizeContribution;
                if (parentIsScroll) { rect.Right += direction.IsRtl() ? padding.Left : padding.Right; rect.Bottom += padding.Bottom; }
                output.ScrollableOverflowRect = rect;
                return output;
            }

            // If there are no items then return just the container size (no baseline)
            if (items.Count == 0)
                return FinishOutput(LayoutOutput.FromOuterSize(containerBorderBox));

            float? gridContainerBaseline = null;
            if (!style.Contain().SuppressesBaseline())
            {
                ushort firstRow = ushort.MaxValue;
                foreach (var item in items) firstRow = Math.Min(firstRow, item.RowIndexes.Start);
                GridItem? first = null, aligned = null;
                foreach (var item in items)
                    if (item.RowIndexes.Start == firstRow) { first ??= item; if (item.ParticipatesInBaselineAlignment) { aligned = item; break; } }
                var baselineItem = aligned ?? first!;
                gridContainerBaseline = baselineItem.YPosition + (baselineItem.Baseline ?? baselineItem.Height);
            }

            return FinishOutput(LayoutOutput.FromSizesAndBaselines(containerBorderBox,
                new Size<float>(itemContentSizeContribution.Right, itemContentSizeContribution.Bottom),
                new Point<float?> { X = null, Y = gridContainerBaseline }));
        }

        // Unlike the final positioning area, the static rectangle preserves raw adjacent
        // track/gutter edges, including reversed extents caused by overflowing alignment.
        private static Rect<float> ResolveStaticPositionGridArea(IGridItemStyle childStyle, NamedLineResolver resolver,
            TrackCounts rowCounts, TrackCounts columnCounts, in ValueList<GridTrack> rows, in ValueList<GridTrack> columns,
            Direction direction, Rect<float> border, Size<float> scrollbarGutter, Size<float> container)
        {
            var columnLines = resolver.ResolveColumnNames(childStyle.GetGridColumn()).IntoOriginZero(columnCounts.Explicit)
                .ResolveAbsolutelyPositionedGridTracks();
            var rowLines = resolver.ResolveRowNames(childStyle.GetGridRow()).IntoOriginZero(rowCounts.Explicit)
                .ResolveAbsolutelyPositionedGridTracks();
            int? cs = columnLines.Start?.TryIntoTrackVecIndex(columnCounts), ce = columnLines.End?.TryIntoTrackVecIndex(columnCounts);
            int? rs = rowLines.Start?.TryIntoTrackVecIndex(rowCounts), re = rowLines.End?.TryIntoTrackVecIndex(rowCounts);
            static float Start(in ValueList<GridTrack> tracks, int index) => tracks[index + 1 < tracks.Count ? index + 1 : index].Offset;
            static float End(in ValueList<GridTrack> tracks, int index) => tracks[index == 0 && tracks.Count > 1 ? 1 : index].Offset;
            static float RtlStart(in ValueList<GridTrack> tracks, int index) => tracks[tracks.Count > index + 1 || index == 0 ? index : index - 1].Offset;
            static float RtlEnd(in ValueList<GridTrack> tracks, int index) => tracks[index == 0 ? 0 : index - 1].Offset;
            float left = direction.IsRtl()
                ? ce is { } e ? RtlEnd(columns, e) : border.Left + scrollbarGutter.Width
                : cs is { } s ? Start(columns, s) : border.Left;
            float right = direction.IsRtl()
                ? cs is { } rtlStart ? RtlStart(columns, rtlStart) : container.Width - border.Right
                : ce is { } ltrEnd ? End(columns, ltrEnd) : container.Width - border.Right - scrollbarGutter.Width;
            return new(left, right, rs is { } top ? Start(rows, top) : border.Top,
                re is { } bottom ? End(rows, bottom) : container.Height - border.Bottom - scrollbarGutter.Height);
        }
    }
}
