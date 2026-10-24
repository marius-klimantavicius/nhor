using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

/// <summary>Typed value binding backed by the reusable native Select controller.</summary>
public class Select<TItem, TValue> : InputComponentBase<TValue>
{
    static Select() => ElementHandlerRegistry.RegisterElementHandler<Select<TItem, TValue>>(renderer => new Handler(renderer));

    [Parameter] public IEnumerable<TItem>? Items { get; set; }
    [Parameter, EditorRequired] public Func<TItem, string> TextSelector { get; set; } = item => item?.ToString() ?? "";
    [Parameter, EditorRequired] public Func<TItem, TValue> ValueSelector { get; set; } = null!;
    [Parameter] public IEqualityComparer<TValue>? ValueComparer { get; set; }
    [Parameter] public TValue Value { get; set; } = default!;
    [Parameter] public EventCallback<TValue> ValueChanged { get; set; }
    [Parameter] public EventCallback<TValue> OnValueChanged { get; set; }
    public bool Focus() => TryFocus();
    private Configuration? _configuration;
    private EventCallback<TValue> _applicationCallback;
    private EventCallback<ChangeEventArgs> _bindingCallback;
    private bool _hasCallback;

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);
        var items = Items?.ToArray() ?? Array.Empty<TItem>();
        var comparer = ValueComparer ?? EqualityComparer<TValue>.Default;
        var keys = items.Select(item => ValueSelector(item)).ToArray();
        var labels = items.Select(item => TextSelector(item) ?? "").ToArray();
        var next = new Configuration(items, TextSelector, ValueSelector, Value, comparer, ResetVersion, keys, labels);
        if (_configuration == null || !Equivalent(_configuration, next)) _configuration = next;
        builder.AddAttribute("Selection", _configuration);
        var index = Value is null ? -1 : Array.FindIndex(keys, key => comparer.Equals(key, Value));
        builder.AddAttribute("SelectedIndex", index);

        var callback = ValueChanged.HasDelegate ? ValueChanged : OnValueChanged;
        if (!_hasCallback || !SameChangeCallback(callback, _applicationCallback))
        {
            _hasCallback = true; _applicationCallback = callback;
            _bindingCallback = EventCallback.Factory.Create<ChangeEventArgs>(this, args =>
            {
                // The immutable typed value travels with the native edit. The index only
                // updates Blazor's retained field so an unchanged parent can reject it.
                var change = (UserSelection)args.Value!; Value = change.Value;
                return callback.InvokeAsync(change.Value);
            });
        }
        builder.AddAttribute("onvaluechanged", _bindingCallback, "SelectedIndex");
    }

    private sealed record Configuration(TItem[] Items, Func<TItem, string> Text, Func<TItem, TValue> Key,
        TValue Value, IEqualityComparer<TValue> Comparer, long ResetVersion, TValue[] Keys, string[] Labels);

    private static bool Equivalent(Configuration a, Configuration b) =>
        a.ResetVersion == b.ResetVersion && ReferenceEquals(a.Comparer, b.Comparer) && a.Comparer.Equals(a.Value, b.Value) &&
        Equals(a.Text, b.Text) && Equals(a.Key, b.Key) && a.Items.SequenceEqual(b.Items) &&
        a.Keys.SequenceEqual(b.Keys, b.Comparer) && a.Labels.SequenceEqual(b.Labels);

    private sealed record UserSelection(TValue Value, int Index)
    {
        public override string ToString() => Index.ToString(CultureInfo.InvariantCulture);
    }

    private sealed class Handler : WinterElementHandler
    {
        private readonly CoalescedEvent _changed;
        private Configuration? _configuration;
        private bool _dirty;
        private Marius.Winter.Select<TItem, TValue> Control => (Marius.Winter.Select<TItem, TValue>)ElementControl;

        public Handler(NativeComponentRenderer renderer) : base(renderer, new Marius.Winter.Select<TItem, TValue>())
        {
            _changed = new CoalescedEvent(this); Control.UserValueChanged += OnUserChanged;
        }

        private void OnUserChanged(TValue value) => _changed.Fire(new UserSelection(value, Control.SelectedIndex));

        public override void ApplyAttribute(ulong id, string name, object value, string updates)
        {
            switch (name)
            {
                case "Selection":
                    _configuration = AttributeHelper.GetObject<Configuration>(value); _dirty = true; break;
                case "SelectedIndex":
                    _dirty = true; break;
                case "onvaluechanged":
                    RegisterEvent(id, _changed.Unregister); _changed.HandlerId = id; break;
                default: base.ApplyAttribute(id, name, value, updates); break;
            }
        }

        public override void CommitAttributes()
        {
            if (!_dirty || IsDisposed) return;
            _dirty = false; _changed.CancelPending();
            var config = _configuration;
            if (config != null) Control.Configure(config.Items, config.Text, config.Key, config.Value, config.Comparer);
        }

        public override void Dispose()
        {
            Control.UserValueChanged -= OnUserChanged; base.Dispose();
        }
    }
}
