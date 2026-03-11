using System;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class ComboBox : WinterComponentBase
{
    static ComboBox()
    {
        ElementHandlerRegistry.RegisterElementHandler<ComboBox>(
            renderer => new Handler(renderer));
    }

    [Parameter] public string[]? Items { get; set; }
    [Parameter] public int SelectedIndex { get; set; }
    [Parameter] public EventCallback<int> OnSelectionChanged { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        if (Items != null)
            builder.AddAttribute("Items", Items);

        builder.AddAttribute("SelectedIndex", SelectedIndex);
        builder.AddAttribute("onselectionchanged", EventCallback.Factory.Create<ChangeEventArgs>(this, value => OnSelectionChanged.InvokeAsync((int)value.Value!)));
    }

    class Handler : WinterElementHandler
    {
        // Shadow state — always holds the latest Blazor-declared values.
        string[] _items = Array.Empty<string>();
        int _selectedIndex = -1;
        ulong OnSelectionChangedEventHandlerId;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.ComboBox())
        {
            ComboBoxControl.SelectionChanged = index =>
            {
                if (OnSelectionChangedEventHandlerId != 0)
                    Renderer.DispatchEventAsync(OnSelectionChangedEventHandlerId, null, new ChangeEventArgs { Value = index });
            };
        }

        Marius.Winter.ComboBox ComboBoxControl => (Marius.Winter.ComboBox)ElementControl;

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Items":
                    _items = AttributeHelper.GetStringArray(attributeValue) ?? Array.Empty<string>();
                    ComboBoxControl.SetItemsAndIndex(_items, _selectedIndex);
                    break;
                case "SelectedIndex":
                    _selectedIndex = AttributeHelper.GetInt(attributeValue, -1);
                    ComboBoxControl.SetItemsAndIndex(_items, _selectedIndex);
                    break;
                case "onselectionchanged":
                    Renderer.RegisterEvent(attributeEventHandlerId, id =>
                    {
                        if (OnSelectionChangedEventHandlerId == id)
                            OnSelectionChangedEventHandlerId = 0;
                    });
                    OnSelectionChangedEventHandlerId = attributeEventHandlerId;
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }
    }
}
