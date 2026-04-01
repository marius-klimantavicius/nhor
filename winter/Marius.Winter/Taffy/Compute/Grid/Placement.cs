// Ported from taffy/src/compute/grid/placement.rs
// Implements placing items in the grid and resolving the implicit grid.
// https://www.w3.org/TR/css-grid-1/#placement

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// Grid item placement algorithm
    /// </summary>
    public static class PlacementUtils
    {
        /// <summary>
        /// Returns whether placement/search should run in reverse for this axis.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AxisIsReversed(Direction direction, AbsoluteAxis axis)
        {
            return direction.IsRtl() && axis == AbsoluteAxis.Horizontal;
        }

        /// <summary>
        /// Advances the cursor by one track in the active search direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static OriginZeroLine AdvancePosition(OriginZeroLine position, bool axisIsReversed)
        {
            return axisIsReversed
                ? new OriginZeroLine((short)(position.Value - 1))
                : new OriginZeroLine((short)(position.Value + 1));
        }

        /// <summary>
        /// Returns the initial search line for sparse/dense placement in the given axis direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static OriginZeroLine SearchStartLine(
            OriginZeroLine gridStartLine,
            OriginZeroLine gridEndLine,
            bool axisIsReversed)
        {
            return axisIsReversed ? gridEndLine - 1 : gridStartLine;
        }

        /// <summary>
        /// Resolves an indefinite span at position, respecting the active axis direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Line<OriginZeroLine> ResolveIndefiniteGridSpan(
            OriginZeroLine position, ushort span, bool axisIsReversed)
        {
            if (axisIsReversed)
            {
                return new Line<OriginZeroLine>
                {
                    Start = (position - span) + 1,
                    End = position + 1,
                };
            }
            else
            {
                return new Line<OriginZeroLine>
                {
                    Start = position,
                    End = position + span,
                };
            }
        }

        /// <summary>
        /// Mirrors a horizontal span around the explicit grid width.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Line<OriginZeroLine> MirrorHorizontalSpan(
            Line<OriginZeroLine> span, ushort explicitColCount)
        {
            var explicitColEndLine = (short)explicitColCount;
            return new Line<OriginZeroLine>
            {
                Start = new OriginZeroLine((short)(explicitColEndLine - span.End.Value)),
                End = new OriginZeroLine((short)(explicitColEndLine - span.Start.Value)),
            };
        }

        /// <summary>
        /// Mirrors horizontal spans for RTL while leaving all other spans unchanged.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Line<OriginZeroLine> MaybeMirrorSpan(
            Line<OriginZeroLine> span,
            AbsoluteAxis axis,
            Direction direction,
            ushort explicitColCount)
        {
            if (axis == AbsoluteAxis.Horizontal && direction.IsRtl())
                return MirrorHorizontalSpan(span, explicitColCount);
            return span;
        }

        /// <summary>
        /// 8.5. Grid Item Placement Algorithm
        /// Place items into the grid, generating new rows/columns into the implicit grid as required
        ///
        /// [Specification](https://www.w3.org/TR/css-grid-2/#auto-placement-algo)
        /// </summary>
        public static void PlaceGridItems(
            CellOccupancyMatrix cellOccupancyMatrix,
            ref ValueList<GridItem> items,
            Func<IEnumerable<(int index, NodeId node, Style style)>> childrenIter,
            Direction direction,
            GridAutoFlow gridAutoFlow,
            AlignItems alignItems,
            AlignItems justifyItems,
            NamedLineResolver namedLineResolver)
        {
            var primaryAxis = gridAutoFlow.PrimaryAxis();
            var secondaryAxis = primaryAxis.OtherAxis();

            var explicitColCount = cellOccupancyMatrix.GetTrackCounts(AbsoluteAxis.Horizontal).Explicit;
            var explicitRowCount = cellOccupancyMatrix.GetTrackCounts(AbsoluteAxis.Vertical).Explicit;

            InBothAbsAxis<Line<GenericGridPlacement<OriginZeroLine>>> MapChildStyleToOriginZeroPlacement(
                int index, NodeId node, Style style)
            {
                var colLine = namedLineResolver.ResolveColumnNames(style.GetGridColumn());
                var rowLine = namedLineResolver.ResolveRowNames(style.GetGridRow());
                return new InBothAbsAxis<Line<GenericGridPlacement<OriginZeroLine>>>
                {
                    Horizontal = colLine.Map(p => p.IntoOriginZeroPlacement(explicitColCount)),
                    Vertical = rowLine.Map(p => p.IntoOriginZeroPlacement(explicitRowCount)),
                };
            }

            // 1. Place children with definite positions
            foreach (var (index, childNode, style) in childrenIter())
            {
                var placement = MapChildStyleToOriginZeroPlacement(index, childNode, style);
                if (placement.Horizontal.IsDefinite() && placement.Vertical.IsDefinite())
                {
                    var (rowSpan, colSpan) = PlaceDefiniteGridItem(placement, primaryAxis, direction, explicitColCount);
                    RecordGridPlacement(
                        cellOccupancyMatrix,
                        ref items,
                        childNode,
                        index,
                        style,
                        alignItems,
                        justifyItems,
                        primaryAxis,
                        rowSpan,
                        colSpan,
                        CellOccupancyState.DefinitelyPlaced);
                }
            }

            // 2. Place remaining children with definite secondary axis positions
            foreach (var (index, childNode, style) in childrenIter())
            {
                var placement = MapChildStyleToOriginZeroPlacement(index, childNode, style);
                if (placement.Get(secondaryAxis).IsDefinite() && !placement.Get(primaryAxis).IsDefinite())
                {
                    var (primarySpan, secondarySpan) =
                        PlaceDefiniteSecondaryAxisItem(cellOccupancyMatrix, placement, gridAutoFlow, direction, explicitColCount);

                    RecordGridPlacement(
                        cellOccupancyMatrix,
                        ref items,
                        childNode,
                        index,
                        style,
                        alignItems,
                        justifyItems,
                        primaryAxis,
                        primarySpan,
                        secondarySpan,
                        CellOccupancyState.AutoPlaced);
                }
            }

            // 3. Determine the number of columns in the implicit grid
            // Already accounted for by expand_to_fit_range, mark_area_as, and grid size estimate

            // 4. Position the remaining grid items
            // (which either have definite position only in the secondary axis or indefinite positions in both axis)
            var primaryAxisGridStartLine = cellOccupancyMatrix.GetTrackCounts(primaryAxis).ImplicitStartLine();
            var primaryAxisGridEndLine = cellOccupancyMatrix.GetTrackCounts(primaryAxis).ImplicitEndLine();
            var secondaryAxisGridStartLine = cellOccupancyMatrix.GetTrackCounts(secondaryAxis).ImplicitStartLine();
            var secondaryAxisGridEndLine = cellOccupancyMatrix.GetTrackCounts(secondaryAxis).ImplicitEndLine();
            var primaryAxisIsReversed = AxisIsReversed(direction, primaryAxis);
            var gridStartPosition = (
                SearchStartLine(primaryAxisGridStartLine, primaryAxisGridEndLine, primaryAxisIsReversed),
                SearchStartLine(secondaryAxisGridStartLine, secondaryAxisGridEndLine, AxisIsReversed(direction, secondaryAxis))
            );
            var gridPosition = gridStartPosition;

            foreach (var (index, childNode, style) in childrenIter())
            {
                var placement = MapChildStyleToOriginZeroPlacement(index, childNode, style);
                if (!placement.Get(secondaryAxis).IsDefinite())
                {
                    // Compute placement
                    var (primarySpan, secondarySpan) = PlaceIndefinitelyPositionedItem(
                        cellOccupancyMatrix,
                        placement,
                        gridAutoFlow,
                        gridPosition,
                        direction,
                        explicitColCount);

                    // Record item
                    RecordGridPlacement(
                        cellOccupancyMatrix,
                        ref items,
                        childNode,
                        index,
                        style,
                        alignItems,
                        justifyItems,
                        primaryAxis,
                        primarySpan,
                        secondarySpan,
                        CellOccupancyState.AutoPlaced);

                    // If using the "dense" placement algorithm then reset the grid position back to grid_start_position ready for the next item
                    // Otherwise set it to the position of the current item so that the next item is placed after it.
                    if (gridAutoFlow.IsDense())
                    {
                        gridPosition = gridStartPosition;
                    }
                    else if (primaryAxisIsReversed)
                    {
                        gridPosition = (primarySpan.Start, secondarySpan.Start);
                    }
                    else
                    {
                        gridPosition = (primarySpan.End, secondarySpan.Start);
                    }
                }
            }
        }

        /// <summary>
        /// 8.5. Grid Item Placement Algorithm
        /// Place a single definitely placed item into the grid
        /// </summary>
        private static (Line<OriginZeroLine> primarySpan, Line<OriginZeroLine> secondarySpan) PlaceDefiniteGridItem(
            InBothAbsAxis<Line<GenericGridPlacement<OriginZeroLine>>> placement,
            AbsoluteAxis primaryAxis,
            Direction direction,
            ushort explicitColCount)
        {
            // Resolve spans to tracks
            var primarySpan = MaybeMirrorSpan(
                placement.Get(primaryAxis).ResolveDefiniteGridLines(),
                primaryAxis,
                direction,
                explicitColCount);
            var secondarySpan = MaybeMirrorSpan(
                placement.Get(primaryAxis.OtherAxis()).ResolveDefiniteGridLines(),
                primaryAxis.OtherAxis(),
                direction,
                explicitColCount);
            return (primarySpan, secondarySpan);
        }

        /// <summary>
        /// 8.5. Grid Item Placement Algorithm
        /// Step 2. Place remaining children with definite secondary axis positions
        /// </summary>
        private static (Line<OriginZeroLine> primarySpan, Line<OriginZeroLine> secondarySpan)
            PlaceDefiniteSecondaryAxisItem(
                CellOccupancyMatrix cellOccupancyMatrix,
                InBothAbsAxis<Line<GenericGridPlacement<OriginZeroLine>>> placement,
                GridAutoFlow autoFlow,
                Direction direction,
                ushort explicitColCount)
        {
            var primaryAxis = autoFlow.PrimaryAxis();
            var secondaryAxis = primaryAxis.OtherAxis();
            var primaryAxisIsReversed = AxisIsReversed(direction, primaryAxis);
            var primaryAxisGridStartLine = cellOccupancyMatrix.GetTrackCounts(primaryAxis).ImplicitStartLine();
            var primaryAxisGridEndLine = cellOccupancyMatrix.GetTrackCounts(primaryAxis).ImplicitEndLine();

            var secondaryAxisPlacement = MaybeMirrorSpan(
                placement.Get(secondaryAxis).ResolveDefiniteGridLines(),
                secondaryAxis,
                direction,
                explicitColCount);

            OriginZeroLine startingPosition;
            if (autoFlow.IsDense())
            {
                startingPosition = SearchStartLine(primaryAxisGridStartLine, primaryAxisGridEndLine, primaryAxisIsReversed);
            }
            else
            {
                OriginZeroLine? lookupResult;
                if (primaryAxisIsReversed)
                {
                    lookupResult = cellOccupancyMatrix.FirstOfType(
                        primaryAxis, secondaryAxisPlacement.Start, CellOccupancyState.AutoPlaced);
                }
                else
                {
                    lookupResult = cellOccupancyMatrix.LastOfType(
                        primaryAxis, secondaryAxisPlacement.Start, CellOccupancyState.AutoPlaced);
                }
                startingPosition = lookupResult ?? SearchStartLine(primaryAxisGridStartLine, primaryAxisGridEndLine, primaryAxisIsReversed);
            }

            var primaryAxisSpan = placement.Get(primaryAxis).IndefiniteSpan();

            var position = startingPosition;
            while (true)
            {
                var primaryAxisPlacement = ResolveIndefiniteGridSpan(position, primaryAxisSpan, primaryAxisIsReversed);

                var doesFit = cellOccupancyMatrix.LineAreaIsUnoccupied(
                    primaryAxis,
                    primaryAxisPlacement,
                    secondaryAxisPlacement);

                if (doesFit)
                    return (primaryAxisPlacement, secondaryAxisPlacement);
                else
                    position = AdvancePosition(position, primaryAxisIsReversed);
            }
        }

        /// <summary>
        /// 8.5. Grid Item Placement Algorithm
        /// Step 4. Position the remaining grid items.
        /// </summary>
        private static (Line<OriginZeroLine> primarySpan, Line<OriginZeroLine> secondarySpan)
            PlaceIndefinitelyPositionedItem(
                CellOccupancyMatrix cellOccupancyMatrix,
                InBothAbsAxis<Line<GenericGridPlacement<OriginZeroLine>>> placement,
                GridAutoFlow autoFlow,
                (OriginZeroLine primary, OriginZeroLine secondary) gridPosition,
                Direction direction,
                ushort explicitColCount)
        {
            var primaryAxis = autoFlow.PrimaryAxis();
            var secondaryAxis = primaryAxis.OtherAxis();
            var primaryAxisIsReversed = AxisIsReversed(direction, primaryAxis);
            var secondaryAxisIsReversed = AxisIsReversed(direction, secondaryAxis);

            var primaryPlacementStyle = placement.Get(primaryAxis);
            var secondaryPlacementStyle = placement.Get(secondaryAxis);

            var secondarySpan = secondaryPlacementStyle.IndefiniteSpan();
            bool hasDefinitePrimaryAxisPosition = primaryPlacementStyle.IsDefinite();
            var primaryAxisGridStartLine = cellOccupancyMatrix.GetTrackCounts(primaryAxis).ImplicitStartLine();
            var primaryAxisGridEndLine = cellOccupancyMatrix.GetTrackCounts(primaryAxis).ImplicitEndLine();
            var secondaryAxisGridStartLine = cellOccupancyMatrix.GetTrackCounts(secondaryAxis).ImplicitStartLine();
            var secondaryAxisGridEndLine = cellOccupancyMatrix.GetTrackCounts(secondaryAxis).ImplicitEndLine();
            var primaryStartPosition = SearchStartLine(primaryAxisGridStartLine, primaryAxisGridEndLine, primaryAxisIsReversed);
            var secondaryStartPosition = SearchStartLine(secondaryAxisGridStartLine, secondaryAxisGridEndLine, secondaryAxisIsReversed);

            var primaryIdx = gridPosition.primary;
            var secondaryIdx = gridPosition.secondary;

            if (hasDefinitePrimaryAxisPosition)
            {
                var primarySpan = MaybeMirrorSpan(
                    primaryPlacementStyle.ResolveDefiniteGridLines(),
                    primaryAxis,
                    direction,
                    explicitColCount);

                // Compute secondary axis starting position for search
                if (autoFlow.IsDense())
                {
                    // If auto-flow is dense then we always search from the first track
                    secondaryIdx = secondaryStartPosition;
                }
                else
                {
                    var shouldAdvanceSecondary = primaryAxisIsReversed
                        ? primarySpan.Start > primaryIdx
                        : primarySpan.Start < primaryIdx;
                    if (shouldAdvanceSecondary)
                        secondaryIdx = AdvancePosition(secondaryIdx, secondaryAxisIsReversed);
                }

                // Item has fixed primary axis position: so we simply increment the secondary axis position
                // until we find a space that the item fits in
                while (true)
                {
                    var secSpan = ResolveIndefiniteGridSpan(secondaryIdx, secondarySpan, secondaryAxisIsReversed);

                    // If area is occupied, increment the index and try again
                    if (!cellOccupancyMatrix.LineAreaIsUnoccupied(primaryAxis, primarySpan, secSpan))
                    {
                        secondaryIdx = AdvancePosition(secondaryIdx, secondaryAxisIsReversed);
                        continue;
                    }

                    // Once we find a free space, return that position
                    return (primarySpan, secSpan);
                }
            }
            else
            {
                var primarySpanVal = primaryPlacementStyle.IndefiniteSpan();

                // Item does not have any fixed axis, so we search along the primary axis until we hit the end of the already
                // existent tracks, and then we reset the primary axis back to zero and increment the secondary axis index.
                // We continue in this vein until we find a space that the item fits in.
                while (true)
                {
                    var priSpan = ResolveIndefiniteGridSpan(primaryIdx, primarySpanVal, primaryAxisIsReversed);
                    var secSpan = ResolveIndefiniteGridSpan(secondaryIdx, secondarySpan, secondaryAxisIsReversed);

                    // If the primary index is out of bounds, then increment the secondary index and reset the primary
                    // index back to the start of the grid
                    bool primaryOutOfBounds = primaryAxisIsReversed
                        ? priSpan.Start < primaryAxisGridStartLine
                        : priSpan.End > primaryAxisGridEndLine;
                    if (primaryOutOfBounds)
                    {
                        secondaryIdx = AdvancePosition(secondaryIdx, secondaryAxisIsReversed);
                        primaryIdx = primaryStartPosition;
                        continue;
                    }

                    // If area is occupied, increment the primary index and try again
                    if (!cellOccupancyMatrix.LineAreaIsUnoccupied(primaryAxis, priSpan, secSpan))
                    {
                        primaryIdx = AdvancePosition(primaryIdx, primaryAxisIsReversed);
                        continue;
                    }

                    // Once we find a free space that's in bounds, return that position
                    return (priSpan, secSpan);
                }
            }
        }

        /// <summary>
        /// Record the grid item in both CellOccupancyMatrix and the GridItems list
        /// once a definite placement has been determined
        /// </summary>
        private static void RecordGridPlacement(
            CellOccupancyMatrix cellOccupancyMatrix,
            ref ValueList<GridItem> items,
            NodeId node,
            int index,
            Style style,
            AlignItems parentAlignItems,
            AlignItems parentJustifyItems,
            AbsoluteAxis primaryAxis,
            Line<OriginZeroLine> primarySpan,
            Line<OriginZeroLine> secondarySpan,
            CellOccupancyState placementType)
        {
            // Mark area of grid as occupied
            cellOccupancyMatrix.MarkAreaAs(primaryAxis, primarySpan, secondarySpan, placementType);

            // Create grid item
            Line<OriginZeroLine> colSpan, rowSpan;
            switch (primaryAxis)
            {
                case AbsoluteAxis.Horizontal:
                    colSpan = primarySpan;
                    rowSpan = secondarySpan;
                    break;
                default:
                    colSpan = secondarySpan;
                    rowSpan = primarySpan;
                    break;
            }

            items.Add(GridItem.NewWithPlacementStyleAndOrder(
                node,
                colSpan,
                rowSpan,
                style,
                parentAlignItems,
                parentJustifyItems,
                (ushort)index));
        }
    }
}
