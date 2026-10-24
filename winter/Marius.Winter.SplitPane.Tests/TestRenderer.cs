using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Tests;

internal sealed class TestRenderer : NativeComponentRenderer
{
    public TestRenderer() : base(new ServiceCollection().BuildServiceProvider(), LoggerFactory.Create(_ => { })) { }
    protected override ElementManager CreateNativeControlManager() => (ElementManager)Activator.CreateInstance(
        typeof(WinterElementHandler).Assembly.GetType("Marius.Winter.Blazor.WinterElementManager", true)!, true)!;
    protected override void HandleException(Exception exception) => ExceptionDispatchInfo.Capture(exception).Throw();
    public int ConfigurationDiffs { get; private set; }
    protected override Task UpdateDisplayAsync(in RenderBatch batch)
    {
        foreach (var component in batch.UpdatedComponents.Array.AsSpan(0, batch.UpdatedComponents.Count))
            foreach (var edit in component.Edits)
                if (edit.Type == RenderTreeEditType.SetAttribute && batch.ReferenceFrames.Array[edit.ReferenceFrameIndex].AttributeName == "Configuration")
                    ConfigurationDiffs++;
        return base.UpdateDisplayAsync(batch);
    }
    public new Task<T> AddComponent<T>(IElementHandler parent, Dictionary<string, object>? parameters = null) where T : IComponent => base.AddComponent<T>(parent, parameters);
    public static RootHandler CreateRootHandler(NativeComponentRenderer renderer, Panel panel) => new(panel);
    internal sealed class RootHandler(Panel panel) : IWinterContainerElementHandler
    {
        public Element ElementControl => panel;
        public object TargetElement => panel;
        public void ApplyAttribute(ulong id, string name, object value, string updates) { }
        public bool IsParented() => true;
        public bool IsParentedTo(Element parent) => false;
        public void SetParent(Element parent) { }
        public void AddChild(Element child, int index) => panel.InsertChild(child, Math.Min(index, panel.Children.Count));
        public void RemoveChild(Element child) => panel.RemoveChild(child);
        public int GetChildIndex(Element child)
        {
            for (int i = 0; i < panel.Children.Count; i++) if (ReferenceEquals(panel.Children[i], child)) return i;
            return -1;
        }
        public void ReorderChildren(List<Element> order) => panel.ReorderChildren(order);
    }
}
