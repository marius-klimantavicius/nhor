/*
 * Copyright (c) 2020 - 2026 ThorVG project. All rights reserved.

 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:

 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.

 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

/*
  LodePNG version 20200306

  Copyright (c) 2005-2020 Lode Vandevenne

  This software is provided 'as-is', without any express or implied
  warranty. In no event will the authors be held liable for any damages
  arising from the use of this software.

  Permission is granted to anyone to use this software for any purpose,
  including commercial applications, and to alter it and redistribute it
  freely, subject to the following restrictions:

    1. The origin of this software must not be misrepresented; you must not
    claim that you wrote the original software. If you use this software
    in a product, an acknowledgment in the product documentation would be
    appreciated but is not required.

    2. Altered source versions must be plainly marked as such, and must not be
    misrepresented as being the original software.

    3. This notice may not be removed or altered from any source distribution.
*/

// Ported from ThorVG/src/loaders/png/tvgLodePng.h and tvgLodePng.cpp
// This is a line-by-line port of the embedded LodePNG C decoder.

using System;
using System.Runtime.CompilerServices;

namespace ThorVG
{
    /// <summary>PNG color types (mirrors C++ LodePNGColorType).</summary>
    public enum LodePNGColorType
    {
        LCT_GREY = 0,       /*grayscale: 1,2,4,8,16 bit*/
        LCT_RGB = 2,        /*RGB: 8,16 bit*/
        LCT_PALETTE = 3,    /*palette: 1,2,4,8 bit*/
        LCT_GREY_ALPHA = 4, /*grayscale with alpha: 8,16 bit*/
        LCT_RGBA = 6,       /*RGB with alpha: 8,16 bit*/
        LCT_MAX_OCTET_VALUE = 255
    }

    /// <summary>Settings for zlib decompression.</summary>
    internal class LodePNGDecompressSettings
    {
        public uint ignore_adler32;
        public uint ignore_nlen;
    }

    /// <summary>Color mode of an image.</summary>
    internal class LodePNGColorMode
    {
        public LodePNGColorType colortype;
        public uint bitdepth;
        public byte[]? palette;
        public int palettesize;
        public uint key_defined;
        public uint key_r;
        public uint key_g;
        public uint key_b;
    }

    /// <summary>Information about the PNG image.</summary>
    internal class LodePNGInfo
    {
        public uint compression_method;
        public uint filter_method;
        public uint interlace_method;
        public LodePNGColorMode color = new LodePNGColorMode();
    }

    /// <summary>Settings for the decoder.</summary>
    internal class LodePNGDecoderSettings
    {
        public LodePNGDecompressSettings zlibsettings = new LodePNGDecompressSettings();
        public uint ignore_crc;
        public uint ignore_critical;
        public uint ignore_end;
        public uint color_convert;
    }

    /// <summary>The settings, state and information for decoding.</summary>
    internal class LodePNGState
    {
        public LodePNGDecoderSettings decoder = new LodePNGDecoderSettings();
        public LodePNGColorMode info_raw = new LodePNGColorMode();
        public LodePNGInfo info_png = new LodePNGInfo();
        public uint error;
    }

    /// <summary>
    /// LodePNG-compatible PNG decoder. Line-by-line port of the C++ LodePNG library.
    /// </summary>
    public static class LodePng
    {
        /************************************************************************/
        /* Internal Class Implementation                                        */
        /************************************************************************/

        #region Utility functions

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint LODEPNG_MAX(uint a, uint b) => a > b ? a : b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int LODEPNG_ABS(int x) => x < 0 ? -x : x;

        private static void lodepng_memcpy(byte[] dst, int dstOffset, byte[] src, int srcOffset, int size)
        {
            Array.Copy(src, srcOffset, dst, dstOffset, size);
        }

        private static void lodepng_memset(byte[] dst, int dstOffset, byte value, int num)
        {
            for (int i = 0; i < num; i++) dst[dstOffset + i] = value;
        }

        /* Safely check if adding two integers will overflow and output result. */
        private static bool lodepng_addofl(long a, long b, out long result)
        {
            result = a + b;
            return result < a;
        }

        /* Safely check if multiplying two integers will overflow and output result. */
        private static bool lodepng_mulofl(long a, long b, out long result)
        {
            result = a * b;
            return (a != 0 && result / a != b);
        }

        /* Safely check if a + b > c, even if overflow could happen. */
        private static bool lodepng_gtofl(long a, long b, long c)
        {
            if (lodepng_addofl(a, b, out long d)) return true;
            return d > c;
        }

        private static uint lodepng_read32bitInt(byte[] buffer, int offset)
        {
            return (((uint)buffer[offset] << 24) | ((uint)buffer[offset + 1] << 16) |
                    ((uint)buffer[offset + 2] << 8) | (uint)buffer[offset + 3]);
        }

        #endregion

        #region UCVector

        /* dynamic vector of unsigned chars */
        private class UCVector
        {
            public byte[] data;
            public int size;       /*used size*/
            public int allocsize;  /*allocated size*/

            public UCVector(byte[]? buffer, int sz)
            {
                data = buffer ?? Array.Empty<byte>();
                allocsize = size = sz;
            }
        }

        /* returns true if success, false if failure */
        private static bool UCVector_resize(UCVector p, int size)
        {
            if (size > p.allocsize)
            {
                int newsize = size + (p.allocsize >> 1);
                var newData = new byte[newsize];
                if (p.data.Length > 0)
                    Array.Copy(p.data, 0, newData, 0, Math.Min(p.data.Length, newsize));
                p.allocsize = newsize;
                p.data = newData;
            }
            p.size = size;
            return true;
        }

        #endregion

        #region Bit Reader

        private class LodePNGBitReader
        {
            public byte[] data = Array.Empty<byte>();
            public int size;       /*size of data in bytes*/
            public long bitsize;   /*size of data in bits*/
            public long bp;
            public uint buffer;    /*buffer for reading bits*/
        }

        /* data size argument is in bytes. Returns error if size too large causing overflow */
        private static uint LodePNGBitReader_init(LodePNGBitReader reader, byte[] data, int dataOffset, int size)
        {
            /* Store data from offset into the reader. We copy the relevant slice. */
            if (dataOffset == 0)
            {
                reader.data = data;
            }
            else
            {
                reader.data = new byte[size];
                Array.Copy(data, dataOffset, reader.data, 0, size);
            }
            reader.size = size;
            if (lodepng_mulofl(size, 8, out reader.bitsize)) return 105;
            if (lodepng_addofl(reader.bitsize, 64, out _)) return 105;
            reader.bp = 0;
            reader.buffer = 0;
            return 0; /*ok*/
        }

        /*See ensureBits documentation. This one ensures up to 9 bits */
        private static bool ensureBits9(LodePNGBitReader reader, int nbits)
        {
            long start = reader.bp >> 3;
            int size = reader.size;
            if (start + 1 < size)
            {
                reader.buffer = (uint)reader.data[start] | ((uint)reader.data[start + 1] << 8);
                reader.buffer >>= (int)(reader.bp & 7);
                return true;
            }
            else
            {
                reader.buffer = 0;
                if (start + 0 < size) reader.buffer |= reader.data[start];
                reader.buffer >>= (int)(reader.bp & 7);
                return reader.bp + nbits <= reader.bitsize;
            }
        }

        /*See ensureBits documentation. This one ensures up to 17 bits */
        private static bool ensureBits17(LodePNGBitReader reader, int nbits)
        {
            long start = reader.bp >> 3;
            int size = reader.size;
            if (start + 2 < size)
            {
                reader.buffer = (uint)reader.data[start] |
                                ((uint)reader.data[start + 1] << 8) |
                                ((uint)reader.data[start + 2] << 16);
                reader.buffer >>= (int)(reader.bp & 7);
                return true;
            }
            else
            {
                reader.buffer = 0;
                if (start + 0 < size) reader.buffer |= reader.data[start];
                if (start + 1 < size) reader.buffer |= ((uint)reader.data[start + 1] << 8);
                reader.buffer >>= (int)(reader.bp & 7);
                return reader.bp + nbits <= reader.bitsize;
            }
        }

        /*See ensureBits documentation. This one ensures up to 25 bits */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ensureBits25(LodePNGBitReader reader, int nbits)
        {
            long start = reader.bp >> 3;
            int size = reader.size;
            if (start + 3 < size)
            {
                reader.buffer = (uint)reader.data[start] |
                                ((uint)reader.data[start + 1] << 8) |
                                ((uint)reader.data[start + 2] << 16) |
                                ((uint)reader.data[start + 3] << 24);
                reader.buffer >>= (int)(reader.bp & 7);
                return true;
            }
            else
            {
                reader.buffer = 0;
                if (start + 0 < size) reader.buffer |= reader.data[start];
                if (start + 1 < size) reader.buffer |= ((uint)reader.data[start + 1] << 8);
                if (start + 2 < size) reader.buffer |= ((uint)reader.data[start + 2] << 16);
                reader.buffer >>= (int)(reader.bp & 7);
                return reader.bp + nbits <= reader.bitsize;
            }
        }

        /*See ensureBits documentation. This one ensures up to 32 bits */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ensureBits32(LodePNGBitReader reader, int nbits)
        {
            long start = reader.bp >> 3;
            int size = reader.size;
            if (start + 4 < size)
            {
                reader.buffer = (uint)reader.data[start] |
                                ((uint)reader.data[start + 1] << 8) |
                                ((uint)reader.data[start + 2] << 16) |
                                ((uint)reader.data[start + 3] << 24);
                reader.buffer >>= (int)(reader.bp & 7);
                reader.buffer |= ((uint)reader.data[start + 4] << 24) << (int)(8 - (reader.bp & 7));
                return true;
            }
            else
            {
                reader.buffer = 0;
                if (start + 0 < size) reader.buffer |= reader.data[start];
                if (start + 1 < size) reader.buffer |= ((uint)reader.data[start + 1] << 8);
                if (start + 2 < size) reader.buffer |= ((uint)reader.data[start + 2] << 16);
                if (start + 3 < size) reader.buffer |= ((uint)reader.data[start + 3] << 24);
                reader.buffer >>= (int)(reader.bp & 7);
                return reader.bp + nbits <= reader.bitsize;
            }
        }

        /* Get bits without advancing the bit pointer. Must have enough bits available with ensureBits. Max nbits is 31. */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint peekBits(LodePNGBitReader reader, int nbits)
        {
            return reader.buffer & ((1u << nbits) - 1u);
        }

        /* Must have enough bits available with ensureBits */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void advanceBits(LodePNGBitReader reader, int nbits)
        {
            reader.buffer >>= nbits;
            reader.bp += nbits;
        }

        /* Must have enough bits available with ensureBits */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint readBits(LodePNGBitReader reader, int nbits)
        {
            uint result = peekBits(reader, nbits);
            advanceBits(reader, nbits);
            return result;
        }

        private static uint reverseBits(uint bits, uint num)
        {
            uint i, result = 0;
            for (i = 0; i < num; i++) result |= ((bits >> (int)(num - i - 1)) & 1u) << (int)i;
            return result;
        }

        #endregion

        #region Deflate - Huffman

