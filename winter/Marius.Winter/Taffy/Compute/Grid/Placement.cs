// Ported from taffy/src/compute/grid/placement.rs
// Implements placing items in the grid and resolving the implicit grid.
// https://www.w3.org/TR/css-grid-1/#placement

using System;
using System.Collections.Generic;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// Grid item placement algorithm
    /// </summary>
    public static class PlacementUtils
    {
        /// <summary>
        /// 8.5. Grid Item Placement Algorithm
        /// Place items into the grid, generating new rows/columns into the implicit grid as required
        ///
        /// [Specification](https://www.w3.org/TR/css-grid-2/#auto-placement-algo)
        /// </summary>
        public static void PlaceGridItems(
            CellOccupancyMatrix cellOccupancyMatrix,
            List<GridItem> items,
            Func<IEnumerable<(int index, NodeId node, Style style)>> childrenIter,
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
                    var (rowSpan, colSpan) = PlaceDefiniteGridItem(placement, primaryAxis);
                    RecordGridPlacement(
                        cellOccupancyMatrix,
                        items,
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
                        PlaceDefiniteSecondaryAxisItem(cellOccupancyMatrix, placement, gridAutoFlow);

                    RecordGridPlacement(
                        cellOccupancyMatrix,
                        items,
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
            var primaryNegTracks = (short)cellOccupancyMatrix.GetTrackCounts(primaryAxis).NegativeImplicit;
            var secondaryNegTracks = (short)cellOccupancyMatrix.GetTrackCounts(secondaryAxis).NegativeImplicit;
            var gridStartPosition = (new OriginZeroLine((short)(-primaryNegTracks)), new OriginZeroLine((short)(-secondaryNegTracks)));
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
                        gridPosition);

                    // Record item
                    RecordGridPlacement(
                        cellOccupancyMatrix,
                        items,
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
                    gridPosition = gridAutoFlow.IsDense()
                        ? gridStartPosition
                        : (primarySpan.End, secondarySpan.Start);
                }
            }
        }

        /// <summary>
        /// 8.5. Grid Item Placement Algorithm
        /// Place a single definitely placed item into the grid
        /// </summary>
        private static (Line<OriginZeroLine> primarySpan, Line<OriginZeroLine> secondarySpan) PlaceDefiniteGridItem(
            InBothAbsAxis<Line<GenericGridPlacement<OriginZeroLine>>> placement,
            AbsoluteAxis primaryAxis)
        {
            // Resolve spans to tracks
            var primarySpan = placement.Get(primaryAxis).ResolveDefiniteGridLines();
            var secondarySpan = placement.Get(primaryAxis.OtherAxis()).ResolveDefiniteGridLines();
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
                GridAutoFlow autoFlow)
        {
            var primaryAxis = autoFlow.PrimaryAxis();
            var secondaryAxis = primaryAxis.OtherAxis();

            var secondaryAxisPlacement = placement.Get(secondaryAxis).ResolveDefiniteGridLines();
            var primaryAxisGridStartLine = cellOccupancyMatrix.GetTrackCounts(primaryAxis).ImplicitStartLine();
            OriginZeroLine startingPosition;
            if (autoFlow.IsDense())
            {
                startingPosition = primaryAxisGridStartLine;
            }
            else
            {
                var lastLine = cellOccupancyMatrix.LastOfType(primaryAxis, secondaryAxisPlacement.Start, CellOccupancyState.AutoPlaced);
                startingPosition = lastLine ?? primaryAxisGridStartLine;
            }

            var position = startingPosition;
            while (true)
            {
                var primaryAxisPlacement = placement.Get(primaryAxis).ResolveIndefiniteGridTracks(position);

                var doesFit = cellOccupancyMatrix.LineAreaIsUnoccupied(
                    primaryAxis,
                    primaryAxisPlacement,
                    secondaryAxisPlacement);

                if (doesFit)
                    return (primaryAxisPlacement, secondaryAxisPlacement);
                else
                    position = position + 1;
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
                (OriginZeroLine primary, OriginZeroLine secondary) gridPosition)
        {
            var primaryAxis = autoFlow.PrimaryAxis();

            var primaryPlacementStyle = placement.Get(primaryAxis);
            var secondaryPlacementStyle = placement.Get(primaryAxis.OtherAxis());

            var secondarySpan = secondaryPlacementStyle.IndefiniteSpan();
            bool hasDefinitePrimaryAxisPosition = primaryPlacementStyle.IsDefinite();
            var primaryAxisGridStartLine = cellOccupancyMatrix.GetTrackCounts(primaryAxis).ImplicitStartLine();
            var primaryAxisGridEndLine = cellOccupancyMatrix.GetTrackCounts(primaryAxis).ImplicitEndLine();
            var secondaryAxisGridStartLine = cellOccupancyMatrix.GetTrackCounts(primaryAxis.OtherAxis()).ImplicitStartLine();

            var primaryIdx = gridPosition.primary;
            var secondaryIdx = gridPosition.secondary;

            if (hasDefinitePrimaryAxisPosition)
            {
                var primarySpan = primaryPlacementStyle.ResolveDefiniteGridLines();

                // Compute secondary axis starting position for search
                if (autoFlow.IsDense())
                {
                    // If auto-flow is dense then we always search from the first track
                    secondaryIdx = secondaryAxisGridStartLine;
                }
                else
                {
                    if (primarySpan.Start < primaryIdx)
                        secondaryIdx = secondaryIdx + 1;
                }

                // Item has fixed primary axis position: so we simply increment the secondary axis position
                // until we find a space that the item fits in
                while (true)
                {
                    var secSpan = new Line<OriginZeroLine> { Start = secondaryIdx, End = secondaryIdx + secondarySpan };

                    // If area is occupied, increment the index and try again
                    if (!cellOccupancyMatrix.LineAreaIsUnoccupied(primaryAxis, primarySpan, secSpan))
                    {
                        secondaryIdx = secondaryIdx + 1;
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
                    var priSpan = new Line<OriginZeroLine> { Start = primaryIdx, End = primaryIdx + primarySpanVal };
                    var secSpan = new Line<OriginZeroLine> { Start = secondaryIdx, End = secondaryIdx + secondarySpan };

                    // If the primary index is out of bounds, then increment the secondary index and reset the primary
                    // index back to the start of the grid
                    bool primaryOutOfBounds = priSpan.End > primaryAxisGridEndLine;
                    if (primaryOutOfBounds)
                    {
                        secondaryIdx = secondaryIdx + 1;
                        primaryIdx = primaryAxisGridStartLine;
                        continue;
                    }

                    // If area is occupied, increment the primary index and try again
                    if (!cellOccupancyMatrix.LineAreaIsUnoccupied(primaryAxis, priSpan, secSpan))
                    {
                        primaryIdx = primaryIdx + 1;
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
            List<GridItem> items,
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
