// Ported from taffy/src/compute/grid/track_sizing.rs
// Implements the track sizing algorithm
// https://www.w3.org/TR/css-grid-1/#layout-algorithm

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// Takes an axis, and a list of grid items sorted firstly by whether they cross a flex track
    /// in the specified axis (items that don't cross a flex track first) and then by the number
    /// of tracks they cross in specified axis (ascending order).
    /// </summary>
    internal struct ItemBatcher
    {
        /// <summary>The axis in which the ItemBatcher is operating. Used when querying properties from items.</summary>
        public AbstractAxis Axis;
        /// <summary>The starting index of the current batch</summary>
        public int IndexOffset;
        /// <summary>The span of the items in the current batch</summary>
        public ushort CurrentSpan;
        /// <summary>Whether the current batch of items cross a flexible track</summary>
        public bool CurrentIsFlex;

        /// <summary>Create a new ItemBatcher for the specified axis</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemBatcher(AbstractAxis axis)
        {
            Axis = axis;
            IndexOffset = 0;
            CurrentSpan = 1;
            CurrentIsFlex = false;
        }

        /// <summary>
        /// This is basically a manual version of Iterator.next which passes items
        /// in as a parameter on each iteration to work around borrow checker rules.
        /// Returns (startIndex, count, isFlex) or null if done.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (int startIndex, int count, bool isFlex)? Next(Span<GridItem> items, int itemCount)
        {
            if (CurrentIsFlex || IndexOffset >= itemCount)
                return null;

            var item = items[IndexOffset];
            CurrentSpan = item.Span(Axis);
            CurrentIsFlex = item.CrossesFlexibleTrack(Axis);

            int nextIndexOffset;
            if (CurrentIsFlex)
            {
                nextIndexOffset = itemCount;
            }
            else
            {
                nextIndexOffset = itemCount;
                for (int i = IndexOffset; i < itemCount; i++)
                {
                    if (items[i].CrossesFlexibleTrack(Axis) || items[i].Span(Axis) > CurrentSpan)
                    {
                        nextIndexOffset = i;
                        break;
                    }
                }
            }

            var startIndex = IndexOffset;
            var count = nextIndexOffset - IndexOffset;
            IndexOffset = nextIndexOffset;

            return (startIndex, count, CurrentIsFlex);
        }
    }

    /// <summary>
    /// Whether it is a minimum or maximum size's space being distributed.
    /// This controls behaviour of the space distribution algorithm when distributing beyond limits.
    /// See "distributing space beyond limits" at https://www.w3.org/TR/css-grid-1/#extra-space
    /// </summary>
    internal enum IntrinsicContributionType
    {
        /// <summary>It's a minimum size's space being distributed</summary>
        Minimum,
        /// <summary>It's a maximum size's space being distributed</summary>
        Maximum,
    }

    /// <summary>
    /// Track sizing algorithm implementation
    /// </summary>
    public static class TrackSizingUtils
    {
        /// <summary>
        /// To make track sizing efficient we want to order tracks.
        /// Here a placement is either a Line representing a row-start/row-end or a column-start/column-end
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int CmpByCrossFlexThenSpanThenStart(GridItem itemA, GridItem itemB, AbstractAxis axis)
        {
            bool aCrossesFlex = itemA.CrossesFlexibleTrack(axis);
            bool bCrossesFlex = itemB.CrossesFlexibleTrack(axis);

            if (!aCrossesFlex && bCrossesFlex) return -1;
            if (aCrossesFlex && !bCrossesFlex) return 1;

            var placementA = itemA.Placement(axis);
            var placementB = itemB.Placement(axis);
            var spanCmp = placementA.Span().CompareTo(placementB.Span());
            if (spanCmp != 0) return spanCmp;
            return placementA.Start.CompareTo(placementB.Start);
        }

        /// <summary>
        /// When applying the track sizing algorithm and estimating the size in the other axis for content sizing items
        /// we should take into account align-content/justify-content if both the grid container and all items in the
        /// other axis have definite sizes. This function computes such a per-gutter additional size adjustment.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ComputeAlignmentGutterAdjustment(
            AlignContent alignment,
            float? axisInnerNodeSize,
            Func<GridTrack, float?, float?> getTrackSizeEstimate,
            List<GridTrack> tracks,
            int trackCount)
        {
            if (trackCount <= 1)
                return 0f;

            // As items never cross the outermost gutters in a grid, we can simplify our calculations by treating
            // AlignContent.Start and AlignContent.End the same
            int outerGutterWeight = alignment switch
            {
                AlignContent.Start => 1,
                AlignContent.FlexStart => 1,
                AlignContent.End => 1,
                AlignContent.FlexEnd => 1,
                AlignContent.Center => 1,
                AlignContent.Stretch => 0,
                AlignContent.SpaceBetween => 0,
                AlignContent.SpaceAround => 1,
                AlignContent.SpaceEvenly => 1,
                _ => 0,
            };

            int innerGutterWeight = alignment switch
            {
                AlignContent.FlexStart => 0,
                AlignContent.Start => 0,
                AlignContent.FlexEnd => 0,
                AlignContent.End => 0,
                AlignContent.Center => 0,
                AlignContent.Stretch => 0,
                AlignContent.SpaceBetween => 1,
                AlignContent.SpaceAround => 2,
                AlignContent.SpaceEvenly => 1,
                _ => 0,
            };

            if (innerGutterWeight == 0)
                return 0f;

            if (axisInnerNodeSize.HasValue)
            {
                float? trackSizeSum = 0f;
                bool allDefined = true;
                for (int i = 0; i < trackCount; i++)
                {
                    var estimate = getTrackSizeEstimate(tracks[i], axisInnerNodeSize);
                    if (estimate.HasValue)
                        trackSizeSum += estimate.Value;
                    else
                    {
                        allDefined = false;
                        break;
                    }
                }

                float freeSpace;
                if (allDefined)
                    freeSpace = MathF.Max(0f, axisInnerNodeSize.Value - trackSizeSum!.Value);
                else
                    freeSpace = 0f;

                int weightedTrackCount =
                    (((trackCount - 3) / 2) * innerGutterWeight) + (2 * outerGutterWeight);

                return (freeSpace / weightedTrackCount) * innerGutterWeight;
            }

            return 0f;
        }

        /// <summary>
        /// Convert origin-zero coordinates track placement in grid track vector indexes
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ResolveItemTrackIndexes(List<GridItem> items, TrackCounts columnCounts, TrackCounts rowCounts)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                item.ColumnIndexes = item.Column.Map(line => (ushort)line.IntoTrackVecIndex(columnCounts));
                item.RowIndexes = item.Row.Map(line => (ushort)line.IntoTrackVecIndex(rowCounts));
                items[i] = item;
            }
        }

        /// <summary>
        /// Determine (in each axis) whether the item crosses any flexible tracks
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DetermineIfItemCrossesFlexibleOrIntrinsicTracks(
            List<GridItem> items,
            List<GridTrack> columns,
            List<GridTrack> rows)
        {
            for (int idx = 0; idx < items.Count; idx++)
            {
                var item = items[idx];

                item.CrossesFlexibleColumn = false;
                item.CrossesIntrinsicColumn = false;
                var colRange = item.TrackRangeExcludingLines(AbstractAxis.Inline);
                for (int i = colRange.Start; i < colRange.End; i += 2)
                {
                    if (columns[i].IsFlexible()) item.CrossesFlexibleColumn = true;
                    if (columns[i].HasIntrinsicSizingFunction()) item.CrossesIntrinsicColumn = true;
                }

                item.CrossesFlexibleRow = false;
                item.CrossesIntrinsicRow = false;
                var rowRange = item.TrackRangeExcludingLines(AbstractAxis.Block);
                for (int i = rowRange.Start; i < rowRange.End; i += 2)
                {
                    if (rows[i].IsFlexible()) item.CrossesFlexibleRow = true;
                    if (rows[i].HasIntrinsicSizingFunction()) item.CrossesIntrinsicRow = true;
                }

                items[idx] = item;
            }
        }

        /// <summary>
        /// Track sizing algorithm
        /// Note: Gutters are treated as empty fixed-size tracks for the purpose of the track sizing algorithm.
        /// </summary>
        public static void TrackSizingAlgorithm<TTree>(
            TTree tree,
            AbstractAxis axis,
            float? axisMinSize,
            float? axisMaxSize,
            AlignContent axisAlignment,
            AlignContent otherAxisAlignment,
            Size<AvailableSpace> availableGridSpace,
            Size<float?> innerNodeSize,
            List<GridTrack> axisTracks,
            List<GridTrack> otherAxisTracks,
            List<GridItem> items,
            Func<GridTrack, float?, TTree, float?> getTrackSizeEstimate,
            bool hasBaselineAlignedItem) where TTree : ILayoutPartialTree
        {
            // 11.4 Initialise Track sizes
            // Initialize each track's base size and growth limit.
            float? percentageBasis = innerNodeSize.Get(axis) ?? axisMinSize;
            InitializeTrackSizes(tree, axisTracks, percentageBasis);

            // 11.5.1 Shim item baselines
            if (hasBaselineAlignedItem)
            {
                ResolveItemBaselines(tree, axis, items, innerNodeSize);
            }

            // If all tracks have base_size == growth_limit, then skip the rest of this function.
            // Note: this can only happen when both track sizing functions have the same fixed track sizing function
            bool allEqual = true;
            for (int i = 0; i < axisTracks.Count; i++)
            {
                if (axisTracks[i].BaseSize != axisTracks[i].GrowthLimit) { allEqual = false; break; }
            }
            if (allEqual) return;

            // Pre-computations for 11.5 Resolve Intrinsic Track Sizes

            // Compute an additional amount to add to each spanned gutter when computing item's estimated size in the
            // in the opposite axis based on the alignment, container size, and estimated track sizes in that axis
            float gutterAlignmentAdjustment = ComputeAlignmentGutterAdjustment(
                otherAxisAlignment,
                innerNodeSize.Get(axis.Other()),
                (track, basis) => getTrackSizeEstimate(track, basis, tree),
                otherAxisTracks,
                otherAxisTracks.Count);

            if (otherAxisTracks.Count > 3)
            {
                for (int i = 2; i < otherAxisTracks.Count; i += 2)
                {
                    var track = otherAxisTracks[i];
                    track.ContentAlignmentAdjustment = gutterAlignmentAdjustment;
                    otherAxisTracks[i] = track;
                }
            }

            // 11.5 Resolve Intrinsic Track Sizes
            ResolveIntrinsicTrackSizes(
                tree,
                axis,
                axisTracks,
                otherAxisTracks,
                items,
                availableGridSpace.Get(axis),
                innerNodeSize,
                getTrackSizeEstimate);

            // 11.6. Maximise Tracks
            // Distributes free space (if any) to tracks with FINITE growth limits, up to their limits.
            MaximiseTracks(axisTracks, innerNodeSize.Get(axis), availableGridSpace.Get(axis));

            // For the purpose of the final two expansion steps ("Expand Flexible Tracks" and "Stretch auto Tracks"), we only want to expand
            // into space generated by the grid container's size (as defined by either its preferred size style or by its parent node through
            // something like stretch alignment), not just any available space.
            AvailableSpace axisAvailableSpaceForExpansion;
            if (innerNodeSize.Get(axis).HasValue)
            {
                axisAvailableSpaceForExpansion = AvailableSpace.Definite(innerNodeSize.Get(axis)!.Value);
            }
            else
            {
                axisAvailableSpaceForExpansion = availableGridSpace.Get(axis) switch
                {
                    var x when x == Taffy.AvailableSpace.MinContent => AvailableSpace.MinContent,
                    _ => AvailableSpace.MaxContent,
                };
            }

            // 11.7. Expand Flexible Tracks
            // This step sizes flexible tracks using the largest value it can assign to an fr without exceeding the available space.
            ExpandFlexibleTracks(
                tree,
                axis,
                axisTracks,
                items,
                axisMinSize,
                axisMaxSize,
                axisAvailableSpaceForExpansion,
                innerNodeSize);

            // 11.8. Stretch auto Tracks
            // This step expands tracks that have an auto max track sizing function by dividing any remaining positive, definite free space equally amongst them.
            if (axisAlignment == AlignContent.Stretch)
            {
                StretchAutoTracks(axisTracks, axisMinSize, axisAvailableSpaceForExpansion);
            }
        }

        /// <summary>
        /// Add any planned base size increases to the base size after a round of distributing space to base sizes.
        /// Reset the planned base size increase to zero ready for the next round.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void FlushPlannedBaseSizeIncreases(List<GridTrack> tracks)
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i];
                track.BaseSize += track.BaseSizePlannedIncrease;
                track.BaseSizePlannedIncrease = 0f;
                tracks[i] = track;
            }
        }

        /// <summary>
        /// Add any planned growth limit increases to the growth limit after a round of distributing space to growth limits.
        /// Reset the planned growth limit increase to zero ready for the next round.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void FlushPlannedGrowthLimitIncreases(List<GridTrack> tracks, bool setInfinitelyGrowable)
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i];
                if (track.GrowthLimitPlannedIncrease > 0f)
                {
                    track.GrowthLimit = float.IsPositiveInfinity(track.GrowthLimit)
                        ? track.BaseSize + track.GrowthLimitPlannedIncrease
                        : track.GrowthLimit + track.GrowthLimitPlannedIncrease;
                    track.InfinitelyGrowable = setInfinitelyGrowable;
                }
                else
                {
                    track.InfinitelyGrowable = false;
                }
                track.GrowthLimitPlannedIncrease = 0f;
                tracks[i] = track;
            }
        }

        /// <summary>
        /// 11.4 Initialise Track sizes
        /// Initialize each track's base size and growth limit.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void InitializeTrackSizes(ILayoutPartialTree tree, List<GridTrack> axisTracks, float? axisInnerNodeSize)
        {
            for (int i = 0; i < axisTracks.Count; i++)
            {
                var track = axisTracks[i];

                // For each track, if the track's min track sizing function is:
                // - A fixed sizing function: Resolve to an absolute length and use that size as the track's initial base size.
                // - An intrinsic sizing function: Use an initial base size of zero.
                track.BaseSize = track.MinTrackSizingFunction.DefiniteValue(axisInnerNodeSize,
                    (val, basis) => tree.ResolveCalcValue(val, basis)) ?? 0f;

                // For each track, if the track's max track sizing function is:
                // - A fixed sizing function: Resolve to an absolute length and use that size as the track's initial growth limit.
                // - An intrinsic or flexible sizing function: Use an initial growth limit of infinity.
                track.GrowthLimit = track.MaxTrackSizingFunction.DefiniteValue(axisInnerNodeSize,
                    (val, basis) => tree.ResolveCalcValue(val, basis)) ?? float.PositiveInfinity;

                // In all cases, if the growth limit is less than the base size, increase the growth limit to match the base size.
                if (track.GrowthLimit < track.BaseSize)
                    track.GrowthLimit = track.BaseSize;

                axisTracks[i] = track;
            }
        }

        /// <summary>
        /// 11.5.1 Shim baseline-aligned items so their intrinsic size contributions reflect their baseline alignment.
        /// </summary>
        private static void ResolveItemBaselines(ILayoutPartialTree tree, AbstractAxis axis, List<GridItem> items, Size<float?> innerNodeSize)
        {
            // Sort items by track in the other axis (row) start position so that we can iterate items in groups which
            // are in the same track in the other axis (row)
            var otherAxis = axis.Other();
            items.Sort((a, b) => a.Placement(otherAxis).Start.CompareTo(b.Placement(otherAxis).Start));

            int remaining = 0;
            while (remaining < items.Count)
            {
                // Get the row index of the current row
                var currentRow = items[remaining].Placement(otherAxis).Start;

                // Find the item index of the first item that is in a different row
                int nextRowFirstItem = items.Count;
                for (int i = remaining; i < items.Count; i++)
                {
                    if (items[i].Placement(otherAxis).Start != currentRow)
                    {
                        nextRowFirstItem = i;
                        break;
                    }
                }

                int rowStart = remaining;
                int rowEnd = nextRowFirstItem;
                remaining = nextRowFirstItem;

                // Count how many items in *this row* are baseline aligned
                int rowBaselineItemCount = 0;
                for (int i = rowStart; i < rowEnd; i++)
                {
                    if (items[i].AlignSelfStyle == AlignItems.Baseline)
                        rowBaselineItemCount++;
                }
                if (rowBaselineItemCount <= 1)
                    continue;

                // Compute the baselines of all items in the row
                for (int i = rowStart; i < rowEnd; i++)
                {
                    var item = items[i];
                    var measuredSizeAndBaselines = tree.ComputeChildLayout(
                        item.Node,
                        new LayoutInput
                        {
                            KnownDimensions = SizeExtensions.NoneF32,
                            ParentSize = innerNodeSize,
                            AvailableSpace = new Size<AvailableSpace>(AvailableSpace.MinContent, AvailableSpace.MinContent),
                            SizingMode = SizingMode.InherentSize,
                            VerticalMarginsAreCollapsible = LineExtensions.FalseLine,
                            RunMode = RunMode.PerformLayout,
                        });

                    var baseline = measuredSizeAndBaselines.FirstBaselines.Y;
                    var height = measuredSizeAndBaselines.Size.Height;

                    item.Baseline = (baseline ?? height)
                        + item.MarginStyle.Top.ResolveOrZero(innerNodeSize.Width, (val, basis) => tree.ResolveCalcValue(val, basis));
                    items[i] = item;
                }

                // Compute the max baseline of all items in the row
                float rowMaxBaseline = 0f;
                for (int i = rowStart; i < rowEnd; i++)
                {
                    var b = items[i].Baseline ?? 0f;
                    if (b > rowMaxBaseline) rowMaxBaseline = b;
                }

                // Compute the baseline shim for each item in the row
                for (int i = rowStart; i < rowEnd; i++)
                {
                    var item = items[i];
                    item.BaselineShim = rowMaxBaseline - (item.Baseline ?? 0f);
                    items[i] = item;
                }
            }
        }

        /// <summary>
        /// 11.5 Resolve Intrinsic Track Sizes
        /// </summary>
        private static void ResolveIntrinsicTrackSizes<TTree>(
            TTree tree,
            AbstractAxis axis,
            List<GridTrack> axisTracks,
            List<GridTrack> otherAxisTracks,
            List<GridItem> items,
            AvailableSpace axisAvailableGridSpace,
            Size<float?> innerNodeSize,
            Func<GridTrack, float?, TTree, float?> getTrackSizeEstimate) where TTree : ILayoutPartialTree
        {
            // Step 1. Shim baseline-aligned items - already done at this point

            // Step 2.
            // The track sizing algorithm requires us to iterate through the items in ascending order of the number of
            // tracks they span (first items that span 1 track, then items that span 2 tracks, etc).
            items.Sort((a, b) => CmpByCrossFlexThenSpanThenStart(a, b, axis));

            float MinContentContribution(ref GridItem item)
            {
                var avail = item.AvailableSpaceCached(axis, otherAxisTracks, innerNodeSize.Get(axis.Other()),
                    (track, basis) => getTrackSizeEstimate(track, basis, tree));
                var marginSums = item.MarginsAxisSumsWithBaselineShims(innerNodeSize.Width, tree);
                var contribution = item.MinContentContributionCached(axis, tree, avail, innerNodeSize);
                return contribution + marginSums.Get(axis);
            }

            float MaxContentContribution(ref GridItem item)
            {
                var avail = item.AvailableSpaceCached(axis, otherAxisTracks, innerNodeSize.Get(axis.Other()),
                    (track, basis) => getTrackSizeEstimate(track, basis, tree));
                var marginSums = item.MarginsAxisSumsWithBaselineShims(innerNodeSize.Width, tree);
                var contribution = item.MaxContentContributionCached(axis, tree, avail, innerNodeSize);
                return contribution + marginSums.Get(axis);
            }

            float MinimumContribution(ref GridItem item)
            {
                var avail = item.AvailableSpaceCached(axis, otherAxisTracks, innerNodeSize.Get(axis.Other()),
                    (track, basis) => getTrackSizeEstimate(track, basis, tree));
                var marginSums = item.MarginsAxisSumsWithBaselineShims(innerNodeSize.Width, tree);
                var contribution = item.MinimumContributionCached(tree, axis, axisTracks, avail, innerNodeSize);
                return contribution + marginSums.Get(axis);
            }

            float CalcResolver(IntPtr val, float basis) => tree.ResolveCalcValue(val, basis);

            float? axisInnerNodeSize = innerNodeSize.Get(axis);
            float flexFactorSum = 0f;
            for (int i = 0; i < axisTracks.Count; i++)
                flexFactorSum += axisTracks[i].FlexFactor();

            // Get span over list's internal array for ref access without copying
            var itemArr = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(items);
            var batcher = new ItemBatcher(axis);

            while (true)
            {
                var batch = batcher.Next(itemArr, itemArr.Length);
                if (!batch.HasValue) break;

                var (batchStart, batchCount, isFlex) = batch.Value;
                var batchSpan = itemArr[batchStart].Placement(axis).Span();

                // 2. Size tracks to fit non-spanning items
                if (!isFlex && batchSpan == 1)
                {
                    for (int bi = batchStart; bi < batchStart + batchCount; bi++)
                    {
                        ref var item = ref itemArr[bi];
                        var trackIndex = item.PlacementIndexes(axis).Start + 1;
                        var track = axisTracks[trackIndex];

                        // Handle base sizes
                        float newBaseSize;
                        var tag = track.MinTrackSizingFunction.Inner.Tag;
                        if (tag == CompactLength.MIN_CONTENT_TAG)
                        {
                            newBaseSize = MathF.Max(track.BaseSize, MinContentContribution(ref item));
                        }
                        else if (tag == CompactLength.PERCENT_TAG || track.MinTrackSizingFunction.Inner.IsCalc())
                        {
                            // If the container size is indefinite and has not yet been resolved then percentage sized
                            // tracks should be treated as min-content
                            if (!axisInnerNodeSize.HasValue)
                                newBaseSize = MathF.Max(track.BaseSize, MinContentContribution(ref item));
                            else
                                newBaseSize = track.BaseSize;
                        }
                        else if (tag == CompactLength.MAX_CONTENT_TAG)
                        {
                            newBaseSize = MathF.Max(track.BaseSize, MaxContentContribution(ref item));
                        }
                        else if (tag == CompactLength.AUTO_TAG)
                        {
                            float space;
                            if ((axisAvailableGridSpace == Taffy.AvailableSpace.MinContent || axisAvailableGridSpace == Taffy.AvailableSpace.MaxContent)
                                && !item.OverflowStyle.Get(axis).IsScrollContainer())
                            {
                                var axisMinimumSize = MinimumContribution(ref item);
                                var axisMinContentSize = MinContentContribution(ref item);
                                var limit = track.MaxTrackSizingFunction.DefiniteLimit(axisInnerNodeSize, CalcResolver);
                                space = axisMinContentSize.MaybeMin(limit).MaybeMax(axisMinimumSize);
                            }
                            else
                            {
                                space = MinimumContribution(ref item);
                            }
                            newBaseSize = MathF.Max(track.BaseSize, space);
                        }
                        else if (tag == CompactLength.LENGTH_TAG)
                        {
                            newBaseSize = track.BaseSize;
                        }
                        else
                        {
                            newBaseSize = track.BaseSize;
                        }

                        track.BaseSize = newBaseSize;

                        // Handle growth limits
                        if (track.MaxTrackSizingFunction.IsFitContent())
                        {
                            if (!item.OverflowStyle.Get(axis).IsScrollContainer())
                            {
                                float minCC = MinContentContribution(ref item);
                                track.GrowthLimitPlannedIncrease = MathF.Max(track.GrowthLimitPlannedIncrease, minCC);
                            }
                            float fitContentLimit = track.FitContentLimit(axisInnerNodeSize);
                            float maxCC = MathF.Min(MaxContentContribution(ref item), fitContentLimit);
                            track.GrowthLimitPlannedIncrease = MathF.Max(track.GrowthLimitPlannedIncrease, maxCC);
                        }
                        else if (track.MaxTrackSizingFunction.IsMaxContentAlike()
                            || (track.MaxTrackSizingFunction.UsesPercentage() && !axisInnerNodeSize.HasValue))
                        {
                            track.GrowthLimitPlannedIncrease = MathF.Max(track.GrowthLimitPlannedIncrease, MaxContentContribution(ref item));
                        }
                        else if (track.MaxTrackSizingFunction.IsIntrinsic())
                        {
                            track.GrowthLimitPlannedIncrease = MathF.Max(track.GrowthLimitPlannedIncrease, MinContentContribution(ref item));
                        }

                        axisTracks[trackIndex] = track;
                    }

                    for (int i = 0; i < axisTracks.Count; i++)
                    {
                        var track = axisTracks[i];
                        if (track.GrowthLimitPlannedIncrease > 0f)
                        {
                            track.GrowthLimit = float.IsPositiveInfinity(track.GrowthLimit)
                                ? track.GrowthLimitPlannedIncrease
                                : MathF.Max(track.GrowthLimit, track.GrowthLimitPlannedIncrease);
                        }
                        track.InfinitelyGrowable = false;
                        track.GrowthLimitPlannedIncrease = 0f;
                        if (track.GrowthLimit < track.BaseSize)
                            track.GrowthLimit = track.BaseSize;
                        axisTracks[i] = track;
                    }

                    // Copy items back for this batch
                    for (int bi = batchStart; bi < batchStart + batchCount; bi++)
                        items[bi] = itemArr[bi];

                    continue;
                }

                bool useFlexFactorForDistribution = isFlex && flexFactorSum != 0f;

                // 1. For intrinsic minimums
                for (int bi = batchStart; bi < batchStart + batchCount; bi++)
                {
                    ref var item = ref itemArr[bi];
                    if (!item.CrossesIntrinsicTrack(axis)) continue;

                    float space;
                    if ((axisAvailableGridSpace == Taffy.AvailableSpace.MinContent || axisAvailableGridSpace == Taffy.AvailableSpace.MaxContent)
                        && !item.OverflowStyle.Get(axis).IsScrollContainer())
                    {
                        var axisMinimumSize = MinimumContribution(ref item);
                        var axisMinContentSize = MinContentContribution(ref item);
                        var limit = item.SpannedTrackLimit(axis, axisTracks, axisInnerNodeSize, CalcResolver);
                        space = axisMinContentSize.MaybeMin(limit).MaybeMax(axisMinimumSize);
                    }
                    else
                    {
                        space = MinimumContribution(ref item);
                    }

                    if (space > 0f)
                    {
                        var range = item.TrackRangeExcludingLines(axis);
                        bool hasIntrinsicMin(GridTrack t) =>
                            !t.MinTrackSizingFunction.DefiniteValue(axisInnerNodeSize, CalcResolver).HasValue;

                        if (item.OverflowStyle.Get(axis).IsScrollContainer())
                        {
                            DistributeItemSpaceToBaseSize(isFlex, useFlexFactorForDistribution, space,
                                axisTracks, range.Start, range.End, hasIntrinsicMin,
                                t => t.FitContentLimitedGrowthLimit(axisInnerNodeSize),
                                IntrinsicContributionType.Minimum);
                        }
                        else
                        {
                            DistributeItemSpaceToBaseSize(isFlex, useFlexFactorForDistribution, space,
                                axisTracks, range.Start, range.End, hasIntrinsicMin,
                                t => t.GrowthLimit,
                                IntrinsicContributionType.Minimum);
                        }
                    }
                }
                FlushPlannedBaseSizeIncreases(axisTracks);

                // 2. For content-based minimums
                for (int bi = batchStart; bi < batchStart + batchCount; bi++)
                {
                    ref var item = ref itemArr[bi];
                    float space = MinContentContribution(ref item);
                    if (space > 0f)
                    {
                        var range = item.TrackRangeExcludingLines(axis);
                        bool hasMinOrMaxContentMin(GridTrack t) => t.MinTrackSizingFunction.IsMinOrMaxContent();

                        if (item.OverflowStyle.Get(axis).IsScrollContainer())
                        {
                            DistributeItemSpaceToBaseSize(isFlex, useFlexFactorForDistribution, space,
                                axisTracks, range.Start, range.End, hasMinOrMaxContentMin,
                                t => t.FitContentLimitedGrowthLimit(axisInnerNodeSize),
                                IntrinsicContributionType.Minimum);
                        }
                        else
                        {
                            DistributeItemSpaceToBaseSize(isFlex, useFlexFactorForDistribution, space,
                                axisTracks, range.Start, range.End, hasMinOrMaxContentMin,
                                t => t.GrowthLimit,
                                IntrinsicContributionType.Minimum);
                        }
                    }
                }
                FlushPlannedBaseSizeIncreases(axisTracks);

                // 3. For max-content minimums
                if (axisAvailableGridSpace == Taffy.AvailableSpace.MaxContent)
                {
                    for (int bi = batchStart; bi < batchStart + batchCount; bi++)
                    {
                        ref var item = ref itemArr[bi];
                        float axisMaxContentSize = MaxContentContribution(ref item);
                        var limit = item.SpannedTrackLimit(axis, axisTracks, axisInnerNodeSize, CalcResolver);
                        float space = axisMaxContentSize.MaybeMin(limit);
                        if (space > 0f)
                        {
                            var range = item.TrackRangeExcludingLines(axis);
                            bool hasMaxContentMin(GridTrack t) => t.MinTrackSizingFunction == MinTrackSizingFunction.MAX_CONTENT;
                            bool hasAutoMin(GridTrack t) => t.MinTrackSizingFunction.IsAuto() && t.MaxTrackSizingFunction != MaxTrackSizingFunction.MIN_CONTENT;

                            bool anyMaxContentMin = false;
                            for (int ri = range.Start; ri < range.End; ri += 2)
                                if (hasMaxContentMin(axisTracks[ri])) { anyMaxContentMin = true; break; }

                            if (anyMaxContentMin)
                            {
                                DistributeItemSpaceToBaseSize(isFlex, useFlexFactorForDistribution, space,
                                    axisTracks, range.Start, range.End, hasMaxContentMin,
                                    _ => float.PositiveInfinity,
                                    IntrinsicContributionType.Maximum);
                            }
                            else
                            {
                                DistributeItemSpaceToBaseSize(isFlex, useFlexFactorForDistribution, space,
                                    axisTracks, range.Start, range.End, hasAutoMin,
                                    t => t.FitContentLimitedGrowthLimit(axisInnerNodeSize),
                                    IntrinsicContributionType.Maximum);
                            }
                        }
                    }
                    FlushPlannedBaseSizeIncreases(axisTracks);
                }

                // In all cases, continue to increase the base size of tracks with a min track sizing function of max-content
                for (int bi = batchStart; bi < batchStart + batchCount; bi++)
                {
                    ref var item = ref itemArr[bi];
                    float space = MaxContentContribution(ref item);
                    if (space > 0f)
                    {
                        var range = item.TrackRangeExcludingLines(axis);
                        bool hasMaxContentMin(GridTrack t) => t.MinTrackSizingFunction == MinTrackSizingFunction.MAX_CONTENT;
                        DistributeItemSpaceToBaseSize(isFlex, useFlexFactorForDistribution, space,
                            axisTracks, range.Start, range.End, hasMaxContentMin,
                            t => t.GrowthLimit,
                            IntrinsicContributionType.Maximum);
                    }
                }
                FlushPlannedBaseSizeIncreases(axisTracks);

                // 4. If at this point any track's growth limit is now less than its base size, increase its growth limit to match
                for (int i = 0; i < axisTracks.Count; i++)
                {
                    var track = axisTracks[i];
                    if (track.GrowthLimit < track.BaseSize)
                    {
                        track.GrowthLimit = track.BaseSize;
                        axisTracks[i] = track;
                    }
                }

                // If a track is a flexible track, then it has flexible max track sizing function
                // It cannot also have an intrinsic max track sizing function, so these steps do not apply.
                if (!isFlex)
                {
                    // 5. For intrinsic maximums
                    for (int bi = batchStart; bi < batchStart + batchCount; bi++)
                    {
                        ref var item = ref itemArr[bi];
                        float space = MinContentContribution(ref item);
                        if (space > 0f)
                        {
                            var range = item.TrackRangeExcludingLines(axis);
                            bool hasIntrinsicMax(GridTrack t) => !t.MaxTrackSizingFunction.HasDefiniteValue(axisInnerNodeSize);
                            DistributeItemSpaceToGrowthLimit(space, axisTracks, range.Start, range.End, hasIntrinsicMax, axisInnerNodeSize);
                        }
                    }
                    FlushPlannedGrowthLimitIncreases(axisTracks, true);

                    // 6. For max-content maximums
                    for (int bi = batchStart; bi < batchStart + batchCount; bi++)
                    {
                        ref var item = ref itemArr[bi];
                        float space = MaxContentContribution(ref item);
                        if (space > 0f)
                        {
                            var range = item.TrackRangeExcludingLines(axis);
                            bool hasMaxContentMax(GridTrack t) =>
                                t.MaxTrackSizingFunction.IsMaxContentAlike()
                                || (t.MaxTrackSizingFunction.UsesPercentage() && !axisInnerNodeSize.HasValue);
                            DistributeItemSpaceToGrowthLimit(space, axisTracks, range.Start, range.End, hasMaxContentMax, axisInnerNodeSize);
                        }
                    }
                    FlushPlannedGrowthLimitIncreases(axisTracks, false);
                }

                // Copy items back
                for (int bi = batchStart; bi < batchStart + batchCount; bi++)
                    items[bi] = itemArr[bi];
            }

            // Step 5. If any track still has an infinite growth limit set its growth limit to its base size.
            for (int i = 0; i < axisTracks.Count; i++)
            {
                var track = axisTracks[i];
                if (float.IsPositiveInfinity(track.GrowthLimit))
                {
                    track.GrowthLimit = track.BaseSize;
                    axisTracks[i] = track;
                }
            }
        }

        /// <summary>
        /// 11.5.1. Distributing Extra Space Across Spanned Tracks
        /// https://www.w3.org/TR/css-grid-1/#extra-space
        /// </summary>
        private static void DistributeItemSpaceToBaseSize(
            bool isFlex,
            bool useFlexFactorForDistribution,
            float space,
            List<GridTrack> tracks,
            int rangeStart,
            int rangeEnd,
            Func<GridTrack, bool> trackIsAffected,
            Func<GridTrack, float> trackLimit,
            IntrinsicContributionType intrinsicContributionType)
        {
            Func<GridTrack, bool> filter;
            Func<GridTrack, float> proportion;

            if (isFlex)
            {
                filter = t => t.IsFlexible() && trackIsAffected(t);
                proportion = useFlexFactorForDistribution ? (t => t.FlexFactor()) : (_ => 1f);
            }
            else
            {
                filter = trackIsAffected;
                proportion = _ => 1f;
            }

            // Skip this distribution if there are no affected tracks
            bool anyAffected = false;
            for (int i = rangeStart; i < rangeEnd; i += 2)
                if (filter(tracks[i])) { anyAffected = true; break; }
            if (space == 0f || !anyAffected) return;

            // 1. Find the space to distribute
            // Sum ALL tracks in range (including gutters) to match Rust sub-slice behavior
            float trackSizes = 0f;
            for (int i = rangeStart; i < rangeEnd; i++)
                trackSizes += tracks[i].BaseSize;
            float extraSpace = MathF.Max(0f, space - trackSizes);

            const float THRESHOLD = 0.000001f;

            // 2. Distribute space up to limits
            extraSpace = DistributeSpaceUpToLimits(
                extraSpace, tracks, rangeStart, rangeEnd,
                filter, proportion,
                t => t.BaseSize, trackLimit);

            // 3. Distribute remaining space beyond limits (if any)
            if (extraSpace > THRESHOLD)
            {
                Func<GridTrack, bool> beyondFilter;
                switch (intrinsicContributionType)
                {
                    case IntrinsicContributionType.Minimum:
                        beyondFilter = t => t.MaxTrackSizingFunction.IsIntrinsic();
                        break;
                    case IntrinsicContributionType.Maximum:
                        beyondFilter = t => t.MinTrackSizingFunction == MinTrackSizingFunction.MAX_CONTENT || t.MaxTrackSizingFunction.IsMaxOrFitContent();
                        break;
                    default:
                        beyondFilter = _ => true;
                        break;
                }

                // If there are no such tracks, then use all affected tracks
                bool anyMatch = false;
                for (int i = rangeStart; i < rangeEnd; i += 2)
                    if (filter(tracks[i]) && beyondFilter(tracks[i])) { anyMatch = true; break; }
                if (!anyMatch)
                    beyondFilter = _ => true;

                DistributeSpaceUpToLimits(
                    extraSpace, tracks, rangeStart, rangeEnd,
                    beyondFilter, proportion,
                    t => t.BaseSize, trackLimit);
            }

            // 4. For each affected track, if the track's item-incurred increase is larger than the track's planned increase
            // set the track's planned increase to that value.
            for (int i = rangeStart; i < rangeEnd; i += 2)
            {
                var track = tracks[i];
                if (track.ItemIncurredIncrease > track.BaseSizePlannedIncrease)
                    track.BaseSizePlannedIncrease = track.ItemIncurredIncrease;
                track.ItemIncurredIncrease = 0f;
                tracks[i] = track;
            }
        }

        /// <summary>
        /// 11.5.1. Distributing Extra Space Across Spanned Tracks
        /// Simplified (and faster) version for growth limits
        /// </summary>
        private static void DistributeItemSpaceToGrowthLimit(
            float space,
            List<GridTrack> tracks,
            int rangeStart,
            int rangeEnd,
            Func<GridTrack, bool> trackIsAffected,
            float? axisInnerNodeSize)
        {
            if (space == 0f) return;
            bool anyAffected = false;
            for (int i = rangeStart; i < rangeEnd; i += 2)
                if (trackIsAffected(tracks[i])) { anyAffected = true; break; }
            if (!anyAffected) return;

            // 1. Find the space to distribute
            // Sum ALL tracks in range (including gutters) to match Rust sub-slice behavior
            float trackSizes = 0f;
            for (int i = rangeStart; i < rangeEnd; i++)
            {
                var t = tracks[i];
                trackSizes += float.IsPositiveInfinity(t.GrowthLimit) ? t.BaseSize : t.GrowthLimit;
            }
            float extraSpace = MathF.Max(0f, space - trackSizes);

            // 2. Distribute space up to limits
            int numberOfGrowableTracks = 0;
            for (int i = rangeStart; i < rangeEnd; i += 2)
            {
                var t = tracks[i];
                if (trackIsAffected(t) && (t.InfinitelyGrowable || float.IsPositiveInfinity(t.FitContentLimitedGrowthLimit(axisInnerNodeSize))))
                    numberOfGrowableTracks++;
            }

            if (numberOfGrowableTracks > 0)
            {
                float itemIncurredIncrease = extraSpace / numberOfGrowableTracks;
                for (int i = rangeStart; i < rangeEnd; i += 2)
                {
                    var t = tracks[i];
                    if (trackIsAffected(t) && (t.InfinitelyGrowable || float.IsPositiveInfinity(t.FitContentLimitedGrowthLimit(axisInnerNodeSize))))
                    {
                        t.ItemIncurredIncrease = itemIncurredIncrease;
                        tracks[i] = t;
                    }
                }
            }
            else
            {
                // 3. Distribute space beyond limits
                DistributeSpaceUpToLimits(
                    extraSpace, tracks, rangeStart, rangeEnd,
                    trackIsAffected,
                    _ => 1f,
                    t => float.IsPositiveInfinity(t.GrowthLimit) ? t.BaseSize : t.GrowthLimit,
                    t => t.FitContentLimit(axisInnerNodeSize));
            }

            // 4. For each affected track
            for (int i = rangeStart; i < rangeEnd; i += 2)
            {
                var track = tracks[i];
                if (track.ItemIncurredIncrease > track.GrowthLimitPlannedIncrease)
                    track.GrowthLimitPlannedIncrease = track.ItemIncurredIncrease;
                track.ItemIncurredIncrease = 0f;
                tracks[i] = track;
            }
        }

        /// <summary>
        /// 11.6 Maximise Tracks
        /// Distributes free space (if any) to tracks with FINITE growth limits, up to their limits.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MaximiseTracks(List<GridTrack> axisTracks, float? axisInnerNodeSize, AvailableSpace axisAvailableGridSpace)
        {
            float usedSpace = 0f;
            for (int i = 0; i < axisTracks.Count; i++)
                usedSpace += axisTracks[i].BaseSize;

            float freeSpace = axisAvailableGridSpace.ComputeFreeSpace(usedSpace);
            if (float.IsPositiveInfinity(freeSpace))
            {
                for (int i = 0; i < axisTracks.Count; i++)
                {
                    var track = axisTracks[i];
                    track.BaseSize = track.GrowthLimit;
                    axisTracks[i] = track;
                }
            }
            else if (freeSpace > 0f)
            {
                DistributeSpaceUpToLimits(
                    freeSpace, axisTracks, 0, axisTracks.Count,
                    _ => true, _ => 1f,
                    t => t.BaseSize,
                    t => t.FitContentLimitedGrowthLimit(axisInnerNodeSize));

                for (int i = 0; i < axisTracks.Count; i++)
                {
                    var track = axisTracks[i];
                    track.BaseSize += track.ItemIncurredIncrease;
                    track.ItemIncurredIncrease = 0f;
                    axisTracks[i] = track;
                }
            }
        }

        /// <summary>
        /// 11.7. Expand Flexible Tracks
        /// This step sizes flexible tracks using the largest value it can assign to an fr without exceeding the available space.
        /// </summary>
        private static void ExpandFlexibleTracks(
            ILayoutPartialTree tree,
            AbstractAxis axis,
            List<GridTrack> axisTracks,
            List<GridItem> items,
            float? axisMinSize,
            float? axisMaxSize,
            AvailableSpace axisAvailableSpaceForExpansion,
            Size<float?> innerNodeSize)
        {
            // First, find the grid's used flex fraction
            float flexFraction;
            if (axisAvailableSpaceForExpansion.IsDefinite())
            {
                float availableSpace = axisAvailableSpaceForExpansion.IntoOption()!.Value;
                float usedSpace = 0f;
                for (int i = 0; i < axisTracks.Count; i++)
                    usedSpace += axisTracks[i].BaseSize;
                float freeSpace = availableSpace - usedSpace;
                if (freeSpace <= 0f)
                    flexFraction = 0f;
                else
                    flexFraction = FindSizeOfFr(axisTracks, availableSpace);
            }
            else if (axisAvailableSpaceForExpansion == Taffy.AvailableSpace.MinContent)
            {
                flexFraction = 0f;
            }
            else
            {
                // MaxContent case
                float maxFrTrack = 0f;
                for (int i = 0; i < axisTracks.Count; i++)
                {
                    var track = axisTracks[i];
                    if (track.MaxTrackSizingFunction.IsFr())
                    {
                        float ff = track.FlexFactor();
                        float val = ff > 1f ? track.BaseSize / ff : track.BaseSize;
                        if (val > maxFrTrack) maxFrTrack = val;
                    }
                }

                float maxFrItem = 0f;
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (item.CrossesFlexibleTrack(axis))
                    {
                        var range = item.TrackRangeExcludingLines(axis);
                        // Create a sub-list of tracks for this item
                        var subTracks = new List<GridTrack>();
                        for (int j = range.Start; j < range.End; j += 2)
                            subTracks.Add(axisTracks[j]);

                        float maxContentContribution = item.MaxContentContributionCached(axis, tree, SizeExtensions.NoneF32, innerNodeSize);
                        float fr = FindSizeOfFr(subTracks, maxContentContribution);
                        if (fr > maxFrItem) maxFrItem = fr;
                    }
                }

                flexFraction = MathF.Max(maxFrTrack, maxFrItem);

                // Check if using this flex fraction would violate min/max constraints
                float hypotheticalGridSize = 0f;
                for (int i = 0; i < axisTracks.Count; i++)
                {
                    var track = axisTracks[i];
                    if (track.MaxTrackSizingFunction.IsFr())
                    {
                        float trackFlexFactor = track.MaxTrackSizingFunction.Inner.Value;
                        hypotheticalGridSize += MathF.Max(track.BaseSize, trackFlexFactor * flexFraction);
                    }
                    else
                    {
                        hypotheticalGridSize += track.BaseSize;
                    }
                }

                float minSz = axisMinSize ?? 0f;
                float maxSz = axisMaxSize ?? float.PositiveInfinity;
                if (hypotheticalGridSize < minSz)
                    flexFraction = FindSizeOfFr(axisTracks, minSz);
                else if (hypotheticalGridSize > maxSz)
                    flexFraction = FindSizeOfFr(axisTracks, maxSz);
            }

            // For each flexible track, if the product of the used flex fraction and the track's flex factor is greater
            // than the track's base size, set its base size to that product.
            for (int i = 0; i < axisTracks.Count; i++)
            {
                var track = axisTracks[i];
                if (track.MaxTrackSizingFunction.IsFr())
                {
                    float trackFlexFactor = track.MaxTrackSizingFunction.Inner.Value;
                    track.BaseSize = MathF.Max(track.BaseSize, trackFlexFactor * flexFraction);
                    axisTracks[i] = track;
                }
            }
        }

        /// <summary>
        /// 11.7.1. Find the Size of an fr
        /// This algorithm finds the largest size that an fr unit can be without exceeding the target size.
        /// It must be called with a set of grid tracks and some quantity of space to fill.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FindSizeOfFr(List<GridTrack> tracks, float spaceToFill)
        {
            // Handle the trivial case where there is no space to fill
            if (spaceToFill == 0f) return 0f;

            float hypotheticalFrSize = float.PositiveInfinity;
            float previousIterHypotheticalFrSize;

            while (true)
            {
                // Let leftover space be the space to fill minus the base sizes of the non-flexible grid tracks.
                // Let flex factor sum be the sum of the flex factors of the flexible tracks.
                float usedSpace = 0f;
                float naiveFlexFactorSum = 0f;
                for (int i = 0; i < tracks.Count; i++)
                {
                    var track = tracks[i];
                    if (track.MaxTrackSizingFunction.IsFr()
                        && track.MaxTrackSizingFunction.Inner.Value * hypotheticalFrSize >= track.BaseSize)
                    {
                        naiveFlexFactorSum += track.MaxTrackSizingFunction.Inner.Value;
                    }
                    else
                    {
                        usedSpace += track.BaseSize;
                    }
                }

                float leftoverSpace = spaceToFill - usedSpace;
                float flexFactor = MathF.Max(naiveFlexFactorSum, 1f);

                previousIterHypotheticalFrSize = hypotheticalFrSize;
                hypotheticalFrSize = leftoverSpace / flexFactor;

                // Check validity
                bool valid = true;
                for (int i = 0; i < tracks.Count; i++)
                {
                    var track = tracks[i];
                    if (track.MaxTrackSizingFunction.IsFr())
                    {
                        float ff = track.MaxTrackSizingFunction.Inner.Value;
                        if (!(ff * hypotheticalFrSize >= track.BaseSize
                            || ff * previousIterHypotheticalFrSize < track.BaseSize))
                        {
                            valid = false;
                            break;
                        }
                    }
                }

                if (valid) break;
            }

            return hypotheticalFrSize;
        }

        /// <summary>
        /// 11.8. Stretch auto Tracks
        /// This step expands tracks that have an auto max track sizing function by dividing any remaining positive, definite free space equally amongst them.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void StretchAutoTracks(List<GridTrack> axisTracks, float? axisMinSize, AvailableSpace axisAvailableSpaceForExpansion)
        {
            int numAutoTracks = 0;
            for (int i = 0; i < axisTracks.Count; i++)
                if (axisTracks[i].MaxTrackSizingFunction.IsAuto()) numAutoTracks++;

            if (numAutoTracks > 0)
            {
                float usedSpace = 0f;
                for (int i = 0; i < axisTracks.Count; i++)
                    usedSpace += axisTracks[i].BaseSize;

                // If the free space is indefinite, but the grid container has a definite min-width/height
                // use that size to calculate the free space for this step instead.
                float freeSpace;
                if (axisAvailableSpaceForExpansion.IsDefinite())
                {
                    freeSpace = axisAvailableSpaceForExpansion.ComputeFreeSpace(usedSpace);
                }
                else
                {
                    freeSpace = axisMinSize.HasValue ? axisMinSize.Value - usedSpace : 0f;
                }

                if (freeSpace > 0f)
                {
                    float extraSpacePerAutoTrack = freeSpace / numAutoTracks;
                    for (int i = 0; i < axisTracks.Count; i++)
                    {
                        if (axisTracks[i].MaxTrackSizingFunction.IsAuto())
                        {
                            var track = axisTracks[i];
                            track.BaseSize += extraSpacePerAutoTrack;
                            axisTracks[i] = track;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Helper function for distributing space to tracks evenly.
        /// Used by both distribute_item_space_to_base_size and maximise_tracks steps.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DistributeSpaceUpToLimits(
            float spaceToDistribute,
            List<GridTrack> tracks,
            int rangeStart,
            int rangeEnd,
            Func<GridTrack, bool> trackIsAffected,
            Func<GridTrack, float> trackDistributionProportion,
            Func<GridTrack, float> trackAffectedProperty,
            Func<GridTrack, float> trackLimit)
        {
            const float THRESHOLD = 0.01f;

            float remaining = spaceToDistribute;
            while (remaining > THRESHOLD)
            {
                float proportionSum = 0f;
                for (int i = rangeStart; i < rangeEnd; i += (rangeEnd == tracks.Count ? 1 : 2))
                {
                    var track = tracks[i];
                    if (trackAffectedProperty(track) + track.ItemIncurredIncrease < trackLimit(track)
                        && trackIsAffected(track))
                    {
                        proportionSum += trackDistributionProportion(track);
                    }
                }

                if (proportionSum == 0f) break;

                // Compute item-incurred increase for this iteration
                float minIncreaseLimit = float.PositiveInfinity;
                for (int i = rangeStart; i < rangeEnd; i += (rangeEnd == tracks.Count ? 1 : 2))
                {
                    var track = tracks[i];
                    if (trackAffectedProperty(track) + track.ItemIncurredIncrease < trackLimit(track)
                        && trackIsAffected(track))
                    {
                        float val = (trackLimit(track) - trackAffectedProperty(track)) / trackDistributionProportion(track);
                        if (val < minIncreaseLimit) minIncreaseLimit = val;
                    }
                }

                float iterationItemIncurredIncrease = MathF.Min(minIncreaseLimit, remaining / proportionSum);

                for (int i = rangeStart; i < rangeEnd; i += (rangeEnd == tracks.Count ? 1 : 2))
                {
                    var track = tracks[i];
                    if (trackIsAffected(track))
                    {
                        float increase = iterationItemIncurredIncrease * trackDistributionProportion(track);
                        if (increase > 0f && trackAffectedProperty(track) + increase <= trackLimit(track) + THRESHOLD)
                        {
                            track.ItemIncurredIncrease += increase;
                            remaining -= increase;
                            tracks[i] = track;
                        }
                    }
                }
            }

            return remaining;
        }
    }
}
