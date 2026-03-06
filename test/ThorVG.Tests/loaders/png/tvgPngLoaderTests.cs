// Tests for the PNG image loader (ported from ThorVG/src/loaders/png/)

using System;
using System.IO;
using System.IO.Compression;
using Xunit;

namespace ThorVG.Tests
{
    public class PngLoaderTests
    {
        /// <summary>
        /// Generate a minimal valid 2x2 red PNG image in memory.
        /// PNG format: signature + IHDR chunk + IDAT chunk + IEND chunk.
        /// </summary>
        private static byte[] CreateMinimalPng(int width = 2, int height = 2)
        {
            using var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            // PNG signature
            bw.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

            // IHDR chunk
            var ihdr = new byte[13];
            WriteInt32BE(ihdr, 0, width);
            WriteInt32BE(ihdr, 4, height);
            ihdr[8] = 8;   // bit depth
            ihdr[9] = 2;   // color type: RGB
            ihdr[10] = 0;  // compression
            ihdr[11] = 0;  // filter
            ihdr[12] = 0;  // interlace
            WriteChunk(bw, "IHDR", ihdr);

            // IDAT chunk - deflate-compressed raw pixel data
            // Each scanline: filter byte (0) + R,G,B per pixel
            using var rawMs = new MemoryStream();
            // Use DeflateStream wrapped in zlib header
            // Write zlib header: CMF=0x78, FLG=0x01
            rawMs.WriteByte(0x78);
            rawMs.WriteByte(0x01);
            using (var deflate = new DeflateStream(rawMs, CompressionLevel.Fastest, leaveOpen: true))
            {
                for (int y = 0; y < height; y++)
                {
                    deflate.WriteByte(0); // filter: None
                    for (int x = 0; x < width; x++)
                    {
                        deflate.WriteByte(0xFF); // R
                        deflate.WriteByte(0x00); // G
                        deflate.WriteByte(0x00); // B
                    }
                }
            }
            // Adler32 checksum
            var rawData = new byte[height * (1 + width * 3)];
            int idx = 0;
            for (int y = 0; y < height; y++)
            {
                rawData[idx++] = 0;
                for (int x = 0; x < width; x++)
                {
                    rawData[idx++] = 0xFF;
                    rawData[idx++] = 0x00;
                    rawData[idx++] = 0x00;
                }
            }
            var adler = Adler32(rawData);
            var adlerBytes = new byte[4];
            WriteInt32BE(adlerBytes, 0, (int)adler);
            rawMs.Write(adlerBytes);
            WriteChunk(bw, "IDAT", rawMs.ToArray());

            // IEND chunk
            WriteChunk(bw, "IEND", Array.Empty<byte>());

            return ms.ToArray();
        }

        private static void WriteInt32BE(byte[] buf, int offset, int value)
        {
            buf[offset] = (byte)((value >> 24) & 0xFF);
            buf[offset + 1] = (byte)((value >> 16) & 0xFF);
            buf[offset + 2] = (byte)((value >> 8) & 0xFF);
            buf[offset + 3] = (byte)(value & 0xFF);
        }

        private static void WriteChunk(BinaryWriter bw, string type, byte[] data)
        {
            // length (big-endian)
            var lenBytes = new byte[4];
            WriteInt32BE(lenBytes, 0, data.Length);
            bw.Write(lenBytes);

            // type
            var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            bw.Write(typeBytes);

            // data
            bw.Write(data);

            // CRC32 over type + data
            var crcData = new byte[4 + data.Length];
            Array.Copy(typeBytes, 0, crcData, 0, 4);
            Array.Copy(data, 0, crcData, 4, data.Length);
            var crc = Crc32(crcData);
            var crcBytes = new byte[4];
            WriteInt32BE(crcBytes, 0, (int)crc);
            bw.Write(crcBytes);
        }

        private static uint Crc32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (var b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                    crc = (crc >> 1) ^ (0xEDB88320 & (uint)(-(int)(crc & 1)));
            }
            return crc ^ 0xFFFFFFFF;
        }

