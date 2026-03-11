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
        readonly CoalescedEvent _changedEvent;
        bool _pendingUserChange;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.Checkbox())
        {
            _changedEvent = new CoalescedEvent(this);
            CheckboxControl.Changed = value =>
            {
                _pendingUserChange = true;
                _changedEvent.Fire(value);
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
                    var val = AttributeHelper.GetBool(attributeValue);
                    if (_pendingUserChange)
                    {
                        // Blazor re-rendered before async event processed —
                        // accept only if it matches native state (confirms the change),
                        // otherwise skip stale value
                        if (val == CheckboxControl.IsChecked)
                            _pendingUserChange = false;
                    }
                    else
                    {
                        CheckboxControl.IsChecked = val;
                    }
                    break;
                case "onchanged":
                    Renderer.RegisterEvent(attributeEventHandlerId, _changedEvent.Unregister);
                    _changedEvent.HandlerId = attributeEventHandlerId;
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }
    }
}
