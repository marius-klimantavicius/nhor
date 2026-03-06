using System;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor;

public class WinterElementHandler : IWinterElementHandler
{
    private Element? _parent;

    public WinterElementHandler(NativeComponentRenderer renderer, Element elementControl)
    {
        Renderer = renderer;
        ElementControl = elementControl;
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
            default:
                throw new ArgumentException($"Attribute '{attributeName}' is not supported by {GetType().Name}.");
        }
    }

    protected static void SetLayoutOn(object parentElement, ILayout layout)
    {
        if (parentElement is ILayoutContainer container)
            container.Layout = layout;
    }

    public virtual bool IsParented() => _parent != null;
    public virtual bool IsParentedTo(Element parent) => _parent == parent;

    public virtual void SetParent(Element parent)
    {
        _parent = parent;
    }
}
