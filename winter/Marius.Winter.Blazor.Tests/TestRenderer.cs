using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Marius.Winter.Blazor;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Tests;

/// <summary>
/// Minimal renderer for headless Blazor tests. Does not require a Window or GLFW.
/// </summary>
internal class TestRenderer : NativeComponentRenderer
{
    public TestRenderer()
        : base(
            new ServiceCollection().BuildServiceProvider(),
            LoggerFactory.Create(_ => { }))
    {
    }

    protected override ElementManager CreateNativeControlManager()
    {
        return new WinterElementManager();
    }

    protected override void HandleException(Exception exception)
    {
        throw exception;
    }

    /// <summary>
    /// Adds a component with the given parent handler and returns it.
    /// Must be called from the dispatcher thread (which is the current thread
    /// since we use the default synchronous dispatcher).
    /// </summary>
    public new async Task<TComponent> AddComponent<TComponent>(
        IElementHandler parent, Dictionary<string, object>? parameters = null)
        where TComponent : IComponent
    {
        return await base.AddComponent<TComponent>(parent, parameters);
    }

    /// <summary>
    /// Creates a root container handler wrapping a Panel element.
    /// </summary>
    public static RootHandler CreateRootHandler(NativeComponentRenderer renderer, Panel panel)
    {
        return new RootHandler(renderer, panel);
    }

    internal class RootHandler : IWinterContainerElementHandler
    {
        private readonly NativeComponentRenderer _renderer;

        public RootHandler(NativeComponentRenderer renderer, Panel panel)
        {
            _renderer = renderer;
            ElementControl = panel;
        }

        public Element ElementControl { get; }
        public object TargetElement => ElementControl;

        public void ApplyAttribute(ulong attributeEventHandlerId, string attributeName,
            object attributeValue, string attributeEventUpdatesAttributeName) { }

        public bool IsParented() => true;
        public bool IsParentedTo(Element parent) => false;
        public void SetParent(Element parent) { }

        public void AddChild(Element child, int physicalSiblingIndex)
        {
            if (physicalSiblingIndex >= ElementControl.Children.Count)
                ElementControl.AddChild(child);
            else
                ElementControl.InsertChild(child, physicalSiblingIndex);
        }

        public void RemoveChild(Element child) => ElementControl.RemoveChild(child);

        public int GetChildIndex(Element child)
        {
            for (int i = 0; i < ElementControl.Children.Count; i++)
                if (ElementControl.Children[i] == child) return i;
            return -1;
        }

        public void ReorderChildren(List<Element> newOrder) => ElementControl.ReorderChildren(newOrder);
    }
}
