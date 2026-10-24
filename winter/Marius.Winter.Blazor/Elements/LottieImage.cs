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

    /// <summary>File path to a Lottie JSON file. Takes precedence over SourceBytes, then Source.</summary>
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
        // Keep all declared sources, including fallbacks hidden by a higher priority source.
        string? _source;
        byte[]? _sourceBytes;
        string? _path;
        bool _isPlaying = true;
        bool _attributesDirty;
        string? _loadedAttribute;
        object? _loadedSource;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.LottieImage()) { }

        Marius.Winter.LottieImage Control => (Marius.Winter.LottieImage)ElementControl;

        public override void CommitAttributes()
        {
            if (!_attributesDirty || IsDisposed) return;
            _attributesDirty = false;

            string? attribute = !string.IsNullOrEmpty(_path) ? "Path"
                : _sourceBytes != null ? "SourceBytes"
                : !string.IsNullOrEmpty(_source) ? "Source" : null;
            object? source = attribute == "Path" ? _path : attribute == "SourceBytes" ? _sourceBytes
                : attribute == "Source" ? _source : null;
            if (attribute != _loadedAttribute || !Equals(source, _loadedSource))
            {
                if (attribute == "Path") Control.LoadFromPath(_path);
                else if (attribute == "SourceBytes") Control.LoadFromBytes(_sourceBytes);
                else Control.Source = (string?)source;
                _loadedAttribute = attribute;
                _loadedSource = source;
            }

            // Loading auto-plays natively; unchanged IsPlaying attributes are absent from diffs.
            Control.IsPlaying = _isPlaying;
        }

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName,
            object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Source":
                    _source = AttributeHelper.GetString(attributeValue);
                    _attributesDirty = true;
                    break;
                case "SourceBytes":
                    _sourceBytes = AttributeHelper.GetByteArray(attributeValue);
                    _attributesDirty = true;
                    break;
                case "Path":
                    _path = AttributeHelper.GetString(attributeValue);
                    _attributesDirty = true;
                    break;
                case "IsPlaying":
                    _isPlaying = attributeValue == null || AttributeHelper.GetBool(attributeValue);
                    _attributesDirty = true;
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
