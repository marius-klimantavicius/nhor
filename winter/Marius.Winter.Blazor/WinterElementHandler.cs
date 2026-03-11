using System;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor;

public class WinterElementHandler : IWinterElementHandler
{
    private Element? _parent;
    private ulong _doubleClickEventHandlerId;

    public WinterElementHandler(NativeComponentRenderer renderer, Element elementControl)
    {
        Renderer = renderer;
        ElementControl = elementControl;

        ElementControl.DoubleClicked = () =>
        {
            if (_doubleClickEventHandlerId != 0)
                Renderer.DispatchEventAsync(_doubleClickEventHandlerId, null, EventArgs.Empty);
        };
    }

    public NativeComponentRenderer Renderer { get; }
    public Element ElementControl { get; }
    public object TargetElement => ElementControl;

    public virtual void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
    {
        switch (attributeName)
        {
            case "Visible":
                ElementControl.Visible = AttributeHelper.GetBool(attributeValue, true);
                break;
            case "Enabled":
                ElementControl.Enabled = AttributeHelper.GetBool(attributeValue, true);
                break;
            case "Opacity":
                ElementControl.Opacity = AttributeHelper.GetFloat(attributeValue, 1f);
                break;
            case "Tooltip":
                ElementControl.Tooltip = (string?)attributeValue;
                break;
            case "LayoutData":
                ElementControl.LayoutData = WeakObjectStore.Get<object>(attributeValue) ?? attributeValue;
                break;
            case "MinWidth":
                ElementControl.MinWidth = AttributeHelper.GetNullableFloat(attributeValue);
                break;
            case "MinHeight":
                ElementControl.MinHeight = AttributeHelper.GetNullableFloat(attributeValue);
                break;
            case "MaxWidth":
                ElementControl.MaxWidth = AttributeHelper.GetNullableFloat(attributeValue);
                break;
            case "MaxHeight":
                ElementControl.MaxHeight = AttributeHelper.GetNullableFloat(attributeValue);
                break;
            case "ondoubleclick":
                Renderer.RegisterEvent(attributeEventHandlerId, id => { if (_doubleClickEventHandlerId == id) _doubleClickEventHandlerId = 0; });
                _doubleClickEventHandlerId = attributeEventHandlerId;
                break;
            default:
                throw new ArgumentException($"Attribute '{attributeName}' is not supported by {GetType().Name}.");
        }
    }

    protected static void SetLayoutOn(object parentElement, ILayout layout)
    {
        if (parentElement is ILayoutContainer container)
            container.Layout = layout;
    }

    /// <summary>
    /// Coalesces rapid-fire native events so that at most one Blazor
    /// DispatchEventAsync call happens per frame (per event slot).
    /// Safe for both high-frequency (mouse drag, key repeat) and
    /// low-frequency (click) events — low-frequency events pass through
    /// without coalescing since no second fire arrives before the queue drains.
    /// </summary>
    protected class CoalescedEvent
    {
        private readonly WinterElementHandler _handler;
        private bool _pending;
        private readonly ChangeEventArgs _args = new();

        public ulong HandlerId;

        public CoalescedEvent(WinterElementHandler handler) => _handler = handler;

        public void Fire(object? value)
        {
            if (HandlerId == 0) return;
            _args.Value = value;
            if (_pending) return;

            var window = _handler.ElementControl.OwnerWindow;
            if (window == null)
            {
                _handler.Renderer.DispatchEventAsync(HandlerId, null, _args);
                return;
            }

            _pending = true;
            window.DispatcherQueue.Enqueue(() =>
            {
                _pending = false;
                if (HandlerId != 0)
                    _handler.Renderer.DispatchEventAsync(HandlerId, null, _args);
            });
        }

        public void Unregister(ulong id)
        {
            if (HandlerId == id) HandlerId = 0;
        }
    }

    public virtual bool IsParented() => _parent != null;
    public virtual bool IsParentedTo(Element parent) => _parent == parent;

    public virtual void SetParent(Element parent)
    {
        _parent = parent;
    }
}
