using System;
using System.Collections.Generic;
using System.Linq;
using static Glfw.GLFW;

namespace Marius.Winter;

public class Form : Panel
{
    private bool _submitting;
    public Form() => Layout = new FlexLayout { Direction = Orientation.Vertical, AlignItems = Alignment.Stretch, Gap = 10 };
    public event Action? ValidSubmit;
    public event Action? InvalidSubmit;
    public event Action? ResetRequested;
    public event Action? SubmitRequested;
    public IEnumerable<Field> Fields => Descendants(this).OfType<Field>();
    private static IEnumerable<Element> Descendants(Element parent)
    {
        foreach (var child in parent.Children)
        {
            if (child is Form) continue;
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    public bool Submit()
    {
        if (IsDisposed || _submitting || !IsEffectivelyEnabled) return false;
        _submitting = true;
        try
        {
            var fields = Fields.Where(f => f.IsEffectivelyVisible && f.IsEffectivelyEnabled).ToArray();
            foreach (var field in fields) if (field.Validate != null) field.SetMessages(field.Validate());
            var invalid = fields.FirstOrDefault(f => f.Invalid);
            if (invalid != null) { invalid.FocusEditor(); InvalidSubmit?.Invoke(); return false; }
            ValidSubmit?.Invoke(); return true;
        }
        finally { _submitting = false; }
    }
    public void RequestSubmit()
    {
        if (IsDisposed || !IsEffectivelyEnabled) return;
        if (SubmitRequested != null) SubmitRequested.Invoke(); else Submit();
    }
    public void Reset()
    {
        if (IsDisposed) return;
        ResetRequested?.Invoke();
        foreach (var field in Fields.ToArray()) { field.Invalid = false; field.SetMessages(Array.Empty<string>()); }
    }
    public override void OnKeyDown(int key, int mods, bool repeat)
    {
        if (key is GLFW_KEY_ENTER or GLFW_KEY_KP_ENTER)
        {
            if (!repeat && (mods & (GLFW_MOD_CONTROL | GLFW_MOD_ALT | GLFW_MOD_SUPER)) == 0) RequestSubmit();
            return;
        }
        base.OnKeyDown(key, mods, repeat);
    }
    protected override void OnDisposed()
    {
        ValidSubmit = null; InvalidSubmit = null; ResetRequested = null; SubmitRequested = null;
        base.OnDisposed();
    }
}
