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

    /************************************************************************/
    /* GIF Encoder                                                          */
    /************************************************************************/
    public static unsafe class GifEncoder
    {
        private const int TRANSPARENT_IDX = 0;
        private const int TRANSPARENT_THRESHOLD = 127;
        private const int BIT_DEPTH = 8;

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
            if (treeRoot > (1 << BIT_DEPTH) - 1)
            {
                int ind = treeRoot - (1 << BIT_DEPTH);
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

        private static void _swapPixels(byte* image, int pixA, int pixB)
        {
            byte rA = image[pixA * 4];
            byte gA = image[pixA * 4 + 1];
            byte bA = image[pixA * 4 + 2];
            byte aA = image[pixA * 4 + 3];

            byte rB = image[pixB * 4];
            byte gB = image[pixB * 4 + 1];
            byte bB = image[pixB * 4 + 2];
            byte aB = image[pixA * 4 + 3]; // Note: matches C++ bug - reads pixA not pixB

            image[pixA * 4] = rB;
            image[pixA * 4 + 1] = gB;
            image[pixA * 4 + 2] = bB;
            image[pixA * 4 + 3] = aB;

            image[pixB * 4] = rA;
            image[pixB * 4 + 1] = gA;
            image[pixB * 4 + 2] = bA;
            image[pixB * 4 + 3] = aA;
        }

        // just the partition operation from quicksort
        private static int _partition(byte* image, int left, int right, int elt, int pivotIndex)
        {
            int pivotValue = image[(pivotIndex) * 4 + elt];
            _swapPixels(image, pivotIndex, right - 1);
            int storeIndex = left;
            bool split = false;

            for (int ii = left; ii < right - 1; ++ii)
            {
                int arrayVal = image[ii * 4 + elt];
                if (arrayVal < pivotValue)
                {
                    _swapPixels(image, ii, storeIndex);
                    ++storeIndex;
                }
                else if (arrayVal == pivotValue)
                {
                    if (split)
                    {
                        _swapPixels(image, ii, storeIndex);
                        ++storeIndex;
                    }

                    split = !split;
                }
            }

            _swapPixels(image, storeIndex, right - 1);
            return storeIndex;
        }

        // Perform an incomplete sort, finding all elements above and below the desired median
        private static void _partitionByMedian(byte* image, int left, int right, int com, int neededCenter)
        {
            if (left < right - 1)
            {
                int pivotIndex = left + (right - left) / 2;
                pivotIndex = _partition(image, left, right, com, pivotIndex);

                // Only "sort" the section of the array that contains the median
                if (pivotIndex > neededCenter) _partitionByMedian(image, left, pivotIndex, com, neededCenter);
                if (pivotIndex < neededCenter) _partitionByMedian(image, pivotIndex + 1, right, com, neededCenter);
            }
        }

        // Builds a palette by creating a balanced k-d tree of all pixels in the image
        private static void _splitPalette(byte* image, int numPixels, int firstElt, int lastElt, int splitElt, int splitDist, int treeNode, GifPalette* pal)
        {
            if (lastElt <= firstElt || numPixels == 0) return;

            // base case, bottom of the tree
            if (lastElt == firstElt + 1)
            {
                // otherwise, take the average of all colors in this subcube
                ulong r = 0, g = 0, b = 0;
                for (int ii = 0; ii < numPixels; ++ii)
                {
                    r += image[ii * 4 + 0];
                    g += image[ii * 4 + 1];
                    b += image[ii * 4 + 2];
                }

                r += (ulong)numPixels / 2; // round to nearest
                g += (ulong)numPixels / 2;
                b += (ulong)numPixels / 2;

                r /= (ulong)numPixels;
                g /= (ulong)numPixels;
                b /= (ulong)numPixels;

                pal->r[firstElt] = (byte)r;
                pal->g[firstElt] = (byte)g;
                pal->b[firstElt] = (byte)b;
                return;
            }

            // Find the axis with the largest range
            int minR = 255, maxR = 0;
            int minG = 255, maxG = 0;
            int minB = 255, maxB = 0;

            for (int ii = 0; ii < numPixels; ++ii)
            {
                int rv = image[ii * 4 + 0];
                int gv = image[ii * 4 + 1];
                int bv = image[ii * 4 + 2];

                if (rv > maxR) maxR = rv;
                if (rv < minR) minR = rv;

                if (gv > maxG) maxG = gv;
                if (gv < minG) minG = gv;

                if (bv > maxB) maxB = bv;
                if (bv < minB) minB = bv;
            }

            int rRange = maxR - minR;
            int gRange = maxG - minG;
            int bRange = maxB - minB;

            // and split along that axis
            int splitCom = 1;
            if (bRange > gRange) splitCom = 2;
            if (rRange > bRange && rRange > gRange) splitCom = 0;

            int subPixelsA = numPixels * (splitElt - firstElt) / (lastElt - firstElt);
            int subPixelsB = numPixels - subPixelsA;

            _partitionByMedian(image, 0, numPixels, splitCom, subPixelsA);

            pal->treeSplitElt[treeNode] = (byte)splitCom;
            pal->treeSplit[treeNode] = image[subPixelsA * 4 + splitCom];

            _splitPalette(image, subPixelsA, firstElt, splitElt, splitElt - splitDist, splitDist / 2, treeNode * 2, pal);
            _splitPalette(image + subPixelsA * 4, subPixelsB, splitElt, lastElt, splitElt + splitDist, splitDist / 2, treeNode * 2 + 1, pal);
        }

        // Finds all pixels that have changed from the previous image and
        // moves them to the front of the buffer.
        private static int _pickChangedPixels(byte* lastFrame, byte* frame, int numPixels, bool transparent)
        {
            int numChanged = 0;
            byte* writeIter = frame;

            for (int ii = 0; ii < numPixels; ++ii)
            {
                if (frame[3] >= TRANSPARENT_THRESHOLD)
                {
                    if (transparent || (lastFrame[0] != frame[0] || lastFrame[1] != frame[1] || lastFrame[2] != frame[2]))
                    {
                        writeIter[0] = frame[0];
                        writeIter[1] = frame[1];
                        writeIter[2] = frame[2];
                        ++numChanged;
                        writeIter += 4;
                    }
                }

                lastFrame += 4;
                frame += 4;
            }

            return numChanged;
        }

        // Creates a palette by placing all the image pixels in a k-d tree and then averaging the blocks at the bottom.
        // This is known as the "modified median split" technique
        private static void _makePalette(GifWriter writer, byte* lastFrame, byte* nextFrame, uint width, uint height, int bitDepth, bool transparent)
        {
            fixed (GifPalette* pal = &writer.pal)
            fixed (byte* tmpImage = writer.tmpImage)
            {
                nuint imageSize = (nuint)(width * height * 4);
                Buffer.MemoryCopy(nextFrame, tmpImage, imageSize, imageSize);

                int numPixels = (int)(width * height);
                if (lastFrame != null) numPixels = _pickChangedPixels(lastFrame, tmpImage, numPixels, transparent);

                int lastElt = 1 << bitDepth;
                int splitElt = lastElt / 2;
                int splitDist = splitElt / 2;

                _splitPalette(tmpImage, numPixels, 1, lastElt, splitElt, splitDist, 1, pal);

                // add the bottom node for the transparency index
                pal->treeSplit[1 << (bitDepth - 1)] = 0;
                pal->treeSplitElt[1 << (bitDepth - 1)] = 0;
                pal->r[0] = pal->g[0] = pal->b[0] = 0;
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
                writer.f = new BufferedStream(new FileStream(filename, FileMode.Create, FileAccess.Write), 65536);
            }
            catch
            {
                return false;
            }

            writer.firstFrame = true;

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

            byte* oldImage = writer.firstFrame ? null : null;
            if (!writer.firstFrame)
            {
                fixed (byte* oldImgPtr = writer.oldImage)
                {
                    oldImage = oldImgPtr;
                }
            }

            writer.firstFrame = false;

            // We need to pin oldImage for the duration of palette/threshold/write
            if (oldImage != null)
            {
                fixed (byte* oldImgPtr = writer.oldImage)
                {
                    _makePalette(writer, oldImgPtr, image, width, height, 8, transparent);
                    _thresholdImage(writer, oldImgPtr, image, width, height, transparent);
                    _writeLzwImage(writer, width, height, delay, transparent);
                }
            }
            else
            {
                _makePalette(writer, null, image, width, height, 8, transparent);
                _thresholdImage(writer, null, image, width, height, transparent);
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