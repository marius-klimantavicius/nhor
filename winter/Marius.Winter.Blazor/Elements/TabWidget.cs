using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class TabWidget : InputComponentBase<int>
{
    static TabWidget()
    {
        ElementHandlerRegistry.RegisterElementHandler<TabWidget>(renderer => new Handler(renderer));
    }

    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public int ActiveTab { get; set; }
    [Parameter] public EventCallback<int> ActiveTabChanged { get; set; }
    [Parameter] public EventCallback<int> OnActiveTabChanged { get; set; }
    public bool Focus() => TryFocus();
    private bool _controlled;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        _controlled = parameters.TryGetValue<int>(nameof(ActiveTab), out _);
        return base.SetParametersAsync(parameters);
    }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);
        if (_controlled)
        {
            RenderReset(builder, ActiveTab);
            builder.AddAttribute(nameof(ActiveTab), ActiveTab);
        }
        builder.AddAttribute("onactivetabchanged", BindChange(ActiveTabChanged, OnActiveTabChanged, value => ActiveTab = value), nameof(ActiveTab));
    }

    protected override RenderFragment? GetChildContent() => ChildContent;

    class Handler : WinterElementHandler, IWinterContainerElementHandler
    {
        private readonly CoalescedEvent _changed;
        private int _index;
        private bool _selectionDirty;
        Marius.Winter.TabWidget TabWidgetControl => (Marius.Winter.TabWidget)ElementControl;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.TabWidget())
        {
            _changed = new CoalescedEvent(this);
            TabWidgetControl.ActiveTabChanged += OnStructuralChange;
            TabWidgetControl.UserActiveTabChanged += OnUserChange;
        }

        private void OnStructuralChange(int _) => _changed.CancelPending();
        private void OnUserChange(int index) => _changed.Fire(index);

        public override void ApplyAttribute(ulong id, string name, object value, string updates)
        {
            switch (name)
            {
                case "InputReset":
                    var reset = AttributeHelper.GetObject<InputReset<int>>(value);
                    if (reset == null) break;
                    _changed.CancelPending(); _index = reset.Value; _selectionDirty = true; break;
                case nameof(ActiveTab):
                    // Removing the parameter returns to native identity-preserving, uncontrolled selection.
                    if (value != null) { _index = AttributeHelper.GetInt(value, -1); _selectionDirty = true; }
                    break;
                case "onactivetabchanged":
                    RegisterEvent(id, _changed.Unregister); _changed.HandlerId = id; break;
                default: base.ApplyAttribute(id, name, value, updates); break;
            }
        }

        public override void CommitAttributes()
        {
            if (!_selectionDirty || IsDisposed) return;
            _selectionDirty = false; TabWidgetControl.ActiveTab = _index;
        }

        public override void Dispose()
        {
            TabWidgetControl.ActiveTabChanged -= OnStructuralChange;
            TabWidgetControl.UserActiveTabChanged -= OnUserChange;
            base.Dispose();
        }

        public void AddChild(Element child, int physicalSiblingIndex)
        {
            if (physicalSiblingIndex >= TabWidgetControl.Children.Count)
                TabWidgetControl.AddChild(child);
            else
                TabWidgetControl.InsertChild(child, physicalSiblingIndex);
        }

        public void RemoveChild(Element child) => TabWidgetControl.RemoveChild(child);

        public void ReorderChildren(List<Element> newOrder) => TabWidgetControl.ReorderChildren(newOrder);

        public int GetChildIndex(Element child)
        {
            for (int i = 0; i < TabWidgetControl.Children.Count; i++)
                if (TabWidgetControl.Children[i] == child) return i;
            return -1;
        }
    }
}
