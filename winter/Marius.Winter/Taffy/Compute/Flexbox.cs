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
        public OofCandidates OofCandidates;
        public Contain Contain;

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
        public bool FlexBasisIsDefinite;
        public float? AspectRatio;
        public Size<Dimension> SizeStyle;
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

        public readonly bool IsStretched(FlexDirection dir) => !(dir.IsRow() ? MarginIsAuto.Top || MarginIsAuto.Bottom : MarginIsAuto.Left || MarginIsAuto.Right)
            && (SizeStyle.Cross(dir).IsStretch() || (AlignSelf == AlignItems.Stretch && SizeStyle.Cross(dir).IsAuto()));

        public readonly bool ParticipatesInBaselineAlignment(FlexDirection direction) =>
            AlignSelf == AlignItems.Baseline && (direction.IsRow()
                ? !MarginIsAuto.Top && !MarginIsAuto.Bottom
                : !MarginIsAuto.Left && !MarginIsAuto.Right);
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
        /// The layout direction of the current segment being laid out
        public Direction LayoutDirection;
        /// Is this segment a row
        public bool IsRow;
        /// Is this segment a column
        public bool IsColumn;
        /// Is wrapping enabled (in either direction)
        public bool IsWrap;
        /// Is the wrap direction inverted
        public bool IsWrapReverse;
        public bool IsBalance, KnownMainSizeIsDefinite, HasDefiniteMainSize, HasDefiniteCrossSize, CrossAvailableSpaceIsDefinite;
        public ushort LineCount;
        public readonly float DividedCrossSpace(float space) => IsWrap && LineCount > 1 ? (space - (LineCount - 1) * Gap.Cross(Dir)) / LineCount : space;

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
        public bool IsScrollContainer;
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
    public static partial class FlexboxAlgorithm
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

                // We can also short-circuit if the width is known and only the width has been requested.
                if (inputs.Axis == RequestedAxis.Horizontal && styledBasedKnownDimensions.Width.HasValue)
                {
                    return LayoutOutput.FromOuterSize(new Size<float>(styledBasedKnownDimensions.Width.Value, 0f));
                }
            }

            var output = ComputePreliminary(tree, node, new LayoutInput
            {
                KnownDimensions = styledBasedKnownDimensions,
                KnownDimensionsAreDefinite = new(inputs.KnownDimensionsAreDefinite.Width || !knownDimensions.Width.HasValue,
                    inputs.KnownDimensionsAreDefinite.Height || !knownDimensions.Height.HasValue),
                ParentSize = inputs.ParentSize,
                AvailableSpace = inputs.AvailableSpace,
                RunMode = inputs.RunMode,
                SizingMode = inputs.SizingMode,
                Axis = inputs.Axis,
                VerticalMarginsAreCollapsible = inputs.VerticalMarginsAreCollapsible,
            });
            if (style.Contain().SuppressesBaseline()) output.Baselines = Baselines.None;
            return output;
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
            var constants = ComputeConstants(tree, tree.GetFlexboxContainerStyle(node), knownDimensions, parentSize, inputs.KnownDimensionsAreDefinite, inputs.AvailableSpace);
            if (tree.ChildCount(node) == 0) return ComputeEmptyContainerLayout(tree, inputs, ref constants);

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
                DetermineContainerMainSize(tree, availableSpace, ref flexLines, ref constants);
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

            if (runMode == RunMode.ComputeSize && inputs.Axis == constants.Dir.MainAxis().ToRequestedAxis())
            {
                var measured = SizeExtensions.ZeroF32; measured.SetMain(constants.Dir, constants.ContainerSize.Main(constants.Dir));
                return LayoutOutput.FromOuterSize(measured);
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
            CalculateChildrenBaseLines(tree, knownDimensions, availableSpace, ref flexLines, ref constants);

            // 8. Calculate the cross size of each flex line.
            CalculateCrossSize(ref flexLines, knownDimensions, ref constants);

            // 9. Handle 'align-content: stretch'.
            HandleAlignContentStretch(ref flexLines, knownDimensions, ref constants);

            // 10. Collapse visibility:collapse items. If any flex items have visibility: collapse, ...
            // TODO implement once (if ever) we support visibility:collapse

            // 11. Determine the used cross size of each flex item.
            DetermineUsedCrossSize(tree, ref flexLines, ref constants);

            // 9.5. Main-Axis Alignment

            // 12. Distribute any remaining free space.
            DistributeRemainingFreeSpace(ref flexLines, ref constants);

            // 9.6. Cross-Axis Alignment

            // 13. Resolve cross-axis auto margins (also includes 14).
            ResolveCrossAxisAutoMargins(ref flexLines, ref constants);

            // 15. Determine the flex container's used cross size.
            var totalLineCrossSize = DetermineContainerCrossSize(ref flexLines, knownDimensions, ref constants);

            // We have the container size.
            // If our caller does not care about performing layout we are done now.
            if (runMode == RunMode.ComputeSize)
            {
                return LayoutOutput.FromOuterSize(constants.ContainerSize);
            }

            // 16. Align all flex lines per align-content.
            AlignFlexLinesPerAlignContent(ref flexLines, ref constants, totalLineCrossSize);

            // Do a final layout pass and gather the resulting layouts
            var inflowContentSize = FinalLayoutPass(tree, ref flexLines, ref constants);

            // Before returning we perform absolute layout on all absolutely positioned children
            var candidates = CollectOofCandidates(tree, node, ref constants, ref flexLines);

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
                var firstLineItems = flexLines[constants.IsWrapReverse ? flexLines.Count - 1 : 0].Items;
                FlexItem? baselineChild = null;

                if (constants.IsColumn && firstLineItems.Length > 0)
                {
                    baselineChild = firstLineItems[constants.Dir.IsReverse() ? firstLineItems.Length - 1 : 0];
                }
                else
                {
                    foreach (var item in firstLineItems)
                    {
                        if (item.ParticipatesInBaselineAlignment(constants.Dir))
                        {
                            baselineChild = item;
                            break;
                        }
                    }
                }
                if (!baselineChild.HasValue && firstLineItems.Length > 0)
                {
                    baselineChild = firstLineItems[0];
                }

                if (baselineChild.HasValue)
                {
                    var child = baselineChild.Value;
                    firstVerticalBaseline = child.Baseline;
                }
                else
                {
                    firstVerticalBaseline = null;
                }
            }

            var output = LayoutOutput.FromSizesAndBaselines(constants.ContainerSize, inflowContentSize, Baselines.FromFirst(firstVerticalBaseline));
            output.OofCandidates = candidates;
            var absoluteInset = constants.Border.Add(new Rect<float>(constants.LayoutDirection.IsRtl() ? constants.ScrollbarGutter.X : 0,
                constants.LayoutDirection.IsRtl() ? 0 : constants.ScrollbarGutter.X, 0, constants.ScrollbarGutter.Y));
            output.OofPositioningArea = new(constants.ContainerSize.Sub(absoluteInset.SumAxes()), new(absoluteInset.Left, absoluteInset.Top));
            return output;
        }

        /// <summary>
        /// Compute constants that can be reused during the flexbox algorithm.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AlgoConstants ComputeConstants(
            ILayoutFlexboxContainer tree,
            IFlexboxContainerStyle style,
            Size<float?> knownDimensions,
            Size<float?> parentSize, DefiniteDimensions definite, Size<AvailableSpace> availableSpace)
        {
            var dir = style.FlexDirection();
            var isRow = dir.IsRow();
            var isColumn = dir.IsColumn();
            var isWrap = style.FlexWrap().IsMultiLine();
            var isWrapReverse = style.FlexWrap().IsReverse();

            var aspectRatio = style.AspectRatio();
            var margin = style.Margin().ResolveOrZero(parentSize.Width, tree.Calc);
            var padding = style.Padding().ResolveOrZero(parentSize.Width, tree.Calc);
            var border = style.Border().ResolveOrZero(parentSize.Width, tree.Calc);
            var paddingBorderSum = padding.SumAxes().Add(border.SumAxes());
            var boxSizingAdjustment =
                style.BoxSizing() == BoxSizing.ContentBox ? paddingBorderSum : SizeExtensions.ZeroF32;

            var alignItems = ResolveNormal(style.AlignItems() ?? AlignItems.Normal);
            var alignContent = style.AlignContent() ?? AlignContent.Normal;
            if (alignContent.Keyword == AlignContentKeyword.Normal) alignContent = AlignContent.Stretch;
            var justifyContent = style.JustifyContent() ?? AlignContent.Normal;
            if (justifyContent.Keyword == AlignContentKeyword.Normal) justifyContent = AlignContent.FlexStart;
            var layoutDirection = style.Direction();

            // Scrollbar gutters are reserved when the `overflow` property is set to `Overflow::Scroll`.
            // However, the axes are switched (transposed) because a node that scrolls vertically needs
            // *horizontal* space to be reserved for a scrollbar
            var overflow = style.Overflow().Transpose();
            var scrollbarGutter = new Point<float>(
                overflow.X == Overflow.Scroll ? style.ScrollbarWidth() : 0f,
                overflow.Y == Overflow.Scroll ? style.ScrollbarWidth() : 0f);
            var contentBoxInset = padding.Add(border);
            contentBoxInset.Bottom += scrollbarGutter.Y;

            switch (layoutDirection)
            {
                case Direction.Ltr:
                    contentBoxInset.Right += scrollbarGutter.X;
                    break;
                case Direction.Rtl:
                    contentBoxInset.Left += scrollbarGutter.X;
                    break;
            }

            var nodeOuterSize = knownDimensions;
            var nodeInnerSize = nodeOuterSize.MaybeSub(contentBoxInset.SumAxes().Map(v => (float?)v));
            var gap = style.Gap().ResolveOrZero(nodeInnerSize.Or(new Size<float?>(0f, 0f)), tree.Calc);

            var containerSize = SizeExtensions.ZeroF32;
            var innerContainerSize = SizeExtensions.ZeroF32;

            return new AlgoConstants
            {
                Dir = dir,
                LayoutDirection = layoutDirection,
                IsRow = isRow,
                IsColumn = isColumn,
                IsWrap = isWrap,
                IsWrapReverse = isWrapReverse,
                IsBalance = style.FlexWrap().IsBalance(), LineCount = System.Math.Max(style.FlexLineCount(), (ushort)1),
                KnownMainSizeIsDefinite = ((Size<bool>)definite).Main(dir),
                HasDefiniteMainSize = ((Size<bool>)definite).Main(dir) && knownDimensions.Main(dir).HasValue,
                HasDefiniteCrossSize = ((Size<bool>)definite).Cross(dir) && knownDimensions.Cross(dir).HasValue,
                CrossAvailableSpaceIsDefinite = (((Size<bool>)definite).Cross(dir) && knownDimensions.Cross(dir).HasValue) || availableSpace.Cross(dir).IsDefinite(),
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
                IsScrollContainer = style.Overflow().X.IsScrollContainer() || style.Overflow().Y.IsScrollContainer(),
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
            var items = new List<FlexItem>(tree.ChildCount(node));
            var percentSize = constants.KnownMainSizeIsDefinite ? constants.NodeInnerSize : constants.NodeInnerSize.WithMain(constants.Dir, null);
            int index = 0;
            foreach (var child in tree.ChildIds(node))
            {
                var childStyle = tree.GetFlexboxChildStyle(child);
                if (childStyle.Position().IsOutOfFlow() ||
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
                        .MaybeResolve(percentSize, tree.Calc)
                        .MaybeApplyAspectRatio(aspectRatio)
                        .MaybeAdd(childBoxSizingAdjOpt),
                    MinSize = childStyle.MinSize().MaybeResolve(percentSize, tree.Calc)
                        .MaybeAdd(childBoxSizingAdjOpt),
                    MaxSize = childStyle.MaxSize().MaybeResolve(percentSize, tree.Calc)
                        .MaybeAdd(childBoxSizingAdjOpt),
                    Inset = childStyle.Position() == Position.Relative ? childStyle.Inset()
                        .ZipSize(constants.NodeInnerSize, (p, s) => p.MaybeResolve(s, tree.Calc)) : new(null, null, null, null),
                    Margin = childStyle
                        .Margin()
                        .ResolveOrZero(constants.NodeInnerSize.Width, tree.Calc),
                    MarginIsAuto = childStyle.Margin().Map(m => m.IsAuto()),
                    Padding = childPadding,
                    Border = childBorder,
                    AlignSelf = ResolveNormal(childStyle.AlignSelf() ?? constants.AlignItemsStyle).ResolveSelfRelative(childStyle.Direction(), constants.LayoutDirection, constants.IsColumn),
                    AspectRatio = aspectRatio, SizeStyle = childStyle.Size(),
                    OverflowStyle = childStyle.Overflow(),
                    Contain = childStyle.Contain(),
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
                    .MaybeSub(constants.ContentBoxInset.HorizontalAxisSum());

            var height = knownDimensions.Height.HasValue
                ? AvailableSpace.Definite(knownDimensions.Height.Value - constants.ContentBoxInset.VerticalAxisSum())
                : outerAvailableSpace.Height
                    .MaybeSub(constants.ContentBoxInset.VerticalAxisSum());

            return new Size<AvailableSpace>(width, height);
        }

        /// <summary>
        /// Determine the flex base size and hypothetical main size of each item.
        ///
        /// # [9.2. Line Length Determination](https://www.w3.org/TR/css-flexbox-1/#line-sizing)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DetermineFlexBaseSize(ILayoutFlexboxContainer tree, ref AlgoConstants constants,
            Size<AvailableSpace> availableSpace, FlexItem[] items)
        {
            var dir = constants.Dir;
            for (int i = 0; i < items.Length; i++)
            {
                ref var child = ref items[i];
                var style = tree.GetFlexboxChildStyle(child.Node);
                var crossParent = constants.NodeInnerSize.Cross(dir);
                var parent = SizeExtensions.FromCross(dir, crossParent);
                var transferredMin = child.MinSize.MaybeApplyAspectRatio(child.AspectRatio);
                var transferredMax = child.MaxSize.MaybeApplyAspectRatio(child.AspectRatio);
                var crossSpace = availableSpace.Cross(dir);
                if (crossSpace.IsDefinite()) crossSpace = AvailableSpace.Definite(TaffySys.F32Max(constants.DividedCrossSpace(crossParent ?? crossSpace.Unwrap())
                    - child.Margin.CrossAxisSum(dir), 0).MaybeClamp(transferredMin.Cross(dir), transferredMax.Cross(dir)));
                else if (crossSpace == AvailableSpace.MinContent && transferredMin.Cross(dir) is { } min) crossSpace = AvailableSpace.Definite(min);
                else if (crossSpace == AvailableSpace.MaxContent && transferredMax.Cross(dir) is { } max) crossSpace = AvailableSpace.Definite(max);
                bool crossDefinite = child.Size.Cross(dir).HasValue;
                var known = child.Size.WithMain(dir, null);
                known.SetCross(dir, known.Cross(dir).MaybeClamp(transferredMin.Cross(dir), transferredMax.Cross(dir)));
                if (child.AlignSelf == AlignItems.Stretch && !RectBoolCrossStart(child.MarginIsAuto, dir)
                    && !RectBoolCrossEnd(child.MarginIsAuto, dir) && !known.Cross(dir).HasValue)
                {
                    known.SetCross(dir, crossSpace.IntoOption());
                    crossDefinite = !constants.IsWrap && constants.HasDefiniteCrossSize && crossParent.HasValue;
                }
                float? basisParent = constants.KnownMainSizeIsDefinite ? constants.NodeInnerSize.Main(dir) : null;
                var basisStyle = style.FlexBasis();
                float adjustment = style.BoxSizing() == BoxSizing.ContentBox ? child.Padding.Add(child.Border).SumAxes().Main(dir) : 0;
                float? basis = basisStyle.MaybeResolve(basisParent, tree.Calc).MaybeAdd(adjustment);
                float? stretch = basisParent.MaybeSub(child.Margin.MainAxisSum(dir)).MaybeMax(0);
                SizingKeywordResolution? keyword = null;
                if (basisStyle.IsContent()) basis = null;
                else if (basisStyle.IsSizingKeyword()) { basis = null; keyword = SizingKeywordUtils.ResolveSizingKeyword(basisStyle, stretch, basisParent, tree.Calc); }
                else
                {
                    if (basisStyle.IsAuto()) basis ??= child.Size.Main(dir);
                    if (!basis.HasValue && basisStyle.IsAuto()) keyword = SizingKeywordUtils.ResolveSizingKeyword(child.SizeStyle.Main(dir), stretch, basisParent, tree.Calc);
                }
                if (keyword is { IsExact: true } exact) basis = exact.Size;
                child.FlexBasisIsDefinite = basis.HasValue;
                if (!basis.HasValue && crossDefinite && child.AspectRatio is { } ratio && known.Cross(dir) is { } cross)
                {
                    basis = dir.IsRow() ? cross * ratio : cross / ratio;
                    child.FlexBasisIsDefinite = true;
                }
                var mainSpace = keyword is { IsExact: false } measure ? measure.AvailableSpace
                    : availableSpace.Main(dir) == AvailableSpace.MinContent ? AvailableSpace.MinContent : AvailableSpace.MaxContent;
                child.FlexBasis = basis ?? tree.MeasureChildSize(child.Node, known, parent,
                    new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent).WithMain(dir, mainSpace).WithCross(dir, crossSpace),
                    SizingMode.ContentSize, dir.MainAxis(), LineExtensions.FalseLine);
                float pbMain = child.Padding.MainAxisSum(dir) + child.Border.MainAxisSum(dir);
                child.FlexBasis = TaffySys.F32Max(child.FlexBasis, pbMain);
                child.InnerFlexBasis = child.FlexBasis - pbMain;
                var pb = child.Padding.Add(child.Border).SumAxes().Map<float?>(v => v);
                float? minMain = child.MinSize.Or(new(child.OverflowStyle.X.MaybeIntoAutomaticMinSize(), child.OverflowStyle.Y.MaybeIntoAutomaticMinSize())).Main(dir);
                child.ResolvedMinimumMainSize = minMain ?? tree.MeasureChildSize(child.Node, known, parent,
                    new Size<AvailableSpace>(AvailableSpace.MinContent, AvailableSpace.MinContent).WithCross(dir, crossSpace),
                    SizingMode.ContentSize, dir.MainAxis(), LineExtensions.FalseLine).MaybeMin(child.Size.Main(dir)).MaybeMin(transferredMax.Main(dir)).MaybeMax(pb.Main(dir));
                float hypotheticalMin = child.ResolvedMinimumMainSize.MaybeMax(transferredMin.Main(dir)).MaybeMax(pb.Main(dir));
                float hypothetical = child.FlexBasis.MaybeClamp(hypotheticalMin, transferredMax.Main(dir));
                child.HypotheticalInnerSize.SetMain(dir, hypothetical);
                child.HypotheticalOuterSize.SetMain(dir, hypothetical + child.Margin.MainAxisSum(dir));
            }
        }

        /// <summary>
        /// Collect flex items into flex lines.
        ///
        /// # [9.3. Main Size Determination](https://www.w3.org/TR/css-flexbox-1/#main-sizing)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ValueList<FlexLine> CollectFlexLines(
            ref AlgoConstants constants,
            Size<AvailableSpace> availableSpace,
            FlexItem[] flexItems)
        {
            var memory = flexItems.AsMemory();

            if (!constants.IsBalance && (!constants.IsWrap || !constants.KnownMainSizeIsDefinite))
            {
                var lines = new ValueList<FlexLine>();
                lines.Add(new FlexLine(memory, 0, 0f, 0f));
                return lines;
            }

            var mainAxisAvailableSpace = availableSpace.Main(constants.Dir);
            if (constants.MaxSize.Main(constants.Dir) is { } maxMain)
            {
                float limit = mainAxisAvailableSpace.IntoOption() ?? maxMain;
                if (!constants.HasDefiniteMainSize) limit = TaffySys.F32Min(limit, maxMain);
                mainAxisAvailableSpace = AvailableSpace.Definite(limit.MaybeMax(constants.MinSize.Main(constants.Dir)));
            }
            else if (constants.IsColumn && !constants.HasDefiniteMainSize && mainAxisAvailableSpace.IsDefinite()) mainAxisAvailableSpace = AvailableSpace.MaxContent;

            if (constants.IsBalance && mainAxisAvailableSpace != AvailableSpace.MinContent)
            {
                if (flexItems.Length == 0) return new();
                if (!constants.KnownMainSizeIsDefinite) mainAxisAvailableSpace = AvailableSpace.MaxContent;
                var sizes = new float[flexItems.Length];
                for (int i = 0; i < sizes.Length; i++) sizes[i] = flexItems[i].HypotheticalOuterSize.Main(constants.Dir);
                var counts = FlexBalance.BalancedLineItemCounts(sizes, mainAxisAvailableSpace.IntoOption() ?? float.PositiveInfinity,
                    constants.Gap.Main(constants.Dir), constants.LineCount);
                var result = new ValueList<FlexLine>(); int start = 0;
                foreach (int count in counts) { result.Add(new(memory.Slice(start, count), start, 0, 0)); start += count; }
                return result;
            }

            if (mainAxisAvailableSpace == AvailableSpace.MaxContent)
            {
                // If we're sizing under a max-content constraint then the flex items will never wrap
                var lines = new ValueList<FlexLine>();
                lines.Add(new FlexLine(memory, 0, 0f, 0f));
                return lines;
            }

            if (mainAxisAvailableSpace == AvailableSpace.MinContent)
            {
                // If flex-wrap is Wrap and we're sizing under a min-content constraint, then we take every
                // possible wrapping opportunity and place each item in its own line
                var lines = new ValueList<FlexLine>();
                for (int i = 0; i < flexItems.Length; i++)
                {
                    lines.Add(new FlexLine(memory.Slice(i, 1), i, 0f, 0f));
                }
                return lines;
            }

            // Definite case
            {
                var mainAxisSpace = mainAxisAvailableSpace.UnwrapOr(0f);
                var lines = new ValueList<FlexLine>();
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
            float initialItemsSpace = 0f;
            for (int i = 0; i < line.Items.Length; i++)
            {
                var child = line.Items[i];
                initialItemsSpace += child.Frozen
                    ? child.OuterTargetSize.Main(constants.Dir)
                    : child.FlexBasis + child.Margin.MainAxisSum(constants.Dir);
            }
            float usedSpace = totalMainAxisGap + initialItemsSpace;
            var initialFreeSpace = (constants.NodeInnerSize.Main(constants.Dir).MaybeSub(usedSpace)) ?? 0f;

            // 4. Loop
            while (true)
            {
                // a. Check frozen state, used item space and unfrozen flex factors in one pass.
                bool allFrozen = true;
                float itemsSpace = 0f;
                float sumFlexGrow = 0f;
                float sumFlexShrink = 0f;
                for (int i = 0; i < line.Items.Length; i++)
                {
                    ref var child = ref line.Items[i];
                    if (child.Frozen)
                    {
                        itemsSpace += child.OuterTargetSize.Main(constants.Dir);
                    }
                    else
                    {
                        allFrozen = false;
                        itemsSpace += child.FlexBasis + child.Margin.MainAxisSum(constants.Dir);
                        sumFlexGrow += child.FlexGrow;
                        sumFlexShrink += child.FlexShrink;
                    }
                }
                if (allFrozen) break;

                // b. Calculate the remaining free space.
                float usedSpace2 = totalMainAxisGap + itemsSpace;

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
                        var clamped = TaffySys.F32Max(
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
        private static void DetermineHypotheticalCrossSize(ILayoutFlexboxContainer tree, FlexLine line,
            ref AlgoConstants constants, Size<AvailableSpace> availableSpace)
        {
            var dir = constants.Dir;
            bool lineKnown = !constants.IsWrap && constants.NodeOuterSize.Cross(dir).HasValue;
            foreach (ref var child in line.Items)
            {
                if (lineKnown && child.IsStretched(dir) && !child.ParticipatesInBaselineAlignment(dir)) continue;
                float pb = child.Padding.Add(child.Border).CrossAxisSum(dir);
                float? min = child.MinSize.MaybeApplyAspectRatio(child.AspectRatio).Cross(dir), max = child.MaxSize.MaybeApplyAspectRatio(child.AspectRatio).Cross(dir);
                var cross = child.Size.Cross(dir).MaybeClamp(min, max).MaybeMax(pb);
                var crossSpace = availableSpace.Cross(dir);
                if (crossSpace.IsDefinite()) crossSpace = AvailableSpace.Definite(TaffySys.F32Max(constants.DividedCrossSpace(crossSpace.Unwrap()) - child.Margin.CrossAxisSum(dir), 0));
                crossSpace = crossSpace.MaybeClamp(min, max).MaybeMax(pb);
                float? stretch = constants.NodeInnerSize.Cross(dir);
                if (stretch.HasValue) stretch = TaffySys.F32Max(constants.DividedCrossSpace(stretch.Value) - child.Margin.CrossAxisSum(dir), 0);
                var keyword = SizingKeywordUtils.ResolveSizingKeyword(child.SizeStyle.Cross(dir), stretch, constants.NodeInnerSize.Cross(dir), tree.Calc);
                if (keyword is { IsExact: false } measured) crossSpace = measured.AvailableSpace;
                float inner;
                if (cross.HasValue) inner = cross.Value;
                else
                {
                    var definite = new Size<bool>(true, true).WithMain(dir, constants.HasDefiniteMainSize || child.FlexBasisIsDefinite)
                        .WithCross(dir, child.IsStretched(dir) || child.Size.Cross(dir).HasValue || (constants.IsColumn && constants.CrossAvailableSpaceIsDefinite));
                    var mainSpace = AvailableSpace.Definite(TaffySys.F32Max(constants.ContainerSize.Main(dir) - child.Margin.MainAxisSum(dir), 0));
                    inner = tree.ComputeChildLayout(child.Node, new LayoutInput { RunMode = RunMode.ComputeSize, SizingMode = SizingMode.ContentSize,
                        Axis = dir.CrossAxis().ToRequestedAxis(), KnownDimensions = constants.IsRow ? new(child.TargetSize.Width, cross) : new(cross, child.TargetSize.Height),
                        KnownDimensionsAreDefinite = definite, ParentSize = constants.NodeInnerSize,
                        AvailableSpace = constants.IsRow ? new(mainSpace, crossSpace) : new(crossSpace, mainSpace), VerticalMarginsAreCollapsible = LineExtensions.FalseLine })
                        .Size.GetAbs(dir.CrossAxis()).MaybeClamp(min, max);
                    inner = TaffySys.F32Max(inner, pb);
                }
                child.HypotheticalInnerSize.SetCross(dir, inner); child.HypotheticalOuterSize.SetCross(dir, inner + child.Margin.CrossAxisSum(dir));
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
            ref ValueList<FlexLine> flexLines,
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
                    if (child.ParticipatesInBaselineAlignment(constants.Dir))
                        lineBaselineChildCount++;
                }
                if (lineBaselineChildCount <= 1)
                    continue;

                for (int i = 0; i < line.Items.Length; i++)
                {
                    ref var child = ref line.Items[i];
                    // Only calculate baselines for children participating in baseline alignment
                    if (!child.ParticipatesInBaselineAlignment(constants.Dir))
                        continue;

                    var measuredSizeAndBaselines = tree.ComputeChildLayout(child.Node, new LayoutInput
                    {
                        RunMode = RunMode.PerformLayout, SizingMode = SizingMode.ContentSize, Axis = RequestedAxis.Both,
                        KnownDimensions = constants.IsRow ? new(child.TargetSize.Width, child.HypotheticalInnerSize.Height)
                            : new(child.HypotheticalInnerSize.Width, child.TargetSize.Height),
                        KnownDimensionsAreDefinite = new Size<bool>(true, true)
                            .WithMain(constants.Dir, constants.HasDefiniteMainSize || child.FlexBasisIsDefinite)
                            .WithCross(constants.Dir, child.IsStretched(constants.Dir) || child.Size.Cross(constants.Dir).HasValue || (constants.IsColumn && constants.CrossAvailableSpaceIsDefinite)),
                        ParentSize = constants.NodeInnerSize,
                        AvailableSpace = new Size<AvailableSpace>(constants.IsRow ? AvailableSpace.Definite(constants.ContainerSize.Width) : availableSpace.Width.MaybeSet(nodeSize.Width),
                            constants.IsRow ? availableSpace.Height.MaybeSet(nodeSize.Height) : AvailableSpace.Definite(constants.ContainerSize.Height))
                            .MaybeSub(child.Margin.SumAxes()).MaybeMax(SizeExtensions.ZeroF32),
                        VerticalMarginsAreCollapsible = LineExtensions.FalseLine,
                    });

                    var height = measuredSizeAndBaselines.Size.Height;
                    var baseline = AlignmentUtils.ChildFirstBaseline(measuredSizeAndBaselines.FirstBaselines.Y, height, child.OverflowStyle.Y);

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
            ref ValueList<FlexLine> flexLines,
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
                        maxBaseline = TaffySys.F32Max(maxBaseline, child.Baseline);

                    float maxCross = 0f;
                    foreach (var child in line.Items)
                    {
                        float val;
                        if (child.ParticipatesInBaselineAlignment(constants.Dir))
                        {
                            val = maxBaseline - child.Baseline + child.HypotheticalOuterSize.Cross(constants.Dir);
                        }
                        else
                        {
                            val = child.HypotheticalOuterSize.Cross(constants.Dir);
                        }
                        maxCross = TaffySys.F32Max(maxCross, val);
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
            ref ValueList<FlexLine> flexLines,
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
                float linesTotalCross = 0f;
                foreach (var line in flexLines)
                    linesTotalCross += line.CrossSize;
                linesTotalCross += totalCrossAxisGap;

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
            ref ValueList<FlexLine> flexLines,
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
                    if (child.IsStretched(constants.Dir))
                    {
                        // For some reason this particular usage of max_width is an exception to the rule that max_width's transfer
                        // using the aspect_ratio (if set).
                        var cPadding = childStyle.Padding().ResolveOrZero(constants.NodeInnerSize.Width, tree.Calc);
                        var cBorder = childStyle.Border().ResolveOrZero(constants.NodeInnerSize.Width, tree.Calc);
                        var cPbSum = cPadding.SumAxes().Add(cBorder.SumAxes());
                        var cBoxSizingAdj = childStyle.BoxSizing() == BoxSizing.ContentBox
                            ? cPbSum : SizeExtensions.ZeroF32;

                        var maxSizeIgnoringAspectRatio = childStyle
                            .MaxSize()
                            .MaybeResolve(constants.NodeInnerSize, tree.Calc)
                            .MaybeAdd(cBoxSizingAdj.Map(v => (float?)v));

                        crossSize = TaffySys.F32Max(lineCrossSize - child.Margin.CrossAxisSum(constants.Dir), 0).MaybeClamp(
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
            ref ValueList<FlexLine> flexLines,
            ref AlgoConstants constants)
        {
            foreach (var line in flexLines)
            {
                var totalMainAxisGap = SumAxisGaps(constants.Gap.Main(constants.Dir), line.Items.Length);
                float itemsSpace = 0f;
                for (int i = 0; i < line.Items.Length; i++)
                    itemsSpace += line.Items[i].OuterTargetSize.Main(constants.Dir);
                float usedSpace = totalMainAxisGap + itemsSpace;
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

                if (freeSpace > 0 && numAutoMargins > 0) freeSpace = 0;
                var numItems = line.Items.Length;
                var layoutReverse = constants.Dir.IsReverse();
                var gap = constants.Gap.Main(constants.Dir);
                var rawJustifyContentMode = constants.JustifyContentStyle ?? AlignContent.FlexStart;
                var justifyContentMode =
                    AlignmentUtils.ApplyAlignmentFallback(freeSpace, numItems, rawJustifyContentMode);

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

        /// <summary>
        /// Resolve cross-axis auto margins.
        ///
        /// # [9.6. Cross-Axis Alignment](https://www.w3.org/TR/css-flexbox-1/#cross-alignment)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ResolveCrossAxisAutoMargins(
            ref ValueList<FlexLine> flexLines,
            ref AlgoConstants constants)
        {
            foreach (var line in flexLines)
            {
                var lineCrossSize = line.CrossSize;
                float maxBaseline = 0f;
                float maxBaselineToBottomDistance = 0f;
                for (int i = 0; i < line.Items.Length; i++)
                {
                    maxBaseline = TaffySys.F32Max(maxBaseline, line.Items[i].Baseline);
                    if (line.Items[i].ParticipatesInBaselineAlignment(constants.Dir))
                        maxBaselineToBottomDistance = TaffySys.F32Max(maxBaselineToBottomDistance,
                            line.Items[i].OuterTargetSize.Cross(constants.Dir) - line.Items[i].Baseline);
                }

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
                        child.OffsetCross = AlignFlexItemsAlongCrossAxis(ref child, freeSpace, maxBaseline, maxBaselineToBottomDistance, ref constants);
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
            float maxBaselineToBottomDistance,
            ref AlgoConstants constants)
        {
            var crossAxisShouldReverse = constants.IsColumn && constants.LayoutDirection.IsRtl();
            var alignKeyword = child.AlignSelf.IsSafe && freeSpace < 0f
                ? AlignItemsKeyword.Start
                : child.AlignSelf.Keyword;

            switch (alignKeyword)
            {
                case AlignItemsKeyword.Start:
                    return crossAxisShouldReverse ? freeSpace : 0f;
                case AlignItemsKeyword.FlexStart:
                    return (constants.IsWrapReverse ^ crossAxisShouldReverse) ? freeSpace : 0f;
                case AlignItemsKeyword.End:
                    return crossAxisShouldReverse ? 0f : freeSpace;
                case AlignItemsKeyword.FlexEnd:
                    return (constants.IsWrapReverse ^ crossAxisShouldReverse) ? 0f : freeSpace;
                case AlignItemsKeyword.Center:
                    return freeSpace / 2f;
                case AlignItemsKeyword.Baseline:
                    if (constants.IsRow)
                    {
                        if (constants.IsWrapReverse)
                        {
                            float lineCrossSize = freeSpace + child.OuterTargetSize.Cross(constants.Dir);
                            return lineCrossSize - maxBaselineToBottomDistance - child.Baseline;
                        }
                        return maxBaseline - child.Baseline;
                    }
                    else
                    {
                        // Until we support vertical writing modes, baseline alignment only makes sense if
                        // the direction is row, so we treat it as flex-start alignment in columns.
                        var baselineColumnShouldReverse = crossAxisShouldReverse && !constants.IsWrap;
                        return (constants.IsWrapReverse ^ baselineColumnShouldReverse) ? freeSpace : 0f;
                    }
                case AlignItemsKeyword.Stretch:
                    return (constants.IsWrapReverse ^ crossAxisShouldReverse) ? freeSpace : 0f;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Determine the flex container's used cross size.
        ///
        /// # [9.6. Cross-Axis Alignment](https://www.w3.org/TR/css-flexbox-1/#cross-alignment)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DetermineContainerCrossSize(
            ref ValueList<FlexLine> flexLines,
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
            outerContainerSize = TaffySys.F32Max(outerContainerSize, paddingBorderSum - crossScrollbarGutter);
            var innerContainerSize = TaffySys.F32Max(outerContainerSize - paddingBorderSum, 0f);

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
            ref ValueList<FlexLine> flexLines,
            ref AlgoConstants constants,
            float totalCrossSize)
        {
            var numLines = flexLines.Count;
            var gap = constants.Gap.Cross(constants.Dir);
            var totalCrossAxisGap = SumAxisGaps(gap, numLines);
            var freeSpace = constants.InnerContainerSize.Cross(constants.Dir) - totalCrossSize - totalCrossAxisGap;

            var alignContentMode = AlignmentUtils.ApplyAlignmentFallback(freeSpace, numLines, constants.AlignContentStyle);

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
            ref Rect<float> totalContentSize,
            Rect<float> containerBorder,
            bool parentIsScrollContainer,
            bool hasDefiniteMainSize, bool crossAvailableDefinite,
            Size<float> containerSize,
            Size<float?> nodeInnerSize,
            FlexDirection direction,
            Direction layoutDirection)
        {
            bool crossStretched = !RectBoolCrossStart(item.MarginIsAuto, direction) && !RectBoolCrossEnd(item.MarginIsAuto, direction)
                && (item.SizeStyle.Cross(direction).IsStretch() || (item.AlignSelf == AlignItems.Stretch && item.SizeStyle.Cross(direction).IsAuto()));
            var definite = new Size<bool>(true, true).WithMain(direction, hasDefiniteMainSize || item.FlexBasisIsDefinite)
                .WithCross(direction, crossStretched || item.Size.Cross(direction).HasValue || (direction.IsColumn() && crossAvailableDefinite));
            var layoutOutput = tree.ComputeChildLayout(item.Node, new LayoutInput { RunMode = RunMode.PerformLayout,
                SizingMode = SizingMode.ContentSize, Axis = RequestedAxis.Both, KnownDimensions = item.TargetSize.Map<float?>(v => v),
                KnownDimensionsAreDefinite = definite, ParentSize = nodeInnerSize,
                AvailableSpace = containerSize.Sub(item.Margin.SumAxes()).Map(v => AvailableSpace.Definite(TaffySys.F32Max(v, 0))),
                VerticalMarginsAreCollapsible = LineExtensions.FalseLine });

            var size = layoutOutput.Size;
            var contentSize = layoutOutput.ContentSize;

            var isRtlRow = direction.IsRow() && layoutDirection.IsRtl();
            var isRtlColumn = direction.IsColumn() && layoutDirection.IsRtl();

            var insetMainStart = item.Inset.MainStart(direction);
            var insetMainEnd = item.Inset.MainEnd(direction);
            var insetCrossStart = item.Inset.CrossStart(direction);
            var insetCrossEnd = item.Inset.CrossEnd(direction);

            float mainRelativeInset;
            if (isRtlRow)
            {
                mainRelativeInset = insetMainEnd ?? (insetMainStart.HasValue ? -insetMainStart.GetValueOrDefault() : 0f);
            }
            else
            {
                mainRelativeInset = insetMainStart ?? (insetMainEnd.HasValue ? -insetMainEnd.GetValueOrDefault() : 0f);
            }

            float crossRelativeInset;
            if (isRtlColumn)
            {
                crossRelativeInset = insetCrossEnd.HasValue
                    ? -insetCrossEnd.GetValueOrDefault()
                    : (insetCrossStart ?? 0f);
            }
            else
            {
                crossRelativeInset = insetCrossStart ?? (insetCrossEnd.HasValue ? -insetCrossEnd.GetValueOrDefault() : 0f);
            }

            var effectiveLineOffsetCross = isRtlColumn ? 0f : lineOffsetCross;

            float offsetMain;
            if (isRtlRow)
            {
                offsetMain = totalOffsetMain - item.OffsetMain - item.Margin.MainEnd(direction) - mainRelativeInset - size.Width;
            }
            else
            {
                offsetMain = totalOffsetMain + item.OffsetMain + item.Margin.MainStart(direction) + mainRelativeInset;
            }

            var offsetCross = totalOffsetCross
                + item.OffsetCross
                + effectiveLineOffsetCross
                + item.Margin.CrossStart(direction)
                + crossRelativeInset;

            if (direction.IsRow())
            {
                var baselineOffsetCross = totalOffsetCross + item.OffsetCross + effectiveLineOffsetCross + item.Margin.CrossStart(direction);
                var innerBaseline = AlignmentUtils.ChildFirstBaseline(layoutOutput.FirstBaselines.Y, size.Height, item.OverflowStyle.Y) ?? size.Height;
                item.Baseline = baselineOffsetCross + innerBaseline;
            }
            else
            {
                var baselineOffsetMain = totalOffsetMain + item.OffsetMain + item.Margin.MainStart(direction);
                var innerBaseline = AlignmentUtils.ChildFirstBaseline(layoutOutput.FirstBaselines.Y, size.Height, item.OverflowStyle.Y) ?? size.Height;
                item.Baseline = baselineOffsetMain + innerBaseline;
            }

            var location = direction.IsRow()
                ? new Point<float>(offsetMain, offsetCross)
                : new Point<float>(offsetCross, offsetMain);

            item.OofCandidates = layoutOutput.OofCandidates.Take();
            item.OofCandidates.Translate(location);

            var scrollbarSize = new Size<float>(
                item.OverflowStyle.Y == Overflow.Scroll ? item.ScrollbarWidth : 0f,
                item.OverflowStyle.X == Overflow.Scroll ? item.ScrollbarWidth : 0f);

            tree.SetUnroundedLayout(
                item.Node,
                new Layout
                {
                    Order = item.Order,
                    Size = size,
                    ScrollableOverflowRect = layoutOutput.ScrollableOverflowRect,
                    ScrollbarSize = scrollbarSize,
                    Location = location,
                    Padding = item.Padding,
                    Border = item.Border,
                    Margin = item.Margin,
                });

            if (isRtlRow)
            {
                totalOffsetMain -= item.OffsetMain + item.Margin.MainAxisSum(direction) + size.Main(direction);
            }
            else
            {
                totalOffsetMain += item.OffsetMain + item.Margin.MainAxisSum(direction) + size.Main(direction);
            }

            var contributionLocation = new Point<float>(layoutDirection.IsRtl()
                ? containerSize.Width - (location.X + size.Width) - containerBorder.Right : location.X - containerBorder.Left,
                location.Y - containerBorder.Top);
            totalContentSize = totalContentSize.Union(ScrollableOverflow.ComputeContribution(contributionLocation,
                size, layoutOutput.ScrollableOverflowRect, item.OverflowStyle, item.Contain, parentIsScrollContainer));

        }

        /// <summary>
        /// Calculates the layout line
        /// </summary>
        private static void CalculateLayoutLine(
            ILayoutFlexboxContainer tree,
            FlexLine line,
            ref float totalOffsetCross,
            ref Rect<float> contentSize,
            Rect<float> containerBorder,
            bool parentIsScrollContainer,
            bool hasDefiniteMainSize, bool crossAvailableDefinite,
            Size<float> containerSize,
            Size<float?> nodeInnerSize,
            Rect<float> paddingBorder,
            FlexDirection direction,
            Direction layoutDirection)
        {
            float totalOffsetMain = (layoutDirection.IsRtl() && direction.IsRow())
                ? containerSize.Width - paddingBorder.MainEnd(direction)
                : paddingBorder.MainStart(direction);
            var lineOffsetCross = line.OffsetCross;

            var isRtlColumn = layoutDirection.IsRtl() && direction.IsColumn();
            if (isRtlColumn)
            {
                totalOffsetCross -= lineOffsetCross + line.CrossSize;
            }

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
                        containerBorder, parentIsScrollContainer, hasDefiniteMainSize, crossAvailableDefinite,
                        containerSize,
                        nodeInnerSize,
                        direction,
                        layoutDirection);
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
                        containerBorder, parentIsScrollContainer, hasDefiniteMainSize, crossAvailableDefinite,
                        containerSize,
                        nodeInnerSize,
                        direction,
                        layoutDirection);
                }
            }

            if (!isRtlColumn)
            {
                totalOffsetCross += lineOffsetCross + line.CrossSize;
            }
        }

        /// <summary>
        /// Do a final layout pass and collect the resulting layouts.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Rect<float> FinalLayoutPass(
            ILayoutFlexboxContainer tree,
            ref ValueList<FlexLine> flexLines,
            ref AlgoConstants constants)
        {
            float totalOffsetCross = (constants.IsColumn && constants.LayoutDirection.IsRtl())
                ? constants.ContainerSize.Width - constants.ContentBoxInset.CrossEnd(constants.Dir)
                : constants.ContentBoxInset.CrossStart(constants.Dir);
            Rect<float> contentSize = default;

            if (constants.IsWrapReverse)
            {
                for (int i = flexLines.Count - 1; i >= 0; i--)
                {
                    CalculateLayoutLine(
                        tree,
                        flexLines[i],
                        ref totalOffsetCross,
                        ref contentSize,
                        constants.Border, constants.IsScrollContainer, constants.HasDefiniteMainSize, constants.CrossAvailableSpaceIsDefinite,
                        constants.ContainerSize,
                        constants.NodeInnerSize,
                        constants.ContentBoxInset,
                        constants.Dir,
                        constants.LayoutDirection);
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
                        constants.Border, constants.IsScrollContainer, constants.HasDefiniteMainSize, constants.CrossAvailableSpaceIsDefinite,
                        constants.ContainerSize,
                        constants.NodeInnerSize,
                        constants.ContentBoxInset,
                        constants.Dir,
                        constants.LayoutDirection);
                }
            }

            if (constants.IsScrollContainer)
            {
                contentSize.Right += constants.LayoutDirection.IsRtl()
                    ? constants.ContentBoxInset.Left - constants.Border.Left - constants.ScrollbarGutter.X
                    : constants.ContentBoxInset.Right - constants.Border.Right - constants.ScrollbarGutter.X;
                contentSize.Bottom += constants.ContentBoxInset.Bottom - constants.Border.Bottom - constants.ScrollbarGutter.Y;
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
        /// Returns true if the float is normal (excludes zero, subnormals, infinity and NaN).
        /// Equivalent to Rust's f32::is_normal()
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsNormal(float value)
        {
            return float.IsNormal(value);
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
