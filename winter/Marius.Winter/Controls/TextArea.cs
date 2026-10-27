using System;
using System.Numerics;
using ThorVG;
using static Glfw.GLFW;

namespace Marius.Winter;

public class TextArea : Element
{
    public bool ReadOnly { get; set; }
    private bool _invalid;
    /// <summary>Explicit validation state, independent of text contents.</summary>
    public bool Invalid
    {
        get => _invalid;
        set { if (_invalid == value) return; _invalid = value; UpdateBorderColor(); MarkDirty(); }
    }
    private const float Padding = 8f;
    private const float ScrollbarWidth = 8f;
    private const float ScrollbarMargin = 2f;
    private const float AutoScrollMargin = 16f;
    private const float AutoScrollSpeed = 4f;

    private Shape? _backgroundShape;
    private Shape? _borderShape;
    private Shape? _cursorShape;
    private Shape? _selectionShape;
    private Shape? _clipShape;
    private Text? _placeholderPaint;
    private Scene? _placeholderScene;
    private Scene? _contentScene;
    // Vertical scrollbar
    private Shape? _scrollTrackV;
    private Shape? _scrollThumbV;
    // Horizontal scrollbar
    private Shape? _scrollTrackH;
    private Shape? _scrollThumbH;

    private Text?[] _lineTexts = Array.Empty<Text?>();
    private string _text = "";
    private string _placeholder = "";
    private string[] _lines = { "" };
    private int[] _lineOffsets = { 0 };
    private readonly TextEditingBoundaries _boundaries = new();
    private TextEditingBoundaries Boundaries
    {
        get { _boundaries.SetText(_text); return _boundaries; }
    }
    private int _cursorPos;
    private int _selectionStart = -1;
    private bool _shapesCreated;
    private float _scrollX;
    private float _scrollY;
    private float _lineHeight;
    private float _totalContentHeight;
    private float _maxLineWidth;
    private float _effectiveFontSize;
    private object? _blinkTag;
    private object? _autoScrollTag;
    private bool _mouseDown;
    private readonly ScrollbarInteraction _scrollbars = new();
    internal override bool IsScrollbarGesture => _scrollbars.Pressed;
    private float _lastMouseLx;
    private float _lastMouseLy;
    private int _firstVisibleLine;
    private int _visibleLineCount;

    public Action<string>? TextChanged;
    /// <summary>User edits only; Text assignments continue to notify the legacy TextChanged delegate.</summary>
    public event Action<string>? UserTextChanged;
    public event Action<TextCommitEventArgs>? TextCommitted;
    private string _notifiedText = "";
    private bool _hasUncommittedEdit;

    private void NotifyUserEdit()
    {
        var text = _text;
        var changed = _notifiedText != text;
        _notifiedText = text;
        if (changed) _hasUncommittedEdit = true;
        TextChanged?.Invoke(text);
        if (changed) UserTextChanged?.Invoke(text);
    }

    /// <summary>Commits once per edited value; synchronization and repeated Enter/blur do not commit.</summary>
    public void CommitEdit(TextCommitReason reason)
    {
        if (!_hasUncommittedEdit) return;
        _hasUncommittedEdit = false;
        TextCommitted?.Invoke(new TextCommitEventArgs(_text, reason));
    }

    public TextArea(string text = "", string placeholder = "")
    {
        _text = text;
        _notifiedText = text;
        _placeholder = placeholder;
        _cursorPos = text.Length;
        Cursor = CursorType.IBeam;
        Focusable = true;
        SplitLines();
    }

    /// <summary>Intentional replacement that also clears an uncommitted edit when the text is unchanged.</summary>
    public void ResetText(string text)
    {
        Text = text;
        _hasUncommittedEdit = false;
        _notifiedText = _text;
    }

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _scrollbars.Cancel();
            _text = value;
            _notifiedText = value;
            _hasUncommittedEdit = false;
            _cursorPos = Boundaries.Floor(_cursorPos);
            _selectionStart = -1;
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

