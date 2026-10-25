using System;
using System.Collections.Generic;
using System.Threading;
using System.Numerics;

using ThorVG;
using static Glfw.GLFW;

namespace Marius.Winter;

/// <summary>
/// A multiline, read-only, selectable log with horizontal and vertical scrolling.
/// Supports pointer/keyboard selection, copy, scrollbars and drag autoscrolling.
/// </summary>
public class LogView : Element
{
    private readonly LogBuffer _localBuffer = new();
    private LogBuffer? _buffer;
    private Subscription? _subscription;
    private LogSnapshot? _snapshot;
    private string _compatibilityText = "";
    private bool _followTail = true;

    /// <summary>Caller-owned buffer, authoritative over Text while supplied.</summary>
    public LogBuffer? Buffer
    {
        get => _buffer;
        set
        {
            if (ReferenceEquals(_buffer, value)) return;
            Unsubscribe();
            _buffer = value;
            if (value == null) _localBuffer.Replace(_compatibilityText, trim: false);
            Subscribe();
            ApplySnapshot(CurrentBuffer.Snapshot, true);
        }
    }

    private LogBuffer CurrentBuffer => _buffer ?? _localBuffer;
    public bool FollowTail { get => _followTail; set => SetFollowTail(value, false); }
    public Action<bool>? FollowTailChanged;
    public int SelectionStart => _selAnchor < 0 ? 0 : Math.Min(_selAnchor, _selEnd);
    public int SelectionLength => _selAnchor < 0 ? 0 : Math.Abs(_selAnchor - _selEnd);
    public string SelectedText => GetSelectedText();
    public float ScrollOffset => _scrollY;
    public float HorizontalScrollOffset => _scrollX;

    /// <summary>UI-thread convenience. Worker producers should append to Buffer.</summary>
    public void Append(string text)
    {
        OwnerWindow?.Dispatcher.VerifyAccess();
        CurrentBuffer.Append(text);
        ApplySnapshot(CurrentBuffer.Snapshot);
    }

    public void Clear()
    {
        OwnerWindow?.Dispatcher.VerifyAccess();
        CurrentBuffer.Clear();
        ApplySnapshot(CurrentBuffer.Snapshot);
    }

    public void JumpToLatest() => SetFollowTail(true, true);

    private void SetFollowTail(bool value, bool user)
    {
        bool changed = _followTail != value;
        _followTail = value;
        if (value) { _scrollY = float.MaxValue; ClampScroll(); UpdateContent(); UpdateScrollbar(); MarkDirty(); }
        if (changed && user) FollowTailChanged?.Invoke(value);
    }

    private void Subscribe()
    {
        if (OwnerWindow is { } window && _subscription == null)
            _subscription = new Subscription(this, CurrentBuffer, window);
    }
    private void Unsubscribe() { _subscription?.Dispose(); _subscription = null; }
    protected override void OnDetaching()
    {
        StopAutoScroll();
        Unsubscribe();
        // Appends can add glyph paints after the last canvas update. Release
        // these owned paints before parent removal asks the rendered scene for
        // damage bounds; unprepared text has no renderer bounds yet.
        RebuildLineTexts();
        _mouseDown = _scrollbarDrag = false;
        base.OnDetaching();
    }
    protected override void OnDisposed() { StopAutoScroll(); Unsubscribe(); FollowTailChanged = null; }
    protected override void OnActivityChanged(bool active)
    {
        if (!active) { _mouseDown = _scrollbarDrag = false; StopAutoScroll(); }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly LogView _view;
        private readonly LogBuffer _source;
        private readonly Window _window;
        private int _queued, _disposed;
        internal Subscription(LogView view, LogBuffer source, Window window)
        { _view = view; _source = source; _window = window; source.Changed += Changed; }
        private void Changed()
        {
            if (Volatile.Read(ref _disposed) != 0) return;
            if (_window.Dispatcher.CheckAccess()) { Apply(); return; }
            if (Interlocked.Exchange(ref _queued, 1) != 0) return;
            _ = _window.Dispatcher.InvokeAsync(() => { Interlocked.Exchange(ref _queued, 0); Apply(); });
        }
        private void Apply()
        {
            if (Volatile.Read(ref _disposed) == 0 && ReferenceEquals(_view._subscription, this) && !_view.IsDisposed)
                _view.ApplySnapshot(_source.Snapshot);
        }
        public void Dispose() { Interlocked.Exchange(ref _disposed, 1); _source.Changed -= Changed; }
    }

