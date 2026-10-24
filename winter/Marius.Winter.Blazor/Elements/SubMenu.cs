using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

/// <summary>A keyed, nonphysical nested menu; Enabled guards its entire branch.</summary>
public class SubMenu : WinterComponentBase
{
    static SubMenu() => ElementHandlerRegistry.RegisterElementHandler<SubMenu>(r => new MenuItem.Handler(r, MenuItem.EntryKind.SubMenu));
    [Parameter] public string? Label { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    protected override RenderFragment? GetChildContent() => ChildContent;
    protected override void RenderAttributes(AttributesBuilder builder)
    { base.RenderAttributes(builder); builder.AddAttribute("Label", Label ?? ""); }
}
