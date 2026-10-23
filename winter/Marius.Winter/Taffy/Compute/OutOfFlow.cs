// Ported from src/compute/oof.rs at 802e497b.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static Marius.Winter.Taffy.TaffySys;

namespace Marius.Winter.Taffy;

public static class OutOfFlow
{
    private static float AlignInLine(Line<float> line, float size, AxisStaticEdge edge) => edge switch
    {
        AxisStaticEdge.Start => line.Start,
        AxisStaticEdge.End => line.End - size,
        _ => (line.Start + line.End - size) / 2f,
    };
    public static Point<float> ResolveStaticOffset(Point<AxisStaticPosition> position, Size<float> size, Rect<float> margin)
    {
        static float Axis(AxisStaticPosition sp, float size, Line<float> margin)
        {
            float outer = size + margin.Start + margin.End;
            bool overflows = sp.Align.Safety == AlignmentSafety.Safe && outer > sp.Area.End - sp.Area.Start;
            return AlignInLine(sp.Area, outer, overflows ? sp.Align.Fallback : sp.Align.Keyword) + margin.Start;
        }
        return new(Axis(position.X, size.Width, new(margin.Left, margin.Right)), Axis(position.Y, size.Height, new(margin.Top, margin.Bottom)));
    }
    private enum OofAlignment { Normal, Stretch, Start, Center, End }
    private static OofAlignment Alignment(AlignItems value) => value.Keyword switch
    {
        AlignItemsKeyword.Normal => OofAlignment.Normal,
        AlignItemsKeyword.Stretch => OofAlignment.Stretch,
        AlignItemsKeyword.Center => OofAlignment.Center,
        AlignItemsKeyword.End or AlignItemsKeyword.FlexEnd or AlignItemsKeyword.SelfEnd => OofAlignment.End,
        _ => OofAlignment.Start,
    };
    private struct OofAxis
    {
        public float CbSize;
        public Line<float?> Inset, Margin;
        public AxisStaticPosition StaticPosition;
        public OofAlignment Alignment;
        public AlignmentSafety Safety;
        public bool IsInline, Reversed, IsScrollAxis;
        private readonly AxisStaticEdge StartEdge => Reversed ? AxisStaticEdge.End : AxisStaticEdge.Start;
        private readonly AxisStaticEdge EndEdge => Reversed ? AxisStaticEdge.Start : AxisStaticEdge.End;
        private readonly AxisStaticEdge PhysicalEdge => Alignment == OofAlignment.Center ? AxisStaticEdge.Center
            : ((Alignment == OofAlignment.End) ^ Reversed) ? AxisStaticEdge.End : AxisStaticEdge.Start;
        public readonly Line<float> Imcb()
        {
            Line<float> line;
            AxisStaticEdge weaker;
            if (Inset.Start is { } start && Inset.End is { } end) { line = new(start, CbSize - end); weaker = EndEdge; }
            else if (Inset.Start is { } left) { line = new(left, CbSize); weaker = AxisStaticEdge.End; }
            else if (Inset.End is { } right) { line = new(0, CbSize - right); weaker = AxisStaticEdge.Start; }
            else
            {
                var sp = StaticPosition;
                if (sp.Align.Keyword == AxisStaticEdge.Start) line = new(sp.Area.Start, CbSize);
                else if (sp.Align.Keyword == AxisStaticEdge.End) line = new(0, sp.Area.End);
                else
                {
                    float center = (sp.Area.Start + sp.Area.End) / 2f;
                    float endDistance = CbSize - center;
                    line = MathF.Abs(center) <= MathF.Abs(endDistance) ? new(0, 2f * center) : new(CbSize - 2f * endDistance, CbSize);
                }
                weaker = EndEdge;
            }
            if (line.End < line.Start)
            {
                if (weaker == AxisStaticEdge.Start) line.Start = line.End;
                else line.End = line.Start;
            }
            return line;
        }
        public readonly float StretchFitSize()
        {
            var line = Imcb();
            return F32Max(line.End - line.Start - (Margin.Start ?? 0) - (Margin.End ?? 0), 0);
        }
        public readonly bool IsStretchSized(bool replaced, bool table) => Inset.Start.HasValue && Inset.End.HasValue
            && (Alignment == OofAlignment.Stretch || (Alignment == OofAlignment.Normal && !replaced && !table));
        public readonly Line<float> ResolveMargins(float size)
        {
            if (Margin.Start is { } start && Margin.End is { } end) return new(start, end);
            if (!Inset.Start.HasValue || !Inset.End.HasValue) return new(Margin.Start ?? 0, Margin.End ?? 0);
            var imcb = Imcb();
            float free = imcb.End - imcb.Start - size;
            if (Margin.Start is { } s) return new(s, free - s);
            if (Margin.End is { } e) return new(free - e, e);
            if (free < 0 && IsInline) return Reversed ? new(free, 0) : new(0, free);
            return new(free / 2f, free / 2f);
        }
        public readonly float Position(float size, Line<float> margin)
        {
            var imcb = Imcb();
            float outer = size + margin.Start + margin.End;
            float extent = imcb.End - imcb.Start;
            if (!Inset.Start.HasValue && !Inset.End.HasValue)
            {
                var sp = StaticPosition;
                return (sp.Align.Safety == AlignmentSafety.Safe && outer > extent
                    ? AlignInLine(imcb, outer, StartEdge) : AlignInLine(sp.Area, outer, sp.Align.Keyword)) + margin.Start;
            }
            if (!Inset.End.HasValue) return imcb.Start + margin.Start;
            if (!Inset.Start.HasValue) return imcb.End - size - margin.End;
            if (!Margin.Start.HasValue || !Margin.End.HasValue) return imcb.Start + margin.Start;
            float offset;
            if (Safety == AlignmentSafety.Unsafe) offset = AlignInLine(imcb, outer, PhysicalEdge);
            else if (Safety == AlignmentSafety.Safe) offset = AlignInLine(imcb, outer, outer > extent ? StartEdge : PhysicalEdge);
            else if (outer <= extent || Alignment == OofAlignment.Normal) offset = AlignInLine(imcb, outer, PhysicalEdge);
            else
            {
                var limit = new Line<float>(F32Min(imcb.Start, 0), F32Max(imcb.End, CbSize));
                if (IsScrollAxis)
                {
                    if (Reversed) limit.Start = float.NegativeInfinity;
                    else limit.End = float.PositiveInfinity;
                }
                offset = outer <= limit.End - limit.Start
                    ? F32Max(F32Min(AlignInLine(imcb, outer, PhysicalEdge), limit.End - outer), limit.Start)
                    : AlignInLine(limit, outer, StartEdge);
            }
            return offset + margin.Start;
        }
    }
    public readonly record struct OofBoxLayout(Layout Layout, OofCandidates Surfaced, Point<Overflow> Overflow, Contain Contain);
    public readonly record struct OofLayoutResult(List<NodeId> Hoisted, OofCandidates Unclaimed, Rect<float> ScrollableOverflowRect);

