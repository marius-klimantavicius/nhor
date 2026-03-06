using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class Checkbox : WinterComponentBase
{
    static Checkbox()
    {
        ElementHandlerRegistry.RegisterElementHandler<Checkbox>(renderer => new Handler(renderer));
    }

    [Parameter] public string? Text { get; set; }
    [Parameter] public bool IsChecked { get; set; }
    [Parameter] public EventCallback<bool> OnChanged { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        if (Text != null)
            builder.AddAttribute("Text", Text);

        builder.AddAttribute("IsChecked", IsChecked);
        builder.AddAttribute("onchanged", EventCallback.Factory.Create<ChangeEventArgs>(this, (value) => OnChanged.InvokeAsync((bool)value.Value!)));
    }

    class Handler : WinterElementHandler
    {
        ulong OnChangedEventHandlerId;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.Checkbox())
        {
            CheckboxControl.Changed = value =>
            {
                if (OnChangedEventHandlerId != 0)
                    Renderer.DispatchEventAsync(OnChangedEventHandlerId, null, new ChangeEventArgs { Value = value });
            };
        }

        Marius.Winter.Checkbox CheckboxControl => (Marius.Winter.Checkbox)ElementControl;

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Text":
                    CheckboxControl.Text = AttributeHelper.GetString(attributeValue) ?? "";
                    break;
                case "IsChecked":
                    CheckboxControl.IsChecked = AttributeHelper.GetBool(attributeValue);
                    break;
                case "onchanged":
                    Renderer.RegisterEvent(attributeEventHandlerId, id =>
                    {
                        if (OnChangedEventHandlerId == id)
                            OnChangedEventHandlerId = 0;
                    });
                    OnChangedEventHandlerId = attributeEventHandlerId;
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }
    }
}
