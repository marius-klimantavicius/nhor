// Ported from taffy/src/tree/traits.rs
// The abstractions that make up the core of Taffy's low-level API

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// Taffy's abstraction for downward tree traversal.
    /// Allows access to a single container node and its immediate children.
    /// </summary>
    public interface ITraversePartialTree
    {
        /// <summary>Get the list of children IDs for the given node</summary>
        IEnumerable<NodeId> ChildIds(NodeId parentNodeId);

        /// <summary>Get the number of children for the given node</summary>
        int ChildCount(NodeId parentNodeId);

        /// <summary>Get a specific child of a node, where the index represents the nth child</summary>
        NodeId GetChildId(NodeId parentNodeId, int childIndex);
    }

    /// <summary>
    /// A marker interface which extends ITraversePartialTree.
    /// Implementing this interface implies the additional guarantee that the child/children methods
    /// can be used to recurse infinitely down the tree.
    /// Required by IRoundTree and IPrintTree.
    /// </summary>
    public interface ITraverseTree : ITraversePartialTree
    {
    }

    /// <summary>
    /// Any type that implements ILayoutPartialTree can be laid out using Taffy's algorithms.
    /// Note that this interface extends ITraversePartialTree (not ITraverseTree).
    /// </summary>
    public interface ILayoutPartialTree : ITraversePartialTree
    {
        /// <summary>Get core container style for the node</summary>
        ICoreStyle GetCoreContainerStyle(NodeId nodeId);

        /// <summary>Set the node's unrounded layout</summary>
        void SetUnroundedLayout(NodeId nodeId, Layout layout);

        /// <summary>Compute the specified node's size or full layout given the specified constraints</summary>
        LayoutOutput ComputeChildLayout(NodeId nodeId, LayoutInput inputs);

        /// <summary>Resolve a calc value given the value pointer and basis</summary>
        float ResolveCalcValue(IntPtr val, float basis) => 0f;
    }

    /// <summary>
    /// Trait used by the compute_cached_layout method which allows cached layout results
    /// to be stored and retrieved.
    /// </summary>
    public interface ICacheTree
    {
        /// <summary>Try to retrieve a cached result from the cache</summary>
        LayoutOutput? CacheGet(
            NodeId nodeId,
            Size<float?> knownDimensions,
            Size<AvailableSpace> availableSpace,
            RunMode runMode);

        /// <summary>Store a computed size in the cache</summary>
        void CacheStore(
            NodeId nodeId,
            Size<float?> knownDimensions,
            Size<AvailableSpace> availableSpace,
            RunMode runMode,
            LayoutOutput layoutOutput);

        /// <summary>Clear all cache entries for the node</summary>
        void CacheClear(NodeId nodeId);
    }

    /// <summary>
    /// Trait used by the round_layout method which takes a tree of unrounded float-valued layouts
    /// and performs rounding to snap the values to the pixel grid.
    /// </summary>
    public interface IRoundTree : ITraverseTree
    {
        /// <summary>Get the node's unrounded layout</summary>
        Layout GetUnroundedLayout(NodeId nodeId);

        /// <summary>Set the node's final layout</summary>
        void SetFinalLayout(NodeId nodeId, Layout layout);
    }

    /// <summary>
    /// Trait used by the print_tree method which prints a debug representation.
    /// </summary>
    public interface IPrintTree : ITraverseTree
    {
        /// <summary>Get a debug label for the node</summary>
        string GetDebugLabel(NodeId nodeId);

        /// <summary>Get the node's final layout</summary>
        Layout GetFinalLayout(NodeId nodeId);
    }

    /// <summary>
    /// Extends ILayoutPartialTree with getters for the styles required for Flexbox layout.
    /// </summary>
    public interface ILayoutFlexboxContainer : ILayoutPartialTree
    {
        /// <summary>Get the container's flexbox styles</summary>
        IFlexboxContainerStyle GetFlexboxContainerStyle(NodeId nodeId);

        /// <summary>Get the child's flexbox item styles</summary>
        IFlexboxItemStyle GetFlexboxChildStyle(NodeId childNodeId);
    }

    /// <summary>
    /// Extends ILayoutPartialTree with getters for the styles required for CSS Grid layout.
    /// </summary>
    public interface ILayoutGridContainer : ILayoutPartialTree
    {
        /// <summary>Get the container's grid styles</summary>
        IGridContainerStyle GetGridContainerStyle(NodeId nodeId);

        /// <summary>Get the child's grid item styles</summary>
        IGridItemStyle GetGridChildStyle(NodeId childNodeId);
    }

    /// <summary>
    /// Extends ILayoutPartialTree with getters for the styles required for CSS Block layout.
    /// </summary>
    public interface ILayoutBlockContainer : ILayoutPartialTree
    {
        /// <summary>Get the container's block styles</summary>
        IBlockContainerStyle GetBlockContainerStyle(NodeId nodeId);

        /// <summary>Get the child's block item styles</summary>
        IBlockItemStyle GetBlockChildStyle(NodeId childNodeId);

        /// <summary>
        /// Compute a block child's layout, optionally passing a BlockContext for
        /// children that share the same Block Formatting Context.
        /// Default implementation delegates to ComputeChildLayout (ignoring the context).
        /// </summary>
        LayoutOutput ComputeBlockChildLayout(NodeId nodeId, LayoutInput inputs, BlockContext? blockCtx)
        {
            return ComputeChildLayout(nodeId, inputs);
        }
    }

    /// <summary>
    /// Extension methods for ILayoutPartialTree that provide convenience wrappers
    /// around ComputeChildLayout.
    /// Port of Rust's LayoutPartialTreeExt trait.
    /// </summary>
    public static class LayoutPartialTreeExt
    {
        /// <summary>Compute the size of the node given the specified constraints (single axis)</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MeasureChildSize(
            this ILayoutPartialTree tree,
            NodeId nodeId,
            Size<float?> knownDimensions,
            Size<float?> parentSize,
            Size<AvailableSpace> availableSpace,
            SizingMode sizingMode,
            AbsoluteAxis axis,
            Line<bool> verticalMarginsAreCollapsible)
        {
            return tree.ComputeChildLayout(
                nodeId,
                new LayoutInput
                {
                    KnownDimensions = knownDimensions,
                    ParentSize = parentSize,
                    AvailableSpace = availableSpace,
                    SizingMode = sizingMode,
                    Axis = (RequestedAxis)axis,
                    RunMode = RunMode.ComputeSize,
                    VerticalMarginsAreCollapsible = verticalMarginsAreCollapsible,
                }
            ).Size.GetAbs(axis);
        }

        /// <summary>Compute the size of the node given the specified constraints (both axes)</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float> MeasureChildSizeBoth(
            this ILayoutPartialTree tree,
            NodeId nodeId,
            Size<float?> knownDimensions,
            Size<float?> parentSize,
            Size<AvailableSpace> availableSpace,
            SizingMode sizingMode,
            Line<bool> verticalMarginsAreCollapsible)
        {
            return tree.ComputeChildLayout(
                nodeId,
                new LayoutInput
                {
                    KnownDimensions = knownDimensions,
                    ParentSize = parentSize,
                    AvailableSpace = availableSpace,
                    SizingMode = sizingMode,
                    Axis = RequestedAxis.Both,
                    RunMode = RunMode.ComputeSize,
                    VerticalMarginsAreCollapsible = verticalMarginsAreCollapsible,
                }
            ).Size;
        }

        /// <summary>Perform a full layout on the node given the specified constraints</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LayoutOutput PerformChildLayout(
            this ILayoutPartialTree tree,
            NodeId nodeId,
            Size<float?> knownDimensions,
            Size<float?> parentSize,
            Size<AvailableSpace> availableSpace,
            SizingMode sizingMode,
            Line<bool> verticalMarginsAreCollapsible)
        {
            return tree.ComputeChildLayout(
                nodeId,
                new LayoutInput
                {
                    KnownDimensions = knownDimensions,
                    ParentSize = parentSize,
                    AvailableSpace = availableSpace,
                    SizingMode = sizingMode,
                    Axis = RequestedAxis.Both,
                    RunMode = RunMode.PerformLayout,
                    VerticalMarginsAreCollapsible = verticalMarginsAreCollapsible,
                }
            );
        }

        /// <summary>Alias to ResolveCalcValue with a shorter function name</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Calc(this ILayoutPartialTree tree, IntPtr val, float basis)
        {
            return tree.ResolveCalcValue(val, basis);
        }
    }

    /// <summary>
    /// Extension methods for RequestedAxis conversions.
    /// </summary>
    public static class RequestedAxisExtensions
    {
        /// <summary>Convert from AbsoluteAxis to RequestedAxis</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RequestedAxis ToRequestedAxis(this AbsoluteAxis axis)
        {
            return axis switch
            {
                AbsoluteAxis.Horizontal => RequestedAxis.Horizontal,
                AbsoluteAxis.Vertical => RequestedAxis.Vertical,
                _ => throw new ArgumentOutOfRangeException(nameof(axis)),
            };
        }

        /// <summary>Try to convert RequestedAxis to AbsoluteAxis. Returns null for Both.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AbsoluteAxis? TryToAbsoluteAxis(this RequestedAxis axis)
        {
            return axis switch
            {
                RequestedAxis.Horizontal => AbsoluteAxis.Horizontal,
                RequestedAxis.Vertical => AbsoluteAxis.Vertical,
                RequestedAxis.Both => null,
                _ => throw new ArgumentOutOfRangeException(nameof(axis)),
            };
        }
    }
}
