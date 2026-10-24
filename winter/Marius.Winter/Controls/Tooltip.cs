using System;
using System.Text;
using ThorVG;

namespace Marius.Winter;

/// <summary>
/// Tooltip content with optional SVGs before and after the text.
/// </summary>
public record TooltipContent(string? Text = null, string? Svg = null)
{
    /// <summary>Optional SVG displayed after the text.</summary>
    public string? TrailingSvg { get; init; }
}

/// <summary>
/// Internal overlay element that renders tooltip content near the cursor.
/// </summary>
internal class TooltipOverlay : Element
{
    private Shape? _backgroundShape;
    private Shape? _borderShape;
    private Text? _textPaint;
    private Scene? _svgScene; // wrapper for SVG positioning
    private Scene? _trailingSvgScene;
    private bool _shapesCreated;
    private float _svgW, _svgH;
    private float _trailingSvgW, _trailingSvgH;
    private float _contentW, _contentH;
    private float _textW, _textH;
    private float _textBx, _textBy; // text bounds origin offset

    private readonly string? _text;
    private readonly string? _svg;
    private readonly string? _trailingSvg;

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
            _trailingSvg = tc.TrailingSvg;
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

        // Each SVG gets its own picture and positioning scene, even for identical data.
        _svgScene = LoadSvg(_svg, style.FontSize, out _svgW, out _svgH);
        _trailingSvgScene = LoadSvg(_trailingSvg, style.FontSize, out _trailingSvgW, out _trailingSvgH);

        // Text content — measure with a separate temp Text to get reliable bounds
        if (!string.IsNullOrEmpty(_text))
        {
            float fontSize = EffectiveFontSize(style.FontSize);

            // Measure
            var measure = ThorVG.Text.Gen();
            try
            {
                measure!.SetFont(style.FontName);
                measure.SetFontSize(fontSize);
                measure.SetText(_text);
                measure.Bounds(out _textBx, out _textBy, out float bw, out float bh);
                _textW = bw > 0 ? bw : 0;
                _textH = bh > 0 ? bh : style.FontSize;
            }
            finally { measure?.Unref(); }

            // Create the actual paint
            _textPaint = ThorVG.Text.Gen();
            _textPaint!.SetFont(style.FontName);
            _textPaint.SetFontSize(fontSize);
            _textPaint.SetText(_text);
            _textPaint.SetFill(style.Foreground.R8, style.Foreground.G8, style.Foreground.B8);
            AddPaint(_textPaint);
        }

        int parts = (_svgW > 0 ? 1 : 0) + (_textW > 0 ? 1 : 0) + (_trailingSvgW > 0 ? 1 : 0);
        _contentW = _svgW + _textW + _trailingSvgW + Math.Max(0, parts - 1) * 6;
        _contentH = MathF.Max(MathF.Max(_svgH, _textH), _trailingSvgH);
    }

    private Scene? LoadSvg(string? svg, float fontSize, out float width, out float height)
    {
        width = height = 0;
        if (string.IsNullOrEmpty(svg)) return null;

        var picture = Picture.Gen();
        try
        {
            var bytes = Encoding.UTF8.GetBytes(svg);
            if (picture.Load(bytes, (uint)bytes.Length, "svg", null, true) != Result.Success)
                return null;

            picture.GetSize(out width, out height);
            float maxSvgH = MathF.Max(fontSize * 2, 48);
            if (height > maxSvgH)
            {
                width *= maxSvgH / height;
                height = maxSvgH;
                picture.SetSize(width, height);
            }

            var scene = Scene.Gen();
            scene.Add(picture);
            AddPaint(scene);
            return scene;
        }
        finally
        {
            // A successfully loaded picture is owned by its scene; a failed
            // load still needs to release the unowned local picture.
            if (picture.Parent() == null) picture.Unref();
        }
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
            xOff += _svgW;
        }

        // Text positioning — use Translate on the paint directly
        if (_textPaint != null)
        {
            if (_svgW > 0 && _textW > 0) xOff += 6;
            float textY = padT + (innerH - _textH) / 2f - _textBy;
            _textPaint.Translate(xOff - _textBx, textY);
            xOff += _textW;
        }

        if (_trailingSvgScene != null)
        {
            if ((_svgW > 0 || _textW > 0) && _trailingSvgW > 0) xOff += 6;
            float svgY = padT + (innerH - _trailingSvgH) / 2f;
            _trailingSvgScene.Translate(xOff, svgY);
        }
    }

    public override Element? HitTest(float x, float y) => null; // tooltips are not interactive

    protected override Style GetDefaultStyle()
    {
        return (OwnerWindow?.Theme ?? Theme.Dark).Tooltip;
    }
}
