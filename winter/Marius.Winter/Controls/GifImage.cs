using System;
using System.Numerics;
using ThorVG;

namespace Marius.Winter;

/// <summary>
/// A control that decodes and plays an animated GIF.
/// Frames are decoded once at load time, then cycled via the AnimationManager.
/// </summary>
public class GifImage : Element
{
    private GifDecoder.GifResult? _gif;
    private Picture? _picture;
    private float _intrinsicW, _intrinsicH;
    private float _requestedWidth, _requestedHeight;
    private bool _shapesCreated;
    private bool _playing;
    private int _currentFrame;
    private float _lastTickElapsed; // total elapsed at last tick
    private float _frameAccum;      // time accumulated in current frame
    private Animation? _tickAnim;

    /// <summary>Load from raw GIF bytes.</summary>
    public void LoadFromBytes(byte[]? data)
    {
        Unload();
        if (data == null || data.Length == 0) return;

        var gif = GifDecoder.Decode(data);
        if (gif == null || gif.Frames.Count == 0) return;

        Setup(gif);
    }

    /// <summary>Load from file path.</summary>
    public void LoadFromPath(string? path)
    {
        Unload();
        if (string.IsNullOrEmpty(path)) return;

        var gif = GifDecoder.Decode(path);
        if (gif == null || gif.Frames.Count == 0) return;

        Setup(gif);
    }

    /// <summary>Whether the animation is currently playing.</summary>
    public bool IsPlaying
    {
        get => _playing;
        set
        {
            if (_playing == value) return;
            _playing = value;
            if (_playing) StartTicking();
            else StopTicking();
        }
    }

    public int FrameCount => _gif?.Frames.Count ?? 0;
    public int CurrentFrameIndex => _currentFrame;

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
            // If data was loaded before attachment, add the picture now
            if (_picture != null)
            {
                AddPaint(_picture);
                ResizePicture();
            }
            if (_gif != null && _gif.Frames.Count > 1 && _playing)
                StartTicking();
        }
    }

    protected override void OnDetaching()
    {
        StopTicking();
    }

    private void Setup(GifDecoder.GifResult gif)
    {
        _gif = gif;
        _intrinsicW = gif.Width;
        _intrinsicH = gif.Height;
        _currentFrame = 0;
        _lastTickElapsed = 0;
        _frameAccum = 0;

        ShowFrame(0);

        InvalidateMeasure();
        MarkDirty();

        if (gif.Frames.Count > 1)
            IsPlaying = true;
    }

    private void ShowFrame(int index)
    {
        if (_gif == null || index < 0 || index >= _gif.Frames.Count) return;

        var frame = _gif.Frames[index];
        var w = (uint)_gif.Width;
        var h = (uint)_gif.Height;

        if (_picture != null && _shapesCreated)
            RemovePaint(_picture);

        var pic = Picture.Gen();
        if (pic.Load(frame.Pixels, w, h, ColorSpace.ARGB8888) == Result.Success)
        {
            _picture = pic;
            if (_shapesCreated)
            {
                AddPaint(_picture);
                ResizePicture();
            }
        }
    }

    private void Unload()
    {
        StopTicking();
        _playing = false;

        if (_picture != null && _shapesCreated)
            RemovePaint(_picture);

        _gif = null;
        _picture = null;
        _intrinsicW = _intrinsicH = 0;
        _currentFrame = 0;
        _lastTickElapsed = 0;
        _frameAccum = 0;

        InvalidateMeasure();
        MarkDirty();
    }

    private void StartTicking()
    {
        if (_tickAnim != null || _gif == null || _gif.Frames.Count <= 1) return;
        if (OwnerWindow == null) return; // defer until OnAttached

        _lastTickElapsed = 0;
        _frameAccum = 0;
        _tickAnim = new Animation
        {
            Duration = float.MaxValue,
            Apply = OnTick,
            Tag = this,
        };
        OwnerWindow.Animator.Start(_tickAnim);
    }

    private void StopTicking()
    {
        if (_tickAnim == null) return;
        OwnerWindow?.Animator.Cancel(this);
        _tickAnim = null;
    }

    private void OnTick(float _)
    {
        if (_gif == null || !_playing || _gif.Frames.Count <= 1) return;

        float elapsed = _tickAnim!.Elapsed;
        float dt = elapsed - _lastTickElapsed;
        _lastTickElapsed = elapsed;

        _frameAccum += dt;

        var frame = _gif.Frames[_currentFrame];
        if (_frameAccum >= frame.DelaySeconds)
        {
            _frameAccum -= frame.DelaySeconds;
            _currentFrame = (_currentFrame + 1) % _gif.Frames.Count;
            ShowFrame(_currentFrame);
            MarkDirty();
        }
    }

    private void ResizePicture()
    {
        if (_picture == null || _intrinsicW <= 0 || _intrinsicH <= 0) return;

        float targetW = Bounds.W > 0 ? Bounds.W : (_requestedWidth > 0 ? _requestedWidth : _intrinsicW);
        float targetH = Bounds.H > 0 ? Bounds.H : (_requestedHeight > 0 ? _requestedHeight : _intrinsicH);

        if (targetW <= 0 || targetH <= 0) return;

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
