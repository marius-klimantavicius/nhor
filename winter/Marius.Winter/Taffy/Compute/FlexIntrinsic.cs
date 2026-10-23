// Ported from determine_container_main_size in src/compute/flexbox.rs at 802e497b.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static Marius.Winter.Taffy.TaffySys;

namespace Marius.Winter.Taffy;

public static partial class FlexboxAlgorithm
{
    private static LayoutOutput ComputeEmptyContainerLayout(ILayoutFlexboxContainer tree, LayoutInput inputs, ref AlgoConstants constants)
    {
        var available = DetermineAvailableSpace(inputs.KnownDimensions, inputs.AvailableSpace, ref constants);
        var lines = new ValueList<FlexLine>();
        var dir = constants.Dir;
        if (constants.NodeInnerSize.Main(dir) is { } main)
        {
            constants.InnerContainerSize.SetMain(dir, main);
            constants.ContainerSize.SetMain(dir, main + constants.ContentBoxInset.MainAxisSum(dir));
        }
        else DetermineContainerMainSize(tree, available, ref lines, ref constants);
        if (inputs.RunMode == RunMode.ComputeSize && inputs.Axis == dir.MainAxis().ToRequestedAxis())
        {
            var size = SizeExtensions.ZeroF32; size.SetMain(dir, constants.ContainerSize.Main(dir));
            return LayoutOutput.FromOuterSize(size);
        }
        DetermineContainerCrossSize(ref lines, inputs.KnownDimensions, ref constants);
        var output = LayoutOutput.FromOuterSize(constants.ContainerSize);
        output.OofPositioningArea = new(default, default);
        return output;
    }

