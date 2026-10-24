using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Marius.Winter.Blazor.Elements;

public class TextField : FormFieldBase<string>
{
    [Parameter] public bool ReadOnly { get; set; }
    protected override bool FieldReadOnly => ReadOnly;
    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public bool Required { get; set; }
    [Parameter] public bool Multiline { get; set; }
    [Parameter] public float MultilineHeight { get; set; } = 90;
    [Parameter] public EventCallback<TextCommitEventArgs> TextCommitted { get; set; }
    protected override void RenderEditor(RenderTreeBuilder b)
    {
        b.OpenComponent(0, Multiline ? typeof(TextArea) : typeof(TextBox));
        b.AddAttribute(1, "Text", Value ?? ""); b.AddAttribute(2, "TextChanged", Changed);
        b.AddAttribute(3, "ReadOnly", ReadOnly); b.AddAttribute(4, "Invalid", IsInvalid);
        b.AddAttribute(5, "Placeholder", Placeholder); b.AddAttribute(6, "ResetVersion", InputResetVersion);
        b.AddAttribute(7, "TextCommitted", TextCommitted);
        if (Multiline) { b.AddAttribute(8, "MinHeight", MultilineHeight); b.AddAttribute(9, "MaxHeight", MultilineHeight); }
        else b.AddAttribute(10, "Required", Required);
        b.CloseComponent();
    }
}
