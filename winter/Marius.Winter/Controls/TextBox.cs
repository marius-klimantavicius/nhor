using System;
using System.Numerics;
using ThorVG;
using static Glfw.GLFW;

namespace Marius.Winter;

public class TextBox : Element
{
    private Shape? _backgroundShape;
    private Shape? _borderShape;
    private Shape? _cursorShape;
    private Shape? _selectionShape;
    private Shape? _textClipShape;
    private Text? _textPaint;
    private Text? _placeholderPaint;
    private Scene? _textScene;
    private Scene? _placeholderScene;
    private string _text = "";
    private string _placeholder = "";
    private int _cursorPos;
    private int _selectionStart = -1;
    private bool _shapesCreated;
    private float _scrollOffset;
    private float _effectiveFontSize;
    private object? _blinkTag;
    private bool _mouseDown;

    // Visible text tracking
    private int _visibleStart;
    private int _visibleEnd;

    public Action<string>? TextChanged;

    public TextBox(string text = "", string placeholder = "")
    {
        _text = text;
        _placeholder = placeholder;
        _cursorPos = text.Length;
        Cursor = CursorType.IBeam;
        Focusable = true;
    }

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            _cursorPos = Math.Clamp(_cursorPos, 0, value.Length);
            _selectionStart = -1;
            UpdateVisibleText();
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