        private const int FIRST_LENGTH_CODE_INDEX = 257;
        private const int LAST_LENGTH_CODE_INDEX = 285;
        private const int NUM_DEFLATE_CODE_SYMBOLS = 288;
        private const int NUM_DISTANCE_SYMBOLS = 32;
        private const int NUM_CODE_LENGTH_CODES = 19;

        private static readonly uint[] LENGTHBASE = {
            3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59,
            67, 83, 99, 115, 131, 163, 195, 227, 258
        };

        private static readonly uint[] LENGTHEXTRA = {
            0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3,
            4, 4, 4, 4, 5, 5, 5, 5, 0
        };

        private static readonly uint[] DISTANCEBASE = {
            1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513,
            769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577
        };

        private static readonly uint[] DISTANCEEXTRA = {
            0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8,
            8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13
        };

        private static readonly uint[] CLCL_ORDER = {
            16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15
        };

        private const int FIRSTBITS = 9;
        private const ushort INVALIDSYMBOL = 65535;

        private class HuffmanTree
        {
            public uint[]? codes;
            public uint[]? lengths;
            public uint maxbitlen;
            public uint numcodes;
            public byte[]? table_len;
            public ushort[]? table_value;
        }

        private static void HuffmanTree_init(HuffmanTree tree)
        {
            tree.codes = null;
            tree.lengths = null;
            tree.table_len = null;
            tree.table_value = null;
        }

        /* make table for huffman decoding */
        private static uint HuffmanTree_makeTable(HuffmanTree tree)
        {
            uint headsize = 1u << FIRSTBITS;
            uint mask = headsize - 1u;
            int numpresent;
            uint pointer;
            int size;
            var maxlens = new uint[headsize];

            /* compute maxlens: max total bit length of symbols sharing prefix in the first table */
            for (uint i = 0; i < tree.numcodes; i++)
            {
                uint symbol = tree.codes![i];
                uint l = tree.lengths![i];
                if (l <= FIRSTBITS) continue;
                uint index = reverseBits(symbol >> (int)(l - FIRSTBITS), FIRSTBITS);
                maxlens[index] = LODEPNG_MAX(maxlens[index], l);
            }

            /* compute total table size */
            size = (int)headsize;
            for (uint i = 0; i < headsize; ++i)
            {
                uint l = maxlens[i];
                if (l > FIRSTBITS) size += (int)(1u << (int)(l - FIRSTBITS));
            }

            tree.table_len = new byte[size];
            tree.table_value = new ushort[size];

            /* initialize with an invalid length to indicate unused entries */
            for (int i = 0; i < size; ++i) tree.table_len[i] = 16;

            /* fill in the first table for long symbols */
            pointer = headsize;
            for (uint i = 0; i < headsize; ++i)
            {
                uint l = maxlens[i];
                if (l <= FIRSTBITS) continue;
                tree.table_len[i] = (byte)l;
                tree.table_value[i] = (ushort)pointer;
                pointer += (1u << (int)(l - FIRSTBITS));
            }

            /* fill in the first table for short symbols, or secondary table for long symbols */
            numpresent = 0;
            for (uint i = 0; i < tree.numcodes; ++i)
            {
                uint l = tree.lengths![i];
                uint symbol = tree.codes![i];
                uint reverse = reverseBits(symbol, l);
                if (l == 0) continue;
                numpresent++;

                if (l <= FIRSTBITS)
                {
                    uint num = 1u << (int)(FIRSTBITS - l);
                    for (uint j = 0; j < num; ++j)
                    {
                        uint idx = reverse | (j << (int)l);
                        if (tree.table_len[idx] != 16) return 55;
                        tree.table_len[idx] = (byte)l;
                        tree.table_value[idx] = (ushort)i;
                    }
                }
                else
                {
                    uint idx = reverse & mask;
                    uint maxlen = tree.table_len[idx];
                    uint tablelen = maxlen - FIRSTBITS;
                    uint start = tree.table_value[idx];
                    uint num = 1u << (int)(tablelen - (l - FIRSTBITS));
                    if (maxlen < l) return 55;
                    for (uint j = 0; j < num; ++j)
                    {
                        uint reverse2 = reverse >> FIRSTBITS;
                        uint index2 = start + (reverse2 | (j << (int)(l - FIRSTBITS)));
                        tree.table_len[index2] = (byte)l;
                        tree.table_value[index2] = (ushort)i;
                    }
                }
            }

            if (numpresent < 2)
            {
                for (int i = 0; i < size; ++i)
                {
                    if (tree.table_len[i] == 16)
                    {
                        tree.table_len[i] = (byte)(i < (int)headsize ? 1 : (FIRSTBITS + 1));
                        tree.table_value[i] = INVALIDSYMBOL;
                    }
                }
            }
            else
            {
                for (int i = 0; i < size; ++i)
                {
                    if (tree.table_len[i] == 16) return 55;
                }
            }
            return 0;
        }

        private static uint HuffmanTree_makeFromLengths2(HuffmanTree tree)
        {
            uint error = 0;

            tree.codes = new uint[tree.numcodes];
            var blcount = new uint[tree.maxbitlen + 1];
            var nextcode = new uint[tree.maxbitlen + 1];

            /* step 1: count number of instances of each code length */
            for (uint bits = 0; bits != tree.numcodes; ++bits) ++blcount[tree.lengths![bits]];
            /* step 2: generate the nextcode values */
            for (uint bits = 1; bits <= tree.maxbitlen; ++bits)
            {
                nextcode[bits] = (nextcode[bits - 1] + blcount[bits - 1]) << 1;
            }
            /* step 3: generate all the codes */
            for (uint n = 0; n != tree.numcodes; ++n)
            {
                if (tree.lengths![n] != 0)
                {
                    tree.codes[n] = nextcode[tree.lengths[n]]++;
                    tree.codes[n] &= ((1u << (int)tree.lengths[n]) - 1u);
                }
            }

            if (error == 0) error = HuffmanTree_makeTable(tree);
            return error;
        }

        private static uint HuffmanTree_makeFromLengths(HuffmanTree tree, uint[] bitlen, int numcodes, uint maxbitlen)
        {
            tree.lengths = new uint[numcodes];
            for (int i = 0; i != numcodes; ++i) tree.lengths[i] = bitlen[i];
            tree.numcodes = (uint)numcodes;
            tree.maxbitlen = maxbitlen;
            return HuffmanTree_makeFromLengths2(tree);
        }

        private static uint generateFixedLitLenTree(HuffmanTree tree)
        {
            var bitlen = new uint[NUM_DEFLATE_CODE_SYMBOLS];
            for (int i = 0; i <= 143; ++i) bitlen[i] = 8;
            for (int i = 144; i <= 255; ++i) bitlen[i] = 9;
            for (int i = 256; i <= 279; ++i) bitlen[i] = 7;
            for (int i = 280; i <= 287; ++i) bitlen[i] = 8;
            return HuffmanTree_makeFromLengths(tree, bitlen, NUM_DEFLATE_CODE_SYMBOLS, 15);
        }

        private static uint generateFixedDistanceTree(HuffmanTree tree)
        {
            var bitlen = new uint[NUM_DISTANCE_SYMBOLS];
            for (int i = 0; i != NUM_DISTANCE_SYMBOLS; ++i) bitlen[i] = 5;
            return HuffmanTree_makeFromLengths(tree, bitlen, NUM_DISTANCE_SYMBOLS, 15);
        }

        private static uint huffmanDecodeSymbol(LodePNGBitReader reader, HuffmanTree codetree)
        {
            ushort code = (ushort)peekBits(reader, FIRSTBITS);
            ushort l = codetree.table_len![code];
            ushort value = codetree.table_value![code];
            if (l <= FIRSTBITS)
            {
                advanceBits(reader, l);
                return value;
            }
            else
            {
                advanceBits(reader, FIRSTBITS);
                uint index2 = (uint)(value + peekBits(reader, l - FIRSTBITS));
                advanceBits(reader, codetree.table_len[index2] - FIRSTBITS);
                return codetree.table_value[index2];
            }
        }

        #endregion

        #region Inflator (Decompressor)

        private static uint getTreeInflateFixed(HuffmanTree tree_ll, HuffmanTree tree_d)
        {
            uint error = generateFixedLitLenTree(tree_ll);
            if (error != 0) return error;
            return generateFixedDistanceTree(tree_d);
        }

        private static uint getTreeInflateDynamic(HuffmanTree tree_ll, HuffmanTree tree_d, LodePNGBitReader reader)
        {
            uint error = 0;
            uint HLIT, HDIST, HCLEN;

            uint[]? bitlen_ll = null;
            uint[]? bitlen_d = null;
            uint[] bitlen_cl;
            var tree_cl = new HuffmanTree();

            if (!ensureBits17(reader, 14)) return 49;

            HLIT = readBits(reader, 5) + 257;
            HDIST = readBits(reader, 5) + 1;
            HCLEN = readBits(reader, 4) + 4;

            bitlen_cl = new uint[NUM_CODE_LENGTH_CODES];

            HuffmanTree_init(tree_cl);

            do
            {
                /* read the code length codes out of 3 * (amount of code length codes) bits */
                if (lodepng_gtofl(reader.bp, HCLEN * 3, reader.bitsize))
                {
                    error = 50;
                    break;
                }
                for (uint i = 0; i != HCLEN; ++i)
                {
                    ensureBits9(reader, 3);
                    bitlen_cl[CLCL_ORDER[i]] = readBits(reader, 3);
                }
                for (uint i = HCLEN; i != NUM_CODE_LENGTH_CODES; ++i)
                {
                    bitlen_cl[CLCL_ORDER[i]] = 0;
                }

                error = HuffmanTree_makeFromLengths(tree_cl, bitlen_cl, NUM_CODE_LENGTH_CODES, 7);
                if (error != 0) break;

                bitlen_ll = new uint[NUM_DEFLATE_CODE_SYMBOLS];
                bitlen_d = new uint[NUM_DISTANCE_SYMBOLS];

                uint idx = 0;
                while (idx < HLIT + HDIST)
                {
                    ensureBits25(reader, 22);
                    uint code = huffmanDecodeSymbol(reader, tree_cl);
                    if (code <= 15)
                    {
                        if (idx < HLIT) bitlen_ll[idx] = code;
                        else bitlen_d[idx - HLIT] = code;
                        ++idx;
                    }
                    else if (code == 16)
                    {
                        uint replength = 3;
                        uint value;
                        if (idx == 0) { error = 54; break; }
                        replength += readBits(reader, 2);
                        if (idx < HLIT + 1) value = bitlen_ll[idx - 1];
                        else value = bitlen_d[idx - HLIT - 1];
                        for (uint n = 0; n < replength; ++n)
                        {
                            if (idx >= HLIT + HDIST) { error = 13; break; }
                            if (idx < HLIT) bitlen_ll[idx] = value;
                            else bitlen_d[idx - HLIT] = value;
                            ++idx;
                        }
                    }
                    else if (code == 17)
                    {
                        uint replength = 3;
                        replength += readBits(reader, 3);
                        for (uint n = 0; n < replength; ++n)
                        {
                            if (idx >= HLIT + HDIST) { error = 14; break; }
                            if (idx < HLIT) bitlen_ll[idx] = 0;
                            else bitlen_d[idx - HLIT] = 0;
                            ++idx;
                        }
                    }
                    else if (code == 18)
                    {
                        uint replength = 11;
                        replength += readBits(reader, 7);
                        for (uint n = 0; n < replength; ++n)
                        {
                            if (idx >= HLIT + HDIST) { error = 15; break; }
                            if (idx < HLIT) bitlen_ll[idx] = 0;
                            else bitlen_d[idx - HLIT] = 0;
                            ++idx;
                        }
                    }
                    else
                    {
                        error = 16;
                        break;
                    }
                    if (error != 0) break;
                    if (reader.bp > reader.bitsize) { error = 50; break; }
                }
                if (error != 0) break;

                if (bitlen_ll[256] == 0) { error = 64; break; }

                error = HuffmanTree_makeFromLengths(tree_ll, bitlen_ll, NUM_DEFLATE_CODE_SYMBOLS, 15);
                if (error != 0) break;
                error = HuffmanTree_makeFromLengths(tree_d, bitlen_d, NUM_DISTANCE_SYMBOLS, 15);

                break; /* end of error-while */
            } while (false);

            return error;
        }

