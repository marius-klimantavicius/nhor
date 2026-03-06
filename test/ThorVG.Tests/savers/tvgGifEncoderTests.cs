using System;
using System.IO;
using Xunit;

namespace ThorVG.Tests
{
    public unsafe class tvgGifEncoderTests : IDisposable
    {
        private readonly string _tempDir;

        public tvgGifEncoderTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ThorVG_GifTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Fact]
        public void GifBegin_CreatesFile()
        {
            var path = Path.Combine(_tempDir, "test_begin.gif");
            var writer = new GifWriter();

            Assert.True(GifEncoder.GifBegin(writer, path, 2, 2, 10));
            Assert.NotNull(writer.f);
            Assert.NotNull(writer.oldImage);
            Assert.NotNull(writer.tmpImage);
            Assert.True(writer.firstFrame);

            Assert.True(GifEncoder.GifEnd(writer));
            Assert.True(File.Exists(path));

            // Verify GIF89a header
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 6);
            Assert.Equal((byte)'G', bytes[0]);
            Assert.Equal((byte)'I', bytes[1]);
            Assert.Equal((byte)'F', bytes[2]);
            Assert.Equal((byte)'8', bytes[3]);
            Assert.Equal((byte)'9', bytes[4]);
            Assert.Equal((byte)'a', bytes[5]);

