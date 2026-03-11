using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class TextArea : WinterComponentBase
{
    static TextArea()
    {
        ElementHandlerRegistry.RegisterElementHandler<TextArea>(
            renderer => new Handler(renderer));
    }

    [Parameter] public string? Text { get; set; }
    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public EventCallback<string> OnTextChanged { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        if (Text != null)
            builder.AddAttribute("Text", Text);
        if (Placeholder != null)
            builder.AddAttribute("Placeholder", Placeholder);

        builder.AddAttribute("ontextchanged", EventCallback.Factory.Create<ChangeEventArgs>(this, value => OnTextChanged.InvokeAsync((string)value.Value!)));
    }

    class Handler : WinterElementHandler
    {
        readonly CoalescedEvent _textChangedEvent;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.TextArea())
        {
            _textChangedEvent = new CoalescedEvent(this);
            TextAreaControl.TextChanged = value => _textChangedEvent.Fire(value);
        }

        Marius.Winter.TextArea TextAreaControl => (Marius.Winter.TextArea)ElementControl;

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Text":
                    TextAreaControl.Text = AttributeHelper.GetString(attributeValue) ?? "";
                    break;
                case "Placeholder":
                    TextAreaControl.Placeholder = AttributeHelper.GetString(attributeValue) ?? "";
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
