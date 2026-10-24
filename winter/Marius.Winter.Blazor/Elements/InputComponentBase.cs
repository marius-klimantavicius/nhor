using System;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

/// <summary>Scalar input synchronization shared by conventional bindings.</summary>
public abstract class InputComponentBase<T> : WinterComponentBase
{
    /// <summary>Increment for an intentional reset, including a reset to an unchanged declared value before queued input is delivered.</summary>
    [Parameter] public long ResetVersion { get; set; }
    private InputReset<T>? _reset;
    private EventCallback<T> _applicationCallback;
    private EventCallback<ChangeEventArgs> _bindingCallback;
    private bool _hasCallback;

    protected EventCallback<ChangeEventArgs> BindChange(EventCallback<T> changed, EventCallback<T> legacy, Action<T> synchronize)
    {
        var callback = changed.HasDelegate ? changed : legacy;
        if (!_hasCallback || !callback.Equals(_applicationCallback))
        {
            _hasCallback = true;
            _applicationCallback = callback;
            // A registration captures its application callback. Replacing/clearing it creates a new
            // renderer identity, invalidating queued native state for the old subscription.
            _bindingCallback = EventCallback.Factory.Create<ChangeEventArgs>(this, args =>
            {
                var value = (T)args.Value!;
                synchronize(value);
                return callback.InvokeAsync(value);
            });
        }
        return _bindingCallback;
    }

    protected void RenderReset(AttributesBuilder builder, T value)
    {
        if (_reset == null || _reset.Version != ResetVersion)
            _reset = new InputReset<T>(ResetVersion, value);
        builder.AddAttribute("InputReset", _reset);
    }
}

internal sealed class InputReset<T>(long version, T value)
{
    public long Version { get; } = version;
    public T Value { get; } = value;
}