    private void ApplySnapshot(LogSnapshot snapshot, bool replace = false)
    {
        if (!replace && _snapshot?.Version == snapshot.Version) return;
        bool reset = replace || _snapshot == null || _snapshot.ResetId != snapshot.ResetId;
        long removed = reset ? 0 : snapshot.StartOffset - _snapshot!.StartOffset;
        if (reset)
        {
            _selAnchor = _selEnd = -1; _scrollX = _scrollY = 0;
            _mouseDown = _scrollbarDrag = false; StopAutoScroll();
        }
        else if (removed > 0)
        {
            int count = (int)Math.Min(removed, _text.Length);
            int lines = 0;
            for (int i = 0; i < count; i++) if (_text[i] == '\n') lines++;
            _scrollY = Math.Max(0, _scrollY - lines * _lineHeight);
            if (_selAnchor >= 0)
            {
                _selAnchor = (int)Math.Clamp(_selAnchor - removed, 0, snapshot.Text.Length);
                _selEnd = (int)Math.Clamp(_selEnd - removed, 0, snapshot.Text.Length);
            }
        }
        _snapshot = snapshot;
        _text = snapshot.Text;
        _boundaries.SetText(_text);
        if (_selAnchor >= 0)
        {
            // An append may complete the final combining/emoji grapheme.
            // Retain the selected range while keeping both endpoints legal.
            if (_selAnchor == _selEnd) _selAnchor = _selEnd = _boundaries.Ceiling(_selEnd);
            else if (_selAnchor < _selEnd)
            { _selAnchor = _boundaries.Floor(_selAnchor); _selEnd = _boundaries.Ceiling(_selEnd); }
            else
            { _selEnd = _boundaries.Floor(_selEnd); _selAnchor = _boundaries.Ceiling(_selAnchor); }
        }
        RebuildLines();
        _totalContentHeight = _lines.Length * _lineHeight;
        if (_followTail) _scrollY = float.MaxValue;
        ClampScroll();
        UpdateContent();
        UpdateScrollbar();
        MarkDirty();
    }
    private Shape? _backgroundShape;
    private Shape? _clipShape;
    private Shape? _selectionShape;
    private Shape? _scrollTrack;
    private Shape? _scrollThumb;
    private Shape? _horizontalTrack;
    private Shape? _horizontalThumb;
    private Scene? _contentScene;

    private string _text = "";
    private string[] _lines = Array.Empty<string>();
    private int[] _lineOffsets = Array.Empty<int>(); // char offset of each line start in _text
    private readonly TextEditingBoundaries _boundaries = new();
    private LogTextLayout[] _lineLayouts = Array.Empty<LogTextLayout>();
    private bool _lineLayoutsValid;
    private Dictionary<string, LogTextLayout>? _previousLayouts;
    private float _totalContentWidth, _baselineY;
    private float _viewportW, _viewportH;
    private bool _horizontalNeeded, _verticalNeeded;

    private float _fontSize = 14f;
    private float _effectiveFontSize;
    private Color4? _color;
    private bool _colorSet;
    private bool _fontSizeSet;

    private float _lineHeight = 18.9f;
    private float _scrollY;
    private float _scrollX;
    private float _totalContentHeight;
    private bool _shapesCreated;

    // Selection: character indices into _text
    private int _selAnchor = -1;
    private int _selEnd = -1;
    private bool _mouseDown;
    private bool _scrollbarDrag;
    private float _scrollbarDragStartY;
    private float _scrollbarDragStartScroll;
    private bool _horizontalDrag;
    private float _lastMouseX, _lastMouseY;
    private readonly object _autoScrollTag = new();

    // Visible line rendering
    private Text?[] _lineTexts = Array.Empty<Text?>();
    private int _firstVisibleLine;
    private int _visibleLineCount;

    private string _fontName = "monospace";

    private const float Padding = 8f;
    private const float ScrollbarWidth = 8f;
    private const float ScrollbarMargin = 2f;

    public LogView()
    {
        Cursor = CursorType.IBeam;
        Focusable = true;
    }

    public string Text
    {
        get => _text;
        set
        {
            _compatibilityText = value ?? "";
            if (_buffer != null || _text == _compatibilityText) return;
            _localBuffer.Replace(_compatibilityText, trim: false);
            ApplySnapshot(_localBuffer.Snapshot);
        }
    }

    public float FontSize
    {
        get => _fontSize;
        set
        {
            _fontSizeSet = true;
            if (_fontSize == value) return;
            float oldHeight = _lineHeight;
            _fontSize = value;
            _effectiveFontSize = EffectiveFontSize(value);
            _lineHeight = _effectiveFontSize * 1.35f;
            InvalidateLineLayouts();
            _scrollY = oldHeight > 0 ? _scrollY / oldHeight * _lineHeight : 0;
            _totalContentHeight = _lines.Length * _lineHeight;
            if (_followTail) _scrollY = float.MaxValue;
            ClampScroll();
            if (_shapesCreated)
            {
                RebuildLineTexts();
                UpdateContent();
                UpdateScrollbar();
            }
            InvalidateMeasure();
            MarkDirty();
        }
    }

