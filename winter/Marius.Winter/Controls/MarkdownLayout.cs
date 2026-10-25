using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Marius.Winter;

internal readonly record struct MarkdownFont(float Size, TextFormat Format = TextFormat.None, bool Code = false);
internal readonly record struct MarkdownTextMetrics(float Width, float Ascent, float Descent);
internal enum MarkdownDrawingKind { Text, CodeBackground, QuoteBar, Rule, Cell, Image, Task }
internal sealed record MarkdownDrawing(MarkdownDrawingKind Kind, RectF Bounds, string Text = "",
    MarkdownFont Font = default, MarkdownSpan? Span = null, float Baseline = 0, bool Header = false);
internal sealed class MarkdownLayout
{
    public List<MarkdownDrawing> Drawings { get; } = new();
    public float Width { get; private set; }
    public float Height { get; private set; }
    public static MarkdownLayout Build(MarkdownContent content, float width, float fontSize,
        Func<string, MarkdownFont, MarkdownTextMetrics> measure, IReadOnlyDictionary<string, Vector2>? images = null)
        => new Builder(Math.Max(1, width), fontSize, measure, images).Build(content);

    private sealed class Builder
    {
        private readonly MarkdownLayout _layout = new();
        private readonly float _width, _size;
        private readonly Func<string, MarkdownFont, MarkdownTextMetrics> _measure;
        private readonly IReadOnlyDictionary<string, Vector2>? _images;
        private readonly Dictionary<(string, MarkdownFont), MarkdownTextMetrics> _cache = new();
        public Builder(float width, float size, Func<string, MarkdownFont, MarkdownTextMetrics> measure, IReadOnlyDictionary<string, Vector2>? images)
        { _width = width; _size = size; _measure = measure; _images = images; }

        public MarkdownLayout Build(MarkdownContent content)
        {
            _layout.Width = _width;
            _layout.Height = Blocks(content.Blocks, 0, 0, _width);
            foreach (var drawing in _layout.Drawings) _layout.Width = Math.Max(_layout.Width, drawing.Bounds.X + drawing.Bounds.W);
            return _layout;
        }

        private MarkdownTextMetrics Metrics(string text, MarkdownFont font)
        {
            var key = (text, font);
            if (!_cache.TryGetValue(key, out var value)) _cache[key] = value = _measure(text, font);
            return value;
        }

        private float Blocks(IReadOnlyList<MarkdownBlock> blocks, float x, float y, float width, bool bold = false)
        {
            foreach (var block in blocks)
            {
                float start = y;
                switch (block.Kind)
                {
                    case MarkdownBlockKind.Paragraph:
                        y = Flow(block.Spans, x, y, width, _size, bold) + _size * .65f; break;
                    case MarkdownBlockKind.Heading:
                        float scale = block.Level switch { 1 => 2, 2 => 1.65f, 3 => 1.4f, 4 => 1.2f, 5 => 1.08f, _ => 1 };
                        if (y > 0) y += _size * .5f;
                        y = Flow(block.Spans, x, y, width, _size * scale, true) + _size * .5f; break;
                    case MarkdownBlockKind.Code:
                        int background = _layout.Drawings.Count;
                        _layout.Drawings.Add(new(MarkdownDrawingKind.CodeBackground, default));
                        float codeY = y + 10, widest = width;
                        var font = new MarkdownFont(_size * .92f, Code: true);
                        foreach (var line in block.Spans[0].Text.Split('\n'))
                        {
                            string text = ExpandTabs(line);
                            var metrics = Metrics(text.Length == 0 ? " " : text, font);
                            float height = Math.Max(_size * 1.45f, metrics.Ascent + metrics.Descent);
                            if (text.Length > 0) _layout.Drawings.Add(new(MarkdownDrawingKind.Text, new(x + 10, codeY, metrics.Width, height), text, font,
                                block.Spans[0], codeY + (height - metrics.Ascent - metrics.Descent) / 2 + metrics.Ascent));
                            widest = Math.Max(widest, metrics.Width + 20);
                            codeY += height;
                        }
                        _layout.Drawings[background] = new(MarkdownDrawingKind.CodeBackground, new(x, y, widest, codeY - y + 10));
                        y = codeY + 10 + _size * .75f; break;
                    case MarkdownBlockKind.Quote:
                        int bar = _layout.Drawings.Count;
                        _layout.Drawings.Add(new(MarkdownDrawingKind.QuoteBar, default));
                        y = Blocks(block.Children, x + _size * 1.2f, y + 4, Math.Max(1, width - _size * 1.2f), bold);
                        _layout.Drawings[bar] = new(MarkdownDrawingKind.QuoteBar, new(x, start, 3, y - start));
                        y += _size * .35f; break;
                    case MarkdownBlockKind.List:
                        var markerFont = new MarkdownFont(_size, bold ? TextFormat.Bold : TextFormat.None);
                        float markerWidth = 0;
                        foreach (var item in block.Children)
                            markerWidth = Math.Max(markerWidth, Metrics(item.Marker ?? "•", markerFont).Width);
                        float indent = Math.Max(_size * 1.8f, markerWidth + _size * .5f);
                        foreach (var item in block.Children)
                        {
                            bool task = item.Children.FirstOrDefault()?.Spans.FirstOrDefault()?.Task != null;
                            bool ordered = item.Marker != null && item.Marker != "•";
                            // A task checkbox replaces an unordered bullet, not
                            // its ordered label. Markers never wrap independently.
                            if (!task || ordered) Flow(new[] { new MarkdownSpan(item.Marker ?? "•") }, x, y, float.PositiveInfinity, _size, bold);
                            y = Blocks(item.Children, x + indent, y, Math.Max(1, width - indent), bold);
                            if (block.Level == 1) y += _size * .35f;
                        }
                        y += _size * .2f; break;
                    case MarkdownBlockKind.Rule:
                        _layout.Drawings.Add(new(MarkdownDrawingKind.Rule, new(x, y + _size * .5f, width, 1)));
                        y += _size * 1.5f; break;
                    case MarkdownBlockKind.Table:
                        y = Table(block, x, y, width) + _size * .75f; break;
                }
            }
            return y;
        }

