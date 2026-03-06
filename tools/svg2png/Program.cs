// Ported from ThorVG/tools/svg2png/svg2png.cpp

using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace ThorVG.Tools
{
    static class PngBuilder
    {
        private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        public static void Build(string fileName, uint width, uint height, uint[] buffer)
        {
            using var fs = File.Create(fileName);

            // PNG signature
            fs.Write(Signature);

            // IHDR
            var ihdr = new byte[13];
            BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0), width);
            BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4), height);
            ihdr[8] = 8;  // bit depth
            ihdr[9] = 6;  // color type: RGBA
            ihdr[10] = 0; // compression method
            ihdr[11] = 0; // filter method
            ihdr[12] = 0; // interlace method
            WriteChunk(fs, "IHDR"u8, ihdr);

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
                            // ARGB8888 → RGBA
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
            WriteChunk(fs, "IDAT"u8, idat);

            // IEND
            WriteChunk(fs, "IEND"u8, ReadOnlySpan<byte>.Empty);
        }

        private static void WriteChunk(Stream s, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
        {
            Span<byte> tmp = stackalloc byte[4];

            // Length
            BinaryPrimitives.WriteUInt32BigEndian(tmp, (uint)data.Length);
            s.Write(tmp);

            // Type
            s.Write(type);

            // Data
            if (data.Length > 0) s.Write(data);

            // CRC32 over type + data
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

    class Renderer
    {
        private const int WIDTH_8K = 7680;
        private const int HEIGHT_8K = 4320;
        private const int SIZE_8K = 33177600; // WIDTH_8K * HEIGHT_8K

        private SwCanvas? canvas;
        private uint[]? buffer;
        private uint bufferSize;

        public int Render(string path, int w, int h, string dst, uint bgColor)
        {
            // Canvas
            if (canvas == null) CreateCanvas();
            if (canvas == null)
            {
                Console.WriteLine("Error: Canvas failure");
                return 1;
            }

            // Picture
            var picture = Picture.Gen();
            var result = picture.Load(path);
            if (result == Result.Unknown)
            {
                Console.WriteLine($"Error: Couldn't load image {path}");
                return 1;
            }
            else if (result == Result.InvalidArguments)
            {
                Console.WriteLine($"Error: Couldn't load image(Invalid path or invalid SVG image) : {path}");
                return 1;
            }
            else if (result == Result.NonSupport)
            {
                Console.WriteLine($"Error: Couldn't load image(Not supported extension) : {path}");
                return 1;
            }

            if (w == 0 || h == 0)
            {
                picture.GetSize(out float fw, out float fh);
                w = (int)fw;
                h = (int)fh;
                if (fw > w) w++;
                if (fh > h) h++;

                if ((long)w * h > SIZE_8K)
                {
                    float scale = fw / fh;
                    if (scale > 1)
                    {
                        w = WIDTH_8K;
                        h = (int)(w / scale);
                    }
                    else
                    {
                        h = HEIGHT_8K;
                        w = (int)(h * scale);
                    }
                    Console.WriteLine($"Warning: The SVG width and/or height values exceed the 8k resolution. " +
                        $"To avoid the heap overflow, the conversion to the PNG file made in {w} x {h} resolution.");
                    picture.SetSize((float)w, (float)h);
                }
            }
            else
            {
                picture.SetSize((float)w, (float)h);
            }

            // Buffer
            CreateBuffer(w, h);
            if (buffer == null)
            {
                Console.WriteLine("Error: Buffer failure");
                return 1;
            }

            if (canvas.Target(buffer, (uint)w, (uint)w, (uint)h, ColorSpace.ARGB8888S) != Result.Success)
            {
                Console.WriteLine("Error: Canvas target failure");
                return 1;
            }

            // Background color if needed
            if (bgColor != 0xffffffff)
            {
                byte r = (byte)((bgColor & 0xff0000) >> 16);
                byte g = (byte)((bgColor & 0x00ff00) >> 8);
                byte b = (byte)(bgColor & 0x0000ff);

                var shape = Shape.Gen();
                shape.AppendRect(0, 0, (float)w, (float)h);
                shape.SetFill(r, g, b);

                if (canvas.Add(shape) != Result.Success) return 1;
            }

            // Drawing
            canvas.Add(picture);
            canvas.Draw(true);
            canvas.Sync();

            // Build Png
            PngBuilder.Build(dst, (uint)w, (uint)h, buffer);

            Console.WriteLine($"Generated PNG file: {dst}");

            return 0;
        }

        public void Terminate()
        {
            Initializer.Term();
            buffer = null;
        }

        private void CreateCanvas()
        {
            // Thread count
            uint threads = (uint)Environment.ProcessorCount;
            if (threads > 0) threads--;

            // Initialize ThorVG Engine
            if (Initializer.Init(threads) != Result.Success)
            {
                Console.WriteLine("Error: Engine is not supported");
            }

            // Create a Canvas
            canvas = SwCanvas.Gen();
        }

        private void CreateBuffer(int w, int h)
        {
            uint size = (uint)(w * h);
            // Reuse old buffer if size is enough
            if (buffer != null && bufferSize >= size) return;

            buffer = new uint[size];
            bufferSize = size;
        }
    }

    class App
    {
        private Renderer renderer = new Renderer();
        private uint bgColor = 0xffffffff;
        private int width = 0;
        private int height = 0;

        private static void HelpMsg()
        {
            Console.WriteLine(
                "Usage:\n" +
                "   svg2png [SVG file] or [SVG folder] [-r resolution] [-b bgColor]\n\n" +
                "Flags:\n" +
                "    -r set the output image resolution.\n" +
                "    -b set the output image background color.\n\n" +
                "Examples:\n" +
                "    $ svg2png input.svg\n" +
                "    $ svg2png input.svg -r 200x200\n" +
                "    $ svg2png input.svg -r 200x200 -b ff00ff\n" +
                "    $ svg2png input1.svg input2.svg -r 200x200 -b ff00ff\n" +
                "    $ svg2png . -r 200x200\n\n" +
                "Note:\n" +
                "    In the case, where the width and height in the SVG file determine the size\n" +
                "    of the image in resolution higher than 8k (7680 x 4320), limiting the\n" +
                "    resolution to this value is enforced.\n");
        }

        private static bool SvgFile(string path)
        {
            return Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase);
        }

        private int RenderFile(string path)
        {
            // Destination png file
            var dst = Path.ChangeExtension(path, ".png");
            return renderer.Render(path, width, height, dst, bgColor);
        }

        private int HandleDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"Couldn't open directory \"{path}\".");
                return 1;
            }

            int ret = 0;
            foreach (var entry in Directory.EnumerateFileSystemEntries(path))
            {
                var name = Path.GetFileName(entry);
                if (name.StartsWith('.') || name.StartsWith('$')) continue;

                if (Directory.Exists(entry))
                {
                    ret = HandleDirectory(entry);
                    if (ret != 0) break;
                }
                else
                {
                    if (!SvgFile(entry)) continue;
                    ret = RenderFile(entry);
                    if (ret != 0) break;
                }
            }
            return ret;
        }

        public int Setup(string[] args)
        {
            var paths = new System.Collections.Generic.List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                var p = args[i];
                if (p.StartsWith('-'))
                {
                    var pArg = (i + 1 < args.Length) ? args[++i] : null;

                    if (p == "-r")
                    {
                        if (pArg == null)
                        {
                            Console.WriteLine("Error: Missing resolution attribute. Expected eg. -r 200x200.");
                            return 1;
                        }
                        var parts = pArg.Split('x');
                        if (parts.Length == 2 &&
                            int.TryParse(parts[0], out var pw) &&
                            int.TryParse(parts[1], out var ph) &&
                            pw > 0 && ph > 0)
                        {
                            width = pw;
                            height = ph;
                        }
                        else
                        {
                            Console.WriteLine($"Error: Resolution ({pArg}) is corrupted. Expected eg. -r 200x200.");
                            return 1;
                        }
                    }
                    else if (p == "-b")
                    {
                        if (pArg == null)
                        {
                            Console.WriteLine("Error: Missing background color attribute. Expected eg. -b fa7410.");
                            return 1;
                        }
                        bgColor = Convert.ToUInt32(pArg, 16);
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Unknown flag ({p}).");
                    }
                }
                else
                {
                    paths.Add(p);
                }
            }

            int ret = 0;
            if (paths.Count == 0)
            {
                HelpMsg();
                return 1;
            }

            foreach (var input in paths)
            {
                var realPath = Path.GetFullPath(input);
                if (!File.Exists(realPath) && !Directory.Exists(realPath))
                {
                    Console.WriteLine($"Error: Invalid file or path name: \"{input}\"");
                    continue;
                }

                if (Directory.Exists(realPath))
                {
                    Console.WriteLine($"Trying load from directory \"{realPath}\".");
                    ret = HandleDirectory(realPath);
                    if (ret != 0) break;
                }
                else
                {
                    if (!SvgFile(realPath))
                    {
                        Console.WriteLine($"Error: File \"{input}\" is not a proper svg file.");
                        continue;
                    }
                    ret = RenderFile(realPath);
                    if (ret != 0) break;
                }
            }

            // Terminate renderer
            renderer.Terminate();

            return ret;
        }
    }

    class Program
    {
        static int Main(string[] args)
        {
            var app = new App();
            return app.Setup(args);
        }
    }
}
