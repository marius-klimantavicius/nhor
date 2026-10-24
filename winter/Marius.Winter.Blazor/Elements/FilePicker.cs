using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

/// <summary>Declarative in-app picker. Supplied controller/provider are borrowed.
/// IsOpenChanged wins over OnIsOpenChanged. Programmatic/reset closes emit no result.</summary>
public sealed class FilePicker : WinterComponentBase
{
    static FilePicker() => ElementHandlerRegistry.RegisterElementHandler<FilePicker>(r => new Handler(r));
    [Parameter] public FilePickerController? Controller { get; set; }
    [Parameter] public IFilePickerProvider? Provider { get; set; }
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public EventCallback<bool> OnIsOpenChanged { get; set; }
    [Parameter] public EventCallback<FilePickerResult> Selected { get; set; }
    [Parameter] public EventCallback<FilePickerResult> Canceled { get; set; }
    [Parameter] public FilePickerMode Mode { get; set; }
    [Parameter] public string InitialDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    [Parameter] public string FileName { get; set; } = "";
    [Parameter] public IReadOnlyList<FilePickerFilter> Filters { get; set; } = Array.Empty<FilePickerFilter>();
    [Parameter] public long ResetVersion { get; set; }
    [Parameter] public float Width { get; set; } = 660;
    [Parameter] public float Height { get; set; } = 570;
    private readonly EventCallback<ChangeEventArgs> _completed;
    private sealed record Callbacks(EventCallback<bool> Open, EventCallback<FilePickerResult> Selected, EventCallback<FilePickerResult> Canceled)
    {
        public bool Same(Callbacks other) => PickerCallbackIdentity.Same(Open, other.Open) && PickerCallbackIdentity.Same(Selected, other.Selected) && PickerCallbackIdentity.Same(Canceled, other.Canceled);
    }
    private sealed record Configuration(FilePickerController? Controller, IFilePickerProvider? Provider, bool Open, FilePickerMode Mode,
        string Directory, string Name, IReadOnlyList<FilePickerFilter> Filters, long Reset, float Width, float Height, Callbacks Callbacks);
    private sealed record Delivery(FilePickerResult Result, Callbacks Callbacks, Func<bool> Current, Action Acknowledge, Func<bool> Continue);
    public FilePicker() => _completed = EventCallback.Factory.Create<ChangeEventArgs>(this, args =>
    {
        var delivery = (Delivery)args.Value!;
        if (!delivery.Current()) return Task.CompletedTask;
        delivery.Acknowledge();
        IsOpen = false;
        var change = delivery.Callbacks.Open.InvokeAsync(false);
        // Close feedback may synchronously reset/replace/unmount the picker. A
        // simple rejection can reopen, while an explicit new identity cancels
        // the remaining callback. Neither callback mutates the view after await.
        var result = delivery.Continue()
            ? (delivery.Result.Accepted ? delivery.Callbacks.Selected : delivery.Callbacks.Canceled).InvokeAsync(delivery.Result)
            : Task.CompletedTask;
        return Task.WhenAll(change, result);
    });
    protected override void RenderAttributes(AttributesBuilder b)
    {
        base.RenderAttributes(b);
        b.AddAttribute("Configuration", new Configuration(Controller, Provider, IsOpen, Mode, InitialDirectory, FileName, Filters, ResetVersion, Width, Height,
            new(IsOpenChanged.HasDelegate ? IsOpenChanged : OnIsOpenChanged, Selected, Canceled)));
        b.AddAttribute("oncompleted", _completed);
    }
    private sealed class Handler : WinterElementHandler
    {
        private Marius.Winter.FilePicker Control => (Marius.Winter.FilePicker)ElementControl;
        private readonly IFilePickerProvider _local = new LocalFilePickerProvider();
        private FilePickerController? _owned;
        private Configuration? _configuration, _pending;
        private long _generation, _subscriptions;
        private long _acknowledgedSession = -1;
        private ulong _completed;
        internal Handler(NativeComponentRenderer renderer) : base(renderer, new Marius.Winter.FilePicker()) => Control.Completed += Complete;
        private void Complete(FilePickerResult result)
        {
            if (_configuration == null || _completed == 0 || IsDisposed) return;
            var id = _completed; var generation = _generation; var subscription = _subscriptions;
            var controller = Control.Controller!; var revision = controller.Revision;
            var callbacks = _configuration.Callbacks;
            bool Continue() => !IsDisposed && id == _completed && generation == _generation && subscription == _subscriptions;
            bool Current() => Continue() && ReferenceEquals(controller, Control.Controller) && controller.Revision == revision &&
                controller.State.SessionId == result.SessionId && _acknowledgedSession != result.SessionId;
            var delivery = new Delivery(result, callbacks, Current, () => _acknowledgedSession = result.SessionId, Continue);
            _ = Renderer.Dispatcher.InvokeAsync(() => Current() ? Renderer.DispatchNativeEventAsync(id, null, new ChangeEventArgs { Value = delivery }) : Task.CompletedTask);
        }
        public override void ApplyAttribute(ulong id, string name, object value, string updates)
        {
            if (name == "Configuration") _pending = WeakObjectStore.Get<Configuration>(value);
            else if (name == "oncompleted") { RegisterEvent(id, old => { if (_completed == old) _completed = 0; }); _completed = id; }
            else base.ApplyAttribute(id, name, value, updates);
        }
        public override void CommitAttributes()
        {
            if (IsDisposed || _pending is not { } next) return;
            _pending = null; var previous = _configuration; _configuration = next;
            bool subscriptionsChanged = previous != null && !previous.Callbacks.Same(next.Callbacks);
            if (previous == null || subscriptionsChanged) _subscriptions++;
            bool replace = previous == null || !ReferenceEquals(previous.Controller, next.Controller);
            if (replace)
            {
                var old = _owned; _owned = next.Controller == null ? new(next.Provider ?? _local) : null;
                Control.SetController(next.Controller ?? _owned!); old?.Dispose();
            }
            var controller = Control.Controller!;
            bool restart = replace || previous!.Reset != next.Reset || previous.Mode != next.Mode || previous.Directory != next.Directory || previous.Name != next.Name ||
                !ReferenceEquals(previous.Filters, next.Filters) || !ReferenceEquals(previous.Provider, next.Provider) || subscriptionsChanged;
            bool openEdge = previous is { Open: false } && next.Open;
            bool acknowledgedClose = _acknowledgedSession == controller.State.SessionId && !controller.State.IsOpen;
            bool closeEdge = previous is { Open: true } && !next.Open && !acknowledgedClose;
            if (restart || openEdge || closeEdge)
            { _generation++; _acknowledgedSession = -1; controller.Reset(); }
            if (next.Controller == null && !ReferenceEquals(controller.Provider, next.Provider ?? _local)) _ = controller.ReplaceProviderAsync(next.Provider ?? _local);
            Control.Filters = next.Filters; Control.DialogWidth = next.Width; Control.DialogHeight = next.Height;
            // A completed controller can still have an immutable result queued
            // on its Window. Unchanged declared true is not a new open request.
            // Once delivery begins, a parent's true feedback rejects the close
            // and may reopen without invalidating that originating result.
            if (next.Open && !controller.State.IsOpen && (restart || openEdge || acknowledgedClose))
            {
                _acknowledgedSession = -1;
                _ = controller.OpenAsync(next.Mode, next.Directory, next.Filters.Count > 0 ? next.Filters[0] : null, next.Name);
            }
            else if (!next.Open && controller.State.IsOpen)
            { _generation++; _acknowledgedSession = -1; controller.Reset(); }
        }
        public override void Dispose()
        {
            if (IsDisposed) return;
            _generation++; Control.Completed -= Complete; base.Dispose(); _owned?.Dispose(); _owned = null;
        }
    }
}

internal static class PickerCallbackIdentity
{
    internal static bool Same<T>(EventCallback<T> a, EventCallback<T> b) => Identity<T>.Same(a, b);

    // Components 8 retains an explicit receiver when a compiled binding targets
    // a model method. Render-frame equality then uses delegate reference identity.
    // Match method/target AND receiver, as the existing scalar/form adapters do;
    // unfamiliar framework layouts retain conservative EventCallback equality.
    private static class Identity<T>
    {
        private static readonly FieldInfo? DelegateField, ReceiverField;
        [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicFields, typeof(EventCallback<>))]
        static Identity()
        {
            DelegateField = typeof(EventCallback<T>).GetField("Delegate", BindingFlags.Instance | BindingFlags.NonPublic);
            ReceiverField = typeof(EventCallback<T>).GetField("Receiver", BindingFlags.Instance | BindingFlags.NonPublic);
        }
        internal static bool Same(EventCallback<T> a, EventCallback<T> b) => a.Equals(b) ||
            DelegateField != null && ReceiverField != null &&
            ReferenceEquals(ReceiverField.GetValue(a), ReceiverField.GetValue(b)) &&
            Equals(DelegateField.GetValue(a), DelegateField.GetValue(b));
    }
}
