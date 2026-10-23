using Xunit;

namespace ThorVG.Tests
{
    public class tvgCompressorTests
    {
        private static bool UnsignedCharAbi => (System.OperatingSystem.IsLinux() || System.OperatingSystem.IsAndroid()) &&
            System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture is
                System.Runtime.InteropServices.Architecture.Arm or System.Runtime.InteropServices.Architecture.Arm64;
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

        [Theory]
        [InlineData("žvaigždė", 13619402030097917329UL, 14023367590645320593UL)]
        [InlineData("🙂", 6380756751UL, 6390244111UL)]
        [InlineData("a\0b", 177670UL, 177670UL)]
        public void Djb2Encode_UsesNativeUtf8BytesAndCStringTermination(string value, ulong signed, ulong unsigned)
        {
            Assert.Equal(UnsignedCharAbi ? unsigned : signed, TvgCompressor.Djb2Encode(value));
        }

        [Theory]
        [InlineData("TWE", "4D61")]
        [InlineData(" TWFu", "4D616E")]
        [InlineData("TW Fu", "4D6005")]
        [InlineData("TQ..", "4D")]
        [InlineData("-_8=", "FBFF")]
        [InlineData("TQ\0=", "4D")]
        [InlineData("\0TQ==", "")]
        public void B64Decode_PreservesNativePermissiveTableAndTermination(string input, string expected)
        {
            Assert.Equal(expected, System.Convert.ToHexString(TvgCompressor.B64Decode(input)));
        }

        [Theory]
        [InlineData("ŽTWFu", "4D616E", "0004D616")]
        [InlineData("TWFŽ", "4D", "4D6140")]
        [InlineData("TWFuŽ", "4D616E", "4D616E00")]
        [InlineData("🙂🙂🙂🙂", "", "000000000000000000000000")]
        public void B64Decode_UsesNativeUtf8WhitespaceAndGroupTermination(string input, string signed, string unsigned)
        {
            Assert.Equal(UnsignedCharAbi ? unsigned : signed,
                System.Convert.ToHexString(TvgCompressor.B64Decode(input)));
        }

        [Fact]
        public void B64Decode_GuardsUndefinedNegativeTableIndex()
        {
            Assert.Equal(UnsignedCharAbi ? "4C00" : "",
                System.Convert.ToHexString(TvgCompressor.B64Decode("TŽ==")));
        }
    }
}
