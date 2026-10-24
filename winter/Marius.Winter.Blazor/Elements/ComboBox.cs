using System;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class ComboBox : InputComponentBase<int>
{
    static ComboBox()
    {
        ElementHandlerRegistry.RegisterElementHandler<ComboBox>(
            renderer => new Handler(renderer));
    }

    [Parameter] public string[]? Items { get; set; }
    [Parameter] public int SelectedIndex { get; set; }
    [Parameter] public EventCallback<int> SelectedIndexChanged { get; set; }
    [Parameter] public EventCallback<int> OnSelectionChanged { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);
        RenderReset(builder, SelectedIndex);

        builder.AddAttribute("Items", (object)(Items ?? Array.Empty<string>()));

        builder.AddAttribute("SelectedIndex", SelectedIndex);
        builder.AddAttribute("onselectionchanged", BindChange(SelectedIndexChanged, OnSelectionChanged, value => SelectedIndex = value), "SelectedIndex");
    }

    class Handler : WinterElementHandler
    {
        // Declared values are committed together after the render batch.
        string[] _items = Array.Empty<string>();
        int _selectedIndex = -1;
        bool _selectionDirty;
        readonly CoalescedEvent _selectionChangedEvent;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.ComboBox())
        {
            _selectionChangedEvent = new CoalescedEvent(this);
            ComboBoxControl.UserSelectionChanged += OnUserChanged;
        }

        private void OnUserChanged(int value)
        {
            _selectedIndex = value;
            _selectionChangedEvent.Fire(value);
        }

        public override void Dispose()
        {
            ComboBoxControl.UserSelectionChanged -= OnUserChanged;
            base.Dispose();
        }

        Marius.Winter.ComboBox ComboBoxControl => (Marius.Winter.ComboBox)ElementControl;

        public override void CommitAttributes()
        {
            if (!_selectionDirty || IsDisposed) return;
            _selectionDirty = false;
            ComboBoxControl.SetItemsAndIndex(_items, _selectedIndex);
        }

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "InputReset":
                    var reset = AttributeHelper.GetObject<InputReset<int>>(attributeValue);
                    if (reset == null) break;
                    _selectionChangedEvent.CancelPending();
                    _selectedIndex = reset.Value;
                    _selectionDirty = true;
                    break;
                case "Items":
                    _items = AttributeHelper.GetObject<string[]>(attributeValue) ?? AttributeHelper.GetStringArray(attributeValue) ?? Array.Empty<string>();
                    _selectionDirty = true;
                    break;
                case "SelectedIndex":
                    _selectedIndex = AttributeHelper.GetInt(attributeValue, -1);
                    _selectionDirty = true;
                    break;
                case "onselectionchanged":
                    RegisterEvent(attributeEventHandlerId, _selectionChangedEvent.Unregister);
                    _selectionChangedEvent.HandlerId = attributeEventHandlerId;
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }
    }
}
