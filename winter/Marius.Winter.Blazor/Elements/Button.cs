using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class Button : WinterComponentBase
{
    static Button()
    {
        ElementHandlerRegistry.RegisterElementHandler<Button>(renderer => new Handler(renderer));
    }

    [Parameter] public string? Text { get; set; }
    [Parameter] public Color4? Background { get; set; }
    [Parameter] public Color4? Color { get; set; }
    [Parameter] public Alignment? TextAlign { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        if (Text != null)
            builder.AddAttribute("Text", Text);
        if (Background.HasValue)
            builder.AddAttribute(nameof(Background), Background.Value);
        if (Color.HasValue)
            builder.AddAttribute(nameof(Color), Color.Value);
        if (TextAlign.HasValue)
            builder.AddAttribute(nameof(TextAlign), TextAlign.Value);

        builder.AddAttribute("onclick", OnClick);
    }

    protected override RenderFragment? GetChildContent() => ChildContent!;

    class Handler : WinterElementHandler, IHandleChildContentText, IWinterContainerElementHandler
    {
        private ulong _clickEventHandlerId;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.Button())
        {
            ButtonControl.Clicked = () =>
            {
                if (_clickEventHandlerId != 0)
                    Renderer.Dispatcher.InvokeAsync(() => Renderer.DispatchEventAsync(_clickEventHandlerId, null, EventArgs.Empty));
            };
        }

        Marius.Winter.Button ButtonControl => (Marius.Winter.Button)ElementControl;

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Text":
                    ButtonControl.Text = AttributeHelper.GetString(attributeValue) ?? "";
                    break;
                case "Background":
                    ButtonControl.Background = attributeValue != null ? AttributeHelper.GetColor4(attributeValue) : null;
                    break;
                case "Color":
                    ButtonControl.Color = attributeValue != null ? AttributeHelper.GetColor4(attributeValue) : null;
                    break;
                case "TextAlign":
                    ButtonControl.TextAlign = AttributeHelper.GetEnum(attributeValue, Alignment.Center);
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

        private int _lastTextIndex = -1;

        public void HandleText(int index, string text)
        {
            if (index == 0 || index <= _lastTextIndex)
            {
                ButtonControl.Text = text?.TrimStart() ?? "";
            }
            else
            {
                ButtonControl.Text += text ?? "";
            }
            _lastTextIndex = index;
        }

        public void AddChild(Element child, int physicalSiblingIndex)
        {
            if (physicalSiblingIndex >= ButtonControl.Children.Count)
                ButtonControl.AddChild(child);
            else
                ButtonControl.InsertChild(child, physicalSiblingIndex);
        }

        public void RemoveChild(Element child) => ButtonControl.RemoveChild(child);

        public void ReorderChildren(List<Element> newOrder) => ButtonControl.ReorderChildren(newOrder);

        public int GetChildIndex(Element child)
        {
            for (int i = 0; i < ButtonControl.Children.Count; i++)
                if (ButtonControl.Children[i] == child) return i;
            return -1;
        }
    }
}
