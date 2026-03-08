using System.Numerics;
using ThorVG;

namespace Marius.Winter;

public class Label : Element
{
    private Text? _textPaint;
    private Scene? _textScene;
    private string _text = "";
    private float _fontSize = 14f;
    private bool _fontSizeSet;
    private Color4 _color;
    private bool _colorSet;
    private bool _italic;
    private bool _bold;
    private TextWrap _textWrapping = TextWrap.None;
    private bool _shapesCreated;

    public Label(string text = "")
    {
        _text = text;
    }

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            if (_textPaint != null)
            {
                _textPaint.SetText(value);
                InvalidateMeasure();
                MarkDirty();
            }
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
            _colorSet = true;
            if (_color.R == value.R && _color.G == value.G && _color.B == value.B && _color.A == value.A) return;
            _color = value;
            _textPaint?.SetFill(value.R8, value.G8, value.B8);
            MarkDirty();
        }
    }

    public bool Italic
    {
        get => _italic;
        set
        {
            if (_italic == value) return;
            _italic = value;
            _textPaint?.SetItalic(value ? 0.18f : 0f);
            MarkDirty();
        }
    }

    public bool Bold
    {
        get => _bold;
        set
        {
            if (_bold == value) return;
            _bold = value;
            _textPaint?.SetFont(value ? "default-bold" : Style.FontName);
            InvalidateMeasure();
            MarkDirty();
        }
    }

    public TextWrap TextWrapping
    {
        get => _textWrapping;
        set
        {
            if (_textWrapping == value) return;
            _textWrapping = value;
            if (_textPaint != null)
            {
                _textPaint.SetWrapping(value);
                InvalidateMeasure();
                MarkDirty();
            }
        }
    }

    protected override void OnAttached()
    {
        if (!_shapesCreated)
        {
            _shapesCreated = true;
            var theme = OwnerWindow?.Theme ?? Theme.Dark;
            var style = Style;

            // Only apply theme/style defaults if the value wasn't explicitly set
            // before attachment (e.g. via Blazor attribute or property setter).
            if (!_colorSet)
                _color = theme.TextColor;
            if (!_fontSizeSet)
                _fontSize = style.FontSize;

            _textPaint = ThorVG.Text.Gen();
            _textPaint!.SetFont(_bold ? "default-bold" : style.FontName);
            _textPaint.SetFontSize(EffectiveFontSize(_fontSize));
            _textPaint.SetText(_text);
            _textPaint.SetFill(_color.R8, _color.G8, _color.B8);
            if (_italic)
                _textPaint.SetItalic(0.18f);
            if (_textWrapping != TextWrap.None)
                _textPaint.SetWrapping(_textWrapping);
            _textScene = Scene.Gen()!;
            _textScene.Add(_textPaint);
            AddPaint(_textScene);
        }
    }

    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        if (_textPaint == null || string.IsNullOrEmpty(_text))
            return new Vector2(0, _fontSize);

        // Use a temporary Text for measurement to avoid world-space transform pollution
        var m = ThorVG.Text.Gen();
        if (m == null) return new Vector2(0, _fontSize);
        m.SetFont(_bold ? "default-bold" : Style.FontName);
        m.SetFontSize(EffectiveFontSize(_fontSize));
        if (_textWrapping != TextWrap.None && availableWidth > 0 && !float.IsInfinity(availableWidth))
        {
            m.SetWrapping(_textWrapping);
            m.SetLayout(availableWidth, 0);
            m.SetText(_text);
            m.GetTextSize(out _, out float wh);
            return new Vector2(availableWidth, wh > 0 ? wh : _fontSize);
        }
        m.SetText(_text);
        m.Bounds(out _, out _, out float bw, out float bh);
        return new Vector2(bw > 0 ? bw : 0, bh > 0 ? bh : _fontSize);
    }

    protected override void OnSizeChanged()
    {
        if (_textPaint == null || string.IsNullOrEmpty(_text)) return;

        if (_textWrapping != TextWrap.None)
        {
            _textPaint.SetLayout(Bounds.W, Bounds.H);
            // Re-set text to trigger reshaping with the new layout box
            _textPaint.SetText(_text);
        }

        // Use a temporary Text to measure local bounds (not affected by scene transforms).
        // The (bx, by) offset is the gap between the text origin and the visual glyph box.
        var m = ThorVG.Text.Gen();
        if (m == null) return;
        m.SetFont(_bold ? "default-bold" : Style.FontName);
        m.SetFontSize(EffectiveFontSize(_fontSize));
        if (_textWrapping != TextWrap.None)
        {
            m.SetWrapping(_textWrapping);
            m.SetLayout(Bounds.W, Bounds.H);
        }
        m.SetText(_text);
        m.Bounds(out float bx, out float by, out _, out _);
        _textScene?.Translate(-bx, -by);
    }

    protected override Style GetDefaultStyle()
    {
        return OwnerWindow?.Theme.Label ?? new Style();
    }

    protected override void OnThemeChanged()
    {
        if (!_shapesCreated || _colorSet) return;
        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        _color = theme.TextColor;
        _textPaint?.SetFill(_color.R8, _color.G8, _color.B8);
        MarkDirty();
    }

}
