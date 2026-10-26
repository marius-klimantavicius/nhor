using System;
using System.Collections.Generic;
using System.Globalization;

namespace Marius.Winter;

/// <summary>Cached caret positions in exactly the string sent to the renderer.</summary>
internal sealed class MarkdownGlyphs
{
    private readonly int[] _offsets;
    private readonly float[] _advances;
    internal float Width => _advances[^1];
    internal MarkdownGlyphs(string text, Func<int, float> advance)
    {
        _offsets = StringInfo.ParseCombiningCharacters(text);
        Array.Resize(ref _offsets, _offsets.Length + 1); _offsets[^1] = text.Length;
        _advances = new float[_offsets.Length];
        for (int i = 1; i < _offsets.Length; i++) _advances[i] = advance(_offsets[i]);
    }
    internal float Advance(int offset)
    {
        int index = Array.BinarySearch(_offsets, offset);
        return _advances[index >= 0 ? index : Math.Max(0, ~index - 1)];
    }
    internal int HitTest(float x)
    {
        int lo = 0, hi = _advances.Length - 1;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (x < (_advances[mid] + _advances[mid + 1]) / 2) hi = mid;
            else lo = mid + 1;
        }
        return _offsets[lo];
    }
}

internal sealed record MarkdownRange(RectF Bounds, int Start, int End, MarkdownGlyphs Glyphs, int[] Offsets, int Line)
{
    internal int HitTest(float x) => Offsets[Glyphs.HitTest(x - Bounds.X)];
    internal float X(int position)
    {
        if (position <= Start) return Bounds.X;
        if (position >= End) return Bounds.Right;
        int index = Array.BinarySearch(Offsets, position);
        if (index < 0) index = ~index;
        // Expanded tab spaces map to the tab's starting offset. Its ending
        // caret is after the complete visual expansion.
        while (index > 0 && Offsets[index - 1] == position) index--;
        return Bounds.X + Glyphs.Advance(index);
    }
}

internal sealed class MarkdownSelectionLayout
{
    internal readonly List<MarkdownRange> Ranges = new();
    internal int HitTest(float x, float y) => HitTest(x, y, out _);
    internal int HitTest(float x, float y, out bool upstream)
    {
        MarkdownRange? nearest = null;
        float distance = float.MaxValue;
        foreach (var range in Ranges)
        {
            float dy = y < range.Bounds.Y ? range.Bounds.Y - y : y > range.Bounds.Bottom ? y - range.Bounds.Bottom : 0;
            float dx = x < range.Bounds.X ? range.Bounds.X - x : x > range.Bounds.Right ? x - range.Bounds.Right : 0;
            // Reading order breaks equal-distance ties; vertical distance takes
            // precedence so wide code and parallel table cells remain reachable.
            float candidate = dy * 100000 + dx;
            if (candidate < distance) { distance = candidate; nearest = range; }
        }
        int position = nearest?.HitTest(x) ?? 0;
        upstream = nearest != null && position == nearest.End;
        return position;
    }
    internal MarkdownRange? At(int position, bool upstream = false)
    {
        MarkdownRange? previous = null;
        foreach (var range in Ranges)
        {
            if (position < range.Start) return previous ?? range;
            if (position < range.End || upstream && position == range.End) return range;
            previous = range;
        }
        return previous;
    }
    internal (int Start, int End) LineBounds(MarkdownRange current)
    {
        int start = current.Start, end = current.End;
        foreach (var range in Ranges)
            if (range.Line == current.Line) { start = Math.Min(start, range.Start); end = Math.Max(end, range.End); }
        return (start, end);
    }
    internal int Adjacent(MarkdownRange current, float x, bool down, out bool upstream)
    {
        MarkdownRange? target = null;
        float distance = float.MaxValue;
        foreach (var range in Ranges)
        {
            if (down ? range.Bounds.Y <= current.Bounds.Y + .1f : range.Bounds.Y >= current.Bounds.Y - .1f) continue;
            float dy = Math.Abs(range.Bounds.Y - current.Bounds.Y);
            float dx = x < range.Bounds.X ? range.Bounds.X - x : x > range.Bounds.Right ? x - range.Bounds.Right : 0;
            float candidate = dy * 100000 + dx;
            if (candidate < distance) { distance = candidate; target = range; }
        }
        int position = target?.HitTest(x) ?? (down ? Ranges[^1].End : Ranges[0].Start);
        upstream = target != null && position == target.End;
        return position;
    }
    internal Dictionary<RectF, RectF> Highlight(int start, int end)
    {
        var result = new Dictionary<RectF, RectF>();
        foreach (var range in Ranges)
        {
            if (range.End <= start || range.Start >= end) continue;
            float left = range.X(Math.Max(start, range.Start));
            float right = range.X(Math.Min(end, range.End));
            if (right > left) result[range.Bounds] = new(left, range.Bounds.Y, right - left, range.Bounds.H);
        }
        return result;
    }
}
