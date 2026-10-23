// Ported from taffy/src/compute/grid/alignment.rs
// Alignment of tracks and final positioning of items

using System;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// Grid track alignment and item positioning
    /// </summary>
    public static class GridAlignmentUtils
    {
        /// <summary>
        /// Align the grid tracks within the grid according to the align-content (rows) or
        /// justify-content (columns) property. This only does anything if the size of the
        /// grid is not equal to the size of the grid container in the axis being aligned.
        /// </summary>
        public static void AlignTracks(
            float gridContainerContentBoxSize,
            Line<float> padding,
            Line<float> border,
            ref ValueList<GridTrack> tracks,
            AlignContent trackAlignmentStyle,
            bool axisIsReversed)
        {
            float usedSize = 0f;
            for (int i = 0; i < tracks.Count; i++)
                usedSize += tracks[i].BaseSize;

            float freeSpace = gridContainerContentBoxSize - usedSize;
            float origin = padding.Start + border.Start;

            // Count the number of non-collapsed tracks (not counting gutters)
            int numTracks = 0;
            for (int i = 1; i < tracks.Count; i += 2)
            {
                if (!tracks[i].IsCollapsed)
                    numTracks++;
            }

            // Grid layout treats gaps as full tracks rather than applying them at alignment so we
            // simply pass zero here. Grid layout is never reversed.
            float gap = 0f;
            bool layoutIsReversed = false;
            var trackAlignment = AlignmentUtils.ApplyAlignmentFallback(freeSpace, numTracks, trackAlignmentStyle);
            if (axisIsReversed)
                trackAlignment = trackAlignment.Reversed();

            // Compute offsets
            float totalOffset = origin + (numTracks == 0
                ? AlignmentUtils.ComputeAlignmentOffset(freeSpace, numTracks, gap, trackAlignment, false, true) : 0);
            bool seenNonCollapsedTrack = false;
            for (int step = 0; step < tracks.Count; step++)
            {
                int i = axisIsReversed ? tracks.Count - 1 - step : step;
                var track = tracks[i];
                // Odd tracks are gutters (but slices are zero-indexed, so odd tracks have even indices)
                bool isGutter = i % 2 == 0;
                bool isNonCollapsedTrack = !isGutter && !track.IsCollapsed;

                // Alignment offsets should be applied only to non-collapsed tracks.
                bool isFirst = isNonCollapsedTrack && !seenNonCollapsedTrack;

                float offset;
                if (isNonCollapsedTrack)
                {
                    offset = AlignmentUtils.ComputeAlignmentOffset(freeSpace, numTracks, gap, trackAlignment, layoutIsReversed, isFirst);
                }
                else
                {
                    offset = 0f;
                }

                track.Offset = totalOffset + offset;
                totalOffset = totalOffset + offset + track.BaseSize;
                tracks[i] = track;

                if (isNonCollapsedTrack)
                    seenNonCollapsedTrack = true;
            }
        }

        /// <summary>
        /// Align and size a grid item into its final position
        /// </summary>
        public static (Rect<float> contentSizeContribution, float yPosition, float height, float? baseline) AlignAndPositionItem(
            ILayoutGridContainer tree,
            NodeId node,
            uint order,
            Rect<float> gridArea,
            InBothAbsAxis<AlignItems?> containerAlignmentStyles,
            float baselineShim,
            Direction direction,
            float containerWidth,
            Rect<float> containerBorder,
            bool parentIsScrollContainer,
            ref OofCandidates candidates)
        {
            var gridAreaSize = new Size<float>
            {
                Width = gridArea.Right - gridArea.Left,
                Height = gridArea.Bottom - gridArea.Top,
            };

            var style = tree.GetGridChildStyle(node);

            var overflow = style.Overflow();
            var scrollbarWidth = style.ScrollbarWidth();
            var aspectRatio = style.AspectRatio();
            var justifySelf = (style.JustifySelf() ?? containerAlignmentStyles.Horizontal ?? AlignItems.Normal).ResolveSelfRelative(style.Direction(), direction, true);
            var alignSelf = (style.AlignSelf() ?? containerAlignmentStyles.Vertical ?? AlignItems.Normal).ResolveSelfRelative(style.Direction(), direction, false);

            var position = style.Position();
            var insetH = style.Inset().HorizontalComponents()
                .Map(size => size.ResolveToOption(gridAreaSize.Width, (val, basis) => tree.Calc(val, basis)));
            var insetV = style.Inset().VerticalComponents()
                .Map(size => size.ResolveToOption(gridAreaSize.Height, (val, basis) => tree.Calc(val, basis)));
            var paddingRect = style.Padding()
                .Map(p => p.ResolveOrZero(gridAreaSize.Width, (val, basis) => tree.Calc(val, basis)));
            var borderRect = style.Border()
                .Map(p => p.ResolveOrZero(gridAreaSize.Width, (val, basis) => tree.Calc(val, basis)));
            var paddingBorderSize = paddingRect.Add(borderRect).SumAxes();

            var boxSizingAdj = style.BoxSizing() == BoxSizing.ContentBox ? paddingBorderSize : SizeExtensions.ZeroF32;
            var boxSizingAdjN = boxSizingAdj.Map<float?>(v => v);
            var gridAreaSizeN = gridAreaSize.Map<float?>(v => v);
            var paddingBorderSizeN = paddingBorderSize.Map<float?>(v => v);

            var inherentSize = style.Size()
                .MaybeResolve(gridAreaSizeN, (val, basis) => tree.Calc(val, basis))
                .MaybeApplyAspectRatio(aspectRatio)
                .MaybeAdd(boxSizingAdjN);
            var minSize = style.MinSize()
                .MaybeResolve(gridAreaSizeN, (val, basis) => tree.Calc(val, basis))
                .MaybeAdd(boxSizingAdjN)
                .Or(paddingBorderSizeN)
                .MaybeMax(paddingBorderSizeN)
                .MaybeApplyAspectRatio(aspectRatio);
            var maxSize = style.MaxSize()
                .MaybeResolve(gridAreaSizeN, (val, basis) => tree.Calc(val, basis))
                .MaybeApplyAspectRatio(aspectRatio)
                .MaybeAdd(boxSizingAdjN);

            // Resolve default alignment styles if they are set on neither the parent or the node itself
            var alignmentStyles = new InBothAbsAxis<AlignItems>
            {
                Horizontal = justifySelf.Keyword == AlignItemsKeyword.Normal ? (inherentSize.Width.HasValue || style.Size().Width.IsSizingKeyword() || style.IsReplaced() ? AlignItems.Start : AlignItems.Stretch) : justifySelf,
                Vertical = alignSelf.Keyword == AlignItemsKeyword.Normal ? (inherentSize.Height.HasValue || style.Size().Height.IsSizingKeyword() || aspectRatio.HasValue || style.IsReplaced() ? AlignItems.Start : AlignItems.Stretch) : alignSelf,
            };

            // Note: This is not a bug. It is part of the CSS spec that both horizontal and vertical margins
            // resolve against the WIDTH of the grid area.
            var margin = style.Margin()
                .Map(m => m.ResolveToOption(gridAreaSize.Width, (val, basis) => tree.Calc(val, basis)));

            var gridAreaMinusItemMarginsSize = new Size<float>
            {
                Width = TaffySys.F32Max(gridAreaSize.Width.MaybeSub(margin.Left).MaybeSub(margin.Right), 0),
                Height = TaffySys.F32Max(gridAreaSize.Height.MaybeSub(margin.Top).MaybeSub(margin.Bottom) - baselineShim, 0),
            };

            var sizeStyle = style.Size();
            var keywordWidth = inherentSize.Width.HasValue ? null : SizingKeywordUtils.ResolveSizingKeyword(sizeStyle.Width, gridAreaMinusItemMarginsSize.Width, gridAreaSize.Width, tree.Calc);
            var keywordHeight = inherentSize.Height.HasValue ? null : SizingKeywordUtils.ResolveSizingKeyword(sizeStyle.Height, gridAreaMinusItemMarginsSize.Height, gridAreaSize.Height, tree.Calc);
            var keywordMeasured = SizeExtensions.NoneF32;
            if (keywordWidth is { IsExact: false } kw && keywordHeight is { IsExact: false } kh)
                keywordMeasured = tree.MeasureChildSizeBoth(node, SizeExtensions.NoneF32, gridAreaSizeN, new(kw.AvailableSpace, kh.AvailableSpace), SizingMode.InherentSize, LineExtensions.FalseLine).Map<float?>(v => v);

            // If node is absolutely positioned and width is not set explicitly, then deduce it
            float? width = inherentSize.Width;
            if (!width.HasValue)
            {
                if (position == Position.Absolute && insetH.Start.HasValue && insetH.End.HasValue)
                {
                    width = TaffySys.F32Max(gridAreaMinusItemMarginsSize.Width - insetH.Start.Value - insetH.End.Value, 0f);
                }
                else if (keywordWidth is { } keyword)
                    width = keyword.IsExact ? keyword.Size : keywordMeasured.Width ?? tree.MeasureChildSize(node, SizeExtensions.NoneF32, gridAreaSizeN,
                        new(keyword.AvailableSpace, AvailableSpace.Definite(gridAreaMinusItemMarginsSize.Height)), SizingMode.InherentSize, AbsoluteAxis.Horizontal, LineExtensions.FalseLine);
                else if (margin.Left.HasValue && margin.Right.HasValue
                    && alignmentStyles.Horizontal == AlignItems.Stretch
                    && position != Position.Absolute)
                {
                    width = gridAreaMinusItemMarginsSize.Width;
                }
            }

            // Reapply aspect ratio after stretch and absolute position width adjustments
            var sizeAfterWidth = new Size<float?> { Width = width, Height = inherentSize.Height }.MaybeApplyAspectRatio(aspectRatio);
            width = sizeAfterWidth.Width;
            float? height = sizeAfterWidth.Height;

            if (!height.HasValue)
            {
                if (position == Position.Absolute && insetV.Start.HasValue && insetV.End.HasValue)
                {
                    height = TaffySys.F32Max(gridAreaMinusItemMarginsSize.Height - insetV.Start.Value - insetV.End.Value, 0f);
                }
                else if (keywordHeight is { } keyword)
                    height = keyword.IsExact ? keyword.Size : keywordMeasured.Height ?? tree.MeasureChildSize(node, new(width, null), gridAreaSizeN,
                        new(AvailableSpace.Definite(width ?? gridAreaMinusItemMarginsSize.Width), keyword.AvailableSpace), SizingMode.InherentSize, AbsoluteAxis.Vertical, LineExtensions.FalseLine);
                else if (margin.Top.HasValue && margin.Bottom.HasValue
                    && alignmentStyles.Vertical == AlignItems.Stretch
                    && position != Position.Absolute)
                {
                    height = gridAreaMinusItemMarginsSize.Height;
                }
            }

            // Reapply aspect ratio after stretch and absolute position height adjustments
            var sizeAfterHeight = new Size<float?> { Width = width, Height = height }.MaybeApplyAspectRatio(aspectRatio);
            width = sizeAfterHeight.Width;
            height = sizeAfterHeight.Height;

            // Clamp size by min and max width/height
            var clampedSize = new Size<float?> { Width = width, Height = height }.MaybeClamp(minSize, maxSize);
            width = clampedSize.Width;
            height = clampedSize.Height;

            // Layout node
            Size<float?> size;
            LayoutOutput layoutOutput;

            if (position == Position.Absolute && (!width.HasValue || !height.HasValue))
            {
                var measuredSize = tree.MeasureChildSizeBoth(
                    node,
                    new Size<float?> { Width = width, Height = height },
                    gridAreaSize.Map<float?>(v => v),
                    gridAreaMinusItemMarginsSize.Map(v => Taffy.AvailableSpace.Definite(v)),
                    SizingMode.InherentSize,
                    LineExtensions.FalseLine);
                size = measuredSize.Map<float?>(v => v);
            }
            else
            {
                size = new Size<float?> { Width = width, Height = height };
            }

            layoutOutput = tree.PerformChildLayout(
                node,
                size,
                gridAreaSize.Map<float?>(v => v),
                gridAreaMinusItemMarginsSize.Map(v => Taffy.AvailableSpace.Definite(v)),
                SizingMode.InherentSize,
                LineExtensions.FalseLine);

            // Resolve final size
            float finalWidth = (size.Width ?? layoutOutput.Size.Width).MaybeClamp(minSize.Width, maxSize.Width);
            float finalHeight = (size.Height ?? layoutOutput.Size.Height).MaybeClamp(minSize.Height, maxSize.Height);

            var (x, xMargin) = AlignItemWithinArea(
                new Line<float> { Start = gridArea.Left, End = gridArea.Right },
                alignmentStyles.Horizontal,
                finalWidth,
                position,
                insetH,
                margin.HorizontalComponents(),
                0f,
                direction);

            var (y, yMargin) = AlignItemWithinArea(
                new Line<float> { Start = gridArea.Top, End = gridArea.Bottom },
                alignmentStyles.Vertical,
                finalHeight,
                position,
                insetV,
                margin.VerticalComponents(),
                baselineShim,
                Direction.Ltr);

            float yWithoutRelative = y;
            if (position == Position.Relative)
            {
                x += direction.IsRtl() ? -insetH.End ?? insetH.Start ?? 0 : insetH.Start ?? -insetH.End ?? 0;
                y += insetV.Start ?? -insetV.End ?? 0;
            }
            var scrollbarSize = new Size<float>
            {
                Width = overflow.Y == Overflow.Scroll ? scrollbarWidth : 0f,
                Height = overflow.X == Overflow.Scroll ? scrollbarWidth : 0f,
            };

            var resolvedMargin = new Rect<float>
            {
                Left = xMargin.Start,
                Right = xMargin.End,
                Top = yMargin.Start,
                Bottom = yMargin.End,
            };

            tree.SetUnroundedLayout(node, new Layout
            {
                Order = order,
                Location = new Point<float> { X = x, Y = y },
                Size = new Size<float> { Width = finalWidth, Height = finalHeight },
                ScrollableOverflowRect = layoutOutput.ScrollableOverflowRect,
                ScrollbarSize = scrollbarSize,
                Padding = paddingRect,
                Border = borderRect,
                Margin = resolvedMargin,
            });

            candidates = layoutOutput.OofCandidates.Take();
            candidates.Translate(new(x, y));
            var relative = new Point<float>(direction.IsRtl() ? containerWidth - (x + finalWidth) - containerBorder.Right : x - containerBorder.Left,
                y - containerBorder.Top);
            var contribution = ScrollableOverflow.ComputeContribution(relative, new(finalWidth, finalHeight),
                layoutOutput.ScrollableOverflowRect, overflow, style.Contain(), parentIsScrollContainer);
            var baseline = AlignmentUtils.ChildFirstBaseline(layoutOutput.FirstBaselines.Y, finalHeight, overflow.Y);
            return (contribution, yWithoutRelative, finalHeight, baseline);

        }

        /// <summary>
        /// Align and size a grid item along a single axis
        /// </summary>
        public static (float position, Line<float> resolvedMargin) AlignItemWithinArea(
            Line<float> gridArea,
            AlignItems alignmentStyle,
            float resolvedSize,
            Position position,
            Line<float?> inset,
            Line<float?> margin,
            float baselineShim,
            Direction direction)
        {
            // Calculate grid area dimension in the axis
            var nonAutoMargin = new Line<float>
            {
                Start = (margin.Start ?? 0f) + baselineShim,
                End = margin.End ?? 0f,
            };
            float gridAreaSize = TaffySys.F32Max(gridArea.End - gridArea.Start, 0f);
            float freeSpace = TaffySys.F32Max(gridAreaSize - resolvedSize - nonAutoMargin.Sum(), 0f);

            // Expand auto margins to fill available space
            int autoMarginCount = (margin.Start.HasValue ? 0 : 1) + (margin.End.HasValue ? 0 : 1);
            float autoMarginSize = autoMarginCount > 0 ? freeSpace / autoMarginCount : 0f;
            var resolvedMargin = new Line<float>
            {
                Start = (margin.Start ?? autoMarginSize) + baselineShim,
                End = margin.End ?? autoMarginSize,
            };

            bool overflows = resolvedSize + nonAutoMargin.Sum() > gridAreaSize;
            var alignmentKeyword = AlignmentUtils.ResolveSelfAlignmentSafety(alignmentStyle, overflows);

            // Compute offset in the axis
            float alignmentBasedOffset = alignmentKeyword switch
            {
                AlignItemsKeyword.Start or AlignItemsKeyword.FlexStart or AlignItemsKeyword.Baseline or AlignItemsKeyword.Stretch =>
                    direction.IsRtl()
                        ? gridAreaSize - resolvedSize - resolvedMargin.End
                        : resolvedMargin.Start,
                AlignItemsKeyword.End or AlignItemsKeyword.FlexEnd =>
                    direction.IsRtl()
                        ? resolvedMargin.Start
                        : gridAreaSize - resolvedSize - resolvedMargin.End,
                AlignItemsKeyword.Center => (gridAreaSize - resolvedSize + resolvedMargin.Start - resolvedMargin.End) / 2f,
                _ => resolvedMargin.Start,
            };

            float offsetWithinArea;
            if (position == Position.Absolute)
            {
                if (inset.Start.HasValue && inset.End.HasValue)
                {
                    offsetWithinArea = direction.IsRtl()
                        ? gridAreaSize - inset.End.Value - resolvedSize - nonAutoMargin.End
                        : inset.Start.Value + nonAutoMargin.Start;
                }
                else if (inset.Start.HasValue)
                    offsetWithinArea = inset.Start.Value + nonAutoMargin.Start;
                else if (inset.End.HasValue)
                    offsetWithinArea = gridAreaSize - inset.End.Value - resolvedSize - nonAutoMargin.End;
                else
                    offsetWithinArea = alignmentBasedOffset;
            }
            else
            {
                offsetWithinArea = alignmentBasedOffset;
            }

            float start = gridArea.Start + offsetWithinArea;
            return (start, resolvedMargin);
        }
    }
}
