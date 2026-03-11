using System;
using System.Numerics;
using ThorVG;

namespace Marius.Winter;

public class Slider : Element
{
    private Shape? _trackShape;
    private Shape? _knobShape;
    private Shape? _knobBorder;
    private Shape? _knobDot;
    private Scene? _valueScene;
    private Text? _valueText;
    private float _value;
    private float _min;
    private float _max = 1f;
    private float _step;
    private bool _showValue;
    private bool _shapesCreated;
    private bool _dragging;

    public Action<float>? ValueChanged;

    public Slider(float min = 0f, float max = 1f, float value = 0f)
    {
        _min = min;
        _max = max;
        _value = Math.Clamp(value, min, max);
        Cursor = CursorType.Hand;
        Focusable = true;
    }

    public float Value
    {
        get => _value;
        set
        {
            float clamped = Math.Clamp(value, _min, _max);
            if (_step > 0) clamped = MathF.Round(clamped / _step) * _step;
            if (_value == clamped) return;
            _value = clamped;
            UpdateShapes();
            MarkDirty();
            ValueChanged?.Invoke(_value);
        }
    }

    public float Min { get => _min; set { _min = value; Value = _value; } }
    public float Max { get => _max; set { _max = value; Value = _value; } }
    public float Step { get => _step; set => _step = value; }

    /// <summary>
    /// Atomically set min, max, and value. Applies min/max first, then clamps
    /// value, then fires ValueChanged once if the effective value changed.
    /// Used by Blazor handler to avoid attribute-ordering bugs.
    /// </summary>
    public void SetRange(float min, float max, float value)
    {
        _min = min;
        _max = max;
        float clamped = Math.Clamp(value, _min, _max);
        if (_step > 0) clamped = MathF.Round(clamped / _step) * _step;
        bool changed = _value != clamped;
        _value = clamped;
        UpdateShapes();
        MarkDirty();
        if (changed) ValueChanged?.Invoke(_value);
    }

    public bool ShowValue
    {
        get => _showValue;
        set
        {
            _showValue = value;
            _valueScene?.Visible(value);
            InvalidateMeasure();
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

            // Track — nanogui uses BoxGradient; we use solid dark fill
            _trackShape = Shape.Gen();
            _trackShape!.SetFill(style.Background.R8, style.Background.G8, style.Background.B8, style.Background.A8);
            AddPaint(_trackShape);

            // Knob — nanogui: LinearGradient from BorderLight to BorderMedium
            _knobShape = Shape.Gen();
            ApplyKnobGradient(theme);
            AddPaint(_knobShape!);

            // Knob border — nanogui: StrokeColor(BorderDark)
            _knobBorder = Shape.Gen();
            _knobBorder!.StrokeWidth(1f);
            _knobBorder.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, theme.BorderDark.A8);
            _knobBorder.SetFill(0, 0, 0, 0);
            AddPaint(_knobBorder);

            // Knob dot — nanogui: inner circle with lighter fill
            _knobDot = Shape.Gen();
            _knobDot!.SetFill(style.Foreground.R8, style.Foreground.G8, style.Foreground.B8, style.Foreground.A8);
            AddPaint(_knobDot);

            // Value text
            _valueText = ThorVG.Text.Gen();
            _valueText!.SetFont(style.FontName);
            _valueText.SetFontSize(EffectiveFontSize(style.FontSize > 0 ? style.FontSize - 2 : 14));
            _valueText.SetText(_value.ToString("F1"));
            _valueText.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);