            // Verify trailer
            Assert.Equal(0x3b, bytes[bytes.Length - 1]);
        }

        [Fact]
        public void GifBegin_InvalidPath_ReturnsFalse()
        {
            var writer = new GifWriter();
            // Use an invalid path that should cause file creation to fail
            Assert.False(GifEncoder.GifBegin(writer, "/nonexistent/dir/test.gif", 2, 2, 10));
        }

        [Fact]
        public void GifEnd_NullFile_ReturnsFalse()
        {
            var writer = new GifWriter();
            Assert.False(GifEncoder.GifEnd(writer));
        }

        [Fact]
        public void GifWriteFrame_NullFile_ReturnsFalse()
        {
            var writer = new GifWriter();
            byte dummy = 0;
            Assert.False(GifEncoder.GifWriteFrame(writer, &dummy, 1, 1, 10, false));
        }

        [Fact]
        public void GifWriteFrame_SingleFrame()
        {
            var path = Path.Combine(_tempDir, "test_single.gif");
            var writer = new GifWriter();
            uint w = 4, h = 4;

            Assert.True(GifEncoder.GifBegin(writer, path, w, h, 10));

            // Create a simple RGBA image (all red)
            var image = new byte[w * h * 4];
            for (int i = 0; i < (int)(w * h); i++)
            {
                image[i * 4 + 0] = 255; // R
                image[i * 4 + 1] = 0;   // G
                image[i * 4 + 2] = 0;   // B
                image[i * 4 + 3] = 255; // A
            }

            fixed (byte* imgPtr = image)
            {
                Assert.True(GifEncoder.GifWriteFrame(writer, imgPtr, w, h, 10, false));
            }

            Assert.True(GifEncoder.GifEnd(writer));
            Assert.True(File.Exists(path));

            var bytes = File.ReadAllBytes(path);
            // Should have GIF header, frame data, and trailer
            Assert.True(bytes.Length > 13); // At least header + logical screen descriptor
        }

        [Fact]
        public void GifWriteFrame_MultipleFrames()
        {
            var path = Path.Combine(_tempDir, "test_multi.gif");
            var writer = new GifWriter();
            uint w = 4, h = 4;

            Assert.True(GifEncoder.GifBegin(writer, path, w, h, 10));

            // Frame 1: Red
            var image1 = new byte[w * h * 4];
            for (int i = 0; i < (int)(w * h); i++)
            {
                image1[i * 4 + 0] = 255;
                image1[i * 4 + 1] = 0;
                image1[i * 4 + 2] = 0;
                image1[i * 4 + 3] = 255;
            }

            fixed (byte* imgPtr = image1)
            {
                Assert.True(GifEncoder.GifWriteFrame(writer, imgPtr, w, h, 10, false));
            }

            // Frame 2: Blue
            var image2 = new byte[w * h * 4];
            for (int i = 0; i < (int)(w * h); i++)
            {
                image2[i * 4 + 0] = 0;
                image2[i * 4 + 1] = 0;
                image2[i * 4 + 2] = 255;
                image2[i * 4 + 3] = 255;
            }

            fixed (byte* imgPtr = image2)
            {
                Assert.True(GifEncoder.GifWriteFrame(writer, imgPtr, w, h, 10, false));
            }

            // Frame 3: Green
            var image3 = new byte[w * h * 4];
            for (int i = 0; i < (int)(w * h); i++)
            {
                image3[i * 4 + 0] = 0;
                image3[i * 4 + 1] = 255;
                image3[i * 4 + 2] = 0;
                image3[i * 4 + 3] = 255;
            }

            fixed (byte* imgPtr = image3)
            {
                Assert.True(GifEncoder.GifWriteFrame(writer, imgPtr, w, h, 10, false));
            }

            Assert.True(GifEncoder.GifEnd(writer));

            var bytes = File.ReadAllBytes(path);
            // Verify GIF header
            Assert.Equal((byte)'G', bytes[0]);
            Assert.Equal((byte)'I', bytes[1]);
            Assert.Equal((byte)'F', bytes[2]);
            // Multiple frames should produce a larger file
            Assert.True(bytes.Length > 100);
        }

        [Fact]
        public void GifWriteFrame_Transparent()
        {
            var path = Path.Combine(_tempDir, "test_transparent.gif");
            var writer = new GifWriter();
            uint w = 4, h = 4;

            Assert.True(GifEncoder.GifBegin(writer, path, w, h, 10));

            // Frame with some transparent and some opaque pixels
            var image = new byte[w * h * 4];
            for (int i = 0; i < (int)(w * h); i++)
            {
                image[i * 4 + 0] = 128; // R
                image[i * 4 + 1] = 64;  // G
                image[i * 4 + 2] = 32;  // B
                image[i * 4 + 3] = (byte)(i < (int)(w * h) / 2 ? 255 : 0); // Half transparent
            }

            fixed (byte* imgPtr = image)
            {
                Assert.True(GifEncoder.GifWriteFrame(writer, imgPtr, w, h, 10, true));
            }

            Assert.True(GifEncoder.GifEnd(writer));
            Assert.True(File.Exists(path));
        }

        [Fact]
        public void GifWriteFrame_LargeImage()
        {
            var path = Path.Combine(_tempDir, "test_large.gif");
            var writer = new GifWriter();
            uint w = 64, h = 64;

            Assert.True(GifEncoder.GifBegin(writer, path, w, h, 5));

            // Create a gradient image
            var image = new byte[w * h * 4];
            for (int y = 0; y < (int)h; y++)
            {
                for (int x = 0; x < (int)w; x++)
                {
                    int idx = (y * (int)w + x) * 4;
                    image[idx + 0] = (byte)(x * 4);       // R gradient
                    image[idx + 1] = (byte)(y * 4);       // G gradient
                    image[idx + 2] = (byte)((x + y) * 2); // B gradient
                    image[idx + 3] = 255;                  // A
                }
            }

            fixed (byte* imgPtr = image)
            {
                Assert.True(GifEncoder.GifWriteFrame(writer, imgPtr, w, h, 5, false));
            }

            Assert.True(GifEncoder.GifEnd(writer));
            Assert.True(File.Exists(path));
        }

        [Fact]
        public void GifBegin_NoDelay_NoAnimationHeader()
        {
            var path = Path.Combine(_tempDir, "test_nodelay.gif");
            var writer = new GifWriter();

            // delay=0 means no animation header (no NETSCAPE2.0 block)
            Assert.True(GifEncoder.GifBegin(writer, path, 2, 2, 0));
            Assert.True(GifEncoder.GifEnd(writer));

            var bytes = File.ReadAllBytes(path);

            // Check that NETSCAPE2.0 is NOT present
            var str = System.Text.Encoding.ASCII.GetString(bytes);
            Assert.DoesNotContain("NETSCAPE2.0", str);
        }

        [Fact]
        public void GifBegin_WithDelay_HasAnimationHeader()
        {
            var path = Path.Combine(_tempDir, "test_animated.gif");
            var writer = new GifWriter();

            Assert.True(GifEncoder.GifBegin(writer, path, 2, 2, 10));
            Assert.True(GifEncoder.GifEnd(writer));

            var bytes = File.ReadAllBytes(path);

            // Check that NETSCAPE2.0 IS present
            var str = System.Text.Encoding.ASCII.GetString(bytes);
            Assert.Contains("NETSCAPE2.0", str);
        }

        [Fact]
        public void GifWriteFrame_IdenticalFramesDelta()
        {
            // Two identical frames should use delta encoding (transparent pixels for unchanged)
            var path = Path.Combine(_tempDir, "test_delta.gif");
            var writer = new GifWriter();
            uint w = 8, h = 8;

            Assert.True(GifEncoder.GifBegin(writer, path, w, h, 10));

            var image = new byte[w * h * 4];
            for (int i = 0; i < (int)(w * h); i++)
            {
                image[i * 4 + 0] = 100;
                image[i * 4 + 1] = 150;
                image[i * 4 + 2] = 200;
                image[i * 4 + 3] = 255;
            }

            fixed (byte* imgPtr = image)
            {
                Assert.True(GifEncoder.GifWriteFrame(writer, imgPtr, w, h, 10, false));
                Assert.True(GifEncoder.GifWriteFrame(writer, imgPtr, w, h, 10, false));
            }

            Assert.True(GifEncoder.GifEnd(writer));
            Assert.True(File.Exists(path));
        }

        [Fact]
        public void GifWriter_Dimensions_EncodedCorrectly()
        {
            var path = Path.Combine(_tempDir, "test_dims.gif");
            var writer = new GifWriter();
            uint w = 320, h = 240;

            Assert.True(GifEncoder.GifBegin(writer, path, w, h, 10));
            Assert.True(GifEncoder.GifEnd(writer));

            var bytes = File.ReadAllBytes(path);
            // Width is at offset 6 (little-endian 16-bit)
            int encodedW = bytes[6] | (bytes[7] << 8);
            // Height is at offset 8 (little-endian 16-bit)
            int encodedH = bytes[8] | (bytes[9] << 8);

            Assert.Equal(320, encodedW);
            Assert.Equal(240, encodedH);
        }
    }

    public class tvgGifSaverTests
    {
        [Fact]
        public void GifSaver_Save_Paint_ReturnsFalse()
        {
            var saver = new GifSaver();
            var shape = Shape.Gen();
            Assert.False(saver.Save(shape, null, "test.gif", 100));
        }

        [Fact]
        public void GifSaver_Close_ReturnsTrue()
        {
            var saver = new GifSaver();
            Assert.True(saver.Close());
        }

        [Fact]
        public void GifSaver_Close_MultipleCalls_Safe()
        {
            var saver = new GifSaver();
            Assert.True(saver.Close());
            Assert.True(saver.Close());
        }

        [Fact]
        public void GifSaver_Dispose_Safe()
        {
            var saver = new GifSaver();
            saver.Dispose();
            // Should not throw
        }
    }
}
