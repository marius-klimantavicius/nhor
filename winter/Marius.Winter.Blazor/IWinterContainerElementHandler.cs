using System.Collections.Generic;

namespace Marius.Winter.Blazor;

public interface IWinterContainerElementHandler : IWinterElementHandler
{
    void AddChild(Element child, int physicalSiblingIndex);
    void RemoveChild(Element child);
    int GetChildIndex(Element child);
    void ReorderChildren(List<Element> newOrder);
}
