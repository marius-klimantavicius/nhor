using System;
using Xunit;

namespace ThorVG.Tests;

public class tvgRawStringUpdateTests
{
    [Theory]
    [InlineData("C5BD61", 0, "00")]
    [InlineData("C5BD61", 1, "C500")]
    [InlineData("C5BD61", 2, "C5BD00")]
    [InlineData("C5BD61", 20, "C5BD6100")]
    [InlineData("F09F9982", 3, "F09F9900")]
    [InlineData("610062", 20, "6100")]
    public void NativeByteDuplicationRetainsEvenPartialUtf8(string input, int max, string expected)
    {
        var result = TvgStr.DuplicateUtf8(Convert.FromHexString(input), (nuint)max, out uint size)!;
        Assert.Equal(expected, Convert.ToHexString(result));
        Assert.Equal((uint)result.Length - 1, size);
    }

    [Theory]
    [InlineData("C5BD", "F09F998261", 2, "C5BDF09F00")]
    [InlineData("610062", "630064", 9, "616300")]
    [InlineData("61", "62", 0, "6100")]
    public void NativeByteAppendUsesCStringBoundaries(string lhs, string rhs, int max, string expected) =>
        Assert.Equal(expected, Convert.ToHexString(TvgStr.AppendUtf8(Convert.FromHexString(lhs), Convert.FromHexString(rhs), (nuint)max)!));

    [Fact]
    public void NullByteBuffersKeepTheNativeDuplicateAndAppendResults()
    {
        Assert.Null(TvgStr.DuplicateUtf8(null, 3, out var size)); Assert.Equal(0u, size);
        var left = new byte[] { 97, 0 };
        Assert.Same(left, TvgStr.AppendUtf8(left, null, 1));
        Assert.Equal(new byte[] { 97, 0 }, TvgStr.AppendUtf8(null, left, 1));
        Assert.Equal("Ž", TvgStr.Duplicate("Ža", 1));
    }
}
