using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class Tab : WinterComponentBase
{
    static Tab()
    {
        ElementHandlerRegistry.RegisterElementHandler<Tab>(renderer => new Handler(renderer));
    }

    [Parameter] public string? Label { get; set; }
    [Parameter] public ILayout? Layout { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        if (Label != null)
            builder.AddAttribute(nameof(Label), Label);
        if (Layout != null)
            builder.AddAttribute(nameof(Layout), Layout);
    }

    protected override RenderFragment? GetChildContent() => ChildContent;

    class Handler : WinterElementHandler, INonPhysicalChild, IWinterContainerElementHandler
    {
        private Marius.Winter.TabWidget? _parentTabWidget;
        private string _label = "";

        Marius.Winter.Panel InnerPanel { get; }

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.Panel())
        {
            InnerPanel = (Marius.Winter.Panel)ElementControl;
            InnerPanel.Layout = new Marius.Winter.FlexLayout { Direction = Orientation.Vertical, AlignItems = Alignment.Stretch };
        }

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Label":
                    _label = AttributeHelper.GetString(attributeValue) ?? "";
                    break;
                case "Layout":
                    InnerPanel.Layout = WeakObjectStore.Get<ILayout>(attributeValue);
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }

        public void SetParent(object parentElement)
        {
            if (parentElement is Marius.Winter.TabWidget tabWidget)
            {
                _parentTabWidget = tabWidget;
                _parentTabWidget.AddTab(_label, InnerPanel);
            }
        }

        public void Remove()
        {
            if (_parentTabWidget != null)
            {
                for (int i = 0; i < _parentTabWidget.TabCount; i++)
                {
                    if (i < _parentTabWidget.Children.Count && _parentTabWidget.Children[i] == InnerPanel)
                    {
                        _parentTabWidget.RemoveTab(i);
                        break;
                    }
                }
                _parentTabWidget = null;
            }
        }

        public void AddChild(Element child, int physicalSiblingIndex)
        {
            // InnerPanel uses a vertical FlexLayout. Children (typically a single
            // ScrollPanel or Panel) must grow to fill the available tab height;
            // without Grow=1 they collapse to their intrinsic height (often 0 for ScrollPanel).
            if (child.LayoutData == null)
                child.LayoutData = new FlexItem { Grow = 1 };

            if (physicalSiblingIndex >= InnerPanel.Children.Count)
                InnerPanel.AddChild(child);
            else
                InnerPanel.InsertChild(child, physicalSiblingIndex);
        }

        public void RemoveChild(Element child) => InnerPanel.RemoveChild(child);

        public void ReorderChildren(List<Element> newOrder) => InnerPanel.ReorderChildren(newOrder);

        public int GetChildIndex(Element child)
        {
            for (int i = 0; i < InnerPanel.Children.Count; i++)
                if (InnerPanel.Children[i] == child) return i;
            return -1;
        }
    }
}
