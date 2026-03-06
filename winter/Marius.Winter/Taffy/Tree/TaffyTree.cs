// Ported from taffy/src/tree/taffy_tree.rs
// Contains TaffyTree: the default implementation of the layout tree, and the error type for Taffy.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// The error Taffy generates on invalid operations
    /// </summary>
    public class TaffyError : Exception
    {
        public TaffyError(string message) : base(message) { }
    }

    /// <summary>
    /// An error that occurs while trying to access or modify a node's children by index.
    /// </summary>
    public class TaffyChildIndexOutOfBoundsError : TaffyError
    {
        public NodeId Parent { get; }
        public int ChildIndex { get; }
        public int ChildCount { get; }

        public TaffyChildIndexOutOfBoundsError(NodeId parent, int childIndex, int childCount)
            : base($"Index (is {childIndex}) should be < child_count ({childCount}) for parent node {parent}")
        {
            Parent = parent;
            ChildIndex = childIndex;
            ChildCount = childCount;
        }
    }

    /// <summary>
    /// The parent node was not found in the TaffyTree instance.
    /// </summary>
    public class TaffyInvalidParentNodeError : TaffyError
    {
        public NodeId Node { get; }
        public TaffyInvalidParentNodeError(NodeId node) : base($"Parent Node {node} is not in the TaffyTree instance") { Node = node; }
    }

    /// <summary>
    /// The child node was not found in the TaffyTree instance.
    /// </summary>
    public class TaffyInvalidChildNodeError : TaffyError
    {
        public NodeId Node { get; }
        public TaffyInvalidChildNodeError(NodeId node) : base($"Child Node {node} is not in the TaffyTree instance") { Node = node; }
    }

    /// <summary>
    /// The supplied node was not found in the TaffyTree instance.
    /// </summary>
    public class TaffyInvalidInputNodeError : TaffyError
    {
        public NodeId Node { get; }
        public TaffyInvalidInputNodeError(NodeId node) : base($"Supplied Node {node} is not in the TaffyTree instance") { Node = node; }
    }

    /// <summary>
    /// Delegate type for measure functions that compute the intrinsic size of leaf nodes.
    /// </summary>
    /// <param name="knownDimensions">Already known dimensions of the node</param>
    /// <param name="availableSpace">Available space for the node</param>
    /// <param name="nodeId">The id of the node being measured</param>
    /// <param name="nodeContext">Optional context data associated with the node</param>
    /// <param name="style">The style of the node</param>
    /// <returns>The measured size of the node</returns>
    public delegate Size<float> MeasureFunction<NodeContext>(
        Size<float?> knownDimensions,
        Size<AvailableSpace> availableSpace,
        NodeId nodeId,
        NodeContext? nodeContext,
        Style style) where NodeContext : class;

    /// <summary>
    /// Delegate type for measure functions with value-type contexts.
    /// </summary>
    public delegate Size<float> MeasureFunctionVal<NodeContext>(
        Size<float?> knownDimensions,
        Size<AvailableSpace> availableSpace,
        NodeId nodeId,
        NodeContext? nodeContext,
        Style style) where NodeContext : struct;

    /// <summary>
    /// Global configuration values for a TaffyTree instance
    /// </summary>
    internal struct TaffyConfig
    {
        /// <summary>Whether to round layout values</summary>
        public bool UseRounding;

        public static TaffyConfig Default() => new TaffyConfig { UseRounding = true };
    }

    /// <summary>
    /// Layout information for a given Node. Stored in a TaffyTree.
    /// </summary>
    internal class NodeData
    {
        /// <summary>The layout strategy used by this node</summary>
        public Style Style;

        /// <summary>
        /// The always unrounded results of the layout computation. We must store this separately from the rounded
        /// layout to avoid errors from rounding already-rounded values.
        /// See https://github.com/DioxusLabs/taffy/issues/501
        /// </summary>
        public Layout UnroundedLayout;

        /// <summary>
        /// The final results of the layout computation.
        /// These may be rounded or unrounded depending on what the use_rounding config setting is set to.
        /// </summary>
        public Layout FinalLayout;

        /// <summary>Whether the node has context data associated with it or not</summary>
        public bool HasContext;

        /// <summary>The cached results of the layout computation</summary>
        public Cache Cache;

        /// <summary>Create the data for a new node</summary>
        public NodeData(Style style)
        {
            Style = style;
            Cache = Cache.New();
            UnroundedLayout = Layout.New();
            FinalLayout = Layout.New();
            HasContext = false;
        }

        /// <summary>
        /// Marks a node and all of its ancestors as requiring relayout.
        /// This clears any cached data and signals that the data must be recomputed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ClearState MarkDirty()
        {
            return Cache.Clear();
        }
    }

    /// <summary>
    /// An entire tree of UI nodes. The entry point to Taffy's high-level API.
    ///
    /// Allows you to build a tree of UI nodes, run Taffy's layout algorithms over that tree,
    /// and then access the resultant layout.
    /// </summary>
    public partial class TaffyTree<NodeContext>
    {
        // --- SlotMap-like storage using List + free list ---
        // Each node is stored in a list at an index. The NodeId wraps that index.
        // Removed nodes have their slot added to a free list for reuse.

        /// <summary>Shared empty list for leaf nodes to avoid per-leaf allocation</summary>
        private static readonly List<NodeId> s_emptyChildren = new();

        /// <summary>The NodeData for each node stored in this tree</summary>
        private readonly List<NodeData?> _nodes;

        /// <summary>The context data for nodes that have it</summary>
        private readonly Dictionary<int, NodeContext> _nodeContextData;

        /// <summary>The children of each node</summary>
        private readonly List<List<NodeId>?> _children;

        /// <summary>The parents of each node</summary>
        private readonly List<NodeId?> _parents;

        /// <summary>Free list of slot indices that have been removed</summary>
        private readonly List<int> _freeList;

        /// <summary>Layout mode configuration</summary>
        private TaffyConfig _config;

        /// <summary>
        /// Creates a new TaffyTree.
        /// The default capacity of a TaffyTree is 16 nodes.
        /// </summary>
        public TaffyTree()
            : this(16)
        {
        }

        /// <summary>
        /// Creates a new TaffyTree that can store capacity nodes before reallocation
        /// </summary>
        public TaffyTree(int capacity)
        {
            _nodes = new List<NodeData?>(capacity);
            _children = new List<List<NodeId>?>(capacity);
            _parents = new List<NodeId?>(capacity);
            _nodeContextData = new Dictionary<int, NodeContext>(capacity);
            _freeList = new List<int>();
            _config = TaffyConfig.Default();
        }

        /// <summary>Enable rounding of layout values. Rounding is enabled by default.</summary>
        public void EnableRounding()
        {
            _config.UseRounding = true;
        }

        /// <summary>Disable rounding of layout values. Rounding is enabled by default.</summary>
        public void DisableRounding()
        {
            _config.UseRounding = false;
        }

        // --- Internal helpers ---

        /// <summary>Allocate a new slot and return its index. Uses shared empty children list for leaves.</summary>
        private int AllocateSlot(NodeData data, bool isLeaf = true)
        {
            int index;
            var childList = isLeaf ? s_emptyChildren : new List<NodeId>();
            if (_freeList.Count > 0)
            {
                index = _freeList[_freeList.Count - 1];
                _freeList.RemoveAt(_freeList.Count - 1);
                _nodes[index] = data;
                _children[index] = childList;
                _parents[index] = null;
            }
            else
            {
                index = _nodes.Count;
                _nodes.Add(data);
                _children.Add(childList);
                _parents.Add(null);
            }
            return index;
        }

        /// <summary>Get the internal index for a NodeId</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Key(NodeId nodeId) => (int)nodeId.Value;

        /// <summary>Ensure the children list for a node is mutable (not the shared empty list)</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private List<NodeId> EnsureMutableChildren(int key)
        {
            var list = _children[key]!;
            if (ReferenceEquals(list, s_emptyChildren))
            {
                list = new List<NodeId>();
                _children[key] = list;
            }
            return list;
        }

        /// <summary>Check that a node id is valid</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsValidNode(NodeId nodeId)
        {
            var key = Key(nodeId);
            return key >= 0 && key < _nodes.Count && _nodes[key] != null;
        }

        // --- Public API ---

        /// <summary>
        /// Creates and adds a new unattached leaf node to the tree, and returns the node id of the new node
        /// </summary>
        public NodeId NewLeaf(Style layout)
        {
            int index = AllocateSlot(new NodeData(layout));
            return new NodeId((ulong)index);
        }

        /// <summary>
        /// Creates and adds a new leaf node with a supplied context
        /// </summary>
        public NodeId NewLeafWithContext(Style layout, NodeContext context)
        {
            var data = new NodeData(layout);
            data.HasContext = true;

            int index = AllocateSlot(data);
            _nodeContextData[index] = context;

            return new NodeId((ulong)index);
        }

        /// <summary>
        /// Creates and adds a new node, which may have any number of children
        /// </summary>
        public NodeId NewWithChildren(Style layout, ReadOnlySpan<NodeId> children)
        {
            var data = new NodeData(layout);
            int index = AllocateSlot(data, isLeaf: false);
            var id = new NodeId((ulong)index);

            for (int i = 0; i < children.Length; i++)
            {
                _parents[Key(children[i])] = id;
            }

            var childList = _children[index]!;
            for (int i = 0; i < children.Length; i++)
            {
                childList.Add(children[i]);
            }

            return id;
        }

        /// <summary>
        /// Drops all nodes in the tree
        /// </summary>
        public void Clear()
        {
            _nodes.Clear();
            _children.Clear();
            _parents.Clear();
            _nodeContextData.Clear();
            _freeList.Clear();
        }

        /// <summary>
        /// Remove a specific node from the tree and drop it.
        /// Returns the id of the node removed.
        /// </summary>
        public NodeId Remove(NodeId node)
        {
            var key = Key(node);

            // Remove from parent's children list
            if (_parents[key].HasValue)
            {
                var parentKey = Key(_parents[key]!.Value);
                _children[parentKey]?.RemoveAll(id => id == node);
            }

            // Remove "parent" references when removing a node
            if (_children[key] != null)
            {
                foreach (var child in _children[key]!)
                {
                    _parents[Key(child)] = null;
                }
            }

            _children[key] = null;
            _parents[key] = null;
            _nodes[key] = null;
            _nodeContextData.Remove(key);
            _freeList.Add(key);

            return node;
        }

        /// <summary>
        /// Sets the context data associated with the node
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetNodeContext(NodeId node, NodeContext? context)
        {
            var key = Key(node);
            if (context != null)
            {
                _nodes[key]!.HasContext = true;
                _nodeContextData[key] = context;
            }
            else
            {
                _nodes[key]!.HasContext = false;
                _nodeContextData.Remove(key);
            }

            MarkDirty(node);
        }

        /// <summary>
        /// Gets a reference to the context data associated with the node
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NodeContext? GetNodeContext(NodeId node)
        {
            _nodeContextData.TryGetValue(Key(node), out var context);
            return context;
        }

        /// <summary>
        /// Adds a child node under the supplied parent
        /// </summary>
        public void AddChild(NodeId parent, NodeId child)
        {
            var parentKey = Key(parent);
            var childKey = Key(child);
            _parents[childKey] = parent;
            EnsureMutableChildren(parentKey).Add(child);
            MarkDirty(parent);
        }

        /// <summary>
        /// Inserts a child node at the given child_index under the supplied parent,
        /// shifting all children after it to the right.
        /// </summary>
        public void InsertChildAtIndex(NodeId parent, int childIndex, NodeId child)
        {
            var parentKey = Key(parent);
            var childCount = _children[parentKey]!.Count;
            if (childIndex > childCount)
            {
                throw new TaffyChildIndexOutOfBoundsError(parent, childIndex, childCount);
            }

            _parents[Key(child)] = parent;
            EnsureMutableChildren(parentKey).Insert(childIndex, child);
            MarkDirty(parent);
        }

        /// <summary>
        /// Directly sets the children of the supplied parent
        /// </summary>
        public void SetChildren(NodeId parent, ReadOnlySpan<NodeId> children)
        {
            var parentKey = Key(parent);

            // Remove node as parent from all its current children.
            foreach (var existingChild in _children[parentKey]!)
            {
                _parents[Key(existingChild)] = null;
            }

            // Build up relation node <-> child
            for (int i = 0; i < children.Length; i++)
            {
                var child = children[i];
                // Remove child from previous parent
                if (_parents[Key(child)].HasValue)
                {
                    var previousParent = _parents[Key(child)]!.Value;
                    RemoveChild(previousParent, child);
                }
                _parents[Key(child)] = parent;
            }

            var parentChildren = EnsureMutableChildren(parentKey);
            parentChildren.Clear();
            for (int i = 0; i < children.Length; i++)
            {
                parentChildren.Add(children[i]);
            }

            MarkDirty(parent);
        }

        /// <summary>
        /// Removes the child of the parent node.
        /// The child is not removed from the tree entirely, it is simply no longer attached to its previous parent.
        /// </summary>
        public NodeId RemoveChild(NodeId parent, NodeId child)
        {
            var parentKey = Key(parent);
            var index = _children[parentKey]!.IndexOf(child);
            if (index < 0)
            {
                throw new TaffyInvalidChildNodeError(child);
            }
            return RemoveChildAtIndex(parent, index);
        }

        /// <summary>
        /// Removes the child at the given index from the parent.
        /// The child is not removed from the tree entirely, it is simply no longer attached to its previous parent.
        /// </summary>
        public NodeId RemoveChildAtIndex(NodeId parent, int childIndex)
        {
            var parentKey = Key(parent);
            var childCount = _children[parentKey]!.Count;
            if (childIndex >= childCount)
            {
                throw new TaffyChildIndexOutOfBoundsError(parent, childIndex, childCount);
            }

            var child = _children[parentKey]![childIndex];
            _children[parentKey]!.RemoveAt(childIndex);
            _parents[Key(child)] = null;

            MarkDirty(parent);

            return child;
        }

        /// <summary>
        /// Replaces the child at the given child_index from the parent node with the new child node.
        /// The old child is not removed from the tree entirely, it is simply no longer attached to its previous parent.
        /// </summary>
        public NodeId ReplaceChildAtIndex(NodeId parent, int childIndex, NodeId newChild)
        {
            var parentKey = Key(parent);
            var childCount = _children[parentKey]!.Count;
            if (childIndex >= childCount)
            {
                throw new TaffyChildIndexOutOfBoundsError(parent, childIndex, childCount);
            }

            _parents[Key(newChild)] = parent;
            var oldChild = _children[parentKey]![childIndex];
            _children[parentKey]![childIndex] = newChild;
            _parents[Key(oldChild)] = null;

            MarkDirty(parent);

            return oldChild;
        }

        /// <summary>
        /// Returns the child node of the parent node at the provided child_index
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NodeId ChildAtIndex(NodeId parent, int childIndex)
        {
            var parentKey = Key(parent);
            var childCount = _children[parentKey]!.Count;
            if (childIndex >= childCount)
            {
                throw new TaffyChildIndexOutOfBoundsError(parent, childIndex, childCount);
            }

            return _children[parentKey]![childIndex];
        }

        /// <summary>
        /// Returns the total number of nodes in the tree
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int TotalNodeCount()
        {
            return _nodes.Count - _freeList.Count;
        }

        /// <summary>
        /// Returns the NodeId of the parent node of the specified node (if it exists).
        /// Returns null if the specified node has no parent.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NodeId? Parent(NodeId childId)
        {
            return _parents[Key(childId)];
        }

        /// <summary>
        /// Returns a list of children that belong to the parent node
        /// </summary>
        public List<NodeId> Children(NodeId parent)
        {
            return new List<NodeId>(_children[Key(parent)]!);
        }

        /// <summary>
        /// Returns the number of children for the given node
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ChildCount(NodeId parent)
        {
            return _children[Key(parent)]!.Count;
        }

        /// <summary>
        /// Sets the Style of the provided node
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetStyle(NodeId node, Style style)
        {
            _nodes[Key(node)]!.Style = style;
            MarkDirty(node);
        }

        /// <summary>
        /// Gets the Style of the provided node
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Style GetStyle(NodeId node)
        {
            return _nodes[Key(node)]!.Style;
        }

        /// <summary>
        /// Return this node layout relative to its parent
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Layout GetLayout(NodeId node)
        {
            if (_config.UseRounding)
            {
                return _nodes[Key(node)]!.FinalLayout;
            }
            else
            {
                return _nodes[Key(node)]!.UnroundedLayout;
            }
        }

        /// <summary>
        /// Returns this node layout with unrounded values relative to its parent.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Layout GetUnroundedLayout(NodeId node)
        {
            return _nodes[Key(node)]!.UnroundedLayout;
        }

        /// <summary>
        /// Marks the layout of this node and its ancestors as outdated
        /// </summary>
        public void MarkDirty(NodeId node)
        {
            MarkDirtyRecursive(Key(node));
        }

        private void MarkDirtyRecursive(int nodeKey)
        {
            var result = _nodes[nodeKey]!.MarkDirty();
            switch (result)
            {
                case ClearState.AlreadyEmpty:
                    // Node was already marked as dirty.
                    // No need to visit ancestors as they should be marked as dirty already.
                    break;
                case ClearState.Cleared:
                    if (_parents[nodeKey].HasValue)
                    {
                        MarkDirtyRecursive(Key(_parents[nodeKey]!.Value));
                    }
                    break;
            }
        }

        /// <summary>
        /// Indicates whether the layout of this node needs to be recomputed
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Dirty(NodeId node)
        {
            return _nodes[Key(node)]!.Cache.IsEmpty();
        }

        /// <summary>
        /// Updates the stored layout of the provided node and its children
        /// using the supplied measure function.
        /// </summary>
        public void ComputeLayoutWithMeasure(
            NodeId nodeId,
            Size<AvailableSpace> availableSpace,
            Func<Size<float?>, Size<AvailableSpace>, NodeId, NodeContext?, Style, Size<float>> measureFunction)
        {
            var useRounding = _config.UseRounding;
            var taffyView = new TaffyView<NodeContext>(this, measureFunction);
            Marius.Winter.Taffy.ComputeLayout.ComputeRootLayout(taffyView, nodeId, availableSpace);
            if (useRounding)
            {
                Marius.Winter.Taffy.ComputeLayout.RoundLayout(taffyView, nodeId);
            }
        }

        /// <summary>
        /// Updates the stored layout of the provided node and its children
        /// </summary>
        public void ComputeLayout(NodeId node, Size<AvailableSpace> availableSpace)
        {
            ComputeLayoutWithMeasure(node, availableSpace, (_, _, _, _, _) => SizeExtensions.ZeroF32);
        }

        /// <summary>
        /// Returns an instance of the layout tree view for internal use
        /// </summary>
        internal TaffyView<NodeContext> AsLayoutTree()
        {
            return new TaffyView<NodeContext>(this, (_, _, _, _, _) => SizeExtensions.ZeroF32);
        }
    }

    /// <summary>
    /// View over the Taffy tree that holds the tree itself along with a reference to the measure function
    /// and implements the layout tree interfaces. This allows the context to be stored outside of the TaffyTree struct
    /// which makes the lifetimes of the context much more flexible.
    /// </summary>
    internal class TaffyView<NodeContext> :
        ITraversePartialTree, ITraverseTree,
        ILayoutPartialTree,
        ICacheTree,
        IRoundTree,
        IPrintTree,
        ILayoutFlexboxContainer,
        ILayoutGridContainer,
        ILayoutBlockContainer
    {
        /// <summary>A reference to the TaffyTree</summary>
        private readonly TaffyTree<NodeContext> _taffy;

        /// <summary>The measure function for leaf nodes</summary>
        private readonly Func<Size<float?>, Size<AvailableSpace>, NodeId, NodeContext?, Style, Size<float>> _measureFunction;

        /// <summary>
        /// Temporary storage for the block context being passed through ComputeBlockChildLayout.
        /// This mirrors Rust's approach where the block_ctx parameter is threaded through compute_child_layout.
        /// </summary>
        private BlockContext? _pendingBlockCtx;

        public TaffyView(
            TaffyTree<NodeContext> taffy,
            Func<Size<float?>, Size<AvailableSpace>, NodeId, NodeContext?, Style, Size<float>> measureFunction)
        {
            _taffy = taffy;
            _measureFunction = measureFunction;
        }

        // --- Internal helpers to access TaffyTree internals ---
        // We use a naming convention to access private members via internal access

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Key(NodeId nodeId) => (int)nodeId.Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private NodeData GetNode(NodeId nodeId) => _taffy.GetNodeData(nodeId);

        // --- ITraversePartialTree ---

        public IEnumerable<NodeId> ChildIds(NodeId parentNodeId)
        {
            return _taffy.ChildrenList(parentNodeId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ChildCount(NodeId parentNodeId)
        {
            return _taffy.ChildCount(parentNodeId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NodeId GetChildId(NodeId parentNodeId, int childIndex)
        {
            return _taffy.ChildrenList(parentNodeId)[childIndex];
        }

        // --- ILayoutPartialTree ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ICoreStyle GetCoreContainerStyle(NodeId nodeId)
        {
            return GetNode(nodeId).Style;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUnroundedLayout(NodeId nodeId, Layout layout)
        {
            GetNode(nodeId).UnroundedLayout = layout;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ResolveCalcValue(IntPtr val, float basis)
        {
            return 0.0f;
        }

        public LayoutOutput ComputeChildLayout(NodeId nodeId, LayoutInput inputs)
        {
            return ComputeChildLayoutImpl(nodeId, inputs, null);
        }

        public LayoutOutput ComputeBlockChildLayout(NodeId nodeId, LayoutInput inputs, BlockContext? blockCtx)
        {
            return ComputeChildLayoutImpl(nodeId, inputs, blockCtx);
        }

        /// <summary>
        /// Unified implementation that both LayoutPartialTree.ComputeChildLayout
        /// and LayoutBlockContainer.ComputeBlockChildLayout delegate to.
        /// </summary>
        private LayoutOutput ComputeChildLayoutImpl(NodeId nodeId, LayoutInput inputs, BlockContext? blockCtx)
        {
            // If RunMode is PerformHiddenLayout then this indicates that an ancestor node is Display.None
            // and thus that we should lay out this node using hidden layout regardless of its own display style.
            if (inputs.RunMode == RunMode.PerformHiddenLayout)
            {
                return Marius.Winter.Taffy.ComputeLayout.ComputeHiddenLayout(this, this, nodeId);
            }

            // Store the block context so it can be accessed in the cached layout closure.
            // This mirrors Rust's approach of threading block_ctx through compute_child_layout.
            var savedBlockCtx = _pendingBlockCtx;
            _pendingBlockCtx = blockCtx;

            // We run the following wrapped in "compute_cached_layout", which will check the cache for an entry
            // matching the node and inputs and:
            //   - Return that entry if exists
            //   - Else call the passed closure (below) to compute the result
            //
            // If there was no cache match and a new result needs to be computed then that result will be added to the cache
            var result = Marius.Winter.Taffy.ComputeLayout.ComputeCachedLayout(this, nodeId, inputs, (tree, nid, inp) =>
            {
                var displayMode = tree.GetNode(nid).Style.Display;
                var hasChildren = tree.ChildCount(nid) > 0;

                // Dispatch to a layout algorithm based on the node's display style and whether the node has children or not.
                return (displayMode, hasChildren) switch
                {
                    (Display.None, _) => Marius.Winter.Taffy.ComputeLayout.ComputeHiddenLayout(tree, tree, nid),
                    (Display.Block, true) => BlockAlgorithm.ComputeBlockLayout(tree, nid, inp, tree._pendingBlockCtx),
                    (Display.Flex, true) => FlexboxAlgorithm.ComputeFlexboxLayout(tree, nid, inp),
                    (Display.Grid, true) => GridAlgorithm.ComputeGridLayout(tree, nid, inp),
                    (_, false) => ComputeLeafNode(tree, nid, inp),
                    _ => ComputeLeafNode(tree, nid, inp),
                };
            });

            _pendingBlockCtx = savedBlockCtx;
            return result;
        }

        /// <summary>
        /// Compute the layout for a leaf node (no children) using the measure function
        /// </summary>
        private static LayoutOutput ComputeLeafNode(TaffyView<NodeContext> tree, NodeId nodeId, LayoutInput inputs)
        {
            var node = tree.GetNode(nodeId);
            var style = node.Style;
            var hasContext = node.HasContext;
            var nodeContext = hasContext ? tree._taffy.GetNodeContext(nodeId) : default;
            Size<float> MeasureFunc(Size<float?> knownDimensions, Size<AvailableSpace> availableSpace)
            {
                return tree._measureFunction(knownDimensions, availableSpace, nodeId, nodeContext, style);
            }
            return LeafCompute.ComputeLeafLayout(inputs, style, (_, _) => 0.0f, MeasureFunc);
        }

        // --- ICacheTree ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LayoutOutput? CacheGet(NodeId nodeId, Size<float?> knownDimensions, Size<AvailableSpace> availableSpace, RunMode runMode)
        {
            return GetNode(nodeId).Cache.Get(knownDimensions, availableSpace, runMode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CacheStore(NodeId nodeId, Size<float?> knownDimensions, Size<AvailableSpace> availableSpace, RunMode runMode, LayoutOutput layoutOutput)
        {
            GetNode(nodeId).Cache.Store(knownDimensions, availableSpace, runMode, layoutOutput);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CacheClear(NodeId nodeId)
        {
            GetNode(nodeId).Cache.Clear();
        }

        // --- IRoundTree ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Layout GetUnroundedLayout(NodeId nodeId)
        {
            return GetNode(nodeId).UnroundedLayout;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFinalLayout(NodeId nodeId, Layout layout)
        {
            GetNode(nodeId).FinalLayout = layout;
        }

        // --- IPrintTree ---

        public string GetDebugLabel(NodeId nodeId)
        {
            var node = GetNode(nodeId);
            var display = node.Style.Display;
            var numChildren = ChildCount(nodeId);

            return (numChildren, display) switch
            {
                (_, Display.None) => "NONE",
                (0, _) => "LEAF",
                (_, Display.Block) => "BLOCK",
                (_, Display.Flex) => node.Style.FlexDirectionValue switch
                {
                    FlexDirection.Row or FlexDirection.RowReverse => "FLEX ROW",
                    FlexDirection.Column or FlexDirection.ColumnReverse => "FLEX COL",
                    _ => "FLEX",
                },
                (_, Display.Grid) => "GRID",
                _ => "UNKNOWN",
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Layout GetFinalLayout(NodeId nodeId)
        {
            if (_taffy.UseRounding)
            {
                return GetNode(nodeId).FinalLayout;
            }
            else
            {
                return GetNode(nodeId).UnroundedLayout;
            }
        }

        // --- ILayoutFlexboxContainer ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IFlexboxContainerStyle GetFlexboxContainerStyle(NodeId nodeId)
        {
            return GetNode(nodeId).Style;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IFlexboxItemStyle GetFlexboxChildStyle(NodeId childNodeId)
        {
            return GetNode(childNodeId).Style;
        }

        // --- ILayoutGridContainer ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IGridContainerStyle GetGridContainerStyle(NodeId nodeId)
        {
            return GetNode(nodeId).Style;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IGridItemStyle GetGridChildStyle(NodeId childNodeId)
        {
            return GetNode(childNodeId).Style;
        }

        // --- ILayoutBlockContainer ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IBlockContainerStyle GetBlockContainerStyle(NodeId nodeId)
        {
            return GetNode(nodeId).Style;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IBlockItemStyle GetBlockChildStyle(NodeId childNodeId)
        {
            return GetNode(childNodeId).Style;
        }
    }

    // Extension methods on TaffyTree<NodeContext> to expose internals to TaffyView
    // without making them part of the public API
    public partial class TaffyTree<NodeContext>
    {
        /// <summary>Get node data - internal access for TaffyView</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal NodeData GetNodeData(NodeId nodeId)
        {
            return _nodes[(int)nodeId.Value]!;
        }

        /// <summary>Get children list - internal access for TaffyView</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal List<NodeId> ChildrenList(NodeId parentNodeId)
        {
            return _children[(int)parentNodeId.Value]!;
        }

        /// <summary>Whether rounding is enabled - internal access for TaffyView</summary>
        internal bool UseRounding => _config.UseRounding;
    }

    /// <summary>
    /// Extension to add ZeroNullableF32 to SizeExtensions
    /// </summary>
    public static class SizeExtensionsExtra
    {
        /// <summary>Size with both dimensions zero (nullable)</summary>
        public static readonly Size<float?> ZeroNullableF32 = new Size<float?>(0f, 0f);
    }
}