        private static uint inflateHuffmanBlock(UCVector outv, LodePNGBitReader reader, uint btype)
        {
            uint error = 0;
            var tree_ll = new HuffmanTree();
            var tree_d = new HuffmanTree();

            HuffmanTree_init(tree_ll);
            HuffmanTree_init(tree_d);

            if (btype == 1) error = getTreeInflateFixed(tree_ll, tree_d);
            else error = getTreeInflateDynamic(tree_ll, tree_d, reader);

            while (error == 0)
            {
                uint code_ll;
                ensureBits25(reader, 20);
                code_ll = huffmanDecodeSymbol(reader, tree_ll);
                if (code_ll <= 255)
                {
                    if (!UCVector_resize(outv, outv.size + 1)) { error = 83; break; }
                    outv.data[outv.size - 1] = (byte)code_ll;
                }
                else if (code_ll >= FIRST_LENGTH_CODE_INDEX && code_ll <= LAST_LENGTH_CODE_INDEX)
                {
                    uint code_d, distance;
                    uint numextrabits_l, numextrabits_d;
                    int start, backward;
                    long length;

                    length = LENGTHBASE[code_ll - FIRST_LENGTH_CODE_INDEX];
                    numextrabits_l = LENGTHEXTRA[code_ll - FIRST_LENGTH_CODE_INDEX];
                    if (numextrabits_l != 0) length += readBits(reader, (int)numextrabits_l);

                    ensureBits32(reader, 28);
                    code_d = huffmanDecodeSymbol(reader, tree_d);
                    if (code_d > 29)
                    {
                        if (code_d <= 31) { error = 18; break; }
                        else { error = 16; break; }
                    }
                    distance = DISTANCEBASE[code_d];
                    numextrabits_d = DISTANCEEXTRA[code_d];
                    if (numextrabits_d != 0) distance += readBits(reader, (int)numextrabits_d);

                    start = outv.size;
                    if (distance > start) { error = 52; break; }
                    backward = start - (int)distance;

                    if (!UCVector_resize(outv, (int)(outv.size + length))) { error = 83; break; }
                    if (distance < (uint)length)
                    {
                        int fwd;
                        Array.Copy(outv.data, backward, outv.data, start, (int)distance);
                        start += (int)distance;
                        for (fwd = (int)distance; fwd < length; ++fwd)
                        {
                            outv.data[start++] = outv.data[backward++];
                        }
                    }
                    else
                    {
                        Array.Copy(outv.data, backward, outv.data, start, (int)length);
                    }
                }
                else if (code_ll == 256)
                {
                    break; /* end code */
                }
                else
                {
                    error = 16;
                    break;
                }
                if (reader.bp > reader.bitsize)
                {
                    error = 51;
                    break;
                }
            }

            return error;
        }

        private static uint inflateNoCompression(UCVector outv, LodePNGBitReader reader, LodePNGDecompressSettings settings)
        {
            long bytepos;
            int size = reader.size;

            /* go to first boundary of byte */
            bytepos = (reader.bp + 7) >> 3;

            if (bytepos + 4 >= size) return 52;
            uint LEN = (uint)reader.data[bytepos] + ((uint)reader.data[bytepos + 1] << 8); bytepos += 2;
            uint NLEN = (uint)reader.data[bytepos] + ((uint)reader.data[bytepos + 1] << 8); bytepos += 2;

            if (settings.ignore_nlen == 0 && LEN + NLEN != 65535) return 21;

            if (!UCVector_resize(outv, (int)(outv.size + LEN))) return 83;

            if (bytepos + LEN > size) return 23;

            Array.Copy(reader.data, (int)bytepos, outv.data, (int)(outv.size - LEN), (int)LEN);
            bytepos += LEN;

            reader.bp = bytepos << 3;

            return 0;
        }

        private static uint lodepng_inflatev(UCVector outv, byte[] data, int dataOffset, int insize, LodePNGDecompressSettings settings)
        {
            uint BFINAL = 0;
            var reader = new LodePNGBitReader();
            uint error = LodePNGBitReader_init(reader, data, dataOffset, insize);

            if (error != 0) return error;

            while (BFINAL == 0)
            {
                uint BTYPE;
                if (!ensureBits9(reader, 3)) return 52;
                BFINAL = readBits(reader, 1);
                BTYPE = readBits(reader, 2);

                if (BTYPE == 3) return 20;
                else if (BTYPE == 0) error = inflateNoCompression(outv, reader, settings);
                else error = inflateHuffmanBlock(outv, reader, BTYPE);

                if (error != 0) return error;
            }

            return error;
        }

        #endregion

        #region Adler32

        private static uint update_adler32(uint adler, byte[] data, int offset, int len)
        {
            uint s1 = adler & 0xffffu;
            uint s2 = (adler >> 16) & 0xffffu;

            int pos = offset;
            while (len != 0)
            {
                uint amount = (uint)(len > 5552 ? 5552 : len);
                len -= (int)amount;
                for (uint i = 0; i != amount; ++i)
                {
                    s1 += data[pos++];
                    s2 += s1;
                }
                s1 %= 65521u;
                s2 %= 65521u;
            }

            return (s2 << 16) | s1;
        }

        private static uint adler32(byte[] data, int offset, int len)
        {
            return update_adler32(1u, data, offset, len);
        }

        #endregion

        #region Zlib

        private static uint lodepng_zlib_decompressv(UCVector outv, byte[] data, int dataOffset, int insize, LodePNGDecompressSettings settings)
        {
            if (insize < 2) return 53;
            if ((data[dataOffset] * 256 + data[dataOffset + 1]) % 31 != 0) return 24;

            uint CM = (uint)(data[dataOffset] & 15);
            uint CINFO = (uint)((data[dataOffset] >> 4) & 15);
            uint FDICT = (uint)((data[dataOffset + 1] >> 5) & 1);

            if (CM != 8 || CINFO > 7) return 25;
            if (FDICT != 0) return 26;

            uint error = lodepng_inflatev(outv, data, dataOffset + 2, insize - 2, settings);
            if (error != 0) return error;

            if (settings.ignore_adler32 == 0)
            {
                uint ADLER32 = lodepng_read32bitInt(data, dataOffset + insize - 4);
                uint checksum = adler32(outv.data, 0, outv.size);
                if (checksum != ADLER32) return 58;
            }

            return 0;
        }

        private static uint zlib_decompress(ref byte[]? outBuf, ref int outsize, int expected_size,
            byte[] data, int dataOffset, int insize, LodePNGDecompressSettings settings)
        {
            var v = new UCVector(outBuf, outsize);
            if (expected_size != 0)
            {
                UCVector_resize(v, outsize + expected_size);
                v.size = outsize;
            }
            uint error = lodepng_zlib_decompressv(v, data, dataOffset, insize, settings);
            outBuf = v.data;
            outsize = v.size;
            return error;
        }

        private static void lodepng_decompress_settings_init(LodePNGDecompressSettings settings)
        {
            settings.ignore_adler32 = 0;
            settings.ignore_nlen = 0;
        }

        #endregion

        #region PNG Color Channel Bits

        private static byte readBitFromReversedStream(ref long bitpointer, byte[] bitstream)
        {
            byte result = (byte)((bitstream[bitpointer >> 3] >> (int)(7 - (bitpointer & 0x7))) & 1);
            ++bitpointer;
            return result;
        }

        private static uint readBitsFromReversedStream(ref long bitpointer, byte[] bitstream, uint nbits)
        {
            uint result = 0;
            for (uint i = 0; i < nbits; ++i)
            {
                result <<= 1;
                result |= readBitFromReversedStream(ref bitpointer, bitstream);
            }
            return result;
        }

        private static void setBitOfReversedStream(ref long bitpointer, byte[] bitstream, byte bit)
        {
            if (bit == 0) bitstream[bitpointer >> 3] &= (byte)(~(1u << (int)(7 - (bitpointer & 7))));
            else bitstream[bitpointer >> 3] |= (byte)(1u << (int)(7 - (bitpointer & 7)));
            ++bitpointer;
        }

        #endregion

        #region PNG Chunks

        private static uint lodepng_chunk_length(byte[] chunk, int offset)
        {
            return lodepng_read32bitInt(chunk, offset);
        }

        private static bool lodepng_chunk_type_equals(byte[] chunk, int offset, string type)
        {
            if (type.Length != 4) return false;
            return (chunk[offset + 4] == (byte)type[0] && chunk[offset + 5] == (byte)type[1] &&
                    chunk[offset + 6] == (byte)type[2] && chunk[offset + 7] == (byte)type[3]);
        }

        private static bool lodepng_chunk_ancillary(byte[] chunk, int offset)
        {
            return (chunk[offset + 4] & 32) != 0;
        }

        private static int lodepng_chunk_next_const(byte[] chunk, int offset, int end)
        {
            if (offset >= end || end - offset < 12) return end;
            if (chunk[offset] == 0x89 && chunk[offset + 1] == 0x50 && chunk[offset + 2] == 0x4e && chunk[offset + 3] == 0x47
                && chunk[offset + 4] == 0x0d && chunk[offset + 5] == 0x0a && chunk[offset + 6] == 0x1a && chunk[offset + 7] == 0x0a)
            {
                return offset + 8;
            }
            else
            {
                uint chunkLen = lodepng_chunk_length(chunk, offset);
                long total;
                if (lodepng_addofl(chunkLen, 12, out total)) return end;
                int result = offset + (int)total;
                if (result < offset) return end;
                return result;
            }
        }

        #endregion

        #region Color types, channels, bits

        private static uint checkColorValidity(LodePNGColorType colortype, uint bd)
        {
            switch (colortype)
            {
                case LodePNGColorType.LCT_GREY:       if (!(bd == 1 || bd == 2 || bd == 4 || bd == 8 || bd == 16)) return 37; break;
                case LodePNGColorType.LCT_RGB:         if (!(bd == 8 || bd == 16)) return 37; break;
                case LodePNGColorType.LCT_PALETTE:     if (!(bd == 1 || bd == 2 || bd == 4 || bd == 8)) return 37; break;
                case LodePNGColorType.LCT_GREY_ALPHA:  if (!(bd == 8 || bd == 16)) return 37; break;
                case LodePNGColorType.LCT_RGBA:        if (!(bd == 8 || bd == 16)) return 37; break;
                case LodePNGColorType.LCT_MAX_OCTET_VALUE: return 31;
                default: return 31;
            }
            return 0;
        }

