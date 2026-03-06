using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class TreeNode : WinterComponentBase
{
    static TreeNode()
    {
        ElementHandlerRegistry.RegisterElementHandler<TreeNode>(renderer => new Handler(renderer));
    }

    [Parameter] public string? Text { get; set; }
    [Parameter] public string? Icon { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        if (Text != null)
            builder.AddAttribute(nameof(Text), Text);
        if (Icon != null)
            builder.AddAttribute(nameof(Icon), Icon);
    }

    protected override RenderFragment? GetChildContent() => ChildContent;

    class Handler : WinterElementHandler, INonPhysicalChild, IWinterContainerElementHandler
    {
        private Marius.Winter.TreeView? _treeView;
        private Marius.Winter.TreeNode? _treeNode;
        private string _text = "";
        private string? _iconSvg;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.Panel()) { }

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Text":
                    _text = AttributeHelper.GetString(attributeValue) ?? "";
                    break;
                case "Icon":
                    _iconSvg = AttributeHelper.GetString(attributeValue);
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }

        public void SetParent(object parentElement)
        {
            if (parentElement is Element element)
            {
                if (TreeView.ElementToTreeInfo.TryGetValue(element, out var info))
                {
                    _treeView = info.TreeView;
                    _treeNode = _treeView.AddNode(_text, info.ParentNode, _iconSvg);

                    TreeView.ElementToTreeInfo[ElementControl] = new TreeViewInfo(_treeView, _treeNode);
                }
            }
        }

        public void Remove()
        {
            TreeView.ElementToTreeInfo.Remove(ElementControl);

            if (_treeView != null && _treeNode != null)
            {
                _treeView.RemoveNode(_treeNode);
            }

            _treeView = null;
            _treeNode = null;
        }

        public void AddChild(Element child, int physicalSiblingIndex) { }
        public void RemoveChild(Element child) { }
        public void ReorderChildren(List<Element> newOrder) { }
        public int GetChildIndex(Element child) => -1;
    }
}
