using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class LogView : WinterComponentBase
{
    static LogView()
    {
        ElementHandlerRegistry.RegisterElementHandler<LogView>(
            renderer => new Handler(renderer));
    }

    [Parameter] public string? Text { get; set; }
    [Parameter] public string? FontName { get; set; }
    [Parameter] public float? FontSize { get; set; }
    [Parameter] public Color4? Color { get; set; }
    [Parameter] public LogBuffer? Buffer { get; set; }
    [Parameter] public bool FollowTail { get; set; } = true;
    [Parameter] public EventCallback<bool> FollowTailChanged { get; set; }
    [Parameter] public EventCallback<bool> OnFollowTailChanged { get; set; }

    private Marius.Winter.LogView? Control => (ElementHandler as IWinterElementHandler)?.ElementControl as Marius.Winter.LogView;
    public void Append(string text) => Control?.Append(text);
    public void Clear() => Control?.Clear();
    public void JumpToLatest() => Control?.JumpToLatest();
    public string SelectedText => Control?.SelectedText ?? "";

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        builder.AddAttribute("Configuration", new Configuration(Text, Buffer, FollowTail));
        builder.AddAttribute("onfollowtailchanged", EventCallback.Factory.Create<ChangeEventArgs>(this, AcceptFollowTail));
        if (FontName != null)
            builder.AddAttribute("FontName", FontName);
        if (FontSize.HasValue)
            builder.AddAttribute("FontSize", FontSize.Value);
        if (Color.HasValue)
            builder.AddAttribute("Color", Color.Value);
    }

    private Task AcceptFollowTail(ChangeEventArgs args)
    {
        FollowTail = (bool)args.Value!;
        return (FollowTailChanged.HasDelegate ? FollowTailChanged : OnFollowTailChanged).InvokeAsync(FollowTail);
    }

    private sealed record Configuration(string? Text, LogBuffer? Buffer, bool FollowTail);

    class Handler : WinterElementHandler
    {
        private Configuration? _pending;
        private ulong _followId;
        private long _generation;
        private bool _latest;
        private long _queued = -1;
        private string? _lastText;
        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.LogView()) { LogViewControl.FollowTailChanged = QueueFollow; }

        private void QueueFollow(bool value)
        {
            if (IsDisposed || _followId == 0) return;
            _latest = value;
            if (LogViewControl.OwnerWindow is not { } window)
            {
                _ = Renderer.DispatchNativeEventAsync(_followId, null, new ChangeEventArgs { Value = value });
                return;
            }
            if (_queued == _generation) return;
            long generation = _generation;
            ulong id = _followId;
            _queued = generation;
            _ = window.Dispatcher.InvokeAsync(() =>
            {
                if (_queued == generation) _queued = -1;
                if (!IsDisposed && _generation == generation && _followId == id)
                    _ = Renderer.DispatchNativeEventAsync(id, null, new ChangeEventArgs { Value = _latest });
            });
        }

        public override void CommitAttributes()
        {
            if (IsDisposed || _pending is not { } config) return;
            _pending = null;
            if (config.Text != null || _lastText != null) LogViewControl.Text = config.Text ?? "";
            _lastText = config.Text;
            LogViewControl.Buffer = config.Buffer;
            LogViewControl.FollowTail = config.FollowTail;
        }

        public override void Dispose()
        {
            _generation++;
            _pending = null;
            LogViewControl.FollowTailChanged = null;
            base.Dispose();
        }

        Marius.Winter.LogView LogViewControl => (Marius.Winter.LogView)ElementControl;

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Configuration": _generation++; _pending = WeakObjectStore.Get<Configuration>(attributeValue); break;
                case "onfollowtailchanged":
                    _generation++;
                    RegisterEvent(attributeEventHandlerId, id => { if (_followId == id) { _followId = 0; _generation++; } });
                    _followId = attributeEventHandlerId;
                    break;
                case "FontName":
                    LogViewControl.FontName = AttributeHelper.GetString(attributeValue) ?? "monospace";
                    break;
                case "FontSize":
                    if (attributeValue == null) LogViewControl.ResetFontSize();
                    else LogViewControl.FontSize = AttributeHelper.GetFloat(attributeValue, 14f);
                    break;
                case "Color":
                    LogViewControl.Color = attributeValue == null ? null : AttributeHelper.GetColor4(attributeValue);
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }
    }
}
