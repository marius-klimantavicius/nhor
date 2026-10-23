using System;
using System.Collections.Generic;
using Marius.Winter.Taffy;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

public class GridOccupancyTests
{
    private static Line<OriginZeroLine> Span(int start, int end) => new(new OriginZeroLine((short)start), new OriginZeroLine((short)end));

    [Fact]
    public void DebugRepresentationIncludesStatesAndBoundsLargeGrids()
    {
        var matrix = CellOccupancyMatrix.WithTrackCounts(new TrackCounts(0, 3, 0), new TrackCounts(0, 1, 0));
        matrix.MarkAreaAs(AbsoluteAxis.Horizontal, Span(0, 1), Span(0, 1), CellOccupancyState.DefinitelyPlaced);
        matrix.MarkAreaAs(AbsoluteAxis.Horizontal, Span(1, 2), Span(0, 1), CellOccupancyState.AutoPlaced);
        Assert.Equal("Rows: neg_implicit=0 explicit=1 pos_implicit=0\nCols: neg_implicit=0 explicit=3 pos_implicit=0\nState:\nDA_\n", matrix.ToString());
        var large = CellOccupancyMatrix.WithTrackCounts(new TrackCounts(0, 101, 0), default);
        Assert.EndsWith("State: (not printed: more than 100 tracks)\n", large.ToString());
    }

    [Fact]
    public void SparseStorageSupportsTheMaximumExplicitGridWithoutADenseAllocation()
    {
        _ = CellOccupancyMatrix.WithTrackCounts(new TrackCounts(0, 1, 0), new TrackCounts(0, 1, 0));
        long before = GC.GetAllocatedBytesForCurrentThread();
        var matrix = CellOccupancyMatrix.WithTrackCounts(new TrackCounts(0, 10000, 0), new TrackCounts(0, 10000, 0));
        matrix.MarkAreaAs(AbsoluteAxis.Horizontal, Span(0, 10000), Span(0, 10000), CellOccupancyState.DefinitelyPlaced);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(bytes < 16 * 1024 * 1024, $"Allocated {bytes} bytes for 10,000 × 10,000 tracks");
        Assert.Equal(new OriginZeroLine(10000), matrix.LineAreaCollisionJump(AbsoluteAxis.Horizontal, Span(500, 501), Span(0, 10000)));
        Assert.True(matrix.RowIsOccupied(9999));
        Assert.True(matrix.ColumnIsOccupied(9999));
    }

    [Fact]
    public void PaintingOverlapsSplitsAndMergesIntervalsWithoutLosingStates()
    {
        var matrix = CellOccupancyMatrix.WithTrackCounts(default, default);
        matrix.MarkAreaAs(AbsoluteAxis.Horizontal, Span(0, 10), Span(0, 1), CellOccupancyState.DefinitelyPlaced);
        matrix.MarkAreaAs(AbsoluteAxis.Horizontal, Span(3, 7), Span(0, 1), CellOccupancyState.AutoPlaced);
        Assert.Equal(new OriginZeroLine(0), matrix.FirstOfType(AbsoluteAxis.Horizontal, new OriginZeroLine(0), CellOccupancyState.DefinitelyPlaced));
        Assert.Equal(new OriginZeroLine(9), matrix.LastOfType(AbsoluteAxis.Horizontal, new OriginZeroLine(0), CellOccupancyState.DefinitelyPlaced));
        Assert.Equal(new OriginZeroLine(3), matrix.FirstOfType(AbsoluteAxis.Horizontal, new OriginZeroLine(0), CellOccupancyState.AutoPlaced));
        Assert.Equal(new OriginZeroLine(6), matrix.LastOfType(AbsoluteAxis.Horizontal, new OriginZeroLine(0), CellOccupancyState.AutoPlaced));
        Assert.Equal(new OriginZeroLine(7), matrix.LineAreaCollisionJump(AbsoluteAxis.Horizontal, Span(4, 5), Span(0, 1)));
        Assert.Equal(new OriginZeroLine(10), matrix.LineAreaCollisionJump(AbsoluteAxis.Horizontal, Span(6, 9), Span(0, 1)));
        matrix.MarkAreaAs(AbsoluteAxis.Horizontal, Span(7, 12), Span(0, 1), CellOccupancyState.AutoPlaced);
        Assert.Equal(new OriginZeroLine(12), matrix.LineAreaCollisionJump(AbsoluteAxis.Horizontal, Span(4, 5), Span(0, 1)));
        Assert.Equal(new OriginZeroLine(2), matrix.LastOfType(AbsoluteAxis.Horizontal, new OriginZeroLine(0), CellOccupancyState.DefinitelyPlaced));
    }

