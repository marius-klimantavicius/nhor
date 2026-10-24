using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
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
        if (!_hasCallback || !SameChangeCallback(callback, _applicationCallback))
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

    // Razor recreates equivalent binding delegates on ordinary parent renders.
    // Keep the originating registration when method, target and receiver agree;
    // a different target/receiver or clear still invalidates queued native input.
    // This is the rich editor's existing comparison, scoped to scalar bindings.
    protected static bool SameChangeCallback(EventCallback<T> a, EventCallback<T> b) => CallbackIdentity.Same(a, b);

    private static class CallbackIdentity
    {
        private static readonly FieldInfo? DelegateField, ReceiverField;
        [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicFields, typeof(EventCallback<>))]
        static CallbackIdentity()
        {
            DelegateField = typeof(EventCallback<T>).GetField("Delegate", BindingFlags.Instance | BindingFlags.NonPublic);
            ReceiverField = typeof(EventCallback<T>).GetField("Receiver", BindingFlags.Instance | BindingFlags.NonPublic);
        }
        internal static bool Same(EventCallback<T> a, EventCallback<T> b) => a.Equals(b) ||
            DelegateField != null && ReceiverField != null &&
            ReferenceEquals(ReceiverField.GetValue(a), ReceiverField.GetValue(b)) &&
            Equals(DelegateField.GetValue(a), DelegateField.GetValue(b));
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
