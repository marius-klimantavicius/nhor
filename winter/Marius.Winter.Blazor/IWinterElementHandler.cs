using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor;

public interface IWinterElementHandler : IElementHandler
{
    Element ElementControl { get; }
    bool IsParented();
    bool IsParentedTo(Element parent);
    void SetParent(Element parent);
}
