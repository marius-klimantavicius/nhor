using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor;

public class WinterElementHandler : IWinterElementHandler, IHandleAttributeBatch, IDisposable
{
    private Element? _parent;
    private ulong _doubleClickEventHandlerId;
    private readonly HashSet<ulong> _eventIds = new();
    private readonly List<CoalescedEvent> _coalescedEvents = new();
    protected bool IsDisposed { get; private set; }

    public WinterElementHandler(NativeComponentRenderer renderer, Element elementControl)
    {
        Renderer = renderer;
        ElementControl = elementControl;

        ElementControl.DoubleClicked = () =>
        {
            if (_doubleClickEventHandlerId != 0)
                Renderer.DispatchNativeEventAsync(_doubleClickEventHandlerId, null, EventArgs.Empty);
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
                RegisterEvent(attributeEventHandlerId, id => { if (_doubleClickEventHandlerId == id) _doubleClickEventHandlerId = 0; });
                _doubleClickEventHandlerId = attributeEventHandlerId;
                break;
            default:
                throw new ArgumentException($"Attribute '{attributeName}' is not supported by {GetType().Name}.");
        }
    }

    protected void RegisterEvent(ulong id, Action<ulong> unregister)
    {
        // Zero is the element diff's clear operation, not a registration.
        if (id == 0 || IsDisposed) return;
        _eventIds.Add(id);
        Renderer.RegisterEvent(id, oldId => { _eventIds.Remove(oldId); unregister(oldId); });
    }

    public virtual void CommitAttributes() { }

    public virtual void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        foreach (var id in new List<ulong>(_eventIds)) Renderer.UnregisterEvent(id);
        foreach (var slot in _coalescedEvents) slot.HandlerId = 0;
        _coalescedEvents.Clear();
        ElementControl.DoubleClicked = null;
        switch (ElementControl)
        {
            case Marius.Winter.Button button: button.Clicked = null; break;
            case Marius.Winter.Slider slider: slider.ValueChanged = null; break;
            case Marius.Winter.ComboBox combo: combo.SelectionChanged = null; break;
            case Marius.Winter.Checkbox checkbox: checkbox.Changed = null; break;
            case Marius.Winter.TextBox textBox: textBox.TextChanged = null; break;
            case Marius.Winter.TextArea textArea: textArea.TextChanged = null; break;
            case Marius.Winter.DialogWindow dialog: dialog.CloseClicked = null; break;
            case Marius.Winter.TreeView tree:
                tree.SelectionChanged = null; tree.NodeExpanded = null; tree.NodeCollapsed = null; tree.NodeDoubleClicked = null;
                break;
        }
        _parent = null;
        if (ElementControl is IDisposable disposable) disposable.Dispose();
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
        private object? _value;
        private ulong _handlerId;
        private int _version;
        private int _pendingVersion = -1;

        public ulong HandlerId
        {
            get => _handlerId;
            set { if (_handlerId == value) return; _handlerId = value; _version++; _value = null; }
        }

        public CoalescedEvent(WinterElementHandler handler)
        {
            _handler = handler;
            handler._coalescedEvents.Add(this);
        }

        public void Fire(object? value)
        {
            if (HandlerId == 0 || _handler.IsDisposed) return;
            _value = value;
            if (_pendingVersion == _version) return;

            var window = _handler.ElementControl.OwnerWindow;
            if (window == null)
            {
                _handler.Renderer.DispatchNativeEventAsync(HandlerId, null, new ChangeEventArgs { Value = value });
                return;
            }

            var version = _version;
            var handlerId = HandlerId;
            _pendingVersion = version;
            window.DispatcherQueue.Enqueue(() =>
            {
                if (_pendingVersion == version) _pendingVersion = -1;
                if (!_handler.IsDisposed && version == _version && HandlerId == handlerId)
                    _handler.Renderer.DispatchNativeEventAsync(handlerId, null, new ChangeEventArgs { Value = _value });
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