    private void SplitLines()
    {
        TextEditingBoundaries.SplitLines(_text, out _lines, out _lineOffsets);
    }

    private ScrollbarViewport ScrollViewport => ScrollbarViewport.Resolve(Bounds.W - 2 * Padding, Bounds.H - 2 * Padding, _maxLineWidth, _totalContentHeight);
    private bool NeedsScrollbarV() => ScrollViewport.Vertical;
    private bool NeedsScrollbarH() => ScrollViewport.Horizontal;
    private float ViewportW() => ScrollViewport.Width;
    private float ViewportH() => ScrollViewport.Height;
    private ScrollbarGeometry ScrollGeometry(bool horizontal)
    {
        float viewport = horizontal ? ViewportW() : ViewportH();
        return ScrollbarGeometry.Create(horizontal, horizontal ? Bounds.H - ScrollbarWidth - ScrollbarMargin : Bounds.W - ScrollbarWidth - ScrollbarMargin,
            Padding, viewport, viewport, horizontal ? _maxLineWidth : _totalContentHeight,
            horizontal ? _scrollX : _scrollY, horizontal ? NeedsScrollbarH() : NeedsScrollbarV());
    }

    protected override void OnAttached()
    {
        if (!_shapesCreated)
        {
            _shapesCreated = true;
            var style = Style;
            var theme = OwnerWindow?.Theme ?? Theme.Dark;
            _effectiveFontSize = EffectiveFontSize(style.FontSize);
            _lineHeight = _effectiveFontSize * 1.35f;
            _blinkTag = new object();
            _autoScrollTag = new object();

            _backgroundShape = Shape.Gen();
            _backgroundShape!.SetFill(style.Background.R8, style.Background.G8, style.Background.B8, style.Background.A8);
            AddPaint(_backgroundShape);

            _borderShape = Shape.Gen();
            _borderShape!.StrokeWidth(1f);
            _borderShape.StrokeFill(style.Border.R8, style.Border.G8, style.Border.B8, style.Border.A8);
            _borderShape.SetFill(0, 0, 0, 0);
            AddPaint(_borderShape);
            UpdateBorderColor();

            _contentScene = Scene.Gen()!;
            AddPaint(_contentScene);

            _clipShape = Shape.Gen();
            _clipShape!.SetFill(255, 255, 255, 255);
            _contentScene.Clip(_clipShape);

            _selectionShape = Shape.Gen();
            var selColor = theme.SelectionColor;
            _selectionShape!.SetFill(selColor.R8, selColor.G8, selColor.B8, selColor.A8);
            _selectionShape.Visible(false);
            _contentScene.Add(_selectionShape);

            _placeholderPaint = ThorVG.Text.Gen();
            _placeholderPaint!.SetFont(style.FontName);
            _placeholderPaint.SetFontSize(_effectiveFontSize);
            _placeholderPaint.SetText(_placeholder);
            _placeholderPaint.SetFill(theme.DisabledTextColor.R8, theme.DisabledTextColor.G8, theme.DisabledTextColor.B8);

            _placeholderScene = Scene.Gen()!;
            _placeholderScene.Add(_placeholderPaint);
            _placeholderScene.Visible(string.IsNullOrEmpty(_text));
            _contentScene.Add(_placeholderScene);

            _cursorShape = Shape.Gen();
            _cursorShape!.SetFill(theme.CursorColor.R8, theme.CursorColor.G8, theme.CursorColor.B8, theme.CursorColor.A8);
            _cursorShape.Visible(false);
            _contentScene.Add(_cursorShape);

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
        _scrollbars.Cancel();
        ClampScroll();
        float w = Bounds.W, h = Bounds.H;

        _backgroundShape?.ResetShape();
        _backgroundShape?.AppendRect(1, 2, w - 2, h - 2, 3, 3);

        _borderShape?.ResetShape();
        _borderShape?.AppendRect(0.5f, 0.5f, w - 1, h - 1, 2.5f, 2.5f);

        _clipShape?.ResetShape();
        _clipShape?.AppendRect(Padding, Padding - 1, ViewportW(), ViewportH() + 1, 0, 0);

        UpdateContent();
    }

    protected override void OnStateChanged(ElementState oldState, ElementState newState)
    {
        UpdateBorderColor();
        MarkDirty();
    }

    private void UpdateBorderColor()
    {
        var style = Style;
        if (_invalid)
            _borderShape?.StrokeFill(217, 51, 51, 255);
        else if ((State & ElementState.Focused) != 0)
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
        OnDragEnd();
        State = State & ~ElementState.Focused;
        _cursorShape?.Visible(false);
        _selectionShape?.Visible(false);
        _selectionStart = -1;
        _mouseDown = false;
        StopAutoScroll();
        StopBlinkAnimation();
        MarkDirty();
        CommitEdit(TextCommitReason.Blur);
    }

    // --- Content rendering ---

    private void UpdateContent()
    {
        if (!_shapesCreated || Bounds.W <= 0 || Bounds.H <= 0) return;

        _totalContentHeight = _lines.Length * _lineHeight;

        // Compute max line width for horizontal scrollbar
        _maxLineWidth = 0;
        for (int i = 0; i < _lines.Length; i++)
        {
            if (_lines[i].Length > 0)
            {
                float lw = MeasureTextWidth(_lines[i]);
                if (lw > _maxLineWidth) _maxLineWidth = lw;
            }
        }

        ClampScroll();

        _placeholderScene?.Visible(string.IsNullOrEmpty(_text));

        if (!string.IsNullOrEmpty(_placeholder))
        {
            MeasureLineBounds(_placeholder, out _, out float pby, out _, out _);
            _placeholderScene?.Translate(Padding - _scrollX, Padding - pby);
        }

        float viewportH = ViewportH();
        int firstVisible = Math.Max(0, (int)(_scrollY / _lineHeight));
        int lastVisible = Math.Min(_lines.Length - 1, (int)((_scrollY + viewportH) / _lineHeight));
        int visibleCount = lastVisible >= firstVisible ? lastVisible - firstVisible + 1 : 0;

        _firstVisibleLine = firstVisible;
        _visibleLineCount = visibleCount;

        EnsureLineTexts(visibleCount);

        for (int i = visibleCount; i < _lineTexts.Length; i++)
            _lineTexts[i]?.SetText("");

        for (int i = 0; i < visibleCount; i++)
        {
            int lineIdx = firstVisible + i;
            var t = _lineTexts[i];
            if (t == null) continue;

            t.SetText(_lines[lineIdx]);
            float y = lineIdx * _lineHeight - _scrollY;
            MeasureLineBounds(_lines[lineIdx].Length > 0 ? _lines[lineIdx] : "M", out _, out float by, out _, out _);
            t.Translate(Padding - _scrollX, Padding + y - by);
        }

        UpdateCursorPosition();
        UpdateSelection();
        UpdateScrollbars();
    }

    private void EnsureLineTexts(int count)
    {
        if (_lineTexts.Length >= count) return;

        var style = Style;
        var theme = OwnerWindow?.Theme ?? Theme.Dark;

        var old = _lineTexts;
        _lineTexts = new Text?[count];
        Array.Copy(old, _lineTexts, old.Length);

        for (int i = old.Length; i < count; i++)
        {
            var t = ThorVG.Text.Gen();
            if (t == null) continue;
            t.SetFont(style.FontName);
            t.SetFontSize(_effectiveFontSize);
            t.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);
            t.SetText("");
            _contentScene?.Add(t);
            _lineTexts[i] = t;
        }
    }

