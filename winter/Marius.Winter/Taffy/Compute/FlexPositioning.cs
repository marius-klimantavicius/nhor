// Ported from collect_oof_candidates in src/compute/flexbox.rs at 802e497b.
namespace Marius.Winter.Taffy;

public static partial class FlexboxAlgorithm
{
    private static AlignItems ResolveNormal(AlignItems value) => value.Keyword == AlignItemsKeyword.Normal
        ? AlignItems.Stretch : value;
    private static OofCandidates CollectOofCandidates(ILayoutFlexboxContainer tree, NodeId node,
        ref AlgoConstants constants, ref ValueList<FlexLine> lines)
    {
        var candidates = new OofCandidates();
        var dir = constants.Dir;
        bool mainRtl = constants.IsRow && constants.LayoutDirection.IsRtl();
        bool crossRtl = constants.IsColumn && constants.LayoutDirection.IsRtl();
        bool mainReverse = dir.IsReverse() ^ mainRtl;
        bool crossReverse = constants.IsWrapReverse ^ crossRtl;
        var mainArea = new Line<float>(constants.ContentBoxInset.MainStart(dir), constants.ContainerSize.Main(dir) - constants.ContentBoxInset.MainEnd(dir));
        var crossArea = new Line<float>(constants.ContentBoxInset.CrossStart(dir), constants.ContainerSize.Cross(dir) - constants.ContentBoxInset.CrossEnd(dir));
        int lineIndex = 0, itemIndex = 0;
        for (int order = 0; order < tree.ChildCount(node); order++)
        {
            var child = tree.GetChildId(node, order);
            var style = tree.GetFlexboxChildStyle(child);
            if (style.BoxGenerationMode() == BoxGenerationMode.None) continue;
            if (!style.Position().IsOutOfFlow())
            {
                while (lineIndex < lines.Count && itemIndex == lines[lineIndex].Items.Length) { lineIndex++; itemIndex = 0; }
                if (lineIndex < lines.Count)
                {
                    ref var item = ref lines[lineIndex].Items[itemIndex++];
                    candidates.Append(ref item.OofCandidates);
                }
                continue;
            }
            var align = style.AlignSelf() ?? constants.AlignItemsStyle;
            if (align.Keyword == AlignItemsKeyword.Normal) align = AlignItems.Stretch;
            align = align.ResolveSelfRelative(style.Direction(), constants.LayoutDirection, constants.IsColumn);
            var mainKeyword = (constants.JustifyContentStyle ?? AlignContent.FlexStart).Keyword;
            var mainEdge = mainKeyword switch
            {
                AlignContentKeyword.Start => mainRtl ? AxisStaticEdge.End : AxisStaticEdge.Start,
                AlignContentKeyword.End => mainRtl ? AxisStaticEdge.Start : AxisStaticEdge.End,
                AlignContentKeyword.Center or AlignContentKeyword.SpaceAround or AlignContentKeyword.SpaceEvenly => AxisStaticEdge.Center,
                AlignContentKeyword.FlexEnd => mainReverse ? AxisStaticEdge.Start : AxisStaticEdge.End,
                _ => mainReverse ? AxisStaticEdge.End : AxisStaticEdge.Start,
            };
            AxisStaticEdge CrossEdge(AlignItemsKeyword keyword) => keyword switch
            {
                AlignItemsKeyword.Start or AlignItemsKeyword.Baseline => crossRtl ? AxisStaticEdge.End : AxisStaticEdge.Start,
                AlignItemsKeyword.End => crossRtl ? AxisStaticEdge.Start : AxisStaticEdge.End,
                AlignItemsKeyword.Center => AxisStaticEdge.Center,
                AlignItemsKeyword.FlexEnd => crossReverse ? AxisStaticEdge.Start : AxisStaticEdge.End,
                _ => crossReverse ? AxisStaticEdge.End : AxisStaticEdge.Start,
            };
            var cross = new AxisStaticPosition(crossArea, new(CrossEdge(align.Keyword), align.Safety,
                CrossEdge(align.IsSafe ? AlignItemsKeyword.Start : align.Keyword)));
            var main = new AxisStaticPosition(mainArea, AxisStaticAlign.FromKeyword(mainEdge));
            candidates.Push(new OofCandidate { Node = child, Order = (uint)order, Position = style.Position(),
                StaticPosition = constants.IsRow ? new(main, cross) : new(cross, main) });
        }
        return candidates;
    }
}
