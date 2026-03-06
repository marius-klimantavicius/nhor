using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class RichLabel : WinterComponentBase
{
    static RichLabel()
    {
        ElementHandlerRegistry.RegisterElementHandler<RichLabel>(
            renderer => new Handler(renderer));
    }

    [Parameter] public string? Text { get; set; }
    [Parameter] public string? Svg { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        if (Text != null)
            builder.AddAttribute("Text", Text);
        if (Svg != null)
            builder.AddAttribute("Svg", Svg);
    }

    class Handler : WinterElementHandler
    {
        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.RichLabel()) { }

        Marius.Winter.RichLabel RichLabelControl => (Marius.Winter.RichLabel)ElementControl;

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Text":
                    RichLabelControl.Text = AttributeHelper.GetString(attributeValue) ?? "";
                    break;
                case "Svg":
                    RichLabelControl.Svg = AttributeHelper.GetString(attributeValue);
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }
    }
}
