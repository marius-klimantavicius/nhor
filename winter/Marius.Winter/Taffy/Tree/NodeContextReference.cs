// Managed equivalent of Option<&mut NodeContext> for measurement callbacks and tree access.
using System;

namespace Marius.Winter.Taffy;

public readonly struct NodeContextReference<T>
{
    private readonly TaffyTree<T>? _tree;
    private readonly NodeId _node;
    internal NodeContextReference(TaffyTree<T> tree, NodeId node) { _tree = tree; _node = node; }
    public bool HasValue => _tree != null && _tree.TryGetNodeContext(_node, out _);
    /// <summary>Borrow the stored context. Do not change tree topology or context storage while using this reference.</summary>
    public ref T Value
    {
        get
        {
            if (_tree == null) throw new InvalidOperationException("The node has no context");
            return ref _tree.ContextReference(_node);
        }
    }
}