    /// <summary>Resume inherited theme font sizing.</summary>
    public void ResetFontSize() { _fontSizeSet = false; if (_shapesCreated) OnThemeChanged(); }

    public string FontName
    {
        get => _fontName;
        set
        {
            if (_fontName == value) return;
            _fontName = value;
            InvalidateLineLayouts();
            ClampScroll();
            if (_shapesCreated)
            {
                RebuildLineTexts();
                UpdateContent();
                UpdateScrollbar();
            }
            InvalidateMeasure();
            MarkDirty();
        }
    }

    public Color4? Color
    {
        get => _color;
        set
        {
            _color = value;
            _colorSet = value.HasValue;
            if (_shapesCreated)
                UpdateTextColors();
            MarkDirty();
        }
    }

    private void RebuildLines()
    {
        Dictionary<string, LogTextLayout>? previous = null;
        if (_lineLayoutsValid)
        {
            previous = new(StringComparer.Ordinal);
            for (int i = 0; i < _lines.Length; i++) previous[_lines[i]] = _lineLayouts[i];
        }
        InvalidateLineLayouts();
        _previousLayouts = previous;
        if (string.IsNullOrEmpty(_text))
        {
            _lines = Array.Empty<string>();
            _lineOffsets = Array.Empty<int>();
            return;
        }

        TextEditingBoundaries.SplitLines(_text, out _lines, out _lineOffsets);
    }

    private void InvalidateLineLayouts()
    {
        _lineLayoutsValid = false;
        _lineLayouts = Array.Empty<LogTextLayout>();
        _previousLayouts = null;
    }

    private void EnsureLineLayouts()
    {
        if (_lineLayoutsValid) return;
        _lineLayouts = new LogTextLayout[_lines.Length];
        _totalContentWidth = 0;
        LogFontMeasure? measure = null;
        try
        {
            for (int i = 0; i < _lines.Length; i++)
            {
                if (_previousLayouts != null && _previousLayouts.TryGetValue(_lines[i], out var cached))
                    _lineLayouts[i] = cached;
                else
                {
                    measure ??= new LogFontMeasure(_fontName, _effectiveFontSize);
                    _baselineY = measure.BaselineY;
                    _lineLayouts[i] = new LogTextLayout(_lines[i], measure);
                }
                _totalContentWidth = Math.Max(_totalContentWidth, _lineLayouts[i].Width);
            }
        }
        finally { measure?.Dispose(); _previousLayouts = null; }
        _lineLayoutsValid = true;
    }

    protected override void OnAttached()
    {
        if (!_shapesCreated)
        {
            _shapesCreated = true;
            var style = Style;
            var theme = OwnerWindow?.Theme ?? Theme.Dark;

            if (!_fontSizeSet)
                _fontSize = style.FontSize;
            _effectiveFontSize = EffectiveFontSize(_fontSize);
            _lineHeight = _effectiveFontSize * 1.35f;

            // Background
            _backgroundShape = Shape.Gen();
            _backgroundShape!.SetFill(style.Background.R8, style.Background.G8, style.Background.B8, style.Background.A8);
            AddPaint(_backgroundShape);

            // Selection rectangles (rendered before text)
            _selectionShape = Shape.Gen();
            var selColor = theme.SelectionColor;
            _selectionShape!.SetFill(selColor.R8, selColor.G8, selColor.B8, selColor.A8);
            _selectionShape.Visible(false);
            AddPaint(_selectionShape);

            // Content scene (holds line texts, clipped to viewport)
            _contentScene = Scene.Gen()!;
            AddPaint(_contentScene);

            _clipShape = Shape.Gen();
            _clipShape!.SetFill(255, 255, 255, 255);
            _contentScene.Clip(_clipShape);

            // Scrollbar track
            _scrollTrack = Shape.Gen();
            _scrollTrack!.SetFill(128, 128, 128, 40);
            Scene.Add(_scrollTrack);

            // Scrollbar thumb
            _scrollThumb = Shape.Gen();
            _scrollThumb!.SetFill(128, 128, 128, 100);
            Scene.Add(_scrollThumb);
            _horizontalTrack = Shape.Gen();
            _horizontalTrack.SetFill(128, 128, 128, 40);
            Scene.Add(_horizontalTrack);
            _horizontalThumb = Shape.Gen();
            _horizontalThumb.SetFill(128, 128, 128, 100);
            Scene.Add(_horizontalThumb);

            RebuildLines();
            RebuildLineTexts();
            UpdateContent();
            UpdateScrollbar();
        }
        Subscribe();
        ApplySnapshot(CurrentBuffer.Snapshot);
        // An unchanged buffer version still needs glyphs after detachment.
        UpdateContent();
    }

