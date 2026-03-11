using System;
using System.Numerics;
using ThorVG;

namespace Marius.Winter;

/// <summary>
/// A control that renders a raster image (PNG, JPEG, WebP) from a byte array.
/// </summary>
public class Image : Element
{
    private Picture? _picture;
    private byte[]? _source;
    private string _mimeType = "png";
    private float _intrinsicW, _intrinsicH;
    private float _requestedWidth, _requestedHeight;
    private bool _shapesCreated;

    public Image()
    {
    }

    /// <summary>Raw image bytes (PNG, JPEG, WebP).</summary>
    public byte[]? Source
    {
        get => _source;
        set
        {
            _source = value;
            if (_shapesCreated)
                ReloadImage();
        }
    }

    /// <summary>MIME type hint: "png", "jpg", "webp". Default is "png".</summary>
    public string MimeType
    {
        get => _mimeType;
        set
        {
            if (_mimeType == value) return;
            _mimeType = value;
            if (_shapesCreated)
                ReloadImage();
        }
    }

    /// <summary>Explicit width. 0 = use intrinsic image width.</summary>
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

    /// <summary>Explicit height. 0 = use intrinsic image height.</summary>
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

    /// <summary>
    /// Atomically set source bytes and MIME type, then reload the image once.
    /// Used by Blazor handler to avoid attribute-ordering bugs.
    /// </summary>
    public void SetSource(byte[]? source, string mimeType)
    {
        _source = source;
        _mimeType = mimeType;
        if (_shapesCreated)
            ReloadImage();
    }

    protected override void OnAttached()
    {
        if (!_shapesCreated)
        {
            _shapesCreated = true;
            ReloadImage();
        }
    }

    private void ReloadImage()
    {
        if (_picture != null)
        {
            RemovePaint(_picture);
            _picture = null;
        }

        _intrinsicW = _intrinsicH = 0;

        if (_source == null || _source.Length == 0) return;

        var picture = Picture.Gen();
        if (picture.Load(_source, (uint)_source.Length, _mimeType, null, true) != Result.Success)
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
        if (_picture == null || _intrinsicW <= 0 || _intrinsicH <= 0) return;

        float targetW = _requestedWidth > 0 ? _requestedWidth : (Bounds.W > 0 ? Bounds.W : _intrinsicW);
        float targetH = _requestedHeight > 0 ? _requestedHeight : (Bounds.H > 0 ? Bounds.H : _intrinsicH);

        if (targetW <= 0 || targetH <= 0) return;

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
