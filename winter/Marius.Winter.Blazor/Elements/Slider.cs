using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class Slider : InputComponentBase<float>
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
    [Parameter] public EventCallback<float> ValueChanged { get; set; }
    [Parameter] public EventCallback<float> OnValueChanged { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);
        RenderReset(builder, Value);

        builder.AddAttribute("Value", Value);
        builder.AddAttribute("Min", Min);
        builder.AddAttribute("Max", Max);
        builder.AddAttribute("Step", Step);
        builder.AddAttribute("ShowValue", ShowValue);
        builder.AddAttribute("onvaluechanged", BindChange(ValueChanged, OnValueChanged, value => Value = value), "Value");
    }

    class Handler : WinterElementHandler
    {
        // Declared values are committed together after the render batch.
        float _min, _max = 1f, _value, _step;
        bool _rangeDirty;
        readonly CoalescedEvent _valueChangedEvent;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.Slider())
        {
            _valueChangedEvent = new CoalescedEvent(this);
            SliderControl.UserValueChanged += OnUserChanged;
        }

        private void OnUserChanged(float value)
        {
            _value = value;
            _valueChangedEvent.Fire(value);
        }

        public override void Dispose()
        {
            SliderControl.UserValueChanged -= OnUserChanged;
            base.Dispose();
        }

        Marius.Winter.Slider SliderControl => (Marius.Winter.Slider)ElementControl;

        public override void CommitAttributes()
        {
            if (!_rangeDirty || IsDisposed) return;
            _rangeDirty = false;
            SliderControl.SetRange(_min, _max, _value, _step);
        }

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "InputReset":
                    var reset = AttributeHelper.GetObject<InputReset<float>>(attributeValue);
                    if (reset == null) break;
                    _valueChangedEvent.CancelPending();
                    _value = reset.Value;
                    _rangeDirty = true;
                    break;
                case "Value":
                    _value = AttributeHelper.GetFloat(attributeValue);
                    _rangeDirty = true;
                    break;
                case "Min":
                    _min = AttributeHelper.GetFloat(attributeValue);
                    _rangeDirty = true;
                    break;
                case "Max":
                    _max = AttributeHelper.GetFloat(attributeValue, 1f);
                    _rangeDirty = true;
                    break;
                case "Step":
                    _step = AttributeHelper.GetFloat(attributeValue);
                    _rangeDirty = true;
                    break;
                case "ShowValue":
                    SliderControl.ShowValue = AttributeHelper.GetBool(attributeValue);
                    break;
                case "onvaluechanged":
                    RegisterEvent(attributeEventHandlerId, _valueChangedEvent.Unregister);
                    _valueChangedEvent.HandlerId = attributeEventHandlerId;
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }
    }
}
