using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Marius.Winter.Blazor.Elements;

public class CheckboxField : FormFieldBase<bool>
{
    [Parameter] public string? Text { get; set; }
    protected override void RenderEditor(RenderTreeBuilder b)
    {
        b.OpenComponent<Checkbox>(0); b.AddAttribute(1, "IsChecked", Value); b.AddAttribute(2, "IsCheckedChanged", Changed);
        b.AddAttribute(3, "Text", Text); b.AddAttribute(4, "ResetVersion", InputResetVersion); b.CloseComponent();
    }
}