        private static uint Adler32(byte[] data)
        {
            uint a = 1, b = 0;
            foreach (var d in data)
            {
                a = (a + d) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }

        // ---- Open from data tests ----

        [Fact]
        public void Open_FromData_ValidPng_ReturnsTrue()
        {
            var loader = new PngLoader();
            var png = CreateMinimalPng();
            Assert.True(loader.Open(png, (uint)png.Length, null, true));
        }

        [Fact]
        public void Open_FromData_SetsDimensions()
        {
            var loader = new PngLoader();
            var png = CreateMinimalPng(4, 3);
            loader.Open(png, (uint)png.Length, null, true);
            Assert.Equal(4.0f, loader.w);
            Assert.Equal(3.0f, loader.h);
        }

        [Fact]
        public void Open_FromData_InvalidData_ReturnsFalse()
        {
            var loader = new PngLoader();
            var garbage = new byte[] { 1, 2, 3, 4, 5 };
            Assert.False(loader.Open(garbage, (uint)garbage.Length, null, true));
        }

        [Fact]
        public void Open_FromData_NoCopy_Works()
        {
            var loader = new PngLoader();
            var png = CreateMinimalPng();
            Assert.True(loader.Open(png, (uint)png.Length, null, false));
        }

        // ---- Read / Decode tests ----

        [Fact]
        public void Read_DecodesPixelData()
        {
            var loader = new PngLoader();
            var png = CreateMinimalPng(2, 2);
            loader.Open(png, (uint)png.Length, null, true);

            Assert.True(loader.Read());
            Assert.NotNull(loader.surface.data);
            Assert.Equal(4, loader.surface.data!.Length); // 2x2
            Assert.Equal(2u, loader.surface.w);
            Assert.Equal(2u, loader.surface.h);
        }

        [Fact]
        public void Read_SetsColorSpace()
        {
            var loader = new PngLoader();
            var png = CreateMinimalPng();
            loader.Open(png, (uint)png.Length, null, true);
            loader.Read();
            Assert.Equal(ColorSpace.ABGR8888S, loader.surface.cs);
        }

        [Fact]
        public void Read_NoData_ReturnsFalse()
        {
            var loader = new PngLoader();
            Assert.False(loader.Read());
        }

        // ---- Bitmap tests ----

        [Fact]
        public void Bitmap_AfterRead_ReturnsSurface()
        {
            var loader = new PngLoader();
            var png = CreateMinimalPng();
            loader.Open(png, (uint)png.Length, null, true);
            loader.Read();

            var bmp = loader.Bitmap();
            Assert.NotNull(bmp);
            Assert.Equal(2u, bmp!.w);
            Assert.Equal(2u, bmp.h);
        }

        [Fact]
        public void Bitmap_BeforeRead_ReturnsNull()
        {
            var loader = new PngLoader();
            Assert.Null(loader.Bitmap());
        }

        // ---- Open from file tests ----

        [Fact]
        public void Open_FromFile_ValidPng()
        {
            var png = CreateMinimalPng(8, 8);
            var tmpFile = Path.GetTempFileName() + ".png";
            try
            {
                File.WriteAllBytes(tmpFile, png);
                var loader = new PngLoader();
                Assert.True(loader.Open(tmpFile));
                Assert.Equal(8.0f, loader.w);
                Assert.Equal(8.0f, loader.h);
            }
            finally
            {
                if (File.Exists(tmpFile)) File.Delete(tmpFile);
            }
        }

        [Fact]
        public void Open_FromFile_InvalidPath_ReturnsFalse()
        {
            var loader = new PngLoader();
            Assert.False(loader.Open("/nonexistent/file.png"));
        }

        // ---- FileType test ----

        [Fact]
        public void Type_IsPng()
        {
            var loader = new PngLoader();
            Assert.Equal(FileType.Png, loader.type);
        }

        // ---- LodePng.Inspect tests ----

        [Fact]
        public void Inspect_ValidPng_ReturnsDimensions()
        {
            var png = CreateMinimalPng(16, 8);
            Assert.Equal(0u, LodePng.Inspect(png, png.Length, out var w, out var h));
            Assert.Equal(16u, w);
            Assert.Equal(8u, h);
        }

        [Fact]
        public void Inspect_InvalidData_ReturnsNonZero()
        {
            var garbage = new byte[] { 0, 0, 0 };
            Assert.NotEqual(0u, LodePng.Inspect(garbage, garbage.Length, out _, out _));
        }
    }
}
