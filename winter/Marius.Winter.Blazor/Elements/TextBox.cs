using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class TextBox : WinterComponentBase
{
    static TextBox()
    {
        ElementHandlerRegistry.RegisterElementHandler<TextBox>(
            renderer => new Handler(renderer));
    }

    [Parameter] public string? Text { get; set; }
    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public bool Required { get; set; }
    [Parameter] public EventCallback<string> OnTextChanged { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        if (Text != null)
            builder.AddAttribute("Text", Text);
        if (Placeholder != null)
            builder.AddAttribute("Placeholder", Placeholder);
        if (Required)
            builder.AddAttribute("Required", true);

        builder.AddAttribute("ontextchanged", EventCallback.Factory.Create<ChangeEventArgs>(this, value => OnTextChanged.InvokeAsync((string)value.Value!)));
    }

    class Handler : WinterElementHandler
    {
        readonly CoalescedEvent _textChangedEvent;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.TextBox())
        {
            _textChangedEvent = new CoalescedEvent(this);
            TextBoxControl.TextChanged = value => _textChangedEvent.Fire(value);
        }

        Marius.Winter.TextBox TextBoxControl => (Marius.Winter.TextBox)ElementControl;

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Text":
                    TextBoxControl.Text = AttributeHelper.GetString(attributeValue) ?? "";
                    break;
                case "Placeholder":
                    TextBoxControl.Placeholder = AttributeHelper.GetString(attributeValue) ?? "";
                    break;
                case "Required":
                    TextBoxControl.Required = AttributeHelper.GetBool(attributeValue);
                    break;
                case "ontextchanged":
                    Renderer.RegisterEvent(attributeEventHandlerId, _textChangedEvent.Unregister);
                    _textChangedEvent.HandlerId = attributeEventHandlerId;
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }
    }
}
