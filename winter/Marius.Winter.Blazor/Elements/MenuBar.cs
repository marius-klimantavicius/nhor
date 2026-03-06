using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class MenuBar : WinterComponentBase
{
    static MenuBar()
    {
        ElementHandlerRegistry.RegisterElementHandler<MenuBar>(renderer => new Handler(renderer));
    }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override RenderFragment? GetChildContent() => ChildContent;

    class Handler : WinterElementHandler, IWinterContainerElementHandler
    {
        Marius.Winter.MenuBar MenuBarControl => (Marius.Winter.MenuBar)ElementControl;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.MenuBar()) { }

        public void AddChild(Element child, int physicalSiblingIndex)
        {
            if (physicalSiblingIndex >= MenuBarControl.Children.Count)
                MenuBarControl.AddChild(child);
            else
                MenuBarControl.InsertChild(child, physicalSiblingIndex);
        }

        public void RemoveChild(Element child) => MenuBarControl.RemoveChild(child);

        public void ReorderChildren(List<Element> newOrder) => MenuBarControl.ReorderChildren(newOrder);

        public int GetChildIndex(Element child)
        {
            for (int i = 0; i < MenuBarControl.Children.Count; i++)
                if (MenuBarControl.Children[i] == child) return i;
            return -1;
        }
    }
}
