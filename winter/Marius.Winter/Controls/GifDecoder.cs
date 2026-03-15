using System;
using System.Collections.Generic;
using System.IO;

namespace Marius.Winter;

/// <summary>
/// Minimal GIF87a/GIF89a decoder. Decodes all frames into ARGB8888 pixel buffers.
/// No external dependencies — implements LZW decompression inline.
/// </summary>
public static class GifDecoder
{
    public class GifFrame
    {
        public uint[] Pixels = Array.Empty<uint>(); // ARGB8888, size = Width * Height of the logical screen
        public float DelaySeconds;                   // display duration before next frame
    }

    public class GifResult
    {
        public int Width;
        public int Height;
        public List<GifFrame> Frames = new();
        public int LoopCount; // 0 = infinite
    }

    public static GifResult? Decode(byte[] data)
    {
        if (data == null || data.Length < 13) return null;

        var r = new Reader(data);

        // Header: GIF87a or GIF89a
        byte g = r.Byte(), i = r.Byte(), f = r.Byte();
        byte v1 = r.Byte(), v2 = r.Byte(), v3 = r.Byte();
        if (g != (byte)'G' || i != (byte)'I' || f != (byte)'F') return null;

        // Logical Screen Descriptor
        int width = r.UInt16LE();
        int height = r.UInt16LE();
        byte packed = r.Byte();
        r.Byte(); // background color index
        r.Byte(); // pixel aspect ratio

        bool hasGct = (packed & 0x80) != 0;
        int gctSize = 1 << ((packed & 0x07) + 1);

        // Global Color Table
        uint[]? gct = null;
        if (hasGct) gct = ReadColorTable(ref r, gctSize);

        var result = new GifResult { Width = width, Height = height };

        // Canvas for compositing (persistent across frames for disposal methods)
        var canvas = new uint[width * height];
        var prevCanvas = new uint[width * height]; // for disposal method 3 (restore to previous)

        // Per-frame GCE state
        int disposalMethod = 0;
        float delay = 0;
        int transparentIndex = -1;

        while (r.Pos < data.Length)
        {
            byte block = r.Byte();

            switch (block)
            {
                case 0x3B: // Trailer
                    goto done;

                case 0x21: // Extension
                {
                    byte label = r.Byte();
                    if (label == 0xF9) // Graphics Control Extension
                    {
                        r.Byte(); // block size (always 4)
                        byte gcPacked = r.Byte();
                        disposalMethod = (gcPacked >> 2) & 0x07;
                        bool hasTransparent = (gcPacked & 0x01) != 0;
                        delay = r.UInt16LE() * 0.01f; // hundredths of seconds -> seconds
                        if (delay <= 0) delay = 0.1f;  // many GIFs use 0 to mean ~100ms
                        int ti = r.Byte();
                        transparentIndex = hasTransparent ? ti : -1;
                        r.Byte(); // block terminator
                    }
                    else if (label == 0xFF) // Application Extension (e.g., NETSCAPE looping)
                    {
                        int sz = r.Byte();
                        // Check for NETSCAPE2.0
                        if (sz == 11 && r.Pos + 11 <= data.Length)
                        {
                            var appId = data.AsSpan(r.Pos, 11);
                            r.Skip(11);
                            // Read sub-blocks
                            while (true)
                            {
                                int sbsz = r.Byte();
                                if (sbsz == 0) break;
                                if (sbsz >= 3 && data[r.Pos] == 1)
                                    result.LoopCount = data[r.Pos + 1] | (data[r.Pos + 2] << 8);
                                r.Skip(sbsz);
                            }
                        }
                        else
                        {
                            r.Skip(sz);
                            SkipSubBlocks(ref r);
                        }
                    }
                    else // Unknown extension
                    {
                        SkipSubBlocks(ref r);
                    }
                    break;
                }

                case 0x2C: // Image Descriptor
                {
                    int left = r.UInt16LE();
                    int top = r.UInt16LE();
                    int frameW = r.UInt16LE();
                    int frameH = r.UInt16LE();
                    byte imgPacked = r.Byte();

                    bool hasLct = (imgPacked & 0x80) != 0;
                    bool interlaced = (imgPacked & 0x40) != 0;
                    int lctSize = 1 << ((imgPacked & 0x07) + 1);

                    uint[]? lct = null;
                    if (hasLct) lct = ReadColorTable(ref r, lctSize);

                    uint[] colorTable = lct ?? gct ?? DefaultColorTable();

                    // Save canvas for disposal method 3 BEFORE rendering the frame
                    if (disposalMethod == 3)
                        Array.Copy(canvas, prevCanvas, canvas.Length);

                    // Decode LZW image data
                    var pixels = DecodeLzw(ref r, frameW, frameH);

                    // Deinterlace if needed
                    if (interlaced)
                        pixels = Deinterlace(pixels, frameW, frameH);

                    // Composite onto canvas
                    for (int y = 0; y < frameH; y++)
                    {
                        int canvasY = top + y;
                        if (canvasY < 0 || canvasY >= height) continue;
                        for (int x = 0; x < frameW; x++)
                        {
                            int canvasX = left + x;
                            if (canvasX < 0 || canvasX >= width) continue;

                            int idx = pixels[y * frameW + x];
                            if (idx == transparentIndex) continue;
                            if (idx < 0 || idx >= colorTable.Length) continue;

                            canvas[canvasY * width + canvasX] = colorTable[idx];
                        }
                    }

                    // Emit frame (snapshot the canvas)
                    var frame = new GifFrame
                    {
                        Pixels = (uint[])canvas.Clone(),
                        DelaySeconds = delay,
                    };
                    result.Frames.Add(frame);

                    // Apply disposal method for NEXT frame
                    switch (disposalMethod)
                    {
                        case 2: // Restore to background (clear the frame area)
                            for (int y = 0; y < frameH; y++)
                            {
                                int cy = top + y;
                                if (cy < 0 || cy >= height) continue;
                                for (int x = 0; x < frameW; x++)
                                {
                                    int cx = left + x;
                                    if (cx < 0 || cx >= width) continue;
                                    canvas[cy * width + cx] = 0;
                                }
                            }
                            break;
                        case 3: // Restore to previous
                            Array.Copy(prevCanvas, canvas, canvas.Length);
                            break;
                        // 0, 1: do not dispose (leave canvas as-is)
                    }

                    // Reset GCE state
                    disposalMethod = 0;
                    delay = 0.1f;
                    transparentIndex = -1;
                    break;
                }

                case 0x00:
                    break; // padding byte, skip

                default:
                    // Unknown block — try to skip
                    goto done;
            }
        }

        done:
        return result.Frames.Count > 0 ? result : null;
    }

