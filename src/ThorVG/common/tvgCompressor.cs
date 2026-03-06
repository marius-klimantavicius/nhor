// Ported from ThorVG/src/common/tvgCompressor.h and tvgCompressor.cpp

using System;

namespace ThorVG
{
    public static class TvgCompressor
    {
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

            var idx = 0;
            var pos = 0;

            while (pos < encoded.Length && pos + 1 < encoded.Length)
            {
                // skip whitespace
                if (encoded[pos] <= 0x20) { ++pos; continue; }

                var value1 = B64Index[encoded[pos]];
                var value2 = B64Index[encoded[pos + 1]];
                output[idx++] = (byte)((value1 << 2) + ((value2 & 0x30) >> 4));

                if (pos + 2 >= encoded.Length || encoded[pos + 2] == '=' || encoded[pos + 2] == '.') break;
                var value3 = B64Index[encoded[pos + 2]];
                output[idx++] = (byte)(((value2 & 0x0f) << 4) + ((value3 & 0x3c) >> 2));

                if (pos + 3 >= encoded.Length || encoded[pos + 3] == '=' || encoded[pos + 3] == '.') break;
                var value4 = B64Index[encoded[pos + 3]];
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
            var maxLen = 3 * (1 + (encoded.Length >> 2)) + 1;
            var buffer = new byte[maxLen];
            var len = B64Decode(encoded.AsSpan(), buffer);
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
                hash = ((hash << 5) + hash) + c; // hash * 33 + c
            }
            return hash;
        }
    }
}
