using System;
using System.Collections.Generic;

namespace Marius.Winter;

public class Animation
{
    public float Duration;
    public float Elapsed;
    public Func<float, float> Easing = Easings.Linear;
    public Action<float>? Apply;
    public Action? OnComplete;
    public object? Tag; // for cancellation by property key
}

public class AnimationManager
{
    private readonly List<Animation> _active = new();
    private bool _hasActiveAnimations;

    public bool HasActiveAnimations => _hasActiveAnimations;

    public void Start(Animation anim)
    {
        _active.Add(anim);
        _hasActiveAnimations = true;
    }

    public void Cancel(object tag)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (_active[i].Tag == tag)
                _active.RemoveAt(i);
        }
        _hasActiveAnimations = _active.Count > 0;
    }

    public void CancelAll()
    {
        _active.Clear();
        _hasActiveAnimations = false;
    }

    public void Tick(float dt)
    {
        if (_active.Count == 0) return;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var a = _active[i];
            a.Elapsed += dt;
            float t = Math.Clamp(a.Elapsed / a.Duration, 0f, 1f);
            float eased = a.Easing(t);
            a.Apply?.Invoke(eased);

            if (t >= 1f)
            {
                a.OnComplete?.Invoke();
                _active.RemoveAt(i);
            }
        }

        _hasActiveAnimations = _active.Count > 0;
    }
}

public static class Easings
{
    public static float Linear(float t) => t;
    public static float EaseIn(float t) => t * t;
    public static float EaseOut(float t) => t * (2f - t);
    public static float EaseInOut(float t) => t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;
    public static float EaseInCubic(float t) => t * t * t;
    public static float EaseOutCubic(float t) { t -= 1f; return t * t * t + 1f; }
    public static float EaseInOutCubic(float t) => t < 0.5f ? 4f * t * t * t : (t - 1f) * (2f * t - 2f) * (2f * t - 2f) + 1f;
}