    public static GifResult? Decode(string path)
    {
        return Decode(File.ReadAllBytes(path));
    }

    // --- LZW Decompression ---

    private static int[] DecodeLzw(ref Reader r, int width, int height)
    {
        int minCodeSize = r.Byte();
        if (minCodeSize < 2 || minCodeSize > 11)
            minCodeSize = Math.Clamp(minCodeSize, 2, 11);

        // Collect sub-block data
        var compressedData = new List<byte>();
        while (true)
        {
            int sz = r.Byte();
            if (sz == 0) break;
            if (r.Pos + sz > r.Data.Length) { r.Pos = r.Data.Length; break; }
            compressedData.AddRange(r.Data.AsSpan(r.Pos, sz).ToArray());
            r.Skip(sz);
        }

        var compressed = compressedData.ToArray();
        int totalPixels = width * height;
        var output = new int[totalPixels];

        int clearCode = 1 << minCodeSize;
        int eoiCode = clearCode + 1;
        int codeSize = minCodeSize + 1;
        int nextCode = eoiCode + 1;
        int codeMask = (1 << codeSize) - 1;
        int maxTableSize = 4096;

        // LZW table: each entry is (prefix, suffix)
        var prefix = new int[maxTableSize];
        var suffix = new byte[maxTableSize];
        var length = new int[maxTableSize]; // length of string at each code

        // Initialize table
        for (int c = 0; c < clearCode; c++)
        {
            prefix[c] = -1;
            suffix[c] = (byte)c;
            length[c] = 1;
        }

        int bitPos = 0;
        int outPos = 0;
        int prevCode = -1;

        int ReadCode()
        {
            if (bitPos + codeSize > compressed.Length * 8) return eoiCode;
            int byteIdx = bitPos >> 3;
            int bitOff = bitPos & 7;
            // Read up to 3 bytes to cover the code
            int raw = compressed[byteIdx];
            if (byteIdx + 1 < compressed.Length) raw |= compressed[byteIdx + 1] << 8;
            if (byteIdx + 2 < compressed.Length) raw |= compressed[byteIdx + 2] << 16;
            bitPos += codeSize;
            return (raw >> bitOff) & codeMask;
        }

        // Temp buffer for outputting a code's string in reverse
        var tempBuf = new byte[maxTableSize];

        void OutputCode(int code)
        {
            int len = length[code];
            // Walk the chain backwards
            int pos = len - 1;
            int c = code;
            while (c >= 0 && pos >= 0)
            {
                tempBuf[pos--] = suffix[c];
                c = prefix[c];
            }
            for (int j = 0; j < len && outPos < totalPixels; j++)
                output[outPos++] = tempBuf[j];
        }

        while (outPos < totalPixels)
        {
            int code = ReadCode();
            if (code == eoiCode) break;

            if (code == clearCode)
            {
                codeSize = minCodeSize + 1;
                codeMask = (1 << codeSize) - 1;
                nextCode = eoiCode + 1;
                prevCode = -1;
                continue;
            }

            if (prevCode == -1)
            {
                // First code after clear
                if (code < nextCode) OutputCode(code);
                prevCode = code;
                continue;
            }

            if (code < nextCode)
            {
                // Code is in table
                OutputCode(code);
                if (nextCode < maxTableSize)
                {
                    // Add new entry: prevCode's string + first char of code's string
                    int c = code;
                    while (prefix[c] >= 0) c = prefix[c];
                    prefix[nextCode] = prevCode;
                    suffix[nextCode] = suffix[c];
                    length[nextCode] = length[prevCode] + 1;
                    nextCode++;
                }
            }
            else
            {
                // Code == nextCode (special case)
                if (nextCode < maxTableSize)
                {
                    int c = prevCode;
                    while (prefix[c] >= 0) c = prefix[c];
                    prefix[nextCode] = prevCode;
                    suffix[nextCode] = suffix[c];
                    length[nextCode] = length[prevCode] + 1;
                    OutputCode(nextCode);
                    nextCode++;
                }
            }

            // Increase code size if needed
            if (nextCode > codeMask && codeSize < 12)
            {
                codeSize++;
                codeMask = (1 << codeSize) - 1;
            }

            prevCode = code;
        }

        return output;
    }

