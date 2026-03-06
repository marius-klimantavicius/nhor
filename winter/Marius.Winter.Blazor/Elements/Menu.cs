using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class Menu : WinterComponentBase
{
    static Menu()
    {
        ElementHandlerRegistry.RegisterElementHandler<Menu>(renderer => new Handler(renderer));
    }

    /// <summary>
    /// Maps the Handler's dummy ElementControl to the native Menu object,
    /// so that child MenuItem handlers can find their parent Menu.
    /// </summary>
    internal static readonly Dictionary<Element, Marius.Winter.Menu> ElementToMenu = new();

    [Parameter] public string? Title { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        if (Title != null)
            builder.AddAttribute(nameof(Title), Title);
    }

    protected override RenderFragment? GetChildContent() => ChildContent;

    class Handler : WinterElementHandler, INonPhysicalChild, IWinterContainerElementHandler
    {
        private Marius.Winter.MenuBar? _parentMenuBar;
        private Marius.Winter.Menu? _menu;
        private string _title = "";

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.Panel()) { }

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Title":
                    _title = AttributeHelper.GetString(attributeValue) ?? "";
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }

        public void SetParent(object parentElement)
        {
            if (parentElement is Marius.Winter.MenuBar menuBar)
            {
                _parentMenuBar = menuBar;
                _menu = _parentMenuBar.AddMenu(_title);
                ElementToMenu[ElementControl] = _menu;
            }
        }

        public void Remove()
        {
            ElementToMenu.Remove(ElementControl);
            _menu = null;
            _parentMenuBar = null;
        }

        public void AddChild(Element child, int physicalSiblingIndex) { }
        public void RemoveChild(Element child) { }
        public void ReorderChildren(List<Element> newOrder) { }
        public int GetChildIndex(Element child) => -1;
    }
}
