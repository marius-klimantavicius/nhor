using System;
using System.Numerics;
using ThorVG;
using static Glfw.GLFW;

namespace Marius.Winter;

public class TextBox : Element
{
    public bool ReadOnly { get; set; }
    private bool _invalid;
    /// <summary>Explicit validation state, independent of text contents.</summary>
    public bool Invalid
    {
        get => _invalid;
        set { if (_invalid == value) return; _invalid = value; UpdateBorderColor(); MarkDirty(); }
    }
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
    private readonly TextEditingBoundaries _boundaries = new();
    private TextEditingBoundaries Boundaries
    {
        get { _boundaries.SetText(_text); return _boundaries; }
    }
    private int _cursorPos;
    private int _selectionStart = -1;
    private bool _shapesCreated;
    private float _scrollOffset;
    private float _effectiveFontSize;
    private readonly TextAdvanceCache _advances = new();
    private readonly TextAdvanceCache _placeholderAdvances = new();
    private readonly TextEditingBoundaries _placeholderBoundaries = new();
    private string? _metricsFont;
    private float _metricsFontSize = float.NaN;
    private int _layoutVersion;
    private object? _blinkTag;
    private bool _mouseDown;

    // Visible text tracking
    private int _visibleStart;
    private int _visibleEnd;
    private bool _required;

    private static readonly Color4 ErrorBorder = new(0.85f, 0.20f, 0.20f, 1f);

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
        if (_required) UpdateBorderColor();
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

