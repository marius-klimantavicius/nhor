using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor;

public class WinterRenderer : NativeComponentRenderer
{
    private readonly Window _window;

    public WinterRenderer(Window window, IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
        : base(serviceProvider, loggerFactory)
    {
        _window = window;
        _dispatcher = new WinterDispatcher(window);
    }

    private readonly WinterDispatcher _dispatcher;
    public override Dispatcher Dispatcher => _dispatcher;

    protected override ElementManager CreateNativeControlManager()
    {
        return new WinterElementManager();
    }

    protected override void HandleException(Exception exception)
    {
        Console.Error.WriteLine($"Blazor render error: {exception}");
    }

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
    {
        // Process all Blazor DOM changes (add/remove children, apply attributes)
        var result = base.UpdateDisplayAsync(renderBatch);

        // Re-layout window children after Blazor updates to fix stale measurements.
        // Without this, incremental child additions during page switches leave
        // panels with stale content heights (e.g. ScrollPanel shows phantom scrollbar).
        // Only re-layout when something actually changed (IsMeasureDirty), to avoid
        // measurement instability from content-less spacer panels that return
        // available space as their desired size.
        var b = _window.Bounds;
        if (b.W > 0 && b.H > 0)
        {
            bool anyDirty = false;
            foreach (var child in _window.Children)
            {
                if (child.IsMeasureDirty)
                {
                    anyDirty = true;
                    break;
                }
            }
            if (anyDirty)
            {
                foreach (var child in _window.Children)
                {
                    child.Measure(b.W, b.H);
                    child.Arrange(new RectF(0, 0, b.W, b.H));
                }
            }
            _window.Dirty = true;
        }

        return result;
    }

    public Task<TComponent> AddComponent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent>(Element parent) where TComponent : IComponent
    {
        var handler = new RootContainerHandler(this, parent);
        return AddComponent<TComponent>(handler);
    }

    public Task<TComponent> AddComponent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent>(Element parent, Dictionary<string, object> parameters) where TComponent : IComponent
    {
        var handler = new RootContainerHandler(this, parent);
        return AddComponent<TComponent>(handler, parameters);
    }

    /// <summary>
    /// Internal handler wrapping the root container element (e.g. the Window or a Panel).
    /// </summary>
    private class RootContainerHandler : IWinterContainerElementHandler
    {
        private readonly NativeComponentRenderer _renderer;

        public RootContainerHandler(NativeComponentRenderer renderer, Element element)
        {
            _renderer = renderer;
            ElementControl = element;
        }

        public Element ElementControl { get; }
        public object TargetElement => ElementControl;

        public void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
        }

        public bool IsParented() => true;
        public bool IsParentedTo(Element parent) => false;
        public void SetParent(Element parent) { }

        public void AddChild(Element child, int physicalSiblingIndex)
        {
            if (physicalSiblingIndex >= ElementControl.Children.Count)
                ElementControl.AddChild(child);
            else
                ElementControl.InsertChild(child, physicalSiblingIndex);

            // Auto-layout: measure and arrange the child to fill the parent
            var b = ElementControl.Bounds;
            if (b.W > 0 && b.H > 0)
            {
                child.Measure(b.W, b.H);
                child.Arrange(new RectF(0, 0, b.W, b.H));
            }
        }

        public void RemoveChild(Element child)
        {
            ElementControl.RemoveChild(child);
        }

        public int GetChildIndex(Element child)
        {
            for (int i = 0; i < ElementControl.Children.Count; i++)
                if (ElementControl.Children[i] == child) return i;
            return -1;
        }

        public void ReorderChildren(List<Element> newOrder) => ElementControl.ReorderChildren(newOrder);
    }
}
