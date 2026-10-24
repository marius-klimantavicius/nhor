using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

/// <summary>A discrete command with live state. Shortcut is display-only.</summary>
public class MenuItem : WinterComponentBase
{
    internal enum EntryKind { Command, Separator, SubMenu }
    static MenuItem() => ElementHandlerRegistry.RegisterElementHandler<MenuItem>(r => new Handler(r));
    [Parameter] public string? Label { get; set; }
    [Parameter] public bool IsChecked { get; set; }
    [Parameter] public EventCallback<bool> IsCheckedChanged { get; set; }
    [Parameter] public EventCallback<bool> OnCheckedChanged { get; set; }
    [Parameter] public string? Shortcut { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }
    /// <summary>Increment to invalidate pending commands on an explicit same-value reset.</summary>
    [Parameter] public long ResetVersion { get; set; }
    private readonly EventCallback<ChangeEventArgs> _delivery;
    public MenuItem() => _delivery = EventCallback.Factory.Create<ChangeEventArgs>(this, Accept);
    private sealed record Configuration(string Label, bool Enabled, bool Checked, string? Shortcut, long Reset,
        EventCallback Click, EventCallback<bool> Check);

    // Components 8 callbacks with an explicit receiver can box an EventCallback
    // whose public equality uses delegate reference identity. Razor recreates
    // ordinary model method delegates, so compare their invocation lists and
    // the exact receiver, for both checked and untyped action subscriptions.
    private static class CallbackIdentity<TCallback> where TCallback : struct
    {
        private static readonly FieldInfo? DelegateField, ReceiverField;
        [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicFields, typeof(EventCallback))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicFields, typeof(EventCallback<>))]
        static CallbackIdentity()
        {
            DelegateField = typeof(TCallback).GetField("Delegate", BindingFlags.Instance | BindingFlags.NonPublic);
            ReceiverField = typeof(TCallback).GetField("Receiver", BindingFlags.Instance | BindingFlags.NonPublic);
        }
        internal static bool Same(TCallback a, TCallback b) => a.Equals(b) ||
            DelegateField != null && ReceiverField != null &&
            ReferenceEquals(ReceiverField.GetValue(a), ReceiverField.GetValue(b)) &&
            Equals(DelegateField.GetValue(a), DelegateField.GetValue(b));
    }
    private sealed record Delivery(Configuration Configuration, bool Checked, Func<bool> Current, Action BeginCheck);
    private async Task Accept(ChangeEventArgs args)
    {
        var delivery = (Delivery)args.Value!;
        if (!delivery.Current()) return;
        if (delivery.Configuration.Check.HasDelegate)
        {
            delivery.BeginCheck();
            IsChecked = delivery.Checked;
            await delivery.Configuration.Check.InvokeAsync(delivery.Checked);
        }
    }
    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);
        builder.AddAttribute("Configuration", new Configuration(Label ?? "", Enabled, IsChecked, Shortcut, ResetVersion,
            OnClick, IsCheckedChanged.HasDelegate ? IsCheckedChanged : OnCheckedChanged));
        builder.AddAttribute("ondelivery", _delivery);
    }
    internal sealed class Handler : WinterElementHandler, IIndexedNonPhysicalChild, IWinterContainerElementHandler
    {
        private readonly EntryKind _kind;
        private string _label = "";
        private bool _enabled = true;
        private MenuBinding? _binding;
        private Marius.Winter.MenuItemDef? _item;
        private Configuration? _configuration, _pending;
        private ulong _deliveryId;
        private long _generation;
        private int _pendingChecks;
        private bool? _checkFeedback;
        internal Handler(NativeComponentRenderer renderer, EntryKind kind = EntryKind.Command) : base(renderer, new Marius.Winter.Panel()) { _kind = kind; }
        public override void ApplyAttribute(ulong id, string name, object value, string updates)
        {
            if (name == "Configuration") _pending = WeakObjectStore.Get<Configuration>(value);
            else if (name == "Label") { _label = AttributeHelper.GetString(value) ?? ""; if (_item != null) _item.Label = _label; }
            else if (name == "Enabled" && _kind != EntryKind.Command) { _enabled = AttributeHelper.GetBool(value, true); if (_item != null) _item.Enabled = _enabled; }
            else if (name == "ondelivery") { RegisterEvent(id, old => { if (_deliveryId == old) _deliveryId = 0; }); _deliveryId = id; }
            else base.ApplyAttribute(id, name, value, updates);
        }
        public override void CommitAttributes()
        {
            if (IsDisposed || _pending is not { } next) return;
            _pending = null; var old = _configuration;
            // Only the render acknowledging a started checked callback may
            // change the declared value without superseding later commands.
            // A changed parent value before delivery is new authority; a
            // rejected proposal also ends that pending sequence.
            var feedback = _checkFeedback; _checkFeedback = null;
            bool checkedAuthority = feedback.HasValue ? next.Checked != feedback.Value : old != null && old.Checked != next.Checked;
            if (old == null || old.Reset != next.Reset || old.Enabled != next.Enabled ||
                !CallbackIdentity<EventCallback>.Same(old.Click, next.Click) ||
                !CallbackIdentity<EventCallback<bool>>.Same(old.Check, next.Check) || checkedAuthority)
            { _generation++; _pendingChecks = 0; }
            _configuration = next;
            Synchronize();
        }
        private void Synchronize()
        {
            if (_item == null || _configuration is not { } c) return;
            _item.Label = c.Label; _item.Enabled = c.Enabled; _item.Shortcut = c.Shortcut;
            if (_pendingChecks == 0) _item.IsChecked = c.Checked;
        }
        private void Queue()
        {
            if (IsDisposed || _deliveryId == 0 || _configuration is not { } c || _item is not { CanActivate: true } item || _binding == null) return;
            var generation = _generation; var id = _deliveryId; var revision = item.ActivationRevision;
            var binding = _binding; var owner = binding.Owner(); var window = owner?.OwnerWindow; var ownerVersion = owner?.MenuInputVersion;
            var contextVersion = owner?.ContextMenuVersion;
            bool Current() => !IsDisposed && generation == _generation && id == _deliveryId && ReferenceEquals(_item, item) &&
                item.CanActivate && item.ActivationRevision == revision && ReferenceEquals(_binding, binding) &&
                (binding.ContextRoot == null || owner != null && owner.ContextMenuVersion == contextVersion && ReferenceEquals(owner.ContextMenu, binding.ContextRoot)) &&
                (window == null || (owner!.IsActive && ReferenceEquals(owner.OwnerWindow, window) && owner.MenuInputVersion == ownerVersion && !window.ShutdownToken.IsCancellationRequested));
            bool check = !item.IsChecked;
            if (c.Check.HasDelegate) { _pendingChecks++; item.IsChecked = check; }
            var delivery = new Delivery(c, check, Current, () => _checkFeedback = check);
            void Deliver()
            {
                if (IsDisposed || generation != _generation) return;
                if (c.Check.HasDelegate) _pendingChecks--;
                if (!Current()) { Synchronize(); return; }
                _ = Renderer.Dispatcher.InvokeAsync(async () =>
                {
                    await Renderer.DispatchNativeEventAsync(id, null, new ChangeEventArgs { Value = delivery });
                    // Dispatch completes the checked feedback render, including
                    // parent rejection, before the separate captured action.
                    if (Current()) await c.Click.InvokeAsync();
                });
            }
            if (window != null) window.DispatcherQueue.Enqueue(Deliver); else Deliver();
        }
        public void SetParent(object parentElement)
        {
            if (parentElement is not Element parent || !Menu.Bindings.TryGetValue(parent, out _binding)) throw new InvalidOperationException("MenuItem requires a menu.");
            _item = _kind switch
            {
                EntryKind.SubMenu => _binding.Menu.InsertSubMenu(_binding.Menu.Entries.Count, _label),
                EntryKind.Separator => _binding.Menu.InsertSeparator(_binding.Menu.Entries.Count),
                _ => _binding.Menu.InsertItem(_binding.Menu.Entries.Count, _configuration?.Label ?? "", Queue)
            };
            if (_kind == EntryKind.SubMenu)
            {
                _item.Enabled = _enabled;
                Menu.ElementToMenu.Add(ElementControl, _item.SubMenu!);
                Menu.Bindings.Add(ElementControl, new MenuBinding(_item.SubMenu!, _binding.Owner, _binding.ContextRoot));
            }
            Synchronize();
        }
        public void Remove()
        {
            _generation++; _pendingChecks = 0;
            Menu.ElementToMenu.Remove(ElementControl); Menu.Bindings.Remove(ElementControl);
            if (_item != null) _binding?.Menu.RemoveItem(_item);
            _item = null; _binding = null;
        }
        public void MoveTo(int index) { if (_item != null) _binding?.Menu.MoveItem(_item, index); }
        public override void Dispose() { if (IsDisposed) return; Remove(); base.Dispose(); }
        public void AddChild(Element child, int index) { }
        public void RemoveChild(Element child) { }
        public void ReorderChildren(System.Collections.Generic.List<Element> order) { }
        public int GetChildIndex(Element child) => -1;
    }
}
