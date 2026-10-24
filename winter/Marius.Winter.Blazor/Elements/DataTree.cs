using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

/// <summary>Typed, controlled text/icon tree. Uses the same controller as native Winter.</summary>
public class DataTree<TItem> : WinterComponentBase
{
    static DataTree() => ElementHandlerRegistry.RegisterElementHandler<DataTree<TItem>>(renderer => new Handler(renderer));

    [Parameter] public TreeController<TItem>? Controller { get; set; }
    [Parameter] public IReadOnlyList<TItem>? Items { get; set; }
    [Parameter] public Func<TItem, string>? KeySelector { get; set; }
    [Parameter] public Func<TItem, string>? TextSelector { get; set; }
    [Parameter] public Func<TItem, IEnumerable<TItem>>? ChildrenSelector { get; set; }
    [Parameter] public Func<TItem, string?>? IconSelector { get; set; }
    [Parameter] public Func<TItem, bool>? MayHaveChildren { get; set; }
    [Parameter] public Func<TItem, CancellationToken, Task<IReadOnlyList<TItem>>>? LoadChildren { get; set; }
    [Parameter] public string? SelectedKey { get; set; }
    [Parameter] public IReadOnlySet<string>? ExpandedKeys { get; set; }
    [Parameter] public EventCallback<string?> SelectedKeyChanged { get; set; }
    [Parameter] public EventCallback<string?> OnSelectionChanged { get; set; }
    [Parameter] public EventCallback<IReadOnlySet<string>> ExpandedKeysChanged { get; set; }
    [Parameter] public EventCallback<IReadOnlySet<string>> OnExpandedKeysChanged { get; set; }
    [Parameter] public EventCallback<TreeRequest<TItem>> OnRequest { get; set; }
    [Parameter] public EventCallback<TItem> OnActivate { get; set; }
    [Parameter] public long ResetVersion { get; set; }
    [Parameter] public float? FontSize { get; set; }

    public TreeController<TItem>? DataController => (ElementHandler as Handler)?.Control.Controller;
    public bool Reveal(string key) => (ElementHandler as Handler)?.Control.Reveal(key) == true;
    public Task RefreshAsync(string key) => (ElementHandler as Handler)?.Control.RefreshAsync(key) ?? Task.CompletedTask;
    public void Focus() { if ((ElementHandler as Handler)?.Control is { } control) control.OwnerWindow?.SetFocus(control); }

    private readonly EventCallback<ChangeEventArgs> _request;
    private sealed record CallbackSet(EventCallback<string?> Selection, EventCallback<IReadOnlySet<string>> Expansion,
        EventCallback<TreeRequest<TItem>> Request, EventCallback<TItem> Activate)
    {
        private readonly object? _selection = SubscriptionIdentity(Selection);
        private readonly object? _expansion = SubscriptionIdentity(Expansion);
        private readonly object? _request = SubscriptionIdentity(Request);
        private readonly object? _activate = SubscriptionIdentity(Activate);
        internal bool SameSubscriptions(CallbackSet other) => Equals(_selection, other._selection) &&
            Equals(_expansion, other._expansion) && Equals(_request, other._request) && Equals(_activate, other._activate);
    }

    private static object? SubscriptionIdentity<T>(EventCallback<T> callback)
    {
        if (!callback.HasDelegate) return null;
        // Use Blazor's public event-frame normalization: an implicit receiver
        // is represented by its delegate (target + method equality), while an
        // explicit receiver retains the EventCallback. EventCallback.Equals
        // alone uses delegate reference identity, dropping unchanged method
        // groups recreated by Razor on an ordinary parent render.
        using var frames = new RenderTreeBuilder();
        frames.OpenElement(0, "tree-action");
        frames.AddAttribute(1, "onrequest", callback);
        frames.CloseElement();
        return frames.GetFrames().Array[1].AttributeValue;
    }
    private sealed record Delivery(TreeRequest<TItem> Request, CallbackSet Callbacks, Func<bool> IsCurrent);
    private sealed record Configuration(TreeController<TItem>? Controller, IReadOnlyList<TItem>? Items,
        Func<TItem, string>? Key, Func<TItem, string>? Text, Func<TItem, IEnumerable<TItem>>? Children,
        Func<TItem, string?>? Icon, Func<TItem, bool>? MayHaveChildren,
        Func<TItem, CancellationToken, Task<IReadOnlyList<TItem>>>? Load, string? Selected,
        IReadOnlySet<string>? Expanded, long ResetVersion, float? FontSize, CallbackSet Callbacks);