    // --- Helpers ---

    private static uint[] ReadColorTable(ref Reader r, int count)
    {
        var table = new uint[count];
        for (int c = 0; c < count; c++)
        {
            byte red = r.Byte();
            byte green = r.Byte();
            byte blue = r.Byte();
            // Standard GIF stores R,G,B. Output as ARGB8888 (0xAARRGGBB).
            table[c] = 0xFF000000u | ((uint)red << 16) | ((uint)green << 8) | blue;
        }
        return table;
    }

    private static uint[] DefaultColorTable()
    {
        var t = new uint[256];
        for (int c = 0; c < 256; c++)
            t[c] = 0xFF000000u | ((uint)c << 16) | ((uint)c << 8) | (uint)c;
        return t;
    }

    private static void SkipSubBlocks(ref Reader r)
    {
        while (true)
        {
            int sz = r.Byte();
            if (sz == 0) break;
            r.Skip(sz);
        }
    }

    private static int[] Deinterlace(int[] src, int width, int height)
    {
        var dst = new int[src.Length];
        int srcRow = 0;
        // Pass 1: every 8th row starting from 0
        for (int y = 0; y < height; y += 8) CopyRow(src, dst, width, srcRow++, y);
        // Pass 2: every 8th row starting from 4
        for (int y = 4; y < height; y += 8) CopyRow(src, dst, width, srcRow++, y);
        // Pass 3: every 4th row starting from 2
        for (int y = 2; y < height; y += 4) CopyRow(src, dst, width, srcRow++, y);
        // Pass 4: every 2nd row starting from 1
        for (int y = 1; y < height; y += 2) CopyRow(src, dst, width, srcRow++, y);
        return dst;
    }

    private static void CopyRow(int[] src, int[] dst, int width, int srcRow, int dstRow)
    {
        Array.Copy(src, srcRow * width, dst, dstRow * width, width);
    }

    private ref struct Reader
    {
        public readonly byte[] Data;
        public int Pos;

        public Reader(byte[] data) { Data = data; Pos = 0; }

        public byte Byte()
        {
            if (Pos >= Data.Length) return 0;
            return Data[Pos++];
        }

        public int UInt16LE()
        {
            if (Pos + 1 >= Data.Length) return 0;
            int v = Data[Pos] | (Data[Pos + 1] << 8);
            Pos += 2;
            return v;
        }

        public void Skip(int n) => Pos = Math.Min(Pos + n, Data.Length);
    }
}
