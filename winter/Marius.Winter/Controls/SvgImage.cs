using System;
using System.Numerics;
using System.Text;
using ThorVG;

namespace Marius.Winter;

/// <summary>
/// A control that renders an SVG string using ThorVG's Picture loader.
/// </summary>
public class SvgImage : Element
{
    private Picture? _picture;
    private string? _svg;
    private float _intrinsicW, _intrinsicH;
    private float _requestedWidth, _requestedHeight;
    private bool _shapesCreated;

    public SvgImage(string? svg = null)
    {
        _svg = svg;
    }

    public string? Svg
    {
        get => _svg;
        set
        {
            if (_svg == value) return;
            _svg = value;
            if (_shapesCreated)
                ReloadSvg();
        }
    }

    /// <summary>Explicit width. 0 = use intrinsic SVG width.</summary>
    public float RequestedWidth
    {
        get => _requestedWidth;
        set
        {
            if (_requestedWidth == value) return;
            _requestedWidth = value;
            InvalidateMeasure();
            if (_shapesCreated) ResizePicture();
            MarkDirty();
        }
    }

    /// <summary>Explicit height. 0 = use intrinsic SVG height.</summary>
    public float RequestedHeight
    {
        get => _requestedHeight;
        set
        {
            if (_requestedHeight == value) return;
            _requestedHeight = value;
            InvalidateMeasure();
            if (_shapesCreated) ResizePicture();
            MarkDirty();
        }
    }

    protected override void OnAttached()
    {
        if (!_shapesCreated)
        {
            _shapesCreated = true;
            ReloadSvg();
        }
    }

    private void ReloadSvg()
    {
        if (_picture != null)
        {
            RemovePaint(_picture);
            _picture = null;
        }

        _intrinsicW = _intrinsicH = 0;

        if (string.IsNullOrEmpty(_svg)) return;

        var picture = Picture.Gen();
        var bytes = Encoding.UTF8.GetBytes(_svg);
        if (picture.Load(bytes, (uint)bytes.Length, "svg", null, true) != Result.Success)
            return;

        picture.GetSize(out _intrinsicW, out _intrinsicH);
        _picture = picture;
        AddPaint(_picture);

        ResizePicture();
        InvalidateMeasure();
        MarkDirty();
    }

    private void ResizePicture()
    {
        if (_picture == null) return;

        float targetW = Bounds.W > 0 ? Bounds.W : (_requestedWidth > 0 ? _requestedWidth : _intrinsicW);
        float targetH = Bounds.H > 0 ? Bounds.H : (_requestedHeight > 0 ? _requestedHeight : _intrinsicH);

        if (targetW <= 0 || targetH <= 0 || _intrinsicW <= 0 || _intrinsicH <= 0)
            return;

        // Scale to fit while preserving aspect ratio
        float scaleX = targetW / _intrinsicW;
        float scaleY = targetH / _intrinsicH;
        float scale = MathF.Min(scaleX, scaleY);

        _picture.SetSize(_intrinsicW * scale, _intrinsicH * scale);
    }

    protected override void OnSizeChanged()
    {
        ResizePicture();
    }

    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        float w = _requestedWidth > 0 ? _requestedWidth : _intrinsicW;
        float h = _requestedHeight > 0 ? _requestedHeight : _intrinsicH;
        return new Vector2(w, h);
    }

    protected override Style GetDefaultStyle()
    {
        return OwnerWindow?.Theme.Label ?? new Style();
    }
}
