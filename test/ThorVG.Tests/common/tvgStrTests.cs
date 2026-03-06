using Xunit;

namespace ThorVG.Tests
{
    public class tvgStrTests
    {
        // ---- Equal -------------------------------------------------------

        [Fact]
        public void Equal_SameStrings() => Assert.True(TvgStr.Equal("abc", "abc"));

        [Fact]
        public void Equal_DifferentStrings() => Assert.False(TvgStr.Equal("abc", "xyz"));

        [Fact]
        public void Equal_Null() => Assert.False(TvgStr.Equal(null, "abc"));

        // ---- Concat ------------------------------------------------------

        [Fact]
        public void Concat_JoinsStrings() => Assert.Equal("helloworld", TvgStr.Concat("hello", "world"));

        // ---- Duplicate ---------------------------------------------------

        [Fact]
        public void Duplicate_Full() => Assert.Equal("hello", TvgStr.Duplicate("hello"));

        [Fact]
        public void Duplicate_Truncated() => Assert.Equal("hel", TvgStr.Duplicate("hello", 3));

        // ---- Append ------------------------------------------------------

        [Fact]
        public void Append_ToNull_Duplicates()
        {
            var result = TvgStr.Append(null, "abc", 3);
            Assert.Equal("abc", result);
        }

        [Fact]
        public void Append_NullRhs_ReturnsLhs()
        {
            var result = TvgStr.Append("abc", null, 0);
            Assert.Equal("abc", result);
        }

        [Fact]
        public void Append_Joins()
        {
            var result = TvgStr.Append("hello", "world", 5);
            Assert.Equal("helloworld", result);
        }

        // ---- Dirname -----------------------------------------------------

        [Fact]
        public void Dirname_UnixPath() => Assert.Equal("/foo/bar/", TvgStr.Dirname("/foo/bar/baz.txt"));

        [Fact]
        public void Dirname_NoSlash() => Assert.Equal("file.txt", TvgStr.Dirname("file.txt"));

        // ---- Filename ----------------------------------------------------

        [Fact]
        public void Filename_UnixPath() => Assert.Equal("baz", TvgStr.Filename("/foo/bar/baz.txt"));

        [Fact]
        public void Filename_NoExtension() => Assert.Equal("baz", TvgStr.Filename("/foo/bar/baz"));

        // ---- Fileext -----------------------------------------------------

        [Fact]
        public void Fileext_HasExtension() => Assert.Equal("txt", TvgStr.Fileext("file.txt"));

        [Fact]
        public void Fileext_NoExtension() => Assert.Equal("", TvgStr.Fileext("file"));

        [Fact]
        public void Fileext_DoubleExtension() => Assert.Equal("gz", TvgStr.Fileext("file.tar.gz"));

        // ---- ToFloat -----------------------------------------------------

        [Fact]
        public void ToFloat_Integer()
        {
            int idx = 0;
            Assert.Equal(42.0f, TvgStr.ToFloat("42", ref idx));
            Assert.Equal(2, idx);
        }

        [Fact]
        public void ToFloat_Decimal()
        {
            int idx = 0;
            var val = TvgStr.ToFloat("3.14", ref idx);
            Assert.True(System.MathF.Abs(val - 3.14f) < 0.001f);
            Assert.Equal(4, idx);
        }

        [Fact]
        public void ToFloat_Negative()
        {
            int idx = 0;
            Assert.Equal(-5.0f, TvgStr.ToFloat("-5", ref idx));
        }

        [Fact]
        public void ToFloat_Exponent()
        {
            int idx = 0;
            var val = TvgStr.ToFloat("1e3", ref idx);
            Assert.True(System.MathF.Abs(val - 1000.0f) < 0.01f);
        }

        [Fact]
        public void ToFloat_NegativeExponent()
        {
            int idx = 0;
            var val = TvgStr.ToFloat("5e-2", ref idx);
            Assert.True(System.MathF.Abs(val - 0.05f) < 0.001f);
        }

        [Fact]
        public void ToFloat_LeadingWhitespace()
        {
            int idx = 0;
            var val = TvgStr.ToFloat("  42", ref idx);
            Assert.Equal(42.0f, val);
        }

        [Fact]
        public void ToFloat_Inf()
        {
            int idx = 0;
            Assert.Equal(float.PositiveInfinity, TvgStr.ToFloat("inf", ref idx));
        }

        [Fact]
        public void ToFloat_NegInf()
        {
            int idx = 0;
            Assert.Equal(float.NegativeInfinity, TvgStr.ToFloat("-infinity", ref idx));
        }

        [Fact]
        public void ToFloat_NaN()
        {
            int idx = 0;
            Assert.True(float.IsNaN(TvgStr.ToFloat("nan", ref idx)));
        }

        [Fact]
        public void ToFloat_EmptyString()
        {
            int idx = 0;
            Assert.Equal(0.0f, TvgStr.ToFloat("", ref idx));
        }

        [Fact]
        public void ToFloat_Null()
        {
            int idx = 0;
            Assert.Equal(0.0f, TvgStr.ToFloat(null!, ref idx));
        }
    }
}
