using System;
using System.Numerics;
using ThorVG;

namespace Marius.Winter;

public class ProgressBar : Element
{
    private Shape? _backgroundShape;
    private Shape? _fillShape;
    private Shape? _borderShape;
    private float _value;
    private bool _shapesCreated;

    public ProgressBar(float value = 0f)
    {
        _value = Math.Clamp(value, 0f, 1f);
    }

    public float Value
    {
        get => _value;
        set
        {
            float clamped = Math.Clamp(value, 0f, 1f);
            if (_value == clamped) return;
            _value = clamped;
            UpdateFill();
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

            // Background — dark inset
            _backgroundShape = Shape.Gen();
            _backgroundShape!.SetFill(style.Background.R8, style.Background.G8, style.Background.B8, style.Background.A8);
            AddPaint(_backgroundShape);

            // Fill bar — lighter, shows progress
            _fillShape = Shape.Gen();
            _fillShape!.SetFill(style.Foreground.R8, style.Foreground.G8, style.Foreground.B8, style.Foreground.A8);
            AddPaint(_fillShape);

            // Border — thin dark outline
            _borderShape = Shape.Gen();
            _borderShape!.StrokeWidth(1f);
            _borderShape.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, theme.BorderDark.A8);
            _borderShape.SetFill(0, 0, 0, 0);
            AddPaint(_borderShape);
        }
    }

    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        // Nanogui: preferred size (70, 12)
        return new Vector2(70, 12);
    }

    protected override void OnSizeChanged()
    {
        float w = Bounds.W, h = Bounds.H;

        // Background
        _backgroundShape?.ResetShape();
        _backgroundShape?.AppendRect(1, 1, w - 2, h - 2, 3, 3);

        // Border
        _borderShape?.ResetShape();
        _borderShape?.AppendRect(0.5f, 0.5f, w - 1, h - 1, 3, 3);

        UpdateFill();
    }

    private void UpdateFill()
    {
        if (_fillShape == null) return;

        float w = Bounds.W, h = Bounds.H;
        float barWidth = Math.Max(0, (w - 4) * _value);

        _fillShape.ResetShape();
        if (barWidth > 0)
            _fillShape.AppendRect(2, 2, barWidth, h - 4, 2, 2);
    }

    protected override Style GetDefaultStyle()
    {
        return OwnerWindow?.Theme.ProgressBar ?? new Style();
    }

    protected override void OnThemeChanged()
    {
        if (!_shapesCreated) return;
        var style = Style;
        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        _backgroundShape?.SetFill(style.Background.R8, style.Background.G8, style.Background.B8, style.Background.A8);
        _fillShape?.SetFill(style.Foreground.R8, style.Foreground.G8, style.Foreground.B8, style.Foreground.A8);
        _borderShape?.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, theme.BorderDark.A8);
        MarkDirty();
    }
}
