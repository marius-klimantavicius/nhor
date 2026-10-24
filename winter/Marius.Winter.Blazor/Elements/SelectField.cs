using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Marius.Winter.Blazor.Elements;

public class SelectField<TItem, TValue> : FormFieldBase<TValue>
{
    [Parameter] public IEnumerable<TItem>? Items { get; set; }
    [Parameter] public Func<TItem, string> TextSelector { get; set; } = null!;
    [Parameter] public Func<TItem, TValue> ValueSelector { get; set; } = null!;
    [Parameter] public IEqualityComparer<TValue>? ValueComparer { get; set; }
    protected override void RenderEditor(RenderTreeBuilder b)
    {
        b.OpenComponent<Select<TItem, TValue>>(0); b.AddAttribute(1, "Value", Value); b.AddAttribute(2, "ValueChanged", Changed);
        b.AddAttribute(3, "Items", Items); b.AddAttribute(4, "TextSelector", TextSelector); b.AddAttribute(5, "ValueSelector", ValueSelector);
        b.AddAttribute(6, "ValueComparer", ValueComparer); b.AddAttribute(7, "ResetVersion", InputResetVersion); b.CloseComponent();
    }
}
