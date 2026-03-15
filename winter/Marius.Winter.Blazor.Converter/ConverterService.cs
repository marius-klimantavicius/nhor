// SVG->PNG and Lottie->GIF conversion using ThorVG.
// PngBuilder adapted from tools/svg2png/Program.cs.

using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using ThorVG;
using TvgAnimation = ThorVG.Animation;

namespace Marius.Winter.Blazor.Converter;

public enum FileKind { Unknown, Svg, Lottie }

public static class ConverterService
{
    private static bool _initialized;

    private static void EnsureInit()
    {
        if (_initialized) return;
        uint threads = (uint)Environment.ProcessorCount;
        if (threads > 1) threads--;
        Initializer.Init(threads);
        _initialized = true;
    }

    public static FileKind Detect(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".svg" => FileKind.Svg,
            ".json" => FileKind.Lottie,
            ".lottie" or ".lot" => FileKind.Lottie,
            _ => FileKind.Unknown,
        };
    }

    /// <summary>Render an SVG file to PNG bytes.</summary>
    public static byte[] ConvertSvgToPng(string svgPath)
    {
        EnsureInit();
        {
            var picture = Picture.Gen();
            if (picture.Load(svgPath) != Result.Success)
                throw new InvalidOperationException($"Failed to load SVG: {svgPath}");

            picture.GetSize(out float fw, out float fh);
            uint w = (uint)MathF.Ceiling(fw);
            uint h = (uint)MathF.Ceiling(fh);
            if (w == 0 || h == 0)
                throw new InvalidOperationException("SVG has zero dimensions");

            // Cap at 4K
            const uint maxDim = 3840;
            if (w > maxDim || h > maxDim)
            {
                float scale = Math.Min((float)maxDim / w, (float)maxDim / h);
                w = (uint)(w * scale);
                h = (uint)(h * scale);
                picture.SetSize(w, h);
            }

            var buffer = new uint[w * h];
            var canvas = SwCanvas.Gen();
            canvas.Target(buffer, w, w, h, ColorSpace.ARGB8888S);
            canvas.Add(picture);
            canvas.Draw(true);
            canvas.Sync();

            return PngBuilder.Build(w, h, buffer);
        }
    }

    /// <summary>Convert a Lottie file to GIF bytes.</summary>
    public static byte[] ConvertLottieToGif(string lottiePath, uint quality = 100, uint fps = 0)
    {
        EnsureInit();
        {
            var animation = TvgAnimation.Gen();
            var pic = animation.GetPicture();
            if (pic.Load(lottiePath) != Result.Success)
                throw new InvalidOperationException($"Failed to load Lottie: {lottiePath}");

            // Add a solid white background so the GIF encoder writes every pixel
            // per frame instead of using transparency-based differencing.
            pic.GetSize(out float fw, out float fh);
            var bg = Shape.Gen();
            bg.AppendRect(0, 0, fw > 0 ? fw : 512, fh > 0 ? fh : 512);
            bg.SetFill(255, 255, 255);

            var tempFile = Path.Combine(Path.GetTempPath(), $"tvg_converter_{Guid.NewGuid():N}.gif");
            try
            {
                var saver = Saver.Gen();
                saver.Background(bg);
                saver.Save(animation, tempFile, quality, fps);
                saver.Sync();
                return File.ReadAllBytes(tempFile);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }
}

/// <summary>
/// Minimal PNG encoder. Adapted from tools/svg2png PngBuilder.
/// Writes ARGB8888 buffer to PNG byte array.
/// </summary>
static class PngBuilder
{
    private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static byte[] Build(uint width, uint height, uint[] buffer)
    {
        using var output = new MemoryStream();

        // PNG signature
        output.Write(Signature);

        // IHDR
        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0), width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // color type: RGBA
        ihdr[10] = 0; // compression method
        ihdr[11] = 0; // filter method
        ihdr[12] = 0; // interlace method
        WriteChunk(output, "IHDR"u8, ihdr);

        // IDAT — deflate-compressed filtered scanlines
        byte[] idat;
        using (var ms = new MemoryStream())
        {
            // zlib header
            ms.WriteByte(0x78); // CMF: deflate, window size 32768
            ms.WriteByte(0x01); // FLG: no dict, check bits

            uint adler = 1;

            using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            {
                var row = new byte[1 + width * 4]; // filter byte + RGBA pixels
                for (uint y = 0; y < height; y++)
                {
                    row[0] = 0; // filter: None
                    for (uint x = 0; x < width; x++)
                    {
                        // ARGB8888 -> RGBA
                        uint n = buffer[y * width + x];
                        int off = (int)(1 + x * 4);
                        row[off + 0] = (byte)((n >> 16) & 0xff); // R
                        row[off + 1] = (byte)((n >> 8) & 0xff);  // G
                        row[off + 2] = (byte)(n & 0xff);          // B
                        row[off + 3] = (byte)((n >> 24) & 0xff); // A
                    }
                    deflate.Write(row);
                    adler = UpdateAdler32(adler, row);
                }
            }

            // Adler-32 checksum
            var adlerBytes = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(adlerBytes, adler);
            ms.Write(adlerBytes);

            idat = ms.ToArray();
        }
        WriteChunk(output, "IDAT"u8, idat);

        // IEND
        WriteChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);

        return output.ToArray();
    }

    private static void WriteChunk(Stream s, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> tmp = stackalloc byte[4];

        BinaryPrimitives.WriteUInt32BigEndian(tmp, (uint)data.Length);
        s.Write(tmp);

        s.Write(type);

        if (data.Length > 0) s.Write(data);

        uint crc = Crc32(type, data);
        BinaryPrimitives.WriteUInt32BigEndian(tmp, crc);
        s.Write(tmp);
    }

    private static readonly uint[] CrcTable = MakeCrcTable();

    private static uint[] MakeCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static uint Crc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in type)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (byte b in data)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFF;
    }

    private static uint UpdateAdler32(uint adler, ReadOnlySpan<byte> data)
    {
        uint s1 = adler & 0xFFFF;
        uint s2 = (adler >> 16) & 0xFFFF;
        foreach (byte b in data)
        {
            s1 = (s1 + b) % 65521;
            s2 = (s2 + s1) % 65521;
        }
        return (s2 << 16) | s1;
    }
}
