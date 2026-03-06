using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class SvgImage : WinterComponentBase
{
    static SvgImage()
    {
        ElementHandlerRegistry.RegisterElementHandler<SvgImage>(
            renderer => new Handler(renderer));
    }

    [Parameter] public string? Svg { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        if (Svg != null)
            builder.AddAttribute("Svg", Svg);
    }

    class Handler : WinterElementHandler
    {
        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.SvgImage()) { }

        Marius.Winter.SvgImage SvgImageControl => (Marius.Winter.SvgImage)ElementControl;

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Svg":
                    SvgImageControl.Svg = AttributeHelper.GetString(attributeValue);
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }
    }
}