    public static void ComputeOofLayout(ILayoutContainingBlock tree, NodeId node, ref LayoutOutput output)
    {
        if (output.OofPositioningArea is not { } area) return;
        if (output.OofCandidates.IsEmpty) { tree.ClearHoistedChildren(node); return; }
        var style = tree.GetCoreContainerStyle(node);
        var candidates = output.OofCandidates.Take();
        var result = ComputeOofLayoutForArea(tree, node, candidates.AsSpan(), area, style.Direction(), style.IsContainingBlock());
        tree.ClearHoistedChildren(node);
        tree.AddHoistedChildren(node, CollectionsMarshal.AsSpan(result.Hoisted));
        output.OofCandidates = result.Unclaimed;
        output.ScrollableOverflowRect = output.ScrollableOverflowRect.Union(result.ScrollableOverflowRect);
    }
    public static OofLayoutResult ComputeOofLayoutForArea(ILayoutContainingBlock tree, NodeId owner,
        ReadOnlySpan<OofCandidate> candidates, OofPositioningArea area, Direction direction, ContainingBlockClaims claims)
    {
        var hoisted = new List<NodeId>();
        var unclaimed = new OofCandidates();
        var rect = PerformOofLayout(tree, owner, candidates, area.Size, area.Offset, direction, claims,
            tree.GetCoreContainerStyle(owner).Overflow(), hoisted, ref unclaimed);
        return new(hoisted, unclaimed, rect);
    }
    public static OofBoxLayout LayoutOofBox(ILayoutContainingBlock tree, OofCandidate candidate, Size<float> areaSize,
        Point<float> areaOffset, Direction direction, Point<Overflow> containerOverflow, NodeId? gridOwner = null,
        Action<LayoutInput>? beforeLayout = null)
    {
        var style = tree.GetOofItemStyle(candidate.Node);
        if (gridOwner is { } owner && tree.GetDetailedGridInfo(owner) is { } grid)
        {
            var area = grid.ResolveAbsoluteGridArea(style.GridRow(), style.GridColumn(), direction,
                new(areaOffset.X, areaOffset.X + areaSize.Width, areaOffset.Y, areaOffset.Y + areaSize.Height));
            areaSize = new(area.Right - area.Left, area.Bottom - area.Top);
            areaOffset = new(area.Left, area.Top);
        }
        var parent = areaSize.Map<float?>(v => v);
        var aspect = style.AspectRatio();
        var margin = style.Margin().Map(m => m.ResolveToOption(areaSize.Width, tree.Calc));
        var padding = style.Padding().ResolveOrZero(areaSize.Width, tree.Calc);
        var border = style.Border().ResolveOrZero(areaSize.Width, tree.Calc);
        var pb = padding.Add(border).SumAxes();
        var adjustment = (style.BoxSizing() == BoxSizing.ContentBox ? pb : SizeExtensions.ZeroF32).Map<float?>(v => v);
        var inset = style.Inset();
        float? left = inset.Left.MaybeResolve(areaSize.Width, tree.Calc), right = inset.Right.MaybeResolve(areaSize.Width, tree.Calc);
        float? top = inset.Top.MaybeResolve(areaSize.Height, tree.Calc), bottom = inset.Bottom.MaybeResolve(areaSize.Height, tree.Calc);
        var sizeStyle = style.Size();
        var known = sizeStyle.MaybeResolve(parent, tree.Calc).MaybeApplyAspectRatio(aspect).MaybeAdd(adjustment);
        var min = style.MinSize().MaybeResolve(parent, tree.Calc).MaybeApplyAspectRatio(aspect).MaybeAdd(adjustment).Or(pb.Map<float?>(v => v)).MaybeMax(pb.Map<float?>(v => v));
        var max = style.MaxSize().MaybeResolve(parent, tree.Calc).MaybeApplyAspectRatio(aspect).MaybeAdd(adjustment);
        known = known.MaybeClamp(min, max);
        var justify = (style.JustifySelf() ?? AlignItems.Normal).ResolveSelfRelative(style.Direction(), direction, true);
        var align = (style.AlignSelf() ?? AlignItems.Normal).ResolveSelfRelative(style.Direction(), direction, false);
        var horizontal = new OofAxis { CbSize = areaSize.Width, Inset = new(left, right), Margin = new(margin.Left, margin.Right),
            StaticPosition = new(new(candidate.StaticPosition.X.Area.Start - areaOffset.X, candidate.StaticPosition.X.Area.End - areaOffset.X), candidate.StaticPosition.X.Align),
            Alignment = Alignment(justify), Safety = justify.Safety, IsInline = true, Reversed = direction.IsRtl(), IsScrollAxis = containerOverflow.X.IsScrollContainer() };
        var vertical = new OofAxis { CbSize = areaSize.Height, Inset = new(top, bottom), Margin = new(margin.Top, margin.Bottom),
            StaticPosition = new(new(candidate.StaticPosition.Y.Area.Start - areaOffset.Y, candidate.StaticPosition.Y.Area.End - areaOffset.Y), candidate.StaticPosition.Y.Align),
            Alignment = Alignment(align), Safety = align.Safety, IsScrollAxis = containerOverflow.Y.IsScrollContainer() };
        var stretch = new Size<float>(horizontal.StretchFitSize(), vertical.StretchFitSize());
        var available = stretch;
        if (style.IsTable()) available = new(F32Min(available.Width, F32Max(areaSize.Width - (margin.Left ?? 0) - (margin.Right ?? 0), 0)),
            F32Min(available.Height, F32Max(areaSize.Height - (margin.Top ?? 0) - (margin.Bottom ?? 0), 0)));
        var space = available.MaybeClamp(min, max).Map(AvailableSpace.Definite);
        if (sizeStyle.Width.IsSizingKeyword() || sizeStyle.Height.IsSizingKeyword())
        {
            SizingKeywordUtils.ResolveAbsoluteSizingKeywords(tree, candidate.Node, ref known, sizeStyle, areaSize,
                new(left, right, top, bottom), margin, SizingMode.ContentSize);
            known = known.MaybeApplyAspectRatio(aspect).MaybeClamp(min, max);
        }
        bool stretchWidth = !known.Width.HasValue && horizontal.IsStretchSized(style.IsReplaced(), style.IsTable());
        bool stretchHeight = !known.Height.HasValue && vertical.IsStretchSized(style.IsReplaced(), style.IsTable());
        float? stretchAspect = stretchWidth && stretchHeight && horizontal.Alignment == OofAlignment.Stretch && vertical.Alignment == OofAlignment.Stretch ? null : aspect;
        if (stretchWidth)
        {
            if (style.IsTable()) min.Width = F32Max(min.Width ?? 0, stretch.Width);
            else { known.Width = stretch.Width; known = known.MaybeApplyAspectRatio(stretchAspect).MaybeClamp(min, max); }
        }
        if (!known.Height.HasValue && stretchHeight)
        {
            if (style.IsTable()) min.Height = F32Max(min.Height ?? 0, stretch.Height);
            else { known.Height = stretch.Height; known = known.MaybeApplyAspectRatio(stretchAspect).MaybeClamp(min, max); }
        }
        var final = known.Width.HasValue && known.Height.HasValue ? new Size<float>(known.Width.Value, known.Height.Value)
            : known.UnwrapOr(tree.MeasureChildSizeBoth(candidate.Node, known, parent, space, SizingMode.ContentSize, LineExtensions.FalseLine));
        final = final.MaybeClamp(min, max);
        var input = new LayoutInput { KnownDimensions = final.Map<float?>(v => v), KnownDimensionsAreDefinite = new(true, true),
            ParentSize = parent, AvailableSpace = space, SizingMode = SizingMode.ContentSize, Axis = RequestedAxis.Both,
            RunMode = RunMode.PerformLayout, VerticalMarginsAreCollapsible = LineExtensions.FalseLine };
        beforeLayout?.Invoke(input);
        var output = tree.ComputeChildLayout(candidate.Node, input);
        var mx = horizontal.ResolveMargins(final.Width);
        var my = vertical.ResolveMargins(final.Height);
        var overflow = style.Overflow();
        var layout = new Layout { Order = candidate.Order, Size = final, ScrollableOverflowRect = output.ScrollableOverflowRect,
            ScrollbarSize = new(overflow.Y == Overflow.Scroll ? style.ScrollbarWidth() : 0, overflow.X == Overflow.Scroll ? style.ScrollbarWidth() : 0),
            Location = new(horizontal.Position(final.Width, mx) + areaOffset.X, vertical.Position(final.Height, my) + areaOffset.Y),
            Padding = padding, Border = border, Margin = new(mx.Start, mx.End, my.Start, my.End) };
        tree.SetUnroundedLayout(candidate.Node, layout);
        return new(layout, output.OofCandidates.Take(), overflow, style.Contain());
    }
    public static Rect<float> PerformOofLayout(ILayoutContainingBlock tree, NodeId node, ReadOnlySpan<OofCandidate> candidates,
        Size<float> areaSize, Point<float> areaOffset, Direction direction, ContainingBlockClaims claims,
        Point<Overflow> containerOverflow, List<NodeId> hoisted, ref OofCandidates unclaimed)
    {
        Rect<float> rect = default;
        if (candidates.IsEmpty) return rect;
        bool scroll = containerOverflow.X.IsScrollContainer() || containerOverflow.Y.IsScrollContainer();
        var work = new List<OofCandidate>();
        foreach (var candidate in candidates)
        {
            if (claims.ForPosition(candidate.Position)) work.Add(candidate);
            else unclaimed.Push(candidate);
        }
        for (int i = 0; i < work.Count; i++)
        {
            var candidate = work[i];
            hoisted.Add(candidate.Node);
            var result = LayoutOofBox(tree, candidate, areaSize, areaOffset, direction, containerOverflow, node);
            var surfaced = result.Surfaced;
            var layout = result.Layout;
            surfaced.Translate(layout.Location);
            foreach (var child in surfaced.AsSpan())
            {
                if (claims.ForPosition(child.Position)) work.Add(child);
                else unclaimed.Push(child);
            }
            var location = new Point<float>(direction.IsRtl() ? areaSize.Width - (layout.Location.X - areaOffset.X) - layout.Size.Width
                : layout.Location.X - areaOffset.X, layout.Location.Y - areaOffset.Y);
            rect = rect.Union(ScrollableOverflow.ComputeContribution(location, layout.Size, layout.ScrollableOverflowRect, result.Overflow, result.Contain, scroll));
        }
        return rect;
    }
}
