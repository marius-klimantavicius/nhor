using Xunit;

namespace ThorVG.Tests
{
    public class tvgSvgUtilTests
    {
        [Fact]
        public void SkipWhiteSpace_SkipsSpacesAndTabs()
        {
            var str = "   hello";
            var pos = SvgUtil.SkipWhiteSpace(str, 0, str.Length);
            Assert.Equal(3, pos);
        }

        [Fact]
        public void SkipWhiteSpace_NoWhiteSpace()
        {
            var str = "hello";
            var pos = SvgUtil.SkipWhiteSpace(str, 0, str.Length);
            Assert.Equal(0, pos);
        }

        [Fact]
        public void SkipWhiteSpace_AllWhiteSpace()
        {
            var str = "   ";
            var pos = SvgUtil.SkipWhiteSpace(str, 0, str.Length);
            Assert.Equal(3, pos);
        }

        [Fact]
        public void SkipWhiteSpace_EmptyString()
        {
            var str = "";
            var pos = SvgUtil.SkipWhiteSpace(str, 0, str.Length);
            Assert.Equal(0, pos);
        }

        [Fact]
        public void UnskipWhiteSpace_SkipsTrailingWhiteSpace()
        {
            var str = "hello   ";
            var pos = SvgUtil.UnskipWhiteSpace(str, str.Length, 0);
            Assert.Equal(5, pos);
        }

        [Fact]
        public void UnskipWhiteSpace_NoTrailingWhiteSpace()
        {
            var str = "hello";
            var pos = SvgUtil.UnskipWhiteSpace(str, str.Length, 0);
            Assert.Equal(5, pos);
        }

        [Fact]
        public void SkipWhiteSpaceAndComma_SkipsCommaAndSpaces()
        {
            var str = " , hello";
            var pos = SvgUtil.SkipWhiteSpaceAndComma(str, 0);
            // Skips whitespace to comma, then skips comma: pos after ","
            Assert.Equal(2, pos);
        }

        [Fact]
        public void SkipWhiteSpaceAndComma_NoComma()
        {
            var str = "  hello";
            var pos = SvgUtil.SkipWhiteSpaceAndComma(str, 0);
            Assert.Equal(2, pos);
        }

        [Fact]
        public void URLDecode_DecodesPercentEncoding()
        {
            var result = SvgUtil.URLDecode("hello%20world");
            Assert.Equal("hello world", result);
        }

        [Fact]
        public void URLDecode_NoEncoding()
        {
            var result = SvgUtil.URLDecode("hello");
            Assert.Equal("hello", result);
        }

        [Fact]
        public void URLDecode_IncompletePercent()
        {
            var result = SvgUtil.URLDecode("hello%2");
            Assert.Equal("hello%2", result);
        }
    }
}