        private float Table(MarkdownBlock table, float x, float y, float width)
        {
            int columns = table.Children.Count == 0 ? 0 : table.Children.Max(r => r.Children.Count);
            if (columns == 0) return y;
            float cellWidth = Math.Max(_size * 6, width / columns);
            foreach (var row in table.Children)
            {
                float bottom = y + _size * 2;
                var backgrounds = new List<int>();
                for (int i = 0; i < row.Children.Count; i++)
                {
                    var cell = row.Children[i];
                    float cellX = x + i * cellWidth;
                    backgrounds.Add(_layout.Drawings.Count);
                    _layout.Drawings.Add(new(MarkdownDrawingKind.Cell, default, Header: row.Level == 1));
                    float textY = y + 6;
                    foreach (var paragraph in cell.Children)
                        textY = Flow(paragraph.Spans, cellX + 8, textY, cellWidth - 16, _size, row.Level == 1, cell.Alignment);
                    bottom = Math.Max(bottom, textY + 6);
                }
                for (int i = 0; i < backgrounds.Count; i++)
                    _layout.Drawings[backgrounds[i]] = _layout.Drawings[backgrounds[i]] with { Bounds = new(x + i * cellWidth, y, cellWidth, bottom - y) };
                y = bottom;
            }
            return y;
        }

        private readonly record struct Piece(string Text, MarkdownSpan Span, MarkdownFont Font, float Width, float Ascent, float Descent,
            MarkdownDrawingKind Kind = MarkdownDrawingKind.Text, float Height = 0);

