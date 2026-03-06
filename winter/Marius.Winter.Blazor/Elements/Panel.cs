using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class Panel : WinterComponentBase
{
    static Panel()
    {
        ElementHandlerRegistry.RegisterElementHandler<Panel>(renderer => new Handler(renderer));
    }

    [Parameter] public ILayout? Layout { get; set; }
    [Parameter] public Color4? Background { get; set; }
    [Parameter] public Color4? BorderColor { get; set; }
    [Parameter] public Thickness? Padding { get; set; }
    [Parameter] public CornerRadius? CornerRadius { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        if (Layout != null)
            builder.AddAttribute(nameof(Layout), Layout);
        if (Background.HasValue)
            builder.AddAttribute(nameof(Background), Background.Value);
        if (BorderColor.HasValue)
            builder.AddAttribute(nameof(BorderColor), BorderColor.Value);
        if (Padding.HasValue)
            builder.AddAttribute(nameof(Padding), Padding.Value);
        if (CornerRadius.HasValue)
            builder.AddAttribute(nameof(CornerRadius), CornerRadius.Value);
    }

    protected override RenderFragment? GetChildContent() => ChildContent;

    class Handler : WinterElementHandler, IWinterContainerElementHandler
    {
        Marius.Winter.Panel PanelControl => (Marius.Winter.Panel)ElementControl;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.Panel()) { }

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Layout":
                    PanelControl.Layout = WeakObjectStore.Get<ILayout>(attributeValue);
                    break;
                case "Background":
                    PanelControl.Background = AttributeHelper.GetColor4(attributeValue);
                    break;
                case "BorderColor":
                    PanelControl.BorderColor = AttributeHelper.GetColor4(attributeValue);
                    break;
                case "Padding":
                    PanelControl.Padding = AttributeHelper.GetThickness(attributeValue);
                    break;
                case "CornerRadius":
                    PanelControl.CornerRadius = AttributeHelper.GetCornerRadius(attributeValue);
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }

        public void AddChild(Element child, int physicalSiblingIndex)
        {
            if (physicalSiblingIndex >= PanelControl.Children.Count)
                PanelControl.AddChild(child);
            else
                PanelControl.InsertChild(child, physicalSiblingIndex);
        }

        public void RemoveChild(Element child) => PanelControl.RemoveChild(child);

        public void ReorderChildren(List<Element> newOrder) => PanelControl.ReorderChildren(newOrder);

        public int GetChildIndex(Element child)
        {
            for (int i = 0; i < PanelControl.Children.Count; i++)
                if (PanelControl.Children[i] == child) return i;
            return -1;
        }
    }
}
