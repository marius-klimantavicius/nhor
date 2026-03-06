// Ported from taffy/src/compute/flexbox.rs
// Computes the flexbox layout algorithm on a tree according to the spec:
// https://www.w3.org/TR/css-flexbox-1/

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// The intermediate results of a flexbox calculation for a single item
    /// </summary>
    internal struct FlexItem
    {
        /// The identifier for the associated node
        public NodeId Node;

        /// The order of the node relative to its siblings
        public uint Order;

        /// The base size of this item
        public Size<float?> Size;
        /// The minimum allowable size of this item
        public Size<float?> MinSize;
        /// The maximum allowable size of this item
        public Size<float?> MaxSize;
        /// The cross-alignment of this item
        public AlignItems AlignSelf;

        /// The overflow style of the item
        public Point<Overflow> OverflowStyle;
        /// The width of the scrollbars (if it has any)
        public float ScrollbarWidth;
        /// The flex shrink style of the item
        public float FlexShrink;
        /// The flex grow style of the item
        public float FlexGrow;

        /// The minimum size of the item. This differs from MinSize above because it also
        /// takes into account content based automatic minimum sizes
        public float ResolvedMinimumMainSize;

        /// The final offset of this item
        public Rect<float?> Inset;
        /// The margin of this item
        public Rect<float> Margin;
        /// Whether each margin is an auto margin or not
        public Rect<bool> MarginIsAuto;
        /// The padding of this item
        public Rect<float> Padding;
        /// The border of this item
        public Rect<float> Border;

        /// The default size of this item
        public float FlexBasis;
        /// The default size of this item, minus padding and border
        public float InnerFlexBasis;
        /// The amount by which this item has deviated from its target size
        public float Violation;
        /// Is the size of this item locked
        public bool Frozen;

        /// Either the max- or min- content flex fraction
        /// See https://www.w3.org/TR/css-flexbox-1/#intrinsic-main-sizes
        public float ContentFlexFraction;

        /// The proposed inner size of this item
        public Size<float> HypotheticalInnerSize;
        /// The proposed outer size of this item
        public Size<float> HypotheticalOuterSize;
        /// The size that this item wants to be
        public Size<float> TargetSize;
        /// The size that this item wants to be, plus any padding and border
        public Size<float> OuterTargetSize;

        /// The position of the bottom edge of this item
        public float Baseline;

        /// A temporary value for the main offset
        ///
        /// Offset is the relative position from the item's natural flow position based on
        /// relative position values, alignment, and justification. Does not include margin/padding/border.
        public float OffsetMain;
        /// A temporary value for the cross offset
        ///
        /// Offset is the relative position from the item's natural flow position based on
        /// relative position values, alignment, and justification. Does not include margin/padding/border.
        public float OffsetCross;

        /// Returns true if the item is a scroll container
        /// https://www.w3.org/TR/css-overflow-3/#scroll-container
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsScrollContainer()
        {
            return OverflowStyle.X.IsScrollContainer() | OverflowStyle.Y.IsScrollContainer();
        }
    }

    /// <summary>
    /// A line of FlexItems used for intermediate computation.
    /// References a slice of the shared backing Memory to avoid per-line copies.
    /// </summary>
    internal class FlexLine
    {
        /// The backing memory slice for this line's items
        private Memory<FlexItem> _items;

        /// The slice of items to iterate over during computation of this line
        public Span<FlexItem> Items
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _items.Span;
        }

        /// The starting index in the original flex_items array
        public int StartIndex;
        /// The dimensions of the cross-axis
        public float CrossSize;
        /// The relative offset of the cross-axis
        public float OffsetCross;

        public FlexLine(Memory<FlexItem> items, int startIndex, float crossSize, float offsetCross)
        {
            _items = items;
            StartIndex = startIndex;
            CrossSize = crossSize;
            OffsetCross = offsetCross;
        }
    }

    /// <summary>
    /// Values that can be cached during the flexbox algorithm
    /// </summary>
    internal struct AlgoConstants
    {
        /// The direction of the current segment being laid out
        public FlexDirection Dir;
        /// Is this segment a row
        public bool IsRow;
        /// Is this segment a column
        public bool IsColumn;
        /// Is wrapping enabled (in either direction)
        public bool IsWrap;
        /// Is the wrap direction inverted
        public bool IsWrapReverse;

        /// The item's min_size style
        public Size<float?> MinSize;
        /// The item's max_size style
        public Size<float?> MaxSize;
        /// The margin of this section
        public Rect<float> Margin;
        /// The border of this section
        public Rect<float> Border;
        /// The space between the content box and the border box.
        /// This consists of padding + border + scrollbar_gutter.
        public Rect<float> ContentBoxInset;
        /// The size reserved for scrollbar gutters in each axis
        public Point<float> ScrollbarGutter;
        /// The gap of this section
        public Size<float> Gap;
        /// The align_items property of this node
        public AlignItems AlignItemsStyle;
        /// The align_content property of this node
        public AlignContent AlignContentStyle;
        /// The justify_content property of this node
        public AlignContent? JustifyContentStyle;

        /// The border-box size of the node being laid out (if known)
        public Size<float?> NodeOuterSize;
        /// The content-box size of the node being laid out (if known)
        public Size<float?> NodeInnerSize;

        /// The size of the virtual container containing the flex items.
        public Size<float> ContainerSize;
        /// The size of the internal container
        public Size<float> InnerContainerSize;
    }

    /// <summary>
    /// Implements the flexbox layout algorithm.
    /// Port of taffy/src/compute/flexbox.rs
    /// </summary>
    public static class FlexboxAlgorithm
    {
        /// <summary>
        /// Computes the layout of a box according to the flexbox algorithm
        /// </summary>
        public static LayoutOutput ComputeFlexboxLayout(
            ILayoutFlexboxContainer tree,
            NodeId node,
            LayoutInput inputs)
        {
            var knownDimensions = inputs.KnownDimensions;
            var parentSize = inputs.ParentSize;
            var runMode = inputs.RunMode;

            var style = tree.GetFlexboxContainerStyle(node);

            // Pull these out earlier to avoid borrowing issues
            var aspectRatio = style.AspectRatio();
            var padding = style.Padding().ResolveOrZero(parentSize.Width, tree.Calc);
            var border = style.Border().ResolveOrZero(parentSize.Width, tree.Calc);
            var paddingBorderSum = padding.SumAxes().Add(border.SumAxes());
            var boxSizingAdjustment =
                style.BoxSizing() == BoxSizing.ContentBox ? paddingBorderSum : SizeExtensions.ZeroF32;

            var minSize = style
                .MinSize()
                .MaybeResolve(parentSize, tree.Calc)
                .MaybeApplyAspectRatio(aspectRatio)
                .MaybeAdd(boxSizingAdjustment.Map(v => (float?)v));
            var maxSize = style
                .MaxSize()
                .MaybeResolve(parentSize, tree.Calc)
                .MaybeApplyAspectRatio(aspectRatio)
                .MaybeAdd(boxSizingAdjustment.Map(v => (float?)v));
            var clampedStyleSize = inputs.SizingMode == SizingMode.InherentSize
                ? style
                    .Size()
                    .MaybeResolve(parentSize, tree.Calc)
                    .MaybeApplyAspectRatio(aspectRatio)
                    .MaybeAdd(boxSizingAdjustment.Map(v => (float?)v))
                    .MaybeClamp(minSize, maxSize)
                : SizeExtensions.NoneF32;

            // If both min and max in a given axis are set and max <= min then this determines the size in that axis
            var minMaxDefiniteSize = new Size<float?>(
                (minSize.Width.HasValue && maxSize.Width.HasValue && maxSize.Width.Value <= minSize.Width.Value) ? minSize.Width : null,
                (minSize.Height.HasValue && maxSize.Height.HasValue && maxSize.Height.Value <= minSize.Height.Value) ? minSize.Height : null
            );

            // The size of the container should be floored by the padding and border
            var styledBasedKnownDimensions = knownDimensions
                .Or(minMaxDefiniteSize.Or(clampedStyleSize).MaybeMax(paddingBorderSum.Map(v => (float?)v)));

            // Short-circuit layout if the container's size is fully determined by the container's size and the run mode
            // is ComputeSize (and thus the container's size is all that we're interested in)
            if (runMode == RunMode.ComputeSize)
            {
                if (styledBasedKnownDimensions.Width.HasValue && styledBasedKnownDimensions.Height.HasValue)
                {
                    return LayoutOutput.FromOuterSize(new Size<float>(
                        styledBasedKnownDimensions.Width.Value,
                        styledBasedKnownDimensions.Height.Value));
                }
            }

            return ComputePreliminary(tree, node, new LayoutInput
            {
                KnownDimensions = styledBasedKnownDimensions,
                ParentSize = inputs.ParentSize,
                AvailableSpace = inputs.AvailableSpace,
                RunMode = inputs.RunMode,
                SizingMode = inputs.SizingMode,
                Axis = inputs.Axis,
                VerticalMarginsAreCollapsible = inputs.VerticalMarginsAreCollapsible,
            });
        }

        /// <summary>
        /// Compute a preliminary size for an item
        /// </summary>
        private static LayoutOutput ComputePreliminary(
            ILayoutFlexboxContainer tree,
            NodeId node,
            LayoutInput inputs)
        {
            var knownDimensions = inputs.KnownDimensions;
            var parentSize = inputs.ParentSize;
            var availableSpace = inputs.AvailableSpace;
            var runMode = inputs.RunMode;

            // Define some general constants we will need for the remainder of the algorithm.
            var constants = ComputeConstants(tree, tree.GetFlexboxContainerStyle(node), knownDimensions, parentSize);

            // 9. Flex Layout Algorithm

            // 9.1. Initial Setup

            // 1. Generate anonymous flex items as described in §4 Flex Items.
            var flexItems = GenerateAnonymousFlexItems(tree, node, ref constants);

            // 9.2. Line Length Determination

            // 2. Determine the available main and cross space for the flex items
            availableSpace = DetermineAvailableSpace(knownDimensions, availableSpace, ref constants);

            // 3. Determine the flex base size and hypothetical main size of each item.
            DetermineFlexBaseSize(tree, ref constants, availableSpace, flexItems);

            // 4. Determine the main size of the flex container
            // This has already been done as part of compute_constants. The inner size is exposed as constants.node_inner_size.

            // 9.3. Main Size Determination

            // 5. Collect flex items into flex lines.
            var flexLines = CollectFlexLines(ref constants, availableSpace, flexItems);

            // If container size is undefined, determine the container's main size
            // and then re-resolve gaps based on newly determined size
            var mainSize = constants.NodeInnerSize.Main(constants.Dir);
            if (mainSize.HasValue)
            {
                var innerMainSize = mainSize.Value;
                var outerMainSize = innerMainSize + constants.ContentBoxInset.MainAxisSum(constants.Dir);
                constants.InnerContainerSize.SetMain(constants.Dir, innerMainSize);
                constants.ContainerSize.SetMain(constants.Dir, outerMainSize);
            }
            else
            {
                // Sets constants.container_size and constants.outer_container_size
                DetermineContainerMainSize(tree, availableSpace, flexLines, ref constants);
                constants.NodeInnerSize.SetMain(constants.Dir, constants.InnerContainerSize.Main(constants.Dir));
                constants.NodeOuterSize.SetMain(constants.Dir, constants.ContainerSize.Main(constants.Dir));

                // Re-resolve percentage gaps
                var style = tree.GetFlexboxContainerStyle(node);
                var innerContainerSize = constants.InnerContainerSize.Main(constants.Dir);
                var newGap = style
                    .Gap()
                    .Main(constants.Dir)
                    .MaybeResolve(innerContainerSize, tree.Calc)
                    ?? 0f;
                constants.Gap.SetMain(constants.Dir, newGap);
            }

            // 6. Resolve the flexible lengths of all the flex items to find their used main size.
            for (int i = 0; i < flexLines.Count; i++)
            {
                ResolveFlexibleLengths(flexLines[i], ref constants);
            }

            // 9.4. Cross Size Determination

            // 7. Determine the hypothetical cross size of each item.
            for (int i = 0; i < flexLines.Count; i++)
            {
                DetermineHypotheticalCrossSize(tree, flexLines[i], ref constants, availableSpace);
            }

            // Calculate child baselines. This function is internally smart and only computes child baselines
            // if they are necessary.
            CalculateChildrenBaseLines(tree, knownDimensions, availableSpace, flexLines, ref constants);

            // 8. Calculate the cross size of each flex line.
            CalculateCrossSize(flexLines, knownDimensions, ref constants);

            // 9. Handle 'align-content: stretch'.
            HandleAlignContentStretch(flexLines, knownDimensions, ref constants);

            // 10. Collapse visibility:collapse items. If any flex items have visibility: collapse, ...
            // TODO implement once (if ever) we support visibility:collapse

            // 11. Determine the used cross size of each flex item.
            DetermineUsedCrossSize(tree, flexLines, ref constants);

            // 9.5. Main-Axis Alignment

            // 12. Distribute any remaining free space.
            DistributeRemainingFreeSpace(flexLines, ref constants);

            // 9.6. Cross-Axis Alignment

            // 13. Resolve cross-axis auto margins (also includes 14).
            ResolveCrossAxisAutoMargins(flexLines, ref constants);

            // 15. Determine the flex container's used cross size.
            var totalLineCrossSize = DetermineContainerCrossSize(flexLines, knownDimensions, ref constants);

            // We have the container size.
            // If our caller does not care about performing layout we are done now.
            if (runMode == RunMode.ComputeSize)
            {
                return LayoutOutput.FromOuterSize(constants.ContainerSize);
            }

            // 16. Align all flex lines per align-content.
            AlignFlexLinesPerAlignContent(flexLines, ref constants, totalLineCrossSize);

            // Do a final layout pass and gather the resulting layouts
            var inflowContentSize = FinalLayoutPass(tree, flexLines, ref constants);

            // Before returning we perform absolute layout on all absolutely positioned children
            var absoluteContentSize = PerformAbsoluteLayoutOnAbsoluteChildren(tree, node, ref constants);

            // Hidden layout
            var len = tree.ChildCount(node);
            for (int order = 0; order < len; order++)
            {
                var child = tree.GetChildId(node, order);
                if (tree.GetFlexboxChildStyle(child).BoxGenerationMode() == BoxGenerationMode.None)
                {
                    tree.SetUnroundedLayout(child, Layout.WithOrder((uint)order));
                    tree.PerformChildLayout(
                        child,
                        SizeExtensions.NoneF32,
                        SizeExtensions.NoneF32,
                        new Size<AvailableSpace>(AvailableSpace.MAX_CONTENT, AvailableSpace.MAX_CONTENT),
                        SizingMode.InherentSize,
                        LineExtensions.FalseLine);
                }
            }

            // 8.5. Flex Container Baselines: calculate the flex container's first baseline
            // See https://www.w3.org/TR/css-flexbox-1/#flex-baselines
            float? firstVerticalBaseline;
            if (flexLines.Count == 0)
            {
                firstVerticalBaseline = null;
            }
            else
            {
                var firstLineItems = flexLines[0].Items;
                FlexItem? baselineChild = null;

                // Find the first item with baseline alignment (for rows) or the first item
                foreach (var item in firstLineItems)
                {
                    if (constants.IsColumn || item.AlignSelf == AlignItems.Baseline)
                    {
                        baselineChild = item;
                        break;
                    }
                }
                if (!baselineChild.HasValue && firstLineItems.Length > 0)
                {
                    baselineChild = firstLineItems[0];
                }

                if (baselineChild.HasValue)
                {
                    var child = baselineChild.Value;
                    var offsetVertical = constants.IsRow ? child.OffsetCross : child.OffsetMain;
                    firstVerticalBaseline = offsetVertical + child.Baseline;
                }
                else
                {
                    firstVerticalBaseline = null;
                }
            }

            return LayoutOutput.FromSizesAndBaselines(
                constants.ContainerSize,
                inflowContentSize.F32Max(absoluteContentSize),
                new Point<float?>(null, firstVerticalBaseline));
        }

        /// <summary>
        /// Compute constants that can be reused during the flexbox algorithm.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AlgoConstants ComputeConstants(
            ILayoutFlexboxContainer tree,
            IFlexboxContainerStyle style,
            Size<float?> knownDimensions,
            Size<float?> parentSize)
        {
            var dir = style.FlexDirection();
            var isRow = dir.IsRow();
            var isColumn = dir.IsColumn();
            var isWrap = style.FlexWrap() == FlexWrap.Wrap || style.FlexWrap() == FlexWrap.WrapReverse;
            var isWrapReverse = style.FlexWrap() == FlexWrap.WrapReverse;

            var aspectRatio = style.AspectRatio();
            var margin = style.Margin().ResolveOrZero(parentSize.Width, tree.Calc);
            var padding = style.Padding().ResolveOrZero(parentSize.Width, tree.Calc);
            var border = style.Border().ResolveOrZero(parentSize.Width, tree.Calc);
            var paddingBorderSum = padding.SumAxes().Add(border.SumAxes());
            var boxSizingAdjustment =
                style.BoxSizing() == BoxSizing.ContentBox ? paddingBorderSum : SizeExtensions.ZeroF32;

            var alignItems = style.AlignItems() ?? AlignItems.Stretch;
            var alignContent = style.AlignContent() ?? AlignContent.Stretch;
            var justifyContent = style.JustifyContent();

            // Scrollbar gutters are reserved when the `overflow` property is set to `Overflow::Scroll`.
            // However, the axes are switched (transposed) because a node that scrolls vertically needs
            // *horizontal* space to be reserved for a scrollbar
            var overflow = style.Overflow().Transpose();
            var scrollbarGutter = new Point<float>(
                overflow.X == Overflow.Scroll ? style.ScrollbarWidth() : 0f,
                overflow.Y == Overflow.Scroll ? style.ScrollbarWidth() : 0f);
            // TODO: make side configurable based on the `direction` property
            var contentBoxInset = padding.Add(border);
            contentBoxInset.Right += scrollbarGutter.X;
            contentBoxInset.Bottom += scrollbarGutter.Y;

            var nodeOuterSize = knownDimensions;
            var nodeInnerSize = nodeOuterSize.MaybeSub(contentBoxInset.SumAxes().Map(v => (float?)v));
            var gap = style.Gap().ResolveOrZero(nodeInnerSize.Or(new Size<float?>(0f, 0f)), tree.Calc);

            var containerSize = SizeExtensions.ZeroF32;
            var innerContainerSize = SizeExtensions.ZeroF32;

            return new AlgoConstants
            {
                Dir = dir,
                IsRow = isRow,
                IsColumn = isColumn,
                IsWrap = isWrap,
                IsWrapReverse = isWrapReverse,
                MinSize = style
                    .MinSize()
                    .MaybeResolve(parentSize, tree.Calc)
                    .MaybeApplyAspectRatio(aspectRatio)
                    .MaybeAdd(boxSizingAdjustment.Map(v => (float?)v)),
                MaxSize = style
                    .MaxSize()
                    .MaybeResolve(parentSize, tree.Calc)
                    .MaybeApplyAspectRatio(aspectRatio)
                    .MaybeAdd(boxSizingAdjustment.Map(v => (float?)v)),
                Margin = margin,
                Border = border,
                Gap = gap,
                ContentBoxInset = contentBoxInset,
                ScrollbarGutter = scrollbarGutter,
                AlignItemsStyle = alignItems,
                AlignContentStyle = alignContent,
                JustifyContentStyle = justifyContent,
                NodeOuterSize = nodeOuterSize,
                NodeInnerSize = nodeInnerSize,
                ContainerSize = containerSize,
                InnerContainerSize = innerContainerSize,
            };
        }

        /// <summary>
        /// Generate anonymous flex items.
        ///
        /// # [9.1. Initial Setup](https://www.w3.org/TR/css-flexbox-1/#box-manip)
        ///
        /// - Generate anonymous flex items as described in §4 Flex Items.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static FlexItem[] GenerateAnonymousFlexItems(
            ILayoutFlexboxContainer tree,
            NodeId node,
            ref AlgoConstants constants)
        {
            var items = new List<FlexItem>();
            int index = 0;
            foreach (var child in tree.ChildIds(node))
            {
                var childStyle = tree.GetFlexboxChildStyle(child);
                if (childStyle.Position() == Position.Absolute ||
                    childStyle.BoxGenerationMode() == BoxGenerationMode.None)
                {
                    index++;
                    continue;
                }

                var aspectRatio = childStyle.AspectRatio();
                var childPadding = childStyle.Padding().ResolveOrZero(constants.NodeInnerSize.Width, tree.Calc);
                var childBorder = childStyle.Border().ResolveOrZero(constants.NodeInnerSize.Width, tree.Calc);
                var pbSum = childPadding.SumAxes().Add(childBorder.SumAxes());
                var childBoxSizingAdj =
                    childStyle.BoxSizing() == BoxSizing.ContentBox ? pbSum : SizeExtensions.ZeroF32;
                var childBoxSizingAdjOpt = childBoxSizingAdj.Map(v => (float?)v);

                items.Add(new FlexItem
                {
                    Node = child,
                    Order = (uint)index,
                    Size = childStyle
                        .Size()
                        .MaybeResolve(constants.NodeInnerSize, tree.Calc)
                        .MaybeApplyAspectRatio(aspectRatio)
                        .MaybeAdd(childBoxSizingAdjOpt),
                    MinSize = childStyle
                        .MinSize()
                        .MaybeResolve(constants.NodeInnerSize, tree.Calc)
                        .MaybeApplyAspectRatio(aspectRatio)
                        .MaybeAdd(childBoxSizingAdjOpt),
                    MaxSize = childStyle
                        .MaxSize()
                        .MaybeResolve(constants.NodeInnerSize, tree.Calc)
                        .MaybeApplyAspectRatio(aspectRatio)
                        .MaybeAdd(childBoxSizingAdjOpt),
                    Inset = childStyle
                        .Inset()
                        .ZipSize(constants.NodeInnerSize, (p, s) => p.MaybeResolve(s, tree.Calc)),
                    Margin = childStyle
                        .Margin()
                        .ResolveOrZero(constants.NodeInnerSize.Width, tree.Calc),
                    MarginIsAuto = childStyle.Margin().Map(m => m.IsAuto()),
                    Padding = childPadding,
                    Border = childBorder,
                    AlignSelf = childStyle.AlignSelf() ?? constants.AlignItemsStyle,
                    OverflowStyle = childStyle.Overflow(),
                    ScrollbarWidth = childStyle.ScrollbarWidth(),
                    FlexGrow = childStyle.FlexGrow(),
                    FlexShrink = childStyle.FlexShrink(),
                    FlexBasis = 0f,
                    InnerFlexBasis = 0f,
                    Violation = 0f,
                    Frozen = false,
                    ResolvedMinimumMainSize = 0f,
                    HypotheticalInnerSize = SizeExtensions.ZeroF32,
                    HypotheticalOuterSize = SizeExtensions.ZeroF32,
                    TargetSize = SizeExtensions.ZeroF32,
                    OuterTargetSize = SizeExtensions.ZeroF32,
                    ContentFlexFraction = 0f,
                    Baseline = 0f,
                    OffsetMain = 0f,
                    OffsetCross = 0f,
                });
                index++;
            }
            return items.ToArray();
        }

        /// <summary>
        /// Determine the available main and cross space for the flex items.
        ///
        /// # [9.2. Line Length Determination](https://www.w3.org/TR/css-flexbox-1/#line-sizing)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Size<AvailableSpace> DetermineAvailableSpace(
            Size<float?> knownDimensions,
            Size<AvailableSpace> outerAvailableSpace,
            ref AlgoConstants constants)
        {
            // Note: min/max/preferred size styles have already been applied to known_dimensions in the `compute` function above
            var width = knownDimensions.Width.HasValue
                ? AvailableSpace.Definite(knownDimensions.Width.Value - constants.ContentBoxInset.HorizontalAxisSum())
                : outerAvailableSpace.Width
                    .MaybeSub(constants.Margin.HorizontalAxisSum())
                    .MaybeSub(constants.ContentBoxInset.HorizontalAxisSum());

            var height = knownDimensions.Height.HasValue
                ? AvailableSpace.Definite(knownDimensions.Height.Value - constants.ContentBoxInset.VerticalAxisSum())
                : outerAvailableSpace.Height
                    .MaybeSub(constants.Margin.VerticalAxisSum())
                    .MaybeSub(constants.ContentBoxInset.VerticalAxisSum());

            return new Size<AvailableSpace>(width, height);
        }

        /// <summary>
        /// Determine the flex base size and hypothetical main size of each item.
        ///
        /// # [9.2. Line Length Determination](https://www.w3.org/TR/css-flexbox-1/#line-sizing)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DetermineFlexBaseSize(
            ILayoutFlexboxContainer tree,
            ref AlgoConstants constants,
            Size<AvailableSpace> availableSpace,
            FlexItem[] flexItems)
        {
            var dir = constants.Dir;

            for (int ci = 0; ci < flexItems.Length; ci++)
            {
                var child = flexItems[ci];
                var childStyle = tree.GetFlexboxChildStyle(child.Node);

                // Parent size for child sizing
                var crossAxisParentSize = constants.NodeInnerSize.Cross(dir);
                var childParentSize = SizeExtensions.FromCross(dir, crossAxisParentSize);

                // Available space for child sizing
                var crossAxisMarginSum = constants.Margin.CrossAxisSum(dir);
                var childMinCross = child.MinSize.Cross(dir).MaybeAdd(crossAxisMarginSum);
                var childMaxCross = child.MaxSize.Cross(dir).MaybeAdd(crossAxisMarginSum);

                // Clamp available space by min- and max- size
                AvailableSpace crossAxisAvailableSpace;
                var crossAvail = availableSpace.Cross(dir);
                if (crossAvail == AvailableSpace.Definite(crossAvail.UnwrapOr(0f)) && crossAvail.IsDefinite())
                {
                    crossAxisAvailableSpace = AvailableSpace.Definite(
                        (crossAxisParentSize ?? crossAvail.UnwrapOr(0f)).MaybeClamp(childMinCross, childMaxCross));
                }
                else if (crossAvail == AvailableSpace.MinContent)
                {
                    crossAxisAvailableSpace = childMinCross.HasValue
                        ? AvailableSpace.Definite(childMinCross.Value)
                        : AvailableSpace.MinContent;
                }
                else // MaxContent
                {
                    crossAxisAvailableSpace = childMaxCross.HasValue
                        ? AvailableSpace.Definite(childMaxCross.Value)
                        : AvailableSpace.MaxContent;
                }

                // Known dimensions for child sizing
                var childKnownDimensions = child.Size.WithMain(dir, null);
                if (child.AlignSelf == AlignItems.Stretch
                    && !RectBoolCrossStart(child.MarginIsAuto, constants.Dir)
                    && !RectBoolCrossEnd(child.MarginIsAuto, constants.Dir)
                    && !childKnownDimensions.Cross(dir).HasValue)
                {
                    childKnownDimensions.SetCross(
                        dir,
                        crossAxisAvailableSpace.IntoOption().MaybeSub(child.Margin.CrossAxisSum(dir)));
                }

                var containerWidth = constants.NodeInnerSize.Main(dir);
                float boxSizingAdj;
                if (childStyle.BoxSizing() == BoxSizing.ContentBox)
                {
                    var csPadding = childStyle.Padding().ResolveOrZero(containerWidth, tree.Calc);
                    var csBorder = childStyle.Border().ResolveOrZero(containerWidth, tree.Calc);
                    boxSizingAdj = csPadding.SumAxes().Add(csBorder.SumAxes()).Main(dir);
                }
                else
                {
                    boxSizingAdj = 0f;
                }

                var flexBasisRaw = childStyle
                    .FlexBasis()
                    .MaybeResolve(containerWidth, tree.Calc)
                    .MaybeAdd(boxSizingAdj);

                // A. If the item has a definite used flex basis, that's the flex base size.
                // B. If the flex item has an intrinsic aspect ratio,
                //    a used flex basis of content, and a definite cross size,
                //    then the flex base size is calculated from its inner cross size and the flex item's intrinsic aspect ratio.
                // Note: `child.size` has already been resolved against aspect_ratio in generate_anonymous_flex_items
                var mainSizeOpt = child.Size.Main(dir);
                var resolvedFlexBasis = flexBasisRaw ?? mainSizeOpt;

                float flexBasis;
                if (resolvedFlexBasis.HasValue)
                {
                    flexBasis = resolvedFlexBasis.Value;
                }
                else
                {
                    // C. If the used flex basis is content or depends on its available space,
                    //    and the flex container is being sized under a min-content or max-content
                    //    constraint, size the item under that constraint.
                    // E. Otherwise, size the item into the available space using its used flex basis
                    //    in place of its main size, treating a value of content as max-content.
                    var childAvailableSpace = new Size<AvailableSpace>(AvailableSpace.MAX_CONTENT, AvailableSpace.MAX_CONTENT)
                        .WithMain(
                            dir,
                            availableSpace.Main(dir) == AvailableSpace.MinContent
                                ? AvailableSpace.MinContent
                                : AvailableSpace.MaxContent)
                        .WithCross(dir, crossAxisAvailableSpace);

                    flexBasis = tree.MeasureChildSize(
                        child.Node,
                        childKnownDimensions,
                        childParentSize,
                        childAvailableSpace,
                        SizingMode.ContentSize,
                        dir.MainAxis(),
                        LineExtensions.FalseLine);
                }

                // Floor flex-basis by the padding_border_sum (floors inner_flex_basis at zero)
                var paddingBorderSumMain = child.Padding.MainAxisSum(constants.Dir) + child.Border.MainAxisSum(constants.Dir);
                flexBasis = MathF.Max(flexBasis, paddingBorderSumMain);

                // The hypothetical main size is the item's flex base size clamped according to its
                // used min and max main sizes (and flooring the content box size at zero).
                child.FlexBasis = flexBasis;
                child.InnerFlexBasis =
                    flexBasis - child.Padding.MainAxisSum(constants.Dir) - child.Border.MainAxisSum(constants.Dir);

                var paddingBorderAxesSums = child.Padding.Add(child.Border).SumAxes().Map(v => (float?)v);

                // Note that it is important that the `parent_size` parameter in the main axis is not set for this
                // function call as it is used for resolving percentages, and percentage size in an axis should not contribute
                // to a min-content contribution in that same axis.
                // See https://drafts.csswg.org/css-sizing-3/#min-percentage-contribution
                var overflowMin = new Size<float?>(
                    child.OverflowStyle.X.MaybeIntoAutomaticMinSize(),
                    child.OverflowStyle.Y.MaybeIntoAutomaticMinSize());
                var styleMinMainSize = child.MinSize.Or(overflowMin).Main(dir);

                if (styleMinMainSize.HasValue)
                {
                    child.ResolvedMinimumMainSize = styleMinMainSize.Value;
                }
                else
                {
                    // Compute min-content main size
                    var childAvailSpace = new Size<AvailableSpace>(AvailableSpace.MIN_CONTENT, AvailableSpace.MIN_CONTENT)
                        .WithCross(dir, crossAxisAvailableSpace);

                    var minContentMainSize = tree.MeasureChildSize(
                        child.Node,
                        childKnownDimensions,
                        childParentSize,
                        childAvailSpace,
                        SizingMode.ContentSize,
                        dir.MainAxis(),
                        LineExtensions.FalseLine);

                    // 4.5. Automatic Minimum Size of Flex Items
                    // https://www.w3.org/TR/css-flexbox-1/#min-size-auto
                    var clampedMinContentSize = ((float?)minContentMainSize)
                        .MaybeMin(child.Size.Main(dir))
                        .MaybeMin(child.MaxSize.Main(dir));
                    child.ResolvedMinimumMainSize = clampedMinContentSize
                        .MaybeMax(paddingBorderAxesSums.Main(dir))
                        ?? 0f;
                }

                var hypotheticalInnerMinMain =
                    ((float?)child.ResolvedMinimumMainSize).MaybeMax(paddingBorderAxesSums.Main(constants.Dir)) ?? child.ResolvedMinimumMainSize;
                var hypotheticalInnerSize =
                    child.FlexBasis.MaybeClamp((float?)hypotheticalInnerMinMain, child.MaxSize.Main(constants.Dir));
                var hypotheticalOuterSize = hypotheticalInnerSize + child.Margin.MainAxisSum(constants.Dir);

                child.HypotheticalInnerSize.SetMain(constants.Dir, hypotheticalInnerSize);
                child.HypotheticalOuterSize.SetMain(constants.Dir, hypotheticalOuterSize);

                flexItems[ci] = child;
            }
        }

        /// <summary>
        /// Collect flex items into flex lines.
        ///
        /// # [9.3. Main Size Determination](https://www.w3.org/TR/css-flexbox-1/#main-sizing)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static List<FlexLine> CollectFlexLines(
            ref AlgoConstants constants,
            Size<AvailableSpace> availableSpace,
            FlexItem[] flexItems)
        {
            var memory = flexItems.AsMemory();

            if (!constants.IsWrap)
            {
                var lines = new List<FlexLine>(1);
                lines.Add(new FlexLine(memory, 0, 0f, 0f));
                return lines;
            }

            var mainAxisAvailableSpace = constants.MaxSize.Main(constants.Dir).HasValue
                ? AvailableSpace.Definite(
                    (availableSpace.Main(constants.Dir).IntoOption() ?? constants.MaxSize.Main(constants.Dir)!.Value)
                        .MaybeMax(constants.MinSize.Main(constants.Dir)))
                : availableSpace.Main(constants.Dir);

            if (mainAxisAvailableSpace == AvailableSpace.MaxContent)
            {
                // If we're sizing under a max-content constraint then the flex items will never wrap
                var lines = new List<FlexLine>(1);
                lines.Add(new FlexLine(memory, 0, 0f, 0f));
                return lines;
            }

            if (mainAxisAvailableSpace == AvailableSpace.MinContent)
            {
                // If flex-wrap is Wrap and we're sizing under a min-content constraint, then we take every
                // possible wrapping opportunity and place each item in its own line
                var lines = new List<FlexLine>(flexItems.Length);
                for (int i = 0; i < flexItems.Length; i++)
                {
                    lines.Add(new FlexLine(memory.Slice(i, 1), i, 0f, 0f));
                }
                return lines;
            }

            // Definite case
            {
                var mainAxisSpace = mainAxisAvailableSpace.UnwrapOr(0f);
                var lines = new List<FlexLine>(1);
                var mainAxisGap = constants.Gap.Main(constants.Dir);
                int startIdx = 0;

                while (startIdx < flexItems.Length)
                {
                    // Find index of the first item in the next line
                    float lineLength = 0f;
                    int splitIdx = flexItems.Length;
                    for (int idx = startIdx; idx < flexItems.Length; idx++)
                    {
                        // Gaps only occur between items (not before the first one or after the last one)
                        float gapContribution = (idx == startIdx) ? 0f : mainAxisGap;
                        lineLength += flexItems[idx].HypotheticalOuterSize.Main(constants.Dir) + gapContribution;
                        if (lineLength > mainAxisSpace && idx != startIdx)
                        {
                            splitIdx = idx;
                            break;
                        }
                    }

                    int count = splitIdx - startIdx;
                    lines.Add(new FlexLine(memory.Slice(startIdx, count), startIdx, 0f, 0f));
                    startIdx = splitIdx;
                }
                return lines;
            }
        }

        /// <summary>
        /// Determine the container's main size (if not already known)
        /// </summary>
        private static void DetermineContainerMainSize(
            ILayoutFlexboxContainer tree,
            Size<AvailableSpace> availableSpace,
            List<FlexLine> lines,
            ref AlgoConstants constants)
        {
            var dir = constants.Dir;
            var mainContentBoxInset = constants.ContentBoxInset.MainAxisSum(constants.Dir);

            float outerMainSize;
            var nodeOuterMain = constants.NodeOuterSize.Main(constants.Dir);
            if (nodeOuterMain.HasValue)
            {
                outerMainSize = nodeOuterMain.Value;
            }
            else
            {
                var mainAvail = availableSpace.Main(dir);
                if (mainAvail.IsDefinite())
                {
                    var mainAxisAvailableSpace = mainAvail.UnwrapOr(0f);
                    float longestLineLength = 0f;
                    foreach (var line in lines)
                    {
                        var lineMainAxisGap = SumAxisGaps(constants.Gap.Main(constants.Dir), line.Items.Length);
                        float totalTargetSize = 0f;
                        foreach (var child in line.Items)
                        {
                            var pbSum = child.Padding.Add(child.Border).MainAxisSum(constants.Dir);
                            totalTargetSize += MathF.Max(
                                child.FlexBasis.MaybeMax(child.MinSize.Main(constants.Dir))
                                    + child.Margin.MainAxisSum(constants.Dir),
                                pbSum);
                        }
                        longestLineLength = MathF.Max(longestLineLength, totalTargetSize + lineMainAxisGap);
                    }
                    var size = longestLineLength + mainContentBoxInset;
                    outerMainSize = lines.Count > 1
                        ? MathF.Max(size, mainAxisAvailableSpace)
                        : size;
                }
                else if (mainAvail == AvailableSpace.MinContent && constants.IsWrap)
                {
                    float longestLineLength = 0f;
                    foreach (var line in lines)
                    {
                        var lineMainAxisGap = SumAxisGaps(constants.Gap.Main(constants.Dir), line.Items.Length);
                        float totalTargetSize = 0f;
                        foreach (var child in line.Items)
                        {
                            var pbSum = child.Padding.Add(child.Border).MainAxisSum(constants.Dir);
                            totalTargetSize += MathF.Max(
                                child.FlexBasis.MaybeMax(child.MinSize.Main(constants.Dir))
                                    + child.Margin.MainAxisSum(constants.Dir),
                                pbSum);
                        }
                        longestLineLength = MathF.Max(longestLineLength, totalTargetSize + lineMainAxisGap);
                    }
                    outerMainSize = longestLineLength + mainContentBoxInset;
                }
                else
                {
                    // MinContent (non-wrap) or MaxContent
                    float mainSize = 0f;

                    for (int li = 0; li < lines.Count; li++)
                    {
                        var line = lines[li];
                        for (int ii = 0; ii < line.Items.Length; ii++)
                        {
                            var item = line.Items[ii];
                            var styleMin = item.MinSize.Main(constants.Dir);
                            var stylePreferred = item.Size.Main(constants.Dir);
                            var styleMax = item.MaxSize.Main(constants.Dir);

                            var clampingBasis = ((float?)item.FlexBasis).MaybeMax(stylePreferred);
                            var flexBasisMin = (item.FlexShrink == 0f) ? clampingBasis : null;
                            var flexBasisMax = (item.FlexGrow == 0f) ? clampingBasis : null;

                            var minMainSize = styleMin
                                .MaybeMax(flexBasisMin)
                                .OrElse(() => flexBasisMin)
                                ?? item.ResolvedMinimumMainSize;
                            minMainSize = MathF.Max(minMainSize, item.ResolvedMinimumMainSize);

                            var maxMainSize = styleMax
                                .MaybeMin(flexBasisMax)
                                .OrElse(() => flexBasisMax)
                                ?? float.PositiveInfinity;

                            float contentContribution;
                            if (maxMainSize <= minMainSize)
                            {
                                if (stylePreferred.HasValue)
                                {
                                    contentContribution = MathF.Max(MathF.Min(stylePreferred.Value, maxMainSize), minMainSize)
                                        + item.Margin.MainAxisSum(constants.Dir);
                                }
                                else
                                {
                                    contentContribution = minMainSize + item.Margin.MainAxisSum(constants.Dir);
                                }
                            }
                            else if (stylePreferred.HasValue && (maxMainSize <= minMainSize || maxMainSize <= stylePreferred.Value))
                            {
                                contentContribution = MathF.Max(MathF.Min(stylePreferred.Value, maxMainSize), minMainSize)
                                    + item.Margin.MainAxisSum(constants.Dir);
                            }
                            else if (item.IsScrollContainer())
                            {
                                contentContribution = item.FlexBasis + item.Margin.MainAxisSum(constants.Dir);
                            }
                            else
                            {
                                // Parent size for child sizing
                                var crossAxisParentSize = constants.NodeInnerSize.Cross(dir);

                                // Available space for child sizing
                                var crossAxisMarginSum = constants.Margin.CrossAxisSum(dir);
                                var childMinCross = item.MinSize.Cross(dir).MaybeAdd(crossAxisMarginSum);
                                var childMaxCross = item.MaxSize.Cross(dir).MaybeAdd(crossAxisMarginSum);
                                var crossAvailBase = availableSpace.Cross(dir);
                                var crossAxisAvailableSpace2 = crossAvailBase
                                    .MapDefiniteValue(val => crossAxisParentSize ?? val)
                                    .MaybeClamp(childMinCross, childMaxCross);

                                var childAvailableSpace2 = availableSpace.WithCross(dir, crossAxisAvailableSpace2);

                                // Known dimensions for child sizing
                                var childKnownDimensions2 = item.Size.WithMain(dir, null);
                                if (item.AlignSelf == AlignItems.Stretch && !childKnownDimensions2.Cross(dir).HasValue)
                                {
                                    childKnownDimensions2.SetCross(
                                        dir,
                                        crossAxisAvailableSpace2.IntoOption().MaybeSub(item.Margin.CrossAxisSum(dir)));
                                }

                                var contentMainSize = tree.MeasureChildSize(
                                    item.Node,
                                    childKnownDimensions2,
                                    constants.NodeInnerSize,
                                    childAvailableSpace2,
                                    SizingMode.InherentSize,
                                    dir.MainAxis(),
                                    LineExtensions.FalseLine)
                                    + item.Margin.MainAxisSum(constants.Dir);

                                if (constants.IsRow)
                                {
                                    contentContribution = contentMainSize
                                        .MaybeClamp(styleMin, styleMax);
                                    contentContribution = MathF.Max(contentContribution, mainContentBoxInset);
                                }
                                else
                                {
                                    contentContribution = MathF.Max(contentMainSize, item.FlexBasis)
                                        .MaybeClamp(styleMin, styleMax);
                                    contentContribution = MathF.Max(contentContribution, mainContentBoxInset);
                                }
                            }

                            item.ContentFlexFraction = ComputeContentFlexFraction(
                                contentContribution, item.FlexBasis, item.FlexGrow, item.FlexShrink, item.InnerFlexBasis);
                            line.Items[ii] = item;
                        }

                        // Add each item's flex base size to the product of its flex factor and the chosen flex fraction
                        float itemMainSizeSum = 0f;
                        for (int ii = 0; ii < line.Items.Length; ii++)
                        {
                            var item = line.Items[ii];
                            var flexFraction = item.ContentFlexFraction;

                            float flexContribution;
                            if (item.ContentFlexFraction > 0f)
                            {
                                flexContribution = MathF.Max(1f, item.FlexGrow) * flexFraction;
                            }
                            else if (item.ContentFlexFraction < 0f)
                            {
                                var scaledShrinkFactor = MathF.Max(1f, item.FlexShrink) * item.InnerFlexBasis;
                                flexContribution = scaledShrinkFactor * flexFraction;
                            }
                            else
                            {
                                flexContribution = 0f;
                            }
                            var sz = item.FlexBasis + flexContribution;
                            item.OuterTargetSize.SetMain(constants.Dir, sz);
                            item.TargetSize.SetMain(constants.Dir, sz);
                            line.Items[ii] = item;
                            itemMainSizeSum += sz;
                        }

                        var gapSum = SumAxisGaps(constants.Gap.Main(constants.Dir), line.Items.Length);
                        mainSize = MathF.Max(mainSize, itemMainSizeSum + gapSum);
                    }

                    outerMainSize = mainSize + mainContentBoxInset;
                }
            }

            outerMainSize = outerMainSize
                .MaybeClamp(constants.MinSize.Main(constants.Dir), constants.MaxSize.Main(constants.Dir));
            outerMainSize = MathF.Max(outerMainSize,
                mainContentBoxInset - constants.ScrollbarGutter.Main(constants.Dir));

            var innerMainSize = MathF.Max(outerMainSize - mainContentBoxInset, 0f);
            constants.ContainerSize.SetMain(constants.Dir, outerMainSize);
            constants.InnerContainerSize.SetMain(constants.Dir, innerMainSize);
            constants.NodeInnerSize.SetMain(constants.Dir, innerMainSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ComputeContentFlexFraction(
            float contentContribution, float flexBasis, float flexGrow, float flexShrink, float innerFlexBasis)
        {
            var diff = contentContribution - flexBasis;
            if (diff > 0f)
            {
                return diff / MathF.Max(1f, flexGrow);
            }
            else if (diff < 0f)
            {
                var scaledShrinkFactor = MathF.Max(1f, flexShrink * innerFlexBasis);
                return diff / scaledShrinkFactor;
            }
            return 0f;
        }

        /// <summary>
        /// Resolve the flexible lengths of the items within a flex line.
        /// Sets the main component of each item's target_size and outer_target_size.
        ///
        /// # [9.7. Resolving Flexible Lengths](https://www.w3.org/TR/css-flexbox-1/#resolve-flexible-lengths)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ResolveFlexibleLengths(FlexLine line, ref AlgoConstants constants)
        {
            var totalMainAxisGap = SumAxisGaps(constants.Gap.Main(constants.Dir), line.Items.Length);

            // 1. Determine the used flex factor.
            float totalHypotheticalOuterMainSize = 0f;
            for (int i = 0; i < line.Items.Length; i++)
                totalHypotheticalOuterMainSize += line.Items[i].HypotheticalOuterSize.Main(constants.Dir);
            float usedFlexFactor = totalMainAxisGap + totalHypotheticalOuterMainSize;
            bool growing = usedFlexFactor < (constants.NodeInnerSize.Main(constants.Dir) ?? 0f);
            bool shrinking = usedFlexFactor > (constants.NodeInnerSize.Main(constants.Dir) ?? 0f);
            bool exactlySized = !growing && !shrinking;

            // 2. Size inflexible items.
            for (int i = 0; i < line.Items.Length; i++)
            {
                ref var child = ref line.Items[i];
                var innerTargetSize = child.HypotheticalInnerSize.Main(constants.Dir);
                child.TargetSize.SetMain(constants.Dir, innerTargetSize);

                if (exactlySized
                    || (child.FlexGrow == 0f && child.FlexShrink == 0f)
                    || (growing && child.FlexBasis > child.HypotheticalInnerSize.Main(constants.Dir))
                    || (shrinking && child.FlexBasis < child.HypotheticalInnerSize.Main(constants.Dir)))
                {
                    child.Frozen = true;
                    var outerTargetSize = innerTargetSize + child.Margin.MainAxisSum(constants.Dir);
                    child.OuterTargetSize.SetMain(constants.Dir, outerTargetSize);
                }
            }

            if (exactlySized)
                return;

            // 3. Calculate initial free space.
            float usedSpace = totalMainAxisGap;
            for (int i = 0; i < line.Items.Length; i++)
            {
                var child = line.Items[i];
                usedSpace += child.Frozen
                    ? child.OuterTargetSize.Main(constants.Dir)
                    : child.FlexBasis + child.Margin.MainAxisSum(constants.Dir);
            }
            var initialFreeSpace = (constants.NodeInnerSize.Main(constants.Dir).MaybeSub(usedSpace)) ?? 0f;

            // 4. Loop
            while (true)
            {
                // a. Check for flexible items. If all the flex items on the line are frozen, exit this loop.
                bool allFrozen = true;
                for (int i = 0; i < line.Items.Length; i++)
                {
                    if (!line.Items[i].Frozen)
                    {
                        allFrozen = false;
                        break;
                    }
                }
                if (allFrozen) break;

                // b. Calculate the remaining free space.
                float usedSpace2 = totalMainAxisGap;
                for (int i = 0; i < line.Items.Length; i++)
                {
                    var child = line.Items[i];
                    usedSpace2 += child.Frozen
                        ? child.OuterTargetSize.Main(constants.Dir)
                        : child.FlexBasis + child.Margin.MainAxisSum(constants.Dir);
                }

                float sumFlexGrow = 0f;
                float sumFlexShrink = 0f;
                for (int i = 0; i < line.Items.Length; i++)
                {
                    if (!line.Items[i].Frozen)
                    {
                        sumFlexGrow += line.Items[i].FlexGrow;
                        sumFlexShrink += line.Items[i].FlexShrink;
                    }
                }

                float freeSpace;
                if (growing && sumFlexGrow < 1f)
                {
                    freeSpace = (initialFreeSpace * sumFlexGrow - totalMainAxisGap)
                        .MaybeMin(constants.NodeInnerSize.Main(constants.Dir).MaybeSub(usedSpace2));
                }
                else if (shrinking && sumFlexShrink < 1f)
                {
                    freeSpace = (initialFreeSpace * sumFlexShrink - totalMainAxisGap)
                        .MaybeMax(constants.NodeInnerSize.Main(constants.Dir).MaybeSub(usedSpace2));
                }
                else
                {
                    freeSpace = (constants.NodeInnerSize.Main(constants.Dir).MaybeSub(usedSpace2))
                        ?? (usedFlexFactor - usedSpace2);
                }

                // c. Distribute free space proportional to the flex factors.
                if (IsNormal(freeSpace))
                {
                    if (growing && sumFlexGrow > 0f)
                    {
                        for (int i = 0; i < line.Items.Length; i++)
                        {
                            if (!line.Items[i].Frozen)
                            {
                                line.Items[i].TargetSize.SetMain(constants.Dir,
                                    line.Items[i].FlexBasis + freeSpace * (line.Items[i].FlexGrow / sumFlexGrow));
                            }
                        }
                    }
                    else if (shrinking && sumFlexShrink > 0f)
                    {
                        float sumScaledShrinkFactor = 0f;
                        for (int i = 0; i < line.Items.Length; i++)
                        {
                            if (!line.Items[i].Frozen)
                                sumScaledShrinkFactor += line.Items[i].InnerFlexBasis * line.Items[i].FlexShrink;
                        }

                        if (sumScaledShrinkFactor > 0f)
                        {
                            for (int i = 0; i < line.Items.Length; i++)
                            {
                                if (!line.Items[i].Frozen)
                                {
                                    var scaledShrinkFactor = line.Items[i].InnerFlexBasis * line.Items[i].FlexShrink;
                                    line.Items[i].TargetSize.SetMain(constants.Dir,
                                        line.Items[i].FlexBasis + freeSpace * (scaledShrinkFactor / sumScaledShrinkFactor));
                                }
                            }
                        }
                    }
                }

                // d. Fix min/max violations.
                float totalViolation = 0f;
                for (int i = 0; i < line.Items.Length; i++)
                {
                    if (!line.Items[i].Frozen)
                    {
                        float? resolvedMinMain = line.Items[i].ResolvedMinimumMainSize;
                        var maxMain = line.Items[i].MaxSize.Main(constants.Dir);
                        var clamped = MathF.Max(
                            line.Items[i].TargetSize.Main(constants.Dir).MaybeClamp(resolvedMinMain, maxMain),
                            0f);
                        line.Items[i].Violation = clamped - line.Items[i].TargetSize.Main(constants.Dir);
                        line.Items[i].TargetSize.SetMain(constants.Dir, clamped);
                        line.Items[i].OuterTargetSize.SetMain(constants.Dir,
                            line.Items[i].TargetSize.Main(constants.Dir) + line.Items[i].Margin.MainAxisSum(constants.Dir));
                        totalViolation += line.Items[i].Violation;
                    }
                }

                // e. Freeze over-flexed items.
                for (int i = 0; i < line.Items.Length; i++)
                {
                    if (!line.Items[i].Frozen)
                    {
                        if (totalViolation > 0f)
                            line.Items[i].Frozen = line.Items[i].Violation > 0f;
                        else if (totalViolation < 0f)
                            line.Items[i].Frozen = line.Items[i].Violation < 0f;
                        else
                            line.Items[i].Frozen = true;
                    }
                }

                // f. Return to the start of this loop.
            }
        }

        /// <summary>
        /// Determine the hypothetical cross size of each item.
        ///
        /// # [9.4. Cross Size Determination](https://www.w3.org/TR/css-flexbox-1/#cross-sizing)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DetermineHypotheticalCrossSize(
            ILayoutFlexboxContainer tree,
            FlexLine line,
            ref AlgoConstants constants,
            Size<AvailableSpace> availableSpace)
        {
            for (int i = 0; i < line.Items.Length; i++)
            {
                ref var child = ref line.Items[i];
                var paddingBorderSum = child.Padding.Add(child.Border).CrossAxisSum(constants.Dir);

                var childKnownMain = (float?)constants.ContainerSize.Main(constants.Dir);

                var childCross = child
                    .Size
                    .Cross(constants.Dir)
                    .MaybeClamp(child.MinSize.Cross(constants.Dir), child.MaxSize.Cross(constants.Dir))
                    .MaybeMax(paddingBorderSum);

                var childAvailableCross = availableSpace
                    .Cross(constants.Dir)
                    .MaybeClamp(child.MinSize.Cross(constants.Dir), child.MaxSize.Cross(constants.Dir))
                    .MaybeMax(paddingBorderSum);

                float childInnerCross;
                if (childCross.HasValue)
                {
                    childInnerCross = childCross.Value;
                }
                else
                {
                    childInnerCross = tree.MeasureChildSize(
                        child.Node,
                        new Size<float?>(
                            constants.IsRow ? (float?)child.TargetSize.Width : childCross,
                            constants.IsRow ? childCross : (float?)child.TargetSize.Height),
                        constants.NodeInnerSize,
                        new Size<AvailableSpace>(
                            constants.IsRow ? (AvailableSpace)childKnownMain : childAvailableCross,
                            constants.IsRow ? childAvailableCross : (AvailableSpace)childKnownMain),
                        SizingMode.ContentSize,
                        constants.Dir.CrossAxis(),
                        LineExtensions.FalseLine);
                    childInnerCross = childInnerCross
                        .MaybeClamp(child.MinSize.Cross(constants.Dir), child.MaxSize.Cross(constants.Dir));
                    childInnerCross = MathF.Max(childInnerCross, paddingBorderSum);
                }

                var childOuterCross = childInnerCross + child.Margin.CrossAxisSum(constants.Dir);

                child.HypotheticalInnerSize.SetCross(constants.Dir, childInnerCross);
                child.HypotheticalOuterSize.SetCross(constants.Dir, childOuterCross);
            }
        }

        /// <summary>
        /// Calculate the base lines of the children.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CalculateChildrenBaseLines(
            ILayoutFlexboxContainer tree,
            Size<float?> nodeSize,
            Size<AvailableSpace> availableSpace,
            List<FlexLine> flexLines,
            ref AlgoConstants constants)
        {
            // Only compute baselines for flex rows
            if (!constants.IsRow)
                return;

            foreach (var line in flexLines)
            {
                // If a flex line has one or zero items participating in baseline alignment then baseline alignment is a no-op
                int lineBaselineChildCount = 0;
                foreach (var child in line.Items)
                {
                    if (child.AlignSelf == AlignItems.Baseline)
                        lineBaselineChildCount++;
                }
                if (lineBaselineChildCount <= 1)
                    continue;

                for (int i = 0; i < line.Items.Length; i++)
                {
                    ref var child = ref line.Items[i];
                    // Only calculate baselines for children participating in baseline alignment
                    if (child.AlignSelf != AlignItems.Baseline)
                        continue;

                    var measuredSizeAndBaselines = tree.PerformChildLayout(
                        child.Node,
                        new Size<float?>(
                            constants.IsRow ? (float?)child.TargetSize.Width : (float?)child.HypotheticalInnerSize.Width,
                            constants.IsRow ? (float?)child.HypotheticalInnerSize.Height : (float?)child.TargetSize.Height),
                        constants.NodeInnerSize,
                        new Size<AvailableSpace>(
                            constants.IsRow
                                ? (AvailableSpace)constants.ContainerSize.Width
                                : availableSpace.Width.MaybeSet(nodeSize.Width),
                            constants.IsRow
                                ? availableSpace.Height.MaybeSet(nodeSize.Height)
                                : (AvailableSpace)constants.ContainerSize.Height),
                        SizingMode.ContentSize,
                        LineExtensions.FalseLine);

                    var baseline = measuredSizeAndBaselines.FirstBaselines.Y;
                    var height = measuredSizeAndBaselines.Size.Height;

                    child.Baseline = (baseline ?? height) + child.Margin.Top;
                }
            }
        }

        /// <summary>
        /// Calculate the cross size of each flex line.
        ///
        /// # [9.4. Cross Size Determination](https://www.w3.org/TR/css-flexbox-1/#cross-sizing)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CalculateCrossSize(
            List<FlexLine> flexLines,
            Size<float?> nodeSize,
            ref AlgoConstants constants)
        {
            // If the flex container is single-line and has a definite cross size,
            // the cross size of the flex line is the flex container's inner cross size.
            if (!constants.IsWrap && nodeSize.Cross(constants.Dir).HasValue)
            {
                var crossAxisPaddingBorder = constants.ContentBoxInset.CrossAxisSum(constants.Dir);
                var crossMinSize = constants.MinSize.Cross(constants.Dir);
                var crossMaxSize = constants.MaxSize.Cross(constants.Dir);
                flexLines[0].CrossSize = (nodeSize
                    .Cross(constants.Dir)
                    .MaybeClamp(crossMinSize, crossMaxSize)
                    .MaybeSub(crossAxisPaddingBorder)
                    .MaybeMax(0f))
                    ?? 0f;
            }
            else
            {
                for (int li = 0; li < flexLines.Count; li++)
                {
                    var line = flexLines[li];
                    float maxBaseline = 0f;
                    foreach (var child in line.Items)
                        maxBaseline = MathF.Max(maxBaseline, child.Baseline);

                    float maxCross = 0f;
                    foreach (var child in line.Items)
                    {
                        float val;
                        if (child.AlignSelf == AlignItems.Baseline
                            && !RectBoolCrossStart(child.MarginIsAuto, constants.Dir)
                            && !RectBoolCrossEnd(child.MarginIsAuto, constants.Dir))
                        {
                            val = maxBaseline - child.Baseline + child.HypotheticalOuterSize.Cross(constants.Dir);
                        }
                        else
                        {
                            val = child.HypotheticalOuterSize.Cross(constants.Dir);
                        }
                        maxCross = MathF.Max(maxCross, val);
                    }
                    line.CrossSize = maxCross;
                }

                // If the flex container is single-line, then clamp the line's cross-size
                if (!constants.IsWrap)
                {
                    var crossAxisPaddingBorder = constants.ContentBoxInset.CrossAxisSum(constants.Dir);
                    var crossMinSize = constants.MinSize.Cross(constants.Dir);
                    var crossMaxSize = constants.MaxSize.Cross(constants.Dir);
                    flexLines[0].CrossSize = flexLines[0].CrossSize.MaybeClamp(
                        crossMinSize.MaybeSub(crossAxisPaddingBorder),
                        crossMaxSize.MaybeSub(crossAxisPaddingBorder));
                }
            }
        }

        /// <summary>
        /// Handle 'align-content: stretch'.
        ///
        /// # [9.4. Cross Size Determination](https://www.w3.org/TR/css-flexbox-1/#cross-sizing)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void HandleAlignContentStretch(
            List<FlexLine> flexLines,
            Size<float?> nodeSize,
            ref AlgoConstants constants)
        {
            if (constants.AlignContentStyle == AlignContent.Stretch)
            {
                var crossAxisPaddingBorder = constants.ContentBoxInset.CrossAxisSum(constants.Dir);
                var crossMinSize = constants.MinSize.Cross(constants.Dir);
                var crossMaxSize = constants.MaxSize.Cross(constants.Dir);
                var containerMinInnerCross = (nodeSize
                    .Cross(constants.Dir)
                    .OrElse(() => crossMinSize)
                    .MaybeClamp(crossMinSize, crossMaxSize)
                    .MaybeSub(crossAxisPaddingBorder)
                    .MaybeMax(0f))
                    ?? 0f;

                var totalCrossAxisGap = SumAxisGaps(constants.Gap.Cross(constants.Dir), flexLines.Count);
                float linesTotalCross = totalCrossAxisGap;
                foreach (var line in flexLines)
                    linesTotalCross += line.CrossSize;

                if (linesTotalCross < containerMinInnerCross)
                {
                    var remaining = containerMinInnerCross - linesTotalCross;
                    var addition = remaining / flexLines.Count;
                    foreach (var line in flexLines)
                        line.CrossSize += addition;
                }
            }
        }

        /// <summary>
        /// Determine the used cross size of each flex item.
        ///
        /// # [9.4. Cross Size Determination](https://www.w3.org/TR/css-flexbox-1/#cross-sizing)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DetermineUsedCrossSize(
            ILayoutFlexboxContainer tree,
            List<FlexLine> flexLines,
            ref AlgoConstants constants)
        {
            foreach (var line in flexLines)
            {
                var lineCrossSize = line.CrossSize;

                for (int i = 0; i < line.Items.Length; i++)
                {
                    ref var child = ref line.Items[i];
                    var childStyle = tree.GetFlexboxChildStyle(child.Node);

                    float crossSize;
                    if (child.AlignSelf == AlignItems.Stretch
                        && !RectBoolCrossStart(child.MarginIsAuto, constants.Dir)
                        && !RectBoolCrossEnd(child.MarginIsAuto, constants.Dir)
                        && childStyle.Size().Cross(constants.Dir).IsAuto())
                    {
                        // For some reason this particular usage of max_width is an exception to the rule that max_width's transfer
                        // using the aspect_ratio (if set).
                        var cPadding = childStyle.Padding().ResolveOrZero(constants.NodeInnerSize, tree.Calc);
                        var cBorder = childStyle.Border().ResolveOrZero(constants.NodeInnerSize, tree.Calc);
                        var cPbSum = cPadding.SumAxes().Add(cBorder.SumAxes());
                        var cBoxSizingAdj = childStyle.BoxSizing() == BoxSizing.ContentBox
                            ? cPbSum : SizeExtensions.ZeroF32;

                        var maxSizeIgnoringAspectRatio = childStyle
                            .MaxSize()
                            .MaybeResolve(constants.NodeInnerSize, tree.Calc)
                            .MaybeAdd(cBoxSizingAdj.Map(v => (float?)v));

                        crossSize = (lineCrossSize - child.Margin.CrossAxisSum(constants.Dir)).MaybeClamp(
                            child.MinSize.Cross(constants.Dir),
                            maxSizeIgnoringAspectRatio.Cross(constants.Dir));
                    }
                    else
                    {
                        crossSize = child.HypotheticalInnerSize.Cross(constants.Dir);
                    }

                    child.TargetSize.SetCross(constants.Dir, crossSize);
                    child.OuterTargetSize.SetCross(
                        constants.Dir,
                        child.TargetSize.Cross(constants.Dir) + child.Margin.CrossAxisSum(constants.Dir));
                }
            }
        }

        /// <summary>
        /// Distribute any remaining free space.
        ///
        /// # [9.5. Main-Axis Alignment](https://www.w3.org/TR/css-flexbox-1/#main-alignment)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DistributeRemainingFreeSpace(
            List<FlexLine> flexLines,
            ref AlgoConstants constants)
        {
            foreach (var line in flexLines)
            {
                var totalMainAxisGap = SumAxisGaps(constants.Gap.Main(constants.Dir), line.Items.Length);
                float usedSpace = totalMainAxisGap;
                for (int i = 0; i < line.Items.Length; i++)
                    usedSpace += line.Items[i].OuterTargetSize.Main(constants.Dir);
                var freeSpace = constants.InnerContainerSize.Main(constants.Dir) - usedSpace;
                int numAutoMargins = 0;

                for (int i = 0; i < line.Items.Length; i++)
                {
                    if (RectBoolMainStart(line.Items[i].MarginIsAuto, constants.Dir))
                        numAutoMargins++;
                    if (RectBoolMainEnd(line.Items[i].MarginIsAuto, constants.Dir))
                        numAutoMargins++;
                }

                if (freeSpace > 0f && numAutoMargins > 0)
                {
                    var marginVal = freeSpace / numAutoMargins;

                    for (int i = 0; i < line.Items.Length; i++)
                    {
                        if (RectBoolMainStart(line.Items[i].MarginIsAuto, constants.Dir))
                        {
                            if (constants.IsRow)
                                line.Items[i].Margin.Left = marginVal;
                            else
                                line.Items[i].Margin.Top = marginVal;
                        }
                        if (RectBoolMainEnd(line.Items[i].MarginIsAuto, constants.Dir))
                        {
                            if (constants.IsRow)
                                line.Items[i].Margin.Right = marginVal;
                            else
                                line.Items[i].Margin.Bottom = marginVal;
                        }
                    }
                }
                else
                {
                    var numItems = line.Items.Length;
                    var layoutReverse = constants.Dir.IsReverse();
                    var gap = constants.Gap.Main(constants.Dir);
                    var isSafe = false; // TODO: Implement safe alignment
                    var rawJustifyContentMode = constants.JustifyContentStyle ?? AlignContent.FlexStart;
                    var justifyContentMode =
                        AlignmentUtils.ApplyAlignmentFallback(freeSpace, numItems, rawJustifyContentMode, isSafe);

                    if (layoutReverse)
                    {
                        int enumIdx = 0;
                        for (int i = line.Items.Length - 1; i >= 0; i--)
                        {
                            line.Items[i].OffsetMain = AlignmentUtils.ComputeAlignmentOffset(
                                freeSpace, numItems, gap, justifyContentMode, layoutReverse, enumIdx == 0);
                            enumIdx++;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < line.Items.Length; i++)
                        {
                            line.Items[i].OffsetMain = AlignmentUtils.ComputeAlignmentOffset(
                                freeSpace, numItems, gap, justifyContentMode, layoutReverse, i == 0);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Resolve cross-axis auto margins.
        ///
        /// # [9.6. Cross-Axis Alignment](https://www.w3.org/TR/css-flexbox-1/#cross-alignment)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ResolveCrossAxisAutoMargins(
            List<FlexLine> flexLines,
            ref AlgoConstants constants)
        {
            foreach (var line in flexLines)
            {
                var lineCrossSize = line.CrossSize;
                float maxBaseline = 0f;
                for (int i = 0; i < line.Items.Length; i++)
                    maxBaseline = MathF.Max(maxBaseline, line.Items[i].Baseline);

                for (int i = 0; i < line.Items.Length; i++)
                {
                    ref var child = ref line.Items[i];
                    var freeSpace = lineCrossSize - child.OuterTargetSize.Cross(constants.Dir);

                    if (RectBoolCrossStart(child.MarginIsAuto, constants.Dir) && RectBoolCrossEnd(child.MarginIsAuto, constants.Dir))
                    {
                        if (constants.IsRow)
                        {
                            child.Margin.Top = freeSpace / 2f;
                            child.Margin.Bottom = freeSpace / 2f;
                        }
                        else
                        {
                            child.Margin.Left = freeSpace / 2f;
                            child.Margin.Right = freeSpace / 2f;
                        }
                    }
                    else if (RectBoolCrossStart(child.MarginIsAuto, constants.Dir))
                    {
                        if (constants.IsRow) child.Margin.Top = freeSpace;
                        else child.Margin.Left = freeSpace;
                    }
                    else if (RectBoolCrossEnd(child.MarginIsAuto, constants.Dir))
                    {
                        if (constants.IsRow) child.Margin.Bottom = freeSpace;
                        else child.Margin.Right = freeSpace;
                    }
                    else
                    {
                        // 14. Align all flex items along the cross-axis.
                        child.OffsetCross = AlignFlexItemsAlongCrossAxis(ref child, freeSpace, maxBaseline, ref constants);
                    }
                }
            }
        }

        /// <summary>
        /// Align all flex items along the cross-axis.
        ///
        /// # [9.6. Cross-Axis Alignment](https://www.w3.org/TR/css-flexbox-1/#cross-alignment)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float AlignFlexItemsAlongCrossAxis(
            ref FlexItem child,
            float freeSpace,
            float maxBaseline,
            ref AlgoConstants constants)
        {
            return child.AlignSelf switch
            {
                AlignItems.Start => 0f,
                AlignItems.FlexStart => constants.IsWrapReverse ? freeSpace : 0f,
                AlignItems.End => freeSpace,
                AlignItems.FlexEnd => constants.IsWrapReverse ? 0f : freeSpace,
                AlignItems.Center => freeSpace / 2f,
                AlignItems.Baseline => constants.IsRow
                    ? maxBaseline - child.Baseline
                    : (constants.IsWrapReverse ? freeSpace : 0f),
                AlignItems.Stretch => constants.IsWrapReverse ? freeSpace : 0f,
                _ => 0f,
            };
        }

        /// <summary>
        /// Determine the flex container's used cross size.
        ///
        /// # [9.6. Cross-Axis Alignment](https://www.w3.org/TR/css-flexbox-1/#cross-alignment)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DetermineContainerCrossSize(
            List<FlexLine> flexLines,
            Size<float?> nodeSize,
            ref AlgoConstants constants)
        {
            var totalCrossAxisGap = SumAxisGaps(constants.Gap.Cross(constants.Dir), flexLines.Count);
            float totalLineCrossSize = 0f;
            foreach (var line in flexLines)
                totalLineCrossSize += line.CrossSize;

            var paddingBorderSum = constants.ContentBoxInset.CrossAxisSum(constants.Dir);
            var crossScrollbarGutter = constants.ScrollbarGutter.Cross(constants.Dir);
            var minCrossSize = constants.MinSize.Cross(constants.Dir);
            var maxCrossSize = constants.MaxSize.Cross(constants.Dir);
            var outerContainerSize = (nodeSize
                .Cross(constants.Dir)
                ?? (totalLineCrossSize + totalCrossAxisGap + paddingBorderSum))
                .MaybeClamp(minCrossSize, maxCrossSize);
            outerContainerSize = MathF.Max(outerContainerSize, paddingBorderSum - crossScrollbarGutter);
            var innerContainerSize = MathF.Max(outerContainerSize - paddingBorderSum, 0f);

            constants.ContainerSize.SetCross(constants.Dir, outerContainerSize);
            constants.InnerContainerSize.SetCross(constants.Dir, innerContainerSize);

            return totalLineCrossSize;
        }

        /// <summary>
        /// Align all flex lines per align-content.
        ///
        /// # [9.6. Cross-Axis Alignment](https://www.w3.org/TR/css-flexbox-1/#cross-alignment)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AlignFlexLinesPerAlignContent(
            List<FlexLine> flexLines,
            ref AlgoConstants constants,
            float totalCrossSize)
        {
            var numLines = flexLines.Count;
            var gap = constants.Gap.Cross(constants.Dir);
            var totalCrossAxisGap = SumAxisGaps(gap, numLines);
            var freeSpace = constants.InnerContainerSize.Cross(constants.Dir) - totalCrossSize - totalCrossAxisGap;
            var isSafe = false; // TODO: Implement safe alignment

            var alignContentMode = AlignmentUtils.ApplyAlignmentFallback(freeSpace, numLines, constants.AlignContentStyle, isSafe);

            if (constants.IsWrapReverse)
            {
                int enumIdx = 0;
                for (int i = flexLines.Count - 1; i >= 0; i--)
                {
                    flexLines[i].OffsetCross = AlignmentUtils.ComputeAlignmentOffset(
                        freeSpace, numLines, gap, alignContentMode, constants.IsWrapReverse, enumIdx == 0);
                    enumIdx++;
                }
            }
            else
            {
                for (int i = 0; i < flexLines.Count; i++)
                {
                    flexLines[i].OffsetCross = AlignmentUtils.ComputeAlignmentOffset(
                        freeSpace, numLines, gap, alignContentMode, constants.IsWrapReverse, i == 0);
                }
            }
        }

        /// <summary>
        /// Calculates the layout for a flex-item
        /// </summary>
        private static void CalculateFlexItem(
            ILayoutFlexboxContainer tree,
            ref FlexItem item,
            ref float totalOffsetMain,
            float totalOffsetCross,
            float lineOffsetCross,
            ref Size<float> totalContentSize,
            Size<float> containerSize,
            Size<float?> nodeInnerSize,
            FlexDirection direction)
        {
            var layoutOutput = tree.PerformChildLayout(
                item.Node,
                item.TargetSize.Map(s => (float?)s),
                nodeInnerSize,
                containerSize.Map(s => (AvailableSpace)s),
                SizingMode.ContentSize,
                LineExtensions.FalseLine);

            var size = layoutOutput.Size;
            var contentSize = layoutOutput.ContentSize;

            var insetMainStart = item.Inset.MainStart(direction);
            var insetMainEnd = item.Inset.MainEnd(direction);
            var mainInsetOffset = insetMainStart ?? (insetMainEnd.HasValue ? -insetMainEnd.Value : 0f);

            var offsetMain = totalOffsetMain
                + item.OffsetMain
                + item.Margin.MainStart(direction)
                + mainInsetOffset;

            var insetCrossStart = item.Inset.CrossStart(direction);
            var insetCrossEnd = item.Inset.CrossEnd(direction);
            var crossInsetOffset = insetCrossStart ?? (insetCrossEnd.HasValue ? -insetCrossEnd.Value : 0f);

            var offsetCross = totalOffsetCross
                + item.OffsetCross
                + lineOffsetCross
                + item.Margin.CrossStart(direction)
                + crossInsetOffset;

            if (direction.IsRow())
            {
                var baselineOffsetCross = totalOffsetCross + item.OffsetCross + item.Margin.CrossStart(direction);
                var innerBaseline = layoutOutput.FirstBaselines.Y ?? size.Height;
                item.Baseline = baselineOffsetCross + innerBaseline;
            }
            else
            {
                var baselineOffsetMain = totalOffsetMain + item.OffsetMain + item.Margin.MainStart(direction);
                var innerBaseline = layoutOutput.FirstBaselines.Y ?? size.Height;
                item.Baseline = baselineOffsetMain + innerBaseline;
            }

            var location = direction.IsRow()
                ? new Point<float>(offsetMain, offsetCross)
                : new Point<float>(offsetCross, offsetMain);

            var scrollbarSize = new Size<float>(
                item.OverflowStyle.Y == Overflow.Scroll ? item.ScrollbarWidth : 0f,
                item.OverflowStyle.X == Overflow.Scroll ? item.ScrollbarWidth : 0f);

            tree.SetUnroundedLayout(
                item.Node,
                new Layout
                {
                    Order = item.Order,
                    Size = size,
                    ContentSize = contentSize,
                    ScrollbarSize = scrollbarSize,
                    Location = location,
                    Padding = item.Padding,
                    Border = item.Border,
                    Margin = item.Margin,
                });

            totalOffsetMain += item.OffsetMain + item.Margin.MainAxisSum(direction) + size.Main(direction);

            totalContentSize = totalContentSize.F32Max(
                ContentSizeUtils.ComputeContentSizeContribution(location, size, contentSize, item.OverflowStyle));
        }

        /// <summary>
        /// Calculates the layout line
        /// </summary>
        private static void CalculateLayoutLine(
            ILayoutFlexboxContainer tree,
            FlexLine line,
            ref float totalOffsetCross,
            ref Size<float> contentSize,
            Size<float> containerSize,
            Size<float?> nodeInnerSize,
            Rect<float> paddingBorder,
            FlexDirection direction)
        {
            float totalOffsetMain = paddingBorder.MainStart(direction);
            var lineOffsetCross = line.OffsetCross;

            if (direction.IsReverse())
            {
                for (int i = line.Items.Length - 1; i >= 0; i--)
                {
                    CalculateFlexItem(
                        tree,
                        ref line.Items[i],
                        ref totalOffsetMain,
                        totalOffsetCross,
                        lineOffsetCross,
                        ref contentSize,
                        containerSize,
                        nodeInnerSize,
                        direction);
                }
            }
            else
            {
                for (int i = 0; i < line.Items.Length; i++)
                {
                    CalculateFlexItem(
                        tree,
                        ref line.Items[i],
                        ref totalOffsetMain,
                        totalOffsetCross,
                        lineOffsetCross,
                        ref contentSize,
                        containerSize,
                        nodeInnerSize,
                        direction);
                }
            }

            totalOffsetCross += lineOffsetCross + line.CrossSize;
        }

        /// <summary>
        /// Do a final layout pass and collect the resulting layouts.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Size<float> FinalLayoutPass(
            ILayoutFlexboxContainer tree,
            List<FlexLine> flexLines,
            ref AlgoConstants constants)
        {
            float totalOffsetCross = constants.ContentBoxInset.CrossStart(constants.Dir);
            var contentSize = SizeExtensions.ZeroF32;

            if (constants.IsWrapReverse)
            {
                for (int i = flexLines.Count - 1; i >= 0; i--)
                {
                    CalculateLayoutLine(
                        tree,
                        flexLines[i],
                        ref totalOffsetCross,
                        ref contentSize,
                        constants.ContainerSize,
                        constants.NodeInnerSize,
                        constants.ContentBoxInset,
                        constants.Dir);
                }
            }
            else
            {
                for (int i = 0; i < flexLines.Count; i++)
                {
                    CalculateLayoutLine(
                        tree,
                        flexLines[i],
                        ref totalOffsetCross,
                        ref contentSize,
                        constants.ContainerSize,
                        constants.NodeInnerSize,
                        constants.ContentBoxInset,
                        constants.Dir);
                }
            }

            contentSize.Width += constants.ContentBoxInset.Right - constants.Border.Right - constants.ScrollbarGutter.X;
            contentSize.Height += constants.ContentBoxInset.Bottom - constants.Border.Bottom - constants.ScrollbarGutter.Y;

            return contentSize;
        }

        /// <summary>
        /// Perform absolute layout on all absolutely positioned children.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Size<float> PerformAbsoluteLayoutOnAbsoluteChildren(
            ILayoutFlexboxContainer tree,
            NodeId node,
            ref AlgoConstants constants)
        {
            var containerWidth = constants.ContainerSize.Width;
            var containerHeight = constants.ContainerSize.Height;
            var insetRelativeSize = constants.ContainerSize
                .Sub(constants.Border.SumAxes())
                .Sub(new Size<float>(constants.ScrollbarGutter.X, constants.ScrollbarGutter.Y));

            var contentSize = SizeExtensions.ZeroF32;

            for (int order = 0; order < tree.ChildCount(node); order++)
            {
                var child = tree.GetChildId(node, order);
                var childStyle = tree.GetFlexboxChildStyle(child);

                // Skip items that are display:none or are not position:absolute
                if (childStyle.BoxGenerationMode() == BoxGenerationMode.None || childStyle.Position() != Position.Absolute)
                    continue;

                var overflow = childStyle.Overflow();
                var scrollbarWidth = childStyle.ScrollbarWidth();
                var aspectRatio = childStyle.AspectRatio();
                var alignSelf = childStyle.AlignSelf() ?? constants.AlignItemsStyle;
                var margin = childStyle
                    .Margin()
                    .Map(m => m.ResolveToOption(insetRelativeSize.Width, tree.Calc));
                var padding = childStyle.Padding().ResolveOrZero((float?)insetRelativeSize.Width, tree.Calc);
                var border = childStyle.Border().ResolveOrZero((float?)insetRelativeSize.Width, tree.Calc);
                var paddingBorderSum = padding.SumAxes().Add(border.SumAxes());
                var boxSizingAdj = childStyle.BoxSizing() == BoxSizing.ContentBox
                    ? paddingBorderSum
                    : SizeExtensions.ZeroF32;

                // Resolve inset
                var left = childStyle.Inset().Left.MaybeResolve(insetRelativeSize.Width, tree.Calc);
                var right = childStyle.Inset().Right.MaybeResolve(insetRelativeSize.Width, tree.Calc);
                var top = childStyle.Inset().Top.MaybeResolve(insetRelativeSize.Height, tree.Calc);
                var bottom = childStyle.Inset().Bottom.MaybeResolve(insetRelativeSize.Height, tree.Calc);

                // Compute known dimensions from min/max/inherent size styles
                var insetRelativeSizeOpt = new Size<float?>((float?)insetRelativeSize.Width, (float?)insetRelativeSize.Height);
                var styleSize = childStyle
                    .Size()
                    .MaybeResolve(insetRelativeSizeOpt, tree.Calc)
                    .MaybeApplyAspectRatio(aspectRatio)
                    .MaybeAdd(boxSizingAdj.Map(v => (float?)v));
                var minSizeAbs = childStyle
                    .MinSize()
                    .MaybeResolve(insetRelativeSizeOpt, tree.Calc)
                    .MaybeApplyAspectRatio(aspectRatio)
                    .MaybeAdd(boxSizingAdj.Map(v => (float?)v))
                    .Or(paddingBorderSum.Map(v => (float?)v))
                    .MaybeMax(paddingBorderSum.Map(v => (float?)v));
                var maxSizeAbs = childStyle
                    .MaxSize()
                    .MaybeResolve(insetRelativeSizeOpt, tree.Calc)
                    .MaybeApplyAspectRatio(aspectRatio)
                    .MaybeAdd(boxSizingAdj.Map(v => (float?)v));
                var knownDimensions = styleSize.MaybeClamp(minSizeAbs, maxSizeAbs);

                // Fill in width from left/right and reapply aspect ratio
                if (!knownDimensions.Width.HasValue && left.HasValue && right.HasValue)
                {
                    var newWidthRaw = insetRelativeSize.Width.MaybeSub(margin.Left).MaybeSub(margin.Right) - left.Value - right.Value;
                    knownDimensions.Width = MathF.Max(newWidthRaw, 0f);
                    knownDimensions = knownDimensions.MaybeApplyAspectRatio(aspectRatio).MaybeClamp(minSizeAbs, maxSizeAbs);
                }

                // Fill in height from top/bottom and reapply aspect ratio
                if (!knownDimensions.Height.HasValue && top.HasValue && bottom.HasValue)
                {
                    var newHeightRaw = insetRelativeSize.Height.MaybeSub(margin.Top).MaybeSub(margin.Bottom) - top.Value - bottom.Value;
                    knownDimensions.Height = MathF.Max(newHeightRaw, 0f);
                    knownDimensions = knownDimensions.MaybeApplyAspectRatio(aspectRatio).MaybeClamp(minSizeAbs, maxSizeAbs);
                }

                var measuredSize = tree.MeasureChildSizeBoth(
                    child,
                    knownDimensions,
                    constants.NodeInnerSize,
                    new Size<AvailableSpace>(
                        AvailableSpace.Definite(containerWidth.MaybeClamp(minSizeAbs.Width, maxSizeAbs.Width)),
                        AvailableSpace.Definite(containerHeight.MaybeClamp(minSizeAbs.Height, maxSizeAbs.Height))),
                    SizingMode.InherentSize,
                    LineExtensions.FalseLine);

                var finalSize = knownDimensions.UnwrapOr(measuredSize).MaybeClamp(minSizeAbs, maxSizeAbs);

                var layoutOutput = tree.PerformChildLayout(
                    child,
                    finalSize.Map(v => (float?)v),
                    constants.NodeInnerSize,
                    new Size<AvailableSpace>(
                        AvailableSpace.Definite(containerWidth.MaybeClamp(minSizeAbs.Width, maxSizeAbs.Width)),
                        AvailableSpace.Definite(containerHeight.MaybeClamp(minSizeAbs.Height, maxSizeAbs.Height))),
                    SizingMode.InherentSize,
                    LineExtensions.FalseLine);

                var nonAutoMargin = margin.Map(m => m ?? 0f);

                var absFreeSpace = new Size<float>(
                    MathF.Max(constants.ContainerSize.Width - finalSize.Width - nonAutoMargin.HorizontalAxisSum(), 0f),
                    MathF.Max(constants.ContainerSize.Height - finalSize.Height - nonAutoMargin.VerticalAxisSum(), 0f));

                // Expand auto margins to fill available space
                float autoMarginSizeWidth;
                {
                    int autoMarginCount = (margin.Left.HasValue ? 0 : 1) + (margin.Right.HasValue ? 0 : 1);
                    autoMarginSizeWidth = autoMarginCount > 0 ? absFreeSpace.Width / autoMarginCount : 0f;
                }
                float autoMarginSizeHeight;
                {
                    int autoMarginCount = (margin.Top.HasValue ? 0 : 1) + (margin.Bottom.HasValue ? 0 : 1);
                    autoMarginSizeHeight = autoMarginCount > 0 ? absFreeSpace.Height / autoMarginCount : 0f;
                }

                var resolvedMargin = new Rect<float>(
                    margin.Left ?? autoMarginSizeWidth,
                    margin.Right ?? autoMarginSizeWidth,
                    margin.Top ?? autoMarginSizeHeight,
                    margin.Bottom ?? autoMarginSizeHeight);

                // Determine flex-relative insets
                var (startMain, endMain) = constants.IsRow ? (left, right) : (top, bottom);
                var (startCross, endCross) = constants.IsRow ? (top, bottom) : (left, right);

                // Apply main-axis alignment
                float offsetMain;
                if (startMain.HasValue)
                {
                    offsetMain = startMain.Value + constants.Border.MainStart(constants.Dir) + resolvedMargin.MainStart(constants.Dir);
                }
                else if (endMain.HasValue)
                {
                    offsetMain = constants.ContainerSize.Main(constants.Dir)
                        - constants.Border.MainEnd(constants.Dir)
                        - constants.ScrollbarGutter.Main(constants.Dir)
                        - finalSize.Main(constants.Dir)
                        - endMain.Value
                        - resolvedMargin.MainEnd(constants.Dir);
                }
                else
                {
                    var jc = constants.JustifyContentStyle ?? AlignContent.Start;
                    offsetMain = (jc, constants.IsWrapReverse) switch
                    {
                        (AlignContent.SpaceBetween, _) or
                        (AlignContent.Start, _) or
                        (AlignContent.Stretch, false) or
                        (AlignContent.FlexStart, false) or
                        (AlignContent.FlexEnd, true) =>
                            constants.ContentBoxInset.MainStart(constants.Dir) + resolvedMargin.MainStart(constants.Dir),

                        (AlignContent.End, _) or
                        (AlignContent.FlexEnd, false) or
                        (AlignContent.FlexStart, true) or
                        (AlignContent.Stretch, true) =>
                            constants.ContainerSize.Main(constants.Dir)
                            - constants.ContentBoxInset.MainEnd(constants.Dir)
                            - finalSize.Main(constants.Dir)
                            - resolvedMargin.MainEnd(constants.Dir),

                        (AlignContent.SpaceEvenly, _) or
                        (AlignContent.SpaceAround, _) or
                        (AlignContent.Center, _) =>
                            (constants.ContainerSize.Main(constants.Dir)
                             + constants.ContentBoxInset.MainStart(constants.Dir)
                             - constants.ContentBoxInset.MainEnd(constants.Dir)
                             - finalSize.Main(constants.Dir)
                             + resolvedMargin.MainStart(constants.Dir)
                             - resolvedMargin.MainEnd(constants.Dir))
                            / 2f,

                        _ => constants.ContentBoxInset.MainStart(constants.Dir) + resolvedMargin.MainStart(constants.Dir),
                    };
                }

                // Apply cross-axis alignment
                float offsetCrossAbs;
                if (startCross.HasValue)
                {
                    offsetCrossAbs = startCross.Value + constants.Border.CrossStart(constants.Dir) + resolvedMargin.CrossStart(constants.Dir);
                }
                else if (endCross.HasValue)
                {
                    offsetCrossAbs = constants.ContainerSize.Cross(constants.Dir)
                        - constants.Border.CrossEnd(constants.Dir)
                        - constants.ScrollbarGutter.Cross(constants.Dir)
                        - finalSize.Cross(constants.Dir)
                        - endCross.Value
                        - resolvedMargin.CrossEnd(constants.Dir);
                }
                else
                {
                    offsetCrossAbs = (alignSelf, constants.IsWrapReverse) switch
                    {
                        // Stretch alignment does not apply to absolutely positioned items
                        (AlignItems.Start, _) or
                        (AlignItems.Baseline or AlignItems.Stretch or AlignItems.FlexStart, false) or
                        (AlignItems.FlexEnd, true) =>
                            constants.ContentBoxInset.CrossStart(constants.Dir) + resolvedMargin.CrossStart(constants.Dir),

                        (AlignItems.End, _) or
                        (AlignItems.Baseline or AlignItems.Stretch or AlignItems.FlexStart, true) or
                        (AlignItems.FlexEnd, false) =>
                            constants.ContainerSize.Cross(constants.Dir)
                            - constants.ContentBoxInset.CrossEnd(constants.Dir)
                            - finalSize.Cross(constants.Dir)
                            - resolvedMargin.CrossEnd(constants.Dir),

                        (AlignItems.Center, _) =>
                            (constants.ContainerSize.Cross(constants.Dir)
                             + constants.ContentBoxInset.CrossStart(constants.Dir)
                             - constants.ContentBoxInset.CrossEnd(constants.Dir)
                             - finalSize.Cross(constants.Dir)
                             + resolvedMargin.CrossStart(constants.Dir)
                             - resolvedMargin.CrossEnd(constants.Dir))
                            / 2f,

                        _ => constants.ContentBoxInset.CrossStart(constants.Dir) + resolvedMargin.CrossStart(constants.Dir),
                    };
                }

                var location = constants.IsRow
                    ? new Point<float>(offsetMain, offsetCrossAbs)
                    : new Point<float>(offsetCrossAbs, offsetMain);

                var scrollbarSize = new Size<float>(
                    overflow.Y == Overflow.Scroll ? scrollbarWidth : 0f,
                    overflow.X == Overflow.Scroll ? scrollbarWidth : 0f);

                tree.SetUnroundedLayout(
                    child,
                    new Layout
                    {
                        Order = (uint)order,
                        Size = finalSize,
                        ContentSize = layoutOutput.ContentSize,
                        ScrollbarSize = scrollbarSize,
                        Location = location,
                        Padding = padding,
                        Border = border,
                        Margin = resolvedMargin,
                    });

                var sizeContentSizeContribution = new Size<float>(
                    overflow.X == Overflow.Visible ? MathF.Max(finalSize.Width, layoutOutput.ContentSize.Width) : finalSize.Width,
                    overflow.Y == Overflow.Visible ? MathF.Max(finalSize.Height, layoutOutput.ContentSize.Height) : finalSize.Height);

                if (sizeContentSizeContribution.HasNonZeroArea())
                {
                    var contentSizeContribution = new Size<float>(
                        location.X + sizeContentSizeContribution.Width,
                        location.Y + sizeContentSizeContribution.Height);
                    contentSize = contentSize.F32Max(contentSizeContribution);
                }
            }

            return contentSize;
        }

        /// <summary>
        /// Computes the total space taken up by gaps in an axis given:
        ///   - The size of each gap
        ///   - The number of items (children or flex-lines) between which there are gaps
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SumAxisGaps(float gap, int numItems)
        {
            // Gaps only exist between items, so...
            if (numItems <= 1)
            {
                // ...if there are less than 2 items then there are no gaps
                return 0f;
            }
            // ...otherwise there are (num_items - 1) gaps
            return gap * (numItems - 1);
        }

        // --- Rect<bool> accessor helpers (since we don't have typed extension methods for Rect<bool>) ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RectBoolMainStart(Rect<bool> rect, FlexDirection direction)
        {
            return direction.IsRow() ? rect.Left : rect.Top;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RectBoolMainEnd(Rect<bool> rect, FlexDirection direction)
        {
            return direction.IsRow() ? rect.Right : rect.Bottom;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RectBoolCrossStart(Rect<bool> rect, FlexDirection direction)
        {
            return direction.IsRow() ? rect.Top : rect.Left;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RectBoolCrossEnd(Rect<bool> rect, FlexDirection direction)
        {
            return direction.IsRow() ? rect.Bottom : rect.Right;
        }

        /// <summary>
        /// Returns true if the float is normal (not zero, not infinity, not NaN)
        /// Equivalent to Rust's f32::is_normal()
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsNormal(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value != 0f && MathF.Abs(value) >= float.MinValue;
        }
    }

    /// <summary>
    /// Extension methods for nullable float needed by flexbox algorithm
    /// </summary>
    internal static class NullableFloatFlexExtensions
    {
        /// <summary>
        /// Returns self if HasValue, else calls the callback.
        /// Equivalent to Rust's Option::or_else
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? OrElse(this float? self, Func<float?> defaultCb)
        {
            return self ?? defaultCb();
        }

        /// <summary>
        /// Returns self if condition is true, else null.
        /// Equivalent to Rust's Option::filter
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? Filter(this float? self, Func<float, bool> predicate)
        {
            if (self.HasValue && predicate(self.Value))
                return self;
            return null;
        }

    }
}