    [Fact]
    public void SparseCursorOnlyAdvancesForItemsStartingInTheSecondaryTrack()
    {
        var matrix = CellOccupancyMatrix.WithTrackCounts(default, default);
        matrix.MarkAreaAs(AbsoluteAxis.Horizontal, Span(1, 4), Span(-2, 1), CellOccupancyState.AutoPlaced);
        Assert.Equal(new OriginZeroLine(4), matrix.AutoPlacementCursor(AbsoluteAxis.Horizontal, new OriginZeroLine(-2)));
        Assert.Null(matrix.AutoPlacementCursor(AbsoluteAxis.Horizontal, new OriginZeroLine(-1)));
        matrix.MarkAreaAs(AbsoluteAxis.Horizontal, Span(-3, 0), Span(-2, -1), CellOccupancyState.AutoPlaced);
        Assert.Equal(new OriginZeroLine(4), matrix.AutoPlacementCursor(AbsoluteAxis.Horizontal, new OriginZeroLine(-2)));
        matrix.MarkAreaAs(AbsoluteAxis.Horizontal, Span(5, 8), Span(-2, -1), CellOccupancyState.DefinitelyPlaced);
        Assert.Equal(new OriginZeroLine(4), matrix.AutoPlacementCursor(AbsoluteAxis.Horizontal, new OriginZeroLine(-2)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SparseOccupancyMatchesADenseOracleAfterNegativeAndPositiveExpansion(int seed)
    {
        var random = new Random(seed);
        var matrix = CellOccupancyMatrix.WithTrackCounts(new TrackCounts(0, 2, 0), new TrackCounts(0, 3, 0));
        var cells = new Dictionary<(int x, int y), CellOccupancyState>();
        for (int step = 0; step < 100; step++)
        {
            int x = random.Next(-15, 15), y = random.Next(-15, 15);
            int width = random.Next(1, 6), height = random.Next(1, 6);
            var state = step % 2 == 0 ? CellOccupancyState.DefinitelyPlaced : CellOccupancyState.AutoPlaced;
            var axis = step % 2 == 0 ? AbsoluteAxis.Horizontal : AbsoluteAxis.Vertical;
            matrix.MarkAreaAs(axis, axis == AbsoluteAxis.Horizontal ? Span(x, x + width) : Span(y, y + height),
                axis == AbsoluteAxis.Horizontal ? Span(y, y + height) : Span(x, x + width), state);
            for (int xx = x; xx < x + width; xx++)
                for (int yy = y; yy < y + height; yy++) cells[(xx, yy)] = state;
            for (int query = 0; query < 40; query++)
            {
                int qx = random.Next(-20, 20), qy = random.Next(-20, 20);
                int qw = random.Next(1, 7), qh = random.Next(1, 7);
                bool empty = true;
                for (int xx = qx; xx < qx + qw; xx++)
                    for (int yy = qy; yy < qy + qh; yy++) empty &= !cells.ContainsKey((xx, yy));
                Assert.Equal(empty, matrix.LineAreaIsUnoccupied(AbsoluteAxis.Horizontal, Span(qx, qx + qw), Span(qy, qy + qh)));
                Assert.Equal(empty, matrix.LineAreaIsUnoccupied(AbsoluteAxis.Vertical, Span(qy, qy + qh), Span(qx, qx + qw)));
            }
        }
    }
}
