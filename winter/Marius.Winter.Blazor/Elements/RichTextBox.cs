using System;
using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

/// <summary>Bounded native rich editing. Non-null Document takes precedence over Text and its callbacks.</summary>
public class RichTextBox : WinterComponentBase
{
    static RichTextBox() => ElementHandlerRegistry.RegisterElementHandler<RichTextBox>(renderer => new Handler(renderer));
    [Parameter] public string? Text { get; set; }
    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public EventCallback<string> TextChanged { get; set; }
    [Parameter] public EventCallback<string> OnTextChanged { get; set; }
    [Parameter] public RichDocument? Document { get; set; }
    [Parameter] public EventCallback<RichDocument> DocumentChanged { get; set; }
    [Parameter] public EventCallback<RichTextFormattingState> FormattingStateChanged { get; set; }
    [Parameter] public long ResetVersion { get; set; }
    [Parameter] public float? FontSize { get; set; }

    private Marius.Winter.RichTextBox? Control => (ElementHandler as Handler) is { Disposed: false } handler ? handler.Control : null;
    public RichDocument? CurrentDocument => Control?.Document;
    public RichTextFormattingState? FormattingState => Control?.FormattingState;
    public bool CanUndo => Control?.CanUndo == true;
    public bool CanRedo => Control?.CanRedo == true;
    public bool Undo() => Control?.Undo() == true;
    public bool Redo() => Control?.Redo() == true;
    public void ToggleBold() => Control?.ToggleBold();
    public void ToggleItalic() => Control?.ToggleItalic();
    public void ToggleUnderline() => Control?.ToggleUnderline();
    public void ToggleStrikethrough() => Control?.ToggleStrikethrough();
    public void SetSelectionColor(Color4 color) => Control?.SetSelectionColor(color);
    public void ClearFormatting() => Control?.ClearFormatting();
    public void Select(int start, int length) => Control?.Select(start, length);
    public void Focus() => Control?.Focus();

    private sealed record Callbacks(EventCallback<string> Text, EventCallback<RichDocument> Document, EventCallback<RichTextFormattingState> State);
    private sealed record Configuration(string Text, RichDocument? Document, string Placeholder, long Reset, float? FontSize, Callbacks Callbacks);
    private sealed record Edit(RichDocument Document, bool TextChanged, bool Rich, Callbacks Callbacks);
    private sealed record StateDelivery(RichTextFormattingState State, EventCallback<RichTextFormattingState> Callback);

    // Razor recreates binding delegates on an unrelated render. EventCallback.Equals
    // compares delegate references, so it cannot distinguish that from replacement.
    // Compare their method/target and receiver instead; unfamiliar framework layouts
    // conservatively keep EventCallback's original identity semantics.
    private static class CallbackIdentity<T>
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

