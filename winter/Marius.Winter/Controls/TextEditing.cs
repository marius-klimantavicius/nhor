using System;
using System.Globalization;
using ThorVG;

namespace Marius.Winter;

/// <summary>
/// Extended grapheme boundaries, expressed as UTF-16 offsets into the original
/// string. Editing uses graphemes; rich-run lengths continue to use UTF-16.
/// </summary>
internal sealed class TextEditingBoundaries
{
    private string _text = "";
    private int[] _offsets = [0];

    public ReadOnlySpan<int> Offsets => _offsets;

    public void SetText(string text)
    {
        if (ReferenceEquals(_text, text)) return;
        _text = text;
        var starts = StringInfo.ParseCombiningCharacters(text);
        Array.Resize(ref starts, starts.Length + 1);
        starts[^1] = text.Length;
        _offsets = starts;
    }

    public int Index(int offset)
    {
        int index = Array.BinarySearch(_offsets, Math.Clamp(offset, 0, _text.Length));
        return index >= 0 ? index : ~index - 1;
    }

    public int Floor(int offset) => _offsets[Index(offset)];

    public int Ceiling(int offset)
    {
        offset = Math.Clamp(offset, 0, _text.Length);
        int index = Index(offset);
        return _offsets[index] == offset ? offset : _offsets[index + 1];
    }

    public int Previous(int offset)
    {
        int index = Index(offset);
        return _offsets[Math.Max(0, _offsets[index] == offset ? index - 1 : index)];
    }

    public int Next(int offset) => _offsets[Math.Min(Index(offset) + 1, _offsets.Length - 1)];

    public int WordLeft(int offset, bool stopAtNewline = false)
    {
        int index = Math.Max(0, Index(offset) - 1);
        if (stopAtNewline)
            while (index > 0 && IsNewline(index)) index--;
        while (index > 0 && char.IsWhiteSpace(_text, _offsets[index]) && (!stopAtNewline || !IsNewline(index))) index--;
        while (index > 0 && !char.IsWhiteSpace(_text, _offsets[index - 1])) index--;
        return _offsets[index];
    }

    public int WordRight(int offset, bool stopAtNewline = false)
    {
        int index = Index(offset);
        int last = _offsets.Length - 1;
        if (stopAtNewline && index < last && IsNewline(index)) return _offsets[index + 1];
        while (index < last && !char.IsWhiteSpace(_text, _offsets[index])) index++;
        while (index < last && char.IsWhiteSpace(_text, _offsets[index]) && (!stopAtNewline || !IsNewline(index))) index++;
        return _offsets[index];
    }

    private bool IsNewline(int index) => _text[_offsets[index]] is '\r' or '\n';

    public int InAdjacentLine(int offset, int lineStart, int targetStart, int targetLength)
    {
        int column = Index(offset) - Index(lineStart);
        return _offsets[Math.Min(Index(targetStart) + column, Index(targetStart + targetLength))];
    }

    // Formatting ranges can separate styled scalars inside a grapheme, but must
    // never hand the renderer a substring containing half a surrogate pair.
    public static int ScalarFloor(string text, int offset) =>
        offset > 0 && offset < text.Length && char.IsLowSurrogate(text[offset]) && char.IsHighSurrogate(text[offset - 1])
            ? offset - 1 : offset;

    public static int ScalarCeiling(string text, int offset) =>
        offset > 0 && offset < text.Length && char.IsLowSurrogate(text[offset]) && char.IsHighSurrogate(text[offset - 1])
            ? offset + 1 : offset;

    public static void SplitLines(string text, out string[] lines, out int[] offsets)
    {
        lines = text.Split('\n');
        offsets = new int[lines.Length];
        int offset = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            offsets[i] = offset;
            offset += lines[i].Length + 1;
            // CRLF is one editing grapheme. The carriage return belongs to the
            // newline, rather than the displayed content or End position.
            if (i < lines.Length - 1 && lines[i].EndsWith('\r'))
                lines[i] = lines[i][..^1];
        }
    }
}

/// <summary>Renderer advances at legal carets; no scene-owned paint is mutated.</summary>
internal sealed class TextAdvanceCache
{
    private string? _text;
    private string? _font;
    private float _fontSize;
    private int _layoutVersion;
    private int[] _offsets = [0];
    private float[] _advances = [0];

    public float BoundsY { get; private set; }
    public float BoundsHeight { get; private set; }

    public void Ensure(string text, TextEditingBoundaries boundaries, string font, float fontSize, int layoutVersion)
    {
        if (ReferenceEquals(text, _text) && font == _font && fontSize == _fontSize && layoutVersion == _layoutVersion)
            return;

        _offsets = boundaries.Offsets.ToArray();
        _advances = new float[_offsets.Length];
        // This temporary paint is never added to a scene/canvas. Release only
        // its own reference after measuring, preserving renderer ownership.
        var measure = ThorVG.Text.Gen();
        measure.Ref();
        try
        {
            measure.SetFont(font);
            measure.SetFontSize(fontSize);
            for (int i = 1; i < _offsets.Length; i++)
            {
                measure.SetText(text[.._offsets[i]]);
                if (measure.GetTextSize(out float width, out _) != Result.Success)
                {
                    measure.Bounds(out float x, out _, out float w, out _);
                    width = x + w;
                }
                _advances[i] = width;
            }
            measure.SetText(text.Length == 0 ? "M" : text);
            measure.Bounds(out _, out float y, out _, out float height);
            BoundsY = y;
            BoundsHeight = height > 0 ? height : fontSize;
        }
        finally { measure.Unref(); }

        _text = text;
        _font = font;
        _fontSize = fontSize;
        _layoutVersion = layoutVersion;
    }

    public float Advance(int offset)
    {
        int index = Array.BinarySearch(_offsets, offset);
        return _advances[index >= 0 ? index : Math.Max(0, ~index - 1)];
    }

    public int HitTest(float x)
    {
        int lo = 0, hi = _advances.Length - 1;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (x < (_advances[mid] + _advances[mid + 1]) / 2f) hi = mid;
            else lo = mid + 1;
        }
        return _offsets[lo];
    }

    // First boundary whose advance exceeds x, or the last boundary.
    public int FirstAfter(float x)
    {
        int lo = 0, hi = _advances.Length - 1;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (_advances[mid] > x) hi = mid;
            else lo = mid + 1;
        }
        return lo;
    }

    public int OffsetAt(int index) => _offsets[index];
}
