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
    private sealed class Entry(Animation animation) { internal readonly Animation Animation = animation; }
    private readonly List<Entry> _active = new();
    internal Action? Changed;

    public bool HasActiveAnimations => _active.Count != 0;

    public void Start(Animation anim)
    {
        ArgumentNullException.ThrowIfNull(anim);
        _active.RemoveAll(e => ReferenceEquals(e.Animation, anim));
        anim.Elapsed = 0;
        _active.Add(new Entry(anim));
        Changed?.Invoke();
    }

    public void Cancel(object tag)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (Equals(_active[i].Animation.Tag, tag))
                _active.RemoveAt(i);
        }
    }

    public void CancelAll()
    {
        _active.Clear();
    }

    /// <summary>Applies the start-of-tick set in insertion order. New starts wait
    /// until the next tick; cancellation suppresses completion. Nonpositive
    /// durations apply their final value once. Returns true for any applied frame.</summary>
    public bool Tick(float dt)
    {
        if (_active.Count == 0) return false;
        bool applied = false;

        foreach (var entry in _active.ToArray())
        {
            if (!_active.Contains(entry)) continue;
            var a = entry.Animation;
            a.Elapsed += Math.Max(0, dt);
            float t = a.Duration <= 0 ? 1 : Math.Clamp(a.Elapsed / a.Duration, 0f, 1f);
            float eased = a.Easing(t);
            applied = true;
            a.Apply?.Invoke(eased);

            if (t >= 1f && _active.Remove(entry))
            {
                a.OnComplete?.Invoke();
            }
        }

        return applied;
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
