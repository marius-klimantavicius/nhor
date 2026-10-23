namespace Marius.Winter.Blazor.Core;

/// <summary>Commits dependent attributes after the complete native render batch.</summary>
public interface IHandleAttributeBatch
{
    void CommitAttributes();
}
