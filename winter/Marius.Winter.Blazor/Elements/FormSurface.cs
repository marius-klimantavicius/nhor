using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

internal class FormSurface : WinterComponentBase
{
    static FormSurface() => ElementHandlerRegistry.RegisterElementHandler<FormSurface>(r => new Handler(r));
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public EventCallback Submit { get; set; }
    protected override RenderFragment? GetChildContent() => ChildContent;
    protected override void RenderAttributes(AttributesBuilder b) { base.RenderAttributes(b); b.AddAttribute("onsubmit", Submit); }
    private sealed class Handler : WinterElementHandler, IWinterContainerElementHandler
    {
        private ulong _submit;
        private Marius.Winter.Form Control => (Marius.Winter.Form)ElementControl;
        public Handler(NativeComponentRenderer r) : base(r, new Marius.Winter.Form()) => Control.SubmitRequested += Requested;
        private void Requested() { if (_submit != 0 && !IsDisposed) Renderer.DispatchNativeEventAsync(_submit, null, EventArgs.Empty); }
        public override void ApplyAttribute(ulong id, string name, object value, string updates)
        {
            if (name != "onsubmit") { base.ApplyAttribute(id, name, value, updates); return; }
            RegisterEvent(id, old => { if (_submit == old) _submit = 0; }); _submit = id;
        }
        public void AddChild(Element child, int index) => Control.InsertChild(child, Math.Min(index, Control.Children.Count));
        public void RemoveChild(Element child) => Control.RemoveChild(child);
        public int GetChildIndex(Element child)
        {
            for (int i = 0; i < Control.Children.Count; i++) if (ReferenceEquals(Control.Children[i], child)) return i;
            return -1;
        }
        public void ReorderChildren(List<Element> children) => Control.ReorderChildren(children);
        public override void Dispose() { Control.SubmitRequested -= Requested; _submit = 0; base.Dispose(); }
    }
}