    public DataTree() => _request = EventCallback.Factory.Create<ChangeEventArgs>(this, AcceptRequest);

    private async Task AcceptRequest(ChangeEventArgs args)
    {
        var delivery = (Delivery)args.Value!;
        if (!delivery.IsCurrent()) return;
        var request = delivery.Request; var callbacks = delivery.Callbacks;
        // Never write state after awaiting application code. Commands retain the
        // subscription and key snapshot from their request, across ordinary renders.
        SelectedKey = request.SelectedKey; ExpandedKeys = request.ExpandedKeys;
        if (request.Kind == TreeRequestKind.Select) await callbacks.Selection.InvokeAsync(request.SelectedKey);
        if (request.Kind is TreeRequestKind.Expand or TreeRequestKind.Collapse or TreeRequestKind.Retry or TreeRequestKind.Reveal)
            await callbacks.Expansion.InvokeAsync(request.ExpandedKeys);
        if (request.Kind == TreeRequestKind.Activate) await callbacks.Activate.InvokeAsync(request.Item);
        // Awaiting application code yields control to parent resets, callback
        // replacement and unmounts. Never start another obsolete callback.
        if (delivery.IsCurrent()) await callbacks.Request.InvokeAsync(request);
    }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);
        var callbacks = new CallbackSet(SelectedKeyChanged.HasDelegate ? SelectedKeyChanged : OnSelectionChanged,
            ExpandedKeysChanged.HasDelegate ? ExpandedKeysChanged : OnExpandedKeysChanged, OnRequest, OnActivate);
        // Composite state is always applied atomically, also carrying same-value parent rejection.
        builder.AddAttribute("Configuration", new Configuration(Controller, Items, KeySelector, TextSelector, ChildrenSelector,
            IconSelector, MayHaveChildren, LoadChildren, SelectedKey, ExpandedKeys, ResetVersion, FontSize, callbacks));
        builder.AddAttribute("onrequest", _request);
    }

    private sealed class Handler : WinterElementHandler
    {
        internal Marius.Winter.DataTree<TItem> Control => (Marius.Winter.DataTree<TItem>)ElementControl;
        private Configuration? _configuration, _pending;
        private ulong _requestId;
        private long _generation, _subscriptionGeneration;
        private int _pendingSelection, _pendingExpansion;
        private static bool Expands(TreeRequestKind kind) => kind is TreeRequestKind.Expand or TreeRequestKind.Collapse or TreeRequestKind.Retry or TreeRequestKind.Reveal;

        internal Handler(NativeComponentRenderer renderer) : base(renderer, new Marius.Winter.DataTree<TItem>()) => Control.Requested += Requested;
        private void Requested(TreeRequest<TItem> request)
        {
            if (IsDisposed || _requestId == 0) return;
            if (_configuration == null) return;
            var id = _requestId; var generation = _generation;
            var subscription = _subscriptionGeneration;
            var controller = Control.Controller; var revision = controller?.Revision;
            bool IsCurrent() => !IsDisposed && id == _requestId && generation == _generation &&
                subscription == _subscriptionGeneration && ReferenceEquals(controller, Control.Controller) && revision == controller?.Revision;
            var delivery = new Delivery(request, _configuration.Callbacks, IsCurrent);
            if (request.Kind == TreeRequestKind.Select) _pendingSelection++;
            if (Expands(request.Kind)) _pendingExpansion++;
            void Deliver()
            {
                if (generation != _generation || IsDisposed) return;
                if (request.Kind == TreeRequestKind.Select) _pendingSelection--;
                if (Expands(request.Kind)) _pendingExpansion--;
                if (IsCurrent())
                    _ = Renderer.Dispatcher.InvokeAsync(() => Renderer.DispatchNativeEventAsync(id, null, new ChangeEventArgs { Value = delivery }));
                else if (_configuration is { } configuration)
                    // A data revision can invalidate the last queued action after
                    // a parent correction was deferred behind it. Apply that
                    // correction now, without reviving the old callback identity.
                    Control.Synchronize(_pendingSelection > 0 ? Control.SelectedKey : configuration.Selected,
                        _pendingExpansion > 0 ? Control.ExpandedKeys : configuration.Expanded);
            }
            if (Control.OwnerWindow is { } window) window.DispatcherQueue.Enqueue(Deliver);
            else Deliver();
        }

        public override void ApplyAttribute(ulong id, string name, object value, string updatesAttribute)
        {
            if (name == "Configuration") _pending = WeakObjectStore.Get<Configuration>(value);
            else if (name == "onrequest")
            {
                RegisterEvent(id, old => { if (_requestId == old) _requestId = 0; });
                _requestId = id;
            }
            else base.ApplyAttribute(id, name, value, updatesAttribute);
        }

        public override void CommitAttributes()
        {
            if (IsDisposed || _pending is not { } next) return;
            _pending = null;
            var previous = _configuration;
            _configuration = next;
            // Explicit removal/replacement ends the captured application's
            // subscription. Never retarget an old action to a new callback.
            if (previous == null || !previous.Callbacks.SameSubscriptions(next.Callbacks)) _subscriptionGeneration++;
            bool selectorsChanged = previous == null || previous.Key != next.Key || previous.Text != next.Text ||
                previous.Children != next.Children || previous.Icon != next.Icon || previous.MayHaveChildren != next.MayHaveChildren || previous.Load != next.Load;
            bool replaced = !ReferenceEquals(previous?.Controller, next.Controller) || (next.Controller == null && selectorsChanged);
            if (previous == null || replaced)
            {
                var controller = next.Controller ?? new TreeController<TItem>(
                    next.Key ?? throw new InvalidOperationException("DataTree requires KeySelector or Controller."),
                    next.Text ?? throw new InvalidOperationException("DataTree requires TextSelector or Controller."),
                    next.Children, next.Icon, next.MayHaveChildren, next.Load);
                Control.SetController(controller, ownsController: next.Controller == null);
            }
            bool reset = previous == null || replaced || next.ResetVersion != previous.ResetVersion || !ReferenceEquals(previous.Items, next.Items);
            if (reset) { _generation++; _pendingSelection = _pendingExpansion = 0; }
            if (next.Controller == null && (previous == null || replaced || !ReferenceEquals(previous.Items, next.Items) || next.ResetVersion != previous.ResetVersion))
                Control.Controller!.SetItems(next.Items ?? Array.Empty<TItem>(), next.Selected, next.Expanded);
            // A click selects and expands before either callback is drained. An
            // earlier field notification must not cancel a later queued command.
            // Explicit ResetVersion/data replacement still supersedes both fields.
            Control.Synchronize(_pendingSelection > 0 ? Control.SelectedKey : next.Selected,
                _pendingExpansion > 0 ? Control.ExpandedKeys : next.Expanded);
            if (next.FontSize is { } size) Control.FontSize = size;
            else if (previous?.FontSize != null) Control.FontSize = Control.OwnerWindow?.Theme.Panel.FontSize ?? Theme.Dark.Panel.FontSize;
        }

        public override void Dispose()
        {
            if (IsDisposed) return;
            _generation++; Control.Requested -= Requested;
            base.Dispose();
        }
    }
}
