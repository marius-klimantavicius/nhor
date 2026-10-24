using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Marius.Winter;

/// <summary>A scalar-safe style in a bounded rich document. Null color follows the theme.</summary>
public readonly record struct RichTextStyle(TextFormat Format = TextFormat.None, Color4? Color = null);

/// <summary>UTF-16 range. Runs may separate scalars in a grapheme, never surrogate pairs.</summary>
public readonly record struct RichTextRun(int Start, int Length, RichTextStyle Style);

/// <summary>Immutable plain text and canonical, complete styled runs; no graphics or UI ownership.</summary>
public sealed class RichDocument : IEquatable<RichDocument>
{
    public const int SchemaVersion = 1;
    public const int MaximumTextLength = 262144;
    public const int MaximumRuns = 16384;
    private readonly ReadOnlyCollection<RichTextRun> _runs;
    public string Text { get; }
    public IReadOnlyList<RichTextRun> Runs => _runs;
    public static RichDocument Empty { get; } = new("");

    /// <summary>Copies sorted nonoverlapping runs, fills gaps with plain style and merges equal neighbors.</summary>
    public RichDocument(string text, IEnumerable<RichTextRun>? runs = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ValidateText(text);
        Text = text;
        var result = new List<RichTextRun>();
        int end = 0, count = 0;
        if (runs != null)
            foreach (var run in runs)
            {
                if (++count > MaximumRuns) throw new ArgumentException("Too many styled runs.", nameof(runs));
                if (run.Start < end || run.Length <= 0 || run.Start > text.Length - run.Length ||
                    TextEditingBoundaries.ScalarFloor(text, run.Start) != run.Start ||
                    TextEditingBoundaries.ScalarFloor(text, run.Start + run.Length) != run.Start + run.Length)
                    throw new ArgumentException("Runs must be sorted, nonoverlapping, positive and on scalar boundaries.", nameof(runs));
                ValidateStyle(run.Style);
                Add(result, end, run.Start - end, default);
                Add(result, run.Start, run.Length, run.Style);
                end = run.Start + run.Length;
            }
        Add(result, end, text.Length - end, default);
        if (result.Count > MaximumRuns) throw new ArgumentException("Too many canonical styled runs.", nameof(runs));
        _runs = result.AsReadOnly();
    }

    internal static void ValidateText(string text)
    {
        if (text.Length > MaximumTextLength) throw new ArgumentException("Document is too long.", nameof(text));
        for (int i = 0; i < text.Length; i++)
            if (char.IsSurrogate(text[i]) && (!char.IsHighSurrogate(text[i]) || ++i == text.Length || !char.IsLowSurrogate(text[i])))
                throw new ArgumentException("Text must contain valid Unicode scalars.", nameof(text));
    }

    private static void ValidateStyle(RichTextStyle style)
    {
        if ((style.Format & ~(TextFormat.Bold | TextFormat.Italic | TextFormat.Underline | TextFormat.Strikethrough)) != 0)
            throw new ArgumentException("Unknown text format.");
        if (style.Color is { } c && (!Unit(c.R) || !Unit(c.G) || !Unit(c.B) || !Unit(c.A)))
            throw new ArgumentException("Color channels must be finite and in [0,1].");
    }
    private static bool Unit(float value) => float.IsFinite(value) && value >= 0 && value <= 1;

    private static void Add(List<RichTextRun> runs, int start, int length, RichTextStyle style)
    {
        if (length == 0) return;
        if (runs.Count > 0 && runs[^1].Style == style)
            runs[^1] = runs[^1] with { Length = runs[^1].Length + length };
        else runs.Add(new(start, length, style));
    }

