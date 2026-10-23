using System;
using Xunit;

namespace ThorVG.Tests;

public class tvgStringUpdateTests
{
    [Theory]
    [InlineData("file.", "file")]
    [InlineData(".", "")]
    [InlineData("..", ".")]
    [InlineData("/a.b/file.", "file")]
    [InlineData("a\0b.svg", "a")]
    public void FilenamePreservesNativeDotsAndCStringBoundary(string input, string expected) =>
        Assert.Equal(expected, TvgStr.Filename(input));

    [Fact]
    public void BackslashIsASeparatorOnlyInTheNativeWindowsPathMode()
    {
        Assert.Equal(OperatingSystem.IsWindows() ? "a\\" : "a\\b.svg", TvgStr.Dirname("a\\b.svg"));
        Assert.Equal(OperatingSystem.IsWindows() ? "b" : "a\\b", TvgStr.Filename("a\\b.svg"));
    }

    [Fact]
    public void StringOperationsStopAtTheNativeCStringBoundary()
    {
        Assert.True(TvgStr.Equal("a\0b", "a"));
        Assert.Equal("ab", TvgStr.Concat("a\0c", "b\0d"));
        Assert.Equal("a", TvgStr.Duplicate("a\0b"));
        Assert.Equal("ab", TvgStr.Append("a\0c", "b\0d", 8));
        Assert.Equal("a", TvgStr.Fileext("a\0b.svg"));
    }

    [Theory]
    [InlineData("-", 0x80000000u, 0)]
    [InlineData("-x", 0x80000000u, 0)]
    [InlineData("-infi", 0u, 0)]
    [InlineData("1em", 0x3f800000u, 3)]
    [InlineData("1.e", 0x3f800000u, 0)]
    [InlineData("\u00a042", 0u, 0)]
    public void FloatParserRetainsSignedZeroAndExactEndPosition(string input, uint bits, int end)
    {
        int index = 0;
        Assert.Equal(bits, BitConverter.SingleToUInt32Bits(TvgStr.ToFloat(input, ref index)));
        Assert.Equal(end, index);
    }
}
