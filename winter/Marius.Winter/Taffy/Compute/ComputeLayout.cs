// Ported from taffy/src/compute/mod.rs
// Low-level access to the layout algorithms themselves.

using System;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// Top-level layout computation functions.
    /// </summary>
    public static class ComputeLayout
    {
        /// <summary>
        /// Compute layout for the root node in the tree
        /// </summary>
        public static void ComputeRootLayout(ILayoutPartialTree tree, NodeId root, Size<AvailableSpace> availableSpace)
        {
            var knownDimensions = SizeExtensions.NoneF32;

            // Block layout root sizing
            {
                var parentSize = new Size<float?>(availableSpace.Width.IntoOption(), availableSpace.Height.IntoOption());
                var style = tree.GetCoreContainerStyle(root);

                if (style.IsBlock())
                {
                    // Pull these out earlier to avoid borrowing issues
                    var aspectRatio = style.AspectRatio();
                    var margin = style.Margin().ResolveOrZero(parentSize.Width, (val, basis) => tree.Calc(val, basis));
                    var padding = style.Padding().ResolveOrZero(parentSize.Width, (val, basis) => tree.Calc(val, basis));
                    var border = style.Border().ResolveOrZero(parentSize.Width, (val, basis) => tree.Calc(val, basis));
                    var paddingBorderSize = padding.Add(border).SumAxes();
                    var boxSizingAdjustment =
                        style.BoxSizing() == BoxSizing.ContentBox
                            ? new Size<float?>(paddingBorderSize.Width, paddingBorderSize.Height)
                            : SizeExtensionsExtra.ZeroNullableF32;

                    var minSize = style
                        .MinSize()
                        .MaybeResolve(parentSize, (val, basis) => tree.Calc(val, basis))
                        .MaybeApplyAspectRatio(aspectRatio)
                        .MaybeAdd(boxSizingAdjustment);
                    var maxSize = style
                        .MaxSize()
                        .MaybeResolve(parentSize, (val, basis) => tree.Calc(val, basis))
                        .MaybeApplyAspectRatio(aspectRatio)
                        .MaybeAdd(boxSizingAdjustment);
                    var clampedStyleSize = style
                        .Size()
                        .MaybeResolve(parentSize, (val, basis) => tree.Calc(val, basis))
                        .MaybeApplyAspectRatio(aspectRatio)
                        .MaybeAdd(boxSizingAdjustment)
                        .MaybeClamp(minSize, maxSize);

                    // If both min and max in a given axis are set and max <= min then this determines the size in that axis
                    var minMaxDefiniteSize = minSize.ZipMap(maxSize, (min, max) =>
                        (min.HasValue && max.HasValue && max.Value <= min.Value) ? min : null);

                    // Block nodes automatically stretch fit their width to fit available space if available space is definite
                    var availableSpaceBasedSize = new Size<float?>
                    {
                        Width = availableSpace.Width.IntoOption().MaybeSub(margin.HorizontalAxisSum()),
                        Height = null,
                    };

                    var styledBasedKnownDimensions = knownDimensions
                        .Or(minMaxDefiniteSize)
                        .Or(clampedStyleSize)
                        .Or(availableSpaceBasedSize)
                        .MaybeMax(new Size<float?>(paddingBorderSize.Width, paddingBorderSize.Height));

                    knownDimensions = styledBasedKnownDimensions;
                }
            }

            // Recursively compute node layout
            var parentSizeForLayout = new Size<float?>(availableSpace.Width.IntoOption(), availableSpace.Height.IntoOption());
            var output = tree.PerformChildLayout(
                root,
                knownDimensions,
                parentSizeForLayout,
                availableSpace,
                SizingMode.InherentSize,
                LineExtensions.FalseLine);

            var styleForLayout = tree.GetCoreContainerStyle(root);
            var paddingFinal = styleForLayout.Padding().ResolveOrZero(availableSpace.Width.IntoOption(), (val, basis) => tree.Calc(val, basis));
            var borderFinal = styleForLayout.Border().ResolveOrZero(availableSpace.Width.IntoOption(), (val, basis) => tree.Calc(val, basis));
            var marginFinal = styleForLayout.Margin().ResolveOrZero(availableSpace.Width.IntoOption(), (val, basis) => tree.Calc(val, basis));
            var scrollbarSize = new Size<float>
            {
                Width = styleForLayout.Overflow().Y == Overflow.Scroll ? styleForLayout.ScrollbarWidth() : 0.0f,
                Height = styleForLayout.Overflow().X == Overflow.Scroll ? styleForLayout.ScrollbarWidth() : 0.0f,
            };

            tree.SetUnroundedLayout(
                root,
                new Layout
                {
                    Order = 0,
                    Location = PointExtensions.ZeroF32,
                    Size = output.Size,
                    ContentSize = output.ContentSize,
                    ScrollbarSize = scrollbarSize,
                    Padding = paddingFinal,
                    Border = borderFinal,
                    // TODO: support auto margins for root node?
                    Margin = marginFinal,
                });
        }

        /// <summary>
        /// Attempts to find a cached layout for the specified node and layout inputs.
        /// Uses the provided closure to compute the layout (and then stores the result in the cache) if no cached layout is found.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LayoutOutput ComputeCachedLayout<TTree>(
            TTree tree,
            NodeId node,
            LayoutInput inputs,
            Func<TTree, NodeId, LayoutInput, LayoutOutput> computeUncached)
            where TTree : ICacheTree
        {
            var knownDimensions = inputs.KnownDimensions;
            var availableSpace = inputs.AvailableSpace;
            var runMode = inputs.RunMode;

            // First we check if we have a cached result for the given input
            var cacheEntry = tree.CacheGet(node, knownDimensions, availableSpace, runMode);
            if (cacheEntry.HasValue)
            {
                return cacheEntry.Value;
            }

            var computedSizeAndBaselines = computeUncached(tree, node, inputs);

            // Cache result
            tree.CacheStore(node, knownDimensions, availableSpace, runMode, computedSizeAndBaselines);

            return computedSizeAndBaselines;
        }

        /// <summary>
        /// Rounds the calculated layout to exact pixel values.
        ///
        /// In order to ensure that no gaps in the layout are introduced we:
        ///   - Always round based on the cumulative x/y coordinates (relative to the viewport) rather than
        ///     parent-relative coordinates
        ///   - Compute width/height by first rounding the top/bottom/left/right and then computing the difference
        ///     rather than rounding the width/height directly
        ///
        /// See https://github.com/facebook/yoga/commit/aa5b296ac78f7a22e1aeaf4891243c6bb76488e2 for more context
        ///
        /// In order to prevent inaccuracies caused by rounding already-rounded values, we read from unrounded_layout
        /// and write to final_layout.
        /// </summary>
        public static void RoundLayout(IRoundTree tree, NodeId nodeId)
        {
            RoundLayoutInner(tree, nodeId, 0.0f, 0.0f);
        }

        /// <summary>
        /// Recursive function to apply rounding to all descendants
        /// </summary>
        private static void RoundLayoutInner(IRoundTree tree, NodeId nodeId, float cumulativeX, float cumulativeY)
        {
            var unroundedLayout = tree.GetUnroundedLayout(nodeId);
            var layout = unroundedLayout;

            var cumX = cumulativeX + unroundedLayout.Location.X;
            var cumY = cumulativeY + unroundedLayout.Location.Y;

            layout.Location = new Point<float>(
                TaffySys.Round(unroundedLayout.Location.X),
                TaffySys.Round(unroundedLayout.Location.Y));
            layout.Size = new Size<float>(
                TaffySys.Round(cumX + unroundedLayout.Size.Width) - TaffySys.Round(cumX),
                TaffySys.Round(cumY + unroundedLayout.Size.Height) - TaffySys.Round(cumY));
            layout.ScrollbarSize = new Size<float>(
                TaffySys.Round(unroundedLayout.ScrollbarSize.Width),
                TaffySys.Round(unroundedLayout.ScrollbarSize.Height));
            layout.Border = new Rect<float>(
                TaffySys.Round(cumX + unroundedLayout.Border.Left) - TaffySys.Round(cumX),
                TaffySys.Round(cumX + unroundedLayout.Size.Width)
                    - TaffySys.Round(cumX + unroundedLayout.Size.Width - unroundedLayout.Border.Right),
                TaffySys.Round(cumY + unroundedLayout.Border.Top) - TaffySys.Round(cumY),
                TaffySys.Round(cumY + unroundedLayout.Size.Height)
                    - TaffySys.Round(cumY + unroundedLayout.Size.Height - unroundedLayout.Border.Bottom));
            layout.Padding = new Rect<float>(
                TaffySys.Round(cumX + unroundedLayout.Padding.Left) - TaffySys.Round(cumX),
                TaffySys.Round(cumX + unroundedLayout.Size.Width)
                    - TaffySys.Round(cumX + unroundedLayout.Size.Width - unroundedLayout.Padding.Right),
                TaffySys.Round(cumY + unroundedLayout.Padding.Top) - TaffySys.Round(cumY),
                TaffySys.Round(cumY + unroundedLayout.Size.Height)
                    - TaffySys.Round(cumY + unroundedLayout.Size.Height - unroundedLayout.Padding.Bottom));

            // Round content size
            layout.ContentSize = new Size<float>(
                TaffySys.Round(cumX + unroundedLayout.ContentSize.Width) - TaffySys.Round(cumX),
                TaffySys.Round(cumY + unroundedLayout.ContentSize.Height) - TaffySys.Round(cumY));

            tree.SetFinalLayout(nodeId, layout);

            var childCount = tree.ChildCount(nodeId);
            for (int index = 0; index < childCount; index++)
            {
                var child = tree.GetChildId(nodeId, index);
                RoundLayoutInner(tree, child, cumX, cumY);
            }
        }

        /// <summary>
        /// Creates a layout for this node and its children, recursively.
        /// Each hidden node has zero size and is placed at the origin.
        /// </summary>
        public static LayoutOutput ComputeHiddenLayout(ILayoutPartialTree tree, ICacheTree cacheTree, NodeId node)
        {
            // Clear cache and set zeroed-out layout for the node
            cacheTree.CacheClear(node);
            tree.SetUnroundedLayout(node, Layout.WithOrder(0));

            // Perform hidden layout on all children
            for (int index = 0; index < tree.ChildCount(node); index++)
            {
                var childId = tree.GetChildId(node, index);
                tree.ComputeChildLayout(childId, LayoutInput.HIDDEN);
            }

            return LayoutOutput.HIDDEN;
        }

        /// <summary>
        /// Overload for trees that implement both ILayoutPartialTree and ICacheTree
        /// </summary>
        public static LayoutOutput ComputeHiddenLayout<TTree>(TTree tree, NodeId node)
            where TTree : ILayoutPartialTree, ICacheTree
        {
            return ComputeHiddenLayout(tree, tree, node);
        }
    }
}