        private static uint getNumColorChannels(LodePNGColorType colortype)
        {
            switch (colortype)
            {
                case LodePNGColorType.LCT_GREY: return 1;
                case LodePNGColorType.LCT_RGB: return 3;
                case LodePNGColorType.LCT_PALETTE: return 1;
                case LodePNGColorType.LCT_GREY_ALPHA: return 2;
                case LodePNGColorType.LCT_RGBA: return 4;
                case LodePNGColorType.LCT_MAX_OCTET_VALUE: return 0;
                default: return 0;
            }
        }

        private static uint lodepng_get_bpp_lct(LodePNGColorType colortype, uint bitdepth)
        {
            return getNumColorChannels(colortype) * bitdepth;
        }

        private static void lodepng_color_mode_init(LodePNGColorMode info)
        {
            info.key_defined = 0;
            info.key_r = info.key_g = info.key_b = 0;
            info.colortype = LodePNGColorType.LCT_RGBA;
            info.bitdepth = 8;
            info.palette = null;
            info.palettesize = 0;
        }

        private static void lodepng_color_mode_alloc_palette(LodePNGColorMode info)
        {
            if (info.palette == null) info.palette = new byte[1024];
            for (int i = 0; i < 256; ++i)
            {
                info.palette[i * 4 + 0] = 0;
                info.palette[i * 4 + 1] = 0;
                info.palette[i * 4 + 2] = 0;
                info.palette[i * 4 + 3] = 255;
            }
        }

        private static void lodepng_palette_clear(LodePNGColorMode info)
        {
            info.palette = null;
            info.palettesize = 0;
        }

        private static void lodepng_color_mode_cleanup(LodePNGColorMode info)
        {
            lodepng_palette_clear(info);
        }

        private static uint lodepng_color_mode_copy(LodePNGColorMode dest, LodePNGColorMode source)
        {
            lodepng_color_mode_cleanup(dest);
            dest.colortype = source.colortype;
            dest.bitdepth = source.bitdepth;
            dest.key_defined = source.key_defined;
            dest.key_r = source.key_r;
            dest.key_g = source.key_g;
            dest.key_b = source.key_b;
            dest.palettesize = source.palettesize;
            if (source.palette != null)
            {
                dest.palette = new byte[1024];
                Array.Copy(source.palette, dest.palette, source.palettesize * 4);
            }
            else
            {
                dest.palette = null;
            }
            return 0;
        }

        private static bool lodepng_color_mode_equal(LodePNGColorMode a, LodePNGColorMode b)
        {
            if (a.colortype != b.colortype) return false;
            if (a.bitdepth != b.bitdepth) return false;
            if (a.key_defined != b.key_defined) return false;
            if (a.key_defined != 0)
            {
                if (a.key_r != b.key_r) return false;
                if (a.key_g != b.key_g) return false;
                if (a.key_b != b.key_b) return false;
            }
            if (a.palettesize != b.palettesize) return false;
            for (int i = 0; i < a.palettesize * 4; ++i)
            {
                if (a.palette![i] != b.palette![i]) return false;
            }
            return true;
        }

        private static long lodepng_get_raw_size_lct(uint w, uint h, LodePNGColorType colortype, uint bitdepth)
        {
            long bpp = lodepng_get_bpp_lct(colortype, bitdepth);
            long n = (long)w * (long)h;
            return ((n / 8) * bpp) + ((n & 7) * bpp + 7) / 8;
        }

        private static long lodepng_get_raw_size(uint w, uint h, LodePNGColorMode color)
        {
            return lodepng_get_raw_size_lct(w, h, color.colortype, color.bitdepth);
        }

        private static long lodepng_get_raw_size_idat(uint w, uint h, uint bpp)
        {
            long line = ((long)(w / 8u) * bpp) + 1 + ((w & 7u) * bpp + 7u) / 8u;
            return (long)h * line;
        }

        private static bool lodepng_pixel_overflow(uint w, uint h, LodePNGColorMode pngcolor, LodePNGColorMode rawcolor)
        {
            long bpp = LODEPNG_MAX(lodepng_get_bpp_lct(pngcolor.colortype, pngcolor.bitdepth),
                                   lodepng_get_bpp_lct(rawcolor.colortype, rawcolor.bitdepth));
            if (lodepng_mulofl((long)w, (long)h, out long numpixels)) return true;
            if (lodepng_mulofl(numpixels, 8, out _)) return true;

            if (lodepng_mulofl((long)(w / 8u), bpp, out long line)) return true;
            if (lodepng_addofl(line, (long)((w & 7u) * (ulong)bpp + 7u) / 8, out line)) return true;
            if (lodepng_addofl(line, 5, out line)) return true;
            if (lodepng_mulofl(line, (long)h, out _)) return true;

            return false;
        }

        private static void lodepng_info_init(LodePNGInfo info)
        {
            lodepng_color_mode_init(info.color);
            info.interlace_method = 0;
            info.compression_method = 0;
            info.filter_method = 0;
        }

        private static void lodepng_info_cleanup(LodePNGInfo info)
        {
            lodepng_color_mode_cleanup(info.color);
        }

        #endregion

        #region Color Conversion

        /* index: bitgroup index, bits: bitgroup size(1, 2 or 4), in: bitgroup value, out: octet array to add bits to */
        private static void addColorBits(byte[] outBuf, int outOffset, long index, uint bits, uint inVal)
        {
            uint m = bits == 1 ? 7u : bits == 2 ? 3u : 1u;
            uint p = (uint)(index & m);
            inVal &= (1u << (int)bits) - 1u;
            inVal = inVal << (int)(bits * (m - p));
            if (p == 0) outBuf[outOffset + index * bits / 8] = (byte)inVal;
            else outBuf[outOffset + index * bits / 8] |= (byte)inVal;
        }

        /* Color tree for palette lookup */
        private class ColorTree
        {
            public ColorTree?[] children = new ColorTree?[16];
            public int index = -1;
        }

        private static int color_tree_get(ColorTree tree, byte r, byte g, byte b, byte a)
        {
            for (int bit = 0; bit < 8; ++bit)
            {
                int i = 8 * ((r >> bit) & 1) + 4 * ((g >> bit) & 1) + 2 * ((b >> bit) & 1) + 1 * ((a >> bit) & 1);
                if (tree.children[i] == null) return -1;
                tree = tree.children[i]!;
            }
            return tree.index;
        }

        private static uint color_tree_add(ColorTree tree, byte r, byte g, byte b, byte a, uint index)
        {
            for (int bit = 0; bit < 8; ++bit)
            {
                int i = 8 * ((r >> bit) & 1) + 4 * ((g >> bit) & 1) + 2 * ((b >> bit) & 1) + 1 * ((a >> bit) & 1);
                if (tree.children[i] == null)
                {
                    tree.children[i] = new ColorTree();
                }
                tree = tree.children[i]!;
            }
            tree.index = (int)index;
            return 0;
        }

        /* put a pixel, given its RGBA color, into image of any color type */
        private static uint rgba8ToPixel(byte[] outBuf, int outOffset, long i, LodePNGColorMode mode, ColorTree? tree,
            byte r, byte g, byte b, byte a)
        {
            if (mode.colortype == LodePNGColorType.LCT_GREY)
            {
                byte gray = r;
                if (mode.bitdepth == 8) outBuf[outOffset + i] = gray;
                else if (mode.bitdepth == 16) { outBuf[outOffset + i * 2] = gray; outBuf[outOffset + i * 2 + 1] = gray; }
                else
                {
                    gray = (byte)(((uint)gray >> (int)(8u - mode.bitdepth)) & ((1u << (int)mode.bitdepth) - 1u));
                    addColorBits(outBuf, outOffset, i, mode.bitdepth, gray);
                }
            }
            else if (mode.colortype == LodePNGColorType.LCT_RGB)
            {
                if (mode.bitdepth == 8)
                {
                    outBuf[outOffset + i * 3] = r;
                    outBuf[outOffset + i * 3 + 1] = g;
                    outBuf[outOffset + i * 3 + 2] = b;
                }
                else
                {
                    outBuf[outOffset + i * 6] = outBuf[outOffset + i * 6 + 1] = r;
                    outBuf[outOffset + i * 6 + 2] = outBuf[outOffset + i * 6 + 3] = g;
                    outBuf[outOffset + i * 6 + 4] = outBuf[outOffset + i * 6 + 5] = b;
                }
            }
            else if (mode.colortype == LodePNGColorType.LCT_PALETTE)
            {
                int idx = color_tree_get(tree!, r, g, b, a);
                if (idx < 0) return 82;
                if (mode.bitdepth == 8) outBuf[outOffset + i] = (byte)idx;
                else addColorBits(outBuf, outOffset, i, mode.bitdepth, (uint)idx);
            }
            else if (mode.colortype == LodePNGColorType.LCT_GREY_ALPHA)
            {
                byte gray = r;
                if (mode.bitdepth == 8)
                {
                    outBuf[outOffset + i * 2] = gray;
                    outBuf[outOffset + i * 2 + 1] = a;
                }
                else if (mode.bitdepth == 16)
                {
                    outBuf[outOffset + i * 4] = outBuf[outOffset + i * 4 + 1] = gray;
                    outBuf[outOffset + i * 4 + 2] = outBuf[outOffset + i * 4 + 3] = a;
                }
            }
            else if (mode.colortype == LodePNGColorType.LCT_RGBA)
            {
                if (mode.bitdepth == 8)
                {
                    outBuf[outOffset + i * 4] = r;
                    outBuf[outOffset + i * 4 + 1] = g;
                    outBuf[outOffset + i * 4 + 2] = b;
                    outBuf[outOffset + i * 4 + 3] = a;
                }
                else
                {
                    outBuf[outOffset + i * 8] = outBuf[outOffset + i * 8 + 1] = r;
                    outBuf[outOffset + i * 8 + 2] = outBuf[outOffset + i * 8 + 3] = g;
                    outBuf[outOffset + i * 8 + 4] = outBuf[outOffset + i * 8 + 5] = b;
                    outBuf[outOffset + i * 8 + 6] = outBuf[outOffset + i * 8 + 7] = a;
                }
            }
            return 0;
        }