        private float Flow(IReadOnlyList<MarkdownSpan> spans, float left, float top, float width, float size, bool bold, int alignment = -1)
        {
            var line = new List<Piece>();
            var word = new List<Piece>();
            float wordWidth = 0;
            float used = 0, y = top;
            void Flush(bool force = false)
            {
                if (line.Count == 0 && !force) return;
                float ascent = size * .8f, descent = size * .2f, height = size * 1.5f;
                foreach (var piece in line)
                { ascent = Math.Max(ascent, piece.Ascent); descent = Math.Max(descent, piece.Descent); height = Math.Max(height, piece.Height); }
                height = Math.Max(height, ascent + descent);
                float baseline = y + (height - ascent - descent) / 2 + ascent;
                float x = left + (alignment == 1 ? Math.Max(0, width - used) : alignment == 0 ? Math.Max(0, (width - used) / 2) : 0);
                for (int i = 0; i < line.Count; i++)
                {
                    var piece = line[i];
                    var text = new StringBuilder(piece.Text);
                    float runWidth = piece.Width;
                    // Merge once per line, not repeatedly while growing an unbounded token.
                    while (i + 1 < line.Count && SameStyle(piece, line[i + 1]))
                    { var next = line[++i]; text.Append(next.Text); runWidth += next.Width; }
                    _layout.Drawings.Add(new(piece.Kind,
                        new(x, piece.Kind == MarkdownDrawingKind.Image ? baseline - piece.Ascent : y, runWidth, piece.Kind == MarkdownDrawingKind.Image ? piece.Height : height),
                        text.ToString(), piece.Font, piece.Span, baseline));
                    x += runWidth;
                }
                y += height; line.Clear(); used = 0;
            }
            void Add(Piece piece, bool whitespace = false, bool preserve = false)
            {
                if (used + piece.Width > width && line.Count > 0) Flush();
                if (whitespace && line.Count == 0 && !preserve) return;
                line.Add(piece); used += piece.Width;
            }
            void EndWord()
            {
                if (word.Count == 0) return;
                if (wordWidth <= width)
                {
                    // Style/link boundaries are not break opportunities. Move
                    // the complete logical word before appending its fragments.
                    if (used + wordWidth > width && line.Count > 0) Flush();
                    line.AddRange(word); used += wordWidth;
                }
                else
                {
                    // Only a genuinely overlong word falls back to graphemes.
                    // Each fragment is visited once; no growing-prefix measures.
                    foreach (var piece in word)
                    {
                        var elements = StringInfo.GetTextElementEnumerator(piece.Text);
                        while (elements.MoveNext())
                        {
                            string unit = elements.GetTextElement(); var m = Metrics(unit, piece.Font);
                            Add(piece with { Text = unit, Width = m.Width, Ascent = m.Ascent, Descent = m.Descent });
                        }
                    }
                }
                word.Clear(); wordWidth = 0;
            }

            foreach (var span in spans)
            {
                var font = new MarkdownFont(size, span.Format | (bold ? TextFormat.Bold : TextFormat.None), span.Code);
                if (span.Break) { EndWord(); Flush(true); continue; }
                if (span.Image != null && _images != null && _images.TryGetValue(span.Image, out var image) && image.X > 0 && image.Y > 0)
                {
                    EndWord();
                    float imageWidth = Math.Min(width, image.X);
                    float imageHeight = (float)((double)image.Y * imageWidth / image.X);
                    Add(new("", span, font, imageWidth, imageHeight, 0, MarkdownDrawingKind.Image, imageHeight));
                    continue;
                }
                if (span.Task != null)
                {
                    EndWord();
                    Add(new("", span, font, size * 1.4f, size * .8f, size * .2f, MarkdownDrawingKind.Task, size));
                    continue;
                }
                string value = span.Image == null ? span.Text : $"[image: {span.Text}]";
                int offset = 0;
                while (offset < value.Length)
                {
                    bool space = char.IsWhiteSpace(value[offset]);
                    int end = offset + 1;
                    while (end < value.Length && char.IsWhiteSpace(value[end]) == space) end++;
                    string token = value[offset..end]; offset = end;
                    var metrics = Metrics(token, font);
                    var piece = new Piece(token, span, font, metrics.Width, metrics.Ascent, metrics.Descent);
                    if (space)
                    { EndWord(); Add(piece, whitespace: true, preserve: span.Code); }
                    else
                    { word.Add(piece); wordWidth += piece.Width; }
                }
            }
            EndWord();
            Flush();
            return y;
        }

        private static bool SameStyle(Piece a, Piece b) => a.Kind == MarkdownDrawingKind.Text && b.Kind == a.Kind && a.Font == b.Font &&
            ReferenceEquals(a.Span.LinkIdentity, b.Span.LinkIdentity) && a.Span.Link == b.Span.Link && a.Span.Image == b.Span.Image && a.Span.Task == b.Span.Task;

        private static string ExpandTabs(string line)
        {
            if (!line.Contains('\t')) return line;
            var result = new StringBuilder();
            int column = 0;
            foreach (char c in line)
                if (c == '\t') { int spaces = 4 - column % 4; result.Append(' ', spaces); column += spaces; }
                else { result.Append(c); column++; }
            return result.ToString();
        }
    }
}