    private void RebuildLineTexts()
    {
        // Remove old texts from content scene
        if (_contentScene != null)
        {
            foreach (var t in _lineTexts)
            {
                if (t != null)
                {
                    _contentScene.Remove(t);
                }
            }
        }
        _lineTexts = Array.Empty<Text?>();
    }

    private void EnsureLineTexts(int count)
    {
        if (_lineTexts.Length >= count) return;

        var style = Style;
        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        var color = _colorSet && _color.HasValue ? _color.Value : theme.TextColor;

        var old = _lineTexts;
        _lineTexts = new Text?[count];
        Array.Copy(old, _lineTexts, old.Length);

        for (int i = old.Length; i < count; i++)
        {
            var t = ThorVG.Text.Gen();
            if (t == null) continue;
            t.SetFont(_fontName);
            t.SetFontSize(_effectiveFontSize);
            t.SetFill(color.R8, color.G8, color.B8);
            t.SetText("");
            _contentScene?.Add(t);
            _lineTexts[i] = t;
        }
    }

    private void UpdateTextColors()
    {
        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        var color = _colorSet && _color.HasValue ? _color.Value : theme.TextColor;
        foreach (var t in _lineTexts)
            t?.SetFill(color.R8, color.G8, color.B8);
    }

    private void UpdateContent()
    {
        if (!_shapesCreated || Bounds.W <= 0 || Bounds.H <= 0) return;
        ClampScroll();
        _totalContentHeight = _lines.Length * _lineHeight;

        float viewportH = _viewportH;
        int firstVisible = Math.Max(0, (int)(_scrollY / _lineHeight));
        int lastVisible = Math.Min(_lines.Length - 1, (int)((_scrollY + viewportH) / _lineHeight));
        int visibleCount = lastVisible >= firstVisible ? lastVisible - firstVisible + 1 : 0;

        _firstVisibleLine = firstVisible;
        _visibleLineCount = visibleCount;

        EnsureLineTexts(visibleCount);

        // Hide excess line texts
        for (int i = visibleCount; i < _lineTexts.Length; i++)
            _lineTexts[i]?.SetText("");

        // Update visible line texts
        for (int i = 0; i < visibleCount; i++)
        {
            int lineIdx = firstVisible + i;
            var t = _lineTexts[i];
            if (t == null) continue;

            var layout = _lineLayouts[lineIdx];
            layout.VisibleRange(_scrollX, _scrollX + _viewportW, out int start, out int end, out float origin);
            string visible = _lines[lineIdx][start..end];
            if (t.GetText() != visible) t.SetText(visible);

            // Position: relative to content scene origin
            float y = lineIdx * _lineHeight - _scrollY;
            t.Translate(Padding + origin - _scrollX, Padding + y - _baselineY);
        }

        UpdateSelection();
    }

    private void MeasureLineBounds(string text, out float bx, out float by, out float bw, out float bh)
    {
        bx = by = 0;
        bw = 0;
        bh = _effectiveFontSize;
        if (string.IsNullOrEmpty(text)) return;
        var t = ThorVG.Text.Gen();
        if (t == null) return;
        try
        {
            t.SetFont(_fontName);
            t.SetFontSize(_effectiveFontSize);
            t.SetText(text);
            t.Bounds(out bx, out by, out bw, out bh);
            if (bh <= 0) bh = _effectiveFontSize;
        }
        finally { t.Unref(); }
    }

