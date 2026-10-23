using System.Collections.Immutable;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

public class GridTraitTests
{
    private sealed class Container : IGridContainerStyle
    {
        public Size<Dimension> Size() => new(Dimension.Length(100), Dimension.Length(50));
        public ImmutableList<GridTemplateComponent> GetGridTemplateColumns() => ImmutableList.Create(GridTemplateComponent.FromLength(25), GridTemplateComponent.FromLength(75));
        public ImmutableList<GridTemplateComponent> GetGridTemplateRows() => ImmutableList.Create(GridTemplateComponent.FromLength(50));
        public ImmutableList<ImmutableList<string>> GetGridTemplateColumnNames() => ImmutableList.Create(ImmutableList.Create("a"), ImmutableList.Create("b"), ImmutableList.Create("c"));
    }
    private sealed class Item : IGridItemStyle
    {
        public Line<GridPlacement> GetGridColumn() => new(GridPlacement.FromNamedLine("b", 1), GridPlacement.FromNamedLine("c", 1));
    }
    [Fact]
    public void GridAlgorithmsConsumeCustomStyleTraits()
    {
        var tree = new CalcIntegrationTests.CalcTree { ContainerStyleOverride = new Container(), ItemStyleOverride = new Item() };
        var child = tree.Add(new Style());
        var root = tree.Add(new Style { Display = Display.Grid, SizeValue = new(Dimension.Length(100), Dimension.Length(50)) }, [child]);
        tree.Compute(root, AvailableSpace.Definite(100));
        Assert.Equal(new Point<float>(25, 0), tree.Layout(child).Location);
        Assert.Equal(new Size<float>(75, 50), tree.Layout(child).Size);
        Assert.Equal("[a] 25px [b] 75px [c]", tree.GetDetailedGridInfo(root)!.GridTemplateColumns());
    }
    [Fact]
    public void DimensionAndMaximumTrackConversionsRejectUnsupportedVariants()
    {
        foreach (var value in new[] { Dimension.FitContent(), Dimension.Stretch(), Dimension.Content() })
        { Assert.Equal(MinTrackSizingFunction.AUTO, (MinTrackSizingFunction)value); Assert.Equal(MaxTrackSizingFunction.AUTO, (MaxTrackSizingFunction)value); }
        foreach (var value in new[] { Dimension.FitContentPx(20), Dimension.FitContentPercent(.5f), Dimension.FitContentCalc(new System.IntPtr(0x1000)) })
            Assert.Equal(MinTrackSizingFunction.AUTO, (MinTrackSizingFunction)value);
        foreach (var value in new[] { MaxTrackSizingFunction.Fr(2), MaxTrackSizingFunction.FitContentPx(20), MaxTrackSizingFunction.FitContentCalc(new System.IntPtr(0x1000)) })
            Assert.Equal(MinTrackSizingFunction.AUTO, (MinTrackSizingFunction)value);
        Assert.Equal(MinTrackSizingFunction.Length(12), (MinTrackSizingFunction)MaxTrackSizingFunction.Length(12));
        var tracks = ImmutableList.CreateRange(new MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>[ushort.MaxValue + 1]);
        Assert.Equal(ushort.MaxValue, new GridTemplateRepetition(RepetitionCount.Count(1), tracks).TrackCount);
    }
}