            _valueScene = Scene.Gen()!;
            _valueScene.Add(_valueText);
            _valueScene.Visible(_showValue);
            AddPaint(_valueScene);
        }
    }

    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        // Nanogui: preferred size (70, 16)
        float width = 150;
        float height = 16;
        if (_showValue) width += 40;
        return new Vector2(width, height);
    }

    protected override void OnSizeChanged()
    {
        UpdateShapes();
    }

    private void UpdateShapes()
    {
        if (_trackShape == null) return;

        float w = Bounds.W;
        float h = Bounds.H;
        float cy = h / 2f;

        // Nanogui: kr = (int)(size.Y * 0.4f), shadow = 3
        float kr = (float)(int)(h * 0.4f);
        float shadow = 3f;
        float startX = kr + shadow;
        float trackW = w - 2 * (kr + shadow);

        float range = _max - _min;
        float ratio = range > 0 ? (_value - _min) / range : 0;
        float knobX = startX + ratio * trackW;

        // Track
        _trackShape.ResetShape();
        _trackShape.AppendRect(startX, cy - 3 + 1, trackW, 6, 2, 2);

        // Knob
        _knobShape!.ResetShape();
        _knobShape.AppendCircle(knobX, cy + 0.5f, kr, kr);

        // Knob border
        _knobBorder!.ResetShape();
        _knobBorder.AppendCircle(knobX, cy + 0.5f, kr - 0.5f, kr - 0.5f);

        // Knob dot
        _knobDot!.ResetShape();
        _knobDot.AppendCircle(knobX, cy + 0.5f, kr / 2, kr / 2);

        if (_showValue && _valueText != null)
            _valueText.SetText(_value.ToString("F1"));
    }

    private void ApplyKnobGradient(Theme theme)
    {
        if (_knobShape == null) return;
        float h = Bounds.H > 0 ? Bounds.H : 16;
        float cy = h / 2f;
        float kr = (float)(int)(h * 0.4f);

        var grad = LinearGradient.Gen();
        grad.Linear(0, cy - kr, 0, cy + kr);
        grad.SetColorStops(new Fill.ColorStop[]
        {
            new(0, theme.BorderLight.R8, theme.BorderLight.G8, theme.BorderLight.B8, theme.BorderLight.A8),
            new(1, theme.BorderMedium.R8, theme.BorderMedium.G8, theme.BorderMedium.B8, theme.BorderMedium.A8)
        }, 2);
        _knobShape.SetFill(grad);
    }

    public override bool OnMouseDown(int button, float x, float y)
    {
        if (!Enabled || button != 0) return false;
        _dragging = true;
        WindowToLocal(x, y, out float lx, out _);
        SetValueFromX(lx);
        return true;
    }

    public override bool OnMouseUp(int button, float x, float y)
    {
        if (button != 0) return false;
        _dragging = false;
        return true;
    }

    public override void OnMouseMove(float x, float y)
    {
        if (_dragging)
        {
            WindowToLocal(x, y, out float lx, out _);
            SetValueFromX(lx);
        }
    }

    private void SetValueFromX(float localX)
    {
        float h = Bounds.H;
        float kr = (float)(int)(h * 0.4f);
        float shadow = 3f;
        float startX = kr + shadow;
        float trackW = Bounds.W - 2 * (kr + shadow);
        if (trackW <= 0) return;

        float ratio = Math.Clamp((localX - startX) / trackW, 0f, 1f);
        Value = _min + ratio * (_max - _min);
    }

    public override void OnKeyDown(int key, int mods, bool repeat)
    {
        float step = _step > 0 ? _step : (_max - _min) * 0.01f;
        if (key == 263 /* GLFW_KEY_LEFT */)
            Value = _value - step;
        else if (key == 262 /* GLFW_KEY_RIGHT */)
            Value = _value + step;
        else
            base.OnKeyDown(key, mods, repeat);
    }

    protected override Style GetDefaultStyle()
    {
        return OwnerWindow?.Theme.Slider ?? new Style();
    }

    protected override void OnStateChanged(ElementState oldState, ElementState newState)
    {
        if (!_shapesCreated) return;

        bool disabled = (newState & ElementState.Disabled) != 0;
        byte opacity = disabled ? (byte)100 : (byte)255;
        _trackShape?.Opacity(opacity);
        _knobShape?.Opacity(opacity);
        _knobBorder?.Opacity(opacity);
        _knobDot?.Opacity(opacity);
        _valueScene?.Opacity(opacity);
        Cursor = disabled ? CursorType.Arrow : CursorType.Hand;
        MarkDirty();
    }

    protected override void OnThemeChanged()
    {
        if (!_shapesCreated) return;
        var style = Style;
        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        _trackShape?.SetFill(style.Background.R8, style.Background.G8, style.Background.B8, style.Background.A8);
        ApplyKnobGradient(theme);
        _knobBorder?.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, theme.BorderDark.A8);
        _knobDot?.SetFill(style.Foreground.R8, style.Foreground.G8, style.Foreground.B8, style.Foreground.A8);
        _valueText?.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);
        MarkDirty();
    }

}
