using System;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public sealed class StatusPanel : WinterComponentBase
{
    static StatusPanel() => ElementHandlerRegistry.RegisterElementHandler<StatusPanel>(renderer => new Handler(renderer));
    [Parameter] public OperationStatus Status { get; set; } = new();
    [Parameter] public EventCallback Retry { get; set; }
    [Parameter] public EventCallback Cancel { get; set; }
    [Parameter] public bool IsProgressPaused { get; set; }
    [Parameter] public bool ShowProgress { get; set; } = true;

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);
        builder.AddAttribute(nameof(Status), Status);
        builder.AddAttribute(nameof(IsProgressPaused), IsProgressPaused);
        builder.AddAttribute(nameof(ShowProgress), ShowProgress);
        builder.AddAttribute("onretry", Retry);
        builder.AddAttribute("oncancel", Cancel);
    }

    private sealed class Handler : WinterElementHandler
    {
        private ulong _retryId, _cancelId;
        private Marius.Winter.StatusPanel Control => (Marius.Winter.StatusPanel)ElementControl;
        internal Handler(NativeComponentRenderer renderer) : base(renderer, new Marius.Winter.StatusPanel()) { }
        public override void ApplyAttribute(ulong id, string name, object value, string updates)
        {
            switch (name)
            {
                case nameof(Status): Control.Status = WeakObjectStore.Get<OperationStatus>(value) ?? new(); break;
                case nameof(IsProgressPaused): Control.IsProgressPaused = AttributeHelper.GetBool(value); break;
                case nameof(ShowProgress): Control.ShowProgress = AttributeHelper.GetBool(value); break;
                case "onretry":
                    RegisterEvent(id, old => { if (_retryId == old) { _retryId = 0; Control.Retry = null; } });
                    _retryId = id;
                    Control.Retry = id == 0 ? null : () => Renderer.DispatchNativeEventAsync(id, null, EventArgs.Empty);
                    break;
                case "oncancel":
                    RegisterEvent(id, old => { if (_cancelId == old) { _cancelId = 0; Control.Cancel = null; } });
                    _cancelId = id;
                    Control.Cancel = id == 0 ? null : () => Renderer.DispatchNativeEventAsync(id, null, EventArgs.Empty);
                    break;
                default: base.ApplyAttribute(id, name, value, updates); break;
            }
        }
        public override void Dispose() { Control.Retry = null; Control.Cancel = null; base.Dispose(); }
    }
}
