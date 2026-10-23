// Ported from ThorVG/src/common/tvgCompressor.h and tvgCompressor.cpp

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ThorVG
{
    public static class TvgCompressor
    {
        // GCC's Linux ARM ABI uses unsigned char. The x86 and Windows ABIs
        // use signed char. This affects both native UTF-8 hashing and decoding.
        internal static bool NativeUnsignedChar =>
            (OperatingSystem.IsLinux() || OperatingSystem.IsAndroid()) &&
            RuntimeInformation.ProcessArchitecture is Architecture.Arm or Architecture.Arm64;

        // Base-64 decode index table (256 entries, matching the C++ version).
        private static ReadOnlySpan<byte> B64Index => new byte[]
        {
            0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
            0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
            0,  0,  0,  0,  0,  0,  0,  62, 63, 62, 62, 63, 52, 53, 54, 55, 56, 57,
            58, 59, 60, 61, 0,  0,  0,  0,  0,  0,  0,  0,  1,  2,  3,  4,  5,  6,
            7,  8,  9,  10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24,
            25, 0,  0,  0,  0,  63, 0,  26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36,
            37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51,  0,  0,  0,
            0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
            0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
            0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
            0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
            0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
            0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
            0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
            0,  0,  0,  0
        };

        /// <summary>
        /// Decode a base-64 encoded string.
        /// Returns the decoded bytes and the number of decoded bytes.
        /// </summary>
        public static int B64Decode(ReadOnlySpan<char> encoded, Span<byte> output)
        {
            if (encoded.IsEmpty) return 0;
            foreach (char ch in encoded)
                if (ch > 127) return B64DecodeUtf8(Encoding.UTF8.GetBytes(encoded.ToString()), output);

            var idx = 0;
            var pos = 0;

            while (pos + 1 < encoded.Length && encoded[pos] != '\0' && encoded[pos + 1] != '\0')
            {
                // skip whitespace
                if (encoded[pos] <= 0x20) { ++pos; continue; }

                var value1 = B64Index[encoded[pos]];
                var value2 = B64Index[encoded[pos + 1]];
                output[idx++] = (byte)((value1 << 2) + ((value2 & 0x30) >> 4));

                if (pos + 2 >= encoded.Length || encoded[pos + 2] == '\0' || encoded[pos + 2] == '=' || encoded[pos + 2] == '.') break;
                var value3 = B64Index[encoded[pos + 2]];
                output[idx++] = (byte)(((value2 & 0x0f) << 4) + ((value3 & 0x3c) >> 2));

                if (pos + 3 >= encoded.Length || encoded[pos + 3] == '\0' || encoded[pos + 3] == '=' || encoded[pos + 3] == '.') break;
                var value4 = B64Index[encoded[pos + 3]];
                output[idx++] = (byte)(((value3 & 0x03) << 6) + value4);
                pos += 4;
            }

            return idx;
        }

        private static int B64DecodeUtf8(ReadOnlySpan<byte> encoded, Span<byte> output)
        {
            int idx = 0, pos = 0;
            bool signed = !NativeUnsignedChar;
            while (pos + 1 < encoded.Length && encoded[pos] != 0 && encoded[pos + 1] != 0)
            {
                // A negative first char is whitespace in the native signed-char
                // build. A negative fourth char terminates the group early.
                if (encoded[pos] <= 0x20 || (signed && encoded[pos] >= 128)) { ++pos; continue; }
                // Native negative table indices have undefined behavior. Keep
                // the managed input boundary safe at the actual index sites.
                if (signed && encoded[pos + 1] >= 128) return 0;
                int value1 = B64Index[encoded[pos]], value2 = B64Index[encoded[pos + 1]];
                output[idx++] = (byte)((value1 << 2) + ((value2 & 0x30) >> 4));
                if (pos + 2 >= encoded.Length || encoded[pos + 2] == 0 ||
                    (signed && pos + 3 < encoded.Length && encoded[pos + 3] >= 128) ||
                    encoded[pos + 2] is (byte)'=' or (byte)'.') break;
                if (signed && encoded[pos + 2] >= 128) return 0;
                int value3 = B64Index[encoded[pos + 2]];
                output[idx++] = (byte)(((value2 & 0x0f) << 4) + ((value3 & 0x3c) >> 2));
                if (pos + 3 >= encoded.Length || encoded[pos + 3] == 0 ||
                    (signed && encoded[pos + 3] >= 128) || encoded[pos + 3] is (byte)'=' or (byte)'.') break;
                int value4 = B64Index[encoded[pos + 3]];
                output[idx++] = (byte)(((value3 & 0x03) << 6) + value4);
                pos += 4;
            }
            return idx;
        }

        /// <summary>
        /// Convenience overload that allocates the output buffer.
        /// </summary>
        public static byte[] B64Decode(string encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return System.Array.Empty<byte>();
            var maxLen = 3 * (1 + (TvgUtf8.ByteCount(encoded) >> 2)) + 1;
            var buffer = new byte[maxLen];
            var len = TvgUtf8.HasRaw(encoded) ? B64DecodeUtf8(TvgUtf8.Encode(encoded), buffer) : B64Decode(encoded.AsSpan(), buffer);
            return buffer.AsSpan(0, len).ToArray();
        }

        /// <summary>
        /// DJB2 string hashing (from tvgCompressor.cpp).
        /// </summary>
        public static ulong Djb2Encode(string? str)
        {
            if (str == null) return 0;

            ulong hash = 5381;
            foreach (var c in str)
            {
                if (c == 0) break;
                if (c > 127) return HashUtf8(str);
                hash = unchecked(((hash << 5) + hash) + c);
            }
            return hash;
        }

        /// <summary>
        /// DJB2 with the native unsigned-long width used for ThorVG identifiers.
        /// Windows uses a 32-bit unsigned long even in a 64-bit process. The
        /// existing Djb2Encode overload retains its public 64-bit accumulator.
        /// </summary>
        public static ulong Djb2EncodeNative(string? str)
        {
            ulong hash = Djb2Encode(str);
            return OperatingSystem.IsWindows() || IntPtr.Size == 4 ? (uint)hash : hash;
        }

        private static ulong HashUtf8(string value)
        {
            ulong hash = 5381;
            bool unsigned = NativeUnsignedChar;
            foreach (byte ch in TvgUtf8.Encode(value))
            {
                if (ch == 0) break;
                hash = unchecked(hash * 33 + (unsigned ? ch : (ulong)(sbyte)ch));
            }
            return hash;
        }
    }
}
