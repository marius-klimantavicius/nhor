// Port of src/compute/common/sizing_keyword.rs.
using System;
using static Marius.Winter.Taffy.TaffySys;

namespace Marius.Winter.Taffy;

public readonly struct SizingKeywordResolution
{
    public bool IsExact { get; }
    public float Size { get; }
    public AvailableSpace AvailableSpace { get; }
    private SizingKeywordResolution(bool isExact, float size, AvailableSpace availableSpace)
    {
        IsExact = isExact;
        Size = size;
        AvailableSpace = availableSpace;
    }
    public static SizingKeywordResolution Exact(float size) => new(true, size, default);
    public static SizingKeywordResolution Measure(AvailableSpace availableSpace) => new(false, 0, availableSpace);
}

public static class SizingKeywordUtils
{
    public static SizingKeywordResolution? ResolveSizingKeyword(Dimension style, float? stretchSize,
        float? percentResolutionBasis, Func<IntPtr, float, float> calcResolver)
    {
        return style.GetTag() switch
        {
            CompactLength.MIN_CONTENT_TAG => SizingKeywordResolution.Measure(AvailableSpace.MinContent),
            CompactLength.MAX_CONTENT_TAG => SizingKeywordResolution.Measure(AvailableSpace.MaxContent),
            CompactLength.FIT_CONTENT_PX_TAG => SizingKeywordResolution.Measure(AvailableSpace.Definite(style.GetValue())),
            CompactLength.FIT_CONTENT_PERCENT_TAG when percentResolutionBasis.HasValue =>
                SizingKeywordResolution.Measure(AvailableSpace.Definite(percentResolutionBasis.Value * style.GetValue())),
            CompactLength.FIT_CONTENT_KEYWORD_TAG when stretchSize.HasValue =>
                SizingKeywordResolution.Measure(AvailableSpace.Definite(stretchSize.Value)),
            _ when style.IsFitContentCalc() && percentResolutionBasis.HasValue =>
                SizingKeywordResolution.Measure(AvailableSpace.Definite(calcResolver(style.CalcValue(), percentResolutionBasis.Value))),
            CompactLength.STRETCH_TAG when stretchSize.HasValue => SizingKeywordResolution.Exact(stretchSize.Value),
            _ => null,
        };
    }

    public static void ResolveAbsoluteSizingKeywords(ILayoutPartialTree tree, NodeId node,
        ref Size<float?> knownDimensions, Size<Dimension> sizeStyle, Size<float> areaSize,
        Rect<float?> inset, Rect<float?> margin, SizingMode sizingMode)
    {
        var stretchSize = new Size<float>(
            F32Max(areaSize.Width - (inset.Left ?? 0) - (inset.Right ?? 0) - (margin.Left ?? 0) - (margin.Right ?? 0), 0),
            F32Max(areaSize.Height - (inset.Top ?? 0) - (inset.Bottom ?? 0) - (margin.Top ?? 0) - (margin.Bottom ?? 0), 0));
        var keywordWidth = knownDimensions.Width.HasValue ? null
            : ResolveSizingKeyword(sizeStyle.Width, stretchSize.Width, areaSize.Width, tree.Calc);
        var keywordHeight = knownDimensions.Height.HasValue ? null
            : ResolveSizingKeyword(sizeStyle.Height, stretchSize.Height, areaSize.Height, tree.Calc);
        var parentSize = areaSize.Map<float?>(v => v);
        if (keywordWidth is { IsExact: false } width && keywordHeight is { IsExact: false } height)
        {
            knownDimensions = tree.MeasureChildSizeBoth(node, SizeExtensions.NoneF32, parentSize,
                new Size<AvailableSpace>(width.AvailableSpace, height.AvailableSpace), sizingMode,
                new Line<bool>(false, false)).Map<float?>(v => v);
            return;
        }
        if (keywordWidth is { } widthResolution)
            knownDimensions.Width = widthResolution.IsExact ? widthResolution.Size
                : tree.MeasureChildSize(node, knownDimensions, parentSize,
                    new Size<AvailableSpace>(widthResolution.AvailableSpace, AvailableSpace.Definite(stretchSize.Height)),
                    sizingMode, AbsoluteAxis.Horizontal, new Line<bool>(false, false));
        if (keywordHeight is { } heightResolution)
            knownDimensions.Height = heightResolution.IsExact ? heightResolution.Size
                : tree.MeasureChildSize(node, knownDimensions, parentSize,
                    new Size<AvailableSpace>(AvailableSpace.Definite(knownDimensions.Width ?? stretchSize.Width), heightResolution.AvailableSpace),
                    sizingMode, AbsoluteAxis.Vertical, new Line<bool>(false, false));
    }
}