    protected override void OnAttached()
    {
        if (!_shapesCreated)
        {
            _shapesCreated = true;
            var style = Style;
            var theme = OwnerWindow?.Theme ?? Theme.Dark;
            _effectiveFontSize = EffectiveFontSize(style.FontSize);
            _blinkTag = new object();

            // Background — nanogui uses BoxGradient; we approximate with solid
            _backgroundShape = Shape.Gen();
            _backgroundShape!.SetFill(style.Background.R8, style.Background.G8, style.Background.B8, style.Background.A8);
            AddPaint(_backgroundShape);

            // Border — nanogui: thin stroke rgba(0,0,0,48)
            _borderShape = Shape.Gen();
            _borderShape!.StrokeWidth(1f);
            _borderShape.StrokeFill(style.Border.R8, style.Border.G8, style.Border.B8, style.Border.A8);
            _borderShape.SetFill(0, 0, 0, 0);
            AddPaint(_borderShape);

            // Selection — nanogui: white semi-transparent
            _selectionShape = Shape.Gen();
            var selColor = theme.SelectionColor;
            _selectionShape!.SetFill(selColor.R8, selColor.G8, selColor.B8, selColor.A8);
            _selectionShape.Visible(false);
            AddPaint(_selectionShape);

            // Text
            _textPaint = ThorVG.Text.Gen();
            _textPaint!.SetFont(style.FontName);
            _textPaint.SetFontSize(_effectiveFontSize);
            _textPaint.SetText(_text);
            _textPaint.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);

            _textScene = Scene.Gen()!;
            _textScene.Add(_textPaint);
            AddPaint(_textScene);

            // Clip text scene to content area (prevents overflow past padding)
            _textClipShape = Shape.Gen();
            _textClipShape!.SetFill(255, 255, 255, 255);
            _textScene.Clip(_textClipShape);

            // Placeholder — nanogui: disabled text color
            _placeholderPaint = ThorVG.Text.Gen();
            _placeholderPaint!.SetFont(style.FontName);
            _placeholderPaint.SetFontSize(_effectiveFontSize);
            _placeholderPaint.SetText(_placeholder);
            _placeholderPaint.SetFill(theme.DisabledTextColor.R8, theme.DisabledTextColor.G8, theme.DisabledTextColor.B8);

            _placeholderScene = Scene.Gen()!;
            _placeholderScene.Add(_placeholderPaint);
            _placeholderScene.Visible(string.IsNullOrEmpty(_text));
            AddPaint(_placeholderScene);

            // Cursor — nanogui: orange (255, 192, 0)
            _cursorShape = Shape.Gen();
            _cursorShape!.SetFill(theme.CursorColor.R8, theme.CursorColor.G8, theme.CursorColor.B8, theme.CursorColor.A8);
            _cursorShape.Visible(false);
            AddPaint(_cursorShape);
        }
    }

    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        var style = Style;
        // Nanogui: height = FontSize * 1.4
        return new Vector2(200, style.FontSize * 1.4f);
    }

    protected override void OnSizeChanged()
    {
        float w = Bounds.W, h = Bounds.H;

        // Background — nanogui: RoundedRect(x+1, y+1+1, w-2, h-2, 3)
        _backgroundShape?.ResetShape();
        _backgroundShape?.AppendRect(1, 2, w - 2, h - 2, 3, 3);

        // Border — nanogui: RoundedRect(x+0.5, y+0.5, w-1, h-1, 2.5)
        _borderShape?.ResetShape();
        _borderShape?.AppendRect(0.5f, 0.5f, w - 1, h - 1, 2.5f, 2.5f);

        // Update text clip to content area (coordinates are in _textScene local space,
        // which is already translated by (xSpacing, textY), so x starts at 0)
        float xSp = XSpacing;
        _textClipShape?.ResetShape();
        _textClipShape?.AppendRect(0, 0, w - 2 * xSp, h, 0, 0);

        UpdateVisibleText();
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
        StopBlinkAnimation();
        MarkDirty();
    }

    public override bool OnMouseDown(int button, float x, float y)
    {
        if (!Enabled || button != 0) return false;

        WindowToLocal(x, y, out float lx, out float ly);
        float textX = lx - XSpacing + _scrollOffset;
        _cursorPos = GetCharIndexAtX(textX);
        _selectionStart = _cursorPos;
        _mouseDown = true;
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
        if (_selectionStart == _cursorPos)
            _selectionStart = -1;
        return true;
    }

    public override void OnMouseMove(float x, float y)
    {
        if (!_mouseDown) return;

        WindowToLocal(x, y, out float lx, out float ly);
        float textX = lx - XSpacing + _scrollOffset;
        _cursorPos = GetCharIndexAtX(textX);
        UpdateCursorPosition();
        EnsureCursorVisible();
        UpdateSelection();
        RestartBlink();
        MarkDirty();
    }

    public override void OnTextInput(string text)
    {
        if (!Enabled) return;

        DeleteSelection();
        _text = _text.Insert(_cursorPos, text);
        _cursorPos += text.Length;
        _selectionStart = -1;
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
                UpdateCursorPosition();
                UpdateSelection();
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
                    _cursorPos++;
                if (!shift) _selectionStart = -1;
                UpdateCursorPosition();
                UpdateSelection();
                EnsureCursorVisible();
                RestartBlink();
                MarkDirty();
                break;

            case GLFW_KEY_HOME:
                if (shift && _selectionStart < 0) _selectionStart = _cursorPos;
                _cursorPos = 0;
                if (!shift) _selectionStart = -1;
                UpdateCursorPosition();
                UpdateSelection();
                EnsureCursorVisible();
                MarkDirty();
                break;

            case GLFW_KEY_END:
                if (shift && _selectionStart < 0) _selectionStart = _cursorPos;
                _cursorPos = _text.Length;
                if (!shift) _selectionStart = -1;
                UpdateCursorPosition();
                UpdateSelection();
                EnsureCursorVisible();
                MarkDirty();
                break;

            case GLFW_KEY_BACKSPACE:
                if (HasSelection())
                    DeleteSelection();
                else if (_cursorPos > 0)
                {
                    int count = ctrl ? _cursorPos - FindWordBoundaryLeft(_cursorPos) : 1;
                    _cursorPos -= count;
                    _text = _text.Remove(_cursorPos, count);
                }
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
                    _text = _text.Remove(_cursorPos, count);
                }
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
                EnsureCursorVisible();
                MarkDirty();
                TextChanged?.Invoke(_text);
                break;

            case GLFW_KEY_V when ctrl:
                PasteFromClipboard();
                break;

            default:
                base.OnKeyDown(key, mods, repeat);
                break;
        }
    }

    // --- Nanogui-style spacing: xSpacing = size.Y * 0.3 ---

    private float XSpacing => Bounds.H * 0.3f;

    // --- Text measurement ---

    private float MeasureTextWidth(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var t = ThorVG.Text.Gen();
        if (t == null) return 0;
        t.SetFont(Style.FontName);
        t.SetFontSize(_effectiveFontSize);
        t.SetText(text);
        if (t.GetTextSize(out float w, out _) == ThorVG.Result.Success)
            return w;
        t.Bounds(out float bx, out _, out float bw, out _);
        return bx + bw;
    }

    private float GetCursorX()
    {
        if (_cursorPos <= 0) return 0;
        return MeasureTextWidth(_text[.._cursorPos]);
    }

    private int GetCharIndexAtX(float x)
    {
        if (string.IsNullOrEmpty(_text)) return 0;
        float prevWidth = 0;
        for (int i = 1; i <= _text.Length; i++)
        {
            float width = MeasureTextWidth(_text[..i]);
            float midpoint = (prevWidth + width) / 2f;
            if (x < midpoint) return i - 1;
            prevWidth = width;
        }
        return _text.Length;
    }

    // --- Visible text management (software clipping) ---

    private void UpdateVisibleText()
    {
        if (_textPaint == null) return;

        _placeholderScene?.Visible(string.IsNullOrEmpty(_text));

        if (string.IsNullOrEmpty(_text) || Bounds.W <= 0)
        {
            _textPaint.SetText("");
            _visibleStart = 0;
            _visibleEnd = 0;
            // Position placeholder even when text is empty
            if (Bounds.W > 0 && !string.IsNullOrEmpty(_placeholder))
            {
                MeasureTextBounds(_placeholder, out _, out float eby, out _, out float ebh);
                _placeholderScene?.Translate(XSpacing, TextSceneY(eby, ebh));
            }
            UpdateCursorPosition();
            UpdateSelection();
            return;
        }

        float xSpacing = XSpacing;
        float visibleWidth = Bounds.W - 2 * xSpacing;
        if (visibleWidth <= 0)
        {
            _textPaint.SetText("");
            return;
        }

        float totalWidth = MeasureTextWidth(_text);

        // Measure text for vertical centering with by offset compensation
        MeasureTextBounds(_text, out float tbx, out float tby, out _, out float tbh);
        float textY = TextSceneY(tby, tbh);

        // Measure placeholder for its own vertical centering
        MeasureTextBounds(string.IsNullOrEmpty(_placeholder) ? "M" : _placeholder, out _, out float pby, out _, out float pbh);
        float placeholderY = TextSceneY(pby, pbh);

        // If all text fits and no scrolling, show everything
        if (totalWidth <= visibleWidth && _scrollOffset <= 0)
        {
            _visibleStart = 0;
            _visibleEnd = _text.Length;
            _textPaint.SetText(_text);
            _textScene?.Translate(xSpacing, textY);
            _placeholderScene?.Translate(xSpacing, placeholderY);
            UpdateCursorPosition();
            UpdateSelection();
            return;
        }

        // Find first visible character
        _visibleStart = 0;
        for (int i = 1; i <= _text.Length; i++)
        {
            float w = MeasureTextWidth(_text[..i]);
            if (w > _scrollOffset)
            {
                _visibleStart = i - 1;
                break;
            }
            if (i == _text.Length)
                _visibleStart = _text.Length;
        }

        // Find last visible character
        _visibleEnd = _text.Length;
        for (int i = _visibleStart + 1; i <= _text.Length; i++)
        {
            float leftEdge = MeasureTextWidth(_text[..i]) - _scrollOffset;
            if (leftEdge > visibleWidth)
            {
                _visibleEnd = i;
                break;
            }
        }

        // Set visible substring
        string visible = _text[_visibleStart.._visibleEnd];
        _textPaint.SetText(visible);

        // Position text scene
        float startOffset = _visibleStart > 0 ? MeasureTextWidth(_text[.._visibleStart]) : 0;
        _textScene?.Translate(xSpacing + startOffset - _scrollOffset, textY);
        _placeholderScene?.Translate(xSpacing, placeholderY);

        UpdateCursorPosition();
        UpdateSelection();
    }

    /// <summary>Measure text bounds with a fresh Text to get clean bx/by/bw/bh.</summary>
    private void MeasureTextBounds(string text, out float bx, out float by, out float bw, out float bh)
    {
        bx = by = 0;
        bw = 0;
        bh = Style.FontSize;
        var t = ThorVG.Text.Gen();
        if (t == null) return;
        t.SetFont(Style.FontName);
        t.SetFontSize(_effectiveFontSize);
        t.SetText(text);
        t.Bounds(out bx, out by, out bw, out bh);
        if (bh <= 0) bh = Style.FontSize;
    }

    /// <summary>Compute scene translate Y that vertically centers text, compensating for by offset.</summary>
    private float TextSceneY(float by, float bh)
    {
        return (Bounds.H - bh) / 2f - by;
    }

    private void UpdateCursorPosition()
    {
        if (_cursorShape == null) return;
        float xSpacing = XSpacing;
        float cursorX = GetCursorX() - _scrollOffset + xSpacing;
        float clipLeft = xSpacing;
        float clipRight = Bounds.W - xSpacing;

        // Cursor spans most of the textbox height, like nanogui's lineHeight
        float cursorH = Bounds.H - 6;
        float cursorY = 3;

        cursorX = Math.Clamp(cursorX, clipLeft, clipRight);

        _cursorShape.ResetShape();
        _cursorShape.AppendRect(cursorX, cursorY, 1f, cursorH, 0, 0);
    }

    private void EnsureCursorVisible()
    {
        float xSpacing = XSpacing;
        float cursorX = GetCursorX();
        float visibleWidth = Bounds.W - 2 * xSpacing;

        if (cursorX - _scrollOffset > visibleWidth - 2)
            _scrollOffset = cursorX - visibleWidth + 2;
        else if (cursorX - _scrollOffset < 0)
            _scrollOffset = cursorX;

        if (_scrollOffset < 0) _scrollOffset = 0;
        UpdateVisibleText();
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
        float xSpacing = XSpacing;
        float clipLeft = xSpacing;
        float clipRight = Bounds.W - xSpacing;

        float startX = MeasureTextWidth(_text[..start]) - _scrollOffset + xSpacing;
        float endX = MeasureTextWidth(_text[..end]) - _scrollOffset + xSpacing;

        startX = Math.Max(startX, clipLeft);
        endX = Math.Min(endX, clipRight);

        float y = 3;
        float h = Bounds.H - 6;

        _selectionShape?.ResetShape();
        if (endX > startX)
        {
            _selectionShape?.AppendRect(startX, y, endX - startX, h, 0, 0);
            _selectionShape?.Visible(true);
        }
        else
        {
            _selectionShape?.Visible(false);
        }
    }

    private void DeleteSelection()
    {
        if (!HasSelection()) return;
        int start = Math.Min(_selectionStart, _cursorPos);
        int end = Math.Max(_selectionStart, _cursorPos);
        _text = _text.Remove(start, end - start);
        _cursorPos = start;
        _selectionStart = -1;
        _selectionShape?.Visible(false);
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
        _text = _text.Insert(_cursorPos, clip);
        _cursorPos += clip.Length;
        EnsureCursorVisible();
        MarkDirty();
        TextChanged?.Invoke(_text);
    }

    // --- Word boundaries ---

    private int FindWordBoundaryLeft(int pos)
    {
        if (pos <= 0) return 0;
        pos--;
        while (pos > 0 && char.IsWhiteSpace(_text[pos])) pos--;
        while (pos > 0 && !char.IsWhiteSpace(_text[pos - 1])) pos--;
        return pos;
    }

    private int FindWordBoundaryRight(int pos)
    {
        if (pos >= _text.Length) return _text.Length;
        while (pos < _text.Length && !char.IsWhiteSpace(_text[pos])) pos++;
        while (pos < _text.Length && char.IsWhiteSpace(_text[pos])) pos++;
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
        _textPaint?.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);
        _placeholderPaint?.SetFill(theme.DisabledTextColor.R8, theme.DisabledTextColor.G8, theme.DisabledTextColor.B8);
        _cursorShape?.SetFill(theme.CursorColor.R8, theme.CursorColor.G8, theme.CursorColor.B8, theme.CursorColor.A8);

        var selColor = theme.SelectionColor;
        _selectionShape?.SetFill(selColor.R8, selColor.G8, selColor.B8, selColor.A8);

        MarkDirty();
    }

}
