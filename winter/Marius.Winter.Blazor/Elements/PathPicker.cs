using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

/// <summary>Typed editable path and Browse with EditContext field identity. Caller owns Provider.
/// Cancel leaves Value unchanged; Changed is authoritative over the legacy fallback.</summary>
public sealed class PathPicker : InputComponentBase<PickerPath?>, IWinterFormField, IDisposable
{
    static PathPicker() => ElementHandlerRegistry.RegisterElementHandler<PathPicker>(r => new Handler(r));
    [CascadingParameter] private EditContext? CascadedContext { get; set; }
    [CascadingParameter] internal WinterFormScope? Scope { get; set; }
    [Parameter] public PickerPath? Value { get; set; }
    [Parameter] public EventCallback<PickerPath?> ValueChanged { get; set; }
    [Parameter] public EventCallback<PickerPath?> OnValueChanged { get; set; }
    [Parameter] public Expression<Func<PickerPath?>>? ValueExpression { get; set; }
    [Parameter] public IFilePickerProvider? Provider { get; set; }
    [Parameter] public FilePickerMode Mode { get; set; } = FilePickerMode.SelectFolder;
    [Parameter] public string InitialDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    [Parameter] public FilePickerFilter[] Filters { get; set; } = [];
    [Parameter] public string Label { get; set; } = "Path";
    [Parameter] public string HelpText { get; set; } = "";
    [Parameter] public bool ReadOnly { get; set; }
    [Parameter] public bool Invalid { get; set; }
    [Parameter] public EventCallback<FilePickerResult> Selected { get; set; }
    [Parameter] public EventCallback<FilePickerResult> Canceled { get; set; }
    [Parameter] public EventCallback<TextCommitEventArgs> TextCommitted { get; set; }
    private EditContext? _context;
    private FieldIdentifier _field;
    private WinterFormScope? _scope;
    private bool _disposed;
    private long _identity, _formReset, _inputReset, _explicitReset, _notifyingIdentity;
    private readonly EventCallback<ChangeEventArgs> _picked;
    private EventCallback<PickerPath?> _application;
    private EventCallback<PickerPath?> _notifyingChange;
    private bool _hasApplication;
    private sealed record Configuration(PickerPath? Value, IFilePickerProvider? Provider, FilePickerMode Mode, string Directory,
        FilePickerFilter[] Filters, string Label, string Help, bool ReadOnly, bool Invalid, string[] Messages, long Reset,
        EventCallback<PickerPath?> Changed, EventCallback<FilePickerResult> Selected, EventCallback<FilePickerResult> Canceled);
    private sealed record Delivery(FilePickerResult Result, Configuration Configuration, Func<bool> Current);
    public PathPicker() => _picked = EventCallback.Factory.Create<ChangeEventArgs>(this, args =>
    {
        var delivery = (Delivery)args.Value!;
        if (!delivery.Current()) return Task.CompletedTask;
        var result = delivery.Result; var config = delivery.Configuration;
        // Start both captured callbacks before awaiting either. No late state writes.
        if (result.Accepted)
        {
            Value = result.Path;
            var context = _context; var field = _field;
            var change = config.Changed.InvokeAsync(result.Path);
            if (!_disposed && ReferenceEquals(context, _context)) context?.NotifyFieldChanged(field);
            var selected = config.Selected.InvokeAsync(result);
            return Task.WhenAll(change, selected);
        }
        return config.Canceled.InvokeAsync(result);
    });
    EditContext? IWinterFormField.Context => _context;
    FieldIdentifier IWinterFormField.Identifier => _field;
    public bool Focus() => !_disposed && ((ElementHandler as IWinterElementHandler)?.ElementControl as Marius.Winter.PathPicker)?.FocusEditor() == true;
    public Task BrowseAsync() => ((ElementHandler as IWinterElementHandler)?.ElementControl as Marius.Winter.PathPicker)?.BrowseAsync() ?? Task.CompletedTask;
    protected override void OnParametersSet()
    {
        if (CascadedContext != null && ValueExpression == null) throw new InvalidOperationException("PathPicker requires ValueExpression (normally @bind-Value) inside an EditContext.");
        var field = ValueExpression == null ? default : FieldIdentifier.Create(ValueExpression);
        if (!ReferenceEquals(_context, CascadedContext) || !_field.Equals(field) || !ReferenceEquals(_scope, Scope))
        {
            Detach(); _identity++; _context = CascadedContext; _field = field; _scope = Scope;
            if (_context != null) _context.OnValidationStateChanged += ValidationChanged;
            _scope?.Form.Register(this); _inputReset++;
        }
        if (_explicitReset != ResetVersion || _formReset != (_scope?.Form.InputResetVersion ?? 0))
        { _explicitReset = ResetVersion; _formReset = _scope?.Form.InputResetVersion ?? 0; _inputReset++; }
        var application = ValueChanged.HasDelegate ? ValueChanged : OnValueChanged;
        if (!_hasApplication || !PickerCallbackIdentity.Same(_application, application) || _notifyingIdentity != _identity)
        {
            _hasApplication = true; _application = application; _notifyingIdentity = _identity;
            var context = _context; var capturedField = _field; var identity = _identity;
            _notifyingChange = EventCallback.Factory.Create<PickerPath?>(this, value =>
            {
                if (_disposed || identity != _identity) return Task.CompletedTask;
                var pending = application.InvokeAsync(value);
                if (!_disposed && identity == _identity && ReferenceEquals(context, _context)) context?.NotifyFieldChanged(capturedField);
                return pending;
            });
        }
    }
    private void ValidationChanged(object? sender, ValidationStateChangedEventArgs args)
    {
        var identity = _identity;
        _ = InvokeAsync(() => { if (!_disposed && identity == _identity && ReferenceEquals(sender, _context)) StateHasChanged(); });
    }
    protected override void RenderAttributes(AttributesBuilder b)
    {
        base.RenderAttributes(b);
        var context = _context; var field = _field;
        b.AddAttribute("Configuration", new Configuration(Value, Provider, Mode, InitialDirectory, Filters, Label, HelpText, ReadOnly, Invalid,
            context?.GetValidationMessages(field).ToArray() ?? [], _inputReset, _application, Selected, Canceled));
        b.AddAttribute("onvaluechanged", BindChange(_notifyingChange, default, value =>
        { (ElementHandler as Handler)?.AcknowledgeEdit(); Value = value; }));
        b.AddAttribute("onpicked", _picked);
        b.AddAttribute("ontextcommitted", TextCommitted);
    }
    private void Detach()
    { if (_context != null) _context.OnValidationStateChanged -= ValidationChanged; _scope?.Form.Unregister(this); }
    public void Dispose() { if (_disposed) return; _disposed = true; _identity++; Detach(); }
    private sealed class Handler : WinterElementHandler
    {
        private Marius.Winter.PathPicker Control => (Marius.Winter.PathPicker)ElementControl;
        private readonly IFilePickerProvider _local = new LocalFilePickerProvider();
        private readonly CoalescedEvent _change;
        private Configuration? _configuration, _pending;
        private ulong _picked, _committed;
        private long _generation, _subscriptions;
        private bool _pendingEdit;
        internal Handler(NativeComponentRenderer r) : base(r, new Marius.Winter.PathPicker())
        { _change = new(this); Control.ValueEdited += Edit; Control.Picked += Pick; Control.TextEditor.TextCommitted += Commit; }
        private void Edit(PickerPath? value) { _pendingEdit = true; _change.Fire(value); }
        internal void AcknowledgeEdit() => _pendingEdit = false;
        private void Commit(TextCommitEventArgs args)
        {
            var id = _committed; var generation = _generation;
            _change.Flush();
            if (!IsDisposed && id != 0 && id == _committed && generation == _generation)
                _ = Renderer.DispatchNativeEventAsync(id, null, args);
        }
        private void Pick(FilePickerResult result)
        {
            if (_configuration == null || IsDisposed || _picked == 0) return;
            var id = _picked; var generation = _generation; var subscriptions = _subscriptions; var config = _configuration;
            bool Current() => !IsDisposed && id == _picked && generation == _generation && subscriptions == _subscriptions;
            _change.Flush();
            // A scalar callback may synchronously reset, replace services or
            // remove this field. Never retarget its earlier browse result.
            if (!Current()) return;
            var delivery = new Delivery(result, config, Current);
            _ = Renderer.Dispatcher.InvokeAsync(() => Current() ? Renderer.DispatchNativeEventAsync(id, null, new ChangeEventArgs { Value = delivery }) : Task.CompletedTask);
        }
        public override void ApplyAttribute(ulong id, string name, object value, string updates)
        {
            switch (name)
            {
                case "Configuration": _pending = WeakObjectStore.Get<Configuration>(value); break;
                case "onvaluechanged":
                    if (_change.HandlerId != id) _pendingEdit = false;
                    RegisterEvent(id, _change.Unregister); _change.HandlerId = id; break;
                case "onpicked": RegisterEvent(id, old => { if (_picked == old) _picked = 0; }); _picked = id; break;
                case "ontextcommitted": RegisterEvent(id, old => { if (_committed == old) _committed = 0; }); _committed = id; break;
                default: base.ApplyAttribute(id, name, value, updates); break;
            }
        }
        public override void CommitAttributes()
        {
            if (IsDisposed || _pending is not { } next) return;
            _pending = null; var previous = _configuration; _configuration = next;
            bool reset = previous == null || previous.Reset != next.Reset || !ReferenceEquals(previous.Provider, next.Provider) || previous.Mode != next.Mode || previous.Directory != next.Directory || !ReferenceEquals(previous.Filters, next.Filters);
            if (reset) { _generation++; _pendingEdit = false; _change.CancelPending(); Control.ResetValue(next.Value); }
            else if (previous!.Value != next.Value) { _pendingEdit = false; _change.CancelPending(); }
            if (previous == null || !PickerCallbackIdentity.Same(previous.Changed, next.Changed) || !PickerCallbackIdentity.Same(previous.Selected, next.Selected) || !PickerCallbackIdentity.Same(previous.Canceled, next.Canceled)) _subscriptions++;
            Control.Provider = next.Provider ?? _local; Control.Mode = next.Mode; Control.InitialDirectory = next.Directory; Control.Filters = next.Filters;
            // Unrelated parent renders must not overwrite a queued native edit
            // before its latest scalar is delivered. Rejection after delivery,
            // changed declared values and explicit resets remain authoritative.
            if (!_pendingEdit) Control.Value = next.Value;
            Control.ReadOnly = next.ReadOnly; Control.Invalid = next.Invalid;
            Control.Label = next.Label; Control.HelpText = next.Help; Control.SetMessages(next.Messages);
        }
        public override void Dispose()
        { if (IsDisposed) return; _generation++; Control.ValueEdited -= Edit; Control.Picked -= Pick; Control.TextEditor.TextCommitted -= Commit; base.Dispose(); }
    }
}
