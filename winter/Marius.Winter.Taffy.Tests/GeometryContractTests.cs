using System;
using Marius.Winter.Taffy;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

public class GeometryContractTests
{
    [Fact]
    public void GenericValueEqualityMatchesNativeFloatAndCompactSemantics()
    {
        using var raw = typeof(GeometryContractTests).Assembly.GetManifestResourceStream("Marius.Winter.Taffy.Tests.Native.geometry-corpus.json.gz")!;
        using var gzip = new System.IO.Compression.GZipStream(raw, System.IO.Compression.CompressionMode.Decompress);
        using var corpus = System.Text.Json.JsonDocument.Parse(gzip);
        foreach (var test in corpus.RootElement.EnumerateArray())
        {
            float a = BitConverter.UInt32BitsToSingle(test.GetProperty("a").GetUInt32()), b = BitConverter.UInt32BitsToSingle(test.GetProperty("b").GetUInt32());
            var actual = new[]
            {
                new Size<float>(a, 1) == new Size<float>(b, 1), new Point<float>(a, 1) == new Point<float>(b, 1),
                new Rect<float>(a, 1, 2, 3) == new Rect<float>(b, 1, 2, 3), new Line<float>(a, 1) == new Line<float>(b, 1),
                new MinMax<float, float>(a, 1) == new MinMax<float, float>(b, 1), new Size<float?>(a, null) == new Size<float?>(b, null),
                new Size<CompactLength>(CompactLength.Length(a), CompactLength.ZERO) == new Size<CompactLength>(CompactLength.Length(b), CompactLength.ZERO),
            };
            int index = 0; foreach (var expected in test.GetProperty("expected").EnumerateArray()) Assert.Equal(expected.GetBoolean(), actual[index++]);
            if (actual[0]) Assert.Equal(new Size<float>(a, 1).GetHashCode(), new Size<float>(b, 1).GetHashCode());
        }
    }
    [Fact]
    public void LayoutOutputCloneOwnsCandidatesAndPreservesValueContracts()
    {
        var output = LayoutOutput.HIDDEN;
        output.Size = new(40, 30);
        output.Baselines = new(4, 8);
        output.TopMargin = CollapsibleMarginSet.FromMargin(7);
        output.OofPositioningArea = new(new(20, 10), new(3, 5));
        output.OofCandidates.Push(new OofCandidate
        {
            Node = new NodeId(1), Position = Position.Absolute,
            StaticPosition = new(AxisStaticPosition.FromEdge(2, AxisStaticEdge.Start), AxisStaticPosition.FromEdge(4, AxisStaticEdge.End)),
        });
        var copy = output.Clone();
        Assert.Equal(output, copy); Assert.Equal(output.GetHashCode(), copy.GetHashCode());
        copy.OofCandidates.Translate(new(10, 20));
        Assert.NotEqual(output, copy);
        Assert.Equal(2, output.OofCandidates.AsSpan()[0].StaticPosition.X.Area.Start);
        var transposed = output.Transpose();
        Assert.Equal(new Size<float>(30, 40), transposed.Size);
        Assert.Equal(Baselines.None, transposed.Baselines);
        Assert.Equal(CollapsibleMarginSet.ZERO, transposed.TopMargin);
        Assert.Equal(output.OofCandidates, transposed.OofCandidates);
        Assert.Equal(new OofPositioningArea(new(10, 20), new(5, 3)), transposed.OofPositioningArea);
        output.Baselines = new(float.NaN, null);
        Assert.False(output.Equals(output.Clone()));
        Assert.False(new Baselines(null, float.NaN).Equals(new Baselines(null, float.NaN)));
        var flags = default(LayoutInput); var constructed = new LayoutInput();
        Assert.Equal(flags, constructed); Assert.Equal(flags.GetHashCode(), constructed.GetHashCode());
    }
    [Fact]
    public void RectangleUnionUsesEdgeCoordinatesAndTransposesBothAxes()
    {
        var first = new Rect<float>(-5, 3, -2, 7);
        var second = new Rect<float>(-1, 8, -9, 4);
        var result = first.Union(second);
        Assert.Equal(new Rect<float>(-5, 8, -9, 7), result);
        Assert.Equal(new Rect<float>(-9, 7, -5, 8), result.Transpose());
        Assert.Equal(first, first.Transpose().Transpose());
        Assert.Equal(new Size<float>(7, 3), new Size<float>(3, 7).Transpose());
        Assert.Equal(new Rect<float?>(null, null, null, null), RectExtensions.NoneF32);
    }
}
