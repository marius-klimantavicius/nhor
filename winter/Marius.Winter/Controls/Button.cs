using System;
using System.Numerics;
using ThorVG;

namespace Marius.Winter;

public class Button : Element
{
    private Shape? _backgroundShape;

    internal override bool ManagesOwnChildLayout => true;
    private Shape? _borderLightShape;
    private Shape? _borderDarkShape;
    private Scene? _shadowScene;
    private Scene? _labelScene;
    private Text? _shadowText;
    private Text? _labelText;
    private string _text = "";
    private bool _shapesCreated;
    private Color4 _currentGradTop;
    private Color4 _currentGradBot;
    private Color4? _customBackground;
    private Color4? _customColor;
    private Alignment _textAlign = Alignment.Center;

    public Action? Clicked;

    public Button(string text = "")
    {
        _text = text;
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
            _shadowText?.SetText(value);
            InvalidateMeasure();
            MarkDirty();
        }
    }

    public Color4? Background
    {
        get => _customBackground;
        set
        {
            if (_customBackground.HasValue == value.HasValue &&
                (!value.HasValue || Color4Eq(_customBackground.GetValueOrDefault(), value.GetValueOrDefault()))) return;
            _customBackground = value;
            if (_shapesCreated)
                OnStateChanged(State, State);
        }
    }

    public Color4? Color
    {
        get => _customColor;
        set
        {
            if (_customColor.HasValue == value.HasValue &&
                (!value.HasValue || Color4Eq(_customColor.GetValueOrDefault(), value.GetValueOrDefault()))) return;
            _customColor = value;
            if (_shapesCreated)
                OnStateChanged(State, State);
        }
    }

    private static bool Color4Eq(Color4 a, Color4 b) =>
        a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A;

    public Alignment TextAlign
    {
        get => _textAlign;
        set
        {
            if (_textAlign == value) return;
            _textAlign = value;
            PositionLabel();
            MarkDirty();
        }
    }

    protected override void OnAttached()
    {
        if (!_shapesCreated)
        {
            _shapesCreated = true;
            var theme = OwnerWindow?.Theme ?? Theme.Dark;
            var style = Style;

            // Background with gradient
            _backgroundShape = Shape.Gen();
            if (_customBackground.HasValue)
            {
                _currentGradTop = _currentGradBot = _customBackground.Value;
            }
            else
            {
                _currentGradTop = theme.ButtonGradientTopUnfocused;
                _currentGradBot = theme.ButtonGradientBotUnfocused;
            }
            ApplyButtonGradient(_currentGradTop, _currentGradBot);
            AddPaint(_backgroundShape!);

            // Light border (highlight) — offset down by 1.5 for 3D emboss
            _borderLightShape = Shape.Gen();
            _borderLightShape!.StrokeWidth(1f);
            if (_customBackground.HasValue)
                _borderLightShape.StrokeFill(0, 0, 0, 0);
            else
                _borderLightShape.StrokeFill(theme.BorderLight.R8, theme.BorderLight.G8, theme.BorderLight.B8, theme.BorderLight.A8);
            _borderLightShape.SetFill(0, 0, 0, 0);
            AddPaint(_borderLightShape);

            // Dark border (shadow)
            _borderDarkShape = Shape.Gen();
            _borderDarkShape!.StrokeWidth(1f);
            if (_customBackground.HasValue)
                _borderDarkShape.StrokeFill(0, 0, 0, 0);
            else
                _borderDarkShape.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, theme.BorderDark.A8);
            _borderDarkShape.SetFill(0, 0, 0, 0);
            AddPaint(_borderDarkShape);

            // Text shadow
            _shadowText = ThorVG.Text.Gen();
            _shadowText!.SetFont(style.FontName);
            _shadowText.SetFontSize(EffectiveFontSize(style.FontSize));
            _shadowText.SetText(_text);
            if (_customColor.HasValue)
                _shadowText.SetFill(_customColor.Value.R8, _customColor.Value.G8, _customColor.Value.B8);
            else
                _shadowText.SetFill(theme.TextColorShadow.R8, theme.TextColorShadow.G8, theme.TextColorShadow.B8);

            // Label text
            _labelText = ThorVG.Text.Gen();
            _labelText!.SetFont(style.FontName);
            _labelText.SetFontSize(EffectiveFontSize(style.FontSize));
            _labelText.SetText(_text);
            if (_customColor.HasValue)
                _labelText.SetFill(_customColor.Value.R8, _customColor.Value.G8, _customColor.Value.B8);
            else
                _labelText.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);

            _shadowScene = Scene.Gen()!;
            _shadowScene.Add(_shadowText);
            AddPaint(_shadowScene);

            _labelScene = Scene.Gen()!;
            _labelScene.Add(_labelText);
            AddPaint(_labelScene);
        }
    }

    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        var pad = Style.Padding;
        float padX = pad.Left, padY = pad.Top;
        if (Children.Count > 0)
        {
            float maxW = 0, maxH = 0;
            foreach (var child in Children)
            {
                if (!child.Visible) continue;
                var sz = child.Measure(availableWidth - 2 * padX, availableHeight - 2 * padY);
                if (sz.X > maxW) maxW = sz.X;
                if (sz.Y > maxH) maxH = sz.Y;
            }
            return new Vector2(maxW + 2 * padX, maxH + 2 * padY);
        }

        var style = Style;
        float textW = 0, textH = style.FontSize;

        if (_labelText != null && !string.IsNullOrEmpty(_text))
        {
            _labelText.Bounds(out _, out _, out float bw, out float bh);
            if (bw > 0) textW = bw;
            if (bh > 0) textH = bh;
        }

        return new Vector2(textW + 2 * padX, textH + 2 * padY);
    }

    protected override void OnSizeChanged()
    {
        RebuildShapes(false);
        if (Children.Count > 0)
        {
            _shadowScene?.Visible(false);
            _labelScene?.Visible(false);
            ArrangeChildren();
        }
        else
        {
            _shadowScene?.Visible(true);
            _labelScene?.Visible(true);
            PositionLabel();
        }
    }

    private void ArrangeChildren()
    {
        var pad = Style.Padding;
        float padX = pad.Left, padY = pad.Top;
        float contentW = Bounds.W - 2 * padX;
        float contentH = Bounds.H - 2 * padY;
        foreach (var child in Children)
        {
            if (!child.Visible) continue;
            child.Measure(contentW, contentH);
            child.Arrange(new RectF(padX, padY, contentW, contentH));
        }
    }

    private void RebuildShapes(bool pressed)
    {
        float w = Bounds.W, h = Bounds.H;
        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        float r = theme.ButtonCornerRadius;

        // Background: inset by 1px
        _backgroundShape?.ResetShape();
        _backgroundShape?.AppendRect(1, 1, w - 2, h - 2, r - 1, r - 1);

        // Light border: shifts up when pressed for emboss effect
        float lightY = pressed ? 0.5f : 1.5f;
        float lightH = h - 1 - (pressed ? 0f : 1f);
        _borderLightShape?.ResetShape();
        _borderLightShape?.AppendRect(0.5f, lightY, w - 1, lightH, r, r);

        // Dark border
        _borderDarkShape?.ResetShape();
        _borderDarkShape?.AppendRect(0.5f, 0.5f, w - 1, h - 2, r, r);
    }

    private void PositionLabel()
    {
        if (_labelScene == null || string.IsNullOrEmpty(_text)) return;

        // Measure with a fresh Text to avoid scene transforms polluting bounds
        var m = ThorVG.Text.Gen();
        if (m == null) return;
        m.SetFont(Style.FontName);
        m.SetFontSize(EffectiveFontSize(Style.FontSize));
        m.SetText(_text);
        m.Bounds(out float bx, out float by, out float bw, out float bh);
        if (bw <= 0 || bh <= 0) return;

        float padX = Style.Padding.Left;
        float x;
        switch (_textAlign)
        {
            case Alignment.Start:
                x = padX - bx;
                break;
            case Alignment.End:
                x = Bounds.W - padX - bw - bx;
                break;
            default: // Center, Stretch
                x = (Bounds.W - bw) / 2f - bx;
                break;
        }
        float y = (Bounds.H - bh) / 2f - by;

        _shadowScene?.Translate(x, y - 1);
        _labelScene.Translate(x, y);
    }

    protected override void OnStateChanged(ElementState oldState, ElementState newState)
    {
        var theme = OwnerWindow?.Theme ?? Theme.Dark;

        Color4 gradTop, gradBot;
        if (_customBackground.HasValue)
        {
            var bg = _customBackground.Value;
            if ((newState & ElementState.Disabled) != 0)
            {
                gradTop = gradBot = Color4.Lerp(bg, new Color4(0.5f, 1f), 0.5f);
            }
            else if ((newState & ElementState.Pressed) != 0)
            {
                gradTop = gradBot = Color4.Lerp(bg, Color4.Black, 0.2f);
            }
            else if ((newState & ElementState.Hovered) != 0)
            {
                gradTop = gradBot = Color4.Lerp(bg, Color4.White, 0.15f);
            }
            else
            {
                gradTop = gradBot = bg;
            }
        }
        else if ((newState & ElementState.Disabled) != 0)
        {
            gradTop = Color4.Lerp(theme.ButtonGradientTopUnfocused, new Color4(0.5f, 1f), 0.5f);
            gradBot = Color4.Lerp(theme.ButtonGradientBotUnfocused, new Color4(0.5f, 1f), 0.5f);
        }
        else if ((newState & ElementState.Pressed) != 0)
        {
            gradTop = theme.ButtonGradientTopPushed;
            gradBot = theme.ButtonGradientBotPushed;
        }
        else if ((newState & ElementState.Hovered) != 0)
        {
            gradTop = theme.ButtonGradientTopFocused;
            gradBot = theme.ButtonGradientBotFocused;
        }
        else
        {
            gradTop = theme.ButtonGradientTopUnfocused;
            gradBot = theme.ButtonGradientBotUnfocused;
        }

        // Animate gradient only when entering hover (not leaving or pressing)
        var animator = OwnerWindow?.Animator;
        bool enteringHover = (newState & ElementState.Hovered) != 0 &&
                             (oldState & ElementState.Hovered) == 0 &&
                             (newState & ElementState.Pressed) == 0;
        if (animator != null && enteringHover)
        {
            var fromTop = _currentGradTop;
            var fromBot = _currentGradBot;
            animator.Cancel(this);
            animator.Start(new Animation
            {
                Duration = 0.15f,
                Easing = Easings.EaseOutCubic,
                Tag = this,
                Apply = t =>
                {
                    _currentGradTop = Color4.Lerp(fromTop, gradTop, t);
                    _currentGradBot = Color4.Lerp(fromBot, gradBot, t);
                    ApplyButtonGradient(_currentGradTop, _currentGradBot);
                }
            });
        }
        else
        {
            animator?.Cancel(this);
            _currentGradTop = gradTop;
            _currentGradBot = gradBot;
            ApplyButtonGradient(gradTop, gradBot);
        }

        bool pressed = (newState & ElementState.Pressed) != 0;
        RebuildShapes(pressed);
        PositionLabel();

        // Update text color
        if (_customColor.HasValue)
        {
            _labelText?.SetFill(_customColor.Value.R8, _customColor.Value.G8, _customColor.Value.B8);
        }
        else
        {
            var textColor = (newState & ElementState.Disabled) != 0 ? theme.DisabledTextColor : theme.TextColor;
            _labelText?.SetFill(textColor.R8, textColor.G8, textColor.B8);
        }

        // Focus border highlight (skip for custom-colored buttons)
        if (!_customBackground.HasValue)
        {
            var style = Style;
            if ((newState & ElementState.Focused) != 0)
                _borderDarkShape?.StrokeFill(style.BorderFocused.R8, style.BorderFocused.G8, style.BorderFocused.B8, style.BorderFocused.A8);
            else
                _borderDarkShape?.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, theme.BorderDark.A8);
        }

        MarkDirty();
    }

    private void ApplyButtonGradient(Color4 top, Color4 bot)
    {
        if (_backgroundShape == null) return;
        float h = Bounds.H > 0 ? Bounds.H : 30;
        var grad = LinearGradient.Gen();
        grad.Linear(0, 0, 0, h);
        grad.SetColorStops(new Fill.ColorStop[]
        {
            new(0, top.R8, top.G8, top.B8, top.A8),
            new(1, bot.R8, bot.G8, bot.B8, bot.A8)
        }, 2);
        _backgroundShape.SetFill(grad);
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

    public override void OnClick()
    {
        if (!Enabled) return;
        Clicked?.Invoke();
    }

    public override void OnKeyDown(int key, int mods, bool repeat)
    {
        // Enter or Space activates the button
        if (key == 257 /* GLFW_KEY_ENTER */ || key == 32 /* GLFW_KEY_SPACE */)
        {
            OnClick();
            return;
        }
        base.OnKeyDown(key, mods, repeat);
    }

    protected override Style GetDefaultStyle()
    {
        return OwnerWindow?.Theme.Button ?? new Style();
    }

    protected override void OnThemeChanged()
    {
        if (!_shapesCreated) return;
        var theme = OwnerWindow?.Theme ?? Theme.Dark;

        // Cancel in-flight gradient animation
        OwnerWindow?.Animator?.Cancel(this);

        if (!_customBackground.HasValue)
        {
            _borderLightShape?.StrokeFill(theme.BorderLight.R8, theme.BorderLight.G8, theme.BorderLight.B8, theme.BorderLight.A8);
            _borderDarkShape?.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, theme.BorderDark.A8);
        }

        if (!_customColor.HasValue)
        {
            _shadowText?.SetFill(theme.TextColorShadow.R8, theme.TextColorShadow.G8, theme.TextColorShadow.B8);
            _labelText?.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);
        }

        // Apply correct gradient for current state
        if (_customBackground.HasValue)
        {
            _currentGradTop = _currentGradBot = _customBackground.Value;
        }
        else if ((State & ElementState.Hovered) != 0)
        {
            _currentGradTop = theme.ButtonGradientTopFocused;
            _currentGradBot = theme.ButtonGradientBotFocused;
        }
        else
        {
            _currentGradTop = theme.ButtonGradientTopUnfocused;
            _currentGradBot = theme.ButtonGradientBotUnfocused;
        }

        ApplyButtonGradient(_currentGradTop, _currentGradBot);
        MarkDirty();
    }

}
