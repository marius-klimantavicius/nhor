using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

/// <summary>Native Markdown display; requires no browser or server.</summary>
public class MarkdownView : WinterComponentBase
{
    static MarkdownView() => ElementHandlerRegistry.RegisterElementHandler<MarkdownView>(renderer => new Handler(renderer));
    [Parameter] public string? Markdown { get; set; }
    [Parameter] public float? FontSize { get; set; }
    [Parameter] public Func<string, MarkdownImage?>? ImageResolver { get; set; }
    [Parameter] public EventCallback<string> LinkClicked { get; set; }
    private Marius.Winter.MarkdownView? Control => (ElementHandler as Handler) is { Disposed: false } handler ? handler.Control : null;
    public void Focus() => Control?.Focus();
    public void ScrollTo(float x, float y) => Control?.ScrollTo(x, y);
    public void ReloadImages() => Control?.ReloadImages();
    public string Text => Control?.Text ?? "";
    public int SelectionStart => Control?.SelectionStart ?? 0;
    public int SelectionLength => Control?.SelectionLength ?? 0;
    public string SelectedText => Control?.SelectedText ?? "";
    public void Select(int start, int length) => Control?.Select(start, length);
    public void SelectAll() => Control?.SelectAll();
    public void ClearSelection() => Control?.ClearSelection();
    private sealed record Configuration(string Markdown, float? FontSize, Func<string, MarkdownImage?>? Resolver, EventCallback<string> Callback);
    private sealed record Delivery(string Destination, EventCallback<string> Callback);
    private readonly EventCallback<ChangeEventArgs> _clicked;
    public MarkdownView() => _clicked = EventCallback.Factory.Create<ChangeEventArgs>(this, (Func<ChangeEventArgs, Task>)(args =>
    {
        var delivery = (Delivery)args.Value!;
        return delivery.Callback.InvokeAsync(delivery.Destination);
    }));
    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);
        builder.AddAttribute("Configuration", new Configuration(Markdown ?? "", FontSize, ImageResolver, LinkClicked));
        builder.AddAttribute("onlinkclicked", _clicked);
    }
    private sealed class Handler : WinterElementHandler
    {
        internal Marius.Winter.MarkdownView Control => (Marius.Winter.MarkdownView)ElementControl;
        internal bool Disposed => IsDisposed;
        private Configuration? _configuration, _pending;
        private ulong _clickedId;
        private long _generation;
        public Handler(NativeComponentRenderer renderer) : base(renderer, new Marius.Winter.MarkdownView()) { Control.LinkClicked += Clicked; }
        private void Clicked(string destination)
        {
            if (IsDisposed || _clickedId == 0 || _configuration is not { Callback.HasDelegate: true } configuration) return;
            var delivery = new Delivery(destination, configuration.Callback);
            var generation = _generation; var id = _clickedId;
            var inputVersion = Control.MenuInputVersion; var owner = Control.OwnerWindow;
            void Dispatch()
            {
                if (!IsDisposed && generation == _generation && id == _clickedId && inputVersion == Control.MenuInputVersion &&
                    ReferenceEquals(owner, Control.OwnerWindow) && Control.IsEffectivelyEnabled && Control.IsEffectivelyVisible)
                    _ = Renderer.Dispatcher.InvokeAsync(() => Renderer.DispatchNativeEventAsync(id, null, new ChangeEventArgs { Value = delivery }));
            }
            if (Control.OwnerWindow is { } window) window.DispatcherQueue.Enqueue(Dispatch); else Dispatch();
        }
        public override void CommitAttributes()
        {
            if (_pending is not { } config || IsDisposed) return;
            _pending = null;
            if (config != _configuration) _generation++;
            _configuration = config;
            Control.Configure(config.Markdown, config.FontSize, config.Resolver);
        }
        public override void ApplyAttribute(ulong id, string name, object value, string updates)
        {
            if (name == "Configuration") { _pending = WeakObjectStore.Get<Configuration>(value); return; }
            if (name == "onlinkclicked")
            {
                RegisterEvent(id, old => { if (_clickedId == old) { _clickedId = 0; _generation++; } });
                _clickedId = id; return;
            }
            base.ApplyAttribute(id, name, value, updates);
        }
        public override void Dispose()
        {
            if (IsDisposed) return;
            _generation++; _clickedId = 0; _configuration = _pending = null;
            Control.LinkClicked -= Clicked; base.Dispose();
        }
    }
}
