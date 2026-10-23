// Ported from perform_final_layout_on_in_flow_children in src/compute/block.rs at 802e497b.
using System;
using System.Collections.Generic;
using static Marius.Winter.Taffy.TaffySys;

namespace Marius.Winter.Taffy;

public static partial class BlockAlgorithm
{
    public static float ComputeBlockAlignContentOffset(AlignContent alignment, float freeSpace) =>
        AlignmentUtils.ComputeAlignmentOffset(freeSpace, 1, 0, AlignmentUtils.ApplyAlignmentFallback(freeSpace, 1, alignment), false, true);

    private static float? ResolveStretchHeight(Dimension style, float? parentHeight, Rect<float> margin, Line<bool> zeroMargins)
        => style.IsStretch() && parentHeight.HasValue ? parentHeight - (zeroMargins.Start ? 0 : margin.Top) - (zeroMargins.End ? 0 : margin.Bottom) : null;
    private static Point<float> RelativeOffset(BlockItem item, Size<float?> parent, Direction direction, ILayoutPartialTree tree)
    {
        if (item.Position != Position.Relative) return default;
        var inset = item.Inset.ZipSize(parent, (p, s) => p.MaybeResolve(s, tree.Calc));
        return new(direction.IsRtl() ? -inset.Right ?? inset.Left ?? 0 : inset.Left ?? -inset.Right ?? 0, inset.Top ?? -inset.Bottom ?? 0);
    }
    private static (Rect<float> inflowContentSize, float intrinsicOuterHeight, CollapsibleMarginSet firstChildTopMarginSet,
        CollapsibleMarginSet lastChildBottomMarginSet, float? firstBaseline) PerformFinalLayoutOnInFlowChildren(
        ILayoutBlockContainer tree, RunMode runMode, List<BlockItem> items, float containerOuterWidth, float? containerPercentageResolutionHeight,
        Rect<float> contentBoxInset, Rect<float> resolvedContentBoxInset, TextAlign textAlign, Direction direction,
        Line<bool> ownMarginsCollapseWithChildren, BlockContext blockCtx, Rect<float> resolvedBorder, bool isScrollContainer,
        AlignItems justifyItems, Line<bool> childrenAdjoin)
    {
        float innerWidth = F32Max(containerOuterWidth - resolvedContentBoxInset.HorizontalAxisSum(), 0);
        float? innerHeight = containerPercentageResolutionHeight.MaybeSub(resolvedContentBoxInset.VerticalAxisSum());
        var parent = new Size<float?>(innerWidth, innerHeight);
        if (blockCtx.IsBfcRoot()) { blockCtx.SetWidth(containerOuterWidth); blockCtx.ApplyContentBoxInset([resolvedContentBoxInset.Left, resolvedContentBoxInset.Right]); }
        if (!ownMarginsCollapseWithChildren.Start) blockCtx.CommitStrut();
        Rect<float> overflowRect = default;
        float committedY = resolvedContentBoxInset.Top, absoluteY = committedY;
        var firstMargins = CollapsibleMarginSet.ZERO; var activeMargins = CollapsibleMarginSet.ZERO;
        bool collapsingFirst = true, activeHasClearance = false, activeFloats = blockCtx.HasActiveFloats(committedY);
        float? firstBaseline = null;
        for (int index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.Position.IsOutOfFlow())
            {
                item.StaticPosition = new(direction.IsRtl() ? containerOuterWidth - resolvedContentBoxInset.Right : resolvedContentBoxInset.Left, absoluteY);
                items[index] = item; continue;
            }
            var margin = item.Margin.Map(m => m.ResolveToOption(innerWidth, tree.Calc));
            var nonauto = margin.Map(v => v ?? 0);
            float marginX = nonauto.HorizontalAxisSum();
            var scrollbars = new Size<float>(item.Overflow.Y == Overflow.Scroll ? item.ScrollbarWidth : 0, item.Overflow.X == Overflow.Scroll ? item.ScrollbarWidth : 0);
            Layout MakeLayout(LayoutOutput output, Point<float> position, Rect<float> resolvedMargin) => new() { Order = item.Order, Size = output.Size,
                ScrollableOverflowRect = output.ScrollableOverflowRect, ScrollbarSize = scrollbars, Location = position,
                Padding = item.Padding, Border = item.Border, Margin = resolvedMargin };
            if (item.Float.GetFloatDirection() is { } floatDirection)
            {
                activeFloats = true;
                float availableWidth = F32Max(innerWidth - marginX, 0);
                var keyword = SizingKeywordUtils.ResolveSizingKeyword(item.SizeStyle.Width, availableWidth, innerWidth, tree.Calc);
                float? knownWidth = keyword is { IsExact: true } exact ? exact.Size : null;
                var space = keyword is { IsExact: false } measured ? measured.AvailableSpace : AvailableSpace.Definite(knownWidth ?? availableWidth);
                float? knownHeight = ResolveStretchHeight(item.SizeStyle.Height, innerHeight, nonauto, LineExtensions.FalseLine);
                var output = tree.PerformChildLayout(item.NodeId, new(knownWidth, knownHeight), parent,
                    new(space, AvailableSpace.MaxContent), SizingMode.InherentSize, LineExtensions.FalseLine);
                item.OofCandidates = output.OofCandidates.Take();
                var outer = output.Size.Add(nonauto.SumAxes());
                bool adjoining = collapsingFirst && ownMarginsCollapseWithChildren.Start;
                float minY = adjoining ? committedY : committedY + activeMargins.Resolve();
                var position = blockCtx.PlaceFloatedBox(item.NodeId.Value, outer, minY, floatDirection, item.Clear, adjoining);
                position.X += nonauto.Left; position.Y += nonauto.Top;
                var relative = RelativeOffset(item, parent, direction, tree); position.X += relative.X; position.Y += relative.Y;
                item.FinalLayout = MakeLayout(output, position, nonauto);
                overflowRect = overflowRect.Union(Contribution(tree, item, position, output.Size, output.ScrollableOverflowRect,
                    containerOuterWidth, direction, resolvedBorder, isScrollContainer));
                items[index] = item; continue;
            }
            float yMargin = 0, stretchWidth, slotWidth;
            Point<float> slotPosition;
            bool avoidsFloats = false, pushedBelow = false;
            if (item.IsInSameBfc) { stretchWidth = F32Max(innerWidth - marginX, 0); slotWidth = 0; slotPosition = default; }
            else
            {
                if (!collapsingFirst || !ownMarginsCollapseWithChildren.Start) yMargin = activeMargins.CollapseWithMargin(nonauto.Top).Resolve();
                float minY = committedY + yMargin;
                if (activeFloats || blockCtx.HasActiveFloats(minY))
                {
                    int? segment = null; BfcSlot slot;
                    while (true)
                    {
                        slot = blockCtx.FindBfcSlot(minY, [nonauto.Left, nonauto.Right], direction, item.Clear, segment);
                        if (!slot.SegmentId.HasValue) break;
                        float width = (item.Size.Width ?? F32Max(F32Max(slot.StretchWidth, -marginX), 0)).MaybeClamp(item.MinSize.Width, item.MaxSize.Width);
                        if (width <= slot.BorderWidth + FloatContext.FIT_TOLERANCE) break;
                        segment = slot.SegmentId;
                    }
                    pushedBelow = slot.Y > minY; activeFloats = slot.SegmentId.HasValue; avoidsFloats = true;
                    stretchWidth = F32Max(F32Max(slot.StretchWidth, -marginX), 0); slotPosition = new(slot.X, slot.Y); slotWidth = slot.BorderWidth;
                }
                else { stretchWidth = F32Max(innerWidth - marginX, 0); slotPosition = new(resolvedContentBoxInset.Left, minY); slotWidth = innerWidth; }
            }
            var justify = (item.JustifySelf ?? justifyItems).ResolveSelfRelative(item.Direction, direction, true);
            bool explicitStretch = justify.Keyword == AlignItemsKeyword.Stretch;
            AlignItems? nonStretch = justify.Keyword is AlignItemsKeyword.Normal or AlignItemsKeyword.Stretch ? null : justify;
            Size<float?> known;
            if ((item.IsTable || item.IsCompressibleReplaced) && !explicitStretch) known = SizeExtensions.NoneF32;
            else
            {
                var widthStyle = nonStretch.HasValue && item.SizeStyle.Width.IsAuto() ? Dimension.FitContent() : item.SizeStyle.Width;
                var keyword = SizingKeywordUtils.ResolveSizingKeyword(widthStyle, stretchWidth, innerWidth, tree.Calc);
                float? width = keyword is { IsExact: true } exact ? exact.Size : keyword is { IsExact: false } measured
                    ? tree.MeasureChildSize(item.NodeId, SizeExtensions.NoneF32, parent, new(measured.AvailableSpace, AvailableSpace.MaxContent),
                        SizingMode.InherentSize, AbsoluteAxis.Horizontal, item.IsInSameBfc ? LineExtensions.TrueLine : LineExtensions.FalseLine) : null;
                known = item.Size;
                known.Width = (known.Width ?? width ?? stretchWidth).MaybeClamp(item.MinSize.Width, item.MaxSize.Width);
                known.Height ??= ResolveStretchHeight(item.SizeStyle.Height, innerHeight, nonauto, childrenAdjoin);
                known = known.MaybeClamp(item.MinSize, item.MaxSize);
            }
            var inputs = new LayoutInput { RunMode = runMode, SizingMode = SizingMode.InherentSize, Axis = RequestedAxis.Both,
                KnownDimensions = known, KnownDimensionsAreDefinite = new(true, true), ParentSize = parent,
                AvailableSpace = new(AvailableSpace.Definite(stretchWidth), AvailableSpace.MaxContent),
                VerticalMarginsAreCollapsible = item.IsInSameBfc ? LineExtensions.TrueLine : LineExtensions.FalseLine };
            float? threshold = blockCtx.ClearedThreshold(item.Clear);
            float clearPos = threshold ?? float.NegativeInfinity, childFloats = float.NegativeInfinity;
            LayoutOutput itemOutput;
            if (item.IsInSameBfc)
            {
                float width = F32Max(known.Width ?? stretchWidth, item.PaddingBorderSum.Width);
                float left = nonauto.Left + contentBoxInset.Left, right = containerOuterWidth - width - left;
                float estimatedY = committedY + activeMargins.CollapseWithMargin(nonauto.Top).Resolve();
                var context = blockCtx.SubContext(F32Max(estimatedY, clearPos), [left, right]);
                itemOutput = tree.ComputeBlockChildLayout(item.NodeId, inputs, context);
                childFloats = context.FloatedContentHeightContribution(); blockCtx.MergeAdjoiningFloats(context.TopAdjoiningFloats());
            }
            else itemOutput = tree.ComputeChildLayout(item.NodeId, inputs);
            item.OofCandidates = itemOutput.OofCandidates.Take();
            var size = itemOutput.Size;
            var top = itemOutput.TopMargin.CollapseWithMargin(nonauto.Top);
            var bottom = itemOutput.BottomMargin.CollapseWithMargin(nonauto.Bottom);
            float free = F32Max(0, stretchWidth - size.Width);
            int autoCount = (margin.Left.HasValue ? 0 : 1) + (margin.Right.HasValue ? 0 : 1);
            float autoMargin = autoCount > 0 ? free / autoCount : 0;
            var resolved = new Rect<float>(margin.Left ?? autoMargin, margin.Right ?? autoMargin, nonauto.Top, nonauto.Bottom);
            var offset = RelativeOffset(item, parent, direction, tree);
            if (item.IsInSameBfc && (!collapsingFirst || !ownMarginsCollapseWithChildren.Start)) yMargin = activeMargins.CollapseWithSet(top).Resolve();
            bool clearance = false;
            if (item.IsInSameBfc && threshold is { } clear)
            {
                float hypothetical = committedY + activeMargins.CollapseWithSet(top).Resolve();
                if (blockCtx.HasAdjoiningFloat(item.Clear) || hypothetical < clear)
                {
                    clearance = true;
                    float escaped = collapsingFirst && ownMarginsCollapseWithChildren.Start ? activeMargins.Resolve() : 0;
                    yMargin = clear - committedY - escaped;
                }
            }
            item.ComputedSize = size; item.CanBeCollapsedThrough = itemOutput.MarginsCanCollapseThrough && !clearance;
            item.StaticPosition = item.IsInSameBfc ? new(direction.IsRtl() ? containerOuterWidth - resolvedContentBoxInset.Right - size.Width : resolvedContentBoxInset.Left,
                F32Max(committedY + activeMargins.Resolve(), clearPos)) : new(direction.IsRtl() ? slotPosition.X + slotWidth - size.Width : slotPosition.X, slotPosition.Y);
            Point<float> location;
            if (item.IsInSameBfc)
                location = new(direction.IsRtl() ? containerOuterWidth - resolvedContentBoxInset.Right - size.Width - resolved.Right + offset.X
                    : resolvedContentBoxInset.Left + offset.X + resolved.Left, committedY + yMargin + offset.Y);
            else
            {
                float extraLeft = avoidsFloats ? resolved.Left - nonauto.Left : resolved.Left;
                float extraRight = avoidsFloats ? resolved.Right - nonauto.Right : resolved.Right;
                location = new(direction.IsRtl() ? slotPosition.X + slotWidth - size.Width - extraRight + offset.X : slotPosition.X + extraLeft + offset.X,
                    slotPosition.Y + offset.Y);
            }
            if (item.IsInSameBfc) blockCtx.AddChildFloatedContentHeightContribution(location.Y - offset.Y + childFloats);
            float outerWidth = size.Width + resolved.HorizontalAxisSum();
            bool hasAuto = !margin.Left.HasValue || !margin.Right.HasValue;
            if (nonStretch is { } alignment && !hasAuto)
            {
                float start = avoidsFloats ? slotPosition.X - resolved.Left : resolvedContentBoxInset.Left;
                float freeX = avoidsFloats ? slotWidth - size.Width : innerWidth - outerWidth;
                var keyword = alignment.IsSafe && freeX < 0 ? AlignItemsKeyword.Start : alignment.Keyword;
                bool end = keyword is AlignItemsKeyword.End or AlignItemsKeyword.FlexEnd or AlignItemsKeyword.SelfEnd;
                float dx = keyword == AlignItemsKeyword.Center ? freeX / 2 : end != direction.IsRtl() ? freeX : 0;
                location.X = start + resolved.Left + offset.X + dx;
            }
            else if (!nonStretch.HasValue && outerWidth < innerWidth)
            {
                float dx = innerWidth - outerWidth;
                if (textAlign == TextAlign.LegacyCenter) location.X += direction.IsRtl() ? -dx / 2 : dx / 2;
                else if (textAlign == TextAlign.LegacyLeft && direction.IsRtl()) location.X -= dx;
                else if (textAlign == TextAlign.LegacyRight && !direction.IsRtl()) location.X += dx;
            }
            firstBaseline ??= AlignmentUtils.ChildFirstBaseline(itemOutput.FirstBaselines.Y, size.Height, item.Overflow.Y).MaybeAdd(location.Y - offset.Y);
            if (avoidsFloats)
            {
                if (!margin.Left.HasValue) resolved.Left += slotPosition.X - resolvedContentBoxInset.Left;
                if (!margin.Right.HasValue) resolved.Right += containerOuterWidth - resolvedContentBoxInset.Right - slotPosition.X - slotWidth;
            }
            item.FinalLayout = MakeLayout(itemOutput, location, resolved);
            overflowRect = overflowRect.Union(Contribution(tree, item, location, size, itemOutput.ScrollableOverflowRect, containerOuterWidth, direction, resolvedBorder, isScrollContainer));
            if (collapsingFirst && pushedBelow) collapsingFirst = false;
            if (collapsingFirst && clearance) collapsingFirst = false;
            else if (collapsingFirst)
            {
                firstMargins = firstMargins.CollapseWithSet(top);
                if (item.CanBeCollapsedThrough) firstMargins = firstMargins.CollapseWithSet(bottom);
                else collapsingFirst = false;
            }
            if (item.CanBeCollapsedThrough)
            {
                activeMargins = activeMargins.CollapseWithSet(top).CollapseWithSet(bottom);
                absoluteY = committedY + size.Height + yMargin;
            }
            else
            {
                committedY = location.Y - offset.Y + size.Height;
                if (clearance && itemOutput.MarginsCanCollapseThrough)
                {
                    committedY -= top.Resolve(); activeMargins = top.CollapseWithSet(bottom); activeHasClearance = true;
                }
                else { activeMargins = bottom; activeHasClearance = false; }
                absoluteY = committedY + activeMargins.Resolve(); blockCtx.CommitStrut();
            }
            items[index] = item;
        }
        var lastMargins = activeHasClearance ? CollapsibleMarginSet.ZERO : activeMargins;
        float bottomOffset = activeHasClearance ? activeMargins.Resolve() : ownMarginsCollapseWithChildren.End ? 0 : lastMargins.Resolve();
        committedY += resolvedContentBoxInset.Bottom + bottomOffset;
        return (overflowRect, F32Max(0, committedY), firstMargins, lastMargins, firstBaseline);
    }
}