    private float MeasureTextWidth(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var t = ThorVG.Text.Gen();
        if (t == null) return 0;
        try
        {
            t.SetFont(_fontName);
            t.SetFontSize(_effectiveFontSize);
            t.SetText(text);
            t.Bounds(out float bx, out _, out float bw, out _);
            return bx + bw;
        }
        finally { t.Unref(); }
    }

    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        // Height is determined by parent layout (flex Grow), not content
        return new Vector2(availableWidth, 0);
    }

    protected override void OnSizeChanged()
    {
        _totalContentHeight = _lines.Length * _lineHeight;
        float w = Bounds.W, h = Bounds.H;
        _backgroundShape?.ResetShape();
        _backgroundShape?.AppendRect(0, 0, w, h, 3, 3);

        // Re-add scrollbar shapes to stay on top
        if (_scrollTrack != null)
        {
            BringPaintToFront(_scrollTrack);
        }
        if (_scrollThumb != null)
        {
            BringPaintToFront(_scrollThumb);
        }
        if (_horizontalTrack != null) BringPaintToFront(_horizontalTrack);
        if (_horizontalThumb != null) BringPaintToFront(_horizontalThumb);

        ClampScroll();
        if (_followTail) { _scrollY = float.MaxValue; ClampScroll(); }
        UpdateContent();
        UpdateScrollbar();
    }

    private void UpdateScrollbar()
    {
        PaintScrollbar(_scrollTrack, _scrollThumb, false, _verticalNeeded);
        PaintScrollbar(_horizontalTrack, _horizontalThumb, true, _horizontalNeeded);
    }

    private float ThumbSize(bool horizontal)
    {
        float viewport = horizontal ? _viewportW : _viewportH;
        float content = horizontal ? _totalContentWidth : _totalContentHeight;
        return Math.Min(viewport, Math.Max(20, content > 0 ? viewport * viewport / content : viewport));
    }

    private float ThumbPosition(bool horizontal)
    {
        float viewport = horizontal ? _viewportW : _viewportH;
        float content = horizontal ? _totalContentWidth : _totalContentHeight;
        float scroll = horizontal ? _scrollX : _scrollY;
        return Padding + (content > viewport ? scroll / (content - viewport) * (viewport - ThumbSize(horizontal)) : 0);
    }

    private void PaintScrollbar(Shape? track, Shape? thumb, bool horizontal, bool needed)
    {
        track?.Visible(needed); thumb?.Visible(needed);
        if (!needed) return;
        float cross = horizontal ? Bounds.H - Padding - ScrollbarWidth : Bounds.W - Padding - ScrollbarWidth;
        float viewport = horizontal ? _viewportW : _viewportH;
        float position = ThumbPosition(horizontal), size = ThumbSize(horizontal);
        track?.ResetShape(); thumb?.ResetShape();
        if (horizontal)
        {
            track?.AppendRect(Padding, cross, viewport, ScrollbarWidth, 4, 4);
            thumb?.AppendRect(position, cross, size, ScrollbarWidth, 4, 4);
        }
        else
        {
            track?.AppendRect(cross, Padding, ScrollbarWidth, viewport, 4, 4);
            thumb?.AppendRect(cross, position, ScrollbarWidth, size, 4, 4);
        }
    }

    private void ClampScroll()
    {
        if (_shapesCreated) EnsureLineLayouts();
        bool horizontal = false, vertical = false;
        // Reserving one bar can make the other necessary.
        for (int i = 0; i < 3; i++)
        {
            _viewportW = Math.Max(0, Bounds.W - 2 * Padding - (vertical ? ScrollbarWidth + ScrollbarMargin : 0));
            _viewportH = Math.Max(0, Bounds.H - 2 * Padding - (horizontal ? ScrollbarWidth + ScrollbarMargin : 0));
            horizontal = _totalContentWidth > _viewportW && _viewportW > 0;
            vertical = _totalContentHeight > _viewportH && _viewportH > 0;
        }
        _horizontalNeeded = horizontal; _verticalNeeded = vertical;
        _scrollX = Math.Clamp(_scrollX, 0, Math.Max(0, _totalContentWidth - _viewportW));
        _scrollY = Math.Clamp(_scrollY, 0, Math.Max(0, _totalContentHeight - _viewportH));
        _clipShape?.ResetShape();
        _clipShape?.AppendRect(Padding, Padding, _viewportW, _viewportH, 0, 0);
    }

    private void RefreshScroll()
    {
        ClampScroll(); UpdateContent(); UpdateScrollbar(); MarkDirty();
    }

    // --- Input handling ---

    public override void OnScroll(float dx, float dy)
    {
        if (!_horizontalNeeded && !_verticalNeeded) return;
        bool shift = OwnerWindow?.GlfwWindow is { } native &&
            (Glfw.Glfw.glfwGetKey(native, GLFW_KEY_LEFT_SHIFT) == GLFW_PRESS ||
             Glfw.Glfw.glfwGetKey(native, GLFW_KEY_RIGHT_SHIFT) == GLFW_PRESS);
        if (shift || (!_verticalNeeded && _horizontalNeeded)) dx += dy;
        else _scrollY -= dy * _lineHeight * 3;
        _scrollX -= dx * _lineHeight * 3;
        SetFollowTail(false, true);
        RefreshScroll();
    }

    private bool OverScrollbar(float x, float y, bool horizontal) => horizontal
        ? _horizontalNeeded && y >= Bounds.H - Padding - ScrollbarWidth && y <= Bounds.H - Padding && x >= Padding && x <= Padding + _viewportW
        : _verticalNeeded && x >= Bounds.W - Padding - ScrollbarWidth && x <= Bounds.W - Padding && y >= Padding && y <= Padding + _viewportH;

    public override bool OnMouseDown(int button, float x, float y)
    {
        if (button != 0) return false;

        WindowToLocal(x, y, out float lx, out float ly);

        // Check scrollbar
        bool horizontal = OverScrollbar(lx, ly, true);
        if (horizontal || OverScrollbar(lx, ly, false))
        {
            SetFollowTail(false, true);
            StopAutoScroll(); _mouseDown = false;
            float pointer = horizontal ? lx : ly;
            float position = ThumbPosition(horizontal), size = ThumbSize(horizontal);
            if (pointer < position || pointer > position + size)
            {
                float delta = pointer < position ? -1 : 1;
                if (horizontal) _scrollX += delta * _viewportW;
                else _scrollY += delta * _viewportH;
                RefreshScroll();
            }
            _scrollbarDrag = true;
            _horizontalDrag = horizontal;
            _scrollbarDragStartY = pointer;
            _scrollbarDragStartScroll = horizontal ? _scrollX : _scrollY;
            return true;
        }

        // Text selection
        SetFollowTail(false, true);
        int charIdx = GetCharIndexAtPosition(lx, ly);
        _selAnchor = charIdx;
        _selEnd = charIdx;
        _mouseDown = true;
        _lastMouseX = lx; _lastMouseY = ly;
        UpdateSelection();
        MarkDirty();
        return true;
    }

    public override bool OnMouseUp(int button, float x, float y)
    {
        if (button != 0) return false;
        _mouseDown = false;
        _scrollbarDrag = false;
        StopAutoScroll();
        return true;
    }

    public override void OnMouseMove(float x, float y)
    {
        WindowToLocal(x, y, out float lx, out float ly);

        // Change cursor: default arrow over scrollbar, IBeam over text
        Cursor = OverScrollbar(lx, ly, true) || OverScrollbar(lx, ly, false) ? CursorType.Arrow : CursorType.IBeam;

        if (_scrollbarDrag)
        {
            float viewport = _horizontalDrag ? _viewportW : _viewportH;
            float scrollRange = (_horizontalDrag ? _totalContentWidth : _totalContentHeight) - viewport;
            if (scrollRange <= 0) return;

            float trackH = viewport - ThumbSize(_horizontalDrag);
            if (trackH <= 0) return;

            float deltaY = (_horizontalDrag ? lx : ly) - _scrollbarDragStartY;
            float scroll = _scrollbarDragStartScroll + deltaY / trackH * scrollRange;
            if (_horizontalDrag) _scrollX = scroll; else _scrollY = scroll;
            RefreshScroll();
            return;
        }

        if (_mouseDown)
        {
            _lastMouseX = lx; _lastMouseY = ly;
            int charIdx = GetCharIndexAtPosition(lx, ly);
            _selEnd = charIdx;
            UpdateSelection();
            MarkDirty();
            StartAutoScroll();
        }
    }

    public override void OnKeyDown(int key, int mods, bool repeat)
    {
        bool ctrl = (mods & GLFW_MOD_CONTROL) != 0;
        bool shift = (mods & GLFW_MOD_SHIFT) != 0;

        switch (key)
        {
            case GLFW_KEY_A when ctrl:
                SetFollowTail(false, true);
                _selAnchor = 0;
                _selEnd = _text.Length;
                UpdateSelection();
                MarkDirty();
                break;

            case GLFW_KEY_C when ctrl:
                CopyToClipboard();
                break;

            case GLFW_KEY_HOME:
                SetFollowTail(false, true);
                MoveSelection(0, shift);
                _scrollX = _scrollY = 0;
                RefreshScroll();
                break;

            case GLFW_KEY_END:
                MoveSelection(_text.Length, shift);
                _scrollX = float.MaxValue;
                JumpToLatest();
                break;

            case GLFW_KEY_LEFT:
            case GLFW_KEY_RIGHT:
            {
                int position = _selEnd < 0 ? 0 : _selEnd;
                if (!shift && HasSelection()) position = key == GLFW_KEY_LEFT ? SelectionStart : SelectionStart + SelectionLength;
                else if (key == GLFW_KEY_LEFT) position = ctrl ? _boundaries.WordLeft(position) : _boundaries.Previous(position);
                else position = ctrl ? _boundaries.WordRight(position) : _boundaries.Next(position);
                SetFollowTail(false, true);
                MoveSelection(position, shift);
                EnsureSelectionVisible();
                break;
            }

            case GLFW_KEY_UP:
            case GLFW_KEY_DOWN:
            {
                int position = _selEnd < 0 ? 0 : _selEnd;
                int line = GetLineAtCharIndex(position);
                if (_lines.Length == 0) break;
                int target = Math.Clamp(line + (key == GLFW_KEY_UP ? -1 : 1), 0, _lines.Length - 1);
                float x = _lineLayouts[line].Advance(position - _lineOffsets[line]);
                SetFollowTail(false, true);
                MoveSelection(_lineOffsets[target] + _lineLayouts[target].HitTest(x), shift);
                EnsureSelectionVisible();
                break;
            }

            case GLFW_KEY_PAGE_UP:
            {
                SetFollowTail(false, true);
                _scrollY -= _viewportH;
                RefreshScroll();
                if (shift) SelectAtViewportEdge(false);
                break;
            }

            case GLFW_KEY_PAGE_DOWN:
            {
                SetFollowTail(false, true);
                _scrollY += _viewportH;
                RefreshScroll();
                if (shift) SelectAtViewportEdge(true);
                break;
            }

            default:
                base.OnKeyDown(key, mods, repeat);
                break;
        }
    }

    public override void OnFocus()
    {
        State = State | ElementState.Focused;
        MarkDirty();
    }

    public override void OnBlur()
    {
        State = State & ~ElementState.Focused;
        _mouseDown = false;
        _scrollbarDrag = false;
        StopAutoScroll();
        MarkDirty();
    }

    // --- Selection ---

    private bool HasSelection() => _selAnchor >= 0 && _selAnchor != _selEnd;

    private void MoveSelection(int position, bool extend)
    {
        if (!extend || _selAnchor < 0) _selAnchor = extend ? Math.Max(0, _selEnd) : position;
        _selEnd = position;
        UpdateSelection(); MarkDirty();
    }

    private void EnsureSelectionVisible()
    {
        if (_lines.Length == 0) return;
        int line = GetLineAtCharIndex(_selEnd);
        float x = _lineLayouts[line].Advance(_selEnd - _lineOffsets[line]);
        if (x < _scrollX) _scrollX = x;
        if (x > _scrollX + _viewportW) _scrollX = x - _viewportW;
        float y = line * _lineHeight;
        if (y < _scrollY) _scrollY = y;
        if (y + _lineHeight > _scrollY + _viewportH) _scrollY = y + _lineHeight - _viewportH;
        RefreshScroll();
    }

    private void SelectAtViewportEdge(bool bottom)
    {
        MoveSelection(GetCharIndexAtPosition(Padding + _viewportW, Padding + (bottom ? _viewportH - 1 : 0)), true);
        UpdateSelection(); MarkDirty();
    }

    private static float AutoScrollStep(float pointer, float viewport) => pointer < Padding
        ? -Math.Min(64, Padding - pointer + 8)
        : pointer > Padding + viewport ? Math.Min(64, pointer - Padding - viewport + 8) : 0;

    private void StartAutoScroll()
    {
        StopAutoScroll();
        if (!_mouseDown || !IsActive || OwnerWindow is not { } window) return;
        float dx = AutoScrollStep(_lastMouseX, _viewportW), dy = AutoScrollStep(_lastMouseY, _viewportH);
        if (dx == 0 && dy == 0) return;
        bool moved = false;
        window.Animator.Start(new Animation
        {
            Duration = .016f, Tag = _autoScrollTag,
            Apply = _ =>
            {
                if (!_mouseDown) return;
                float x = _scrollX, y = _scrollY;
                _scrollX += dx; _scrollY += dy; ClampScroll();
                moved = x != _scrollX || y != _scrollY;
                if (!moved) return;
                _selEnd = GetCharIndexAtPosition(_lastMouseX, _lastMouseY);
                RefreshScroll();
            },
            OnComplete = () => { if (moved) StartAutoScroll(); }
        });
    }

    private void StopAutoScroll() => OwnerWindow?.Animator.Cancel(_autoScrollTag);

    private void UpdateSelection()
    {
        if (!HasSelection() || _lines.Length == 0 || !_lineLayoutsValid)
        {
            _selectionShape?.Visible(false);
            return;
        }

        int start = Math.Min(_selAnchor, _selEnd);
        int end = Math.Max(_selAnchor, _selEnd);

        // Find which lines are selected
        int startLine = GetLineAtCharIndex(start);
        int endLine = GetLineAtCharIndex(end);

        _selectionShape?.ResetShape();
        bool anyRect = false;

        int first = Math.Max(startLine, (int)(_scrollY / _lineHeight));
        int last = Math.Min(endLine, Math.Min(_lines.Length - 1, (int)((_scrollY + _viewportH) / _lineHeight)));
        for (int line = first; line <= last; line++)
        {
            float lineY = Padding + line * _lineHeight - _scrollY;
            if (lineY + _lineHeight < Padding || lineY > Padding + _viewportH)
                continue;

            float selX1, selX2;

            if (line == startLine)
            {
                int colStart = start - _lineOffsets[line];
                selX1 = Padding + _lineLayouts[line].Advance(colStart) - _scrollX;
            }
            else
            {
                selX1 = Padding;
            }

            if (line == endLine)
            {
                int colEnd = end - _lineOffsets[line];
                colEnd = Math.Min(colEnd, _lines[line].Length);
                selX2 = Padding + _lineLayouts[line].Advance(colEnd) - _scrollX;
            }
            else
            {
                selX2 = Padding + _viewportW;
            }

            if (selX2 > selX1)
            {
                float top = Math.Max(Padding, lineY);
                float bottom = Math.Min(Padding + _viewportH, lineY + _lineHeight);
                float left = Math.Clamp(selX1, Padding, Padding + _viewportW);
                float right = Math.Clamp(selX2, left, Padding + _viewportW);
                _selectionShape?.AppendRect(left, top, right - left, Math.Max(0, bottom - top), 0, 0);
                anyRect = true;
            }
        }

        _selectionShape?.Visible(anyRect);
    }

    private int GetLineAtCharIndex(int charIdx)
    {
        int index = Array.BinarySearch(_lineOffsets, charIdx);
        return index >= 0 ? index : Math.Max(0, ~index - 1);
    }

    private int GetCharIndexAtPosition(float lx, float ly)
    {
        float textY = ly - Padding + _scrollY;
        int line = Math.Clamp((int)(textY / _lineHeight), 0, Math.Max(0, _lines.Length - 1));

        if (_lines.Length == 0) return 0;

        float textX = lx - Padding + _scrollX;
        if (textX <= 0) return _lineOffsets[line];

        EnsureLineLayouts();
        return _lineOffsets[line] + _lineLayouts[line].HitTest(textX);
    }

    private string GetSelectedText()
    {
        if (!HasSelection()) return "";
        int start = Math.Min(_selAnchor, _selEnd);
        int end = Math.Max(_selAnchor, _selEnd);
        start = Math.Clamp(start, 0, _text.Length);
        end = Math.Clamp(end, 0, _text.Length);
        return _text[start..end];
    }

    private void CopyToClipboard()
    {
        var sel = GetSelectedText();
        if (!string.IsNullOrEmpty(sel))
            Glfw.Glfw.glfwSetClipboardString(null, sel);
    }

    // --- Style ---

    protected override Style GetDefaultStyle()
    {
        return OwnerWindow?.Theme.TextBox ?? new Style();
    }

    protected override void OnThemeChanged()
    {
        if (!_shapesCreated) return;
        var style = Style;
        var theme = OwnerWindow?.Theme ?? Theme.Dark;

        float oldLineHeight = _lineHeight;
        if (!_fontSizeSet) _fontSize = style.FontSize;
        _effectiveFontSize = EffectiveFontSize(_fontSize);
        _lineHeight = _effectiveFontSize * 1.35f;
        if (_lineHeight != oldLineHeight)
        {
            InvalidateLineLayouts();
            _scrollY = oldLineHeight > 0 ? _scrollY / oldLineHeight * _lineHeight : 0;
            _totalContentHeight = _lines.Length * _lineHeight;
            RebuildLineTexts();
            if (_followTail) _scrollY = float.MaxValue;
            ClampScroll();
            UpdateContent();
            UpdateScrollbar();
            InvalidateMeasure();
        }

        _backgroundShape?.SetFill(style.Background.R8, style.Background.G8, style.Background.B8, style.Background.A8);

        var selColor = theme.SelectionColor;
        _selectionShape?.SetFill(selColor.R8, selColor.G8, selColor.B8, selColor.A8);

        if (!_colorSet)
            UpdateTextColors();

        MarkDirty();
    }

    public override void OnBuildContextMenu(Menu menu)
    {
        if (HasSelection())
            menu.AddItem("Copy", CopyToClipboard);
        if (_text.Length > 0)
        {
            if (menu.Items.Count > 0) menu.AddSeparator();
            menu.AddItem("Select All", () =>
            {
                SetFollowTail(false, true);
                _selAnchor = 0;
                _selEnd = _text.Length;
                UpdateSelection();
                MarkDirty();
            });
        }
    }
}
