using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

/// <summary>Attaches declarative commands to its physical parent. Removal restores the previous caller-owned menu.</summary>
public class ContextMenu : WinterComponentBase
{
    static ContextMenu() => ElementHandlerRegistry.RegisterElementHandler<ContextMenu>(r => new MenuContainerHandler(r, MenuContainerKind.Context));
    [Parameter] public RenderFragment? ChildContent { get; set; }
    protected override RenderFragment? GetChildContent() => ChildContent;
}
