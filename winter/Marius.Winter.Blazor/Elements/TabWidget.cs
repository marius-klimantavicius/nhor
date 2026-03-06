using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class TabWidget : WinterComponentBase
{
    static TabWidget()
    {
        ElementHandlerRegistry.RegisterElementHandler<TabWidget>(renderer => new Handler(renderer));
    }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override RenderFragment? GetChildContent() => ChildContent;

    class Handler : WinterElementHandler, IWinterContainerElementHandler
    {
        Marius.Winter.TabWidget TabWidgetControl => (Marius.Winter.TabWidget)ElementControl;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.TabWidget()) { }

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
