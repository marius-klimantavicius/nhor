using System;
using System.Collections.Generic;
using System.Numerics;
using ThorVG;
using static Glfw.GLFW;

namespace Marius.Winter;

[Flags]
public enum TextFormat
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Underline = 4,
    Strikethrough = 8,
}

public class RichTextBox : Element
{
    private const float Padding = 8f;
    private const float ScrollbarWidth = 8f;
    private const float ScrollbarMargin = 2f;
    private const float AutoScrollMargin = 16f;
    private const float AutoScrollSpeed = 4f;
    private const float ItalicShear = 0.2f;

    // --- Styled run ---

    private struct TextRun
    {
        public int Start;
        public int Length;
        public TextFormat Format;
        public Color4? Color;

        public int End => Start + Length;
    }

    // --- Render slot (pooled ThorVG objects for visible run segments) ---

    private struct RenderSlot
    {
        public Text? TextPaint;
        public Shape? Underline;
        public Shape? Strikethrough;
    }

    // --- Fields ---

    private Shape? _backgroundShape;
    private Shape? _borderShape;
    private Shape? _cursorShape;
    private Shape? _selectionShape;
    private Shape? _clipShape;
    private Text? _placeholderPaint;
    private Scene? _placeholderScene;
    private Scene? _contentScene;
    private Shape? _scrollTrackV;
    private Shape? _scrollThumbV;
    private Shape? _scrollTrackH;
    private Shape? _scrollThumbH;

    private RenderSlot[] _slots = Array.Empty<RenderSlot>();
    private Text? _measureText;
    private Text? _measureTextBold;

    private string _text = "";
    private string _placeholder = "";
    private readonly List<TextRun> _runs = new();
    private string[] _lines = { "" };
    private int[] _lineOffsets = { 0 };

    private int _cursorPos;
    private int _selectionStart = -1;
    private TextFormat? _pendingFormat;
    private Color4? _pendingColor;

    private bool _shapesCreated;
    private float _scrollX;
    private float _scrollY;
    private float _lineHeight;
    private float _totalContentHeight;
    private float _maxLineWidth;
    private float _effectiveFontSize;
    private float _effectiveBoldFontSize;
    private float _ascent;
    private float _descent;
    private object? _blinkTag;
    private object? _autoScrollTag;
    private bool _mouseDown;
    private bool _scrollbarDragV;
    private bool _scrollbarDragH;
    private float _scrollbarDragStart;
    private float _scrollbarDragStartScroll;
    private float _lastMouseLx;
    private float _lastMouseLy;

    public Action<string>? TextChanged;
    public Action<TextFormat>? FormatChanged;

    // --- Constructor ---

    public RichTextBox(string text = "", string placeholder = "")
    {
        _text = text;
        _placeholder = placeholder;
        _cursorPos = text.Length;
        Cursor = CursorType.IBeam;
        Focusable = true;
        _runs.Add(new TextRun { Start = 0, Length = text.Length, Format = TextFormat.None, Color = null });
        SplitLines();
    }

