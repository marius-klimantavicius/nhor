using System;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor;

internal class WinterElementManager : ElementManager<IWinterElementHandler>
{
    protected override bool IsParented(IWinterElementHandler handler)
    {
        return handler.IsParented();
    }

    protected override void AddChildElement(
        IWinterElementHandler parentHandler,
        IWinterElementHandler childHandler,
        int physicalSiblingIndex)
    {
        if (childHandler is INonPhysicalChild nonPhysicalChild)
        {
            nonPhysicalChild.SetParent(parentHandler.ElementControl);
            return;
        }

        if (parentHandler is not IWinterContainerElementHandler container)
        {
            throw new NotSupportedException(
                $"Handler of type '{parentHandler.GetType().FullName}' representing element type " +
                $"'{parentHandler.ElementControl?.GetType().FullName ?? "<null>"}' doesn't support adding a child " +
                $"(child type is '{childHandler.ElementControl?.GetType().FullName}').");
        }

        container.AddChild(childHandler.ElementControl, physicalSiblingIndex);
        childHandler.SetParent(parentHandler.ElementControl);
    }

    protected override int GetChildElementIndex(
        IWinterElementHandler parentHandler,
        IWinterElementHandler childHandler)
    {
        return parentHandler is IWinterContainerElementHandler container
            ? container.GetChildIndex(childHandler.ElementControl)
            : -1;
    }

    protected override void RemoveChildElement(
        IWinterElementHandler parentHandler,
        IWinterElementHandler childHandler)
    {
        if (childHandler is INonPhysicalChild nonPhysicalChild)
        {
            nonPhysicalChild.Remove();
        }
        else if (parentHandler is IWinterContainerElementHandler container)
        {
            container.RemoveChild(childHandler.ElementControl);
        }
    }

    protected override bool IsParentOfChild(
        IWinterElementHandler parentHandler,
        IWinterElementHandler childHandler)
    {
        return childHandler.IsParentedTo(parentHandler.ElementControl);
    }
}
