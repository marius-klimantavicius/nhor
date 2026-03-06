using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class Slider : WinterComponentBase
{
    static Slider()
    {
        ElementHandlerRegistry.RegisterElementHandler<Slider>(
            renderer => new Handler(renderer));
    }

    [Parameter] public float Value { get; set; }
    [Parameter] public float Min { get; set; }
    [Parameter] public float Max { get; set; } = 1f;
    [Parameter] public float Step { get; set; }
    [Parameter] public bool ShowValue { get; set; }
    [Parameter] public EventCallback<float> OnValueChanged { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        builder.AddAttribute("Value", Value);
        builder.AddAttribute("Min", Min);
        builder.AddAttribute("Max", Max);
        builder.AddAttribute("Step", Step);
        builder.AddAttribute("ShowValue", ShowValue);
        builder.AddAttribute("onvaluechanged", EventCallback.Factory.Create<ChangeEventArgs>(this, value => OnValueChanged.InvokeAsync((float)value.Value!)));
    }

    class Handler : WinterElementHandler
    {
        ulong OnValueChangedEventHandlerId;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.Slider())
        {
            SliderControl.ValueChanged = value =>
            {
                if (OnValueChangedEventHandlerId != 0)
                    Renderer.DispatchEventAsync(OnValueChangedEventHandlerId, null, new ChangeEventArgs { Value = value });
            };
        }

        Marius.Winter.Slider SliderControl => (Marius.Winter.Slider)ElementControl;

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Value":
                    SliderControl.Value = AttributeHelper.GetFloat(attributeValue);
                    break;
                case "Min":
                    SliderControl.Min = AttributeHelper.GetFloat(attributeValue);
                    break;
                case "Max":
                    SliderControl.Max = AttributeHelper.GetFloat(attributeValue, 1f);
                    break;
                case "Step":
                    SliderControl.Step = AttributeHelper.GetFloat(attributeValue);
                    break;
                case "ShowValue":
                    SliderControl.ShowValue = AttributeHelper.GetBool(attributeValue);
                    break;
                case "onvaluechanged":
                    Renderer.RegisterEvent(attributeEventHandlerId, id =>
                    {
                        if (OnValueChangedEventHandlerId == id)
                            OnValueChangedEventHandlerId = 0;
                    });
                    OnValueChangedEventHandlerId = attributeEventHandlerId;
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }
    }
}
