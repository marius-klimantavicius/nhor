using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

/// <summary>Declarative in-window modal. The renderer owns slot content;
/// caller-provided services and action delegates remain caller owned.</summary>
public sealed class ModalDialog : WinterComponentBase, IDisposable
{
    static ModalDialog() => ElementHandlerRegistry.RegisterElementHandler<ModalDialog>(r => new Handler(r));
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public EventCallback<bool> OnIsOpenChanged { get; set; }
    [Parameter] public EventCallback<DialogResult> Closed { get; set; }
    [Parameter] public EventCallback<DialogResult> CloseRequested { get; set; }
    [Parameter] public EventCallback<DialogResult> CloseRejected { get; set; }
    [Parameter] public string Title { get; set; } = "Dialog";
    [Parameter] public float Width { get; set; } = 520;
    [Parameter] public float Height { get; set; } = 400;
    [Parameter] public bool CloseOnBackdrop { get; set; }
    [Parameter] public bool CloseOnEscape { get; set; } = true;
    [Parameter] public bool ShowCloseButton { get; set; } = true;
    [Parameter] public long ResetVersion { get; set; }
    [Parameter] public RenderFragment? Body { get; set; }
    [Parameter] public RenderFragment? Footer { get; set; }
    [Parameter] public Func<WinterComponentBase?>? InitialFocus { get; set; }
    [Parameter] public Func<CancellationToken, ValueTask<bool>>? DefaultAction { get; set; }
    [Parameter] public Func<DialogResult, CancellationToken, ValueTask<bool>>? CloseGuard { get; set; }
    private bool _disposed;
    private readonly EventCallback<DialogResult> _completed, _requested;
    private EventCallback<bool> _requestChanged;
    private EventCallback<DialogResult> _requestClosed;

    public ModalDialog()
    {
        _requested = EventCallback.Factory.Create<DialogResult>(this, result =>
        {
            _requestChanged = IsOpenChanged.HasDelegate ? IsOpenChanged : OnIsOpenChanged;
            _requestClosed = Closed;
            return CloseRequested.InvokeAsync(result);
        });
        _completed = EventCallback.Factory.Create<DialogResult>(this, result =>
        {
            if (_disposed) return Task.CompletedTask;
            IsOpen = false;
            var changed = _requestChanged; var closed = _requestClosed;
            var change = changed.InvokeAsync(false);
            var completion = _disposed ? Task.CompletedTask : closed.InvokeAsync(result);
            return Task.WhenAll(change, completion);
        });
    }