        /* put a pixel, given its RGBA16 color, into image of any color 16-bitdepth type */
        private static void rgba16ToPixel(byte[] outBuf, int outOffset, long i, LodePNGColorMode mode,
            ushort r, ushort g, ushort b, ushort a)
        {
            if (mode.colortype == LodePNGColorType.LCT_GREY)
            {
                ushort gray = r;
                outBuf[outOffset + i * 2] = (byte)((gray >> 8) & 255);
                outBuf[outOffset + i * 2 + 1] = (byte)(gray & 255);
            }
            else if (mode.colortype == LodePNGColorType.LCT_RGB)
            {
                outBuf[outOffset + i * 6] = (byte)((r >> 8) & 255);
                outBuf[outOffset + i * 6 + 1] = (byte)(r & 255);
                outBuf[outOffset + i * 6 + 2] = (byte)((g >> 8) & 255);
                outBuf[outOffset + i * 6 + 3] = (byte)(g & 255);
                outBuf[outOffset + i * 6 + 4] = (byte)((b >> 8) & 255);
                outBuf[outOffset + i * 6 + 5] = (byte)(b & 255);
            }
            else if (mode.colortype == LodePNGColorType.LCT_GREY_ALPHA)
            {
                ushort gray = r;
                outBuf[outOffset + i * 4] = (byte)((gray >> 8) & 255);
                outBuf[outOffset + i * 4 + 1] = (byte)(gray & 255);
                outBuf[outOffset + i * 4 + 2] = (byte)((a >> 8) & 255);
                outBuf[outOffset + i * 4 + 3] = (byte)(a & 255);
            }
            else if (mode.colortype == LodePNGColorType.LCT_RGBA)
            {
                outBuf[outOffset + i * 8] = (byte)((r >> 8) & 255);
                outBuf[outOffset + i * 8 + 1] = (byte)(r & 255);
                outBuf[outOffset + i * 8 + 2] = (byte)((g >> 8) & 255);
                outBuf[outOffset + i * 8 + 3] = (byte)(g & 255);
                outBuf[outOffset + i * 8 + 4] = (byte)((b >> 8) & 255);
                outBuf[outOffset + i * 8 + 5] = (byte)(b & 255);
                outBuf[outOffset + i * 8 + 6] = (byte)((a >> 8) & 255);
                outBuf[outOffset + i * 8 + 7] = (byte)(a & 255);
            }
        }

        /* Get RGBA8 color of pixel with index i from the raw image with given color type. */
        private static void getPixelColorRGBA8(out byte r, out byte g, out byte b, out byte a,
            byte[] inBuf, int inOffset, long i, LodePNGColorMode mode)
        {
            r = g = b = a = 0;
            if (mode.colortype == LodePNGColorType.LCT_GREY)
            {
                if (mode.bitdepth == 8)
                {
                    r = g = b = inBuf[inOffset + i];
                    if (mode.key_defined != 0 && r == mode.key_r) a = 0;
                    else a = 255;
                }
                else if (mode.bitdepth == 16)
                {
                    r = g = b = inBuf[inOffset + i * 2];
                    if (mode.key_defined != 0 && 256u * inBuf[inOffset + i * 2] + inBuf[inOffset + i * 2 + 1] == mode.key_r) a = 0;
                    else a = 255;
                }
                else
                {
                    uint highest = (1u << (int)mode.bitdepth) - 1u;
                    long j = i * mode.bitdepth;
                    uint value = readBitsFromReversedStream(ref j, inBuf, mode.bitdepth);
                    r = g = b = (byte)((value * 255) / highest);
                    if (mode.key_defined != 0 && value == mode.key_r) a = 0;
                    else a = 255;
                }
            }
            else if (mode.colortype == LodePNGColorType.LCT_RGB)
            {
                if (mode.bitdepth == 8)
                {
                    r = inBuf[inOffset + i * 3]; g = inBuf[inOffset + i * 3 + 1]; b = inBuf[inOffset + i * 3 + 2];
                    if (mode.key_defined != 0 && r == mode.key_r && g == mode.key_g && b == mode.key_b) a = 0;
                    else a = 255;
                }
                else
                {
                    r = inBuf[inOffset + i * 6];
                    g = inBuf[inOffset + i * 6 + 2];
                    b = inBuf[inOffset + i * 6 + 4];
                    if (mode.key_defined != 0 && 256u * inBuf[inOffset + i * 6] + inBuf[inOffset + i * 6 + 1] == mode.key_r
                        && 256u * inBuf[inOffset + i * 6 + 2] + inBuf[inOffset + i * 6 + 3] == mode.key_g
                        && 256u * inBuf[inOffset + i * 6 + 4] + inBuf[inOffset + i * 6 + 5] == mode.key_b) a = 0;
                    else a = 255;
                }
            }
            else if (mode.colortype == LodePNGColorType.LCT_PALETTE)
            {
                uint index;
                if (mode.bitdepth == 8) index = inBuf[inOffset + i];
                else
                {
                    long j = i * mode.bitdepth;
                    index = readBitsFromReversedStream(ref j, inBuf, mode.bitdepth);
                }
                r = mode.palette![index * 4];
                g = mode.palette[index * 4 + 1];
                b = mode.palette[index * 4 + 2];
                a = mode.palette[index * 4 + 3];
            }
            else if (mode.colortype == LodePNGColorType.LCT_GREY_ALPHA)
            {
                if (mode.bitdepth == 8)
                {
                    r = g = b = inBuf[inOffset + i * 2];
                    a = inBuf[inOffset + i * 2 + 1];
                }
                else
                {
                    r = g = b = inBuf[inOffset + i * 4];
                    a = inBuf[inOffset + i * 4 + 2];
                }
            }
            else if (mode.colortype == LodePNGColorType.LCT_RGBA)
            {
                if (mode.bitdepth == 8)
                {
                    r = inBuf[inOffset + i * 4];
                    g = inBuf[inOffset + i * 4 + 1];
                    b = inBuf[inOffset + i * 4 + 2];
                    a = inBuf[inOffset + i * 4 + 3];
                }
                else
                {
                    r = inBuf[inOffset + i * 8];
                    g = inBuf[inOffset + i * 8 + 2];
                    b = inBuf[inOffset + i * 8 + 4];
                    a = inBuf[inOffset + i * 8 + 6];
                }
            }
        }

        /* Optimized batch conversion to RGBA8. */
        private static void getPixelColorsRGBA8(byte[] buffer, int bufOffset, long numpixels,
            byte[] inBuf, int inOffset, LodePNGColorMode mode)
        {
            int num_channels = 4;
            int bp = bufOffset;
            if (mode.colortype == LodePNGColorType.LCT_GREY)
            {
                if (mode.bitdepth == 8)
                {
                    for (long i = 0; i < numpixels; ++i, bp += num_channels)
                    {
                        buffer[bp] = buffer[bp + 1] = buffer[bp + 2] = inBuf[inOffset + i];
                        buffer[bp + 3] = 255;
                    }
                    if (mode.key_defined != 0)
                    {
                        bp = bufOffset;
                        for (long i = 0; i < numpixels; ++i, bp += num_channels)
                        {
                            if (buffer[bp] == mode.key_r) buffer[bp + 3] = 0;
                        }
                    }
                }
                else if (mode.bitdepth == 16)
                {
                    for (long i = 0; i < numpixels; ++i, bp += num_channels)
                    {
                        buffer[bp] = buffer[bp + 1] = buffer[bp + 2] = inBuf[inOffset + i * 2];
                        buffer[bp + 3] = (byte)(mode.key_defined != 0 && 256u * inBuf[inOffset + i * 2] + inBuf[inOffset + i * 2 + 1] == mode.key_r ? 0 : 255);
                    }
                }
                else
                {
                    uint highest = (1u << (int)mode.bitdepth) - 1u;
                    long j = 0;
                    for (long i = 0; i < numpixels; ++i, bp += num_channels)
                    {
                        uint value = readBitsFromReversedStream(ref j, inBuf, mode.bitdepth);
                        buffer[bp] = buffer[bp + 1] = buffer[bp + 2] = (byte)((value * 255) / highest);
                        buffer[bp + 3] = (byte)(mode.key_defined != 0 && value == mode.key_r ? 0 : 255);
                    }
                }
            }
            else if (mode.colortype == LodePNGColorType.LCT_RGB)
            {
                if (mode.bitdepth == 8)
                {
                    for (long i = 0; i < numpixels; ++i, bp += num_channels)
                    {
                        Array.Copy(inBuf, (int)(inOffset + i * 3), buffer, bp, 3);
                        buffer[bp + 3] = 255;
                    }
                    if (mode.key_defined != 0)
                    {
                        bp = bufOffset;
                        for (long i = 0; i < numpixels; ++i, bp += num_channels)
                        {
                            if (buffer[bp] == mode.key_r && buffer[bp + 1] == mode.key_g && buffer[bp + 2] == mode.key_b) buffer[bp + 3] = 0;
                        }
                    }
                }
                else
                {
                    for (long i = 0; i < numpixels; ++i, bp += num_channels)
                    {
                        buffer[bp] = inBuf[inOffset + i * 6];
                        buffer[bp + 1] = inBuf[inOffset + i * 6 + 2];
                        buffer[bp + 2] = inBuf[inOffset + i * 6 + 4];
                        buffer[bp + 3] = (byte)(mode.key_defined != 0
                            && 256u * inBuf[inOffset + i * 6] + inBuf[inOffset + i * 6 + 1] == mode.key_r
                            && 256u * inBuf[inOffset + i * 6 + 2] + inBuf[inOffset + i * 6 + 3] == mode.key_g
                            && 256u * inBuf[inOffset + i * 6 + 4] + inBuf[inOffset + i * 6 + 5] == mode.key_b ? 0 : 255);
                    }
                }
            }
            else if (mode.colortype == LodePNGColorType.LCT_PALETTE)
            {
                if (mode.bitdepth == 8)
                {
                    for (long i = 0; i < numpixels; ++i, bp += num_channels)
                    {
                        uint index = inBuf[inOffset + i];
                        Array.Copy(mode.palette!, (int)(index * 4), buffer, bp, 4);
                    }
                }
                else
                {
                    long j = 0;
                    for (long i = 0; i < numpixels; ++i, bp += num_channels)
                    {
                        uint index = readBitsFromReversedStream(ref j, inBuf, mode.bitdepth);
                        Array.Copy(mode.palette!, (int)(index * 4), buffer, bp, 4);
                    }
                }
            }
            else if (mode.colortype == LodePNGColorType.LCT_GREY_ALPHA)
            {
                if (mode.bitdepth == 8)
                {
                    for (long i = 0; i < numpixels; ++i, bp += num_channels)
                    {
                        buffer[bp] = buffer[bp + 1] = buffer[bp + 2] = inBuf[inOffset + i * 2];
                        buffer[bp + 3] = inBuf[inOffset + i * 2 + 1];
                    }
                }
                else
                {
                    for (long i = 0; i < numpixels; ++i, bp += num_channels)
                    {
                        buffer[bp] = buffer[bp + 1] = buffer[bp + 2] = inBuf[inOffset + i * 4];
                        buffer[bp + 3] = inBuf[inOffset + i * 4 + 2];
                    }
                }
            }
            else if (mode.colortype == LodePNGColorType.LCT_RGBA)
            {
                if (mode.bitdepth == 8)
                {
                    Array.Copy(inBuf, inOffset, buffer, bufOffset, (int)(numpixels * 4));
                }
                else
                {
                    for (long i = 0; i < numpixels; ++i, bp += num_channels)
                    {
                        buffer[bp] = inBuf[inOffset + i * 8];
                        buffer[bp + 1] = inBuf[inOffset + i * 8 + 2];
                        buffer[bp + 2] = inBuf[inOffset + i * 8 + 4];
                        buffer[bp + 3] = inBuf[inOffset + i * 8 + 6];
                    }
                }
            }
        }

