using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public abstract class WinterComponentBase : NativeControlComponentBase
{
    [Parameter] public bool Visible { get; set; } = true;
    [Parameter] public bool Enabled { get; set; } = true;
    [Parameter] public float Opacity { get; set; } = 1f;
    [Parameter] public string? Tooltip { get; set; }
    [Parameter] public object? LayoutData { get; set; }
    [Parameter] public float? MinWidth { get; set; }
    [Parameter] public float? MinHeight { get; set; }
    [Parameter] public float? MaxWidth { get; set; }
    [Parameter] public float? MaxHeight { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        if (!Visible)
            builder.AddAttribute("Visible", false);
        if (!Enabled)
            builder.AddAttribute("Enabled", false);
        if (Opacity < 1f)
            builder.AddAttribute("Opacity", Opacity);
        if (Tooltip != null)
            builder.AddAttribute("Tooltip", Tooltip);
        if (LayoutData != null)
            builder.AddAttribute("LayoutData", LayoutData);
        if (MinWidth.HasValue)
            builder.AddAttribute("MinWidth", MinWidth.Value);
        if (MinHeight.HasValue)
            builder.AddAttribute("MinHeight", MinHeight.Value);
        if (MaxWidth.HasValue)
            builder.AddAttribute("MaxWidth", MaxWidth.Value);
        if (MaxHeight.HasValue)
            builder.AddAttribute("MaxHeight", MaxHeight.Value);
    }
}
