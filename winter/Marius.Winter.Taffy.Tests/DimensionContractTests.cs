using System;
using System.Collections.Generic;
using Marius.Winter.Taffy;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

public class DimensionContractTests
{
    private sealed class KeywordMeasureTree : ILayoutPartialTree
    {
        public readonly List<LayoutInput> Inputs = new();
        public IEnumerable<NodeId> ChildIds(NodeId parent) => Array.Empty<NodeId>();
        public int ChildCount(NodeId parent) => 0;
        public NodeId GetChildId(NodeId parent, int index) => throw new ArgumentOutOfRangeException(nameof(index));
        public ICoreStyle GetCoreContainerStyle(NodeId node) => new Style();
        public void SetUnroundedLayout(NodeId node, Layout layout) => throw new InvalidOperationException("Keyword resolution must not commit layouts");
        public LayoutOutput ComputeChildLayout(NodeId node, LayoutInput input)
        {
            Inputs.Add(input);
            float Measure(AvailableSpace space) => space == AvailableSpace.MinContent ? 10
                : space == AvailableSpace.MaxContent ? 100 : space.Unwrap();
            return LayoutOutput.FromOuterSize(new Size<float>(input.KnownDimensions.Width ?? Measure(input.AvailableSpace.Width),
                input.KnownDimensions.Height ?? Measure(input.AvailableSpace.Height)));
        }
    }

    [Fact]
    public void TwoAbsoluteSizingKeywordsShareOneIntrinsicMeasurement()
    {
        var tree = new KeywordMeasureTree();
        var known = SizeExtensions.NoneF32;
        SizingKeywordUtils.ResolveAbsoluteSizingKeywords(tree, default, ref known,
            new Size<Dimension>(Dimension.MinContent(), Dimension.MaxContent()), new Size<float>(100, 80),
            RectExtensions.NoneF32, RectExtensions.NoneF32, SizingMode.ContentSize);
        Assert.Equal(new Size<float?>(10, 100), known);
        var input = Assert.Single(tree.Inputs);
        Assert.Equal(RequestedAxis.Both, input.Axis);
        Assert.Equal(RunMode.ComputeSize, input.RunMode);
        Assert.Equal(SizingMode.ContentSize, input.SizingMode);
        Assert.Equal(new Size<float?>(100, 80), input.ParentSize);
    }

    [Fact]
    public void ExactStretchIsResolvedBeforeMeasuringTheOtherAbsoluteAxis()
    {
        var tree = new KeywordMeasureTree();
        var known = SizeExtensions.NoneF32;
        SizingKeywordUtils.ResolveAbsoluteSizingKeywords(tree, default, ref known,
            new Size<Dimension>(Dimension.Stretch(), Dimension.MaxContent()), new Size<float>(100, 80),
            new Rect<float?>(2, 3, null, null), new Rect<float?>(4, 5, null, null), SizingMode.ContentSize);
        Assert.Equal(new Size<float?>(86, 100), known);
        var input = Assert.Single(tree.Inputs);
        Assert.Equal(RequestedAxis.Vertical, input.Axis);
        Assert.Equal(new Size<float?>(86, null), input.KnownDimensions);
        Assert.Equal(AvailableSpace.Definite(86), input.AvailableSpace.Width);
    }

    [Fact]
    public void EveryDimensionVariantExpandsAndRoundTripsWithoutLosingItsPayload()
    {
        var handle = new IntPtr(0x128);
        var cases = new[] { Dimension.Length(12.125f), Dimension.Percent(.375f), Dimension.AUTO,
            Dimension.MinContent(), Dimension.MaxContent(), Dimension.FitContentPx(30.25f),
            Dimension.FitContentPercent(.75f), Dimension.FitContent(), Dimension.Stretch(),
            Dimension.Content(), Dimension.Calc(handle), Dimension.FitContentCalc(handle) };
        foreach (var value in cases)
        {
            Dimension roundTrip = value.Expand();
            Assert.Equal(value, roundTrip);
            ExpandedDimension expanded = value;
            Assert.Equal(value.Expand(), expanded);
        }
        Assert.Equal(ExpandedDimension.FitContentPx(30.25f), Dimension.FitContentPx(30.25f).Expand());
        Assert.Equal(ExpandedDimension.Content, Dimension.Content().Expand());
        Assert.Equal(handle, Dimension.FitContentCalc(handle).Expand().CalcValue);
    }

    [Fact]
    public void LengthPercentageVariantsExpandAndRoundTrip()
    {
        var handle = new IntPtr(0x128);
        foreach (var value in new[] { LengthPercentage.Length(12), LengthPercentage.Percent(.5f), LengthPercentage.ZERO, LengthPercentage.Calc(handle) })
        {
            LengthPercentage roundTrip = value.Expand();
            Assert.Equal(value, roundTrip);
        }
        foreach (var value in new[] { LengthPercentageAuto.Length(12), LengthPercentageAuto.Percent(.5f), LengthPercentageAuto.AUTO, LengthPercentageAuto.Calc(handle) })
        {
            LengthPercentageAuto roundTrip = value.Expand();
            Assert.Equal(value, roundTrip);
        }
        Assert.Equal(ExpandedLengthPercentageAuto.Auto, LengthPercentageAuto.AUTO.Expand());
    }

