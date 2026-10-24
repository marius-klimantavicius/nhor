using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

internal record TreeViewInfo(Marius.Winter.TreeView TreeView, Marius.Winter.TreeNode? ParentNode);

public class TreeView : WinterComponentBase
{
    static TreeView()
    {
        ElementHandlerRegistry.RegisterElementHandler<TreeView>(renderer => new Handler(renderer));
    }

    internal static readonly Dictionary<Element, TreeViewInfo> ElementToTreeInfo = new();

    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public EventCallback<string?> OnSelectionChanged { get; set; }
    [Parameter] public EventCallback<string?> OnNodeExpanded { get; set; }
    [Parameter] public EventCallback<string?> OnNodeCollapsed { get; set; }
    [Parameter] public EventCallback<string?> OnNodeDoubleClicked { get; set; }

    /// <summary>
    /// Callback invoked once after the native TreeView is created.
    /// Use to get a reference for imperative node manipulation.
    /// </summary>
    [Parameter] public Action<Marius.Winter.TreeView>? OnNativeTreeView { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);
        builder.AddAttribute("onselectionchanged", EventCallback.Factory.Create<ChangeEventArgs>(this, value => OnSelectionChanged.InvokeAsync((string?)value.Value)));
        builder.AddAttribute("onnodeexpanded", EventCallback.Factory.Create<ChangeEventArgs>(this, value => OnNodeExpanded.InvokeAsync((string?)value.Value)));
        builder.AddAttribute("onnodecollapsed", EventCallback.Factory.Create<ChangeEventArgs>(this, value => OnNodeCollapsed.InvokeAsync((string?)value.Value)));
        builder.AddAttribute("onnodedoubleclicked", EventCallback.Factory.Create<ChangeEventArgs>(this, value => OnNodeDoubleClicked.InvokeAsync((string?)value.Value)));
        if (OnNativeTreeView != null)
            builder.AddAttribute("onnativetreeview", OnNativeTreeView);
    }

    protected override RenderFragment? GetChildContent() => ChildContent;

    class Handler : WinterElementHandler, IWinterContainerElementHandler
    {
        readonly CoalescedEvent _selectionChangedEvent;
        readonly DiscreteTreeEvent _nodeExpandedEvent;
        readonly DiscreteTreeEvent _nodeCollapsedEvent;
        readonly DiscreteTreeEvent _nodeDoubleClickedEvent;

        Marius.Winter.TreeView TreeViewControl => (Marius.Winter.TreeView)ElementControl;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.TreeView())
        {
            ElementToTreeInfo[ElementControl] = new TreeViewInfo(TreeViewControl, null);

            _selectionChangedEvent = new CoalescedEvent(this);
            TreeViewControl.SelectionChanged = node => _selectionChangedEvent.Fire(node?.Tag as string);

            _nodeExpandedEvent = new DiscreteTreeEvent(this);
            TreeViewControl.NodeExpanded = node =>
            {
                _nodeExpandedEvent.Fire(node.Tag as string);
            };

            _nodeCollapsedEvent = new DiscreteTreeEvent(this);
            TreeViewControl.NodeCollapsed = node =>
            {
                _nodeCollapsedEvent.Fire(node.Tag as string);
            };

            _nodeDoubleClickedEvent = new DiscreteTreeEvent(this);
            TreeViewControl.NodeDoubleClicked = node =>
            {
                _nodeDoubleClickedEvent.Fire(node.Tag as string);
            };
        }

        // Expansion and activation are commands: preserve every immutable string-tag snapshot.
        private sealed class DiscreteTreeEvent(Handler owner)
        {
            public ulong HandlerId { get; set; }
            public void Unregister(ulong id) { if (HandlerId == id) HandlerId = 0; }
            public void Fire(string? key)
            {
                var id = HandlerId;
                if (id == 0 || owner.IsDisposed) return;
                void Deliver()
                {
                    if (!owner.IsDisposed && HandlerId == id)
                        _ = owner.Renderer.DispatchNativeEventAsync(id, null, new ChangeEventArgs { Value = key });
                }
                if (owner.ElementControl.OwnerWindow is { } window)
                    window.DispatcherQueue.Enqueue(Deliver);
                else Deliver();
            }
        }

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "onselectionchanged":
                    RegisterEvent(attributeEventHandlerId, _selectionChangedEvent.Unregister);
                    _selectionChangedEvent.HandlerId = attributeEventHandlerId;
                    break;
                case "onnodeexpanded":
                    RegisterEvent(attributeEventHandlerId, _nodeExpandedEvent.Unregister);
                    _nodeExpandedEvent.HandlerId = attributeEventHandlerId;
                    break;
                case "onnodecollapsed":
                    RegisterEvent(attributeEventHandlerId, _nodeCollapsedEvent.Unregister);
                    _nodeCollapsedEvent.HandlerId = attributeEventHandlerId;
                    break;
                case "onnodedoubleclicked":
                    RegisterEvent(attributeEventHandlerId, _nodeDoubleClickedEvent.Unregister);
                    _nodeDoubleClickedEvent.HandlerId = attributeEventHandlerId;
                    break;
                case "onnativetreeview":
                    WeakObjectStore.Get<Action<Marius.Winter.TreeView>>(attributeValue)?.Invoke(TreeViewControl);
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }

        public override void Dispose()
        {
            if (IsDisposed) return;
            ElementToTreeInfo.Remove(ElementControl);
            base.Dispose();
        }

        public void AddChild(Element child, int physicalSiblingIndex)
        {
            if (physicalSiblingIndex >= TreeViewControl.Children.Count)
                TreeViewControl.AddChild(child);
            else
                TreeViewControl.InsertChild(child, physicalSiblingIndex);
        }

        public void RemoveChild(Element child) => TreeViewControl.RemoveChild(child);

        public void ReorderChildren(List<Element> newOrder) => TreeViewControl.ReorderChildren(newOrder);

        public int GetChildIndex(Element child)
        {
            for (int i = 0; i < TreeViewControl.Children.Count; i++)
                if (TreeViewControl.Children[i] == child) return i;
            return -1;
        }
    }
}
