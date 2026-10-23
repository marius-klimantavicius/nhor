using Marius.Winter.Taffy;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

// Upstream src/tree/cache.rs tests plus regressions for axis and definiteness keys.
public class CacheContractTests
{
    private static LayoutInput Input(float width) => new()
    {
        RunMode = RunMode.ComputeSize,
        SizingMode = SizingMode.InherentSize,
        Axis = RequestedAxis.Both,
        KnownDimensions = new Size<float?>(width, null),
        ParentSize = SizeExtensions.NoneF32,
        AvailableSpace = new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent),
    };

    private static LayoutOutput Output(float width) => LayoutOutput.FromOuterSize(new Size<float>(width, width));

    private static LayoutInput FinalInput() => new()
    {
        RunMode = RunMode.PerformLayout,
        SizingMode = SizingMode.InherentSize,
        Axis = RequestedAxis.Both,
        KnownDimensions = new Size<float?>(100, 50),
        ParentSize = new Size<float?>(100, 50),
        AvailableSpace = new Size<AvailableSpace>(AvailableSpace.MaxContent, AvailableSpace.MaxContent),
    };

    private static LayoutInput Mode(LayoutInput input, int bits)
    {
        input.SizingMode = (bits & 1) == 0 ? SizingMode.ContentSize : SizingMode.InherentSize;
        input.VerticalMarginsAreCollapsible = new Line<bool>((bits & 2) != 0, (bits & 4) != 0);
        return input;
    }

    [Fact]
    public void RecentlyUsedMeasurementsReceiveASecondChance()
    {
        var cache = Cache.New();
        for (int i = 0; i < CacheConstants.CACHE_SIZE; i++)
            cache.Store(Input(i), Output(i));
        cache.Store(Input(9), Output(9));
        Assert.Equal(Output(1), cache.Get(Input(1)));
        cache.Store(Input(10), Output(10));
        Assert.Equal(Output(1), cache.Get(Input(1)));
        Assert.Null(cache.Get(Input(2)));
    }

    [Fact]
    public void UpdatingAMeasurementDoesNotEvictADifferentEntry()
    {
        var cache = Cache.New();
        for (int i = 0; i < CacheConstants.CACHE_SIZE; i++)
            cache.Store(Input(i), Output(i));
        cache.Store(Input(1), Output(3));
        Assert.Equal(Output(3), cache.Get(Input(1)));
        for (int i = 0; i < CacheConstants.CACHE_SIZE; i++)
            if (i != 1) Assert.Equal(Output(i), cache.Get(Input(i)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CollapsingMarginMetadataCannotBeReconstructedFromSize(int variant)
    {
        var cache = Cache.New();
        var output = Output(1);
        if (variant == 0) output.MarginsCanCollapseThrough = true;
        if (variant == 1) output.TopMargin = CollapsibleMarginSet.FromMargin(10);
        if (variant == 2) output.BottomMargin = CollapsibleMarginSet.FromMargin(-10);
        cache.Store(Input(1), output);
        Assert.Null(cache.Get(Input(1)));
        Assert.True(cache.IsEmpty());
    }

    [Fact]
    public void EverySizingAndMarginModeIsDistinct()
    {
        for (int storedMode = 0; storedMode < 8; storedMode++)
        {
            var cache = Cache.New();
            cache.Store(Mode(Input(1), storedMode), Output(1));
            cache.Store(Mode(FinalInput(), storedMode), Output(2));
            for (int queryMode = 0; queryMode < 8; queryMode++)
            {
                Assert.Equal(queryMode == storedMode ? Output(1) : (LayoutOutput?)null,
                    cache.Get(Mode(Input(1), queryMode)));
                Assert.Equal(queryMode == storedMode ? Output(2) : (LayoutOutput?)null,
                    cache.Get(Mode(FinalInput(), queryMode)));
            }
            Assert.Equal(Output(1), cache.Get(Mode(Input(1), storedMode)));
        }
    }

    [Fact]
    public void StoringADifferentModeEvictsBothFinalAndMeasureEntries()
    {
        for (int mode = 0; mode < 8; mode++)
        {
            if (mode == 1) continue;
            var cache = Cache.New();
            cache.Store(Input(1), Output(1));
            cache.Store(FinalInput(), Output(2));
            var other = Mode(Input(3), mode);
            cache.Store(other, Output(3));
            Assert.Equal(Output(3), cache.Get(other));
            cache.Store(Input(4), Output(4));
            Assert.Null(cache.Get(Input(1)));
            Assert.Null(cache.Get(FinalInput()));
            Assert.Null(cache.Get(other));
            Assert.Equal(Output(4), cache.Get(Input(4)));
        }
    }

    [Theory]
    [InlineData(RequestedAxis.Horizontal, RequestedAxis.Horizontal, true)]
    [InlineData(RequestedAxis.Horizontal, RequestedAxis.Vertical, false)]
    [InlineData(RequestedAxis.Horizontal, RequestedAxis.Both, false)]
    [InlineData(RequestedAxis.Vertical, RequestedAxis.Horizontal, false)]
    [InlineData(RequestedAxis.Vertical, RequestedAxis.Vertical, true)]
    [InlineData(RequestedAxis.Vertical, RequestedAxis.Both, false)]
    [InlineData(RequestedAxis.Both, RequestedAxis.Horizontal, true)]
    [InlineData(RequestedAxis.Both, RequestedAxis.Vertical, true)]
    [InlineData(RequestedAxis.Both, RequestedAxis.Both, true)]
    public void SingleAxisMeasurementsCannotSupplyAnUncomputedAxis(RequestedAxis stored, RequestedAxis requested, bool hit)
    {
        var cache = Cache.New();
        var input = Input(1);
        input.Axis = stored;
        cache.Store(input, Output(7));
        input.Axis = requested;
        Assert.Equal(hit ? Output(7) : (LayoutOutput?)null, cache.Get(input));
    }

    [Theory]
    [InlineData(RunMode.ComputeSize)]
    [InlineData(RunMode.PerformLayout)]
    public void KnownDimensionDefinitenessIsPartOfTheKey(RunMode mode)
    {
        var cache = Cache.New();
        var input = Input(1);
        input.RunMode = mode;
        cache.Store(input, Output(7));
        input.KnownDimensionsAreDefinite.Width = false;
        Assert.Null(cache.Get(input));
        input.KnownDimensionsAreDefinite.Width = true;
        input.KnownDimensionsAreDefinite.Height = false;
        Assert.Equal(Output(7), cache.Get(input)); // Unknown axes normalize to definite.
    }

    [Fact]
    public void HiddenLayoutDoesNotChangeTheModeOrEvictEntries()
    {
        var cache = Cache.New();
        cache.Store(Input(1), Output(1));
        cache.Store(LayoutInput.HIDDEN, Output(2));
        Assert.Equal(Output(1), cache.Get(Input(1)));
    }

    [Fact]
    public void ClearingResetsClockStateAndAllEntries()
    {
        var cache = Cache.New();
        for (int i = 0; i < 20; i++) cache.Store(Input(i), Output(i));
        cache.Store(FinalInput(), Output(100));
        Assert.Equal(ClearState.Cleared, cache.Clear());
        Assert.True(cache.IsEmpty());
        Assert.Equal(ClearState.AlreadyEmpty, cache.Clear());
        for (int i = 0; i < 20; i++) Assert.Null(cache.Get(Input(i)));
        Assert.Null(cache.Get(FinalInput()));
        cache.Store(Input(1), Output(1));
        Assert.Equal(Output(1), cache.Get(Input(1)));
    }

#if DEBUG
    [Fact]
    public void ModeEvictionsCountOnlyNonemptyCaches()
    {
        var cache = Cache.New();
        int evictions = Cache.ModeChangeEvictions;
        cache.Store(Mode(Input(1), 0), Output(1));
        cache.Store(Mode(Input(2), 0), Output(2));
        Assert.Equal(evictions, Cache.ModeChangeEvictions);
        cache.Store(Input(1), Output(1));
        Assert.Equal(evictions + 1, Cache.ModeChangeEvictions);
        cache.Clear();
        cache.Store(Mode(Input(1), 0), Output(1));
        Assert.Equal(evictions + 1, Cache.ModeChangeEvictions);
    }
#endif
}
