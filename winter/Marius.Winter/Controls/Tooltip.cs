using System;
using System.Text;
using ThorVG;

namespace Marius.Winter;

/// <summary>
/// Tooltip content that can include text, SVG, or both.
/// </summary>
public record TooltipContent(string? Text = null, string? Svg = null);

/// <summary>
/// Internal overlay element that renders tooltip content near the cursor.
/// </summary>
internal class TooltipOverlay : Element
{
    private Shape? _backgroundShape;
    private Shape? _borderShape;
    private Text? _textPaint;
    private Picture? _svgPaint;
    private Scene? _svgScene; // wrapper for SVG positioning
    private bool _shapesCreated;
    private float _svgW, _svgH;
    private float _contentW, _contentH;
    private float _textW, _textH;
    private float _textBx, _textBy; // text bounds origin offset

    private readonly string? _text;
    private readonly string? _svg;

    public TooltipOverlay(object tooltipData)
    {
        if (tooltipData is string s)
        {
            _text = s;
        }
        else if (tooltipData is TooltipContent tc)
        {
            _text = tc.Text;
            _svg = tc.Svg;
        }
    }

    /// <summary>
    /// Measures content and positions the overlay near the given cursor position,
    /// clamped to stay within window bounds.
    /// </summary>
    public void PositionNear(float cursorX, float cursorY, float windowW, float windowH, Theme theme)
    {
        EnsureShapes(theme);

        var style = theme.Tooltip;
        float padH = style.Padding.HorizontalTotal;
        float padV = style.Padding.VerticalTotal;

        float totalW = _contentW + padH;
        float totalH = _contentH + padV;

        // Position below and to the right of cursor
        float x = cursorX + 12;
        float y = cursorY + 18;

        // Clamp to window bounds
        if (x + totalW > windowW - 4) x = windowW - 4 - totalW;
        if (y + totalH > windowH - 4) y = cursorY - totalH - 4; // flip above cursor
        if (x < 4) x = 4;
        if (y < 4) y = 4;

        Arrange(new RectF(x, y, totalW, totalH));
    }

    private void EnsureShapes(Theme theme)
    {
        if (_shapesCreated) return;
        _shapesCreated = true;

        var style = theme.Tooltip;

        // Background
        _backgroundShape = Shape.Gen();
        _backgroundShape!.SetFill(style.Background.R8, style.Background.G8, style.Background.B8, style.Background.A8);
        AddPaint(_backgroundShape);

        // Border
        _borderShape = Shape.Gen();
        _borderShape!.StrokeWidth(style.BorderWidth);
        _borderShape.StrokeFill(style.Border.R8, style.Border.G8, style.Border.B8, style.Border.A8);
        _borderShape.SetFill(0, 0, 0, 0);
        AddPaint(_borderShape);

        // SVG content
        if (!string.IsNullOrEmpty(_svg))
        {
            var picture = Picture.Gen();
            var bytes = Encoding.UTF8.GetBytes(_svg);
            if (picture.Load(bytes, (uint)bytes.Length, "svg", null, true) == Result.Success)
            {
                picture.GetSize(out float iw, out float ih);
                // Constrain SVG height to match font-based content
                float maxSvgH = MathF.Max(style.FontSize * 2, 48);
                if (ih > maxSvgH && ih > 0)
                {
                    float scale = maxSvgH / ih;
                    picture.SetSize(iw * scale, ih * scale);
                    _svgW = iw * scale;
                    _svgH = ih * scale;
                }
                else
                {
                    _svgW = iw;
                    _svgH = ih;
                }

                _svgPaint = picture;
                _svgScene = Scene.Gen()!;
                _svgScene.Add(_svgPaint);
                AddPaint(_svgScene);
            }
        }

        // Text content — measure with a separate temp Text to get reliable bounds
        if (!string.IsNullOrEmpty(_text))
        {
            float fontSize = EffectiveFontSize(style.FontSize);

            // Measure
            var measure = ThorVG.Text.Gen();
            measure!.SetFont(style.FontName);
            measure.SetFontSize(fontSize);
            measure.SetText(_text);
            measure.Bounds(out _textBx, out _textBy, out float bw, out float bh);
            _textW = bw > 0 ? bw : 0;
            _textH = bh > 0 ? bh : style.FontSize;

            // Create the actual paint
            _textPaint = ThorVG.Text.Gen();
            _textPaint!.SetFont(style.FontName);
            _textPaint.SetFontSize(fontSize);
            _textPaint.SetText(_text);
            _textPaint.SetFill(style.Foreground.R8, style.Foreground.G8, style.Foreground.B8);
            AddPaint(_textPaint);
        }

        // Layout: SVG left, text right (if both present)
        float gap = (_svgW > 0 && _textW > 0) ? 6 : 0;
        _contentW = _svgW + gap + _textW;
        _contentH = MathF.Max(_svgH, _textH);
    }

    protected override void OnSizeChanged()
    {
        if (!_shapesCreated) return;

        float w = Bounds.W, h = Bounds.H;
        var style = (OwnerWindow?.Theme ?? Theme.Dark).Tooltip;
        float r = style.CornerRadius;

        _backgroundShape?.ResetShape();
        _backgroundShape?.AppendRect(0, 0, w, h, r, r);

        _borderShape?.ResetShape();
        _borderShape?.AppendRect(0.5f, 0.5f, w - 1, h - 1, r, r);

        // Position content inside padding
        float padL = style.Padding.Left;
        float padT = style.Padding.Top;
        float innerH = h - style.Padding.VerticalTotal;

        float xOff = padL;

        // SVG positioning
        if (_svgScene != null)
        {
            float svgY = padT + (innerH - _svgH) / 2f;
            _svgScene.Translate(padL, svgY);
            xOff += _svgW + (_textPaint != null ? 6 : 0);
        }

        // Text positioning — use Translate on the paint directly
        if (_textPaint != null)
        {
            float textY = padT + (innerH - _textH) / 2f - _textBy;
            _textPaint.Translate(xOff - _textBx, textY);
        }
    }

    public override Element? HitTest(float x, float y) => null; // tooltips are not interactive

    protected override Style GetDefaultStyle()
    {
        return (OwnerWindow?.Theme ?? Theme.Dark).Tooltip;
    }
}
