using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class GifImage : WinterComponentBase
{
    static GifImage()
    {
        ElementHandlerRegistry.RegisterElementHandler<GifImage>(
            renderer => new Handler(renderer));
    }

    /// <summary>Raw GIF bytes.</summary>
    [Parameter] public byte[]? Source { get; set; }

    /// <summary>File path to a GIF file.</summary>
    [Parameter] public string? Path { get; set; }

    /// <summary>Whether the animation is playing. Default: auto-play.</summary>
    [Parameter] public bool IsPlaying { get; set; } = true;

    [Parameter] public float RequestedWidth { get; set; }
    [Parameter] public float RequestedHeight { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        if (Source != null)
            builder.AddAttribute("Source", Source);
        if (Path != null)
            builder.AddAttribute("Path", Path);
        builder.AddAttribute("IsPlaying", IsPlaying);
        if (RequestedWidth > 0)
            builder.AddAttribute("RequestedWidth", RequestedWidth);
        if (RequestedHeight > 0)
            builder.AddAttribute("RequestedHeight", RequestedHeight);
    }

    class Handler : WinterElementHandler
    {
        byte[]? _lastSource;
        string? _lastPath;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.GifImage()) { }

        Marius.Winter.GifImage Control => (Marius.Winter.GifImage)ElementControl;

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName,
            object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Source":
                    var bytes = AttributeHelper.GetByteArray(attributeValue);
                    if (bytes != _lastSource)
                    {
                        _lastSource = bytes;
                        _lastPath = null;
                        Control.LoadFromBytes(bytes);
                    }
                    break;
                case "Path":
                    var path = AttributeHelper.GetString(attributeValue);
                    if (path != _lastPath)
                    {
                        _lastPath = path;
                        _lastSource = null;
                        Control.LoadFromPath(path);
                    }
                    break;
                case "IsPlaying":
                    Control.IsPlaying = AttributeHelper.GetBool(attributeValue);
                    break;
                case "RequestedWidth":
                    Control.RequestedWidth = AttributeHelper.GetFloat(attributeValue);
                    break;
                case "RequestedHeight":
                    Control.RequestedHeight = AttributeHelper.GetFloat(attributeValue);
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue,
                        attributeEventUpdatesAttributeName);
                    break;
            }
        }
    }
}
