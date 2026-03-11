using System;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class MenuItem : WinterComponentBase
{
    static MenuItem()
    {
        ElementHandlerRegistry.RegisterElementHandler<MenuItem>(renderer => new Handler(renderer));
    }

    [Parameter] public string? Label { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        if (Label != null)
            builder.AddAttribute(nameof(Label), Label);

        builder.AddAttribute("onclick", OnClick);
    }

    class Handler : WinterElementHandler, INonPhysicalChild
    {
        private string _label = "";
        private ulong _clickEventHandlerId;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.Panel()) { }

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Label":
                    _label = AttributeHelper.GetString(attributeValue) ?? "";
                    break;
                case "onclick":
                    Renderer.RegisterEvent(attributeEventHandlerId, id => { if (_clickEventHandlerId == id) { _clickEventHandlerId = 0; } });
                    _clickEventHandlerId = attributeEventHandlerId;
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }

        public void SetParent(object parentElement)
        {
            if (parentElement is Element element && Menu.ElementToMenu.TryGetValue(element, out var menu))
            {
                var renderer = Renderer;
                menu.AddItem(_label, () =>
                {
                    if (_clickEventHandlerId != 0)
                        renderer.DispatchEventAsync(_clickEventHandlerId, null, EventArgs.Empty);
                });
            }
        }

        public void Remove()
        {
        }
    }
}