    /// <summary>Style at a scalar; at the end, the preceding scalar's style. Empty is plain.</summary>
    public RichTextStyle StyleAt(int offset)
    {
        if (offset < 0 || offset > Text.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        if (Text.Length == 0) return default;
        offset = Math.Min(offset, Text.Length - 1);
        foreach (var run in _runs) if (offset < run.Start + run.Length) return run.Style;
        return default;
    }

    private (int Start, int End) EditRange(int start, int length)
    {
        if (start < 0 || length < 0 || start > Text.Length - length) throw new ArgumentOutOfRangeException(nameof(start));
        var boundaries = new TextEditingBoundaries(); boundaries.SetText(Text);
        return (boundaries.Floor(start), length == 0 ? boundaries.Floor(start) : boundaries.Ceiling(start + length));
    }

    /// <summary>Replace whole graphemes. Insertion floors to a caret; omitted style inherits left (right at zero).</summary>
    public RichDocument Replace(int start, int length, string text, RichTextStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var (from, end) = EditRange(start, length);
        var inserted = style ?? StyleAt(from > 0 ? from - 1 : 0);
        var result = new List<RichTextRun>();
        foreach (var run in _runs)
            if (run.Start < from) Add(result, run.Start, Math.Min(from, run.Start + run.Length) - run.Start, run.Style);
        Add(result, from, text.Length, inserted);
        foreach (var run in _runs)
            if (run.Start + run.Length > end)
            {
                int source = Math.Max(run.Start, end);
                Add(result, from + text.Length + source - end, run.Start + run.Length - source, run.Style);
            }
        return new RichDocument(Text[..from] + text + Text[end..], result);
    }

    /// <summary>Set complete style over a scalar-safe UTF-16 range; expand partial surrogate pairs.</summary>
    public RichDocument WithStyle(int start, int length, RichTextStyle style)
    {
        ValidateStyle(style);
        return TransformStyle(start, length, _ => style);
    }

    internal RichDocument TransformStyle(int start, int length, Func<RichTextStyle, RichTextStyle> transform)
    {
        if (start < 0 || length < 0 || start > Text.Length - length) throw new ArgumentOutOfRangeException(nameof(start));
        if (length == 0) return this;
        int end = TextEditingBoundaries.ScalarCeiling(Text, start + length);
        start = TextEditingBoundaries.ScalarFloor(Text, start);
        var result = new List<RichTextRun>();
        foreach (var run in _runs)
        {
            int until = run.Start + run.Length;
            if (until <= start || run.Start >= end) { Add(result, run.Start, run.Length, run.Style); continue; }
            Add(result, run.Start, Math.Max(0, start - run.Start), run.Style);
            int from = Math.Max(run.Start, start), to = Math.Min(until, end);
            var style = transform(run.Style); ValidateStyle(style);
            Add(result, from, to - from, style);
            Add(result, to, until - to, run.Style);
        }
        return new RichDocument(Text, result);
    }

    public bool Equals(RichDocument? other) => ReferenceEquals(this, other) || other != null && Text == other.Text && _runs.SequenceEqual(other._runs);
    public override bool Equals(object? obj) => obj is RichDocument document && Equals(document);
    public override int GetHashCode()
    {
        var hash = new HashCode(); hash.Add(Text);
        foreach (var run in _runs) hash.Add(run);
        return hash.ToHashCode();
    }

    /// <summary>Version 1 JSON snapshot; history, selection and theme are not serialized.</summary>
    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject(); writer.WriteNumber("Version", SchemaVersion); writer.WriteString("Text", Text);
            writer.WriteStartArray("Runs");
            foreach (var run in _runs)
            {
                writer.WriteStartObject(); writer.WriteNumber("Start", run.Start); writer.WriteNumber("Length", run.Length);
                writer.WriteNumber("Format", (int)run.Style.Format);
                if (run.Style.Color is { } c)
                {
                    writer.WriteStartArray("Color"); writer.WriteNumberValue(c.R); writer.WriteNumberValue(c.G);
                    writer.WriteNumberValue(c.B); writer.WriteNumberValue(c.A); writer.WriteEndArray();
                }
                else writer.WriteNull("Color");
                writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static RichDocument FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > MaximumTextLength * 8 + MaximumRuns * 200) throw new ArgumentException("Serialized document is too long.", nameof(json));
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        int version = root.GetProperty("Version").GetInt32();
        if (version != SchemaVersion) throw new NotSupportedException($"Rich document version {version} is not supported.");
        var text = root.GetProperty("Text").GetString() ?? throw new JsonException("Text is required.");
        var runs = new List<RichTextRun>();
        foreach (var item in root.GetProperty("Runs").EnumerateArray())
        {
            if (runs.Count == MaximumRuns) throw new ArgumentException("Too many styled runs.", nameof(json));
            var color = item.GetProperty("Color"); Color4? channels = null;
            if (color.ValueKind != JsonValueKind.Null)
            {
                if (color.GetArrayLength() != 4) throw new JsonException("Expected four color channels.");
                channels = new Color4(color[0].GetSingle(), color[1].GetSingle(), color[2].GetSingle(), color[3].GetSingle());
            }
            runs.Add(new(item.GetProperty("Start").GetInt32(), item.GetProperty("Length").GetInt32(),
                new((TextFormat)item.GetProperty("Format").GetInt32(), channels)));
        }
        return new RichDocument(text, runs);
    }
}
