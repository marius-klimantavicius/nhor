using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class Checkbox : InputComponentBase<bool>
{
    static Checkbox()
    {
        ElementHandlerRegistry.RegisterElementHandler<Checkbox>(renderer => new Handler(renderer));
    }

    [Parameter] public string? Text { get; set; }
    [Parameter] public bool IsChecked { get; set; }
    [Parameter] public EventCallback<bool> IsCheckedChanged { get; set; }
    [Parameter] public EventCallback<bool> OnChanged { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);
        RenderReset(builder, IsChecked);

        builder.AddAttribute("Text", Text ?? "");

        builder.AddAttribute("IsChecked", IsChecked);
        builder.AddAttribute("onchanged", BindChange(IsCheckedChanged, OnChanged, value => IsChecked = value), "IsChecked");
    }

    class Handler : WinterElementHandler
    {
        readonly CoalescedEvent _changedEvent;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.Checkbox())
        {
            _changedEvent = new CoalescedEvent(this);
            CheckboxControl.UserChanged += OnUserChanged;
        }

        private void OnUserChanged(bool value) => _changedEvent.Fire(value);

        public override void Dispose()
        {
            CheckboxControl.UserChanged -= OnUserChanged;
            base.Dispose();
        }

        Marius.Winter.Checkbox CheckboxControl => (Marius.Winter.Checkbox)ElementControl;

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "InputReset":
                    var reset = AttributeHelper.GetObject<InputReset<bool>>(attributeValue);
                    if (reset == null) break;
                    _changedEvent.CancelPending();
                    CheckboxControl.IsChecked = reset.Value;
                    break;
                case "Text":
                    CheckboxControl.Text = AttributeHelper.GetString(attributeValue) ?? "";
                    break;
                case "IsChecked":
                    CheckboxControl.IsChecked = AttributeHelper.GetBool(attributeValue);
                    break;
                case "onchanged":
                    RegisterEvent(attributeEventHandlerId, _changedEvent.Unregister);
                    _changedEvent.HandlerId = attributeEventHandlerId;
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }
    }
}
