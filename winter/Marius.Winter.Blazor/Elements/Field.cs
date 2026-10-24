using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

/// <summary>Native field composition around exactly one Winter editor.</summary>
public class Field : WinterComponentBase
{
    static Field() => ElementHandlerRegistry.RegisterElementHandler<Field>(r => new Handler(r));
    [Parameter] public string? Label { get; set; }
    [Parameter] public string? HelpText { get; set; }
    [Parameter] public IEnumerable<string>? Messages { get; set; }
    [Parameter] public bool ReadOnly { get; set; }
    [Parameter] public bool Invalid { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    public bool FocusEditor() => ElementHandler is IWinterElementHandler h && h.ElementControl is Marius.Winter.Field field && field.FocusEditor();
    protected override RenderFragment? GetChildContent() => ChildContent;
    protected override void RenderAttributes(AttributesBuilder b)
    {
        base.RenderAttributes(b);
        b.AddAttribute("Label", Label ?? ""); b.AddAttribute("HelpText", HelpText ?? "");
        b.AddAttribute("Messages", Messages ?? Array.Empty<string>());
        b.AddAttribute("ReadOnly", ReadOnly); b.AddAttribute("Invalid", Invalid);
    }
    private sealed class Handler(NativeComponentRenderer renderer) : WinterElementHandler(renderer, new Marius.Winter.Field()), IWinterContainerElementHandler
    {
        private Marius.Winter.Field Control => (Marius.Winter.Field)ElementControl;
        public override void ApplyAttribute(ulong id, string name, object value, string updates)
        {
            switch (name)
            {
                case "Label": Control.Label = AttributeHelper.GetString(value) ?? ""; break;
                case "HelpText": Control.HelpText = AttributeHelper.GetString(value) ?? ""; break;
                case "Messages": Control.SetMessages(AttributeHelper.GetObject<IEnumerable<string>>(value)); break;
                case "ReadOnly": Control.ReadOnly = AttributeHelper.GetBool(value); break;
                case "Invalid": Control.Invalid = AttributeHelper.GetBool(value); break;
                default: base.ApplyAttribute(id, name, value, updates); break;
            }
        }
        public void AddChild(Element child, int index)
        {
            if (Control.Editor != null && !ReferenceEquals(Control.Editor, child)) throw new InvalidOperationException("Field accepts exactly one editor.");
            Control.Editor = child;
        }
        public void RemoveChild(Element child) { if (ReferenceEquals(Control.Editor, child)) Control.Editor = null; }
        public int GetChildIndex(Element child) => ReferenceEquals(Control.Editor, child) ? 0 : -1;
        public void ReorderChildren(List<Element> children) { }
    }
}
