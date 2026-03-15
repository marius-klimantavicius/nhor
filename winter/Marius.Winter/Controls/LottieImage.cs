using System;
using System.Numerics;
using System.Text;
using ThorVG;

namespace Marius.Winter;

/// <summary>
/// A control that loads and plays a Lottie animation using ThorVG's Animation class.
/// Renders directly via Picture, advancing frames each tick while playing.
/// </summary>
public class LottieImage : Element
{
    private ThorVG.Animation? _animation;
    private Picture? _picture;
    private float _intrinsicW, _intrinsicH;
    private float _requestedWidth, _requestedHeight;
    private bool _shapesCreated;
    private bool _playing;
    private float _elapsed;
    private float _totalFrames;
    private float _duration;
    private Animation? _tickAnim; // Window animation manager handle

    /// <summary>Lottie JSON string content.</summary>
    public string? Source
    {
        get => null; // write-only, we don't store the string
        set => LoadFromString(value);
    }

    /// <summary>Load from raw bytes (Lottie JSON).</summary>
    public void LoadFromBytes(byte[]? data)
    {
        Unload();
        if (data == null || data.Length == 0) return;

        var anim = ThorVG.Animation.Gen();
        var pic = anim.GetPicture();
        if (pic.Load(data, (uint)data.Length, "lottie+json", null, true) != Result.Success)
            return;

        SetupAnimation(anim, pic);
    }

    /// <summary>Load from file path.</summary>
    public void LoadFromPath(string? path)
    {
        Unload();
        if (string.IsNullOrEmpty(path)) return;

        var anim = ThorVG.Animation.Gen();
        var pic = anim.GetPicture();
        if (pic.Load(path) != Result.Success)
            return;

        SetupAnimation(anim, pic);
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

    public float TotalFrames => _totalFrames;
    public float Duration => _duration;
    public float CurrentFrame => _animation?.CurFrame() ?? 0;

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
            if (_animation != null && _totalFrames > 1 && _duration > 0 && _playing)
                StartTicking();
        }
    }

    protected override void OnDetaching()
    {
        StopTicking();
    }

    private void LoadFromString(string? json)
    {
        if (string.IsNullOrEmpty(json)) { Unload(); return; }
        var bytes = Encoding.UTF8.GetBytes(json);
        LoadFromBytes(bytes);
    }

    private void SetupAnimation(ThorVG.Animation anim, Picture pic)
    {
        _animation = anim;
        _picture = pic;

        pic.GetSize(out _intrinsicW, out _intrinsicH);
        _totalFrames = anim.TotalFrame();
        _duration = anim.Duration();
        _elapsed = 0;

        if (_shapesCreated)
        {
            AddPaint(_picture);
            ResizePicture();
        }

        InvalidateMeasure();
        MarkDirty();

        // Auto-play if there are multiple frames
        if (_totalFrames > 1 && _duration > 0)
            IsPlaying = true;
    }

    private void Unload()
    {
        StopTicking();
        _playing = false;

        if (_picture != null && _shapesCreated)
            RemovePaint(_picture);

        _animation = null;
        _picture = null;
        _intrinsicW = _intrinsicH = 0;
        _totalFrames = 0;
        _duration = 0;
        _elapsed = 0;

        InvalidateMeasure();
        MarkDirty();
    }

    private void StartTicking()
    {
        if (_tickAnim != null || _animation == null || _duration <= 0) return;
        if (OwnerWindow == null) return; // defer until OnAttached

        _tickAnim = new Animation
        {
            // Use a very long duration — we'll keep restarting it
            Duration = float.MaxValue,
            Apply = OnTick,
            Tag = this,
        };
        OwnerWindow?.Animator.Start(_tickAnim);
    }

    private void StopTicking()
    {
        if (_tickAnim == null) return;
        OwnerWindow?.Animator.Cancel(this);
        _tickAnim = null;
    }

    private void OnTick(float _)
    {
        if (_animation == null || _duration <= 0 || !_playing) return;

        // The AnimationManager sets Elapsed on the animation, which is
        // cumulative dt. We use that directly as our elapsed time.
        _elapsed = _tickAnim!.Elapsed;

        // Loop the animation
        float t = (_elapsed % _duration) / _duration;
        float frame = t * _totalFrames;

        _animation.Frame(frame);
        MarkDirty();
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
