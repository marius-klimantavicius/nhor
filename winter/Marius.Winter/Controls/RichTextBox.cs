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

public sealed record RichTextFormattingState(TextFormat Format, Color4? Color, bool MixedFormat, bool MixedColor,
    int SelectionStart, int SelectionLength, bool CanUndo, bool CanRedo);

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

    private readonly TextEditingBoundaries _boundaries = new();
    private TextEditingBoundaries Boundaries
    {
        get { _boundaries.SetText(_text); return _boundaries; }
    }
    private int _cursorPos;
    private int _selectionStart = -1;
    private TextFormat? _pendingFormat;
    private Color4? _pendingColor;
    private bool _plainPendingColor;

    private bool _shapesCreated;
    private float _scrollX;
    private float _scrollY;
    private float _lineHeight;
    private float _totalContentHeight;
    private float _maxLineWidth;
    private float _effectiveFontSize;
    private float _effectiveBoldFontSize;
    private float? _fontSize;
    private float _ascent;
    private float _descent;
    private float _regularAscent;
    private float _boldAscent;
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
    public event Action<string>? UserTextChanged;
    public event Action<RichDocument>? DocumentChanged;
    public event Action<RichDocument>? UserDocumentChanged;
    public event Action<RichTextFormattingState>? FormattingStateChanged;
    private readonly RichDocumentHistory _history;
    private long _revision;

    // --- Constructor ---

    public RichTextBox(string text = "", string placeholder = "")
    {
        _history = new RichDocumentHistory(new RichDocument(text));
        _text = text;
        _placeholder = placeholder;
        _cursorPos = text.Length;
        Cursor = CursorType.IBeam;
        Focusable = true;
        _runs.Add(new TextRun { Start = 0, Length = text.Length, Format = TextFormat.None, Color = null });
        SplitLines();
    }

    // --- Properties ---

    /// <summary>Logical editor font size; does not mutate the shared theme.</summary>
    public float FontSize
    {
        get => _fontSize ?? Style.FontSize;
        set
        {
            if (!float.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (_fontSize == value) return;
            _fontSize = value; OnThemeChanged(); InvalidateMeasure(); MarkDirty();
        }
    }
    public void ResetFontSize() { if (_fontSize == null) return; _fontSize = null; OnThemeChanged(); InvalidateMeasure(); MarkDirty(); }

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            ResetDocument(new RichDocument(value));
        }
    }

    /// <summary>Equal assignment preserves editing state. Unequal external assignment starts a new history baseline.</summary>
    public RichDocument Document
    {
        get => _history.Current;
        set { ArgumentNullException.ThrowIfNull(value); if (!Document.Equals(value)) ResetDocument(value); }
    }
    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;
    public int SelectionStart => HasSelection() ? Math.Min(_selectionStart, _cursorPos) : _cursorPos;
    public int SelectionLength => HasSelection() ? Math.Abs(_selectionStart - _cursorPos) : 0;
    public RichTextFormattingState FormattingState
    {
        get
        {
            var format = GetCurrentFormat(); var color = GetCurrentColor();
            bool mixedFormat = false, mixedColor = false;
            if (HasSelection())
            {
                bool first = true;
                foreach (var run in _runs)
                {
                    if (run.End <= SelectionStart || run.Start >= SelectionStart + SelectionLength) continue;
                    if (first) { format = run.Format; color = run.Color; first = false; }
                    else { mixedFormat |= format != run.Format; mixedColor |= !Nullable.Equals(color, run.Color); format &= run.Format; }
                }
            }
            return new(format, color, mixedFormat, mixedColor, SelectionStart, SelectionLength, CanUndo, CanRedo);
        }
    }

    public void Focus()
    {
        OwnerWindow?.SetFocus(this);
        UpdateSelection();
    }

    /// <summary>Select whole graphemes; a zero-length selection floors to a legal caret.</summary>
    public void Select(int start, int length)
    {
        if (start < 0 || length < 0 || start > _text.Length - length) throw new ArgumentOutOfRangeException(nameof(start));
        _cursorPos = length == 0 ? Boundaries.Floor(start) : Boundaries.Ceiling(start + length);
        _selectionStart = length == 0 ? -1 : Boundaries.Floor(start);
        _pendingFormat = null; _pendingColor = null; _plainPendingColor = false;
        EnsureCursorVisible(); RestartBlink(); NotifyFormatting(); MarkDirty();
    }

    /// <summary>Explicit reset, even for equal text: clear styles, pending style, selection and history.</summary>
    public void ResetText(string text) => ResetDocument(new RichDocument(text));

    public void ResetDocument(RichDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var previous = Document;
        _history.Reset(document); var revision = ++_revision;
        LoadDocument(document, clearSelection: true);
        if (previous.Text != document.Text) TextChanged?.Invoke(document.Text);
        if (revision != _revision) return;
        DocumentChanged?.Invoke(document);
        if (revision == _revision) NotifyFormatting();
    }

    public bool Undo() => MoveHistory(redo: false);
    public bool Redo() => MoveHistory(redo: true);
    private bool MoveHistory(bool redo)
    {
        var before = Document;
        if (!(redo ? _history.Redo() : _history.Undo())) return false;
        var document = Document; var revision = ++_revision;
        LoadDocument(document, clearSelection: false);
        Focus();
        PublishChange(before, document, revision, user: true);
        return true;
    }

    private void LoadDocument(RichDocument document, bool clearSelection)
    {
        _text = document.Text; _runs.Clear();
        foreach (var run in document.Runs)
            _runs.Add(new TextRun { Start = run.Start, Length = run.Length, Format = run.Style.Format, Color = run.Style.Color });
        if (_runs.Count == 0) _runs.Add(new TextRun());
        _cursorPos = Boundaries.Floor(_cursorPos);
        _selectionStart = clearSelection || _selectionStart < 0 ? -1 : Boundaries.Floor(_selectionStart);
        _pendingFormat = null; _pendingColor = null; _plainPendingColor = false;
        SplitLines(); EnsureCursorVisible(); UpdateContent(); MarkDirty();
    }

    private RichDocument Snapshot()
    {
        var runs = new List<RichTextRun>();
        foreach (var run in _runs) if (run.Length > 0) runs.Add(new(run.Start, run.Length, new(run.Format, run.Color)));
        return new RichDocument(_text, runs);
    }
    private void CompleteUserEdit() => CompleteChange(user: true);
    private void CompleteChange(bool user)
    {
        var before = Document; var document = Snapshot();
        if (!_history.Apply(document)) { NotifyFormatting(); return; }
        var revision = ++_revision;
        PublishChange(before, document, revision, user);
    }
    private void PublishChange(RichDocument before, RichDocument document, long revision, bool user)
    {
        if (before.Text != document.Text) TextChanged?.Invoke(document.Text);
        if (revision != _revision) return;
        DocumentChanged?.Invoke(document);
        if (revision != _revision) return;
        if (user) UserDocumentChanged?.Invoke(document);
        if (revision != _revision) return;
        if (user && before.Text != document.Text) UserTextChanged?.Invoke(document.Text);
        if (revision == _revision) NotifyFormatting();
    }
    private void NotifyFormatting()
    {
        FormatChanged?.Invoke(GetCurrentFormat());
        FormattingStateChanged?.Invoke(FormattingState);
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
        _ = Document.WithStyle(0, 0, new(TextFormat.None, color)); // Validate before changing editor state.
        Focus();
        if (!HasSelection())
        {
            _pendingColor = color; _plainPendingColor = false;
            NotifyFormatting();
            return;
        }
        ApplyStyleChange(SelectionStart, SelectionLength, style => style with { Color = color }, user: true);
    }

    public void ClearFormatting()
    {
        Focus();
        if (!HasSelection())
        {
            _pendingFormat = TextFormat.None; _pendingColor = null; _plainPendingColor = true;
            NotifyFormatting();
            return;
        }
        ApplyStyleChange(SelectionStart, SelectionLength, _ => default, user: true);
    }

    private void ApplyStyleChange(int start, int length, Func<RichTextStyle, RichTextStyle> transform, bool user)
    {
        RichDocument next;
        try { next = Document.TransformStyle(start, length, transform); }
        catch (ArgumentException) when (user) { return; } // Decline an edit beyond the canonical-run bound before mutation.
        if (Document.Equals(next)) return;
        LoadDocument(next, clearSelection: false);
        CompleteChange(user);
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
        if (_plainPendingColor) return null;
        if (_pendingColor.HasValue) return _pendingColor.Value;
        if (_runs.Count == 0) return null;
        int ri = FindRunIndex(_cursorPos > 0 ? _cursorPos - 1 : 0);
        if (ri >= 0 && ri < _runs.Count) return _runs[ri].Color;
        return null;
    }

    // --- Programmatic formatting ---

    public void SetFormat(int start, int length, TextFormat format)
    {
        if (start < 0 || length <= 0 || start > _text.Length - length) return;
        ApplyStyleChange(start, length, style => style with { Format = format }, user: false);
    }

    public void SetColor(int start, int length, Color4 color)
    {
        if (start < 0 || length <= 0 || start > _text.Length - length) return;
        ApplyStyleChange(start, length, style => style with { Color = color }, user: false);
    }

    // --- Lifecycle ---

    private void SplitLines()
    {
        TextEditingBoundaries.SplitLines(_text, out _lines, out _lineOffsets);
    }

    protected override void OnAttached()
    {
        if (!_shapesCreated)
        {
            _shapesCreated = true;
            var style = Style;
            var theme = OwnerWindow?.Theme ?? Theme.Dark;
            _effectiveFontSize = EffectiveFontSize(FontSize);
            _effectiveBoldFontSize = EffectiveBoldFontSize(FontSize);
            _blinkTag = new object();
            _autoScrollTag = new object();

            // Create cached measurement texts
            _measureText = ThorVG.Text.Gen()!;
            _measureText.SetFont(style.FontName);
            _measureText.SetFontSize(_effectiveFontSize);

            _measureTextBold = ThorVG.Text.Gen()!;
            _measureTextBold.SetFont("default-bold");
            _measureTextBold.SetFontSize(_effectiveBoldFontSize);
            UpdateFontMetrics();

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
        UpdateSelection();
        StartBlinkAnimation();
        MarkDirty();
    }

    public override void OnBlur()
    {
        State = State & ~ElementState.Focused;
        _cursorShape?.Visible(false);
        _selectionShape?.Visible(false);
        // Retain the logical selection for toolbar commands across focus changes.
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
        Focus();
        if (!HasSelection())
        {
            // Toggle pending format
            var cur = _pendingFormat ?? GetCurrentFormat();
            if ((cur & flag) != 0)
                _pendingFormat = cur & ~flag;
            else
                _pendingFormat = cur | flag;
            NotifyFormatting();
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

        ApplyStyleChange(start, end - start, style => style with
        {
            Format = allHaveFlag ? style.Format & ~flag : style.Format | flag
        }, user: true);
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

    private void UpdateFontMetrics()
    {
        _measureText!.GetMetrics(out var regular);
        _measureTextBold!.GetMetrics(out var bold);
        _regularAscent = regular.ascent;
        _boldAscent = bold.ascent;
        // Both fonts share a baseline; reserve room for either font's extents.
        _ascent = Math.Max(regular.ascent, bold.ascent);
        _descent = Math.Min(regular.descent, bold.descent);
        _lineHeight = (_ascent - _descent) * 1.35f;
        if (_lineHeight <= 0) _lineHeight = _effectiveFontSize * 1.35f;
    }

    private float LineLeading => (_lineHeight - (_ascent - _descent)) / 2f;

    private float LineBaseline(float lineY) => Padding + lineY + LineLeading + _ascent;

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

        ClampScroll();

        _placeholderScene?.Visible(string.IsNullOrEmpty(_text));

        if (!string.IsNullOrEmpty(_placeholder))
        {
            _placeholderScene?.Translate(Padding - _scrollX, LineBaseline(0) - _regularAscent);
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

            float baselineY = LineBaseline(lineY);

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

                // ThorVG's text origin is the font top, with the baseline at
                // its ascent. Glyph ink bounds (for example "M") are not an
                // origin: subtracting them shifts text above its selection cell.
                slot.TextPaint.Translate(Padding + x - _scrollX,
                    baselineY - (bold ? _boldAscent : _regularAscent));

                float segWidth = MeasureRunWidth(segText, bold);

                // Underline decoration
                if (underline)
                {
                    float ulY = baselineY - _descent * 0.5f;
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
                    float stY = baselineY - _ascent * 0.4f;
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
            try
            {
                t.SetFont(bold ? "default-bold" : Style.FontName);
                t.SetFontSize(bold ? _effectiveBoldFontSize : _effectiveFontSize);
                t.SetText(text);
                if (t.GetTextSize(out float fw, out _) == Result.Success)
                    return fw;
                return 0;
            }
            finally { t.Unref(); }
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
        try
        {
            t.SetFont(bold ? "default-bold" : Style.FontName);
            t.SetFontSize(bold ? _effectiveBoldFontSize : _effectiveFontSize);
            t.SetText(text);
            t.Bounds(out bx, out by, out bw, out bh);
            if (bh <= 0) bh = _effectiveFontSize;
        }
        finally { t.Unref(); }
    }

    private float EffectiveBoldFontSize(float requestedSize)
    {
        var t = ThorVG.Text.Gen();
        if (t == null) return requestedSize;
        try
        {
            t.SetFont("default-bold");
            t.SetFontSize(requestedSize);
            t.SetText("M");
            t.GetMetrics(out var m);
            float measuredHeight = m.ascent - m.descent;
            if (measuredHeight > 0)
                return requestedSize * requestedSize / measuredHeight;
            return requestedSize;
        }
        finally { t.Unref(); }
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

        _cursorShape.ResetShape();
        _cursorShape.AppendRect(Padding + cursorX - _scrollX, Padding + cursorY + LineLeading,
            1f, _ascent - _descent, 0, 0);
    }

    private void EnsureCursorVisible()
    {
        _cursorPos = Boundaries.Floor(_cursorPos);
        // Parameter synchronization can happen before attach or layout. A
        // negative provisional viewport must never manufacture a scroll offset.
        if (!_shapesCreated || _lineHeight <= 0 || Bounds.W <= 2 * Padding || Bounds.H <= 2 * Padding) return;
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
        int start = _lineOffsets[line];
        int end = start + lineText.Length;
        for (int pos = Boundaries.Next(start), previous = start; pos <= end && pos > previous;
             previous = pos, pos = Boundaries.Next(pos))
        {
            float x = GetXOffsetInLine(line, pos - start);
            float midpoint = (prevX + x) / 2f;
            if (textX < midpoint) return previous;
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
        // Normalize after the complete edit, preserving this offset for replacement.
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
        var gw = OwnerWindow?.GlfwWindow;
        if (gw == null) return;
        string? clip = Glfw.Glfw.glfwGetClipboardString(gw);
        if (!string.IsNullOrEmpty(clip)) ReplaceSelectionWithText(clip);
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
        _pendingColor = null; _plainPendingColor = false;
        StartAutoScroll();
        UpdateCursorPosition();
        UpdateSelection();
        RestartBlink();
        NotifyFormatting();
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
        NotifyFormatting();
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
        if (!Enabled || string.IsNullOrEmpty(text)) return;
        ReplaceSelectionWithText(text);
    }

    private void ReplaceSelectionWithText(string text)
    {
        int start = SelectionStart;
        var style = new RichTextStyle(_pendingFormat ?? GetFormatAtPos(start),
            _plainPendingColor ? null : _pendingColor ?? GetColorAtPos(start));
        RichDocument next;
        try { next = Document.Replace(start, SelectionLength, text, style); }
        catch (ArgumentException) { return; } // A bounded editor declines an edit beyond its documented limits.
        LoadDocument(next, clearSelection: true);
        _cursorPos = Boundaries.Ceiling(start + text.Length);
        EnsureCursorVisible(); RestartBlink(); MarkDirty();
        CompleteUserEdit();
    }

    public override void OnKeyDown(int key, int mods, bool repeat)
    {
        if (!Enabled) return;

        bool shift = (mods & GLFW_MOD_SHIFT) != 0;
        bool ctrl = (mods & GLFW_MOD_CONTROL) != 0;

        switch (key)
        {
            case GLFW_KEY_Z when ctrl:
                if (shift) Redo(); else Undo();
                break;
            case GLFW_KEY_Y when ctrl:
                Redo();
                break;
            case GLFW_KEY_ENTER:
            case GLFW_KEY_KP_ENTER:
                ReplaceSelectionWithText("\n");
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
                    _cursorPos = Boundaries.Previous(_cursorPos);
                if (!shift) _selectionStart = -1;
                _pendingFormat = null;
                _pendingColor = null; _plainPendingColor = false;
                EnsureCursorVisible();
                RestartBlink();
                NotifyFormatting();
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
                    _cursorPos = Boundaries.Next(_cursorPos);
                if (!shift) _selectionStart = -1;
                _pendingFormat = null;
                _pendingColor = null; _plainPendingColor = false;
                EnsureCursorVisible();
                RestartBlink();
                NotifyFormatting();
                MarkDirty();
                break;

            case GLFW_KEY_UP:
            {
                if (shift && _selectionStart < 0) _selectionStart = _cursorPos;
                int line = GetLineAtCharIndex(_cursorPos);
                if (line > 0)
                {
                    _cursorPos = Boundaries.InAdjacentLine(_cursorPos, _lineOffsets[line],
                        _lineOffsets[line - 1], _lines[line - 1].Length);
                }
                else
                {
                    _cursorPos = 0;
                }
                if (!shift) _selectionStart = -1;
                _pendingFormat = null;
                _pendingColor = null; _plainPendingColor = false;
                EnsureCursorVisible();
                RestartBlink();
                NotifyFormatting();
                MarkDirty();
                break;
            }

            case GLFW_KEY_DOWN:
            {
                if (shift && _selectionStart < 0) _selectionStart = _cursorPos;
                int line = GetLineAtCharIndex(_cursorPos);
                if (line < _lines.Length - 1)
                {
                    _cursorPos = Boundaries.InAdjacentLine(_cursorPos, _lineOffsets[line],
                        _lineOffsets[line + 1], _lines[line + 1].Length);
                }
                else
                {
                    _cursorPos = _text.Length;
                }
                if (!shift) _selectionStart = -1;
                _pendingFormat = null;
                _pendingColor = null; _plainPendingColor = false;
                EnsureCursorVisible();
                RestartBlink();
                NotifyFormatting();
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
                _pendingColor = null; _plainPendingColor = false;
                EnsureCursorVisible();
                NotifyFormatting();
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
                _pendingColor = null; _plainPendingColor = false;
                EnsureCursorVisible();
                NotifyFormatting();
                MarkDirty();
                break;
            }

            case GLFW_KEY_BACKSPACE:
                if (HasSelection())
                    DeleteSelection();
                else if (_cursorPos > 0)
                {
                    int count = _cursorPos - (ctrl ? FindWordBoundaryLeft(_cursorPos) : Boundaries.Previous(_cursorPos));
                    int deleteStart = _cursorPos - count;
                    DeleteRangeInRuns(deleteStart, count);
                    _cursorPos = deleteStart;
                    _text = _text.Remove(_cursorPos, count);
                    SplitLines();
                }
                _pendingFormat = null;
                _pendingColor = null; _plainPendingColor = false;
                EnsureCursorVisible();
                MarkDirty();
                CompleteUserEdit();
                break;

            case GLFW_KEY_DELETE:
                if (HasSelection())
                    DeleteSelection();
                else if (_cursorPos < _text.Length)
                {
                    int count = (ctrl ? FindWordBoundaryRight(_cursorPos) : Boundaries.Next(_cursorPos)) - _cursorPos;
                    DeleteRangeInRuns(_cursorPos, count);
                    _text = _text.Remove(_cursorPos, count);
                    SplitLines();
                }
                _pendingFormat = null;
                _pendingColor = null; _plainPendingColor = false;
                EnsureCursorVisible();
                MarkDirty();
                CompleteUserEdit();
                break;

            case GLFW_KEY_A when ctrl:
                Select(0, _text.Length);
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
                CompleteUserEdit();
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

    private int FindWordBoundaryLeft(int pos) => Boundaries.WordLeft(pos, stopAtNewline: true);

    private int FindWordBoundaryRight(int pos) => Boundaries.WordRight(pos, stopAtNewline: true);

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

    protected override void OnDisposed()
    {
        _measureText?.Unref();
        _measureTextBold?.Unref();
        _measureText = _measureTextBold = null;
    }

    protected override Style GetDefaultStyle()
    {
        return OwnerWindow?.Theme.TextBox ?? new Style();
    }

    protected override void OnThemeChanged()
    {
        if (!_shapesCreated) return;
        var style = Style;
        var theme = OwnerWindow?.Theme ?? Theme.Dark;

        _effectiveFontSize = EffectiveFontSize(FontSize);
        _effectiveBoldFontSize = EffectiveBoldFontSize(FontSize);
        _measureText!.SetFont(style.FontName);
        _measureText.SetFontSize(_effectiveFontSize);
        _measureTextBold!.SetFontSize(_effectiveBoldFontSize);
        UpdateFontMetrics();
        _placeholderPaint?.SetFont(style.FontName);
        _placeholderPaint?.SetFontSize(_effectiveFontSize);

        _backgroundShape?.SetFill(style.Background.R8, style.Background.G8, style.Background.B8, style.Background.A8);
        _borderShape?.StrokeFill(style.Border.R8, style.Border.G8, style.Border.B8, style.Border.A8);
        _cursorShape?.SetFill(theme.CursorColor.R8, theme.CursorColor.G8, theme.CursorColor.B8, theme.CursorColor.A8);

        var selColor = theme.SelectionColor;
        _selectionShape?.SetFill(selColor.R8, selColor.G8, selColor.B8, selColor.A8);

        _placeholderPaint?.SetFill(theme.DisabledTextColor.R8, theme.DisabledTextColor.G8, theme.DisabledTextColor.B8);

        UpdateContent();
        MarkDirty();
    }

    public override void OnBuildContextMenu(Menu menu)
    {
        if (HasSelection())
        {
            menu.AddItem("Cut", () =>
            {
                CopyToClipboard();
                DeleteSelection();
                SplitLines();
                EnsureCursorVisible();
                MarkDirty();
                CompleteUserEdit();
            });
            menu.AddItem("Copy", CopyToClipboard);
        }
        menu.AddItem("Paste", PasteFromClipboard);
        if (_text.Length > 0)
        {
            menu.AddSeparator();
            menu.AddItem("Select All", () =>
            {
                _selectionStart = 0;
                _cursorPos = _text.Length;
                UpdateCursorPosition();
                UpdateSelection();
                MarkDirty();
            });
        }
    }
}
