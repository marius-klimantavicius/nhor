namespace Marius.Winter.Blazor.Core;

/// <summary>A metadata child with a stable native item in an ordered collection.</summary>
public interface IIndexedNonPhysicalChild : INonPhysicalChild
{
    void MoveTo(int index);
}
