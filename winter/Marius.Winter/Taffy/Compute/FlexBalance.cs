// Ported from the balance module in src/compute/flexbox.rs at 802e497b.
using System;
using System.Collections.Generic;

namespace Marius.Winter.Taffy;

public static class FlexBalance
{
    public static int[] BalancedLineItemCounts(ReadOnlySpan<float> items, float lineLimit, float gap, int minLineCount)
    {
        int n = items.Length;
        if (n == 0) throw new ArgumentException("Balancing requires at least one item", nameof(items));
        double usedGap = Math.Clamp((double)gap, 0, float.MaxValue);
        double limit = lineLimit;
        var sums = new double[n]; var zero = new bool[n];
        double sum = 0;
        for (int i = 0; i < n; i++) { double size = Math.Clamp((double)items[i], 0, float.MaxValue); sum += size + usedGap; sums[i] = sum; zero[i] = size == 0; }
        double Size(int start, int end) => sums[end] - (start == 0 ? 0 : sums[start - 1]) - usedGap;
        double Cost(int start, int end) { double size = Size(start, end); return end > start && size > limit ? double.PositiveInfinity : size * size; }
        var suffix = new int[n + 1]; int lineEnd = n;
        for (int start = n - 1; start >= 0; start--)
        {
            while (lineEnd > start + 1 && Size(start, lineEnd - 1) > limit) lineEnd--;
            suffix[start] = 1 + suffix[lineEnd];
        }
        int lineCount = Math.Max(suffix[0], Math.Clamp(minLineCount, 1, n));
        var counts = new int[lineCount];
        if (lineCount == n) { Array.Fill(counts, 1); return counts; }
        var fitEnds = new int[n]; int fit = 0;
        for (int start = 0; start < n; start++)
        {
            if (fit < start) fit = start;
            while (fit + 1 < n && Size(start, fit + 1) <= limit) fit++;
            fitEnds[start] = fit;
        }
        var nonzero = new List<int>(n - 1);
        for (int end = 0; end < n - 1; end++) if (!zero[end + 1]) nonzero.Add(end);
        var previous = new double[n]; var current = new double[n];
        for (int i = 0; i < n; i++) previous[i] = Cost(i, n - 1);
        Array.Fill(current, double.PositiveInfinity);
        var options = new int[checked((lineCount - 1) * n)];
        for (int lines = 2; lines <= lineCount; lines++)
        {
            int maxEnd = n - lines, firstStart = 0;
            while (firstStart < maxEnd && suffix[firstStart] > lines) current[firstStart++] = double.PositiveInfinity;
            int hi = 0; while (hi < nonzero.Count && nonzero[hi] <= maxEnd) hi++;
            int rowOffset = (lines - 2) * n;
            void Fill(int loStart, int hiStart, int loColumn, int hiColumn)
            {
                if (loStart > hiStart) return;
                int start = loStart + (hiStart - loStart) / 2;
                int firstColumn = loColumn;
                while (firstColumn < hiColumn && nonzero[firstColumn] < start) firstColumn++;
                double minCost = double.PositiveInfinity; int? minColumn = null;
                for (int col = firstColumn; col < hiColumn; col++)
                {
                    int end = nonzero[col]; double lineCost = Cost(start, end);
                    if (double.IsPositiveInfinity(lineCost)) break;
                    double cost = lineCost + previous[end + 1];
                    if (cost <= minCost) { minCost = cost; minColumn = col; }
                }
                int? minEnd = minColumn.HasValue ? nonzero[minColumn.Value] : null;
                int zeroEnd = Math.Min(fitEnds[start], maxEnd);
                if (zero[zeroEnd + 1])
                {
                    double cost = Cost(start, zeroEnd) + previous[zeroEnd + 1];
                    if (cost < minCost || (cost == minCost && (!minEnd.HasValue || minEnd.Value < zeroEnd))) { minCost = cost; minEnd = zeroEnd; }
                }
                current[start] = minCost; options[rowOffset + start] = minEnd ?? start;
                if (start > loStart) Fill(loStart, start - 1, loColumn, minColumn.HasValue ? minColumn.Value + 1 : hiColumn);
                if (start < hiStart) Fill(start + 1, hiStart, minColumn ?? firstColumn, hiColumn);
            }
            Fill(firstStart, maxEnd, 0, hi);
            (previous, current) = (current, previous);
        }
        int index = 0, position = 0;
        for (int lines = lineCount; lines >= 2; lines--)
        {
            int end = options[(lines - 2) * n + position]; counts[index++] = end - position + 1; position = end + 1;
        }
        counts[index] = n - position;
        return counts;
    }
}
