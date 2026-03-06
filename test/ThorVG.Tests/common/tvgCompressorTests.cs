using Xunit;

namespace ThorVG.Tests
{
    public class tvgCompressorTests
    {
        [Fact]
        public void B64Decode_SimpleString()
        {
            // "SGVsbG8=" encodes "Hello"
            var decoded = TvgCompressor.B64Decode("SGVsbG8=");
            var text = System.Text.Encoding.ASCII.GetString(decoded);
            Assert.Equal("Hello", text);
        }

        [Fact]
        public void B64Decode_EmptyString()
        {
            var decoded = TvgCompressor.B64Decode("");
            Assert.Empty(decoded);
        }

        [Fact]
        public void B64Decode_NullReturnsEmpty()
        {
            var decoded = TvgCompressor.B64Decode(null!);
            Assert.Empty(decoded);
        }

        [Fact]
        public void B64Decode_TwoCharPadding()
        {
            // "TQ==" encodes "M"
            var decoded = TvgCompressor.B64Decode("TQ==");
            Assert.Single(decoded);
            Assert.Equal((byte)'M', decoded[0]);
        }

        [Fact]
        public void Djb2Encode_KnownHash()
        {
            // DJB2 of "hello" computed with 64-bit accumulator
            var hash = TvgCompressor.Djb2Encode("hello");
            Assert.Equal(210714636441UL, hash);
        }

        [Fact]
        public void Djb2Encode_EmptyString()
        {
            var hash = TvgCompressor.Djb2Encode("");
            Assert.Equal(5381UL, hash);
        }

        [Fact]
        public void Djb2Encode_Null_Returns0()
        {
            Assert.Equal(0UL, TvgCompressor.Djb2Encode(null));
        }
    }
}
