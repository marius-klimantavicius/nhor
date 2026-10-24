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

    /// <summary>File path to a GIF file. Takes precedence over Source.</summary>
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
        byte[]? _source;
        string? _path;
        bool _isPlaying = true;
        bool _attributesDirty;
        string? _loadedPath;
        byte[]? _loadedSource;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.GifImage()) { }

        Marius.Winter.GifImage Control => (Marius.Winter.GifImage)ElementControl;

        public override void CommitAttributes()
        {
            if (!_attributesDirty || IsDisposed) return;
            _attributesDirty = false;

            string? path = string.IsNullOrEmpty(_path) ? null : _path;
            byte[]? source = path == null ? _source : null;
            if (path != _loadedPath || source != _loadedSource)
            {
                if (path != null) Control.LoadFromPath(path);
                else Control.LoadFromBytes(source);
                _loadedPath = path;
                _loadedSource = source;
            }

            Control.IsPlaying = _isPlaying;
        }

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName,
            object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Source":
                    _source = AttributeHelper.GetByteArray(attributeValue);
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
