using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class LottieImage : WinterComponentBase
{
    static LottieImage()
    {
        ElementHandlerRegistry.RegisterElementHandler<LottieImage>(
            renderer => new Handler(renderer));
    }

    /// <summary>Lottie JSON string content.</summary>
    [Parameter] public string? Source { get; set; }

    /// <summary>Raw Lottie JSON bytes.</summary>
    [Parameter] public byte[]? SourceBytes { get; set; }

    /// <summary>File path to a Lottie JSON file.</summary>
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
        if (SourceBytes != null)
            builder.AddAttribute("SourceBytes", SourceBytes);
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
        // Shadow state for atomic source loading
        string? _source;
        byte[]? _sourceBytes;
        string? _path;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.LottieImage()) { }

        Marius.Winter.LottieImage Control => (Marius.Winter.LottieImage)ElementControl;

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName,
            object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Source":
                    var src = AttributeHelper.GetString(attributeValue);
                    if (src != _source) { _source = src; _sourceBytes = null; _path = null; Control.Source = _source; }
                    break;
                case "SourceBytes":
                    var sb = AttributeHelper.GetByteArray(attributeValue);
                    if (sb != _sourceBytes) { _sourceBytes = sb; _source = null; _path = null; Control.LoadFromBytes(_sourceBytes); }
                    break;
                case "Path":
                    var p = AttributeHelper.GetString(attributeValue);
                    if (p != _path) { _path = p; _source = null; _sourceBytes = null; Control.LoadFromPath(_path); }
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
