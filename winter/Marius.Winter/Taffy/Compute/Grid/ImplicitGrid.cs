// Ported from taffy/src/compute/grid/implicit_grid.rs
// This module is not required for spec compliance, but is used as a performance optimisation
// to reduce the number of allocations required when creating a grid.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// Functions to estimate the number of rows and columns in the implicit grid
    /// </summary>
    public static class ImplicitGridUtils
    {
        /// <summary>
        /// Estimate the number of rows and columns in the grid
        /// This is used as a performance optimisation to pre-size vectors and reduce allocations. It also forms a necessary step
        /// in the auto-placement
        ///   - The estimates for the explicit and negative implicit track counts are exact.
        ///   - However, the estimates for the positive explicit track count is a lower bound as auto-placement can affect this
        ///     in ways which are impossible to predict until the auto-placement algorithm is run.
        ///
        /// Note that this function internally mixes use of grid track numbers and grid line numbers
        /// </summary>
        public static (TrackCounts columnCounts, TrackCounts rowCounts) ComputeGridSizeEstimate(
            ushort explicitColCount,
            ushort explicitRowCount,
            Direction direction,
            IEnumerable<Style> childStylesIter)
        {
            // Iterate over children, producing an estimate of the min and max grid lines (in origin-zero coordinates)
            // along with the span of each item
            var (colMin, colMax, colMaxSpan, rowMin, rowMax, rowMaxSpan) =
                GetKnownChildPositions(childStylesIter, explicitColCount, explicitRowCount, direction);

            // Compute *track* count estimates for each axis from:
            //   - The explicit track counts
            //   - The origin-zero coordinate min and max grid line variables
            ushort negativeImplicitInlineTracks = colMin.ImpliedNegativeImplicitTracks();
            ushort explicitInlineTracks = explicitColCount;
            ushort positiveImplicitInlineTracks = colMax.ImpliedPositiveImplicitTracks(explicitColCount);
            ushort negativeImplicitBlockTracks = rowMin.ImpliedNegativeImplicitTracks();
            ushort explicitBlockTracks = explicitRowCount;
            ushort positiveImplicitBlockTracks = rowMax.ImpliedPositiveImplicitTracks(explicitRowCount);

            // In each axis, adjust positive track estimate if any items have a span that does not fit within
            // the total number of tracks in the estimate
            ushort totInlineTracks = (ushort)(negativeImplicitInlineTracks + explicitInlineTracks + positiveImplicitInlineTracks);
            if (totInlineTracks < colMaxSpan)
            {
                positiveImplicitInlineTracks = (ushort)(colMaxSpan - explicitInlineTracks - negativeImplicitInlineTracks);
            }

            ushort totBlockTracks = (ushort)(negativeImplicitBlockTracks + explicitBlockTracks + positiveImplicitBlockTracks);
            if (totBlockTracks < rowMaxSpan)
            {
                positiveImplicitBlockTracks = (ushort)(rowMaxSpan - explicitBlockTracks - negativeImplicitBlockTracks);
            }

            var columnCounts = TrackCounts.FromRaw(negativeImplicitInlineTracks, explicitInlineTracks, positiveImplicitInlineTracks);
            var rowCounts = TrackCounts.FromRaw(negativeImplicitBlockTracks, explicitBlockTracks, positiveImplicitBlockTracks);

            return (columnCounts, rowCounts);
        }

        /// <summary>
        /// Iterate over children, producing an estimate of the min and max grid *lines* along with the span of each item
        ///
        /// Min and max grid lines are returned in origin-zero coordinates
        /// The span is measured in tracks spanned
        /// </summary>
        private static (OriginZeroLine colMin, OriginZeroLine colMax, ushort colMaxSpan,
            OriginZeroLine rowMin, OriginZeroLine rowMax, ushort rowMaxSpan)
            GetKnownChildPositions(
                IEnumerable<Style> childrenIter,
                ushort explicitColCount,
                ushort explicitRowCount,
                Direction direction)
        {
            var colMin = new OriginZeroLine(0);
            var colMax = new OriginZeroLine(0);
            ushort colMaxSpan = 0;
            var rowMin = new OriginZeroLine(0);
            var rowMax = new OriginZeroLine(0);
            ushort rowMaxSpan = 0;

            foreach (var childStyle in childrenIter)
            {
                // Note: that the children reference the lines in between (and around) the tracks not tracks themselves,
                // and thus we must subtract 1 to get an accurate estimate of the number of tracks
                var (childColMin, childColMax, childColSpan) =
                    ChildMinLineMaxLineSpan(childStyle.GetGridColumn(), explicitColCount);
                var (childRowMin, childRowMax, childRowSpan) =
                    ChildMinLineMaxLineSpan(childStyle.GetGridRow(), explicitRowCount);

                // Placement mirrors horizontal spans in RTL, so mirror known column line bounds here
                // to keep implicit-grid pre-sizing consistent with actual placement.
                if (direction.IsRtl() && (childColMin != new OriginZeroLine(0) || childColMax != new OriginZeroLine(0)))
                {
                    var explicitColEndLine = (short)explicitColCount;
                    var mirroredMin = new OriginZeroLine((short)(explicitColEndLine - childColMax.Value));
                    var mirroredMax = new OriginZeroLine((short)(explicitColEndLine - childColMin.Value));
                    childColMin = mirroredMin;
                    childColMax = mirroredMax;
                }

                colMin = (childColMin < colMin ? childColMin : colMin);
                colMax = (childColMax > colMax ? childColMax : colMax);
                colMaxSpan = Math.Max(colMaxSpan, childColSpan);
                rowMin = (childRowMin < rowMin ? childRowMin : rowMin);
                rowMax = (childRowMax > rowMax ? childRowMax : rowMax);
                rowMaxSpan = Math.Max(rowMaxSpan, childRowSpan);
            }

            return (colMin, colMax, colMaxSpan, rowMin, rowMax, rowMaxSpan);
        }

        /// <summary>
        /// Helper function for ComputeGridSizeEstimate
        /// Produces a conservative estimate of the greatest and smallest grid lines used by a single grid item
        ///
        /// Values are returned in origin-zero coordinates
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (OriginZeroLine min, OriginZeroLine max, ushort span) ChildMinLineMaxLineSpan(
            Line<GridPlacement> line,
            ushort explicitTrackCount)
        {
            // 8.3.1. Grid Placement Conflict Handling
            // A. If the placement for a grid item contains two lines, and the start line is further end-ward than the end line, swap the two lines.
            // B. If the start line is equal to the end line, remove the end line.
            // C. If the placement contains two spans, remove the one contributed by the end grid-placement property.
            // D. If the placement contains only a span for a named line, replace it with a span of 1.

            // Convert line into origin-zero coordinates before attempting to analyze
            // We ignore named lines here as they are accounted for separately
            var ozLine = line.IntoOriginZeroIgnoringNamed(explicitTrackCount);

            OriginZeroLine min;
            if (ozLine.Start.IsLine && ozLine.End.IsLine)
            {
                var track1 = ozLine.Start.LineValue;
                var track2 = ozLine.End.LineValue;
                min = track1.Equals(track2) ? track1 : (track1 < track2 ? track1 : track2);
            }
            else if (ozLine.Start.IsLine && (ozLine.End.IsAuto || ozLine.End.IsSpan))
            {
                min = ozLine.Start.LineValue;
            }
            else if (ozLine.End.IsLine && ozLine.Start.IsAuto)
            {
                min = ozLine.End.LineValue;
            }
            else if (ozLine.End.IsLine && ozLine.Start.IsSpan)
            {
                min = ozLine.End.LineValue - ozLine.Start.SpanValue;
            }
            else
            {
                // Only spans or autos
                // We ignore spans here by returning 0 which never affects the estimate as these are accounted for separately
                min = new OriginZeroLine(0);
            }

            OriginZeroLine max;
            if (ozLine.Start.IsLine && ozLine.End.IsLine)
            {
                var track1 = ozLine.Start.LineValue;
                var track2 = ozLine.End.LineValue;
                max = track1.Equals(track2) ? track1 + 1 : (track1 > track2 ? track1 : track2);
            }
            else if (ozLine.Start.IsLine && ozLine.End.IsAuto)
            {
                max = ozLine.Start.LineValue + 1;
            }
            else if (ozLine.Start.IsLine && ozLine.End.IsSpan)
            {
                max = ozLine.Start.LineValue + ozLine.End.SpanValue;
            }
            else if (ozLine.End.IsLine && (ozLine.Start.IsAuto || ozLine.Start.IsSpan))
            {
                max = ozLine.End.LineValue;
            }
            else
            {
                // Only spans or autos
                max = new OriginZeroLine(0);
            }

            // Calculate span only for indefinitely placed items as we don't need it for other items (whose required space will
            // be taken into account by min and max)
            ushort span;
            if ((ozLine.Start.IsAuto || ozLine.Start.IsSpan)
                && (ozLine.End.IsAuto || ozLine.End.IsSpan))
            {
                span = ozLine.IndefiniteSpan();
            }
            else
            {
                span = 1;
            }

            return (min, max, span);
        }
    }
}
