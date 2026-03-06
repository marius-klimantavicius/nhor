// Tests for the JPEG image loader (ported from ThorVG/src/loaders/jpg/)

using System.IO;
using Xunit;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace ThorVG.Tests
{
    public class JpgLoaderTests
    {
        /// <summary>
        /// Generate a minimal valid JPEG image in memory using ImageSharp.
        /// </summary>
        private static byte[] CreateTestJpeg(int width = 4, int height = 4)
        {
            using var image = new Image<Rgba32>(width, height);

            // Fill with solid blue
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        row[x] = new Rgba32(0, 0, 255, 255);
                    }
                }
            });

            using var ms = new MemoryStream();
            image.SaveAsJpeg(ms, new JpegEncoder { Quality = 90 });
            return ms.ToArray();
        }

        // ---- Open from data tests ----

        [Fact]
        public void Open_FromData_ValidJpeg_ReturnsTrue()
        {
            var loader = new JpgLoader();
            var jpg = CreateTestJpeg();
            Assert.True(loader.Open(jpg, (uint)jpg.Length, null, true));
        }

        [Fact]
        public void Open_FromData_SetsDimensions()
        {
            var loader = new JpgLoader();
            var jpg = CreateTestJpeg(8, 6);
            loader.Open(jpg, (uint)jpg.Length, null, true);
            Assert.Equal(8.0f, loader.w);
            Assert.Equal(6.0f, loader.h);
        }

        [Fact]
        public void Open_FromData_InvalidData_ReturnsFalse()
        {
            var loader = new JpgLoader();
            var garbage = new byte[] { 1, 2, 3, 4, 5 };
            Assert.False(loader.Open(garbage, (uint)garbage.Length, null, true));
        }

        [Fact]
        public void Open_FromData_NoCopy_Works()
        {
            var loader = new JpgLoader();
            var jpg = CreateTestJpeg();
            Assert.True(loader.Open(jpg, (uint)jpg.Length, null, false));
        }

        // ---- Read / Decode tests ----

        [Fact]
        public void Read_DecodesPixelData()
        {
            var loader = new JpgLoader();
            var jpg = CreateTestJpeg(4, 4);
            loader.Open(jpg, (uint)jpg.Length, null, true);

            Assert.True(loader.Read());
            Assert.NotNull(loader.surface.data);
            Assert.Equal(16, loader.surface.data!.Length); // 4x4
            Assert.Equal(4u, loader.surface.w);
            Assert.Equal(4u, loader.surface.h);
        }

        [Fact]
        public void Read_SetsSurfaceChannelSize()
        {
            var loader = new JpgLoader();
            var jpg = CreateTestJpeg();
            loader.Open(jpg, (uint)jpg.Length, null, true);
            loader.Read();
            Assert.Equal(4, loader.surface.channelSize);
        }

        [Fact]
        public void Read_SetsPremultiplied()
        {
            var loader = new JpgLoader();
            var jpg = CreateTestJpeg();
            loader.Open(jpg, (uint)jpg.Length, null, true);
            loader.Read();
            Assert.True(loader.surface.premultiplied);
        }

        [Fact]
        public void Read_WithoutOpen_ReturnsFalse()
        {
            var loader = new JpgLoader();
            Assert.False(loader.Read());
        }

        // ---- Bitmap tests ----

        [Fact]
        public void Bitmap_AfterRead_ReturnsSurface()
        {
            var loader = new JpgLoader();
            var jpg = CreateTestJpeg();
            loader.Open(jpg, (uint)jpg.Length, null, true);
            loader.Read();

            var bmp = loader.Bitmap();
            Assert.NotNull(bmp);
            Assert.Equal(4u, bmp!.w);
            Assert.Equal(4u, bmp.h);
        }

        [Fact]
        public void Bitmap_BeforeRead_ReturnsNull()
        {
            var loader = new JpgLoader();
            Assert.Null(loader.Bitmap());
        }

        // ---- Open from file tests ----

        [Fact]
        public void Open_FromFile_ValidJpeg()
        {
            var jpg = CreateTestJpeg(16, 12);
            var tmpFile = Path.GetTempFileName() + ".jpg";
            try
            {
                File.WriteAllBytes(tmpFile, jpg);
                var loader = new JpgLoader();
                Assert.True(loader.Open(tmpFile));
                Assert.Equal(16.0f, loader.w);
                Assert.Equal(12.0f, loader.h);
            }
            finally
            {
                if (File.Exists(tmpFile)) File.Delete(tmpFile);
            }
        }

        [Fact]
        public void Open_FromFile_InvalidPath_ReturnsFalse()
        {
            var loader = new JpgLoader();
            Assert.False(loader.Open("/nonexistent/file.jpg"));
        }

        // ---- Close test ----

        [Fact]
        public void Close_WithZeroSharing_ReturnsTrue()
        {
            var loader = new JpgLoader();
            Assert.True(loader.Close());
        }

        // ---- FileType test ----

        [Fact]
        public void Type_IsJpg()
        {
            var loader = new JpgLoader();
            Assert.Equal(FileType.Jpg, loader.type);
        }

        // ---- JpegDecoder direct tests ----

        [Fact]
        public void JpegDecoder_FromData_ValidJpeg()
        {
            var jpg = CreateTestJpeg(8, 8);
            var decoder = JpegDecoder.FromData(jpg, jpg.Length, out var w, out var h);
            Assert.NotNull(decoder);
            Assert.Equal(8, w);
            Assert.Equal(8, h);
        }

        [Fact]
        public void JpegDecoder_FromData_InvalidData_ReturnsNull()
        {
            var garbage = new byte[] { 0xFF, 0xD8, 0x00 }; // partial JPEG header
            var decoder = JpegDecoder.FromData(garbage, garbage.Length, out _, out _);
            Assert.Null(decoder);
        }

        [Fact]
        public void JpegDecoder_Decompress_ReturnsPixels()
        {
            var jpg = CreateTestJpeg(4, 4);
            var decoder = JpegDecoder.FromData(jpg, jpg.Length, out _, out _);
            Assert.NotNull(decoder);
            var pixels = decoder!.Decompress(ColorSpace.ABGR8888);
            Assert.NotNull(pixels);
            Assert.Equal(16, pixels!.Length);
        }
    }
}