    public TextBox(string text = "", string placeholder = "")
    {
        _text = text;
        _notifiedText = text;
        _placeholder = placeholder;
        _cursorPos = text.Length;
        Cursor = CursorType.IBeam;
        Focusable = true;
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
            _text = value;
            _notifiedText = value;
            _hasUncommittedEdit = false;
            _cursorPos = Boundaries.Floor(_cursorPos);
            _selectionStart = -1;
            UpdateVisibleText();
            if (_required) UpdateBorderColor();
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
            UpdateVisibleText();
            MarkDirty();
        }
    }

    public bool Required
    {
        get => _required;
        set
        {
            if (_required == value) return;
            _required = value;
            UpdateBorderColor();
            MarkDirty();
        }
    }

    protected override void OnAttached()
    {
        _layoutVersion++;
        _metricsFont = null;
        if (!_shapesCreated)
        {
            _shapesCreated = true;
            var style = Style;
            var theme = OwnerWindow?.Theme ?? Theme.Dark;
            EnsureTextMetrics();
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
            UpdateBorderColor();
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
        _layoutVersion++;
        float w = Bounds.W, h = Bounds.H;

        // Background — nanogui: RoundedRect(x+1, y+1+1, w-2, h-2, 3)
        _backgroundShape?.ResetShape();
        if (w > 2 && h > 2) _backgroundShape?.AppendRect(1, 2, w - 2, h - 2, 3, 3);

        // Border — nanogui: RoundedRect(x+0.5, y+0.5, w-1, h-1, 2.5)
        _borderShape?.ResetShape();
        if (w > 1 && h > 1) _borderShape?.AppendRect(0.5f, 0.5f, w - 1, h - 1, 2.5f, 2.5f);

        // Update text clip to content area (coordinates are in _textScene local space,
        // which is already translated by (xSpacing, textY), so x starts at 0)
        float xSp = XSpacing;
        if (w > 2 * xSp && h > 0)
        {
            _textClipShape?.ResetShape();
            _textClipShape?.AppendRect(0, 0, w - 2 * xSp, h, 0, 0);
        }

        if (_textPaint != null) EnsureCursorVisible();
    }

    protected override void OnStateChanged(ElementState oldState, ElementState newState)
    {
        UpdateBorderColor();
        MarkDirty();
    }

    private void UpdateBorderColor()
    {
        if (_borderShape == null) return;
        var style = Style;

        if (_invalid || (_required && string.IsNullOrEmpty(_text)))
            _borderShape.StrokeFill(ErrorBorder.R8, ErrorBorder.G8, ErrorBorder.B8, ErrorBorder.A8);
        else if ((State & ElementState.Focused) != 0)
            _borderShape.StrokeFill(style.BorderFocused.R8, style.BorderFocused.G8, style.BorderFocused.B8, style.BorderFocused.A8);
        else
            _borderShape.StrokeFill(style.Border.R8, style.Border.G8, style.Border.B8, style.Border.A8);
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
        CommitEdit(TextCommitReason.Blur);
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
        if (!IsEffectivelyEnabled || ReadOnly) return;

        DeleteSelection();
        _text = _text.Insert(_cursorPos, text);
        _cursorPos = Boundaries.Ceiling(_cursorPos + text.Length);
        _selectionStart = -1;
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

        if (key == GLFW_KEY_ENTER || key == GLFW_KEY_KP_ENTER)
        {
            if (!repeat)
            {
                CommitEdit(TextCommitReason.Enter);
                base.OnKeyDown(key, mods, repeat);
            }
            return;
        }

        if (ReadOnly && (key == GLFW_KEY_BACKSPACE || key == GLFW_KEY_DELETE ||
            (ctrl && (key == GLFW_KEY_X || key == GLFW_KEY_V)))) return;

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
                    _cursorPos = Boundaries.Previous(_cursorPos);
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
                    _cursorPos = Boundaries.Next(_cursorPos);
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
                    int count = _cursorPos - (ctrl ? FindWordBoundaryLeft(_cursorPos) : Boundaries.Previous(_cursorPos));
                    _cursorPos -= count;
                    _text = _text.Remove(_cursorPos, count);
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

    // --- Nanogui-style spacing: xSpacing = size.Y * 0.3 ---

    private float XSpacing => Bounds.H * 0.3f;

    // --- Text measurement ---

    private void EnsureTextMetrics()
    {
        var style = Style;
        if (_metricsFont != style.FontName || _metricsFontSize != style.FontSize)
        {
            _metricsFont = style.FontName;
            _metricsFontSize = style.FontSize;
            var measure = ThorVG.Text.Gen();
            measure.Ref();
            try
            {
                measure.SetFont(style.FontName);
                measure.SetFontSize(style.FontSize);
                measure.SetText("M");
                measure.GetMetrics(out var metrics);
                float height = metrics.ascent - metrics.descent;
                _effectiveFontSize = height > 0 ? style.FontSize * style.FontSize / height : style.FontSize;
            }
            finally { measure.Unref(); }
            _textPaint?.SetFont(style.FontName);
            _textPaint?.SetFontSize(_effectiveFontSize);
            _placeholderPaint?.SetFont(style.FontName);
            _placeholderPaint?.SetFontSize(_effectiveFontSize);
        }
        _advances.Ensure(_text, Boundaries, style.FontName, _effectiveFontSize, _layoutVersion);
        string placeholder = string.IsNullOrEmpty(_placeholder) ? "M" : _placeholder;
        _placeholderBoundaries.SetText(placeholder);
        _placeholderAdvances.Ensure(placeholder, _placeholderBoundaries, style.FontName, _effectiveFontSize, _layoutVersion);
    }

    private float GetCursorX()
    {
        EnsureTextMetrics();
        return _advances.Advance(_cursorPos);
    }

    private int GetCharIndexAtX(float x)
    {
        EnsureTextMetrics();
        return _advances.HitTest(x);
    }

    // --- Visible text management (software clipping) ---

    private void UpdateVisibleText()
    {
        if (_textPaint == null) return;
        EnsureTextMetrics();

        bool hasTextViewport = Bounds.W > 2 * XSpacing && Bounds.H > 0;
        // Retain prepared glyphs/clip geometry through a collapsed viewport.
        // Hiding the scene avoids preparing an empty GL clip, which would lose
        // the retained text on return even though its editing state survived.
        _textScene?.Visible(hasTextViewport);
        _placeholderScene?.Visible(string.IsNullOrEmpty(_text) && hasTextViewport);
        if (_required) UpdateBorderColor();

        if (!hasTextViewport)
        {
            UpdateCursorPosition(); UpdateSelection();
            return;
        }
        float visibleWidth = Bounds.W - 2 * XSpacing;
        float totalWidth = _advances.Advance(_text.Length);
        // Attachment/theme work may run before layout restores a viewport.
        // Once it returns, retain only scroll that the current text can use.
        _scrollOffset = Math.Clamp(_scrollOffset, 0,
            totalWidth <= visibleWidth ? 0 : Math.Max(0, totalWidth - visibleWidth + 2));
        if (string.IsNullOrEmpty(_text))
        {
            _textPaint.SetText("");
            _visibleStart = 0;
            _visibleEnd = 0;
            // Position placeholder even when text is empty
            if (Bounds.W > 0 && !string.IsNullOrEmpty(_placeholder))
            {
                _placeholderScene?.Translate(XSpacing,
                    TextSceneY(_placeholderAdvances.BoundsY, _placeholderAdvances.BoundsHeight));
            }
            UpdateCursorPosition();
            UpdateSelection();
            return;
        }

        float xSpacing = XSpacing;

        // Measure text for vertical centering with by offset compensation
        float textY = TextSceneY(_advances.BoundsY, _advances.BoundsHeight);

        // Measure placeholder for its own vertical centering
        float placeholderY = TextSceneY(_placeholderAdvances.BoundsY, _placeholderAdvances.BoundsHeight);

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

        // Clip only at grapheme boundaries, using the same cached advances as
        // caret placement. Include a partially visible cluster at either edge.
        _visibleStart = totalWidth <= _scrollOffset ? _text.Length :
            _advances.OffsetAt(Math.Max(0, _advances.FirstAfter(_scrollOffset) - 1));
        _visibleEnd = _advances.OffsetAt(_advances.FirstAfter(_scrollOffset + visibleWidth));

        // Set visible substring
        string visible = _text[_visibleStart.._visibleEnd];
        _textPaint.SetText(visible);

        // Position text scene
        float startOffset = _advances.Advance(_visibleStart);
        _textScene?.Translate(xSpacing + startOffset - _scrollOffset, textY);
        _placeholderScene?.Translate(xSpacing, placeholderY);

        UpdateCursorPosition();
        UpdateSelection();
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
        _cursorShape.ResetShape();
        // Hidden layout and narrow resize can leave no content interval. Keep
        // the editing offsets, but paint no caret until that interval returns.
        if (Bounds.W <= 2 * xSpacing || Bounds.H <= 6) return;
        float cursorX = GetCursorX() - _scrollOffset + xSpacing;
        float clipLeft = xSpacing;
        float clipRight = Bounds.W - xSpacing;

        // Cursor spans most of the textbox height, like nanogui's lineHeight
        float cursorH = Bounds.H - 6;
        float cursorY = 3;

        cursorX = Math.Clamp(cursorX, clipLeft, clipRight);

        _cursorShape.AppendRect(cursorX, cursorY, 1f, cursorH, 0, 0);
    }

    private void EnsureCursorVisible()
    {
        _cursorPos = Boundaries.Floor(_cursorPos);
        float xSpacing = XSpacing;
        float visibleWidth = Bounds.W - 2 * xSpacing;
        if (visibleWidth <= 0 || Bounds.H <= 0)
        {
            UpdateVisibleText();
            return;
        }
        float cursorX = GetCursorX();

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
        _selectionShape?.ResetShape();
        if (!HasSelection() || Bounds.W <= 2 * XSpacing || Bounds.H <= 6)
        {
            _selectionShape?.Visible(false);
            return;
        }

        int start = Math.Min(_selectionStart, _cursorPos);
        int end = Math.Max(_selectionStart, _cursorPos);
        EnsureTextMetrics();
        float xSpacing = XSpacing;
        float clipLeft = xSpacing;
        float clipRight = Bounds.W - xSpacing;

        float startX = _advances.Advance(start) - _scrollOffset + xSpacing;
        float endX = _advances.Advance(end) - _scrollOffset + xSpacing;

        startX = Math.Max(startX, clipLeft);
        endX = Math.Min(endX, clipRight);

        float y = 3;
        float h = Bounds.H - 6;

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
        if (ReadOnly || !IsEffectivelyEnabled) return;
        if (!HasSelection()) return;
        int start = Math.Min(_selectionStart, _cursorPos);
        int end = Math.Max(_selectionStart, _cursorPos);
        _text = _text.Remove(start, end - start);
        // Keep the original insertion point until replacement is complete;
        // removing text can temporarily join its two neighboring graphemes.
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
        if (ReadOnly || !IsEffectivelyEnabled) return;
        var clip = Glfw.Glfw.glfwGetClipboardString(null);
        if (string.IsNullOrEmpty(clip)) return;
        DeleteSelection();
        _text = _text.Insert(_cursorPos, clip);
        _cursorPos = Boundaries.Ceiling(_cursorPos + clip.Length);
        _selectionStart = -1;
        EnsureCursorVisible();
        MarkDirty();
        NotifyUserEdit();
    }

    // --- Word boundaries ---

    private int FindWordBoundaryLeft(int pos) => Boundaries.WordLeft(pos);

    private int FindWordBoundaryRight(int pos) => Boundaries.WordRight(pos);

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

    protected override void OnStyleChanged()
    {
        InvalidateMeasure();
        OnThemeChanged();
    }

    protected override void OnThemeChanged()
    {
        _layoutVersion++;
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

        EnsureCursorVisible();
        UpdateBorderColor();
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
                EnsureCursorVisible();
                UpdateVisibleText();
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
