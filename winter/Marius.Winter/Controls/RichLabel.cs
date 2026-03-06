using System;
using System.Numerics;
using System.Text;
using ThorVG;

namespace Marius.Winter;

/// <summary>
/// A label that can display either plain text or an SVG graphic.
/// Setting <see cref="Text"/> switches to text mode; setting <see cref="Svg"/> switches to SVG mode.
/// </summary>
public class RichLabel : Element
{
    private enum ContentMode { None, Text, Svg }

    private Text? _textPaint;
    private Picture? _svgPaint;
    private ContentMode _mode = ContentMode.None;

    private string _text = "";
    private string? _svg;
    private float _fontSize = 14f;
    private bool _fontSizeSet;
    private Color4 _color;
    private float _svgIntrinsicW, _svgIntrinsicH;
    private bool _shapesCreated;

    public RichLabel(string text = "")
    {
        _text = text;
        if (!string.IsNullOrEmpty(text))
            _mode = ContentMode.Text;
    }

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value && _mode == ContentMode.Text) return;
            _text = value;
            _svg = null;
            if (_shapesCreated)
                SwitchToText();
            else
                _mode = ContentMode.Text;
        }
    }

    public string? Svg
    {
        get => _svg;
        set
        {
            if (_svg == value && _mode == ContentMode.Svg) return;
            _svg = value;
            if (_shapesCreated)
                SwitchToSvg();
            else
                _mode = ContentMode.Svg;
        }
    }

    public float FontSize
    {
        get => _fontSize;
        set
        {
            _fontSizeSet = true;
            if (_fontSize == value) return;
            _fontSize = value;
            if (_textPaint != null)
            {
                _textPaint.SetFontSize(EffectiveFontSize(value));
                InvalidateMeasure();
                MarkDirty();
            }
        }
    }

    public Color4 Color
    {
        get => _color;
        set
        {
            if (_color.R == value.R && _color.G == value.G && _color.B == value.B && _color.A == value.A) return;
            _color = value;
            _textPaint?.SetFill(value.R8, value.G8, value.B8);
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

            if (_color.A == 0)
                _color = theme.TextColor;
            if (!_fontSizeSet)
                _fontSize = style.FontSize;

            if (_mode == ContentMode.Svg && !string.IsNullOrEmpty(_svg))
                SwitchToSvg();
            else if (!string.IsNullOrEmpty(_text))
                SwitchToText();
        }
    }

    private void SwitchToText()
    {
        // Remove SVG if present
        if (_svgPaint != null)
        {
            RemovePaint(_svgPaint);
            _svgPaint = null;
        }

        _mode = ContentMode.Text;

        if (_textPaint == null)
        {
            var style = Style;
            _textPaint = ThorVG.Text.Gen();
            _textPaint!.SetFont(style.FontName);
            _textPaint.SetFontSize(EffectiveFontSize(_fontSize));
            _textPaint.SetFill(_color.R8, _color.G8, _color.B8);
            AddPaint(_textPaint);
        }

        _textPaint.SetText(_text);
        InvalidateMeasure();
        MarkDirty();
    }

    private void SwitchToSvg()
    {
        // Remove text if present
        if (_textPaint != null)
        {
            RemovePaint(_textPaint);
            _textPaint = null;
        }

        // Remove old SVG
        if (_svgPaint != null)
        {
            RemovePaint(_svgPaint);
            _svgPaint = null;
        }

        _svgIntrinsicW = _svgIntrinsicH = 0;
        _mode = ContentMode.Svg;

        if (string.IsNullOrEmpty(_svg)) return;

        var picture = Picture.Gen();
        var bytes = Encoding.UTF8.GetBytes(_svg);
        if (picture.Load(bytes, (uint)bytes.Length, "svg", null, true) != Result.Success)
            return;

        picture.GetSize(out _svgIntrinsicW, out _svgIntrinsicH);
        _svgPaint = picture;
        AddPaint(_svgPaint);

        ResizeSvg();
        InvalidateMeasure();
        MarkDirty();
    }

    private void ResizeSvg()
    {
        if (_svgPaint == null || _svgIntrinsicW <= 0 || _svgIntrinsicH <= 0) return;

        float targetW = Bounds.W > 0 ? Bounds.W : _svgIntrinsicW;
        float targetH = Bounds.H > 0 ? Bounds.H : _svgIntrinsicH;

        float scaleX = targetW / _svgIntrinsicW;
        float scaleY = targetH / _svgIntrinsicH;
        float scale = MathF.Min(scaleX, scaleY);

        _svgPaint.SetSize(_svgIntrinsicW * scale, _svgIntrinsicH * scale);
    }

    protected override void OnSizeChanged()
    {
        if (_mode == ContentMode.Svg)
            ResizeSvg();
    }

    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        if (_mode == ContentMode.Text)
        {
            if (_textPaint == null || string.IsNullOrEmpty(_text))
                return new Vector2(0, _fontSize);

            _textPaint.Bounds(out float bx, out float by, out float bw, out float bh);
            return new Vector2(bw > 0 ? bw : 0, bh > 0 ? bh : _fontSize);
        }

        if (_mode == ContentMode.Svg)
            return new Vector2(_svgIntrinsicW, _svgIntrinsicH);

        return new Vector2(0, _fontSize);
    }

    protected override Style GetDefaultStyle()
    {
        return OwnerWindow?.Theme.Label ?? new Style();
    }

    protected override void OnThemeChanged()
    {
        if (!_shapesCreated) return;
        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        _color = theme.TextColor;
        _textPaint?.SetFill(_color.R8, _color.G8, _color.B8);
        MarkDirty();
    }
}
