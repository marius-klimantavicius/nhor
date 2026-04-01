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
            return new BlockContext(this, 0.0f, new float[] { 0.0f, 0.0f }, new float[] { 0.0f, 0.0f }, 0.0f, true);
        }
    }

    /// <summary>
    /// Context for each individual Block within a Block Formatting Context.
    /// Contains a reference to the BlockFormattingContext + block-specific data.
    /// </summary>
    public class BlockContext
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
            return new BlockContext(
                Bfc,
                YOffset + additionalYOffset,
                newInsets,
                new float[] { newInsets[0], newInsets[1] },
                0.0f,
                false
            );
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

            FloatContentContribution = MathF.Max(FloatContentContribution, pos.Y + floatedBox.Height);

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
            FloatContentContribution = MathF.Max(FloatContentContribution, childContribution);
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
    }

    /// <summary>
    /// Computes the layout of LayoutBlockContainer according to the block layout algorithm
    /// </summary>
    public static class BlockAlgorithm
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
            }

            // Unwrap the block formatting context if one was passed, or else create a new one
            if (blockCtx != null && !isScrollContainer)
            {
                return ComputeInner(
                    tree,
                    nodeId,
                    new LayoutInput
                    {
                        KnownDimensions = styledBasedKnownDimensions,
                        ParentSize = inputs.ParentSize,
                        AvailableSpace = inputs.AvailableSpace,
                        RunMode = inputs.RunMode,
                        SizingMode = inputs.SizingMode,
                        Axis = inputs.Axis,
                        VerticalMarginsAreCollapsible = inputs.VerticalMarginsAreCollapsible,
                    },
                    blockCtx
                );
            }
            else
            {
                var rootBfc = new BlockFormattingContext();
                var rootCtx = rootBfc.RootBlockContext();
                return ComputeInner(
                    tree,
                    nodeId,
                    new LayoutInput
                    {
                        KnownDimensions = styledBasedKnownDimensions,
                        ParentSize = inputs.ParentSize,
                        AvailableSpace = inputs.AvailableSpace,
                        RunMode = inputs.RunMode,
                        SizingMode = inputs.SizingMode,
                        Axis = inputs.Axis,
                        VerticalMarginsAreCollapsible = inputs.VerticalMarginsAreCollapsible,
                    },
                    rootCtx
                );
            }
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
            var containerContentBoxSize = knownDimensions.MaybeSub(contentBoxInset.SumAxes().Map<float?>(v => v));

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

            var overflow = style.Overflow();
            bool isScrollContainer = overflow.X.IsScrollContainer() || overflow.Y.IsScrollContainer();

            // Determine margin collapsing behaviour
            var ownMarginsCollapseWithChildren = new Line<bool>(
                start: verticalMarginsAreCollapsible.Start
                    && !isScrollContainer
                    && style.Position() == Position.Relative
                    && padding.Top == 0.0f
                    && border.Top == 0.0f,
                end: verticalMarginsAreCollapsible.End
                    && !isScrollContainer
                    && style.Position() == Position.Relative
                    && padding.Bottom == 0.0f
                    && border.Bottom == 0.0f
                    && !size.Height.HasValue
            );
            bool hasStylesPreventingBeingCollapsedThrough = !style.IsBlock()
                || blockCtx.IsBfcRoot()
                || isScrollContainer
                || style.Position() == Position.Absolute
                || padding.Top > 0.0f
                || padding.Bottom > 0.0f
                || border.Top > 0.0f
                || border.Bottom > 0.0f
                || (size.Height.HasValue && size.Height.Value > 0.0f)
                || (minSize.Height.HasValue && minSize.Height.Value > 0.0f);

            var textAlign = style.TextAlign();

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

            float? containerPercentageResolutionHeight =
                knownDimensions.Height ?? size.Height.MaybeMax(minSize.Height) ?? minSize.Height;

            // 3. Perform final item layout and return content height
            var resolvedPadding = rawPadding.ResolveOrZero((float?)containerOuterWidth, tree.Calc);
            var resolvedBorder = rawBorder.ResolveOrZero((float?)containerOuterWidth, tree.Calc);
            var resolvedContentBoxInset = resolvedPadding.Add(resolvedBorder).Add(scrollbarGutter);
            var (inflowContentSize, intrinsicOuterHeight, firstChildTopMarginSet, lastChildBottomMarginSet) =
                PerformFinalLayoutOnInFlowChildren(
                    tree,
                    items,
                    containerOuterWidth,
                    containerPercentageResolutionHeight,
                    contentBoxInset,
                    resolvedContentBoxInset,
                    textAlign,
                    direction,
                    ownMarginsCollapseWithChildren,
                    blockCtx
                );

            // Root BFCs contain floats
            if (blockCtx.IsBfcRoot() || isScrollContainer)
            {
                intrinsicOuterHeight = MathF.Max(intrinsicOuterHeight, blockCtx.FloatedContentHeightContribution());
            }

            float containerOuterHeight = (knownDimensions.Height ??
                intrinsicOuterHeight.MaybeClamp(minSize.Height, maxSize.Height))
                .MaybeMax(paddingBorderSize.Height);
            var finalOuterSize = new Size<float>(containerOuterWidth, containerOuterHeight);

            // Short-circuit if computing size
            if (runMode == RunMode.ComputeSize)
            {
                return LayoutOutput.FromOuterSize(finalOuterSize);
            }

            // 4. Layout absolutely positioned children
            var absolutePositionInset = resolvedBorder.Add(scrollbarGutter);
            var absolutePositionArea = finalOuterSize.Sub(absolutePositionInset.SumAxes());
            var absolutePositionOffset = new Point<float>(absolutePositionInset.Left, absolutePositionInset.Top);
            var absoluteContentSize =
                PerformAbsoluteLayoutOnAbsoluteChildren(tree, items, absolutePositionArea, absolutePositionOffset, direction);

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

            // 7. Determine whether this node can be collapsed through
            bool allInFlowChildrenCanBeCollapsedThrough = true;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Position != Position.Absolute && !items[i].CanBeCollapsedThrough)
                {
                    allInFlowChildrenCanBeCollapsedThrough = false;
                    break;
                }
            }
            bool canBeCollapsedThrough =
                !hasStylesPreventingBeingCollapsedThrough && allInFlowChildrenCanBeCollapsedThrough;

            var contentSize = inflowContentSize.F32Max(absoluteContentSize);

            return new LayoutOutput
            {
                Size = finalOuterSize,
                ContentSize = contentSize,
                FirstBaselines = PointExtensions.NoneF32,
                TopMargin = ownMarginsCollapseWithChildren.Start
                    ? firstChildTopMarginSet
                    : CollapsibleMarginSet.FromMargin(
                        rawMargin.Top.ResolveOrZero(parentSize.Width, tree.Calc)),
                BottomMargin = ownMarginsCollapseWithChildren.End
                    ? lastChildBottomMarginSet
                    : CollapsibleMarginSet.FromMargin(
                        rawMargin.Bottom.ResolveOrZero(parentSize.Width, tree.Calc)),
                MarginsCanCollapseThrough = canBeCollapsedThrough,
            };
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
                var childPadding = childStyle.Padding().ResolveOrZero(nodeInnerSize, tree.Calc);
                var childBorder = childStyle.Border().ResolveOrZero(nodeInnerSize, tree.Calc);
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
                    isBlock && !isTable && position != Position.Absolute && isNotFloated && !isChildScrollContainer;

                var boxSizingAdjOpt = boxSizingAdj.Map<float?>(v => v);

                items.Add(new BlockItem
                {
                    NodeId = childNodeId,
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
                if (item.Position == Position.Absolute) continue;

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
                    var sizeAndBaselines = tree.PerformChildLayout(
                        item.NodeId,
                        knownDims,
                        SizeExtensions.NoneF32,
                        new Size<AvailableSpace>(availSpace.Width.MaybeSub(itemXMarginSum), availSpace.Height),
                        SizingMode.InherentSize,
                        LineExtensions.TrueLine
                    );
                    width = sizeAndBaselines.Size.Width;
                }

                width = MathF.Max(width, item.PaddingBorderSum.Width) + itemXMarginSum;

                var floatDir = item.Float.GetFloatDirection();
                if (floatDir.HasValue)
                {
                    floatContribution.AddFloat(width, floatDir.Value, item.Clear);
                    continue;
                }

                maxChildWidth = MathF.Max(maxChildWidth, width);
            }

            maxChildWidth = MathF.Max(maxChildWidth, floatContribution.Result());

            return maxChildWidth;
        }

        /// <summary>
        /// Compute each child's final size and position
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (Size<float> inflowContentSize, float intrinsicOuterHeight, CollapsibleMarginSet firstChildTopMarginSet, CollapsibleMarginSet lastChildBottomMarginSet)
            PerformFinalLayoutOnInFlowChildren(
                ILayoutBlockContainer tree,
                List<BlockItem> items,
                float containerOuterWidth,
                float? containerPercentageResolutionHeight,
                Rect<float> contentBoxInset,
                Rect<float> resolvedContentBoxInset,
                TextAlign textAlign,
                Direction direction,
                Line<bool> ownMarginsCollapseWithChildren,
                BlockContext blockCtx)
        {
            // Resolve container_inner_width for sizing child nodes using initial content_box_inset
            float containerInnerWidth = containerOuterWidth - resolvedContentBoxInset.HorizontalAxisSum();
            float? containerPctResHeight =
                containerPercentageResolutionHeight.MaybeSub(resolvedContentBoxInset.VerticalAxisSum());
            var parentSize = new Size<float?>(containerInnerWidth, containerPctResHeight);
            var availableSpace =
                new Size<AvailableSpace>(AvailableSpace.Definite(containerInnerWidth), AvailableSpace.MinContent);

            // TODO: handle nested blocks with different widths
            if (blockCtx.IsBfcRoot())
            {
                blockCtx.SetWidth(containerOuterWidth);
                blockCtx.ApplyContentBoxInset(new float[] { resolvedContentBoxInset.Left, resolvedContentBoxInset.Right });
            }

            var inflowContentSize = SizeExtensions.ZeroF32;
            float committedYOffset = resolvedContentBoxInset.Top;
            float yOffsetForAbsolute = resolvedContentBoxInset.Top;
            var firstChildTopMarginSet = CollapsibleMarginSet.ZERO;
            var activeCollapsibleMarginSet = CollapsibleMarginSet.ZERO;
            bool isCollapsingWithFirstMarginSet = true;

            bool hasActiveFloats = blockCtx.HasActiveFloats(committedYOffset);
            float yOffsetForFloat = resolvedContentBoxInset.Top;

            for (int idx = 0; idx < items.Count; idx++)
            {
                var item = items[idx];

                if (item.Position == Position.Absolute)
                {
                    float x = direction == Direction.Ltr
                        ? resolvedContentBoxInset.Left
                        : containerOuterWidth - resolvedContentBoxInset.Right;
                    item.StaticPosition = new Point<float>(x, yOffsetForAbsolute);
                    items[idx] = item;
                }
                else
                {
                    var itemMargin = item.Margin.Map(margin =>
                        margin.ResolveToOption(containerOuterWidth, tree.Calc));
                    var itemNonAutoMargin = itemMargin.Map(m => m ?? 0.0f);
                    float itemNonAutoXMarginSum = itemNonAutoMargin.HorizontalAxisSum();

                    var scrollbarSize = new Size<float>(
                        item.Overflow.Y == Overflow.Scroll ? item.ScrollbarWidth : 0.0f,
                        item.Overflow.X == Overflow.Scroll ? item.ScrollbarWidth : 0.0f
                    );

                    // Handle floated boxes
                    var floatDirection = item.Float.GetFloatDirection();
                    if (floatDirection.HasValue)
                    {
                        hasActiveFloats = true;

                        var floatLayout = tree.PerformChildLayout(
                            item.NodeId,
                            SizeExtensions.NoneF32,
                            parentSize,
                            new Size<AvailableSpace>(AvailableSpace.MAX_CONTENT, AvailableSpace.MAX_CONTENT),
                            SizingMode.InherentSize,
                            LineExtensions.TrueLine
                        );
                        var marginBox = floatLayout.Size.Add(itemNonAutoMargin.SumAxes());

                        var floatLocation = blockCtx.PlaceFloatedBox(marginBox, yOffsetForFloat, floatDirection.Value, item.Clear);

                        // Convert the margin-box location returned by float placement into a border-box location
                        // for the output Layout
                        floatLocation.Y += itemNonAutoMargin.Top;
                        floatLocation.X += itemNonAutoMargin.Left;

                        tree.SetUnroundedLayout(
                            item.NodeId,
                            new Layout
                            {
                                Order = item.Order,
                                Size = floatLayout.Size,
                                ContentSize = floatLayout.ContentSize,
                                ScrollbarSize = scrollbarSize,
                                Location = floatLocation,
                                Padding = item.Padding,
                                Border = item.Border,
                                Margin = itemNonAutoMargin,
                            }
                        );

                        inflowContentSize = inflowContentSize.F32Max(
                            ContentSizeUtils.ComputeContentSizeContribution(
                                floatLocation,
                                floatLayout.Size,
                                floatLayout.ContentSize,
                                item.Overflow
                            )
                        );

                        continue;
                    }

                    // Handle non-floated boxes

                    float yMarginOffset = 0.0f;

                    float stretchWidth;
                    Point<float> floatAvoidingPosition;
                    float floatAvoidingWidth;

                    if (item.IsInSameBfc)
                    {
                        stretchWidth = containerInnerWidth - itemNonAutoXMarginSum;
                        floatAvoidingPosition = new Point<float>(0.0f, 0.0f);
                        floatAvoidingWidth = 0.0f;
                    }
                    else
                    {
                        // Set y_margin_offset (different bfc child)
                        if (!isCollapsingWithFirstMarginSet || !ownMarginsCollapseWithChildren.Start)
                        {
                            yMarginOffset =
                                activeCollapsibleMarginSet.CollapseWithMargin(itemNonAutoMargin.Top).Resolve();
                        }
                        float minY = committedYOffset + yMarginOffset;

                        if (hasActiveFloats)
                        {
                            var slot = blockCtx.FindContentSlot(minY, item.Clear, null);
                            hasActiveFloats = slot.SegmentId.HasValue;
                            stretchWidth = slot.Width - itemNonAutoXMarginSum;
                            floatAvoidingPosition = new Point<float>(slot.X, slot.Y);
                            floatAvoidingWidth = slot.Width;
                        }
                        else
                        {
                            stretchWidth = containerInnerWidth - itemNonAutoXMarginSum;
                            floatAvoidingPosition = new Point<float>(resolvedContentBoxInset.Left, minY);
                            floatAvoidingWidth = containerInnerWidth;
                        }
                    }

                    Size<float?> knownDimensions;
                    if (item.IsTable)
                    {
                        knownDimensions = SizeExtensions.NoneF32;
                    }
                    else
                    {
                        knownDimensions = item.Size
                            .MapWidth(w =>
                            {
                                // TODO: Allow stretch-sizing to be conditional, as there are exceptions.
                                // e.g. Table children of blocks do not stretch fit
                                return (float?)((w ?? stretchWidth).MaybeClamp(item.MinSize.Width, item.MaxSize.Width));
                            })
                            .MaybeClamp(item.MinSize, item.MaxSize);
                    }

                    var layoutInputs = new LayoutInput
                    {
                        RunMode = RunMode.PerformLayout,
                        SizingMode = SizingMode.InherentSize,
                        Axis = RequestedAxis.Both,
                        KnownDimensions = knownDimensions,
                        ParentSize = parentSize,
                        AvailableSpace = new Size<AvailableSpace>(AvailableSpace.Definite(stretchWidth), availableSpace.Height),
                        VerticalMarginsAreCollapsible = item.IsInSameBfc ? LineExtensions.TrueLine : LineExtensions.FalseLine,
                    };

                    float clearPos = blockCtx.ClearedThreshold(item.Clear) ?? 0.0f;

                    LayoutOutput itemLayout;
                    if (item.IsInSameBfc)
                    {
                        float width = knownDimensions.Width
                            ?? throw new InvalidOperationException("Same-bfc child will always have defined width due to stretch sizing");

                        // TODO: account for auto margins
                        float insetLeft = itemNonAutoMargin.Left + contentBoxInset.Left;
                        float insetRight = containerOuterWidth - width - insetLeft;
                        var insets = new float[] { insetLeft, insetRight };

                        // Compute child layout
                        var childBlockCtx =
                            blockCtx.SubContext(MathF.Max(yOffsetForAbsolute + itemNonAutoMargin.Top, clearPos), insets);
                        itemLayout = tree.ComputeBlockChildLayout(item.NodeId, layoutInputs, childBlockCtx);

                        // Extract float contribution from child block context
                        float childContribution = childBlockCtx.FloatedContentHeightContribution();
                        blockCtx.AddChildFloatedContentHeightContribution(yOffsetForAbsolute + childContribution);
                    }
                    else
                    {
                        itemLayout = tree.ComputeChildLayout(item.NodeId, layoutInputs);
                    }
                    var finalSize = itemLayout.Size;

                    var topMarginSet = itemLayout.TopMargin.CollapseWithMargin(itemMargin.Top ?? 0.0f);
                    var bottomMarginSet = itemLayout.BottomMargin.CollapseWithMargin(itemMargin.Bottom ?? 0.0f);

                    // Expand auto margins to fill available space
                    // Note: Vertical auto-margins for relatively positioned block items simply resolve to 0.
                    // See: https://www.w3.org/TR/CSS21/visudet.html#abs-non-replaced-width
                    float freeXSpace = MathF.Max(0.0f, stretchWidth - finalSize.Width);
                    float xAxisAutoMarginSize;
                    {
                        int autoMarginCount = (!itemMargin.Left.HasValue ? 1 : 0) + (!itemMargin.Right.HasValue ? 1 : 0);
                        xAxisAutoMarginSize = autoMarginCount > 0 ? freeXSpace / autoMarginCount : 0.0f;
                    }
                    var resolvedMargin = new Rect<float>(
                        itemMargin.Left ?? xAxisAutoMarginSize,
                        itemMargin.Right ?? xAxisAutoMarginSize,
                        topMarginSet.Resolve(),
                        bottomMarginSet.Resolve()
                    );

                    // Resolve item inset
                    var insetRect = item.Inset.ZipSize(
                        new Size<float>(containerInnerWidth, 0.0f),
                        (p, s) => p.MaybeResolve(s, tree.Calc)
                    );
                    var insetOffset = new Point<float>(
                        direction.IsRtl()
                            ? (insetRect.Right.HasValue ? -insetRect.Right.Value : insetRect.Left ?? 0.0f)
                            : (insetRect.Left ?? (insetRect.Right.HasValue ? -insetRect.Right.Value : 0.0f)),
                        insetRect.Top ?? (insetRect.Bottom.HasValue ? -insetRect.Bottom.Value : 0.0f)
                    );

                    // Set y_margin_offset (same bfc child)
                    if (item.IsInSameBfc
                        && (!isCollapsingWithFirstMarginSet || !ownMarginsCollapseWithChildren.Start))
                    {
                        yMarginOffset = activeCollapsibleMarginSet.CollapseWithMargin(resolvedMargin.Top).Resolve();
                    }

                    bool floatOrNotClear = item.Float.IsFloated() || item.Clear == Clear.None;

                    item.ComputedSize = itemLayout.Size;
                    item.CanBeCollapsedThrough = itemLayout.MarginsCanCollapseThrough && floatOrNotClear;
                    if (item.IsInSameBfc)
                    {
                        float unclearedY = committedYOffset + activeCollapsibleMarginSet.Resolve();
                        item.StaticPosition = new Point<float>(
                            direction == Direction.Ltr
                                ? resolvedContentBoxInset.Left
                                : containerOuterWidth - resolvedContentBoxInset.Right - finalSize.Width,
                            MathF.Max(unclearedY, clearPos));
                    }
                    else
                    {
                        item.StaticPosition = new Point<float>(
                            direction == Direction.Ltr
                                ? floatAvoidingPosition.X
                                : floatAvoidingPosition.X + floatAvoidingWidth - finalSize.Width,
                            floatAvoidingPosition.Y);
                    }

                    Point<float> location;
                    if (item.IsInSameBfc)
                    {
                        float locationX = direction == Direction.Ltr
                            ? resolvedContentBoxInset.Left + insetOffset.X + resolvedMargin.Left
                            : containerOuterWidth - resolvedContentBoxInset.Right - finalSize.Width - resolvedMargin.Right + insetOffset.X;
                        location = new Point<float>(
                            locationX,
                            MathF.Max(committedYOffset, clearPos) + insetOffset.Y + yMarginOffset
                        );
                    }
                    else
                    {
                        // TODO: handle inset and margins
                        float locationX = direction == Direction.Ltr
                            ? floatAvoidingPosition.X + resolvedMargin.Left + insetOffset.X
                            : floatAvoidingPosition.X + floatAvoidingWidth - finalSize.Width - resolvedMargin.Right + insetOffset.X;
                        location = new Point<float>(
                            locationX,
                            floatAvoidingPosition.Y + insetOffset.Y
                        );
                    }

                    // Apply alignment
                    float itemOuterWidth = itemLayout.Size.Width + resolvedMargin.HorizontalAxisSum();
                    if (itemOuterWidth < containerInnerWidth)
                    {
                        float alignmentFreeSpace = containerInnerWidth - itemOuterWidth;
                        switch (textAlign, direction)
                        {
                            case (TextAlign.Auto, _):
                                // Do nothing
                                break;
                            case (TextAlign.LegacyLeft, Direction.Ltr):
                                // Do nothing. Left aligned by default.
                                break;
                            case (TextAlign.LegacyLeft, Direction.Rtl):
                                location.X -= alignmentFreeSpace;
                                break;
                            case (TextAlign.LegacyRight, Direction.Ltr):
                                location.X += alignmentFreeSpace;
                                break;
                            case (TextAlign.LegacyRight, Direction.Rtl):
                                // Do nothing. Right aligned by default.
                                break;
                            case (TextAlign.LegacyCenter, Direction.Ltr):
                                location.X += alignmentFreeSpace / 2.0f;
                                break;
                            case (TextAlign.LegacyCenter, Direction.Rtl):
                                location.X -= alignmentFreeSpace / 2.0f;
                                break;
                        }
                    }

                    tree.SetUnroundedLayout(
                        item.NodeId,
                        new Layout
                        {
                            Order = item.Order,
                            Size = itemLayout.Size,
                            ContentSize = itemLayout.ContentSize,
                            ScrollbarSize = scrollbarSize,
                            Location = location,
                            Padding = item.Padding,
                            Border = item.Border,
                            Margin = resolvedMargin,
                        }
                    );

                    inflowContentSize = inflowContentSize.F32Max(
                        ContentSizeUtils.ComputeContentSizeContribution(
                            new Point<float>(location.X - resolvedContentBoxInset.Left, location.Y - resolvedContentBoxInset.Top),
                            finalSize,
                            itemLayout.ContentSize,
                            item.Overflow
                        )
                    );

                    // Update first_child_top_margin_set
                    if (isCollapsingWithFirstMarginSet)
                    {
                        if (item.CanBeCollapsedThrough)
                        {
                            firstChildTopMarginSet = firstChildTopMarginSet
                                .CollapseWithSet(topMarginSet)
                                .CollapseWithSet(bottomMarginSet);
                        }
                        else
                        {
                            firstChildTopMarginSet = firstChildTopMarginSet.CollapseWithSet(topMarginSet);
                            isCollapsingWithFirstMarginSet = false;
                        }
                    }

                    // Update active_collapsible_margin_set
                    if (item.CanBeCollapsedThrough)
                    {
                        activeCollapsibleMarginSet = activeCollapsibleMarginSet
                            .CollapseWithSet(topMarginSet)
                            .CollapseWithSet(bottomMarginSet);
                        yOffsetForAbsolute = committedYOffset + itemLayout.Size.Height + yMarginOffset;
                        yOffsetForFloat = committedYOffset + itemLayout.Size.Height + yMarginOffset;
                    }
                    else
                    {
                        committedYOffset = location.Y - insetOffset.Y + itemLayout.Size.Height;
                        activeCollapsibleMarginSet = bottomMarginSet;
                        yOffsetForAbsolute = committedYOffset + activeCollapsibleMarginSet.Resolve();
                        yOffsetForFloat = committedYOffset;
                    }

                    items[idx] = item;
                }
            }

            var lastChildBottomMarginSet = activeCollapsibleMarginSet;
            float bottomYMarginOffset =
                ownMarginsCollapseWithChildren.End ? 0.0f : lastChildBottomMarginSet.Resolve();

            committedYOffset += resolvedContentBoxInset.Bottom + bottomYMarginOffset;
            float contentHeight = MathF.Max(0.0f, committedYOffset);
            return (inflowContentSize, contentHeight, firstChildTopMarginSet, lastChildBottomMarginSet);
        }

        /// <summary>
        /// Perform absolute layout on all absolutely positioned children.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Size<float> PerformAbsoluteLayoutOnAbsoluteChildren(
            ILayoutBlockContainer tree,
            List<BlockItem> items,
            Size<float> areaSize,
            Point<float> areaOffset,
            Direction direction)
        {
            float areaWidth = areaSize.Width;
            float areaHeight = areaSize.Height;

            var absoluteContentSize = SizeExtensions.ZeroF32;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Position != Position.Absolute) continue;

                var childStyle = tree.GetBlockChildStyle(item.NodeId);

                // Skip items that are display:none or are not position:absolute
                if (childStyle.BoxGenerationMode() == BoxGenerationMode.None || childStyle.Position() != Position.Absolute)
                {
                    continue;
                }

                var aspectRatio = childStyle.AspectRatio();
                var margin = childStyle.Margin().Map(m =>
                    m.ResolveToOption(areaWidth, tree.Calc));
                var childPadding = childStyle.Padding().ResolveOrZero((float?)areaWidth, tree.Calc);
                var childBorder = childStyle.Border().ResolveOrZero((float?)areaWidth, tree.Calc);
                var paddingBorderSum = childPadding.Add(childBorder).SumAxes();
                var boxSizingAdj =
                    childStyle.BoxSizing() == BoxSizing.ContentBox ? paddingBorderSum : SizeExtensions.ZeroF32;

                // Resolve inset
                var childInset = childStyle.Inset();
                float? left = childInset.Left.MaybeResolve(areaWidth, tree.Calc);
                float? right = childInset.Right.MaybeResolve(areaWidth, tree.Calc);
                float? top = childInset.Top.MaybeResolve(areaHeight, tree.Calc);
                float? bottom = childInset.Bottom.MaybeResolve(areaHeight, tree.Calc);

                // Compute known dimensions from min/max/inherent size styles
                var boxSizingAdjOpt = boxSizingAdj.Map<float?>(v => v);
                var styleSize = childStyle
                    .Size()
                    .MaybeResolve(areaSize.Map<float?>(v => v), tree.Calc)
                    .MaybeApplyAspectRatio(aspectRatio)
                    .MaybeAdd(boxSizingAdjOpt);
                var minSizeAbs = childStyle
                    .MinSize()
                    .MaybeResolve(areaSize.Map<float?>(v => v), tree.Calc)
                    .MaybeApplyAspectRatio(aspectRatio)
                    .MaybeAdd(boxSizingAdjOpt)
                    .Or(paddingBorderSum.Map<float?>(v => v))
                    .MaybeMax(paddingBorderSum.Map<float?>(v => v));
                var maxSizeAbs = childStyle
                    .MaxSize()
                    .MaybeResolve(areaSize.Map<float?>(v => v), tree.Calc)
                    .MaybeApplyAspectRatio(aspectRatio)
                    .MaybeAdd(boxSizingAdjOpt);
                var knownDims = styleSize.MaybeClamp(minSizeAbs, maxSizeAbs);

                // Fill in width from left/right and reapply aspect ratio if:
                //   - Width is not already known
                //   - Item has both left and right inset properties set
                if (!knownDims.Width.HasValue && left.HasValue && right.HasValue)
                {
                    float newWidthRaw = areaWidth.MaybeSub(margin.Left).MaybeSub(margin.Right) - left.Value - right.Value;
                    knownDims.Width = MathF.Max(newWidthRaw, 0.0f);
                    knownDims = knownDims.MaybeApplyAspectRatio(aspectRatio).MaybeClamp(minSizeAbs, maxSizeAbs);
                }

                // Fill in height from top/bottom and reapply aspect ratio if:
                //   - Height is not already known
                //   - Item has both top and bottom inset properties set
                if (!knownDims.Height.HasValue && top.HasValue && bottom.HasValue)
                {
                    float newHeightRaw = areaHeight.MaybeSub(margin.Top).MaybeSub(margin.Bottom) - top.Value - bottom.Value;
                    knownDims.Height = MathF.Max(newHeightRaw, 0.0f);
                    knownDims = knownDims.MaybeApplyAspectRatio(aspectRatio).MaybeClamp(minSizeAbs, maxSizeAbs);
                }

                var measuredSize = tree.MeasureChildSizeBoth(
                    item.NodeId,
                    knownDims,
                    areaSize.Map<float?>(v => v),
                    new Size<AvailableSpace>(
                        AvailableSpace.Definite(areaWidth.MaybeClamp(minSizeAbs.Width, maxSizeAbs.Width)),
                        AvailableSpace.Definite(areaHeight.MaybeClamp(minSizeAbs.Height, maxSizeAbs.Height))
                    ),
                    SizingMode.ContentSize,
                    LineExtensions.FalseLine
                );

                var finalSize = knownDims.UnwrapOr(measuredSize).MaybeClamp(minSizeAbs, maxSizeAbs);

                var layoutOutput = tree.PerformChildLayout(
                    item.NodeId,
                    finalSize.Map<float?>(v => v),
                    areaSize.Map<float?>(v => v),
                    new Size<AvailableSpace>(
                        AvailableSpace.Definite(areaWidth.MaybeClamp(minSizeAbs.Width, maxSizeAbs.Width)),
                        AvailableSpace.Definite(areaHeight.MaybeClamp(minSizeAbs.Height, maxSizeAbs.Height))
                    ),
                    SizingMode.ContentSize,
                    LineExtensions.FalseLine
                );

                var nonAutoMargin = new Rect<float>(
                    left.HasValue ? margin.Left ?? 0.0f : 0.0f,
                    right.HasValue ? margin.Right ?? 0.0f : 0.0f,
                    top.HasValue ? margin.Top ?? 0.0f : 0.0f,
                    bottom.HasValue ? margin.Bottom ?? 0.0f : 0.0f
                );

                // Expand auto margins to fill available space
                // https://www.w3.org/TR/CSS21/visudet.html#abs-non-replaced-width
                Rect<float> autoMargin;
                {
                    // Auto margins for absolutely positioned elements in block containers only resolve
                    // if inset is set. Otherwise they resolve to 0.
                    var absoluteAutoMarginSpace = new Point<float>(
                        right.HasValue ? areaSize.Width - right.Value - (left ?? 0.0f) : finalSize.Width,
                        bottom.HasValue ? areaSize.Height - bottom.Value - (top ?? 0.0f) : finalSize.Height
                    );
                    var freeSpace = new Size<float>(
                        absoluteAutoMarginSpace.X - finalSize.Width - nonAutoMargin.HorizontalAxisSum(),
                        absoluteAutoMarginSpace.Y - finalSize.Height - nonAutoMargin.VerticalAxisSum()
                    );

                    float autoMarginWidth;
                    {
                        int autoMarginCount = (!margin.Left.HasValue ? 1 : 0) + (!margin.Right.HasValue ? 1 : 0);
                        if (autoMarginCount == 2
                            && (!styleSize.Width.HasValue || styleSize.Width.Value >= freeSpace.Width))
                        {
                            autoMarginWidth = 0.0f;
                        }
                        else if (autoMarginCount > 0)
                        {
                            autoMarginWidth = freeSpace.Width / autoMarginCount;
                        }
                        else
                        {
                            autoMarginWidth = 0.0f;
                        }
                    }
                    float autoMarginHeight;
                    {
                        int autoMarginCount = (!margin.Top.HasValue ? 1 : 0) + (!margin.Bottom.HasValue ? 1 : 0);
                        if (autoMarginCount == 2
                            && (!styleSize.Height.HasValue || styleSize.Height.Value >= freeSpace.Height))
                        {
                            autoMarginHeight = 0.0f;
                        }
                        else if (autoMarginCount > 0)
                        {
                            autoMarginHeight = freeSpace.Height / autoMarginCount;
                        }
                        else
                        {
                            autoMarginHeight = 0.0f;
                        }
                    }

                    autoMargin = new Rect<float>(
                        margin.Left.HasValue ? 0.0f : autoMarginWidth,
                        margin.Right.HasValue ? 0.0f : autoMarginWidth,
                        margin.Top.HasValue ? 0.0f : autoMarginHeight,
                        margin.Bottom.HasValue ? 0.0f : autoMarginHeight
                    );
                }

                var resolvedMargin = new Rect<float>(
                    margin.Left ?? autoMargin.Left,
                    margin.Right ?? autoMargin.Right,
                    margin.Top ?? autoMargin.Top,
                    margin.Bottom ?? autoMargin.Bottom
                );

                float xOffset;
                if (left.HasValue && right.HasValue)
                {
                    xOffset = direction.IsRtl()
                        ? areaSize.Width - finalSize.Width - right.Value - resolvedMargin.Right
                        : left.Value + resolvedMargin.Left;
                }
                else if (left.HasValue)
                {
                    xOffset = left.Value + resolvedMargin.Left;
                }
                else if (right.HasValue)
                {
                    xOffset = areaSize.Width - finalSize.Width - right.Value - resolvedMargin.Right;
                }
                else
                {
                    xOffset = direction.IsRtl()
                        ? item.StaticPosition.X - finalSize.Width - resolvedMargin.Right - areaOffset.X
                        : item.StaticPosition.X + resolvedMargin.Left - areaOffset.X;
                }

                var location = new Point<float>(
                    x: xOffset + areaOffset.X,
                    y: top.HasValue
                        ? (top.Value + resolvedMargin.Top + areaOffset.Y)
                        : bottom.HasValue
                            ? (areaSize.Height - finalSize.Height - bottom.Value - resolvedMargin.Bottom + areaOffset.Y)
                            : (item.StaticPosition.Y + resolvedMargin.Top)
                );

                // Note: axis intentionally switched here as scrollbars take up space in the opposite axis
                // to the axis in which scrolling is enabled.
                var scrollbarSize = new Size<float>(
                    item.Overflow.Y == Overflow.Scroll ? item.ScrollbarWidth : 0.0f,
                    item.Overflow.X == Overflow.Scroll ? item.ScrollbarWidth : 0.0f
                );

                tree.SetUnroundedLayout(
                    item.NodeId,
                    new Layout
                    {
                        Order = item.Order,
                        Size = finalSize,
                        ContentSize = layoutOutput.ContentSize,
                        ScrollbarSize = scrollbarSize,
                        Location = location,
                        Padding = childPadding,
                        Border = childBorder,
                        Margin = resolvedMargin,
                    }
                );

                var relativeLocation = new Point<float>(location.X - areaOffset.X, location.Y - areaOffset.Y);
                absoluteContentSize = absoluteContentSize.F32Max(
                    ContentSizeUtils.ComputeContentSizeContribution(
                        relativeLocation,
                        finalSize,
                        layoutOutput.ContentSize,
                        item.Overflow
                    )
                );
            }

            return absoluteContentSize;
        }
    }
}