    // --- Properties ---

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            _cursorPos = Math.Clamp(_cursorPos, 0, value.Length);
            _selectionStart = -1;
            _pendingFormat = null;
            _pendingColor = null;
            _runs.Clear();
            _runs.Add(new TextRun { Start = 0, Length = value.Length, Format = TextFormat.None, Color = null });
            SplitLines();
            UpdateContent();
            MarkDirty();
            TextChanged?.Invoke(value);
        }
    }

    public string Placeholder
    {
        get => _placeholder;
        set
        {
            _placeholder = value;
            _placeholderPaint?.SetText(value);
            _placeholderScene?.Visible(string.IsNullOrEmpty(_text));
            MarkDirty();
        }
    }

    // --- Format operations ---

    public void ToggleBold() => ToggleFormatFlag(TextFormat.Bold);
    public void ToggleItalic() => ToggleFormatFlag(TextFormat.Italic);
    public void ToggleUnderline() => ToggleFormatFlag(TextFormat.Underline);
    public void ToggleStrikethrough() => ToggleFormatFlag(TextFormat.Strikethrough);

    public void SetSelectionColor(Color4 color)
    {
        if (!HasSelection())
        {
            _pendingColor = color;
            return;
        }

        int start = Math.Min(_selectionStart, _cursorPos);
        int end = Math.Max(_selectionStart, _cursorPos);
        ApplyColorToRange(start, end, color);
        MergeAdjacentRuns();
        UpdateContent();
        MarkDirty();
    }

    public void ClearFormatting()
    {
        if (!HasSelection()) return;

        int start = Math.Min(_selectionStart, _cursorPos);
        int end = Math.Max(_selectionStart, _cursorPos);
        SplitRunAt(start);
        SplitRunAt(end);

        for (int i = 0; i < _runs.Count; i++)
        {
            var run = _runs[i];
            if (run.Start >= start && run.End <= end)
            {
                run.Format = TextFormat.None;
                run.Color = null;
                _runs[i] = run;
            }
        }

        MergeAdjacentRuns();
        UpdateContent();
        MarkDirty();
    }

    public TextFormat GetCurrentFormat()
    {
        if (_pendingFormat.HasValue) return _pendingFormat.Value;
        if (_runs.Count == 0) return TextFormat.None;
        int ri = FindRunIndex(_cursorPos > 0 ? _cursorPos - 1 : 0);
        if (ri >= 0 && ri < _runs.Count) return _runs[ri].Format;
        return TextFormat.None;
    }

    public Color4? GetCurrentColor()
    {
        if (_pendingColor.HasValue) return _pendingColor.Value;
        if (_runs.Count == 0) return null;
        int ri = FindRunIndex(_cursorPos > 0 ? _cursorPos - 1 : 0);
        if (ri >= 0 && ri < _runs.Count) return _runs[ri].Color;
        return null;
    }

    // --- Programmatic formatting ---

    public void SetFormat(int start, int length, TextFormat format)
    {
        if (start < 0 || length <= 0 || start + length > _text.Length) return;
        int end = start + length;
        SplitRunAt(start);
        SplitRunAt(end);
        for (int i = 0; i < _runs.Count; i++)
        {
            var run = _runs[i];
            if (run.Start >= start && run.End <= end)
            {
                run.Format = format;
                _runs[i] = run;
            }
        }
        MergeAdjacentRuns();
        UpdateContent();
        MarkDirty();
    }

    public void SetColor(int start, int length, Color4 color)
    {
        if (start < 0 || length <= 0 || start + length > _text.Length) return;
        ApplyColorToRange(start, start + length, color);
        MergeAdjacentRuns();
        UpdateContent();
        MarkDirty();
    }

    // --- Lifecycle ---

    private void SplitLines()
    {
        _lines = _text.Split('\n');
        _lineOffsets = new int[_lines.Length];
        int offset = 0;
        for (int i = 0; i < _lines.Length; i++)
        {
            _lineOffsets[i] = offset;
            offset += _lines[i].Length + 1;
        }
    }

    protected override void OnAttached()
    {
        if (!_shapesCreated)
        {
            _shapesCreated = true;
            var style = Style;
            var theme = OwnerWindow?.Theme ?? Theme.Dark;
            _effectiveFontSize = EffectiveFontSize(style.FontSize);
            _effectiveBoldFontSize = EffectiveBoldFontSize(style.FontSize);
            _blinkTag = new object();
            _autoScrollTag = new object();

            // Compute font metrics for line height
            var mt = ThorVG.Text.Gen()!;
            mt.SetFont(style.FontName);
            mt.SetFontSize(_effectiveFontSize);
            mt.SetText("Mg");
            mt.GetMetrics(out var metrics);
            _ascent = metrics.ascent;
            _descent = metrics.descent;
            _lineHeight = (_ascent - _descent) * 1.35f;
            if (_lineHeight <= 0) _lineHeight = _effectiveFontSize * 1.35f;

            // Create cached measurement texts
            _measureText = ThorVG.Text.Gen()!;
            _measureText.SetFont(style.FontName);
            _measureText.SetFontSize(_effectiveFontSize);

            _measureTextBold = ThorVG.Text.Gen()!;
            _measureTextBold.SetFont("default-bold");
            _measureTextBold.SetFontSize(_effectiveBoldFontSize);

            // Background
            _backgroundShape = Shape.Gen();
            _backgroundShape!.SetFill(style.Background.R8, style.Background.G8, style.Background.B8, style.Background.A8);
            AddPaint(_backgroundShape);

            // Border
            _borderShape = Shape.Gen();
            _borderShape!.StrokeWidth(1f);
            _borderShape.StrokeFill(style.Border.R8, style.Border.G8, style.Border.B8, style.Border.A8);
            _borderShape.SetFill(0, 0, 0, 0);
            AddPaint(_borderShape);

            // Content scene (clipped)
            _contentScene = Scene.Gen()!;
            AddPaint(_contentScene);

            _clipShape = Shape.Gen();
            _clipShape!.SetFill(255, 255, 255, 255);
            _contentScene.Clip(_clipShape);

            // Selection
            _selectionShape = Shape.Gen();
            var selColor = theme.SelectionColor;
            _selectionShape!.SetFill(selColor.R8, selColor.G8, selColor.B8, selColor.A8);
            _selectionShape.Visible(false);
            _contentScene.Add(_selectionShape);

            // Placeholder
            _placeholderPaint = ThorVG.Text.Gen();
            _placeholderPaint!.SetFont(style.FontName);
            _placeholderPaint.SetFontSize(_effectiveFontSize);
            _placeholderPaint.SetText(_placeholder);
            _placeholderPaint.SetFill(theme.DisabledTextColor.R8, theme.DisabledTextColor.G8, theme.DisabledTextColor.B8);

            _placeholderScene = Scene.Gen()!;
            _placeholderScene.Add(_placeholderPaint);
            _placeholderScene.Visible(string.IsNullOrEmpty(_text));
            _contentScene.Add(_placeholderScene);

            // Cursor
            _cursorShape = Shape.Gen();
            _cursorShape!.SetFill(theme.CursorColor.R8, theme.CursorColor.G8, theme.CursorColor.B8, theme.CursorColor.A8);
            _cursorShape.Visible(false);
            _contentScene.Add(_cursorShape);

            // Scrollbars
            _scrollTrackV = Shape.Gen();
            _scrollTrackV!.SetFill(128, 128, 128, 40);
            AddPaint(_scrollTrackV);

            _scrollThumbV = Shape.Gen();
            _scrollThumbV!.SetFill(128, 128, 128, 100);
            AddPaint(_scrollThumbV);

            _scrollTrackH = Shape.Gen();
            _scrollTrackH!.SetFill(128, 128, 128, 40);
            AddPaint(_scrollTrackH);

            _scrollThumbH = Shape.Gen();
            _scrollThumbH!.SetFill(128, 128, 128, 100);
            AddPaint(_scrollThumbH);
        }
    }

    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        return new Vector2(200, 120);
    }

    protected override void OnSizeChanged()
    {
        float w = Bounds.W, h = Bounds.H;

        _backgroundShape?.ResetShape();
        _backgroundShape?.AppendRect(1, 2, w - 2, h - 2, 3, 3);

        _borderShape?.ResetShape();
        _borderShape?.AppendRect(0.5f, 0.5f, w - 1, h - 1, 2.5f, 2.5f);

        _clipShape?.ResetShape();
        _clipShape?.AppendRect(Padding, Padding - 1, w - 2 * Padding, h - 2 * Padding + 1, 0, 0);

        UpdateContent();
    }

    protected override void OnStateChanged(ElementState oldState, ElementState newState)
    {
        var style = Style;
        if ((newState & ElementState.Focused) != 0)
            _borderShape?.StrokeFill(style.BorderFocused.R8, style.BorderFocused.G8, style.BorderFocused.B8, style.BorderFocused.A8);
        else
            _borderShape?.StrokeFill(style.Border.R8, style.Border.G8, style.Border.B8, style.Border.A8);
        MarkDirty();
    }

    public override void OnFocus()
    {
        State = State | ElementState.Focused;
        _cursorShape?.Visible(true);
        StartBlinkAnimation();
        MarkDirty();
    }

    public override void OnBlur()
    {
        State = State & ~ElementState.Focused;
        _cursorShape?.Visible(false);
        _selectionShape?.Visible(false);
        _selectionStart = -1;
        _mouseDown = false;
        StopAutoScroll();
        StopBlinkAnimation();
        MarkDirty();
    }

    // --- Run management ---

    private int FindRunIndex(int charPos)
    {
        for (int i = 0; i < _runs.Count; i++)
        {
            if (charPos < _runs[i].End)
                return i;
        }
        return _runs.Count - 1;
    }

    private void SplitRunAt(int charPos)
    {
        if (charPos <= 0 || charPos >= _text.Length) return;
        for (int i = 0; i < _runs.Count; i++)
        {
            var run = _runs[i];
            if (charPos > run.Start && charPos < run.End)
            {
                int leftLen = charPos - run.Start;
                int rightLen = run.Length - leftLen;
                _runs[i] = new TextRun { Start = run.Start, Length = leftLen, Format = run.Format, Color = run.Color };
                _runs.Insert(i + 1, new TextRun { Start = charPos, Length = rightLen, Format = run.Format, Color = run.Color });
                return;
            }
        }
    }

    private void InsertTextInRuns(int pos, int len)
    {
        if (len <= 0) return;

        // Determine format for inserted text
        TextFormat fmt;
        Color4? color;
        if (_pendingFormat.HasValue || _pendingColor.HasValue)
        {
            fmt = _pendingFormat ?? GetFormatAtPos(pos);
            color = _pendingColor ?? GetColorAtPos(pos);
        }
        else
        {
            fmt = GetFormatAtPos(pos);
            color = GetColorAtPos(pos);
        }

        if (_runs.Count == 0)
        {
            _runs.Add(new TextRun { Start = 0, Length = len, Format = fmt, Color = color });
            return;
        }

        int ri = FindRunIndexForInsert(pos);

        // Shift all runs after the insertion point
        for (int i = ri + 1; i < _runs.Count; i++)
        {
            var r = _runs[i];
            r.Start += len;
            _runs[i] = r;
        }

        var run = _runs[ri];

        if (RunStyleEquals(run, fmt, color))
        {
            // Same format: extend the run
            run.Length += len;
            _runs[ri] = run;
        }
        else if (pos == run.Start)
        {
            // Insert before this run
            _runs.Insert(ri, new TextRun { Start = pos, Length = len, Format = fmt, Color = color });
            // Shift this run
            var shifted = _runs[ri + 1];
            shifted.Start += len;
            _runs[ri + 1] = shifted;
        }
        else if (pos == run.End)
        {
            // Insert after this run (before next)
            // Shift already done above for runs after ri
            _runs.Insert(ri + 1, new TextRun { Start = pos, Length = len, Format = fmt, Color = color });
        }
        else
        {
            // Insert in the middle of a run — split it
            int leftLen = pos - run.Start;
            int rightLen = run.Length - leftLen;
            _runs[ri] = new TextRun { Start = run.Start, Length = leftLen, Format = run.Format, Color = run.Color };
            _runs.Insert(ri + 1, new TextRun { Start = pos, Length = len, Format = fmt, Color = color });
            _runs.Insert(ri + 2, new TextRun { Start = pos + len, Length = rightLen, Format = run.Format, Color = run.Color });
        }

        _pendingFormat = null;
        _pendingColor = null;
    }

    private void DeleteRangeInRuns(int start, int len)
    {
        if (len <= 0) return;
        int end = start + len;

        for (int i = _runs.Count - 1; i >= 0; i--)
        {
            var run = _runs[i];

            if (run.End <= start)
            {
                // Completely before deleted range — no change
                continue;
            }
            else if (run.Start >= end)
            {
                // Completely after deleted range — shift back
                run.Start -= len;
                _runs[i] = run;
            }
            else if (run.Start >= start && run.End <= end)
            {
                // Completely inside deleted range — remove
                _runs.RemoveAt(i);
            }
            else if (run.Start < start && run.End > end)
            {
                // Straddles the entire deleted range — shrink
                run.Length -= len;
                _runs[i] = run;
            }
            else if (run.Start < start)
            {
                // Overlaps from the left — truncate
                run.Length = start - run.Start;
                _runs[i] = run;
            }
            else
            {
                // Overlaps from the right — truncate and shift
                int overlap = end - run.Start;
                run.Start = start;
                run.Length -= overlap;
                _runs[i] = run;
            }
        }

        // Ensure at least one run exists
        if (_runs.Count == 0)
            _runs.Add(new TextRun { Start = 0, Length = 0, Format = TextFormat.None, Color = null });

        MergeAdjacentRuns();
    }

    private void ToggleFormatFlag(TextFormat flag)
    {
        if (!HasSelection())
        {
            // Toggle pending format
            var cur = _pendingFormat ?? GetCurrentFormat();
            if ((cur & flag) != 0)
                _pendingFormat = cur & ~flag;
            else
                _pendingFormat = cur | flag;
            FormatChanged?.Invoke(_pendingFormat.Value);
            return;
        }

        int start = Math.Min(_selectionStart, _cursorPos);
        int end = Math.Max(_selectionStart, _cursorPos);

        // Check if all runs in range have the flag
        bool allHaveFlag = true;
        for (int i = 0; i < _runs.Count; i++)
        {
            var run = _runs[i];
            if (run.End <= start || run.Start >= end) continue;
            if ((run.Format & flag) == 0) { allHaveFlag = false; break; }
        }

        SplitRunAt(start);
        SplitRunAt(end);

        for (int i = 0; i < _runs.Count; i++)
        {
            var run = _runs[i];
            if (run.Start >= start && run.End <= end)
            {
                if (allHaveFlag)
                    run.Format &= ~flag;
                else
                    run.Format |= flag;
                _runs[i] = run;
            }
        }

        MergeAdjacentRuns();
        UpdateContent();
        MarkDirty();
    }

    private void ApplyColorToRange(int start, int end, Color4 color)
    {
        SplitRunAt(start);
        SplitRunAt(end);

        for (int i = 0; i < _runs.Count; i++)
        {
            var run = _runs[i];
            if (run.Start >= start && run.End <= end)
            {
                run.Color = color;
                _runs[i] = run;
            }
        }
    }

    private void MergeAdjacentRuns()
    {
        for (int i = _runs.Count - 2; i >= 0; i--)
        {
            var a = _runs[i];
            var b = _runs[i + 1];
            if (RunStyleEquals(a, b.Format, b.Color))
            {
                a.Length += b.Length;
                _runs[i] = a;
                _runs.RemoveAt(i + 1);
            }
        }
    }

    private TextFormat GetFormatAtPos(int pos)
    {
        if (_runs.Count == 0) return TextFormat.None;
        // For insert: use the run to the left of pos if at a boundary
        int ri = FindRunIndex(pos > 0 ? pos - 1 : 0);
        if (ri >= 0 && ri < _runs.Count) return _runs[ri].Format;
        return TextFormat.None;
    }

    private Color4? GetColorAtPos(int pos)
    {
        if (_runs.Count == 0) return null;
        int ri = FindRunIndex(pos > 0 ? pos - 1 : 0);
        if (ri >= 0 && ri < _runs.Count) return _runs[ri].Color;
        return null;
    }

    private int FindRunIndexForInsert(int pos)
    {
        for (int i = 0; i < _runs.Count; i++)
        {
            if (pos <= _runs[i].End) return i;
        }
        return _runs.Count - 1;
    }

    private static bool RunStyleEquals(TextRun run, TextFormat fmt, Color4? color)
    {
        if (run.Format != fmt) return false;
        if (run.Color.HasValue != color.HasValue) return false;
        if (run.Color.HasValue && color.HasValue)
        {
            var a = run.Color.Value;
            var b = color.Value;
            return a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A;
        }
        return true;
    }

    // --- Content rendering ---

    private bool NeedsScrollbarV()
    {
        float viewportH = Bounds.H - 2 * Padding;
        return _totalContentHeight > viewportH;
    }

    private bool NeedsScrollbarH()
    {
        float viewportW = Bounds.W - 2 * Padding;
        if (NeedsScrollbarV()) viewportW -= ScrollbarWidth + ScrollbarMargin;
        return _maxLineWidth > viewportW;
    }

    private float ViewportW()
    {
        float w = Bounds.W - 2 * Padding;
        if (NeedsScrollbarV()) w -= ScrollbarWidth + ScrollbarMargin;
        return w;
    }

    private float ViewportH()
    {
        float h = Bounds.H - 2 * Padding;
        if (NeedsScrollbarH()) h -= ScrollbarWidth + ScrollbarMargin;
        return h;
    }

    private void UpdateContent()
    {
        if (!_shapesCreated || Bounds.W <= 0 || Bounds.H <= 0) return;

        _totalContentHeight = _lines.Length * _lineHeight;

        // Compute max line width
        _maxLineWidth = 0;
        for (int i = 0; i < _lines.Length; i++)
        {
            if (_lines[i].Length > 0)
            {
                float lw = GetXOffsetInLine(i, _lines[i].Length);
                if (lw > _maxLineWidth) _maxLineWidth = lw;
            }
        }

        _placeholderScene?.Visible(string.IsNullOrEmpty(_text));

        if (!string.IsNullOrEmpty(_placeholder))
        {
            MeasureLineBounds(_placeholder, false, out _, out float pby, out _, out _);
            _placeholderScene?.Translate(Padding - _scrollX, Padding - pby);
        }

        float viewportH = ViewportH();
        int firstVisible = Math.Max(0, (int)(_scrollY / _lineHeight));
        int lastVisible = Math.Min(_lines.Length - 1, (int)((_scrollY + viewportH) / _lineHeight));
        int visibleCount = lastVisible >= firstVisible ? lastVisible - firstVisible + 1 : 0;

        // Count total run segments needed for visible lines
        int totalSlots = 0;
        for (int i = 0; i < visibleCount; i++)
        {
            int lineIdx = firstVisible + i;
            totalSlots += CountRunSegmentsOnLine(lineIdx);
        }

        EnsureRenderSlots(totalSlots);

        // Assign slots to visible run segments
        int slotIdx = 0;
        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        var defaultTextColor = theme.TextColor;

        for (int i = 0; i < visibleCount; i++)
        {
            int lineIdx = firstVisible + i;
            string line = _lines[lineIdx];
            int lineStart = _lineOffsets[lineIdx];
            float lineY = lineIdx * _lineHeight - _scrollY;

            MeasureLineBounds("M", false, out _, out float by, out _, out float bh);
            float baselineY = Padding + lineY - by;

            float x = 0;

            for (int ri = 0; ri < _runs.Count; ri++)
            {
                var run = _runs[ri];
                int segStart = Math.Max(0, run.Start - lineStart);
                int segEnd = Math.Min(line.Length, run.End - lineStart);

                if (segEnd <= segStart) continue;
                if (segStart >= line.Length) break;
                if (segEnd <= 0) continue;

                string segText = line[segStart..segEnd];
                bool bold = (run.Format & TextFormat.Bold) != 0;
                bool italic = (run.Format & TextFormat.Italic) != 0;
                bool underline = (run.Format & TextFormat.Underline) != 0;
                bool strikethrough = (run.Format & TextFormat.Strikethrough) != 0;

                var slot = _slots[slotIdx];

                // Configure text paint
                slot.TextPaint!.SetFont(bold ? "default-bold" : Style.FontName);
                slot.TextPaint.SetFontSize(bold ? _effectiveBoldFontSize : _effectiveFontSize);
                slot.TextPaint.SetItalic(italic ? ItalicShear : 0f);
                slot.TextPaint.SetText(segText);

                var color = run.Color ?? defaultTextColor;
                slot.TextPaint.SetFill(color.R8, color.G8, color.B8);

                slot.TextPaint.Translate(Padding + x - _scrollX, baselineY);

                float segWidth = MeasureRunWidth(segText, bold);

                // Underline decoration
                if (underline)
                {
                    float ulY = Padding + lineY + (_ascent - _descent) * 1.1f;
                    slot.Underline!.ResetShape();
                    slot.Underline.AppendRect(Padding + x - _scrollX, ulY, segWidth, 1f, 0, 0);
                    slot.Underline.SetFill(color.R8, color.G8, color.B8, color.A8);
                    slot.Underline.Visible(true);
                }
                else
                {
                    slot.Underline!.Visible(false);
                }

                // Strikethrough decoration
                if (strikethrough)
                {
                    float stY = Padding + lineY + (_ascent - _descent) * 0.55f;
                    slot.Strikethrough!.ResetShape();
                    slot.Strikethrough.AppendRect(Padding + x - _scrollX, stY, segWidth, 1f, 0, 0);
                    slot.Strikethrough.SetFill(color.R8, color.G8, color.B8, color.A8);
                    slot.Strikethrough.Visible(true);
                }
                else
                {
                    slot.Strikethrough!.Visible(false);
                }

                x += segWidth;
                slotIdx++;
            }
        }

        // Hide unused slots
        for (int i = slotIdx; i < _slots.Length; i++)
        {
            _slots[i].TextPaint?.SetText("");
            _slots[i].Underline?.Visible(false);
            _slots[i].Strikethrough?.Visible(false);
        }

        UpdateCursorPosition();
        UpdateSelection();
        UpdateScrollbars();
    }

    private int CountRunSegmentsOnLine(int lineIdx)
    {
        string line = _lines[lineIdx];
        if (line.Length == 0) return 0;
        int lineStart = _lineOffsets[lineIdx];
        int lineEnd = lineStart + line.Length;
        int count = 0;
        for (int i = 0; i < _runs.Count; i++)
        {
            var run = _runs[i];
            if (run.End <= lineStart) continue;
            if (run.Start >= lineEnd) break;
            int segStart = Math.Max(lineStart, run.Start);
            int segEnd = Math.Min(lineEnd, run.End);
            if (segEnd > segStart) count++;
        }
        return count;
    }

    private void EnsureRenderSlots(int count)
    {
        if (_slots.Length >= count) return;

        int oldLen = _slots.Length;
        var newSlots = new RenderSlot[count];
        Array.Copy(_slots, newSlots, oldLen);

        for (int i = oldLen; i < count; i++)
        {
            var text = ThorVG.Text.Gen()!;
            text.SetFont(Style.FontName);
            text.SetFontSize(_effectiveFontSize);
            text.SetText("");
            var theme = OwnerWindow?.Theme ?? Theme.Dark;
            text.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);
            _contentScene!.Add(text);

            var ul = Shape.Gen()!;
            ul.SetFill(255, 255, 255, 255);
            ul.Visible(false);
            _contentScene.Add(ul);

            var st = Shape.Gen()!;
            st.SetFill(255, 255, 255, 255);
            st.Visible(false);
            _contentScene.Add(st);

            newSlots[i] = new RenderSlot { TextPaint = text, Underline = ul, Strikethrough = st };
        }

        _slots = newSlots;
    }

    private void UpdateScrollbars()
    {
        float w = Bounds.W, h = Bounds.H;
        bool needsV = NeedsScrollbarV();
        bool needsH = NeedsScrollbarH();
        float halfW = ScrollbarWidth / 2;

        _scrollTrackV?.ResetShape();
        _scrollThumbV?.ResetShape();

        if (!needsV)
        {
            _scrollTrackV?.Visible(false);
            _scrollThumbV?.Visible(false);
        }
        else
        {
            _scrollTrackV?.Visible(true);
            _scrollThumbV?.Visible(true);

            float trackX = w - ScrollbarWidth - ScrollbarMargin;
            float trackY = Padding;
            float trackH = ViewportH();

            _scrollTrackV?.AppendRect(trackX, trackY, ScrollbarWidth, trackH, halfW, halfW);

            float ratio = trackH / _totalContentHeight;
            float thumbH = Math.Max(20, trackH * ratio);
            float maxScroll = _totalContentHeight - trackH;
            float thumbY = trackY + (maxScroll > 0 ? (_scrollY / maxScroll) * (trackH - thumbH) : 0);

            _scrollThumbV?.AppendRect(trackX, thumbY, ScrollbarWidth, thumbH, halfW, halfW);
        }

        _scrollTrackH?.ResetShape();
        _scrollThumbH?.ResetShape();

        if (!needsH)
        {
            _scrollTrackH?.Visible(false);
            _scrollThumbH?.Visible(false);
        }
        else
        {
            _scrollTrackH?.Visible(true);
            _scrollThumbH?.Visible(true);

            float trackY = h - ScrollbarWidth - ScrollbarMargin;
            float trackX = Padding;
            float trackW = ViewportW();

            _scrollTrackH?.AppendRect(trackX, trackY, trackW, ScrollbarWidth, halfW, halfW);

            float ratio = trackW / _maxLineWidth;
            float thumbW = Math.Max(20, trackW * ratio);
            float maxScroll = _maxLineWidth - trackW;
            float thumbX = trackX + (maxScroll > 0 ? (_scrollX / maxScroll) * (trackW - thumbW) : 0);

            _scrollThumbH?.AppendRect(thumbX, trackY, thumbW, ScrollbarWidth, halfW, halfW);
        }
    }

    // --- Text measurement ---

    private float MeasureRunWidth(string text, bool bold)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var mt = bold ? _measureTextBold : _measureText;
        if (mt == null)
        {
            // Fallback before OnAttached
            var t = ThorVG.Text.Gen()!;
            t.SetFont(bold ? "default-bold" : Style.FontName);
            t.SetFontSize(bold ? _effectiveBoldFontSize : _effectiveFontSize);
            t.SetText(text);
            if (t.GetTextSize(out float fw, out _) == Result.Success)
                return fw;
            return 0;
        }
        mt.SetText(text);
        if (mt.GetTextSize(out float w, out _) == Result.Success)
            return w;
        return 0;
    }

    private void MeasureLineBounds(string text, bool bold, out float bx, out float by, out float bw, out float bh)
    {
        bx = by = 0;
        bw = 0;
        bh = _effectiveFontSize;
        if (string.IsNullOrEmpty(text)) text = "M";
        var t = ThorVG.Text.Gen();
        if (t == null) return;
        t.SetFont(bold ? "default-bold" : Style.FontName);
        t.SetFontSize(bold ? _effectiveBoldFontSize : _effectiveFontSize);
        t.SetText(text);
        t.Bounds(out bx, out by, out bw, out bh);
        if (bh <= 0) bh = _effectiveFontSize;
    }

    private float EffectiveBoldFontSize(float requestedSize)
    {
        var t = ThorVG.Text.Gen();
        if (t == null) return requestedSize;
        t.SetFont("default-bold");
        t.SetFontSize(requestedSize);
        t.SetText("M");
        t.GetMetrics(out var m);
        float measuredHeight = m.ascent - m.descent;
        if (measuredHeight > 0)
            return requestedSize * requestedSize / measuredHeight;
        return requestedSize;
    }

    /// <summary>
    /// Get x-offset of column <paramref name="col"/> in line <paramref name="lineIdx"/>,
    /// accounting for per-run font differences.
    /// </summary>
    private float GetXOffsetInLine(int lineIdx, int col)
    {
        if (col <= 0) return 0;

        string line = _lines[lineIdx];
        int lineStart = _lineOffsets[lineIdx];
        float x = 0;

        for (int ri = 0; ri < _runs.Count; ri++)
        {
            var run = _runs[ri];
            int segStartInLine = Math.Max(0, run.Start - lineStart);
            int segEndInLine = Math.Min(line.Length, run.End - lineStart);

            if (segEndInLine <= 0) continue;
            if (segStartInLine >= line.Length) break;
            if (segStartInLine >= col) break;

            int measEnd = Math.Min(segEndInLine, col);
            if (measEnd > segStartInLine)
            {
                bool bold = (run.Format & TextFormat.Bold) != 0;
                x += MeasureRunWidth(line[segStartInLine..measEnd], bold);
            }
        }

        return x;
    }

    // --- Cursor & position mapping ---

    private int GetLineAtCharIndex(int charIdx)
    {
        for (int i = _lineOffsets.Length - 1; i >= 0; i--)
        {
            if (charIdx >= _lineOffsets[i])
                return i;
        }
        return 0;
    }

    private int GetColumnInLine(int charIdx, int line)
    {
        return charIdx - _lineOffsets[line];
    }

    private float GetCursorX()
    {
        int line = GetLineAtCharIndex(_cursorPos);
        int col = GetColumnInLine(_cursorPos, line);
        return GetXOffsetInLine(line, col);
    }

    private float GetCursorY()
    {
        int line = GetLineAtCharIndex(_cursorPos);
        return line * _lineHeight;
    }

    private void UpdateCursorPosition()
    {
        if (_cursorShape == null) return;

        int line = GetLineAtCharIndex(_cursorPos);
        int col = GetColumnInLine(_cursorPos, line);
        float cursorX = GetXOffsetInLine(line, col);
        float cursorY = line * _lineHeight - _scrollY;

        MeasureLineBounds("M", false, out _, out _, out _, out float bh);
        _cursorShape.ResetShape();
        _cursorShape.AppendRect(Padding + cursorX - _scrollX, Padding + cursorY, 1f, bh, 0, 0);
    }

    private void EnsureCursorVisible()
    {
        float viewportH = ViewportH();
        float viewportW = ViewportW();
        float cursorY = GetCursorY();
        float cursorX = GetCursorX();

        if (cursorY - _scrollY + _lineHeight > viewportH)
            _scrollY = cursorY + _lineHeight - viewportH;
        else if (cursorY - _scrollY < 0)
            _scrollY = cursorY;

        const float hMargin = 4f;
        if (cursorX - _scrollX + hMargin > viewportW)
            _scrollX = cursorX - viewportW + hMargin;
        else if (cursorX - _scrollX < 0)
            _scrollX = cursorX;

        ClampScroll();
        UpdateContent();
    }

    private void ClampScroll()
    {
        float viewportH = ViewportH();
        float viewportW = ViewportW();
        float maxScrollY = Math.Max(0, _totalContentHeight - viewportH);
        float maxScrollX = Math.Max(0, _maxLineWidth - viewportW);
        _scrollY = Math.Clamp(_scrollY, 0, maxScrollY);
        _scrollX = Math.Clamp(_scrollX, 0, maxScrollX);
    }

    private int GetCharIndexAtPosition(float lx, float ly)
    {
        float textY = ly - Padding + _scrollY;
        int line = Math.Clamp((int)(textY / _lineHeight), 0, Math.Max(0, _lines.Length - 1));

        float textX = lx - Padding + _scrollX;
        if (textX <= 0) return _lineOffsets[line];

        string lineText = _lines[line];
        if (string.IsNullOrEmpty(lineText)) return _lineOffsets[line];

        float prevX = 0;
        for (int i = 1; i <= lineText.Length; i++)
        {
            float x = GetXOffsetInLine(line, i);
            float midpoint = (prevX + x) / 2f;
            if (textX < midpoint) return _lineOffsets[line] + i - 1;
            prevX = x;
        }
        return _lineOffsets[line] + lineText.Length;
    }

    // --- Selection ---

    private bool HasSelection() => _selectionStart >= 0 && _selectionStart != _cursorPos;

    private void UpdateSelection()
    {
        if (!HasSelection())
        {
            _selectionShape?.Visible(false);
            return;
        }

        int start = Math.Min(_selectionStart, _cursorPos);
        int end = Math.Max(_selectionStart, _cursorPos);

        int startLine = GetLineAtCharIndex(start);
        int endLine = GetLineAtCharIndex(end);

        _selectionShape?.ResetShape();
        bool anyRect = false;

        float contentWidth = ViewportW();

        for (int line = startLine; line <= endLine; line++)
        {
            float lineY = Padding + line * _lineHeight - _scrollY;
            if (lineY + _lineHeight < Padding || lineY > Bounds.H - Padding)
                continue;

            float selX1, selX2;

            if (line == startLine)
            {
                int colStart = start - _lineOffsets[line];
                selX1 = Padding + GetXOffsetInLine(line, colStart) - _scrollX;
            }
            else
            {
                selX1 = Padding - _scrollX;
            }

            if (line == endLine)
            {
                int colEnd = end - _lineOffsets[line];
                colEnd = Math.Min(colEnd, _lines[line].Length);
                selX2 = Padding + GetXOffsetInLine(line, colEnd) - _scrollX;
            }
            else
            {
                selX2 = Padding + contentWidth;
            }

            if (selX2 > selX1)
            {
                _selectionShape?.AppendRect(selX1, lineY, selX2 - selX1, _lineHeight, 0, 0);
                anyRect = true;
            }
        }

        _selectionShape?.Visible(anyRect);
    }

    private void DeleteSelection()
    {
        if (!HasSelection()) return;
        int start = Math.Min(_selectionStart, _cursorPos);
        int end = Math.Max(_selectionStart, _cursorPos);
        int len = end - start;
        DeleteRangeInRuns(start, len);
        _text = _text.Remove(start, len);
        _cursorPos = start;
        _selectionStart = -1;
        _selectionShape?.Visible(false);
        SplitLines();
    }

    private string GetSelectedText()
    {
        if (!HasSelection()) return "";
        int start = Math.Min(_selectionStart, _cursorPos);
        int end = Math.Max(_selectionStart, _cursorPos);
        return _text[start..end];
    }

    private void CopyToClipboard()
    {
        var sel = GetSelectedText();
        if (!string.IsNullOrEmpty(sel))
            Glfw.Glfw.glfwSetClipboardString(null, sel);
    }

    private void PasteFromClipboard()
    {
        var clip = Glfw.Glfw.glfwGetClipboardString(null);
        if (string.IsNullOrEmpty(clip)) return;
        DeleteSelection();
        InsertTextInRuns(_cursorPos, clip.Length);
        _text = _text.Insert(_cursorPos, clip);
        _cursorPos += clip.Length;
        SplitLines();
        EnsureCursorVisible();
        MarkDirty();
        TextChanged?.Invoke(_text);
    }

    // --- Auto-scroll during mouse selection ---

    private void StartAutoScroll()
    {
        var window = OwnerWindow;
        if (window == null) return;
        StopAutoScroll();
        RunAutoScroll(window);
    }

    private void RunAutoScroll(Window window)
    {
        window.Animator.Start(new Animation
        {
            Duration = 0.016f,
            Easing = Easings.Linear,
            Tag = _autoScrollTag,
            Apply = _ =>
            {
                if (!_mouseDown) return;

                bool scrolled = false;
                float viewportW = ViewportW();
                float viewportH = ViewportH();

                if (_lastMouseLx < Padding + AutoScrollMargin)
                {
                    _scrollX -= AutoScrollSpeed;
                    scrolled = true;
                }
                else if (_lastMouseLx > Padding + viewportW - AutoScrollMargin)
                {
                    _scrollX += AutoScrollSpeed;
                    scrolled = true;
                }

                if (_lastMouseLy < Padding + AutoScrollMargin)
                {
                    _scrollY -= AutoScrollSpeed;
                    scrolled = true;
                }
                else if (_lastMouseLy > Padding + viewportH - AutoScrollMargin)
                {
                    _scrollY += AutoScrollSpeed;
                    scrolled = true;
                }

                if (scrolled)
                {
                    ClampScroll();
                    _cursorPos = GetCharIndexAtPosition(_lastMouseLx, _lastMouseLy);
                    UpdateContent();
                    MarkDirty();
                }
            },
            OnComplete = () =>
            {
                if (_mouseDown)
                    RunAutoScroll(window);
            }
        });
    }

    private void StopAutoScroll()
    {
        var window = OwnerWindow;
        if (window != null && _autoScrollTag != null)
            window.Animator.Cancel(_autoScrollTag);
    }

    // --- Input ---

    public override bool OnMouseDown(int button, float x, float y)
    {
        if (!Enabled || button != 0) return false;

        WindowToLocal(x, y, out float lx, out float ly);

        // Check vertical scrollbar hit
        if (NeedsScrollbarV())
        {
            float trackX = Bounds.W - ScrollbarWidth - ScrollbarMargin * 2;
            if (lx >= trackX && ly >= Padding && ly <= Padding + ViewportH())
            {
                _scrollbarDragV = true;
                _scrollbarDragStart = ly;
                _scrollbarDragStartScroll = _scrollY;
                return true;
            }
        }

        // Check horizontal scrollbar hit
        if (NeedsScrollbarH())
        {
            float trackY = Bounds.H - ScrollbarWidth - ScrollbarMargin * 2;
            if (ly >= trackY && lx >= Padding && lx <= Padding + ViewportW())
            {
                _scrollbarDragH = true;
                _scrollbarDragStart = lx;
                _scrollbarDragStartScroll = _scrollX;
                return true;
            }
        }

        _cursorPos = GetCharIndexAtPosition(lx, ly);
        _selectionStart = _cursorPos;
        _mouseDown = true;
        _lastMouseLx = lx;
        _lastMouseLy = ly;
        _pendingFormat = null;
        _pendingColor = null;
        StartAutoScroll();
        UpdateCursorPosition();
        UpdateSelection();
        RestartBlink();
        FormatChanged?.Invoke(GetCurrentFormat());
        MarkDirty();
        return true;
    }

    public override bool OnMouseUp(int button, float x, float y)
    {
        if (button != 0) return false;
        _mouseDown = false;
        _scrollbarDragV = false;
        _scrollbarDragH = false;
        StopAutoScroll();
        if (_selectionStart == _cursorPos)
            _selectionStart = -1;
        return true;
    }

    private bool IsOverScrollbar(float lx, float ly)
    {
        if (NeedsScrollbarV())
        {
            float trackX = Bounds.W - ScrollbarWidth - ScrollbarMargin * 2;
            if (lx >= trackX && ly >= Padding && ly <= Padding + ViewportH())
                return true;
        }
        if (NeedsScrollbarH())
        {
            float trackY = Bounds.H - ScrollbarWidth - ScrollbarMargin * 2;
            if (ly >= trackY && lx >= Padding && lx <= Padding + ViewportW())
                return true;
        }
        return false;
    }

    public override void OnMouseMove(float x, float y)
    {
        WindowToLocal(x, y, out float lx, out float ly);

        Cursor = IsOverScrollbar(lx, ly) ? CursorType.Arrow : CursorType.IBeam;

        if (_scrollbarDragV)
        {
            float viewportH = ViewportH();
            float maxScroll = Math.Max(0, _totalContentHeight - viewportH);
            float ratio = viewportH / _totalContentHeight;
            float thumbH = Math.Max(20, viewportH * ratio);
            float trackH = viewportH - thumbH;

            if (trackH > 0)
            {
                float delta = ly - _scrollbarDragStart;
                _scrollY = _scrollbarDragStartScroll + delta * maxScroll / trackH;
                ClampScroll();
                UpdateContent();
                MarkDirty();
            }
            return;
        }

        if (_scrollbarDragH)
        {
            float viewportW = ViewportW();
            float maxScroll = Math.Max(0, _maxLineWidth - viewportW);
            float ratio = viewportW / _maxLineWidth;
            float thumbW = Math.Max(20, viewportW * ratio);
            float trackW = viewportW - thumbW;

            if (trackW > 0)
            {
                float delta = lx - _scrollbarDragStart;
                _scrollX = _scrollbarDragStartScroll + delta * maxScroll / trackW;
                ClampScroll();
                UpdateContent();
                MarkDirty();
            }
            return;
        }

        if (!_mouseDown) return;

        _lastMouseLx = lx;
        _lastMouseLy = ly;
        _cursorPos = GetCharIndexAtPosition(lx, ly);
        UpdateCursorPosition();
        UpdateSelection();
        RestartBlink();
        MarkDirty();
    }

    public override void OnScroll(float dx, float dy)
    {
        var gw = OwnerWindow?.GlfwWindow;
        bool shift = gw != null
            && (Glfw.Glfw.glfwGetKey(gw, GLFW_KEY_LEFT_SHIFT) == GLFW_PRESS
             || Glfw.Glfw.glfwGetKey(gw, GLFW_KEY_RIGHT_SHIFT) == GLFW_PRESS);
        if (shift)
        {
            dx = dy;
            dy = 0;
        }

        if (dy != 0)
            _scrollY -= dy * _lineHeight * 3;
        if (dx != 0)
            _scrollX -= dx * _effectiveFontSize * 3;

        ClampScroll();
        UpdateContent();
        MarkDirty();
    }

    public override void OnTextInput(string text)
    {
        if (!Enabled) return;

        DeleteSelection();
        InsertTextInRuns(_cursorPos, text.Length);
        _text = _text.Insert(_cursorPos, text);
        _cursorPos += text.Length;
        _selectionStart = -1;
        SplitLines();
        EnsureCursorVisible();
        RestartBlink();
        MarkDirty();
        TextChanged?.Invoke(_text);
    }

    public override void OnKeyDown(int key, int mods, bool repeat)
    {
        if (!Enabled) return;

        bool shift = (mods & GLFW_MOD_SHIFT) != 0;
        bool ctrl = (mods & GLFW_MOD_CONTROL) != 0;

        switch (key)
        {
            case GLFW_KEY_ENTER:
            case GLFW_KEY_KP_ENTER:
                DeleteSelection();
                InsertTextInRuns(_cursorPos, 1);
                _text = _text.Insert(_cursorPos, "\n");
                _cursorPos++;
                _selectionStart = -1;
                SplitLines();
                EnsureCursorVisible();
                RestartBlink();
                MarkDirty();
                TextChanged?.Invoke(_text);
                break;

            case GLFW_KEY_LEFT:
                if (shift && _selectionStart < 0) _selectionStart = _cursorPos;
                if (!shift && HasSelection())
                {
                    _cursorPos = Math.Min(_selectionStart, _cursorPos);
                    _selectionStart = -1;
                }
                else if (ctrl)
                    _cursorPos = FindWordBoundaryLeft(_cursorPos);
                else if (_cursorPos > 0)
                    _cursorPos--;
                if (!shift) _selectionStart = -1;
                _pendingFormat = null;
                _pendingColor = null;
                EnsureCursorVisible();
                RestartBlink();
                FormatChanged?.Invoke(GetCurrentFormat());
                MarkDirty();
                break;

            case GLFW_KEY_RIGHT:
                if (shift && _selectionStart < 0) _selectionStart = _cursorPos;
                if (!shift && HasSelection())
                {
                    _cursorPos = Math.Max(_selectionStart, _cursorPos);
                    _selectionStart = -1;
                }
                else if (ctrl)
                    _cursorPos = FindWordBoundaryRight(_cursorPos);
                else if (_cursorPos < _text.Length)
                    _cursorPos++;
                if (!shift) _selectionStart = -1;
                _pendingFormat = null;
                _pendingColor = null;
                EnsureCursorVisible();
                RestartBlink();
                FormatChanged?.Invoke(GetCurrentFormat());
                MarkDirty();
                break;

            case GLFW_KEY_UP:
            {
                if (shift && _selectionStart < 0) _selectionStart = _cursorPos;
                int line = GetLineAtCharIndex(_cursorPos);
                if (line > 0)
                {
                    int col = GetColumnInLine(_cursorPos, line);
                    col = Math.Min(col, _lines[line - 1].Length);
                    _cursorPos = _lineOffsets[line - 1] + col;
                }
                else
                {
                    _cursorPos = 0;
                }
                if (!shift) _selectionStart = -1;
                _pendingFormat = null;
                _pendingColor = null;
                EnsureCursorVisible();
                RestartBlink();
                FormatChanged?.Invoke(GetCurrentFormat());
                MarkDirty();
                break;
            }

            case GLFW_KEY_DOWN:
            {
                if (shift && _selectionStart < 0) _selectionStart = _cursorPos;
                int line = GetLineAtCharIndex(_cursorPos);
                if (line < _lines.Length - 1)
                {
                    int col = GetColumnInLine(_cursorPos, line);
                    col = Math.Min(col, _lines[line + 1].Length);
                    _cursorPos = _lineOffsets[line + 1] + col;
                }
                else
                {
                    _cursorPos = _text.Length;
                }
                if (!shift) _selectionStart = -1;
                _pendingFormat = null;
                _pendingColor = null;
                EnsureCursorVisible();
                RestartBlink();
                FormatChanged?.Invoke(GetCurrentFormat());
                MarkDirty();
                break;
            }

            case GLFW_KEY_HOME:
            {
                if (shift && _selectionStart < 0) _selectionStart = _cursorPos;
                if (ctrl)
                    _cursorPos = 0;
                else
                {
                    int line = GetLineAtCharIndex(_cursorPos);
                    _cursorPos = _lineOffsets[line];
                }
                if (!shift) _selectionStart = -1;
                _pendingFormat = null;
                _pendingColor = null;
                EnsureCursorVisible();
                FormatChanged?.Invoke(GetCurrentFormat());
                MarkDirty();
                break;
            }

            case GLFW_KEY_END:
            {
                if (shift && _selectionStart < 0) _selectionStart = _cursorPos;
                if (ctrl)
                    _cursorPos = _text.Length;
                else
                {
                    int line = GetLineAtCharIndex(_cursorPos);
                    _cursorPos = _lineOffsets[line] + _lines[line].Length;
                }
                if (!shift) _selectionStart = -1;
                _pendingFormat = null;
                _pendingColor = null;
                EnsureCursorVisible();
                FormatChanged?.Invoke(GetCurrentFormat());
                MarkDirty();
                break;
            }

            case GLFW_KEY_BACKSPACE:
                if (HasSelection())
                    DeleteSelection();
                else if (_cursorPos > 0)
                {
                    int count = ctrl ? _cursorPos - FindWordBoundaryLeft(_cursorPos) : 1;
                    int deleteStart = _cursorPos - count;
                    DeleteRangeInRuns(deleteStart, count);
                    _cursorPos = deleteStart;
                    _text = _text.Remove(_cursorPos, count);
                    SplitLines();
                }
                _pendingFormat = null;
                _pendingColor = null;
                EnsureCursorVisible();
                MarkDirty();
                TextChanged?.Invoke(_text);
                break;

            case GLFW_KEY_DELETE:
                if (HasSelection())
                    DeleteSelection();
                else if (_cursorPos < _text.Length)
                {
                    int count = ctrl ? FindWordBoundaryRight(_cursorPos) - _cursorPos : 1;
                    DeleteRangeInRuns(_cursorPos, count);
                    _text = _text.Remove(_cursorPos, count);
                    SplitLines();
                }
                _pendingFormat = null;
                _pendingColor = null;
                EnsureCursorVisible();
                MarkDirty();
                TextChanged?.Invoke(_text);
                break;

            case GLFW_KEY_A when ctrl:
                _selectionStart = 0;
                _cursorPos = _text.Length;
                UpdateCursorPosition();
                UpdateSelection();
                MarkDirty();
                break;

            case GLFW_KEY_C when ctrl:
                CopyToClipboard();
                break;

            case GLFW_KEY_X when ctrl:
                CopyToClipboard();
                DeleteSelection();
                SplitLines();
                EnsureCursorVisible();
                MarkDirty();
                TextChanged?.Invoke(_text);
                break;

            case GLFW_KEY_V when ctrl:
                PasteFromClipboard();
                break;

            case GLFW_KEY_B when ctrl:
                ToggleBold();
                break;

            case GLFW_KEY_I when ctrl:
                ToggleItalic();
                break;

            case GLFW_KEY_U when ctrl:
                ToggleUnderline();
                break;

            default:
                base.OnKeyDown(key, mods, repeat);
                break;
        }
    }

    // --- Word boundaries ---

    private int FindWordBoundaryLeft(int pos)
    {
        if (pos <= 0) return 0;
        pos--;
        while (pos > 0 && _text[pos] == '\n') pos--;
        while (pos > 0 && char.IsWhiteSpace(_text[pos]) && _text[pos] != '\n') pos--;
        while (pos > 0 && !char.IsWhiteSpace(_text[pos - 1])) pos--;
        return pos;
    }

    private int FindWordBoundaryRight(int pos)
    {
        if (pos >= _text.Length) return _text.Length;
        while (pos < _text.Length && _text[pos] == '\n') { pos++; return pos; }
        while (pos < _text.Length && !char.IsWhiteSpace(_text[pos])) pos++;
        while (pos < _text.Length && char.IsWhiteSpace(_text[pos]) && _text[pos] != '\n') pos++;
        return pos;
    }

    // --- Blink animation ---

    private void StartBlinkAnimation()
    {
        var window = OwnerWindow;
        if (window == null || _cursorShape == null) return;
        StopBlinkAnimation();
        AnimateBlink(window, true);
    }

    private void AnimateBlink(Window window, bool show)
    {
        _cursorShape!.Opacity(show ? (byte)255 : (byte)0);
        window.Animator.Start(new Animation
        {
            Duration = 0.53f,
            Easing = Easings.Linear,
            Tag = _blinkTag,
            Apply = _ => { },
            OnComplete = () =>
            {
                if ((State & ElementState.Focused) != 0)
                    AnimateBlink(window, !show);
            }
        });
    }

    private void RestartBlink()
    {
        StopBlinkAnimation();
        _cursorShape?.Opacity(255);
        StartBlinkAnimation();
    }

    private void StopBlinkAnimation()
    {
        var window = OwnerWindow;
        if (window != null && _blinkTag != null)
            window.Animator.Cancel(_blinkTag);
    }

    // --- Theme/Style ---

    protected override Style GetDefaultStyle()
    {
        return OwnerWindow?.Theme.TextBox ?? new Style();
    }

    protected override void OnThemeChanged()
    {
        if (!_shapesCreated) return;
        var style = Style;
        var theme = OwnerWindow?.Theme ?? Theme.Dark;

        _backgroundShape?.SetFill(style.Background.R8, style.Background.G8, style.Background.B8, style.Background.A8);
        _borderShape?.StrokeFill(style.Border.R8, style.Border.G8, style.Border.B8, style.Border.A8);
        _cursorShape?.SetFill(theme.CursorColor.R8, theme.CursorColor.G8, theme.CursorColor.B8, theme.CursorColor.A8);

        var selColor = theme.SelectionColor;
        _selectionShape?.SetFill(selColor.R8, selColor.G8, selColor.B8, selColor.A8);

        _placeholderPaint?.SetFill(theme.DisabledTextColor.R8, theme.DisabledTextColor.G8, theme.DisabledTextColor.B8);

        MarkDirty();
    }
}
