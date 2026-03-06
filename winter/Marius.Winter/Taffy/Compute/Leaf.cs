// Ported from taffy/src/compute/leaf.rs
// Computes size using styles and measure functions

using System;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// Computes the layout of leaf nodes (nodes with no children).
    /// </summary>
    public static class LeafCompute
    {
        /// <summary>
        /// Compute the size of a leaf node (node with no children)
        /// </summary>
        /// <param name="inputs">The layout input constraints/hints from the parent</param>
        /// <param name="style">The style of the node</param>
        /// <param name="resolveCalcValue">Function to resolve calc() values</param>
        /// <param name="measureFunction">Function to measure the node's content size</param>
        /// <returns>The computed layout output</returns>
        public static LayoutOutput ComputeLeafLayout(
            LayoutInput inputs,
            ICoreStyle style,
            Func<IntPtr, float, float> resolveCalcValue,
            Func<Size<float?>, Size<AvailableSpace>, Size<float>> measureFunction)
        {
            var knownDimensions = inputs.KnownDimensions;
            var parentSize = inputs.ParentSize;
            var availableSpace = inputs.AvailableSpace;
            var sizingMode = inputs.SizingMode;
            var runMode = inputs.RunMode;

            // Note: both horizontal and vertical percentage padding/borders are resolved against the container's inline size (i.e. width).
            // This is not a bug, but is how CSS is specified (see: https://developer.mozilla.org/en-US/docs/Web/CSS/padding#values)
            var margin = style.Margin().ResolveOrZero(parentSize.Width, resolveCalcValue);
            var padding = style.Padding().ResolveOrZero(parentSize.Width, resolveCalcValue);
            var border = style.Border().ResolveOrZero(parentSize.Width, resolveCalcValue);
            var paddingBorder = padding.Add(border);
            var pbSum = paddingBorder.SumAxes();
            var boxSizingAdjustment = style.BoxSizing() == BoxSizing.ContentBox
                ? pbSum
                : SizeExtensions.ZeroF32;

            // Resolve node's preferred/min/max sizes against the available space
            // For ContentSize mode, we pretend that the node has no size styles
            Size<float?> nodeSize;
            Size<float?> nodeMinSize;
            Size<float?> nodeMaxSize;
            float? aspectRatio;

            switch (sizingMode)
            {
                case SizingMode.ContentSize:
                    nodeSize = knownDimensions;
                    nodeMinSize = SizeExtensions.NoneF32;
                    nodeMaxSize = SizeExtensions.NoneF32;
                    aspectRatio = null;
                    break;

                case SizingMode.InherentSize:
                    aspectRatio = style.AspectRatio();
                    var styleSize = style.Size()
                        .MaybeResolve(parentSize, resolveCalcValue)
                        .MaybeApplyAspectRatio(aspectRatio)
                        .MaybeAdd(boxSizingAdjustment.Map<float?>(v => v));
                    var styleMinSize = style.MinSize()
                        .MaybeResolve(parentSize, resolveCalcValue)
                        .MaybeApplyAspectRatio(aspectRatio)
                        .MaybeAdd(boxSizingAdjustment.Map<float?>(v => v));
                    var styleMaxSize = style.MaxSize()
                        .MaybeResolve(parentSize, resolveCalcValue)
                        .MaybeAdd(boxSizingAdjustment.Map<float?>(v => v));

                    nodeSize = knownDimensions.Or(styleSize);
                    nodeMinSize = styleMinSize;
                    nodeMaxSize = styleMaxSize;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(sizingMode));
            }

            // Scrollbar gutters are reserved when the overflow property is set to Overflow.Scroll.
            // However, the axes are switched (transposed) because a node that scrolls vertically needs
            // horizontal space to be reserved for a scrollbar
            var scrollbarGutter = style.Overflow().Transpose().Map(overflow =>
                overflow == Overflow.Scroll ? style.ScrollbarWidth() : 0f);

            // TODO: make side configurable based on the direction property
            var contentBoxInset = paddingBorder;
            contentBoxInset.Right += scrollbarGutter.X;
            contentBoxInset.Bottom += scrollbarGutter.Y;

            var hasStylesPreventingBeingCollapsedThrough =
                !style.IsBlock()
                || style.Overflow().X.IsScrollContainer()
                || style.Overflow().Y.IsScrollContainer()
                || style.Position() == Position.Absolute
                || padding.Top > 0f
                || padding.Bottom > 0f
                || border.Top > 0f
                || border.Bottom > 0f
                || (nodeSize.Height.HasValue && nodeSize.Height.Value > 0f)
                || (nodeMinSize.Height.HasValue && nodeMinSize.Height.Value > 0f);

            // Return early if both width and height are known
            if (runMode == RunMode.ComputeSize && hasStylesPreventingBeingCollapsedThrough)
            {
                if (nodeSize.Width.HasValue && nodeSize.Height.HasValue)
                {
                    var earlySize = new Size<float>(nodeSize.Width.Value, nodeSize.Height.Value)
                        .MaybeClamp(nodeMinSize, nodeMaxSize)
                        .MaybeMax(paddingBorder.SumAxes().Map<float?>(v => v));
                    return new LayoutOutput
                    {
                        Size = earlySize,
                        ContentSize = SizeExtensions.ZeroF32,
                        FirstBaselines = PointExtensions.NoneF32,
                        TopMargin = CollapsibleMarginSet.ZERO,
                        BottomMargin = CollapsibleMarginSet.ZERO,
                        MarginsCanCollapseThrough = false,
                    };
                }
            }

            // Compute available space
            var computedAvailableSpace = new Size<AvailableSpace>(
                width: knownDimensions.Width.HasValue
                    ? ((AvailableSpace)knownDimensions.Width.Value)
                    : availableSpace.Width
                    .MaybeSub(margin.HorizontalAxisSum())
                    .MaybeSet(knownDimensions.Width)
                    .MaybeSet(nodeSize.Width)
                    .MapDefiniteValue(size =>
                        size.MaybeClamp(nodeMinSize.Width, nodeMaxSize.Width) - contentBoxInset.HorizontalAxisSum()),
                height: knownDimensions.Height.HasValue
                    ? ((AvailableSpace)knownDimensions.Height.Value)
                    : availableSpace.Height
                    .MaybeSub(margin.VerticalAxisSum())
                    .MaybeSet(knownDimensions.Height)
                    .MaybeSet(nodeSize.Height)
                    .MapDefiniteValue(size =>
                        size.MaybeClamp(nodeMinSize.Height, nodeMaxSize.Height) - contentBoxInset.VerticalAxisSum())
            );

            // Measure node
            Size<float?> measureKnownDimensions;
            switch (runMode)
            {
                case RunMode.ComputeSize:
                    measureKnownDimensions = knownDimensions;
                    break;
                case RunMode.PerformLayout:
                    measureKnownDimensions = SizeExtensions.NoneF32;
                    break;
                default:
                    throw new InvalidOperationException("PerformHiddenLayout should not reach leaf compute");
            }

            var measuredSize = measureFunction(measureKnownDimensions, computedAvailableSpace);

            var clampedSize = knownDimensions
                .Or(nodeSize)
                .UnwrapOr(measuredSize.Add(contentBoxInset.SumAxes()))
                .MaybeClamp(nodeMinSize, nodeMaxSize);

            var finalSize = new Size<float>(
                clampedSize.Width,
                MathF.Max(clampedSize.Height, aspectRatio.HasValue ? clampedSize.Width / aspectRatio.Value : 0f)
            );
            finalSize = finalSize.MaybeMax(paddingBorder.SumAxes().Map<float?>(v => v));

            return new LayoutOutput
            {
                Size = finalSize,
                ContentSize = measuredSize.Add(padding.SumAxes()),
                FirstBaselines = PointExtensions.NoneF32,
                TopMargin = CollapsibleMarginSet.ZERO,
                BottomMargin = CollapsibleMarginSet.ZERO,
                MarginsCanCollapseThrough = !hasStylesPreventingBeingCollapsedThrough
                    && finalSize.Height == 0f
                    && measuredSize.Height == 0f,
            };
        }
    }
}