        /* Similar to getPixelColorsRGBA8, but with 3-channel RGB output. */
        private static void getPixelColorsRGB8(byte[] buffer, int bufOffset, long numpixels,
            byte[] inBuf, int inOffset, LodePNGColorMode mode)
        {
            int num_channels = 3;
            int bp = bufOffset;
            if (mode.colortype == LodePNGColorType.LCT_GREY)
            {
                if (mode.bitdepth == 8)
                {
                    for (long i = 0; i < numpixels; ++i, bp += num_channels)
                        buffer[bp] = buffer[bp + 1] = buffer[bp + 2] = inBuf[inOffset + i];
                }
                else if (mode.bitdepth == 16)
                {
                    for (long i = 0; i < numpixels; ++i, bp += num_channels)
                        buffer[bp] = buffer[bp + 1] = buffer[bp + 2] = inBuf[inOffset + i * 2];
                }
                else
                {
                    uint highest = (1u << (int)mode.bitdepth) - 1u;
                    long j = 0;
                    for (long i = 0; i < numpixels; ++i, bp += num_channels)
                    {
                        uint value = readBitsFromReversedStream(ref j, inBuf, mode.bitdepth);
                        buffer[bp] = buffer[bp + 1] = buffer[bp + 2] = (byte)((value * 255) / highest);
                    }
                }
            }
            else if (mode.colortype == LodePNGColorType.LCT_RGB)
            {
                if (mode.bitdepth == 8)
                    Array.Copy(inBuf, inOffset, buffer, bufOffset, (int)(numpixels * 3));
                else
                {
                    for (long i = 0; i < numpixels; ++i, bp += num_channels)
                    {
                        buffer[bp] = inBuf[inOffset + i * 6];
                        buffer[bp + 1] = inBuf[inOffset + i * 6 + 2];
                        buffer[bp + 2] = inBuf[inOffset + i * 6 + 4];
                    }
                }
            }
            else if (mode.colortype == LodePNGColorType.LCT_PALETTE)
            {
                if (mode.bitdepth == 8)
                {
                    for (long i = 0; i < numpixels; ++i, bp += num_channels)
                    {
                        uint index = inBuf[inOffset + i];
                        Array.Copy(mode.palette!, (int)(index * 4), buffer, bp, 3);
                    }
                }
                else
                {
                    long j = 0;
                    for (long i = 0; i < numpixels; ++i, bp += num_channels)
                    {
                        uint index = readBitsFromReversedStream(ref j, inBuf, mode.bitdepth);
                        Array.Copy(mode.palette!, (int)(index * 4), buffer, bp, 3);
                    }
                }
            }
            else if (mode.colortype == LodePNGColorType.LCT_GREY_ALPHA)
            {
                if (mode.bitdepth == 8)
                {
                    for (long i = 0; i < numpixels; ++i, bp += num_channels)
                        buffer[bp] = buffer[bp + 1] = buffer[bp + 2] = inBuf[inOffset + i * 2];
                }
                else
                {
                    for (long i = 0; i < numpixels; ++i, bp += num_channels)
                        buffer[bp] = buffer[bp + 1] = buffer[bp + 2] = inBuf[inOffset + i * 4];
                }
            }
            else if (mode.colortype == LodePNGColorType.LCT_RGBA)
            {
                if (mode.bitdepth == 8)
                {
                    for (long i = 0; i < numpixels; ++i, bp += num_channels)
                        Array.Copy(inBuf, (int)(inOffset + i * 4), buffer, bp, 3);
                }
                else
                {
                    for (long i = 0; i < numpixels; ++i, bp += num_channels)
                    {
                        buffer[bp] = inBuf[inOffset + i * 8];
                        buffer[bp + 1] = inBuf[inOffset + i * 8 + 2];
                        buffer[bp + 2] = inBuf[inOffset + i * 8 + 4];
                    }
                }
            }
        }

        /* Get RGBA16 color of pixel with index i from the raw image with given color type (must be 16-bit). */
        private static void getPixelColorRGBA16(out ushort r, out ushort g, out ushort b, out ushort a,
            byte[] inBuf, int inOffset, long i, LodePNGColorMode mode)
        {
            r = g = b = a = 0;
            if (mode.colortype == LodePNGColorType.LCT_GREY)
            {
                r = g = b = (ushort)(256 * inBuf[inOffset + i * 2] + inBuf[inOffset + i * 2 + 1]);
                if (mode.key_defined != 0 && 256u * inBuf[inOffset + i * 2] + inBuf[inOffset + i * 2 + 1] == mode.key_r) a = 0;
                else a = 65535;
            }
            else if (mode.colortype == LodePNGColorType.LCT_RGB)
            {
                r = (ushort)(256u * inBuf[inOffset + i * 6] + inBuf[inOffset + i * 6 + 1]);
                g = (ushort)(256u * inBuf[inOffset + i * 6 + 2] + inBuf[inOffset + i * 6 + 3]);
                b = (ushort)(256u * inBuf[inOffset + i * 6 + 4] + inBuf[inOffset + i * 6 + 5]);
                if (mode.key_defined != 0
                    && 256u * inBuf[inOffset + i * 6] + inBuf[inOffset + i * 6 + 1] == mode.key_r
                    && 256u * inBuf[inOffset + i * 6 + 2] + inBuf[inOffset + i * 6 + 3] == mode.key_g
                    && 256u * inBuf[inOffset + i * 6 + 4] + inBuf[inOffset + i * 6 + 5] == mode.key_b) a = 0;
                else a = 65535;
            }
            else if (mode.colortype == LodePNGColorType.LCT_GREY_ALPHA)
            {
                r = g = b = (ushort)(256u * inBuf[inOffset + i * 4] + inBuf[inOffset + i * 4 + 1]);
                a = (ushort)(256u * inBuf[inOffset + i * 4 + 2] + inBuf[inOffset + i * 4 + 3]);
            }
            else if (mode.colortype == LodePNGColorType.LCT_RGBA)
            {
                r = (ushort)(256u * inBuf[inOffset + i * 8] + inBuf[inOffset + i * 8 + 1]);
                g = (ushort)(256u * inBuf[inOffset + i * 8 + 2] + inBuf[inOffset + i * 8 + 3]);
                b = (ushort)(256u * inBuf[inOffset + i * 8 + 4] + inBuf[inOffset + i * 8 + 5]);
                a = (ushort)(256u * inBuf[inOffset + i * 8 + 6] + inBuf[inOffset + i * 8 + 7]);
            }
        }

        private static uint lodepng_convert(byte[] outBuf, byte[] inBuf, LodePNGColorMode mode_out, LodePNGColorMode mode_in, uint w, uint h)
        {
            long numpixels = (long)w * (long)h;
            uint error = 0;

            if (mode_in.colortype == LodePNGColorType.LCT_PALETTE && mode_in.palette == null)
                return 107;

            if (lodepng_color_mode_equal(mode_out, mode_in))
            {
                long numbytes = lodepng_get_raw_size(w, h, mode_in);
                Array.Copy(inBuf, 0, outBuf, 0, (int)numbytes);
                return 0;
            }

            ColorTree? tree = null;
            if (mode_out.colortype == LodePNGColorType.LCT_PALETTE)
            {
                int palettesize = mode_out.palettesize;
                byte[]? palette = mode_out.palette;
                long palsize = 1L << (int)mode_out.bitdepth;

                if (palettesize == 0)
                {
                    palettesize = mode_in.palettesize;
                    palette = mode_in.palette;
                    if (mode_in.colortype == LodePNGColorType.LCT_PALETTE && mode_in.bitdepth == mode_out.bitdepth)
                    {
                        long numbytes = lodepng_get_raw_size(w, h, mode_in);
                        Array.Copy(inBuf, 0, outBuf, 0, (int)numbytes);
                        return 0;
                    }
                }
                if (palettesize < palsize) palsize = palettesize;
                tree = new ColorTree();
                for (long i = 0; i < palsize; ++i)
                {
                    var p = palette!;
                    error = color_tree_add(tree, p[i * 4], p[i * 4 + 1], p[i * 4 + 2], p[i * 4 + 3], (uint)i);
                    if (error != 0) break;
                }
            }

            if (error == 0)
            {
                if (mode_in.bitdepth == 16 && mode_out.bitdepth == 16)
                {
                    for (long i = 0; i < numpixels; ++i)
                    {
                        getPixelColorRGBA16(out ushort r, out ushort g, out ushort b, out ushort a, inBuf, 0, i, mode_in);
                        rgba16ToPixel(outBuf, 0, i, mode_out, r, g, b, a);
                    }
                }
                else if (mode_out.bitdepth == 8 && mode_out.colortype == LodePNGColorType.LCT_RGBA)
                {
                    getPixelColorsRGBA8(outBuf, 0, numpixels, inBuf, 0, mode_in);
                }
                else if (mode_out.bitdepth == 8 && mode_out.colortype == LodePNGColorType.LCT_RGB)
                {
                    getPixelColorsRGB8(outBuf, 0, numpixels, inBuf, 0, mode_in);
                }
                else
                {
                    for (long i = 0; i < numpixels; ++i)
                    {
                        getPixelColorRGBA8(out byte r, out byte g, out byte b, out byte a, inBuf, 0, i, mode_in);
                        error = rgba8ToPixel(outBuf, 0, i, mode_out, tree, r, g, b, a);
                        if (error != 0) break;
                    }
                }
            }

            return error;
        }

        #endregion

        #region PNG Decoder (filters, Adam7, post-processing)

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte paethPredictor(short a, short b, short c)
        {
            short pa = (short)LODEPNG_ABS(b - c);
            short pb = (short)LODEPNG_ABS(a - c);
            short pc = (short)LODEPNG_ABS(a + b - c - c);
            if (pb < pa) { a = b; pa = pb; }
            return (byte)((pc < pa) ? c : a);
        }

        private static readonly uint[] ADAM7_IX = { 0, 4, 0, 2, 0, 1, 0 };
        private static readonly uint[] ADAM7_IY = { 0, 0, 4, 0, 2, 0, 1 };
        private static readonly uint[] ADAM7_DX = { 8, 8, 4, 4, 2, 2, 1 };
        private static readonly uint[] ADAM7_DY = { 8, 8, 8, 4, 4, 2, 2 };

        private static void Adam7_getpassvalues(uint[] passw, uint[] passh,
            long[] filter_passstart, long[] padded_passstart, long[] passstart, uint w, uint h, uint bpp)
        {
            for (int i = 0; i != 7; ++i)
            {
                passw[i] = (w + ADAM7_DX[i] - ADAM7_IX[i] - 1) / ADAM7_DX[i];
                passh[i] = (h + ADAM7_DY[i] - ADAM7_IY[i] - 1) / ADAM7_DY[i];
                if (passw[i] == 0) passh[i] = 0;
                if (passh[i] == 0) passw[i] = 0;
            }

            filter_passstart[0] = padded_passstart[0] = passstart[0] = 0;
            for (int i = 0; i != 7; ++i)
            {
                filter_passstart[i + 1] = filter_passstart[i]
                    + ((passw[i] != 0 && passh[i] != 0) ? passh[i] * (1 + (passw[i] * bpp + 7) / 8) : 0);
                padded_passstart[i + 1] = padded_passstart[i] + passh[i] * ((passw[i] * bpp + 7) / 8);
                passstart[i + 1] = passstart[i] + (passh[i] * passw[i] * bpp + 7) / 8;
            }
        }