    private Marius.Winter.ModalDialog? Native => (ElementHandler as IWinterElementHandler)?.ElementControl as Marius.Winter.ModalDialog;
    public Task<bool> RequestCloseAsync(DialogCloseReason reason = DialogCloseReason.Cancel) => Native?.RequestCloseAsync(reason) ?? Task.FromResult(false);

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);
        builder.AddAttribute("Configuration", new Configuration(IsOpen, ResetVersion, Title, Width, Height, CloseOnBackdrop, CloseOnEscape, ShowCloseButton,
            InitialFocus, DefaultAction, CloseGuard));
        builder.AddAttribute("oncompleted", _completed);
        builder.AddAttribute("oncloserequested", _requested);
        builder.AddAttribute("oncloserejected", CloseRejected);
    }

    protected override RenderFragment GetChildContent() => b =>
    {
        Slot(b, 0, false, Body); Slot(b, 1, true, Footer);
    };
    private static void Slot(RenderTreeBuilder b, int sequence, bool footer, RenderFragment? content)
    {
        b.OpenComponent<ContentSlot>(sequence); b.SetKey(footer);
        b.AddAttribute(2, nameof(ContentSlot.IsFooter), footer);
        b.AddAttribute(3, nameof(ContentSlot.ChildContent), content); b.CloseComponent();
    }
    public void Dispose() => _disposed = true;
    private sealed record Configuration(bool Open, long Reset, string Title, float Width, float Height, bool Backdrop, bool Escape, bool CloseButton,
        Func<WinterComponentBase?>? Focus, Func<CancellationToken, ValueTask<bool>>? Default, Func<DialogResult, CancellationToken, ValueTask<bool>>? Guard);

    private sealed class Handler : WinterElementHandler
    {
        private Configuration? _pending;
        private long? _reset;
        private ulong _completed, _requested, _rejected;
        private Func<CancellationToken, ValueTask<bool>>? _default;
        private Func<DialogResult, CancellationToken, ValueTask<bool>>? _guard;
        private Marius.Winter.ModalDialog Control => (Marius.Winter.ModalDialog)ElementControl;
        internal Handler(NativeComponentRenderer renderer) : base(renderer, new Marius.Winter.ModalDialog())
        {
            Control.Closed += Complete;
            Control.CloseRequested += Request;
            Control.CloseRejected += Reject;
            Control.DefaultAction = InvokeDefault; Control.CloseGuard = InvokeGuard;
        }
        private void Dispatch(ulong id, DialogResult value)
        { if (!IsDisposed && id != 0) _ = Renderer.Dispatcher.InvokeAsync(() => { if (!IsDisposed) return Renderer.DispatchNativeEventAsync(id, null, value); return Task.CompletedTask; }); }
        private void Complete(DialogResult value) => Dispatch(_completed, value);
        private void Request(DialogResult value) => Dispatch(_requested, value);
        private void Reject(DialogResult value) => Dispatch(_rejected, value);
        private ValueTask<bool> InvokeDefault(CancellationToken token)
        { var callback = _default; return new(Renderer.Dispatcher.InvokeAsync(async () => callback == null || await callback(token))); }
        private ValueTask<bool> InvokeGuard(DialogResult result, CancellationToken token)
        { var callback = _guard; return new(Renderer.Dispatcher.InvokeAsync(async () => callback == null || await callback(result, token))); }
        public override void ApplyAttribute(ulong id, string name, object value, string updates)
        {
            switch (name)
            {
                case "Configuration": _pending = WeakObjectStore.Get<Configuration>(value); break;
                case "oncompleted":
                    RegisterEvent(id, old => { if (_completed == old) _completed = 0; }); _completed = id; break;
                case "oncloserequested": RegisterEvent(id, old => { if (_requested == old) _requested = 0; }); _requested = id; break;
                case "oncloserejected": RegisterEvent(id, old => { if (_rejected == old) _rejected = 0; }); _rejected = id; break;
                default: base.ApplyAttribute(id, name, value, updates); break;
            }
        }
        public override void CommitAttributes()
        {
            if (IsDisposed || _pending is not { } c) return;
            _pending = null;
            Control.Title = c.Title; Control.Width = c.Width; Control.Height = c.Height;
            Control.CloseOnBackdrop = c.Backdrop; Control.CloseOnEscape = c.Escape; Control.ShowCloseButton = c.CloseButton;
            Control.InitialFocus = () => (c.Focus?.Invoke()?.ElementHandler as IWinterElementHandler)?.ElementControl;
            if (!Equals(_default, c.Default) || !Equals(_guard, c.Guard)) Control.CancelCloseRequest();
            _default = c.Default; _guard = c.Guard;
            if (_reset.HasValue && _reset != c.Reset) Control.ResetSession(c.Open);
            else Control.IsOpen = c.Open;
            _reset = c.Reset;
        }
        public override void Dispose()
        {
            if (IsDisposed) return;
            Control.Closed -= Complete; Control.CloseRequested -= Request; Control.CloseRejected -= Reject;
            _completed = _requested = _rejected = 0;
            base.Dispose();
        }
    }

    private sealed class ContentSlot : WinterComponentBase
    {
        public ContentSlot() { }
        static ContentSlot() => ElementHandlerRegistry.RegisterElementHandler<ContentSlot>(r => new SlotHandler(r));
        [Parameter] public bool IsFooter { get; set; }
        [Parameter] public RenderFragment? ChildContent { get; set; }
        protected override void RenderAttributes(AttributesBuilder b) => b.AddAttribute(nameof(IsFooter), IsFooter);
        protected override RenderFragment? GetChildContent() => ChildContent;
        private sealed class SlotLayout : ILayout
        {
            private readonly Marius.Winter.StackLayout _stack = new() { Spacing = 8, CrossAlignment = Alignment.Stretch };
            public Vector2 Measure(Element e, float w, float h) => _stack.Measure(e, w, h);
            public void Arrange(Element e, RectF b) { if (e.Children.Count == 1) e.Children[0].Arrange(b); else _stack.Arrange(e, b); }
        }
        private sealed class SlotHandler : WinterElementHandler, INonPhysicalChild, IWinterContainerElementHandler
        {
            private Marius.Winter.ModalDialog? _owner;
            private bool _footer;
            internal SlotHandler(NativeComponentRenderer r) : base(r, new Marius.Winter.Panel { Layout = new SlotLayout(), Padding = new Thickness(0) }) { }
            public override void ApplyAttribute(ulong id, string name, object value, string updates)
            { if (name == nameof(IsFooter)) _footer = AttributeHelper.GetBool(value); else base.ApplyAttribute(id, name, value, updates); }
            public void SetParent(object parent)
            {
                _owner = parent as Marius.Winter.ModalDialog ?? throw new InvalidOperationException("Modal slots require a ModalDialog parent.");
                if (_footer) _owner.Footer = ElementControl; else _owner.Body = ElementControl;
            }
            public void Remove()
            {
                if (_owner is { IsDisposed: false })
                { if (_footer && ReferenceEquals(_owner.Footer, ElementControl)) _owner.Footer = null; else if (!_footer && ReferenceEquals(_owner.Body, ElementControl)) _owner.Body = null; }
                _owner = null;
            }
            public void AddChild(Element child, int index) => ElementControl.InsertChild(child, Math.Min(index, ElementControl.Children.Count));
            public void RemoveChild(Element child) => ElementControl.RemoveChild(child);
            public int GetChildIndex(Element child) { for (int i = 0; i < ElementControl.Children.Count; i++) if (ReferenceEquals(ElementControl.Children[i], child)) return i; return -1; }
            public void ReorderChildren(List<Element> order) => ElementControl.ReorderChildren(order);
            public override void Dispose() { Remove(); base.Dispose(); }
        }
    }
}