    private static bool SameCallbacks(Configuration a, Configuration b) =>
        (b.Document != null ? CallbackIdentity<RichDocument>.Same(a.Callbacks.Document, b.Callbacks.Document) :
            CallbackIdentity<string>.Same(a.Callbacks.Text, b.Callbacks.Text)) &&
        CallbackIdentity<RichTextFormattingState>.Same(a.Callbacks.State, b.Callbacks.State);
    private readonly EventCallback<ChangeEventArgs> _edit, _state;
    public RichTextBox()
    {
        _edit = EventCallback.Factory.Create<ChangeEventArgs>(this, AcceptEdit);
        _state = EventCallback.Factory.Create<ChangeEventArgs>(this, args =>
        {
            var delivery = (StateDelivery)args.Value!;
            return delivery.Callback.InvokeAsync(delivery.State);
        });
    }
    private Task AcceptEdit(ChangeEventArgs args)
    {
        var edit = (Edit)args.Value!;
        // Only write before invoking application code; completion never restores an older snapshot.
        if (edit.Rich) { Document = edit.Document; return edit.Callbacks.Document.InvokeAsync(edit.Document); }
        Text = edit.Document.Text;
        return edit.TextChanged ? edit.Callbacks.Text.InvokeAsync(Text) : Task.CompletedTask;
    }
    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);
        builder.AddAttribute("Configuration", new Configuration(Text ?? "", Document, Placeholder ?? "", ResetVersion, FontSize,
            new(TextChanged.HasDelegate ? TextChanged : OnTextChanged, DocumentChanged, FormattingStateChanged)));
        builder.AddAttribute("onedit", _edit);
        builder.AddAttribute("onformatstate", _state);
    }

    private sealed class Handler : WinterElementHandler
    {
        internal Marius.Winter.RichTextBox Control => (Marius.Winter.RichTextBox)ElementControl;
        internal bool Disposed => IsDisposed;
        private Configuration? _configuration, _pending;
        private ulong _editId, _stateId;
        private long _generation;
        private int _queuedEdits;
        private RichDocument? _delivering;
        private string _lastUserText = "";
        private RichTextFormattingState? _lastState;
        internal Handler(NativeComponentRenderer renderer) : base(renderer, new Marius.Winter.RichTextBox())
        {
            Control.UserDocumentChanged += Edited;
            Control.FormattingStateChanged += StateChanged;
        }
        private void Enqueue(Action action)
        {
            if (Control.OwnerWindow is { } window) window.DispatcherQueue.Enqueue(action);
            else action();
        }
        private void Edited(RichDocument document)
        {
            if (IsDisposed || _editId == 0 || _configuration is not { } config) return;
            bool textChanged = _lastUserText != document.Text;
            _lastUserText = document.Text;
            var edit = new Edit(document, textChanged, config.Document != null, config.Callbacks);
            var generation = _generation; var id = _editId;
            _queuedEdits++;
            Enqueue(() =>
            {
                if (IsDisposed || generation != _generation || id != _editId) return;
                _queuedEdits--;
                _delivering = document;
                try { _ = Renderer.Dispatcher.InvokeAsync(() => Renderer.DispatchNativeEventAsync(id, null, new ChangeEventArgs { Value = edit })); }
                finally { _delivering = null; }
            });
        }
        private void StateChanged(RichTextFormattingState state)
        {
            if (IsDisposed || _stateId == 0 || _configuration is not { } config || !config.Callbacks.State.HasDelegate || state == _lastState) return;
            _lastState = state;
            var generation = _generation; var id = _stateId;
            var delivery = new StateDelivery(state, config.Callbacks.State);
            Enqueue(() =>
            {
                if (!IsDisposed && generation == _generation && id == _stateId)
                    _ = Renderer.Dispatcher.InvokeAsync(() => Renderer.DispatchNativeEventAsync(id, null, new ChangeEventArgs { Value = delivery }));
            });
        }
        public override void ApplyAttribute(ulong id, string name, object value, string updatesAttribute)
        {
            if (name == "Configuration") _pending = WeakObjectStore.Get<Configuration>(value);
            else if (name == "onedit")
            {
                RegisterEvent(id, old => { if (_editId == old) { _editId = 0; Invalidate(); } });
                _editId = id;
            }
            else if (name == "onformatstate")
            {
                RegisterEvent(id, old => { if (_stateId == old) _stateId = 0; });
                _stateId = id;
            }
            else base.ApplyAttribute(id, name, value, updatesAttribute);
        }
        private void Invalidate() { _generation++; _queuedEdits = 0; _lastState = null; }
        private static bool SameValue(Configuration a, Configuration b) =>
            a.Document != null ? b.Document != null && a.Document.Equals(b.Document) : b.Document == null && a.Text == b.Text;
        private static bool Matches(Configuration configuration, RichDocument document) =>
            configuration.Document != null ? configuration.Document.Equals(document) : configuration.Text == document.Text;
        public override void CommitAttributes()
        {
            if (IsDisposed || _pending is not { } next) return;
            _pending = null;
            var previous = _configuration;
            bool reset = previous == null || previous.Reset != next.Reset || (previous.Document == null) != (next.Document == null);
            bool callbacksChanged = previous != null && !SameCallbacks(previous, next);
            bool correction = previous != null && (_delivering != null ? !Matches(next, _delivering) : !SameValue(previous, next));
            _configuration = next;
            if (reset || correction || callbacksChanged) Invalidate();
            if (reset)
                Control.ResetDocument(next.Document ?? new RichDocument(next.Text));
            else if (_queuedEdits == 0)
            {
                if (next.Document != null) Control.Document = next.Document;
                else Control.Text = next.Text;
            }
            _lastUserText = Control.Text;
            Control.Placeholder = next.Placeholder;
            if (next.FontSize is { } size) Control.FontSize = size;
            else if (previous?.FontSize != null) Control.ResetFontSize();
            StateChanged(Control.FormattingState);
        }
        public override void Dispose()
        {
            if (IsDisposed) return;
            Invalidate();
            Control.UserDocumentChanged -= Edited;
            Control.FormattingStateChanged -= StateChanged;
            base.Dispose();
        }
    }
}