        private static unsafe uint unfilterScanline(byte[] recon, int reconOffset,
            byte[] scanline, int scanOffset,
            byte[]? precon, int preconOffset,
            int bytewidth, byte filterType, int length)
        {
            fixed (byte* reconBase = &recon[reconOffset])
            fixed (byte* scanBase = &scanline[scanOffset])
            {
                if (precon != null)
                {
                    fixed (byte* pb = &precon[preconOffset])
                    {
                        return unfilterScanlineCore(reconBase, scanBase, pb, bytewidth, filterType, length);
                    }
                }
                return unfilterScanlineCore(reconBase, scanBase, null, bytewidth, filterType, length);
            }
        }

        private static unsafe uint unfilterScanlineCore(byte* r, byte* s, byte* p, int bytewidth, byte filterType, int length)
        {
            int i;
            switch (filterType)
            {
                case 0:
                    Buffer.MemoryCopy(s, r, length, length);
                    break;
                case 1:
                    for (i = 0; i < bytewidth; ++i) r[i] = s[i];
                    for (i = bytewidth; i < length; ++i)
                        r[i] = (byte)(s[i] + r[i - bytewidth]);
                    break;
                case 2:
                    if (p != null)
                    {
                        for (i = 0; i < length; ++i) r[i] = (byte)(s[i] + p[i]);
                    }
                    else
                    {
                        Buffer.MemoryCopy(s, r, length, length);
                    }
                    break;
                case 3:
                    if (p != null)
                    {
                        for (i = 0; i < bytewidth; ++i) r[i] = (byte)(s[i] + (p[i] >> 1));
                        for (i = bytewidth; i < length; ++i)
                            r[i] = (byte)(s[i] + ((r[i - bytewidth] + p[i]) >> 1));
                    }
                    else
                    {
                        for (i = 0; i < bytewidth; ++i) r[i] = s[i];
                        for (i = bytewidth; i < length; ++i)
                            r[i] = (byte)(s[i] + (r[i - bytewidth] >> 1));
                    }
                    break;
                case 4:
                    if (p != null)
                    {
                        for (i = 0; i < bytewidth; ++i)
                            r[i] = (byte)(s[i] + p[i]);

                        if (bytewidth >= 4)
                        {
                            for (; i + 3 < length; i += 4)
                            {
                                int j = i - bytewidth;
                                byte s0 = s[i], s1 = s[i + 1], s2 = s[i + 2], s3 = s[i + 3];
                                byte r0 = r[j], r1 = r[j + 1], r2 = r[j + 2], r3 = r[j + 3];
                                byte p0 = p[i], p1 = p[i + 1], p2 = p[i + 2], p3 = p[i + 3];
                                byte q0 = p[j], q1 = p[j + 1], q2 = p[j + 2], q3 = p[j + 3];
                                r[i] = (byte)(s0 + paethPredictor(r0, p0, q0));
                                r[i + 1] = (byte)(s1 + paethPredictor(r1, p1, q1));
                                r[i + 2] = (byte)(s2 + paethPredictor(r2, p2, q2));
                                r[i + 3] = (byte)(s3 + paethPredictor(r3, p3, q3));
                            }
                        }
                        else if (bytewidth >= 3)
                        {
                            for (; i + 2 < length; i += 3)
                            {
                                int j = i - bytewidth;
                                byte s0 = s[i], s1 = s[i + 1], s2 = s[i + 2];
                                byte r0 = r[j], r1 = r[j + 1], r2 = r[j + 2];
                                byte p0 = p[i], p1 = p[i + 1], p2 = p[i + 2];
                                byte q0 = p[j], q1 = p[j + 1], q2 = p[j + 2];
                                r[i] = (byte)(s0 + paethPredictor(r0, p0, q0));
                                r[i + 1] = (byte)(s1 + paethPredictor(r1, p1, q1));
                                r[i + 2] = (byte)(s2 + paethPredictor(r2, p2, q2));
                            }
                        }
                        else if (bytewidth >= 2)
                        {
                            for (; i + 1 < length; i += 2)
                            {
                                int j = i - bytewidth;
                                byte s0 = s[i], s1 = s[i + 1];
                                byte r0 = r[j], r1 = r[j + 1];
                                byte p0 = p[i], p1 = p[i + 1];
                                byte q0 = p[j], q1 = p[j + 1];
                                r[i] = (byte)(s0 + paethPredictor(r0, p0, q0));
                                r[i + 1] = (byte)(s1 + paethPredictor(r1, p1, q1));
                            }
                        }

                        for (; i < length; ++i)
                            r[i] = (byte)(s[i] + paethPredictor(r[i - bytewidth], p[i], p[i - bytewidth]));
                    }
                    else
                    {
                        for (i = 0; i < bytewidth; ++i) r[i] = s[i];
                        for (i = bytewidth; i < length; ++i)
                            r[i] = (byte)(s[i] + r[i - bytewidth]);
                    }
                    break;
                default: return 36;
            }
            return 0;
        }

        private static uint unfilter(byte[] outBuf, int outOffset, byte[] inBuf, int inOffset, uint w, uint h, uint bpp)
        {
            int bytewidth = (int)((bpp + 7) / 8);
            long linebytes = lodepng_get_raw_size_idat(w, 1, bpp) - 1;

            for (uint y = 0; y < h; ++y)
            {
                long outindex = (long)linebytes * y;
                long inindex = (long)(1 + linebytes) * y;
                byte filterType = inBuf[inOffset + inindex];

                int prevOffset = y == 0 ? -1 : outOffset + (int)(outindex - linebytes);
                byte[]? prevLine = y == 0 ? null : outBuf;

                uint err = unfilterScanline(
                    outBuf, outOffset + (int)outindex,
                    inBuf, inOffset + (int)inindex + 1,
                    prevLine, prevOffset,
                    bytewidth, filterType, (int)linebytes);
                if (err != 0) return err;
            }

            return 0;
        }

        private static void Adam7_deinterlace(byte[] outBuf, int outOffset, byte[] inBuf, int inOffset, uint w, uint h, uint bpp)
        {
            uint[] passw = new uint[7], passh = new uint[7];
            long[] filter_passstart = new long[8], padded_passstart = new long[8], passstart = new long[8];

            Adam7_getpassvalues(passw, passh, filter_passstart, padded_passstart, passstart, w, h, bpp);

            if (bpp >= 8)
            {
                for (int pi = 0; pi != 7; ++pi)
                {
                    uint bytewidth = bpp / 8u;
                    for (uint y = 0; y < passh[pi]; ++y)
                    for (uint x = 0; x < passw[pi]; ++x)
                    {
                        long pixelinstart = passstart[pi] + (y * passw[pi] + x) * bytewidth;
                        long pixeloutstart = ((ADAM7_IY[pi] + (long)y * ADAM7_DY[pi]) * w + ADAM7_IX[pi] + (long)x * ADAM7_DX[pi]) * bytewidth;
                        for (uint b2 = 0; b2 < bytewidth; ++b2)
                            outBuf[outOffset + pixeloutstart + b2] = inBuf[inOffset + pixelinstart + b2];
                    }
                }
            }
            else
            {
                for (int pi = 0; pi != 7; ++pi)
                {
                    uint ilinebits = bpp * passw[pi];
                    uint olinebits = bpp * w;
                    for (uint y = 0; y < passh[pi]; ++y)
                    for (uint x = 0; x < passw[pi]; ++x)
                    {
                        long ibp = (8 * passstart[pi]) + (y * ilinebits + x * bpp);
                        long obp = (ADAM7_IY[pi] + (long)y * ADAM7_DY[pi]) * olinebits + (ADAM7_IX[pi] + (long)x * ADAM7_DX[pi]) * bpp;
                        for (uint b2 = 0; b2 < bpp; ++b2)
                        {
                            byte bit = readBitFromReversedStream(ref ibp, inBuf);
                            setBitOfReversedStream(ref obp, outBuf, bit);
                        }
                    }
                }
            }
        }

        private static void removePaddingBits(byte[] outBuf, int outOffset, byte[] inBuf, int inOffset,
            long olinebits, long ilinebits, uint h)
        {
            long diff = ilinebits - olinebits;
            long ibp = 0, obp = 0;
            for (uint y = 0; y < h; ++y)
            {
                for (long x = 0; x < olinebits; ++x)
                {
                    long ibpLocal = ibp;
                    byte bit = readBitFromReversedStream(ref ibpLocal, inBuf);
                    ibp = ibpLocal;
                    long obpLocal = obp;
                    setBitOfReversedStream(ref obpLocal, outBuf, bit);
                    obp = obpLocal;
                }
                ibp += diff;
            }
        }

        private static uint postProcessScanlines(byte[] outBuf, byte[] inBuf, uint w, uint h, LodePNGInfo info_png)
        {
            uint bpp = lodepng_get_bpp_lct(info_png.color.colortype, info_png.color.bitdepth);
            if (bpp == 0) return 31;

            if (info_png.interlace_method == 0)
            {
                if (bpp < 8 && w * bpp != ((w * bpp + 7u) / 8u) * 8u)
                {
                    uint err = unfilter(inBuf, 0, inBuf, 0, w, h, bpp);
                    if (err != 0) return err;
                    removePaddingBits(outBuf, 0, inBuf, 0, w * bpp, ((w * bpp + 7u) / 8u) * 8u, h);
                }
                else
                {
                    uint err = unfilter(outBuf, 0, inBuf, 0, w, h, bpp);
                    if (err != 0) return err;
                }
            }
            else
            {
                uint[] passw = new uint[7], passh = new uint[7];
                long[] filter_passstart = new long[8], padded_passstart = new long[8], passstart = new long[8];

                Adam7_getpassvalues(passw, passh, filter_passstart, padded_passstart, passstart, w, h, bpp);

                for (int i = 0; i != 7; ++i)
                {
                    uint err = unfilter(inBuf, (int)padded_passstart[i], inBuf, (int)filter_passstart[i], passw[i], passh[i], bpp);
                    if (err != 0) return err;
                    if (bpp < 8)
                    {
                        removePaddingBits(inBuf, (int)passstart[i], inBuf, (int)padded_passstart[i],
                            passw[i] * bpp, ((passw[i] * bpp + 7u) / 8u) * 8u, passh[i]);
                    }
                }
                Adam7_deinterlace(outBuf, 0, inBuf, 0, w, h, bpp);
            }
            return 0;
        }

        #endregion

        #region PNG Chunk Readers

        private static uint readChunk_PLTE(LodePNGColorMode color, byte[] data, int dataOffset, int chunkLength)
        {
            int pos = 0;
            color.palettesize = chunkLength / 3;
            if (color.palettesize == 0 || color.palettesize > 256) return 38;
            lodepng_color_mode_alloc_palette(color);
            if (color.palette == null && color.palettesize > 0)
            {
                color.palettesize = 0;
                return 83;
            }

            for (int i = 0; i < color.palettesize; ++i)
            {
                color.palette![4 * i] = data[dataOffset + pos++];
                color.palette[4 * i + 1] = data[dataOffset + pos++];
                color.palette[4 * i + 2] = data[dataOffset + pos++];
                color.palette[4 * i + 3] = 255;
            }

            return 0;
        }

