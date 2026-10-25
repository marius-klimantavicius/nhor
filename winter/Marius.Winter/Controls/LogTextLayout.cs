using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ThorVG;

namespace Marius.Winter;

/// <summary>
/// Caret advances for an unwrapped log line. ThorVG's SFNT renderer places
/// scalars with pair kerning; measuring short pairs preserves that placement
/// without repeatedly constructing and outlining every prefix of a long line.
/// This cache owns only managed data, never scene paints or font leases.
/// </summary>
internal sealed class LogTextLayout
{
    private readonly int[] _offsets;
    private readonly float[] _advances;
    private readonly float[] _origins;
    internal float Width => _advances[^1];

    internal LogTextLayout(string text, LogFontMeasure measure)
    {
        _offsets = StringInfo.ParseCombiningCharacters(text);
        Array.Resize(ref _offsets, _offsets.Length + 1);
        _offsets[^1] = text.Length;
        _advances = new float[_offsets.Length];
        _origins = new float[_offsets.Length];
        double advance = 0;
        int previous = -1, cluster = 0;
        for (int offset = 0; offset < text.Length;)
        {
            Rune.DecodeFromUtf16(text.AsSpan(offset), out var rune, out int consumed);
            if (consumed == 0) consumed = 1;
            float width = measure.Advance(previous, rune.Value);
            if (offset == _offsets[cluster])
                _origins[cluster] = (float)(advance + width - measure.Advance(-1, rune.Value));
            advance += width;
            previous = rune.Value;
            offset += consumed;
            if (offset == _offsets[cluster + 1]) _advances[++cluster] = (float)advance;
        }
        _origins[^1] = (float)advance;
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

    internal void VisibleRange(float left, float right, out int start, out int end, out float origin)
    {
        // Include neighboring graphemes to retain overhangs and pair context at
        // the clip edges. Source/selection/copy always use the complete string.
        int first = Math.Max(0, BoundaryAt(left) - 1);
        int last = Math.Min(_offsets.Length - 1, BoundaryAt(right) + 2);
        start = _offsets[first]; end = _offsets[last]; origin = _origins[first];
    }

    private int BoundaryAt(float x)
    {
        int index = Array.BinarySearch(_advances, x);
        return index >= 0 ? index : Math.Max(0, ~index - 1);
    }
}

/// <summary>One short-lived measurement paint and bounded pair reuse per rebuild.</summary>
internal sealed class LogFontMeasure : IDisposable
{
    private readonly Text _paint = Text.Gen();
    private readonly Dictionary<(int, int), float> _advances = new();
    internal float BaselineY { get; }

    internal LogFontMeasure(string font, float size)
    {
        _paint.SetFont(font); _paint.SetFontSize(size);
        _paint.SetText("Mg");
        _paint.Bounds(out _, out float y, out _, out _);
        BaselineY = y;
    }

    internal float Advance(int previous, int scalar)
    {
        var key = (previous, scalar);
        if (_advances.TryGetValue(key, out float advance)) return advance;
        string current = new Rune(scalar).ToString();
        string text = previous < 0 ? current : new Rune(previous) + current;
        _paint.SetText(text);
        _paint.GetTextSize(out float width, out _);
        advance = previous < 0 ? width : width - Advance(-1, previous);
        _advances[key] = advance;
        return advance;
    }

    public void Dispose() => _paint.Unref();
}
