using Xunit;

namespace ThorVG.Tests;

public class tvgRleClipUpdateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyClippedCoverageReturnsFalseForBothClipKinds(bool rectangle)
    {
        var rle = new SwRle();
        var clip = new SwRle();
        rle.spans.Push(new SwSpan { x = 0, y = 0, len = 10, coverage = 255 });
        clip.spans.Push(new SwSpan { x = 20, y = 0, len = 10, coverage = 255 });
        try
        {
            bool result = rectangle ? SwRleOps.rleClip(ref rle, new RenderRegion(20, 0, 30, 1)) : SwRleOps.rleClip(ref rle, clip);
            Assert.False(result);
            Assert.True(rle.Invalid());
        }
        finally { rle.spans.Dispose(); clip.spans.Dispose(); }
    }

    [Fact]
    public void OrderedSpanClippingPreservesAllSegmentsAndCoverage()
    {
        var rle = new SwRle();
        var clip = new SwRle();
        rle.spans.Push(new SwSpan { x = 0, y = 4, len = 12, coverage = 200 });
        clip.spans.Push(new SwSpan { x = 1, y = 4, len = 3, coverage = 100 });
        clip.spans.Push(new SwSpan { x = 7, y = 4, len = 3, coverage = 255 });
        try
        {
            Assert.True(SwRleOps.rleClip(ref rle, clip));
            Assert.Equal(2u, rle.Size());
            Assert.Equal(1, rle.spans[0].x); Assert.Equal(3, rle.spans[0].len); Assert.Equal(79, rle.spans[0].coverage);
            Assert.Equal(7, rle.spans[1].x); Assert.Equal(3, rle.spans[1].len); Assert.Equal(200, rle.spans[1].coverage);
        }
        finally { rle.spans.Dispose(); clip.spans.Dispose(); }
    }

    [Fact]
    public void UnsupportedImageClipMatchesTheNativePassThroughContract()
    {
        var rle = new SwRle();
        rle.spans.Push(new SwSpan { x = 1, y = 2, len = 3, coverage = 128 });
        try
        {
            Assert.True(new SwImageTask().Clip(ref rle));
            Assert.Equal(1u, rle.Size());
            Assert.Equal(128, rle.spans[0].coverage);
        }
        finally { rle.spans.Dispose(); }
    }
}
