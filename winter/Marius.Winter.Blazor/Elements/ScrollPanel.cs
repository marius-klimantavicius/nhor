using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class ScrollPanel : WinterComponentBase
{
    static ScrollPanel()
    {
        ElementHandlerRegistry.RegisterElementHandler<ScrollPanel>(renderer => new Handler(renderer));
    }

    [Parameter] public ILayout? Layout { get; set; }
    [Parameter] public Color4? Background { get; set; }
    [Parameter] public Thickness? Padding { get; set; }
    [Parameter] public ScrollMode VerticalScroll { get; set; } = ScrollMode.Auto;
    [Parameter] public ScrollMode HorizontalScroll { get; set; } = ScrollMode.Auto;
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        if (Layout != null)
            builder.AddAttribute(nameof(Layout), Layout);
        if (Background.HasValue)
            builder.AddAttribute(nameof(Background), Background.Value);
        if (Padding.HasValue)
            builder.AddAttribute(nameof(Padding), Padding.Value);
        if (VerticalScroll != ScrollMode.Auto)
            builder.AddAttribute(nameof(VerticalScroll), VerticalScroll);
        if (HorizontalScroll != ScrollMode.Auto)
            builder.AddAttribute(nameof(HorizontalScroll), HorizontalScroll);
    }

    protected override RenderFragment? GetChildContent() => ChildContent;

    class Handler : WinterElementHandler, IWinterContainerElementHandler
    {
        Marius.Winter.ScrollPanel ScrollPanelControl => (Marius.Winter.ScrollPanel)ElementControl;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.ScrollPanel()) { }

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Layout":
                    ScrollPanelControl.Layout = WeakObjectStore.Get<ILayout>(attributeValue);
                    break;
                case "Background":
                    ScrollPanelControl.Background = AttributeHelper.GetColor4(attributeValue);
                    break;
                case "Padding":
                    ScrollPanelControl.Padding = AttributeHelper.GetThickness(attributeValue);
                    break;
                case "VerticalScroll":
                    ScrollPanelControl.VerticalScroll = AttributeHelper.GetEnum<ScrollMode>(attributeValue);
                    break;
                case "HorizontalScroll":
                    ScrollPanelControl.HorizontalScroll = AttributeHelper.GetEnum<ScrollMode>(attributeValue);
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }

        public void AddChild(Element child, int physicalSiblingIndex)
        {
            if (physicalSiblingIndex >= ScrollPanelControl.Children.Count)
                ScrollPanelControl.AddChild(child);
            else
                ScrollPanelControl.InsertChild(child, physicalSiblingIndex);
        }

        public void RemoveChild(Element child) => ScrollPanelControl.RemoveChild(child);

        public void ReorderChildren(List<Element> newOrder) => ScrollPanelControl.ReorderChildren(newOrder);

        public int GetChildIndex(Element child)
        {
            for (int i = 0; i < ScrollPanelControl.Children.Count; i++)
                if (ScrollPanelControl.Children[i] == child) return i;
            return -1;
        }
    }
}
