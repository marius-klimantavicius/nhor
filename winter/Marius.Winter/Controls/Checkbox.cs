using System;
using System.Numerics;
using ThorVG;

namespace Marius.Winter;

public class Checkbox : Element
{
    private Shape? _boxShape;
    private Shape? _boxBorder;
    private Shape? _checkShape;
    private Scene? _labelScene;
    private Text? _labelText;
    private string _text = "";
    private bool _isChecked;
    private bool _shapesCreated;

    public Action<bool>? Changed;

    public Checkbox(string text = "", bool isChecked = false)
    {
        _text = text;
        _isChecked = isChecked;
        Cursor = CursorType.Hand;
        Focusable = true;
    }

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            _labelText?.SetText(value);
            InvalidateMeasure();
            MarkDirty();
        }
    }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            _checkShape?.Visible(value);
            MarkDirty();
            Changed?.Invoke(value);
        }
    }

    protected override void OnAttached()
    {
        if (!_shapesCreated)
        {
            _shapesCreated = true;
            var style = Style;
            var theme = OwnerWindow?.Theme ?? Theme.Dark;

            // Nanogui: box size = height - 2, positioned at (x+1, y+1)
            float boxSize = style.FontSize * 1.3f - 2f;

            // Box fill — nanogui uses BoxGradient; we use solid
            _boxShape = Shape.Gen();
            _boxShape!.AppendRect(1.5f, 1.5f, boxSize, boxSize, 3, 3);
            _boxShape.SetFill(style.Background.R8, style.Background.G8, style.Background.B8, style.Background.A8);
            AddPaint(_boxShape);

            // Box border — dark outline
            _boxBorder = Shape.Gen();
            _boxBorder!.AppendRect(1f, 1f, boxSize + 1, boxSize + 1, 3, 3);
            _boxBorder.StrokeWidth(1f);
            _boxBorder.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, theme.BorderDark.A8);
            _boxBorder.SetFill(0, 0, 0, 0);
            AddPaint(_boxBorder);

            // Check mark
            _checkShape = Shape.Gen();
            float cx = 1f + boxSize / 2f;
            float cy = 1f + boxSize / 2f;
            float s = boxSize * 0.35f;
            _checkShape!.MoveTo(cx - s, cy);
            _checkShape.LineTo(cx - s * 0.3f, cy + s * 0.6f);
            _checkShape.LineTo(cx + s, cy - s * 0.5f);
            _checkShape.StrokeWidth(2f);
            _checkShape.StrokeFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8, 255);
            _checkShape.StrokeCap(StrokeCap.Round);
            _checkShape.StrokeJoin(StrokeJoin.Round);
            _checkShape.SetFill(0, 0, 0, 0);
            _checkShape.Visible(_isChecked);
            AddPaint(_checkShape);

            // Label — nanogui: positioned at 1.6 * fontSize from left
            _labelText = ThorVG.Text.Gen();
            _labelText!.SetFont(style.FontName);
            _labelText.SetFontSize(EffectiveFontSize(style.FontSize));
            _labelText.SetText(_text);
            _labelText.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);

            _labelScene = Scene.Gen()!;
            _labelScene.Add(_labelText);
            AddPaint(_labelScene);

            // Position label: 1.6 * fontSize from left, vertically centered with by compensation
            PositionLabel(style);
        }
    }

    private void PositionLabel(Style style)
    {
        if (_labelScene == null || string.IsNullOrEmpty(_text)) return;
        var m = ThorVG.Text.Gen();
        if (m == null) return;
        m.SetFont(style.FontName);
        m.SetFontSize(EffectiveFontSize(style.FontSize));
        m.SetText(_text);
        m.Bounds(out _, out float by, out _, out float bh);
        if (bh <= 0) return;
        float h = Bounds.H > 0 ? Bounds.H : style.FontSize * 1.3f;
        float y = (h - bh) / 2f - by;
        _labelScene.Translate(1.6f * style.FontSize, y);
    }

    protected override void OnSizeChanged()
    {
        PositionLabel(Style);
    }

    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        var style = Style;
        float textW = 0;

        // Use fresh text for measurement
        var m = ThorVG.Text.Gen();
        if (m != null && !string.IsNullOrEmpty(_text))
        {
            m.SetFont(style.FontName);
            m.SetFontSize(EffectiveFontSize(style.FontSize));
            m.SetText(_text);
            m.Bounds(out _, out _, out float bw, out _);
            if (bw > 0) textW = bw;
        }

        // Nanogui: (textW + 1.8 * fontSize, fontSize * 1.3)
        return new Vector2(textW + 1.8f * style.FontSize, style.FontSize * 1.3f);
    }

    protected override void OnStateChanged(ElementState oldState, ElementState newState)
    {
        var style = Style;

        // Nanogui: gradTop changes based on state (32 → 66 hovered → 100 pressed)
        Color4 bg;
        if ((newState & ElementState.Pressed) != 0)
            bg = style.BackgroundPressed;
        else if ((newState & ElementState.Hovered) != 0)
            bg = style.BackgroundHovered;
        else
            bg = style.Background;

        _boxShape?.SetFill(bg.R8, bg.G8, bg.B8, bg.A8);

        // Focus border
        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        if ((newState & ElementState.Focused) != 0)
            _boxBorder?.StrokeFill(style.BorderFocused.R8, style.BorderFocused.G8, style.BorderFocused.B8, style.BorderFocused.A8);
        else
            _boxBorder?.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, theme.BorderDark.A8);

        MarkDirty();
    }

    public override void OnClick()
    {
        if (!Enabled) return;
        IsChecked = !_isChecked;
    }

    public override void OnKeyDown(int key, int mods, bool repeat)
    {
        // Space toggles the checkbox
        if (key == 32 /* GLFW_KEY_SPACE */)
        {
            OnClick();
            return;
        }
        base.OnKeyDown(key, mods, repeat);
    }

    public override void OnMouseEnter()
    {
        if (!Enabled) return;
        State = State | ElementState.Hovered;
    }

    public override void OnMouseLeave()
    {
        State = State & ~ElementState.Hovered & ~ElementState.Pressed;
    }

    public override bool OnMouseDown(int button, float x, float y)
    {
        if (!Enabled || button != 0) return false;
        State = State | ElementState.Pressed;
        return true;
    }

    public override bool OnMouseUp(int button, float x, float y)
    {
        if (button != 0) return false;
        State = State & ~ElementState.Pressed;
        return true;
    }

    protected override Style GetDefaultStyle()
    {
        return OwnerWindow?.Theme.Checkbox ?? new Style();
    }

    protected override void OnThemeChanged()
    {
        if (!_shapesCreated) return;
        var style = Style;
        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        _boxShape?.SetFill(style.Background.R8, style.Background.G8, style.Background.B8, style.Background.A8);
        _boxBorder?.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, theme.BorderDark.A8);
        _checkShape?.StrokeFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8, 255);
        _labelText?.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);
        MarkDirty();
    }

}
