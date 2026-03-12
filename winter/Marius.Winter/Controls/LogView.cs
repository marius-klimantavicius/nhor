using System;
using System.Numerics;

using ThorVG;
using static Glfw.GLFW;

namespace Marius.Winter;

/// <summary>
/// A multiline, read-only, selectable text control with built-in vertical scrolling.
/// Designed for log display. Supports mouse selection, Ctrl+A, Ctrl+C, and mouse wheel scrolling.
/// </summary>
public class LogView : Element
{
    private Shape? _backgroundShape;
    private Shape? _clipShape;
    private Shape? _selectionShape;
    private Shape? _scrollTrack;
    private Shape? _scrollThumb;
    private Scene? _contentScene;

    private string _text = "";
    private string[] _lines = Array.Empty<string>();
    private int[] _lineOffsets = Array.Empty<int>(); // char offset of each line start in _text

    private float _fontSize = 14f;
    private float _effectiveFontSize;
    private Color4? _color;
    private bool _colorSet;
    private bool _fontSizeSet;

    private float _lineHeight;
    private float _scrollY;
    private float _totalContentHeight;
    private bool _shapesCreated;

    // Selection: character indices into _text
    private int _selAnchor = -1;
    private int _selEnd = -1;
    private bool _mouseDown;
    private bool _scrollbarDrag;
    private float _scrollbarDragStartY;
    private float _scrollbarDragStartScroll;

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
            if (_text == value) return;
            _text = value ?? "";
            RebuildLines();
            _selAnchor = -1;
            _selEnd = -1;
            if (_shapesCreated)
            {
                UpdateContent();
                UpdateScrollbar();
            }
            InvalidateMeasure();
            MarkDirty();
        }
    }

    public float FontSize
    {
        get => _fontSize;
        set
        {
            if (_fontSize == value) return;
            _fontSize = value;
            _fontSizeSet = true;
            _effectiveFontSize = EffectiveFontSize(value);
            _lineHeight = _effectiveFontSize * 1.35f;
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

    public string FontName
    {
        get => _fontName;
        set
        {
            if (_fontName == value) return;
            _fontName = value;
            if (_shapesCreated)
            {
                RebuildLineTexts();
                UpdateContent();
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
        if (string.IsNullOrEmpty(_text))
        {
            _lines = Array.Empty<string>();
            _lineOffsets = Array.Empty<int>();
            return;
        }

        var lines = _text.Split('\n');
        _lines = lines;
        _lineOffsets = new int[lines.Length];
        int offset = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            _lineOffsets[i] = offset;
            offset += lines[i].Length + 1; // +1 for the \n
        }
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
            Scene.Remove(_scrollTrack);
            Scene.Add(_scrollTrack);

            // Scrollbar thumb
            _scrollThumb = Shape.Gen();
            _scrollThumb!.SetFill(128, 128, 128, 100);
            Scene.Remove(_scrollThumb);
            Scene.Add(_scrollThumb);

            RebuildLines();
            RebuildLineTexts();
            UpdateContent();
            UpdateScrollbar();
        }
    }

    private void RebuildLineTexts()
    {
        // Remove old texts from content scene
        if (_contentScene != null)
        {
            foreach (var t in _lineTexts)
            {
                if (t != null)
                    _contentScene.Remove(t);
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

        _totalContentHeight = _lines.Length * _lineHeight;

        float viewportH = Bounds.H - 2 * Padding;
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

            t.SetText(_lines[lineIdx]);

            // Position: relative to content scene origin
            float y = lineIdx * _lineHeight - _scrollY;
            // Get baseline offset for this text
            MeasureLineBounds(_lines[lineIdx], out _, out float by, out _, out _);
            t.Translate(Padding, Padding + y - by);
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
        t.SetFont(_fontName);
        t.SetFontSize(_effectiveFontSize);
        t.SetText(text);
        t.Bounds(out bx, out by, out bw, out bh);
        if (bh <= 0) bh = _effectiveFontSize;
    }

    private float MeasureTextWidth(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var t = ThorVG.Text.Gen();
        if (t == null) return 0;
        t.SetFont(_fontName);
        t.SetFontSize(_effectiveFontSize);
        t.SetText(text);
        t.Bounds(out float bx, out _, out float bw, out _);
        return bx + bw;
    }

    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        // Height is determined by parent layout (flex Grow), not content
        return new Vector2(availableWidth, 0);
    }

    protected override void OnSizeChanged()
    {
        float w = Bounds.W, h = Bounds.H;
        _backgroundShape?.ResetShape();
        _backgroundShape?.AppendRect(0, 0, w, h, 3, 3);

        _clipShape?.ResetShape();
        _clipShape?.AppendRect(Padding, Padding, w - 2 * Padding, h - 2 * Padding, 0, 0);

        // Re-add scrollbar shapes to stay on top
        if (_scrollTrack != null)
        {
            Scene.Remove(_scrollTrack);
            Scene.Add(_scrollTrack);
        }
        if (_scrollThumb != null)
        {
            Scene.Remove(_scrollThumb);
            Scene.Add(_scrollThumb);
        }

        ClampScroll();
        UpdateContent();
        UpdateScrollbar();
    }

    private void UpdateScrollbar()
    {
        float viewportH = Bounds.H - 2 * Padding;
        bool needed = _totalContentHeight > viewportH && viewportH > 0;

        _scrollTrack?.Visible(needed);
        _scrollThumb?.Visible(needed);

        if (!needed) return;

        float trackX = Bounds.W - ScrollbarWidth - ScrollbarMargin;
        float trackY = Padding;
        float trackH = viewportH;

        _scrollTrack?.ResetShape();
        _scrollTrack?.AppendRect(trackX, trackY, ScrollbarWidth, trackH, ScrollbarWidth / 2, ScrollbarWidth / 2);

        float thumbRatio = viewportH / _totalContentHeight;
        float thumbH = MathF.Max(20f, trackH * thumbRatio);
        float scrollRange = _totalContentHeight - viewportH;
        float thumbY = trackY + (scrollRange > 0 ? (_scrollY / scrollRange) * (trackH - thumbH) : 0);

        _scrollThumb?.ResetShape();
        _scrollThumb?.AppendRect(trackX, thumbY, ScrollbarWidth, thumbH, ScrollbarWidth / 2, ScrollbarWidth / 2);
    }

    private void ClampScroll()
    {
        float viewportH = Bounds.H - 2 * Padding;
        float maxScroll = MathF.Max(0, _totalContentHeight - viewportH);
        _scrollY = Math.Clamp(_scrollY, 0, maxScroll);
    }

    // --- Input handling ---

    public override void OnScroll(float dx, float dy)
    {
        float viewportH = Bounds.H - 2 * Padding;
        if (_totalContentHeight <= viewportH) return;

        _scrollY -= dy * _lineHeight * 3;
        ClampScroll();
        UpdateContent();
        UpdateScrollbar();
        MarkDirty();
    }

    public override bool OnMouseDown(int button, float x, float y)
    {
        if (button != 0) return false;

        WindowToLocal(x, y, out float lx, out float ly);

        // Check scrollbar
        float trackX = Bounds.W - ScrollbarWidth - ScrollbarMargin * 2;
        if (lx >= trackX)
        {
            _scrollbarDrag = true;
            _scrollbarDragStartY = ly;
            _scrollbarDragStartScroll = _scrollY;
            return true;
        }

        // Text selection
        int charIdx = GetCharIndexAtPosition(lx, ly);
        _selAnchor = charIdx;
        _selEnd = charIdx;
        _mouseDown = true;
        UpdateSelection();
        MarkDirty();
        return true;
    }

    public override bool OnMouseUp(int button, float x, float y)
    {
        if (button != 0) return false;
        _mouseDown = false;
        _scrollbarDrag = false;
        return true;
    }

    public override void OnMouseMove(float x, float y)
    {
        WindowToLocal(x, y, out float lx, out float ly);

        // Change cursor: default arrow over scrollbar, IBeam over text
        float trackX = Bounds.W - ScrollbarWidth - ScrollbarMargin * 2;
        Cursor = lx >= trackX ? CursorType.Arrow : CursorType.IBeam;

        if (_scrollbarDrag)
        {
            float viewportH = Bounds.H - 2 * Padding;
            float scrollRange = _totalContentHeight - viewportH;
            if (scrollRange <= 0) return;

            float thumbRatio = viewportH / _totalContentHeight;
            float thumbH = MathF.Max(20f, viewportH * thumbRatio);
            float trackH = viewportH - thumbH;
            if (trackH <= 0) return;

            float deltaY = ly - _scrollbarDragStartY;
            _scrollY = _scrollbarDragStartScroll + (deltaY / trackH) * scrollRange;
            ClampScroll();
            UpdateContent();
            UpdateScrollbar();
            MarkDirty();
            return;
        }

        if (_mouseDown)
        {
            int charIdx = GetCharIndexAtPosition(lx, ly);
            _selEnd = charIdx;
            UpdateSelection();
            MarkDirty();
        }
    }

    public override void OnKeyDown(int key, int mods, bool repeat)
    {
        bool ctrl = (mods & GLFW_MOD_CONTROL) != 0;

        switch (key)
        {
            case GLFW_KEY_A when ctrl:
                _selAnchor = 0;
                _selEnd = _text.Length;
                UpdateSelection();
                MarkDirty();
                break;

            case GLFW_KEY_C when ctrl:
                CopyToClipboard();
                break;

            case GLFW_KEY_HOME:
                _scrollY = 0;
                ClampScroll();
                UpdateContent();
                UpdateScrollbar();
                MarkDirty();
                break;

            case GLFW_KEY_END:
                _scrollY = float.MaxValue;
                ClampScroll();
                UpdateContent();
                UpdateScrollbar();
                MarkDirty();
                break;

            case GLFW_KEY_PAGE_UP:
            {
                float viewportH = Bounds.H - 2 * Padding;
                _scrollY -= viewportH;
                ClampScroll();
                UpdateContent();
                UpdateScrollbar();
                MarkDirty();
                break;
            }

            case GLFW_KEY_PAGE_DOWN:
            {
                float viewportH = Bounds.H - 2 * Padding;
                _scrollY += viewportH;
                ClampScroll();
                UpdateContent();
                UpdateScrollbar();
                MarkDirty();
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
        MarkDirty();
    }

    // --- Selection ---

    private bool HasSelection() => _selAnchor >= 0 && _selAnchor != _selEnd;

    private void UpdateSelection()
    {
        if (!HasSelection())
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

        float viewportH = Bounds.H - 2 * Padding;
        float contentWidth = Bounds.W - 2 * Padding - ScrollbarWidth - ScrollbarMargin;

        for (int line = startLine; line <= endLine; line++)
        {
            float lineY = Padding + line * _lineHeight - _scrollY;
            if (lineY + _lineHeight < Padding || lineY > Bounds.H - Padding)
                continue;

            float selX1, selX2;

            if (line == startLine)
            {
                int colStart = start - _lineOffsets[line];
                selX1 = Padding + MeasureTextWidth(_lines[line][..colStart]);
            }
            else
            {
                selX1 = Padding;
            }

            if (line == endLine)
            {
                int colEnd = end - _lineOffsets[line];
                colEnd = Math.Min(colEnd, _lines[line].Length);
                selX2 = Padding + MeasureTextWidth(_lines[line][..colEnd]);
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

    private int GetLineAtCharIndex(int charIdx)
    {
        for (int i = _lineOffsets.Length - 1; i >= 0; i--)
        {
            if (charIdx >= _lineOffsets[i])
                return i;
        }
        return 0;
    }

    private int GetCharIndexAtPosition(float lx, float ly)
    {
        float textY = ly - Padding + _scrollY;
        int line = Math.Clamp((int)(textY / _lineHeight), 0, Math.Max(0, _lines.Length - 1));

        if (_lines.Length == 0) return 0;

        float textX = lx - Padding;
        if (textX <= 0) return _lineOffsets[line];

        string lineText = _lines[line];
        if (string.IsNullOrEmpty(lineText)) return _lineOffsets[line];

        // Binary-ish search for character position
        float prevWidth = 0;
        for (int i = 1; i <= lineText.Length; i++)
        {
            float width = MeasureTextWidth(lineText[..i]);
            float midpoint = (prevWidth + width) / 2f;
            if (textX < midpoint) return _lineOffsets[line] + i - 1;
            prevWidth = width;
        }
        return _lineOffsets[line] + lineText.Length;
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
                _selAnchor = 0;
                _selEnd = _text.Length;
                UpdateSelection();
                MarkDirty();
            });
        }
    }
}