        private static uint readChunk_tRNS(LodePNGColorMode color, byte[] data, int dataOffset, int chunkLength)
        {
            if (color.colortype == LodePNGColorType.LCT_PALETTE)
            {
                if (chunkLength > color.palettesize) return 39;
                for (int i = 0; i < chunkLength; ++i) color.palette![4 * i + 3] = data[dataOffset + i];
            }
            else if (color.colortype == LodePNGColorType.LCT_GREY)
            {
                if (chunkLength != 2) return 30;
                color.key_defined = 1;
                color.key_r = color.key_g = color.key_b = 256u * data[dataOffset] + data[dataOffset + 1];
            }
            else if (color.colortype == LodePNGColorType.LCT_RGB)
            {
                if (chunkLength != 6) return 41;
                color.key_defined = 1;
                color.key_r = 256u * data[dataOffset] + data[dataOffset + 1];
                color.key_g = 256u * data[dataOffset + 2] + data[dataOffset + 3];
                color.key_b = 256u * data[dataOffset + 4] + data[dataOffset + 5];
            }
            else return 42;

            return 0;
        }

        #endregion

        #region Decode Generic

        /* read a PNG, the result will be in the same color type as the PNG */
        private static void decodeGeneric(out byte[]? outBuf, out uint w, out uint h, LodePNGState state, byte[] inData, int insize)
        {
            outBuf = null;
            w = h = 0;
            byte IEND = 0;
            byte[] idat;
            int idatsize = 0;
            byte[]? scanlines = null;
            int scanlines_size = 0;
            int expected_size = 0;
            long outsize = 0;

            state.error = lodepng_inspect_internal(out w, out h, state, inData, insize);
            if (state.error != 0) return;

            if (lodepng_pixel_overflow(w, h, state.info_png.color, state.info_raw))
            {
                state.error = 92;
                return;
            }

            idat = new byte[insize];

            int chunk = 33; /* first byte of the first chunk after the header */

            while (IEND == 0 && state.error == 0)
            {
                if (chunk + 12 > insize || chunk < 0)
                {
                    if (state.decoder.ignore_end != 0) break;
                    state.error = 30;
                    break;
                }

                uint chunkLength = lodepng_chunk_length(inData, chunk);
                if (chunkLength > 2147483647)
                {
                    if (state.decoder.ignore_end != 0) break;
                    state.error = 63;
                    break;
                }

                if (chunk + (int)chunkLength + 12 > insize)
                {
                    state.error = 64;
                    break;
                }

                int dataOff = chunk + 8; /* data starts at offset 8 within chunk */

                if (lodepng_chunk_type_equals(inData, chunk, "IDAT"))
                {
                    long newsize;
                    if (lodepng_addofl(idatsize, (int)chunkLength, out newsize)) { state.error = 95; break; }
                    if (newsize > insize) { state.error = 95; break; }
                    Array.Copy(inData, dataOff, idat, idatsize, (int)chunkLength);
                    idatsize += (int)chunkLength;
                }
                else if (lodepng_chunk_type_equals(inData, chunk, "IEND"))
                {
                    IEND = 1;
                }
                else if (lodepng_chunk_type_equals(inData, chunk, "PLTE"))
                {
                    state.error = readChunk_PLTE(state.info_png.color, inData, dataOff, (int)chunkLength);
                    if (state.error != 0) break;
                }
                else if (lodepng_chunk_type_equals(inData, chunk, "tRNS"))
                {
                    state.error = readChunk_tRNS(state.info_png.color, inData, dataOff, (int)chunkLength);
                    if (state.error != 0) break;
                }
                else
                {
                    if (state.decoder.ignore_critical == 0 && !lodepng_chunk_ancillary(inData, chunk))
                    {
                        state.error = 69;
                        break;
                    }
                }

                if (IEND == 0) chunk = lodepng_chunk_next_const(inData, chunk, insize);
            }

            if (state.info_png.color.colortype == LodePNGColorType.LCT_PALETTE && state.info_png.color.palette == null)
            {
                state.error = 106;
            }

            if (state.error == 0)
            {
                uint bpp = lodepng_get_bpp_lct(state.info_png.color.colortype, state.info_png.color.bitdepth);
                if (state.info_png.interlace_method == 0)
                {
                    expected_size = (int)lodepng_get_raw_size_idat(w, h, bpp);
                }
                else
                {
                    expected_size = 0;
                    expected_size += (int)lodepng_get_raw_size_idat((w + 7) >> 3, (h + 7) >> 3, bpp);
                    if (w > 4) expected_size += (int)lodepng_get_raw_size_idat((w + 3) >> 3, (h + 7) >> 3, bpp);
                    expected_size += (int)lodepng_get_raw_size_idat((w + 3) >> 2, (h + 3) >> 3, bpp);
                    if (w > 2) expected_size += (int)lodepng_get_raw_size_idat((w + 1) >> 2, (h + 3) >> 2, bpp);
                    expected_size += (int)lodepng_get_raw_size_idat((w + 1) >> 1, (h + 1) >> 2, bpp);
                    if (w > 1) expected_size += (int)lodepng_get_raw_size_idat((w + 0) >> 1, (h + 1) >> 1, bpp);
                    expected_size += (int)lodepng_get_raw_size_idat(w, (h + 0) >> 1, bpp);
                }
                state.error = zlib_decompress(ref scanlines, ref scanlines_size, expected_size,
                    idat, 0, idatsize, state.decoder.zlibsettings);
            }

            if (state.error == 0 && scanlines_size != expected_size) state.error = 91;

            if (state.error == 0)
            {
                outsize = lodepng_get_raw_size(w, h, state.info_png.color);
                outBuf = new byte[outsize];
            }
            if (state.error == 0)
            {
                state.error = postProcessScanlines(outBuf!, scanlines!, w, h, state.info_png);
            }
        }

        #endregion

        #region State Init/Cleanup

        private static void lodepng_decoder_settings_init(LodePNGDecoderSettings settings)
        {
            settings.color_convert = 1;
            settings.ignore_crc = 0;
            settings.ignore_critical = 0;
            settings.ignore_end = 0;
            lodepng_decompress_settings_init(settings.zlibsettings);
        }

        private static void lodepng_state_init(LodePNGState state)
        {
            lodepng_decoder_settings_init(state.decoder);
            lodepng_color_mode_init(state.info_raw);
            lodepng_info_init(state.info_png);
            state.error = 1;
        }

        #endregion

        #region External API (Inspect and Decode)

        /* Read the information from the header and store it in the LodePNGInfo. return value is error */
        private static uint lodepng_inspect_internal(out uint w, out uint h, LodePNGState state, byte[] inData, int insize)
        {
            w = h = 0;
            LodePNGInfo info = state.info_png;
            if (insize == 0 || inData == null)
            {
                state.error = 48;
                return state.error;
            }
            if (insize < 33)
            {
                state.error = 27;
                return state.error;
            }

            /* reset info */
            lodepng_info_cleanup(info);
            lodepng_info_init(info);

            if (inData[0] != 137 || inData[1] != 80 || inData[2] != 78 || inData[3] != 71 ||
                inData[4] != 13 || inData[5] != 10 || inData[6] != 26 || inData[7] != 10)
            {
                state.error = 28;
                return state.error;
            }
            if (lodepng_chunk_length(inData, 8) != 13)
            {
                state.error = 94;
                return state.error;
            }
            if (!lodepng_chunk_type_equals(inData, 8, "IHDR"))
            {
                state.error = 29;
                return state.error;
            }

            uint width = lodepng_read32bitInt(inData, 16);
            uint height = lodepng_read32bitInt(inData, 20);
            w = width;
            h = height;
            info.color.bitdepth = inData[24];
            info.color.colortype = (LodePNGColorType)inData[25];
            info.compression_method = inData[26];
            info.filter_method = inData[27];
            info.interlace_method = inData[28];

            if (width == 0 || height == 0)
            {
                state.error = 93;
                return state.error;
            }
            state.error = checkColorValidity(info.color.colortype, info.color.bitdepth);
            if (state.error != 0) return state.error;
            if (info.compression_method != 0)
            {
                state.error = 32;
                return state.error;
            }
            if (info.filter_method != 0)
            {
                state.error = 33;
                return state.error;
            }
            if (info.interlace_method > 1)
            {
                state.error = 34;
                return state.error;
            }

            return state.error;
        }

        private static uint lodepng_decode_internal(out byte[]? outBuf, out uint w, out uint h, LodePNGState state, byte[] inData, int insize)
        {
            outBuf = null;
            w = h = 0;
            decodeGeneric(out outBuf, out w, out h, state, inData, insize);
            if (state.error != 0) return state.error;
            if (state.decoder.color_convert == 0 || lodepng_color_mode_equal(state.info_raw, state.info_png.color))
            {
                if (state.decoder.color_convert == 0)
                {
                    state.error = lodepng_color_mode_copy(state.info_raw, state.info_png.color);
                    if (state.error != 0) return state.error;
                }
            }
            else
            {
                byte[] data = outBuf!;

                if (!(state.info_raw.colortype == LodePNGColorType.LCT_RGB || state.info_raw.colortype == LodePNGColorType.LCT_RGBA)
                    && !(state.info_raw.bitdepth == 8))
                {
                    return 56;
                }

                long outsz = lodepng_get_raw_size(w, h, state.info_raw);
                outBuf = new byte[outsz];
                state.error = lodepng_convert(outBuf, data, state.info_raw, state.info_png.color, w, h);
            }
            return state.error;
        }

        /// <summary>
        /// Inspect a PNG buffer to extract width and height without full decoding.
        /// Returns 0 on success, non-zero on error.
        /// </summary>
        public static uint Inspect(byte[] data, int dataSize, out uint width, out uint height)
        {
            width = 0;
            height = 0;

            if (data == null || dataSize < 24) return 1;

            var state = new LodePNGState();
            lodepng_state_init(state);

            uint error = lodepng_inspect_internal(out width, out height, state, data, dataSize);
            return error;
        }

        /// <summary>
        /// Decode a PNG buffer into RGBA8888 pixel data.
        /// Returns 0 on success, non-zero on error.
        /// The output is in RGBA byte order (R at lowest address).
        /// </summary>
        public static uint Decode(byte[] pngData, int dataSize, out uint[]? pixels, out uint width, out uint height)
        {
            pixels = null;
            width = 0;
            height = 0;

            if (pngData == null || dataSize == 0) return 1;

            var state = new LodePNGState();
            lodepng_state_init(state);

            uint error = lodepng_decode_internal(out byte[]? rawBytes, out width, out height, state, pngData, dataSize);
            if (error != 0) return error;
            if (rawBytes == null) return 1;

            /* Convert RGBA byte array to uint[] packed as ABGR8888S (R in lowest byte) */
            long numPixels = (long)width * height;
            pixels = new uint[numPixels];
            for (long i = 0; i < numPixels; i++)
            {
                byte r = rawBytes[i * 4];
                byte g = rawBytes[i * 4 + 1];
                byte b = rawBytes[i * 4 + 2];
                byte a = rawBytes[i * 4 + 3];
                pixels[i] = (uint)(r | (g << 8) | (b << 16) | (a << 24));
            }

            return 0;
        }

        #endregion
    }
}
