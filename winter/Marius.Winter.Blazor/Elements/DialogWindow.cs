using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class DialogWindow : WinterComponentBase
{
    static DialogWindow()
    {
        ElementHandlerRegistry.RegisterElementHandler<DialogWindow>(renderer => new Handler(renderer));
    }

    [Parameter] public string? Title { get; set; }
    [Parameter] public ILayout? Layout { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        if (Title != null)
            builder.AddAttribute(nameof(Title), Title);
        if (Layout != null)
            builder.AddAttribute(nameof(Layout), Layout);
    }

    protected override RenderFragment? GetChildContent() => ChildContent;

    class Handler : WinterElementHandler, IWinterContainerElementHandler
    {
        Marius.Winter.DialogWindow DialogWindowControl => (Marius.Winter.DialogWindow)ElementControl;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.DialogWindow()) { }

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Title":
                    DialogWindowControl.Title = AttributeHelper.GetString(attributeValue) ?? "Window";
                    break;
                case "Layout":
                    DialogWindowControl.Layout = WeakObjectStore.Get<ILayout>(attributeValue);
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }

        public void AddChild(Element child, int physicalSiblingIndex)
        {
            if (physicalSiblingIndex >= DialogWindowControl.Children.Count)
                DialogWindowControl.AddChild(child);
            else
                DialogWindowControl.InsertChild(child, physicalSiblingIndex);
        }

        public void RemoveChild(Element child) => DialogWindowControl.RemoveChild(child);

        public void ReorderChildren(List<Element> newOrder) => DialogWindowControl.ReorderChildren(newOrder);

        public int GetChildIndex(Element child)
        {
            for (int i = 0; i < DialogWindowControl.Children.Count; i++)
                if (DialogWindowControl.Children[i] == child) return i;
            return -1;
        }
    }
}
