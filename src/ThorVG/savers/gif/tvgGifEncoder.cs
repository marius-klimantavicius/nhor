// Ported from ThorVG/src/savers/gif/tvgGifEncoder.h and tvgGifEncoder.cpp
//
// gif.h
// by Charlie Tangora
// Public domain.
//
// This file offers a simple, very limited way to create animated GIFs directly in code.
// Only RGBA8 is currently supported as an input format. (The alpha is ignored.)

using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace ThorVG
{
    public struct GifPalette
    {
        // Fixed-size arrays in struct via unsafe fixed buffers
        public unsafe fixed byte r[256];
        public unsafe fixed byte g[256];
        public unsafe fixed byte b[256];

        // k-d tree over RGB space, organized in heap fashion
        // i.e. left child of node i is node i*2, right child is node i*2+1
        // nodes 256-511 are implicitly the leaves, containing a color
        public unsafe fixed byte treeSplitElt[256];
        public unsafe fixed byte treeSplit[256];
    }

    public class GifWriter
    {
        public Stream? f;
        public byte[]? oldImage;
        public byte[]? tmpImage;
        public GifPalette pal;
        public bool firstFrame;
    }

    // Simple structure to write out the LZW-compressed portion of the image one bit at a time
    internal struct GifBitStatus
    {
        public byte bitIndex; // how many bits in the partial byte written so far
        public byte @byte; // current partial byte

        public uint chunkIndex;
        public unsafe fixed byte chunk[256]; // bytes are written in here until we have 256 of them, then written to the file
    }

    // The LZW dictionary is a 256-ary tree constructed as the file is encoded,
    // this is one node
    internal struct GifLzwNode
    {
        public unsafe fixed ushort m_next[256];
    }

    internal unsafe struct GifBoxes
    {
        public fixed long sse[255];
        public fixed int start[255];
        public fixed int len[255];
        public fixed byte axis[255];
        public fixed byte avg[255 * 3];
    }

    /************************************************************************/
    /* GIF Encoder                                                          */
    /************************************************************************/
    public static unsafe class GifEncoder
    {
        private const int TRANSPARENT_IDX = 0;
        private const int TRANSPARENT_THRESHOLD = 127;
        private const int BIT_DEPTH = 8;
        private const int LEAF_NODE = 0xff;
        private const int MAX_PAL_COLORS = (1 << BIT_DEPTH) - 1;

        /************************************************************************/
        /* Internal Class Implementation                                        */
        /************************************************************************/

        // walks the k-d tree to pick the palette entry for a desired color.
        // Takes as in/out parameters the current best color and its error -
        // only changes them if it finds a better color in its subtree.
        // this is the major hotspot in the code at the moment.
        private static void _getClosestPaletteColor(GifPalette* pPal, int r, int g, int b, int* bestInd, int* bestDiff, int treeRoot)
        {
            // base case, reached the bottom of the tree
            if (treeRoot > (1 << BIT_DEPTH) - 1 || pPal->treeSplitElt[treeRoot] == LEAF_NODE)
            {
                int ind = treeRoot > (1 << BIT_DEPTH) - 1 ? treeRoot - (1 << BIT_DEPTH) : pPal->treeSplit[treeRoot];
                if (ind == TRANSPARENT_IDX) return;

                // check whether this color is better than the current winner
                int r_err = r - ((int)pPal->r[ind]);
                int g_err = g - ((int)pPal->g[ind]);
                int b_err = b - ((int)pPal->b[ind]);
                int diff = Math.Abs(r_err) + Math.Abs(g_err) + Math.Abs(b_err);

                if (diff < *bestDiff)
                {
                    *bestInd = ind;
                    *bestDiff = diff;
                }

                return;
            }

            // take the appropriate color (r, g, or b) for this node of the k-d tree
            int* comps = stackalloc int[3];
            comps[0] = r;
            comps[1] = g;
            comps[2] = b;

            int splitComp = comps[pPal->treeSplitElt[treeRoot]];

            int splitPos = pPal->treeSplit[treeRoot];
            if (splitPos > splitComp)
            {
                // check the left subtree
                _getClosestPaletteColor(pPal, r, g, b, bestInd, bestDiff, treeRoot * 2);
                if (*bestDiff > splitPos - splitComp)
                {
                    // cannot prove there's not a better value in the right subtree, check that too
                    _getClosestPaletteColor(pPal, r, g, b, bestInd, bestDiff, treeRoot * 2 + 1);
                }
            }
            else
            {
                _getClosestPaletteColor(pPal, r, g, b, bestInd, bestDiff, treeRoot * 2 + 1);
                if (*bestDiff > splitComp - splitPos)
                {
                    _getClosestPaletteColor(pPal, r, g, b, bestInd, bestDiff, treeRoot * 2);
                }
            }
        }

        private static void _swapPixels(byte* image, int pixA, int pixB, int stride)
        {
            for (int cc = 0; cc < stride; ++cc)
            {
                byte temporary = image[pixA * stride + cc];
                image[pixA * stride + cc] = image[pixB * stride + cc];
                image[pixB * stride + cc] = temporary;
            }
        }

        private static int _partitionByValue(byte* image, int numPixels, int elt, int splitVal)
        {
            int storeIndex = 0;
            for (int ii = 0; ii < numPixels; ++ii)
            {
                if (image[ii * 4 + elt] < splitVal)
                {
                    _swapPixels(image, ii, storeIndex, 4);
                    ++storeIndex;
                }
            }
            return storeIndex;
        }

        private static int _findMedianValue(byte* data, int count, int axis, int stride, int* below)
        {
            uint* hist = stackalloc uint[256];
            new Span<uint>(hist, 256).Clear();
            for (int ii = 0; ii < count; ++ii) ++hist[data[ii * stride + axis]];
            int acc = 0;
            for (int v = 0; v < 256; ++v)
            {
                acc += (int)hist[v];
                if (acc > count / 2)
                {
                    if (below != null) *below = acc - (int)hist[v];
                    return v;
                }
            }
            return 0;
        }

        private static void _computeBoxStats(byte* image, GifBoxes* boxes, int idx)
        {
            var pixels = image + boxes->start[idx] * 4;
            long* sum = stackalloc long[3];
            long* sqsum = stackalloc long[3];
            new Span<long>(sum, 3).Clear(); new Span<long>(sqsum, 3).Clear();
            int len = boxes->len[idx];
            for (int ii = 0; ii < len; ++ii)
            {
                for (int cc = 0; cc < 3; ++cc)
                {
                    long v = pixels[ii * 4 + cc];
                    sum[cc] += v; sqsum[cc] += v * v;
                }
            }
            long worst = -1;
            boxes->sse[idx] = 0; boxes->axis[idx] = 0;
            for (int cc = 0; cc < 3; ++cc)
            {
                long q = sum[cc] / len, r = sum[cc] % len;
                long sqerr = sqsum[cc] - (q * q * len + 2 * q * r + r * r / len);
                boxes->sse[idx] += sqerr;
                if (sqerr > worst) { worst = sqerr; boxes->axis[idx] = (byte)cc; }
                boxes->avg[idx * 3 + cc] = (byte)((sum[cc] + len / 2) / len);
            }
        }

        private static void _splitBox(byte* image, GifBoxes* boxes, int idx, int rhsIdx)
        {
            var pixels = image + boxes->start[idx] * 4;
            int below = -1;
            int splitVal = _findMedianValue(pixels, boxes->len[idx], boxes->axis[idx], 4, &below);
            if (below == 0) ++splitVal;
            int left = _partitionByValue(pixels, boxes->len[idx], boxes->axis[idx], splitVal);
            boxes->start[rhsIdx] = boxes->start[idx] + left;
            boxes->len[rhsIdx] = boxes->len[idx] - left;
            boxes->len[idx] = left;
            _computeBoxStats(image, boxes, idx); _computeBoxStats(image, boxes, rhsIdx);
        }

        private static int _pickChangedPixels(byte* lastFrame, byte* frame, int numPixels, bool transparent)
        {
            int numChanged = 0;
            byte* writeIter = frame;
            for (int ii = 0; ii < numPixels; ++ii)
            {
                if (frame[3] >= TRANSPARENT_THRESHOLD &&
                    (lastFrame == null || transparent || lastFrame[0] != frame[0] || lastFrame[1] != frame[1] || lastFrame[2] != frame[2]))
                {
                    writeIter[0] = frame[0]; writeIter[1] = frame[1]; writeIter[2] = frame[2];
                    ++numChanged; writeIter += 4;
                }
                if (lastFrame != null) lastFrame += 4;
                frame += 4;
            }
            return numChanged;
        }

        private static void _buildSearchTree(GifPalette* pal, byte* colors, int count, int firstElt, int lastElt, int treeRoot)
        {
            if (count == 1)
            {
                pal->r[firstElt] = colors[0]; pal->g[firstElt] = colors[1]; pal->b[firstElt] = colors[2];
                if (treeRoot <= (1 << BIT_DEPTH) - 1)
                {
                    pal->treeSplitElt[treeRoot] = LEAF_NODE;
                    pal->treeSplit[treeRoot] = (byte)firstElt;
                }
                return;
            }
            byte* min = stackalloc byte[3] {255, 255, 255};
            byte* max = stackalloc byte[3] {0, 0, 0};
            for (int ii = 0; ii < count; ++ii)
            {
                for (int cc = 0; cc < 3; ++cc)
                {
                    if (colors[ii * 3 + cc] < min[cc]) min[cc] = colors[ii * 3 + cc];
                    if (colors[ii * 3 + cc] > max[cc]) max[cc] = colors[ii * 3 + cc];
                }
            }
            int axis = 0;
            for (int cc = 1; cc < 3; ++cc) if (max[cc] - min[cc] > max[axis] - min[axis]) axis = cc;
            int splitVal = _findMedianValue(colors, count, axis, 3, null);
            int low = 0, mid = 0, high = count;
            while (mid < high)
            {
                int v = colors[mid * 3 + axis];
                if (v < splitVal) { _swapPixels(colors, mid, low, 3); ++low; ++mid; }
                else if (v > splitVal) { --high; _swapPixels(colors, mid, high, 3); }
                else ++mid;
            }
            int splitElt = (firstElt + lastElt) / 2;
            pal->treeSplitElt[treeRoot] = (byte)axis; pal->treeSplit[treeRoot] = (byte)splitVal;
            _buildSearchTree(pal, colors, count / 2, firstElt, splitElt, treeRoot * 2);
            _buildSearchTree(pal, colors + count / 2 * 3, count - count / 2, splitElt, lastElt, treeRoot * 2 + 1);
        }

        private static void _makePalette(GifWriter writer, byte* lastFrame, byte* nextFrame, uint width, uint height, bool transparent)
        {
            fixed (GifPalette* pal = &writer.pal)
            fixed (byte* tmpImage = writer.tmpImage)
            {
                nuint imageSize = (nuint)width * height * 4;
                Buffer.MemoryCopy(nextFrame, tmpImage, imageSize, imageSize);
                int numPixels = _pickChangedPixels(lastFrame, tmpImage, (int)(width * height), transparent);
                if (numPixels == 0) return;
                GifBoxes boxes = default;
                boxes.start[0] = 0; boxes.len[0] = numPixels;
                _computeBoxStats(tmpImage, &boxes, 0);
                int numBoxes = 1;
                while (numBoxes < MAX_PAL_COLORS)
                {
                    int target = -1; long worst = 0;
                    for (int ii = 0; ii < numBoxes; ++ii)
                    {
                        if (boxes.sse[ii] > worst) { worst = boxes.sse[ii]; target = ii; }
                    }
                    if (target < 0) break;
                    _splitBox(tmpImage, &boxes, target, numBoxes);
                    ++numBoxes;
                }
                _buildSearchTree(pal, boxes.avg, numBoxes, 1, 1 << BIT_DEPTH, 1);
            }
        }

        private static void _palettizePixel(byte* nextFrame, byte* outFrame, GifPalette* pPal)
        {
            int bestDiff = 1000000;
            int bestInd = 1;
            _getClosestPaletteColor(pPal, nextFrame[0], nextFrame[1], nextFrame[2], &bestInd, &bestDiff, 1);

            // Write the resulting color to the output buffer
            outFrame[0] = pPal->r[bestInd];
            outFrame[1] = pPal->g[bestInd];
            outFrame[2] = pPal->b[bestInd];
            outFrame[3] = (byte)bestInd;
        }

        // Picks palette colors for the image using simple threshholding, no dithering
        private static void _thresholdImage(GifWriter writer, byte* lastFrame, byte* nextFrame, uint width, uint height, bool transparent)
        {
            fixed (GifPalette* pal = &writer.pal)
            fixed (byte* oldImagePtr = writer.oldImage)
            {
                byte* outFrame = oldImagePtr;
                uint numPixels = width * height;

                if (transparent)
                {
                    for (uint ii = 0; ii < numPixels; ++ii)
                    {
                        if (nextFrame[3] < TRANSPARENT_THRESHOLD)
                        {
                            outFrame[0] = 0;
                            outFrame[1] = 0;
                            outFrame[2] = 0;
                            outFrame[3] = TRANSPARENT_IDX;
                        }
                        else
                        {
                            _palettizePixel(nextFrame, outFrame, pal);
                        }

                        if (lastFrame != null) lastFrame += 4;
                        outFrame += 4;
                        nextFrame += 4;
                    }
                }
                else
                {
                    for (uint ii = 0; ii < numPixels; ++ii)
                    {
                        // if a previous color is available, and it matches the current color,
                        // set the pixel to transparent
                        if (lastFrame != null && lastFrame[0] == nextFrame[0] && lastFrame[1] == nextFrame[1] && lastFrame[2] == nextFrame[2])
                        {
                            outFrame[0] = lastFrame[0];
                            outFrame[1] = lastFrame[1];
                            outFrame[2] = lastFrame[2];
                            outFrame[3] = TRANSPARENT_IDX;
                        }
                        else
                        {
                            _palettizePixel(nextFrame, outFrame, pal);
                        }

                        if (lastFrame != null) lastFrame += 4;
                        outFrame += 4;
                        nextFrame += 4;
                    }
                }
            }
        }

        // insert a single bit
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void _writeBit(GifBitStatus* stat, uint bit)
        {
            bit = bit & 1;
            bit = bit << stat->bitIndex;
            stat->@byte |= (byte)bit;

            ++stat->bitIndex;
            if (stat->bitIndex > 7)
            {
                // move the newly-finished byte to the chunk buffer
                stat->chunk[stat->chunkIndex++] = stat->@byte;
                // and start a new byte
                stat->bitIndex = 0;
                stat->@byte = 0;
            }
        }

        // write all bytes so far to the file
        private static void _writeChunk(Stream f, GifBitStatus* stat)
        {
            f.WriteByte((byte)stat->chunkIndex);
            var span = new ReadOnlySpan<byte>(stat->chunk, (int)stat->chunkIndex);
            f.Write(span);

            stat->bitIndex = 0;
            stat->@byte = 0;
            stat->chunkIndex = 0;
        }

        private static void _writeCode(Stream f, GifBitStatus* stat, uint code, uint length)
        {
            for (uint ii = 0; ii < length; ++ii)
            {
                _writeBit(stat, code);
                code = code >> 1;
                if (stat->chunkIndex == 255) _writeChunk(f, stat);
            }
        }

        // write a 256-color (8-bit) image palette to the file
        private static void _writePalette(GifPalette* pPal, Stream f)
        {
            f.WriteByte(0); // first color: transparency
            f.WriteByte(0);
            f.WriteByte(0);

            for (int ii = 1; ii < (1 << BIT_DEPTH); ++ii)
            {
                uint r = pPal->r[ii];
                uint g = pPal->g[ii];
                uint b = pPal->b[ii];

                f.WriteByte((byte)r);
                f.WriteByte((byte)g);
                f.WriteByte((byte)b);
            }
        }

        // write the image header, LZW-compress and write out the image
        private static void _writeLzwImage(GifWriter writer, uint width, uint height, uint delay, bool transparent)
        {
            var f = writer.f!;

            fixed (byte* image = writer.oldImage)
            fixed (GifPalette* pal = &writer.pal)
            {
                // graphics control extension
                f.WriteByte(0x21);
                f.WriteByte(0xf9);
                f.WriteByte(0x04);
                f.WriteByte((byte)(transparent ? 0x09 : 0x05)); // clear prev frame or not
                f.WriteByte((byte)(delay & 0xff));
                f.WriteByte((byte)((delay >> 8) & 0xff));
                f.WriteByte((byte)TRANSPARENT_IDX); // transparent color index
                f.WriteByte(0);

                f.WriteByte(0x2c); // image descriptor block

                // corner of image (left, top) in canvas space
                f.WriteByte(0);
                f.WriteByte(0);
                f.WriteByte(0);
                f.WriteByte(0);

                f.WriteByte((byte)(width & 0xff)); // width and height of image
                f.WriteByte((byte)((width >> 8) & 0xff));
                f.WriteByte((byte)(height & 0xff));
                f.WriteByte((byte)((height >> 8) & 0xff));

                f.WriteByte((byte)(0x80 + BIT_DEPTH - 1)); // local color table present, 2 ^ bitDepth entries
                _writePalette(pal, f);

                int minCodeSize = BIT_DEPTH;
                uint clearCode = 1u << BIT_DEPTH;

                f.WriteByte((byte)minCodeSize); // min code size 8 bits

                var codetree = new GifLzwNode[4096];

                int curCode = -1;
                uint codeSize = (uint)minCodeSize + 1;
                uint maxCode = clearCode + 1;

                GifBitStatus stat;
                stat.@byte = 0;
                stat.bitIndex = 0;
                stat.chunkIndex = 0;

                _writeCode(f, &stat, clearCode, codeSize); // start with a fresh LZW dictionary

                fixed (GifLzwNode* codetreePtr = codetree)
                {
                    for (uint yy = 0; yy < height; ++yy)
                    {
                        for (uint xx = 0; xx < width; ++xx)
                        {
                            // top-left origin
                            byte nextValue = image[(yy * width + xx) * 4 + 3];

                            if (curCode < 0)
                            {
                                // first value in a new run
                                curCode = nextValue;
                            }
                            else if (codetreePtr[curCode].m_next[nextValue] != 0)
                            {
                                // current run already in the dictionary
                                curCode = codetreePtr[curCode].m_next[nextValue];
                            }
                            else
                            {
                                // finish the current run, write a code
                                _writeCode(f, &stat, (uint)curCode, codeSize);

                                // insert the new run into the dictionary
                                codetreePtr[curCode].m_next[nextValue] = (ushort)(++maxCode);

                                if (maxCode >= (1u << (int)codeSize))
                                {
                                    // dictionary entry count has broken a size barrier,
                                    // we need more bits for codes
                                    codeSize++;
                                }

                                if (maxCode == 4095)
                                {
                                    // the dictionary is full, clear it out and begin anew
                                    _writeCode(f, &stat, clearCode, codeSize); // clear tree

                                    Array.Clear(codetree);
                                    codeSize = (uint)(minCodeSize + 1);
                                    maxCode = clearCode + 1;
                                }

                                curCode = nextValue;
                            }
                        }
                    }
                }

                // compression footer
                _writeCode(f, &stat, (uint)curCode, codeSize);
                _writeCode(f, &stat, clearCode, codeSize);
                _writeCode(f, &stat, clearCode + 1, (uint)minCodeSize + 1);

                // write out the last partial chunk
                while (stat.bitIndex != 0) _writeBit(&stat, 0);
                if (stat.chunkIndex != 0) _writeChunk(f, &stat);

                f.WriteByte(0); // image block terminator
            }
        }

        /************************************************************************/
        /* External Class Implementation                                        */
        /************************************************************************/

        private static ReadOnlySpan<byte> Header => "GIF89a"u8;
        private static ReadOnlySpan<byte> Netscape => "NETSCAPE2.0"u8;

        public static bool GifBegin(GifWriter writer, string filename, uint width, uint height, uint delay)
        {
            try
            {
                writer.f = new BufferedStream(TvgUtf8.Create(filename), 65536);
            }
            catch
            {
                return false;
            }

            writer.firstFrame = true;
            writer.pal = default;

            // allocate
            writer.oldImage = new byte[width * height * 4];
            writer.tmpImage = new byte[width * height * 4];

            // Write "GIF89a"
            writer.f.Write(Header);

            // screen descriptor
            writer.f.WriteByte((byte)(width & 0xff));
            writer.f.WriteByte((byte)((width >> 8) & 0xff));
            writer.f.WriteByte((byte)(height & 0xff));
            writer.f.WriteByte((byte)((height >> 8) & 0xff));

            writer.f.WriteByte(0xf0); // there is an unsorted global color table of 2 entries
            writer.f.WriteByte(0); // background color
            writer.f.WriteByte(0); // pixels are square (we need to specify this because it's 1989)

            // now the "global" palette (really just a dummy palette)
            // color 0: black
            writer.f.WriteByte(0);
            writer.f.WriteByte(0);
            writer.f.WriteByte(0);
            // color 1: also black
            writer.f.WriteByte(0);
            writer.f.WriteByte(0);
            writer.f.WriteByte(0);

            if (delay != 0)
            {
                // animation header
                writer.f.WriteByte(0x21); // extension
                writer.f.WriteByte(0xff); // application specific
                writer.f.WriteByte(11); // length 11
                writer.f.Write(Netscape); // yes, really
                writer.f.WriteByte(3); // 3 bytes of NETSCAPE2.0 data

                writer.f.WriteByte(1); // JUST BECAUSE
                writer.f.WriteByte(0); // loop infinitely (byte 0)
                writer.f.WriteByte(0); // loop infinitely (byte 1)

                writer.f.WriteByte(0); // block terminator
            }

            return true;
        }

        public static bool GifWriteFrame(GifWriter writer, byte* image, uint width, uint height, uint delay, bool transparent)
        {
            if (writer.f == null) return false;

            fixed (byte* oldImage = writer.oldImage)
            {
                var previous = writer.firstFrame ? null : oldImage;
                writer.firstFrame = false;
                _makePalette(writer, previous, image, width, height, transparent);
                _thresholdImage(writer, previous, image, width, height, transparent);
                _writeLzwImage(writer, width, height, delay, transparent);
            }

            return true;
        }

        public static bool GifEnd(GifWriter writer)
        {
            if (writer.f == null) return false;

            writer.f.WriteByte(0x3b); // end of file
            writer.f.Close();
            writer.f.Dispose();

            writer.oldImage = null;
            writer.tmpImage = null;
            writer.f = null;

            return true;
        }
    }
}
