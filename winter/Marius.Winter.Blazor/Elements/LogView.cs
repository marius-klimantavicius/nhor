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

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        if (Text != null)
            builder.AddAttribute("Text", Text);
        if (FontName != null)
            builder.AddAttribute("FontName", FontName);
        if (FontSize.HasValue)
            builder.AddAttribute("FontSize", FontSize.Value);
        if (Color.HasValue)
            builder.AddAttribute("Color", Color.Value);
    }

    class Handler : WinterElementHandler
    {
        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.LogView()) { }

        Marius.Winter.LogView LogViewControl => (Marius.Winter.LogView)ElementControl;

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Text":
                    LogViewControl.Text = AttributeHelper.GetString(attributeValue) ?? "";
                    break;
                case "FontName":
                    LogViewControl.FontName = AttributeHelper.GetString(attributeValue) ?? "monospace";
                    break;
                case "FontSize":
                    LogViewControl.FontSize = AttributeHelper.GetFloat(attributeValue, 14f);
                    break;
                case "Color":
                    LogViewControl.Color = AttributeHelper.GetColor4(attributeValue);
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }
    }
}
