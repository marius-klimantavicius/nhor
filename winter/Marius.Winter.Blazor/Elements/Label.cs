using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;
using TextWrap = ThorVG.TextWrap;

namespace Marius.Winter.Blazor.Elements;

public class Label : WinterComponentBase
{
    static Label()
    {
        ElementHandlerRegistry.RegisterElementHandler<Label>(
            renderer => new Handler(renderer));
    }

    [Parameter] public string? Text { get; set; }
    [Parameter] public float? FontSize { get; set; }
    [Parameter] public Color4? Color { get; set; }
    [Parameter] public bool Italic { get; set; }
    [Parameter] public bool Bold { get; set; }
    [Parameter] public bool Subpixel { get; set; }
    [Parameter] public TextWrap TextWrapping { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        if (Text != null)
            builder.AddAttribute("Text", Text);
        if (FontSize.HasValue)
            builder.AddAttribute("FontSize", FontSize.Value);
        if (Color.HasValue)
            builder.AddAttribute("Color", Color.Value);
        builder.AddAttribute("Italic", Italic);
        builder.AddAttribute("Bold", Bold);
        builder.AddAttribute("Subpixel", Subpixel);
        if (TextWrapping != TextWrap.None)
            builder.AddAttribute("TextWrapping", TextWrapping);
    }

    protected override RenderFragment? GetChildContent() => ChildContent;

    class Handler : WinterElementHandler, IHandleChildContentText
    {
        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.Label()) { }

        Marius.Winter.Label LabelControl => (Marius.Winter.Label)ElementControl;

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Text":
                    LabelControl.Text = AttributeHelper.GetString(attributeValue) ?? "";
                    break;
                case "FontSize":
                    LabelControl.FontSize = AttributeHelper.GetFloat(attributeValue, 14f);
                    break;
                case "Color":
                    LabelControl.Color = AttributeHelper.GetColor4(attributeValue);
                    break;
                case "Italic":
                    LabelControl.Italic = AttributeHelper.GetBool(attributeValue);
                    break;
                case "Bold":
                    LabelControl.Bold = AttributeHelper.GetBool(attributeValue);
                    break;
                case "Subpixel":
                    LabelControl.Subpixel = AttributeHelper.GetBool(attributeValue);
                    break;
                case "TextWrapping":
                    LabelControl.TextWrapping = attributeValue is TextWrap tw
                        ? tw
                        : System.Enum.Parse<TextWrap>(AttributeHelper.GetString(attributeValue) ?? "None");
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }

        private int _lastTextIndex = -1;

        public void HandleText(int index, string text)
        {
            // Multiple text frames from Blazor expressions (e.g. "Progress: @(val)%")
            // arrive as separate HandleText calls. Concatenate them.
            if (index == 0 || index <= _lastTextIndex)
            {
                // First fragment: trim leading whitespace only, preserve trailing
                LabelControl.Text = text?.TrimStart() ?? "";
            }
            else
            {
                LabelControl.Text += text ?? "";
            }
            _lastTextIndex = index;
        }
    }
}
