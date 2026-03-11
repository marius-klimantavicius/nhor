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

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);
        builder.AddAttribute("onselectionchanged", EventCallback.Factory.Create<ChangeEventArgs>(this, value => OnSelectionChanged.InvokeAsync((string?)value.Value)));
    }

    protected override RenderFragment? GetChildContent() => ChildContent;

    class Handler : WinterElementHandler, IWinterContainerElementHandler
    {
        readonly CoalescedEvent _selectionChangedEvent;

        Marius.Winter.TreeView TreeViewControl => (Marius.Winter.TreeView)ElementControl;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.TreeView())
        {
            ElementToTreeInfo[ElementControl] = new TreeViewInfo(TreeViewControl, null);

            _selectionChangedEvent = new CoalescedEvent(this);
            TreeViewControl.SelectionChanged = node => _selectionChangedEvent.Fire(node?.Tag as string);
        }

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "onselectionchanged":
                    Renderer.RegisterEvent(attributeEventHandlerId, _selectionChangedEvent.Unregister);
                    _selectionChangedEvent.HandlerId = attributeEventHandlerId;
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