    [Fact]
    public void CompactTagsAndKeywordPredicatesMatchUpstream()
    {
        var values = new[] { CompactLength.Length(1), CompactLength.Percent(.5f), CompactLength.AUTO,
            CompactLength.Fr(1), CompactLength.Content(), CompactLength.MIN_CONTENT, CompactLength.MAX_CONTENT,
            CompactLength.FitContentPx(1), CompactLength.FitContentPercent(.5f), CompactLength.FitContentKeyword(),
            CompactLength.Stretch(), CompactLength.Calc(new IntPtr(0x128)), CompactLength.FitContentCalc(new IntPtr(0x128)) };
        var tags = new HashSet<byte>();
        for (int index = 0; index < values.Length; index++)
        {
            var value = values[index];
            Assert.True(tags.Add(value.Tag));
            bool plain = value.Tag is CompactLength.LENGTH_TAG or CompactLength.PERCENT_TAG;
            Assert.Equal(plain, value.IsLengthOrPercentage());
            Assert.Equal(plain || value.IsPlainCalc(), value.IsLengthPercentageOrCalc());
            Assert.Equal(index is >= 5 and <= 10 or 12, value.IsSizingKeyword());
            Assert.Equal(index is 1 or 8 or 11 or 12, value.UsesPercentage());
        }
    }

    [Fact]
    public void FitContentCalcRetainsItsHandleAndRequiresContextualResolution()
    {
        var handle = new IntPtr(0x128);
        var compact = CompactLength.FitContent(LengthPercentage.Calc(handle));
        Assert.True(compact.HasCalc());
        Assert.True(compact.IsFitContentCalc());
        Assert.False(compact.IsPlainCalc());
        Assert.True(compact.IsIntrinsic());
        Assert.True(compact.IsMaxContentAlike());
        Assert.Equal(handle, compact.CalcValue);
        int calls = 0;
        float Calc(IntPtr pointer, float basis) { calls++; Assert.Equal(handle, pointer); return basis / 2 + 3; }
        var dimension = Dimension.FitContentCalc(handle);
        Assert.Null(dimension.MaybeResolve(100f, Calc));
        Assert.Null(SizingKeywordUtils.ResolveSizingKeyword(dimension, 80, null, Calc));
        Assert.Equal(0, calls);
        var resolution = SizingKeywordUtils.ResolveSizingKeyword(dimension, 80, 100, Calc)!.Value;
        Assert.False(resolution.IsExact);
        Assert.Equal(AvailableSpace.Definite(53), resolution.AvailableSpace);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void CalcHandlesRejectNullAndUnalignedPointers()
    {
        Assert.Throws<ArgumentException>(() => CompactLength.Calc(IntPtr.Zero));
        Assert.Throws<ArgumentException>(() => CompactLength.FitContentCalc(new IntPtr(1)));
        Assert.Throws<ArgumentException>(() => LengthPercentage.Calc(new IntPtr(0x129)));
    }

    [Fact]
    public void CompactEqualityPreservesFloatBitsWhileExpandedEqualityUsesFloatValues()
    {
        var positiveZero = Dimension.Length(0);
        var negativeZero = Dimension.Length(BitConverter.UInt32BitsToSingle(0x80000000));
        Assert.NotEqual(positiveZero, negativeZero);
        Assert.Equal(positiveZero.Expand(), negativeZero.Expand());
        Assert.False(negativeZero.IntoRaw().IsZero());
        var nan = Dimension.Length(BitConverter.UInt32BitsToSingle(0x7fc00001));
        Assert.Equal(nan, (Dimension)nan.Expand());
        Assert.False(nan.Expand().Equals(nan.Expand()));
    }

    [Fact]
    public void KeywordResolutionDistinguishesExactStretchAndIntrinsicMeasurement()
    {
        float Calc(IntPtr _, float basis) => basis;
        Assert.Equal(AvailableSpace.MinContent, SizingKeywordUtils.ResolveSizingKeyword(Dimension.MinContent(), null, null, Calc)!.Value.AvailableSpace);
        Assert.Equal(AvailableSpace.MaxContent, SizingKeywordUtils.ResolveSizingKeyword(Dimension.MaxContent(), null, null, Calc)!.Value.AvailableSpace);
        Assert.Equal(AvailableSpace.Definite(40), SizingKeywordUtils.ResolveSizingKeyword(Dimension.FitContent(), 40, null, Calc)!.Value.AvailableSpace);
        Assert.Equal(AvailableSpace.Definite(30), SizingKeywordUtils.ResolveSizingKeyword(Dimension.FitContentPercent(.25f), null, 120, Calc)!.Value.AvailableSpace);
        Assert.Null(SizingKeywordUtils.ResolveSizingKeyword(Dimension.FitContentPercent(.25f), 40, null, Calc));
        Assert.Null(SizingKeywordUtils.ResolveSizingKeyword(Dimension.Stretch(), null, 120, Calc));
        var stretch = SizingKeywordUtils.ResolveSizingKeyword(Dimension.Stretch(), 40, null, Calc)!.Value;
        Assert.True(stretch.IsExact);
        Assert.Equal(40, stretch.Size);
        Assert.Null(Dimension.Content().MaybeResolve(120f, Calc));
    }
}
