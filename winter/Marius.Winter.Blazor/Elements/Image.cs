using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class Image : WinterComponentBase
{
    static Image()
    {
        ElementHandlerRegistry.RegisterElementHandler<Image>(
            renderer => new Handler(renderer));
    }

    [Parameter] public byte[]? Source { get; set; }
    [Parameter] public string? MimeType { get; set; }
    [Parameter] public float RequestedWidth { get; set; }
    [Parameter] public float RequestedHeight { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        if (Source != null)
            builder.AddAttribute("Source", Source);
        if (MimeType != null)
            builder.AddAttribute("MimeType", MimeType);
        if (RequestedWidth > 0)
            builder.AddAttribute("RequestedWidth", RequestedWidth);
        if (RequestedHeight > 0)
            builder.AddAttribute("RequestedHeight", RequestedHeight);
    }

    class Handler : WinterElementHandler
    {
        // Shadow state — always holds the latest Blazor-declared values.
        byte[]? _source;
        string _mimeType = "png";

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.Image()) { }

        Marius.Winter.Image ImageControl => (Marius.Winter.Image)ElementControl;

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Source":
                    _source = AttributeHelper.GetByteArray(attributeValue);
                    ImageControl.SetSource(_source, _mimeType);
                    break;
                case "MimeType":
                    _mimeType = AttributeHelper.GetString(attributeValue) ?? "png";
                    ImageControl.SetSource(_source, _mimeType);
                    break;
                case "RequestedWidth":
                    ImageControl.RequestedWidth = AttributeHelper.GetFloat(attributeValue);
                    break;
                case "RequestedHeight":
                    ImageControl.RequestedHeight = AttributeHelper.GetFloat(attributeValue);
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }
    }
}
