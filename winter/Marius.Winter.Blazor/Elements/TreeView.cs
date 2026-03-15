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
        readonly CoalescedEvent _nodeExpandedEvent;
        readonly CoalescedEvent _nodeCollapsedEvent;
        readonly CoalescedEvent _nodeDoubleClickedEvent;

        Marius.Winter.TreeView TreeViewControl => (Marius.Winter.TreeView)ElementControl;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.TreeView())
        {
            ElementToTreeInfo[ElementControl] = new TreeViewInfo(TreeViewControl, null);

            _selectionChangedEvent = new CoalescedEvent(this);
            TreeViewControl.SelectionChanged = node => _selectionChangedEvent.Fire(node?.Tag as string);

            _nodeExpandedEvent = new CoalescedEvent(this);
            TreeViewControl.NodeExpanded = node =>
            {
                _nodeExpandedEvent.Fire(node.Tag as string);
            };

            _nodeCollapsedEvent = new CoalescedEvent(this);
            TreeViewControl.NodeCollapsed = node =>
            {
                _nodeCollapsedEvent.Fire(node.Tag as string);
            };

            _nodeDoubleClickedEvent = new CoalescedEvent(this);
            TreeViewControl.NodeDoubleClicked = node =>
            {
                _nodeDoubleClickedEvent.Fire(node.Tag as string);
            };
        }

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "onselectionchanged":
                    Renderer.RegisterEvent(attributeEventHandlerId, _selectionChangedEvent.Unregister);
                    _selectionChangedEvent.HandlerId = attributeEventHandlerId;
                    break;
                case "onnodeexpanded":
                    Renderer.RegisterEvent(attributeEventHandlerId, _nodeExpandedEvent.Unregister);
                    _nodeExpandedEvent.HandlerId = attributeEventHandlerId;
                    break;
                case "onnodecollapsed":
                    Renderer.RegisterEvent(attributeEventHandlerId, _nodeCollapsedEvent.Unregister);
                    _nodeCollapsedEvent.HandlerId = attributeEventHandlerId;
                    break;
                case "onnodedoubleclicked":
                    Renderer.RegisterEvent(attributeEventHandlerId, _nodeDoubleClickedEvent.Unregister);
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