    private void UpdateScrollbars()
    {
        _clipShape?.ResetShape();
        _clipShape?.AppendRect(Padding, Padding - 1, ViewportW(), ViewportH() + 1, 0, 0);
        ScrollGeometry(false).Paint(this, _scrollTrackV, _scrollThumbV, _scrollbars.Hovered(false), _scrollbars.Axis == 1);
        ScrollGeometry(true).Paint(this, _scrollTrackH, _scrollThumbH, _scrollbars.Hovered(true), _scrollbars.Axis == 2);
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

    private float MeasureTextWidth(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var t = ThorVG.Text.Gen();
        if (t == null) return 0;
        try
        {
            t.SetFont(Style.FontName);
            t.SetFontSize(_effectiveFontSize);
            t.SetText(text);
            t.Bounds(out _, out _, out float bw, out _);
            return bw;
        }
        finally { t.Unref(); }
    }

    private void MeasureLineBounds(string text, out float bx, out float by, out float bw, out float bh)
    {
        bx = by = 0;
        bw = 0;
        bh = _effectiveFontSize;
        if (string.IsNullOrEmpty(text)) text = "M";
        var t = ThorVG.Text.Gen();
        if (t == null) return;
        try
        {
            t.SetFont(Style.FontName);
            t.SetFontSize(_effectiveFontSize);
            t.SetText(text);
            t.Bounds(out bx, out by, out bw, out bh);
            if (bh <= 0) bh = _effectiveFontSize;
        }
        finally { t.Unref(); }
    }

    private float GetCursorX()
    {
        int line = GetLineAtCharIndex(_cursorPos);
        int col = GetColumnInLine(_cursorPos, line);
        if (col <= 0) return 0;
        return MeasureTextWidth(_lines[line][..col]);
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
        float cursorX = col > 0 ? MeasureTextWidth(_lines[line][..col]) : 0;
        float cursorY = line * _lineHeight - _scrollY;

        MeasureLineBounds("M", out _, out _, out _, out float bh);
        _cursorShape.ResetShape();
        _cursorShape.AppendRect(Padding + cursorX - _scrollX, Padding + cursorY, 1f, bh, 0, 0);
    }

    private void EnsureCursorVisible()
    {
        _cursorPos = Boundaries.Floor(_cursorPos);
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

        float prevWidth = 0;
        int start = _lineOffsets[line];
        int end = start + lineText.Length;
        for (int pos = Boundaries.Next(start), previous = start; pos <= end && pos > previous;
             previous = pos, pos = Boundaries.Next(pos))
        {
            float width = MeasureTextWidth(lineText[..(pos - start)]);
            float midpoint = (prevWidth + width) / 2f;
            if (textX < midpoint) return previous;
            prevWidth = width;
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
                selX1 = Padding + (colStart > 0 ? MeasureTextWidth(_lines[line][..colStart]) : 0) - _scrollX;
            }
            else
            {
                selX1 = Padding - _scrollX;
            }

            if (line == endLine)
            {
                int colEnd = end - _lineOffsets[line];
                colEnd = Math.Min(colEnd, _lines[line].Length);
                selX2 = Padding + (colEnd > 0 ? MeasureTextWidth(_lines[line][..colEnd]) : 0) - _scrollX;
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
        if (ReadOnly || !IsEffectivelyEnabled) return;
        if (!HasSelection()) return;
        int start = Math.Min(_selectionStart, _cursorPos);
        int end = Math.Max(_selectionStart, _cursorPos);
        _text = _text.Remove(start, end - start);
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
        if (ReadOnly || !IsEffectivelyEnabled) return;
        var clip = Glfw.Glfw.glfwGetClipboardString(null);
        if (string.IsNullOrEmpty(clip)) return;
        DeleteSelection();
        _text = _text.Insert(_cursorPos, clip);
        _cursorPos = Boundaries.Ceiling(_cursorPos + clip.Length);
        _selectionStart = -1;
        SplitLines();
        EnsureCursorVisible();
        MarkDirty();
        NotifyUserEdit();
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
            Duration = 0.016f, // ~60fps
            Easing = Easings.Linear,
            Tag = _autoScrollTag,
            Apply = _ =>
            {
                if (!_mouseDown) return;

                bool scrolled = false;
                float viewportW = ViewportW();
                float viewportH = ViewportH();

                // Horizontal auto-scroll
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

                // Vertical auto-scroll
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

        _scrollbars.Cancel();
        var vertical = ScrollGeometry(false); var horizontal = ScrollGeometry(true);
        if (vertical.Contains(lx, ly) || horizontal.Contains(lx, ly))
        {
            StopAutoScroll(); _mouseDown = false;
            var geometry = vertical.Contains(lx, ly) ? vertical : horizontal;
            float value = _scrollbars.Begin(this, geometry, lx, ly);
            if (geometry.Horizontal) _scrollX = value; else _scrollY = value;
            Cursor = CursorType.Arrow;
            ClampScroll(); UpdateContent(); MarkDirty();
            return true;
        }

        _cursorPos = GetCharIndexAtPosition(lx, ly);
        _selectionStart = _cursorPos;
        _mouseDown = true;
        _lastMouseLx = lx;
        _lastMouseLy = ly;
        StartAutoScroll();
        UpdateCursorPosition();
        UpdateSelection();
        RestartBlink();
        MarkDirty();
        return true;
    }

    public override bool OnMouseUp(int button, float x, float y)
    {
        if (button != 0) return false;
        _mouseDown = false;
        _scrollbars.Cancel(); UpdateScrollbars(); MarkDirty();
        StopAutoScroll();
        if (_selectionStart == _cursorPos)
            _selectionStart = -1;
        return true;
    }

    private bool IsOverScrollbar(float lx, float ly) => ScrollGeometry(false).Contains(lx, ly) || ScrollGeometry(true).Contains(lx, ly);

    public override void OnDragEnd()
    {
        _scrollbars.Cancel(); _mouseDown = false; StopAutoScroll(); UpdateScrollbars(); MarkDirty();
    }
    public override void OnMouseLeave() { _scrollbars.Leave(); UpdateScrollbars(); MarkDirty(); }
    protected override void OnActivityChanged(bool active) { if (!active) OnDragEnd(); else { UpdateScrollbars(); MarkDirty(); } }

    public override void OnMouseMove(float x, float y)
    {
        WindowToLocal(x, y, out float lx, out float ly);

        // Toggle cursor: default arrow over scrollbar, IBeam over text
        Cursor = IsOverScrollbar(lx, ly) ? CursorType.Arrow : CursorType.IBeam;

        if (_scrollbars.Hover(lx, ly, ScrollGeometry(false), ScrollGeometry(true))) { UpdateScrollbars(); MarkDirty(); }
        if (_scrollbars.Pressed)
        {
            Cursor = CursorType.Arrow;
            int axis = _scrollbars.Axis;
            if (_scrollbars.Move(this, ScrollGeometry(false), ScrollGeometry(true), lx, ly, out var value))
            {
                if (axis == 2) _scrollX = value; else _scrollY = value;
                ClampScroll(); UpdateContent(); MarkDirty();
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
        // Shift+scroll → horizontal scroll
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
        if (!IsEffectivelyEnabled || ReadOnly) return;

        DeleteSelection();
        _text = _text.Insert(_cursorPos, text);
        _cursorPos = Boundaries.Ceiling(_cursorPos + text.Length);
        _selectionStart = -1;
        SplitLines();
        EnsureCursorVisible();
        RestartBlink();
        MarkDirty();
        NotifyUserEdit();
    }

    public override void OnKeyDown(int key, int mods, bool repeat)
    {
        if (!IsEffectivelyEnabled) return;

        bool shift = (mods & GLFW_MOD_SHIFT) != 0;
        bool ctrl = (mods & GLFW_MOD_CONTROL) != 0;

        if (ctrl && (key == GLFW_KEY_ENTER || key == GLFW_KEY_KP_ENTER))
        {
            if (!repeat) CommitEdit(TextCommitReason.Enter);
            return;
        }

        if (ReadOnly && (key == GLFW_KEY_BACKSPACE || key == GLFW_KEY_DELETE ||
            (ctrl && (key == GLFW_KEY_X || key == GLFW_KEY_V)) || key == GLFW_KEY_ENTER || key == GLFW_KEY_KP_ENTER)) return;

        switch (key)
        {
            case GLFW_KEY_ENTER:
            case GLFW_KEY_KP_ENTER:
                DeleteSelection();
                _text = _text.Insert(_cursorPos, "\n");
                _cursorPos = Boundaries.Next(_cursorPos);
                _selectionStart = -1;
                SplitLines();
                EnsureCursorVisible();
                RestartBlink();
                MarkDirty();
                NotifyUserEdit();
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
                EnsureCursorVisible();
                RestartBlink();
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
                EnsureCursorVisible();
                RestartBlink();
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
                EnsureCursorVisible();
                RestartBlink();
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
                EnsureCursorVisible();
                RestartBlink();
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
                EnsureCursorVisible();
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
                EnsureCursorVisible();
                MarkDirty();
                break;
            }

            case GLFW_KEY_BACKSPACE:
                if (HasSelection())
                    DeleteSelection();
                else if (_cursorPos > 0)
                {
                    int count = _cursorPos - (ctrl ? FindWordBoundaryLeft(_cursorPos) : Boundaries.Previous(_cursorPos));
                    _cursorPos -= count;
                    _text = _text.Remove(_cursorPos, count);
                    SplitLines();
                }
                EnsureCursorVisible();
                MarkDirty();
                NotifyUserEdit();
                break;

            case GLFW_KEY_DELETE:
                if (HasSelection())
                    DeleteSelection();
                else if (_cursorPos < _text.Length)
                {
                    int count = (ctrl ? FindWordBoundaryRight(_cursorPos) : Boundaries.Next(_cursorPos)) - _cursorPos;
                    _text = _text.Remove(_cursorPos, count);
                    SplitLines();
                }
                EnsureCursorVisible();
                MarkDirty();
                NotifyUserEdit();
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
                NotifyUserEdit();
                break;

            case GLFW_KEY_V when ctrl:
                PasteFromClipboard();
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

    protected override Style GetDefaultStyle()
    {
        return OwnerWindow?.Theme.TextBox ?? new Style();
    }

    protected override void OnThemeChanged()
    {
        _scrollbars.Cancel();
        if (!_shapesCreated) return;
        var style = Style;
        var theme = OwnerWindow?.Theme ?? Theme.Dark;

        _effectiveFontSize = EffectiveFontSize(style.FontSize);
        _lineHeight = _effectiveFontSize * 1.35f;
        _placeholderPaint?.SetFont(style.FontName);
        _placeholderPaint?.SetFontSize(_effectiveFontSize);

        _backgroundShape?.SetFill(style.Background.R8, style.Background.G8, style.Background.B8, style.Background.A8);
        UpdateBorderColor();
        _cursorShape?.SetFill(theme.CursorColor.R8, theme.CursorColor.G8, theme.CursorColor.B8, theme.CursorColor.A8);

        var selColor = theme.SelectionColor;
        _selectionShape?.SetFill(selColor.R8, selColor.G8, selColor.B8, selColor.A8);

        foreach (var t in _lineTexts)
        {
            t?.SetFont(style.FontName);
            t?.SetFontSize(_effectiveFontSize);
            t?.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);
        }

        _placeholderPaint?.SetFill(theme.DisabledTextColor.R8, theme.DisabledTextColor.G8, theme.DisabledTextColor.B8);

        UpdateContent();

        MarkDirty();
    }

    public override void OnBuildContextMenu(Menu menu)
    {
        if (HasSelection())
        {
            if (!ReadOnly) menu.AddItem("Cut", () =>
            {
                if (ReadOnly || !IsEffectivelyEnabled) return;
                CopyToClipboard();
                DeleteSelection();
                SplitLines();
                EnsureCursorVisible();
                MarkDirty();
                NotifyUserEdit();
            });
            menu.AddItem("Copy", CopyToClipboard);
        }
        if (!ReadOnly) menu.AddItem("Paste", PasteFromClipboard);
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
