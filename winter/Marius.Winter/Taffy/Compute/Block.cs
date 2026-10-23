// Ported from taffy/src/compute/block.rs
// Computes the CSS block layout algorithm in the case that the block container being laid out
// contains only block-level boxes

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// Context for positioning Block and Float boxes within a Block Formatting Context
    /// </summary>
    public class BlockFormattingContext
    {
        /// <summary>
        /// The float positioning context that handles positioning floats within this Block Formatting Context
        /// </summary>
        internal FloatContext FloatCtx = new FloatContext();

        /// <summary>Create a new BlockFormattingContext</summary>
        public BlockFormattingContext()
        {
        }

        /// <summary>Create an initial BlockContext for this BlockFormattingContext</summary>
        public BlockContext RootBlockContext()
        {
            return new BlockContext(this, 0.0f, new float[] { 0.0f, 0.0f }, new float[] { 0.0f, 0.0f }, float.NegativeInfinity, true);
        }
    }

    /// <summary>
    /// Context for each individual Block within a Block Formatting Context.
    /// Contains a reference to the BlockFormattingContext + block-specific data.
    /// </summary>
    public partial class BlockContext
    {
        /// <summary>A reference to the root BlockFormattingContext that this BlockContext belongs to</summary>
        internal BlockFormattingContext Bfc;

        /// <summary>
        /// The y-offset of the border-top of the block node, relative to the border-top of the
        /// root node of the Block Formatting Context it belongs to.
        /// </summary>
        internal float YOffset;

        /// <summary>
        /// The x-inset of the border-box in from each side of the block node, relative to the root node
        /// of the Block Formatting Context it belongs to.
        /// </summary>
        internal float[] Insets;

        /// <summary>The x-insets of the content box</summary>
        internal float[] ContentBoxInsets;

        /// <summary>The height that floats take up in the element</summary>
        internal float FloatContentContribution;

        /// <summary>Whether the node is the root of the Block Formatting Context it belongs to.</summary>
        internal bool IsRoot;

        internal BlockContext(BlockFormattingContext bfc, float yOffset, float[] insets, float[] contentBoxInsets, float floatContentContribution, bool isRoot)
        {
            Bfc = bfc;
            YOffset = yOffset;
            Insets = insets;
            ContentBoxInsets = contentBoxInsets;
            FloatContentContribution = floatContentContribution;
            IsRoot = isRoot;
        }

        /// <summary>Create a sub-BlockContext for a child block node</summary>
        public BlockContext SubContext(float additionalYOffset, float[] insets)
        {
            var newInsets = new float[] { this.Insets[0] + insets[0], this.Insets[1] + insets[1] };
            var child = new BlockContext(
                Bfc,
                YOffset + additionalYOffset,
                newInsets,
                new float[] { newInsets[0], newInsets[1] },
                float.NegativeInfinity,
                false
            );
            child.AdjoiningLeft = AdjoiningLeft; child.AdjoiningRight = AdjoiningRight;
            return child;
        }

        /// <summary>Returns whether this block is the root block of its Block Formatting Context</summary>
        public bool IsBfcRoot()
        {
            return IsRoot;
        }

        #region Float layout methods

        /// <summary>
        /// Set the width of the overall Block Formatting Context. This is used to resolve positions
        /// that are relative to the right of the context such as right-floated boxes.
        /// </summary>
        public void SetWidth(float availableWidth)
        {
            Bfc.FloatCtx.SetWidth(availableWidth);
        }

        /// <summary>
        /// Set the x-axis content-box insets of the BlockContext. These are the difference between the border-box
        /// and the content-box of the box (padding + border + scrollbar_gutter).
        /// </summary>
        public void ApplyContentBoxInset(float[] contentBoxXInsets)
        {
            ContentBoxInsets[0] = Insets[0] + contentBoxXInsets[0];
            ContentBoxInsets[1] = Insets[1] + contentBoxXInsets[1];
        }

        /// <summary>Whether the float context contains any floats</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasFloats()
        {
            return Bfc.FloatCtx.HasFloats();
        }

        /// <summary>Whether the float context contains any floats that extend to or below min_y</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasActiveFloats(float minY)
        {
            return Bfc.FloatCtx.HasActiveFloats(minY + YOffset);
        }

        /// <summary>Position a floated box within the context</summary>
        public Point<float> PlaceFloatedBox(
            Size<float> floatedBox,
            float minY,
            FloatDirection direction,
            Clear clear)
        {
            var pos = Bfc.FloatCtx.PlaceFloatedBox(
                floatedBox,
                minY + YOffset,
                ContentBoxInsets,
                direction,
                clear
            );
            pos.Y -= YOffset;
            pos.X -= Insets[0];

            FloatContentContribution = TaffySys.F32Max(FloatContentContribution, pos.Y + floatedBox.Height);

            return pos;
        }

        /// <summary>Search a space suitable for laying out non-floated content into</summary>
        public ContentSlot FindContentSlot(float minY, Clear clear, int? after)
        {
            var slot = Bfc.FloatCtx.FindContentSlot(minY + YOffset, ContentBoxInsets, clear, after);
            slot.Y -= YOffset;
            slot.X -= Insets[0];
            return slot;
        }

        /// <summary>Get the bottom of lowest relevant float for the specific clear property</summary>
        public float? ClearedThreshold(Clear clear)
        {
            var threshold = Bfc.FloatCtx.ClearedThreshold(clear);
            return threshold.HasValue ? threshold.Value - YOffset : null;
        }

        /// <summary>Update the height that descendant floats with the height that floats consume within a particular child</summary>
        internal void AddChildFloatedContentHeightContribution(float childContribution)
        {
            FloatContentContribution = TaffySys.F32Max(FloatContentContribution, childContribution);
        }

        /// <summary>Returns the height that descendant floats consume</summary>
        public float FloatedContentHeightContribution()
        {
            return FloatContentContribution;
        }

        #endregion
    }

    /// <summary>
    /// Per-child data that is accumulated and modified over the course of the layout algorithm
    /// </summary>
    internal struct BlockItem
    {
        /// <summary>The identifier for the associated node</summary>
        public NodeId NodeId;
        public OofCandidates OofCandidates;
        public Size<Dimension> SizeStyle;
        public bool IsCompressibleReplaced;
        public Direction Direction;
        public AlignItems? AlignSelf, JustifySelf;
        public Contain Contain;

        /// <summary>
        /// The "source order" of the item. This is the index of the item within the children iterator,
        /// and controls the order in which the nodes are placed
        /// </summary>
        public uint Order;

        /// <summary>Items that are tables don't have stretch sizing applied to them</summary>
        public bool IsTable;

        /// <summary>Whether the child is a non-independent block or inline node</summary>
        public bool IsInSameBfc;

        /// <summary>The float style of the node</summary>
        public Float Float;

        /// <summary>The clear style of the node</summary>
        public Clear Clear;

        /// <summary>The base size of this item</summary>
        public Size<float?> Size;
        /// <summary>The minimum allowable size of this item</summary>
        public Size<float?> MinSize;
        /// <summary>The maximum allowable size of this item</summary>
        public Size<float?> MaxSize;

        /// <summary>The overflow style of the item</summary>
        public Point<Overflow> Overflow;
        /// <summary>The width of the item's scrollbars (if it has scrollbars)</summary>
        public float ScrollbarWidth;

        /// <summary>The position style of the item</summary>
        public Position Position;
        /// <summary>The final offset of this item</summary>
        public Rect<LengthPercentageAuto> Inset;
        /// <summary>The margin of this item</summary>
        public Rect<LengthPercentageAuto> Margin;
        /// <summary>The padding of this item</summary>
        public Rect<float> Padding;
        /// <summary>The border of this item</summary>
        public Rect<float> Border;
        /// <summary>The sum of padding and border for this item</summary>
        public Size<float> PaddingBorderSum;

        /// <summary>The computed border box size of this item</summary>
        public Size<float> ComputedSize;
        /// <summary>
        /// The computed "static position" of this item. The static position is the position
        /// taking into account padding, border, margins, and scrollbar_gutters but not inset
        /// </summary>
        public Point<float> StaticPosition;
        /// <summary>Whether margins can be collapsed through this item</summary>
        public bool CanBeCollapsedThrough;

        /// <summary>
        /// Pending layout for in-flow non-floated items. Held back so align-content can shift
        /// the block-axis location before the layout is committed to the tree.
        /// </summary>
        public Layout? FinalLayout;
    }

    /// <summary>
    /// Computes the layout of LayoutBlockContainer according to the block layout algorithm
    /// </summary>
    public static partial class BlockAlgorithm
    {
        /// <summary>
        /// Computes the layout of LayoutBlockContainer according to the block layout algorithm
        /// </summary>
        public static LayoutOutput ComputeBlockLayout(
            ILayoutBlockContainer tree,
            NodeId nodeId,
            LayoutInput inputs,
            BlockContext? blockCtx)
        {
            var knownDimensions = inputs.KnownDimensions;
            var parentSize = inputs.ParentSize;
            var runMode = inputs.RunMode;

            var style = tree.GetBlockContainerStyle(nodeId);

            // Pull these out earlier to avoid borrowing issues
            var overflow = style.Overflow();
            bool isScrollContainer = overflow.X.IsScrollContainer() || overflow.Y.IsScrollContainer();
            var aspectRatio = style.AspectRatio();
            var padding = style.Padding().ResolveOrZero(parentSize.Width, tree.Calc);
            var border = style.Border().ResolveOrZero(parentSize.Width, tree.Calc);
            var paddingBorderSize = padding.Add(border).SumAxes();
            var boxSizingAdjustment =
                style.BoxSizing() == BoxSizing.ContentBox ? paddingBorderSize : SizeExtensions.ZeroF32;

            var minSize = style
                .MinSize()
                .MaybeResolve(parentSize, tree.Calc)
                .MaybeApplyAspectRatio(aspectRatio)
                .MaybeAdd(boxSizingAdjustment.Map<float?>(v => v));
            var maxSize = style
                .MaxSize()
                .MaybeResolve(parentSize, tree.Calc)
                .MaybeApplyAspectRatio(aspectRatio)
                .MaybeAdd(boxSizingAdjustment.Map<float?>(v => v));
            var clampedStyleSize = inputs.SizingMode == SizingMode.InherentSize
                ? style
                    .Size()
                    .MaybeResolve(parentSize, tree.Calc)
                    .MaybeApplyAspectRatio(aspectRatio)
                    .MaybeAdd(boxSizingAdjustment.Map<float?>(v => v))
                    .MaybeClamp(minSize, maxSize)
                : SizeExtensions.NoneF32;

            // If both min and max in a given axis are set and max <= min then this determines the size in that axis
            var minMaxDefiniteSize = minSize.ZipMap(maxSize, (min, max) =>
            {
                if (min.HasValue && max.HasValue && max.Value <= min.Value) return min;
                return (float?)null;
            });

            var styledBasedKnownDimensions = knownDimensions
                .Or(minMaxDefiniteSize)
                .Or(clampedStyleSize)
                .MaybeMax(paddingBorderSize.Map<float?>(v => v));

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
                    return LayoutOutput.FromOuterSize(new Size<float>(styledBasedKnownDimensions.Width.Value, 0.0f));
                }
            }

            bool establishesBfc = isScrollContainer || (style.AlignContent() ?? AlignContent.Normal).Keyword != AlignContentKeyword.Normal
                || style.Contain().EstablishesIndependentFormattingContext();
            var innerInput = inputs; innerInput.KnownDimensions = styledBasedKnownDimensions;
            var context = blockCtx != null && !establishesBfc ? blockCtx : new BlockFormattingContext().RootBlockContext();
            var output = ComputeInner(tree, nodeId, innerInput, context);
            if (style.Contain().SuppressesBaseline()) output.Baselines = Baselines.None;
            return output;
        }

        /// <summary>
        /// Computes the layout of LayoutBlockContainer according to the block layout algorithm (inner implementation)
        /// </summary>
        private static LayoutOutput ComputeInner(
            ILayoutBlockContainer tree,
            NodeId nodeId,
            LayoutInput inputs,
            BlockContext blockCtx)
        {
            var knownDimensions = inputs.KnownDimensions;
            var parentSize = inputs.ParentSize;
            var availableSpace = inputs.AvailableSpace;
            var runMode = inputs.RunMode;
            var verticalMarginsAreCollapsible = inputs.VerticalMarginsAreCollapsible;

            var style = tree.GetBlockContainerStyle(nodeId);
            var rawPadding = style.Padding();
            var rawBorder = style.Border();
            var rawMargin = style.Margin();
            var aspectRatio = style.AspectRatio();
            var padding = rawPadding.ResolveOrZero(parentSize.Width, tree.Calc);
            var border = rawBorder.ResolveOrZero(parentSize.Width, tree.Calc);
            var direction = style.Direction();

            // Scrollbar gutters are reserved when the `overflow` property is set to `Overflow::Scroll`.
            // However, the axis are switched (transposed) because a node that scrolls vertically needs
            // *horizontal* space to be reserved for a scrollbar
            var overflowStyle = style.Overflow();
            var transposedOverflow = new Point<Overflow>(overflowStyle.Y, overflowStyle.X);
            float scrollbarWidthVal = style.ScrollbarWidth();
            float offsetsX = transposedOverflow.X == Overflow.Scroll ? scrollbarWidthVal : 0.0f;
            float offsetsY = transposedOverflow.Y == Overflow.Scroll ? scrollbarWidthVal : 0.0f;
            var scrollbarGutter = direction == Direction.Ltr
                ? new Rect<float>(0.0f, offsetsX, 0.0f, offsetsY)
                : new Rect<float>(offsetsX, 0.0f, 0.0f, offsetsY);
            var paddingBorder = padding.Add(border);
            var paddingBorderSize = paddingBorder.SumAxes();
            var contentBoxInset = paddingBorder.Add(scrollbarGutter);

            // Apply content box inset
            blockCtx.ApplyContentBoxInset(new float[] { contentBoxInset.Left, contentBoxInset.Right });

            var boxSizingAdjustment =
                style.BoxSizing() == BoxSizing.ContentBox ? paddingBorderSize : SizeExtensions.ZeroF32;
            var size = style
                .Size()
                .MaybeResolve(parentSize, tree.Calc)
                .MaybeApplyAspectRatio(aspectRatio)
                .MaybeAdd(boxSizingAdjustment.Map<float?>(v => v));
            var minSize = style
                .MinSize()
                .MaybeResolve(parentSize, tree.Calc)
                .MaybeApplyAspectRatio(aspectRatio)
                .MaybeAdd(boxSizingAdjustment.Map<float?>(v => v));
            var maxSize = style
                .MaxSize()
                .MaybeResolve(parentSize, tree.Calc)
                .MaybeApplyAspectRatio(aspectRatio)
                .MaybeAdd(boxSizingAdjustment.Map<float?>(v => v));

            // css-sizing-4: a definite size in one axis transfers through aspect-ratio to make
            // the other definite. Only adopt a newly-derived axis so incoming known sizes retain
            // any padding/border overrides already resolved by the parent.
            var derivedKnownDimensions = knownDimensions
                .MaybeApplyAspectRatio(aspectRatio)
                .MaybeClamp(minSize, maxSize);
            knownDimensions = new Size<float?>(
                knownDimensions.Width ?? derivedKnownDimensions.Width,
                knownDimensions.Height ?? derivedKnownDimensions.Height);
            var percentageBasis = knownDimensions;
            if (!inputs.KnownDimensionsAreDefinite.Height) percentageBasis.Height = null;
            var containerContentBoxSize = percentageBasis.MaybeSub(contentBoxInset.SumAxes().Map<float?>(v => v));

            var overflow = style.Overflow();
            bool isScrollContainer = overflow.X.IsScrollContainer() || overflow.Y.IsScrollContainer();

            bool establishesBfc = isScrollContainer || (style.AlignContent() ?? AlignContent.Normal).Keyword != AlignContentKeyword.Normal
                || style.Contain().EstablishesIndependentFormattingContext();
            var childrenAdjoin = new Line<bool>(verticalMarginsAreCollapsible.Start && !establishesBfc && !style.Position().IsOutOfFlow() && padding.Top == 0 && border.Top == 0,
                verticalMarginsAreCollapsible.End && !establishesBfc && !style.Position().IsOutOfFlow() && padding.Bottom == 0 && border.Bottom == 0);
            var ownMarginsCollapseWithChildren = new Line<bool>(childrenAdjoin.Start, childrenAdjoin.End && !size.Height.HasValue);
            bool hasStylesPreventingBeingCollapsedThrough = !style.IsBlock() || blockCtx.IsBfcRoot() || establishesBfc || style.Position().IsOutOfFlow()
                || padding.Top > 0 || padding.Bottom > 0 || border.Top > 0 || border.Bottom > 0;

            var textAlign = style.TextAlign();
            var alignContent = style.AlignContent();

            // 1. Generate items
            var items = GenerateItemList(tree, nodeId, containerContentBoxSize);

            // 2. Compute container width
            float containerOuterWidth;
            if (knownDimensions.Width.HasValue)
            {
                containerOuterWidth = knownDimensions.Width.Value;
            }
            else
            {
                var avWidth = availableSpace.Width.MaybeSub(contentBoxInset.HorizontalAxisSum());
                float intrinsicWidth = DetermineContentBasedContainerWidth(tree, items, avWidth)
                    + contentBoxInset.HorizontalAxisSum();
                containerOuterWidth = intrinsicWidth.MaybeClamp(minSize.Width, maxSize.Width).MaybeMax(paddingBorderSize.Width);
            }

            // Short-circuit if computing size and both dimensions known
            if (runMode == RunMode.ComputeSize && knownDimensions.Height.HasValue)
            {
                return LayoutOutput.FromOuterSize(new Size<float>(containerOuterWidth, knownDimensions.Height.Value));
            }

            // We can also short-circuit if the width is known and only the width has been requested.
            if (runMode == RunMode.ComputeSize && inputs.Axis == RequestedAxis.Horizontal)
            {
                return LayoutOutput.FromOuterSize(new Size<float>(containerOuterWidth, 0.0f));
            }

            float? containerPercentageResolutionHeight =
                percentageBasis.Height ?? (inputs.SizingMode == SizingMode.InherentSize ? size.Height.MaybeMax(minSize.Height) : null);

            // 3. Perform final item layout and return content height
            var resolvedPadding = rawPadding.ResolveOrZero(parentSize.Width ?? containerOuterWidth, tree.Calc);
            var resolvedBorder = rawBorder.ResolveOrZero(parentSize.Width ?? containerOuterWidth, tree.Calc);
            var resolvedContentBoxInset = resolvedPadding.Add(resolvedBorder).Add(scrollbarGutter);
            var (inflowContentSize, intrinsicOuterHeight, firstChildTopMarginSet, lastChildBottomMarginSet, firstBaseline) =
                PerformFinalLayoutOnInFlowChildren(
                    tree,
                    runMode,
                    items,
                    containerOuterWidth,
                    containerPercentageResolutionHeight,
                    contentBoxInset,
                    resolvedContentBoxInset,
                    textAlign,
                    direction,
                    ownMarginsCollapseWithChildren,
                    blockCtx, resolvedBorder, isScrollContainer, style.JustifyItems() ?? AlignItems.Normal, childrenAdjoin
                );

            // Root BFCs contain floats
            if (blockCtx.IsBfcRoot() || establishesBfc)
            {
                intrinsicOuterHeight = TaffySys.F32Max(intrinsicOuterHeight, blockCtx.FloatedContentHeightContribution());
            }

            float containerOuterHeight = (knownDimensions.Height ??
                intrinsicOuterHeight.MaybeClamp(minSize.Height, maxSize.Height))
                .MaybeMax(paddingBorderSize.Height);
            var finalOuterSize = new Size<float>(containerOuterWidth, containerOuterHeight);
            bool ownBottomCollapses = ownMarginsCollapseWithChildren.End && !(minSize.Height is { } mh && mh > 0 && mh >= containerOuterHeight);

            // Treat the entire stack of in-flow children as one alignment subject. Passing one
            // item applies the single-subject fallback for all distribution keywords.
            if (alignContent is { Keyword: not AlignContentKeyword.Normal })
            {
                float containerInnerHeight = containerOuterHeight - resolvedContentBoxInset.VerticalAxisSum();
                float inflowContentHeight = intrinsicOuterHeight - resolvedContentBoxInset.VerticalAxisSum();
                float freeSpace = containerInnerHeight - inflowContentHeight;
                bool anyInFlow = false;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].FinalLayout.HasValue)
                    {
                        anyInFlow = true;
                        break;
                    }
                }

                float groupOffset = ComputeBlockAlignContentOffset(alignContent.Value, freeSpace);
                firstBaseline = firstBaseline.MaybeAdd(groupOffset);
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (item.FinalLayout is { } layout) { layout.Location.Y += groupOffset; item.FinalLayout = layout; }
                    if (item.Position.IsOutOfFlow()) item.StaticPosition.Y += groupOffset;
                    items[i] = item;
                }
                if (anyInFlow)
                {
                    inflowContentSize = default;
                    for (int i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        if (!item.FinalLayout.HasValue) continue;

                        var layout = item.FinalLayout.Value;
                        inflowContentSize = inflowContentSize.Union(Contribution(tree, item, layout.Location,
                            layout.Size, layout.ScrollableOverflowRect, containerOuterWidth, direction, resolvedBorder, isScrollContainer));
                    }
                }
            }

            // Determine whether this node can be collapsed through
            bool allInFlowChildrenCanBeCollapsedThrough = true;
            for (int i = 0; i < items.Count; i++)
            {
                if (!items[i].Position.IsOutOfFlow() && !items[i].Float.IsFloated() && !items[i].CanBeCollapsedThrough)
                {
                    allInFlowChildrenCanBeCollapsedThrough = false;
                    break;
                }
            }
            bool canBeCollapsedThrough =
                !hasStylesPreventingBeingCollapsedThrough && containerOuterHeight == 0 && allInFlowChildrenCanBeCollapsedThrough;

            var output = new LayoutOutput
            {
                Size = finalOuterSize,
                ContentSize = SizeExtensions.ZeroF32,
                FirstBaselines = new Point<float?>(null, firstBaseline),
                TopMargin = ownMarginsCollapseWithChildren.Start
                    ? firstChildTopMarginSet
                    : CollapsibleMarginSet.FromMargin(
                        rawMargin.Top.ResolveOrZero(parentSize.Width, tree.Calc)),
                BottomMargin = ownBottomCollapses
                    ? lastChildBottomMarginSet
                    : CollapsibleMarginSet.FromMargin(
                        rawMargin.Bottom.ResolveOrZero(parentSize.Width, tree.Calc)),
                MarginsCanCollapseThrough = canBeCollapsedThrough,
            };

            // Short-circuit if computing size. Parent block containers rely on the margin-collapsing
            // outputs of their children to compute their own intrinsic height.
            if (runMode == RunMode.ComputeSize)
            {
                return output;
            }

            // Floated items commit during placement. Commit deferred in-flow layouts now that
            // block-axis alignment has been applied.
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.FinalLayout.HasValue)
                {
                    tree.SetUnroundedLayout(item.NodeId, item.FinalLayout.Value);
                }
            }

            // 4. Layout absolutely positioned children
            var absolutePositionInset = resolvedBorder.Add(scrollbarGutter);
            var absolutePositionArea = finalOuterSize.Sub(absolutePositionInset.SumAxes());
            var absolutePositionOffset = new Point<float>(absolutePositionInset.Left, absolutePositionInset.Top);
            var candidates = new OofCandidates();
            foreach (var item in items)
            {
                if (item.Position.IsOutOfFlow())
                {
                    var justify = (item.JustifySelf ?? style.JustifyItems() ?? AlignItems.Normal)
                        .ResolveSelfRelative(item.Direction, direction, true);
                    var align = (item.AlignSelf ?? AlignItems.Normal).ResolveSelfRelative(item.Direction, direction, false);
                    candidates.Push(new OofCandidate { Node = item.NodeId, Order = item.Order, Position = item.Position,
                        StaticPosition = new(AxisStaticPosition.FromAlignment(justify,
                            new(resolvedContentBoxInset.Left, containerOuterWidth - resolvedContentBoxInset.Right), direction.IsRtl()),
                            AxisStaticPosition.FromAlignment(align, new(item.StaticPosition.Y, item.StaticPosition.Y), false)) });
                }
                else
                {
                    var surfaced = item.OofCandidates;
                    if (item.FinalLayout is { } layout) surfaced.Translate(layout.Location);
                    candidates.Append(ref surfaced);
                }
            }
            output.OofCandidates = candidates;
            output.OofPositioningArea = new(absolutePositionArea, absolutePositionOffset);
            if (isScrollContainer)
            {
                inflowContentSize.Right += direction.IsRtl() ? resolvedPadding.Left : resolvedPadding.Right;
                inflowContentSize.Bottom += resolvedPadding.Bottom;
            }
            output.ScrollableOverflowRect = inflowContentSize;

            // 5. Perform hidden layout on hidden children
            int len = tree.ChildCount(nodeId);
            for (int order = 0; order < len; order++)
            {
                var child = tree.GetChildId(nodeId, order);
                if (tree.GetBlockChildStyle(child).BoxGenerationMode() == BoxGenerationMode.None)
                {
                    tree.SetUnroundedLayout(child, Layout.WithOrder((uint)order));
                    tree.PerformChildLayout(
                        child,
                        SizeExtensions.NoneF32,
                        SizeExtensions.NoneF32,
                        new Size<AvailableSpace>(AvailableSpace.MAX_CONTENT, AvailableSpace.MAX_CONTENT),
                        SizingMode.InherentSize,
                        LineExtensions.FalseLine
                    );
                }
            }

            return output;
        }

        /// <summary>
        /// Create a List of BlockItem structs where each item in the list represents a child of the current node
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static List<BlockItem> GenerateItemList(
            ILayoutBlockContainer tree,
            NodeId node,
            Size<float?> nodeInnerSize)
        {
            var items = new List<BlockItem>();
            int order = 0;

            foreach (var childNodeId in tree.ChildIds(node))
            {
                var childStyle = tree.GetBlockChildStyle(childNodeId);
                if (childStyle.BoxGenerationMode() == BoxGenerationMode.None)
                    continue;

                var aspectRatio = childStyle.AspectRatio();
                var childPadding = childStyle.Padding().ResolveOrZero(nodeInnerSize.Width, tree.Calc);
                var childBorder = childStyle.Border().ResolveOrZero(nodeInnerSize.Width, tree.Calc);
                var pbSum = childPadding.Add(childBorder).SumAxes();
                var boxSizingAdj =
                    childStyle.BoxSizing() == BoxSizing.ContentBox ? pbSum : SizeExtensions.ZeroF32;

                var position = childStyle.Position();
                var childOverflow = childStyle.Overflow();

                var floatStyle = childStyle.Float();
                bool isNotFloated = floatStyle == Float.None;

                bool isBlock = childStyle.IsBlock();
                bool isTable = childStyle.IsTable();
                bool isChildScrollContainer = childOverflow.X.IsScrollContainer() || childOverflow.Y.IsScrollContainer();

                bool isInSameBfc =
                    isBlock && !isTable && !position.IsOutOfFlow() && isNotFloated && !isChildScrollContainer && !childStyle.Contain().EstablishesIndependentFormattingContext()
                    && (childStyle.AlignContent() ?? AlignContent.Normal).Keyword == AlignContentKeyword.Normal;

                var boxSizingAdjOpt = boxSizingAdj.Map<float?>(v => v);

                items.Add(new BlockItem
                {
                    NodeId = childNodeId, SizeStyle = childStyle.Size(), IsCompressibleReplaced = childStyle.IsCompressibleReplaced(),
                    Direction = childStyle.Direction(), AlignSelf = childStyle.AlignSelf(), JustifySelf = childStyle.JustifySelf(), Contain = childStyle.Contain(),
                    Order = (uint)order,
                    IsTable = isTable,
                    IsInSameBfc = isInSameBfc,
                    Float = floatStyle,
                    Clear = childStyle.Clear(),
                    Size = childStyle
                        .Size()
                        .MaybeResolve(nodeInnerSize, tree.Calc)
                        .MaybeApplyAspectRatio(aspectRatio)
                        .MaybeAdd(boxSizingAdjOpt),
                    MinSize = childStyle
                        .MinSize()
                        .MaybeResolve(nodeInnerSize, tree.Calc)
                        .MaybeApplyAspectRatio(aspectRatio)
                        .MaybeAdd(boxSizingAdjOpt),
                    MaxSize = childStyle
                        .MaxSize()
                        .MaybeResolve(nodeInnerSize, tree.Calc)
                        .MaybeApplyAspectRatio(aspectRatio)
                        .MaybeAdd(boxSizingAdjOpt),
                    Overflow = childOverflow,
                    ScrollbarWidth = childStyle.ScrollbarWidth(),
                    Position = position,
                    Inset = childStyle.Inset(),
                    Margin = childStyle.Margin(),
                    Padding = childPadding,
                    Border = childBorder,
                    PaddingBorderSum = pbSum,

                    // Fields to be computed later (for now we initialise with dummy values)
                    ComputedSize = SizeExtensions.ZeroF32,
                    StaticPosition = PointExtensions.ZeroF32,
                    CanBeCollapsedThrough = false,
                    FinalLayout = null,
                });

                order++;
            }

            return items;
        }

        /// <summary>
        /// Compute the content-based width in the case that the width of the container is not known
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DetermineContentBasedContainerWidth(
            ILayoutBlockContainer tree,
            List<BlockItem> items,
            AvailableSpace availableWidth)
        {
            var availSpace = new Size<AvailableSpace>(availableWidth, AvailableSpace.MinContent);

            float maxChildWidth = 0.0f;
            var floatContribution = new FloatIntrinsicWidthCalculator(availableWidth);

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Position.IsOutOfFlow()) continue;

                var knownDims = item.Size.MaybeClamp(item.MinSize, item.MaxSize);

                float itemXMarginSum = item
                    .Margin
                    .ResolveOrZero(availSpace.Width.IntoOption(), tree.Calc)
                    .HorizontalAxisSum();

                float width;
                if (knownDims.Width.HasValue)
                {
                    width = knownDims.Width.Value;
                }
                else
                {
                    var keyword = SizingKeywordUtils.ResolveSizingKeyword(item.SizeStyle.Width, null, null, tree.Calc);
                    var itemWidth = keyword is { IsExact: true } exact ? AvailableSpace.Definite(exact.Size)
                        : keyword is { IsExact: false } measured ? measured.AvailableSpace : availSpace.Width.MaybeSub(itemXMarginSum);
                    width = tree.MeasureChildSize(
                        item.NodeId,
                        knownDims,
                        SizeExtensions.NoneF32,
                        new Size<AvailableSpace>(itemWidth, availSpace.Height),
                        SizingMode.InherentSize,
                        AbsoluteAxis.Horizontal,
                        item.IsInSameBfc ? LineExtensions.TrueLine : LineExtensions.FalseLine
                    );
                }

                width = TaffySys.F32Max(width, item.PaddingBorderSum.Width) + itemXMarginSum;

                var floatDir = item.Float.GetFloatDirection();
                if (floatDir.HasValue)
                {
                    floatContribution.AddFloat(width, floatDir.Value, item.Clear);
                    continue;
                }

                maxChildWidth = TaffySys.F32Max(maxChildWidth, width);
            }

            maxChildWidth = TaffySys.F32Max(maxChildWidth, floatContribution.Result());

            return maxChildWidth;
        }

        /// <summary>
        /// Compute each child's final size and position
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Rect<float> Contribution(ILayoutBlockContainer tree, BlockItem item, Point<float> location,
            Size<float> size, Rect<float> rect, float containerWidth, Direction direction, Rect<float> border, bool scroll)
        {
            var relative = new Point<float>(direction.IsRtl() ? containerWidth - (location.X + size.Width) - border.Right : location.X - border.Left,
                location.Y - border.Top);
            return ScrollableOverflow.ComputeContribution(relative, size, rect, item.Overflow, tree.GetBlockChildStyle(item.NodeId).Contain(), scroll);
        }

    }
}