    private static void DetermineContainerMainSize(ILayoutFlexboxContainer tree, Size<AvailableSpace> available,
        ref ValueList<FlexLine> lines, ref AlgoConstants constants)
    {
        var dir = constants.Dir;
        float inset = constants.ContentBoxInset.MainAxisSum(dir), outer;
        if (constants.NodeOuterSize.Main(dir) is { } definite) outer = definite;
        else if (available.Main(dir).IsDefinite() || (available.Main(dir) == AvailableSpace.MinContent && constants.IsWrap))
        {
            float longest = 0;
            List<float>? lengths = constants.IsBalance ? new() : null;
            foreach (var line in lines)
            {
                float length = 0;
                foreach (var item in line.Items)
                {
                    float size = F32Max(item.FlexBasis.MaybeMax(item.MinSize.Main(dir)) + item.Margin.MainAxisSum(dir),
                        item.Padding.Add(item.Border).MainAxisSum(dir));
                    length += size; lengths?.Add(size);
                }
                length += SumAxisGaps(constants.Gap.Main(dir), line.Items.Length);
                longest = F32Max(longest, length);
            }
            outer = longest + inset;
            if (available.Main(dir).IsDefinite())
            {
                if (lengths is { Count: > 0 })
                {
                    int index = 0; float widest = 0;
                    var counts = FlexBalance.BalancedLineItemCounts(CollectionsMarshal.AsSpan(lengths), float.PositiveInfinity, constants.Gap.Main(dir), constants.LineCount);
                    foreach (int count in counts)
                    {
                        float size = 0; for (int i = 0; i < count; i++) size += lengths[index++];
                        widest = F32Max(widest, size + SumAxisGaps(constants.Gap.Main(dir), count));
                    }
                    outer = F32Max(outer, F32Min(widest + inset, available.Main(dir).Unwrap()));
                }
                else if (!constants.IsBalance && lines.Count > 1) outer = F32Max(outer, available.Main(dir).Unwrap());
            }
        }
        else
        {
            float main = 0;
            foreach (var line in lines)
            {
                foreach (ref var item in line.Items)
                {
                    float? styleMin = item.MinSize.Main(dir), pref = item.Size.Main(dir), styleMax = item.MaxSize.Main(dir);
                    float margin = item.Margin.MainAxisSum(dir);
                    float? basis = constants.IsRow ? ((float?)item.FlexBasis).MaybeMax(pref) : item.FlexBasis;
                    float? basisMin = item.FlexShrink == 0 ? basis : null, basisMax = item.FlexGrow == 0 ? basis : null;
                    float min = F32Max(styleMin.MaybeMax(basisMin) ?? basisMin ?? item.ResolvedMinimumMainSize, item.ResolvedMinimumMainSize);
                    float max = styleMax.MaybeMin(basisMax) ?? basisMax ?? float.PositiveInfinity;
                    float contribution;
                    if (pref is { } p && (max <= min || max <= p)) contribution = F32Max(F32Min(p, max), min) + margin;
                    else if (max <= min) contribution = min + margin;
                    else if (item.IsScrollContainer()) contribution = item.FlexBasis + margin;
                    else
                    {
                        float content;
                        if (pref is { } preferred) content = F32Max(preferred, item.Padding.MainAxisSum(dir) + item.Border.MainAxisSum(dir));
                        else
                        {
                            float? crossParent = constants.NodeInnerSize.Cross(dir);
                            var crossSpace = available.Cross(dir);
                            if (crossSpace.IsDefinite()) crossSpace = AvailableSpace.Definite(F32Max(constants.DividedCrossSpace(crossParent ?? crossSpace.Unwrap()) - item.Margin.CrossAxisSum(dir), 0));
                            crossSpace = crossSpace.MaybeClamp(item.MinSize.Cross(dir), item.MaxSize.Cross(dir));
                            var known = item.Size.WithMain(dir, null);
                            known.SetCross(dir, known.Cross(dir).MaybeClamp(item.MinSize.MaybeApplyAspectRatio(item.AspectRatio).Cross(dir), item.MaxSize.MaybeApplyAspectRatio(item.AspectRatio).Cross(dir)));
                            if (item.AlignSelf == AlignItems.Stretch && !known.Cross(dir).HasValue) known.SetCross(dir, crossSpace.IntoOption());
                            content = tree.MeasureChildSize(item.Node, known, constants.NodeInnerSize, available.WithCross(dir, crossSpace),
                                SizingMode.ContentSize, dir.MainAxis(), LineExtensions.FalseLine);
                            if (item.AspectRatio is { } ratio && known.Cross(dir) is { } cross) content = F32Max(content, constants.IsRow ? cross * ratio : cross / ratio);
                        }
                        contribution = ((constants.IsRow ? content : F32Max(content, item.FlexBasis)) + margin).MaybeClamp(styleMin, styleMax);
                    }
                    float diff = contribution - margin - item.FlexBasis;
                    item.ContentFlexFraction = diff > 0 ? diff / F32Max(1, item.FlexGrow)
                        : diff < 0 ? diff / (F32Max(1, item.FlexShrink) * item.InnerFlexBasis) : 0;
                }
                float sum = 0, largest = 0;
                foreach (var item in line.Items)
                {
                    float fraction = item.ContentFlexFraction;
                    float scaled = F32Max(1, item.FlexShrink) * item.InnerFlexBasis;
                    float contribution = fraction > 0 ? F32Max(1, item.FlexGrow) * fraction : fraction < 0 && scaled != 0 ? scaled * fraction : 0;
                    float size = item.FlexBasis + contribution + item.Margin.MainAxisSum(dir);
                    if (constants.IsWrap && constants.IsRow) largest = F32Max(largest, size);
                    sum += size;
                }
                main = F32Max(main, F32Max(largest, sum + SumAxisGaps(constants.Gap.Main(dir), line.Items.Length)));
            }
            outer = main + inset;
        }
        outer = F32Max(outer.MaybeClamp(constants.MinSize.Main(dir), constants.MaxSize.Main(dir)), inset - constants.ScrollbarGutter.Main(dir));
        float inner = F32Max(outer - inset, 0);
        constants.ContainerSize.SetMain(dir, outer); constants.InnerContainerSize.SetMain(dir, inner); constants.NodeInnerSize.SetMain(dir, inner);
    }
}
