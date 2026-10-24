using System;
using System.Globalization;

namespace Marius.Winter;

/// <summary>A thread-safe, bounded text tail. Notifications run on the producer's thread.</summary>
public sealed class LogBuffer
{
    private readonly object _gate = new();
    private string _text = "";
    private long _version, _start, _reset;

    public LogBuffer(int maxLines = 1000, int maxCharacters = 100_000)
    {
        if (maxLines < 1) throw new ArgumentOutOfRangeException(nameof(maxLines));
        if (maxCharacters < 1) throw new ArgumentOutOfRangeException(nameof(maxCharacters));
        MaxLines = maxLines;
        MaxCharacters = maxCharacters;
    }

    public int MaxLines { get; }
    /// <summary>Maximum retained UTF-16 code units, trimming only at grapheme boundaries.</summary>
    public int MaxCharacters { get; }
    public event Action? Changed;
    public LogSnapshot Snapshot { get { lock (_gate) return new(_text, _version, _start, _reset); } }

    public void Append(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        lock (_gate)
        {
            _text += text;
            Trim();
            _version++;
        }
        Changed?.Invoke();
    }

    public void AppendLine(string? text) => Append((text ?? "") + "\n");
    public void Clear() => Replace("");

    // Whole-text compatibility is a new document, even when the old selection
    // could numerically fit. Append is the operation that preserves inspection.
    internal void Replace(string text, bool trim = true)
    {
        lock (_gate)
        {
            _text = text;
            _start = 0;
            _reset++;
            if (trim) Trim();
            _version++;
        }
        Changed?.Invoke();
    }

    private void Trim()
    {
        int lines = _text.Length == 0 ? 0 : 1;
        foreach (char c in _text) if (c == '\n') lines++;
        int cut = 0;
        while (lines > MaxLines)
        {
            cut = _text.IndexOf('\n', cut) + 1;
            lines--;
        }
        int required = Math.Max(cut, _text.Length - MaxCharacters);
        if (required > cut)
        {
            // Prefer dropping whole old lines. An oversized final line retains
            // its bounded suffix without splitting surrogate/combining clusters.
            int newline = _text.IndexOf('\n', required - 1);
            cut = newline >= 0 && newline < _text.Length - 1 ? newline + 1 : required;
        }
        if (cut > 0)
        {
            var boundaries = StringInfo.ParseCombiningCharacters(_text);
            int index = Array.BinarySearch(boundaries, cut);
            if (index < 0) index = ~index;
            cut = index < boundaries.Length ? boundaries[index] : _text.Length;
            _text = _text[cut..];
            _start += cut;
        }
    }
}

/// <param name="StartOffset">UTF-16 units discarded from this document's start.</param>
/// <param name="ResetId">Changes on Clear/whole-text replacement.</param>
public sealed record LogSnapshot(string Text, long Version, long StartOffset, long ResetId);
