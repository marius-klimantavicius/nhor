// Tests for the Raw image loader (ported from ThorVG/src/loaders/raw/)

using Xunit;

namespace ThorVG.Tests
{
    public class RawLoaderTests
    {
        /// <summary>Create a simple 4x4 pixel test image (solid red, ABGR8888).</summary>
        private static uint[] CreateTestImage(uint w, uint h, uint pixelValue = 0xFF0000FF)
        {
            var pixels = new uint[w * h];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = pixelValue;
            return pixels;
        }

        // ---- Open tests ----

        [Fact]
        public void Open_ValidData_ReturnsTrue()
        {
            var loader = new RawLoader();
            var data = CreateTestImage(4, 4);
            Assert.True(loader.Open(data, 4, 4, ColorSpace.ABGR8888, false));
        }

        [Fact]
        public void Open_NullData_ReturnsFalse()
        {
            var loader = new RawLoader();
            Assert.False(loader.Open(null!, 4, 4, ColorSpace.ABGR8888, false));
        }

        [Fact]
        public void Open_ZeroWidth_ReturnsFalse()
        {
            var loader = new RawLoader();
            var data = CreateTestImage(4, 4);
            Assert.False(loader.Open(data, 0, 4, ColorSpace.ABGR8888, false));
        }

        [Fact]
        public void Open_ZeroHeight_ReturnsFalse()
        {
            var loader = new RawLoader();
            var data = CreateTestImage(4, 4);
            Assert.False(loader.Open(data, 4, 0, ColorSpace.ABGR8888, false));
        }

        [Fact]
        public void Open_SetsDimensions()
        {
            var loader = new RawLoader();
            var data = CreateTestImage(8, 6);
            loader.Open(data, 8, 6, ColorSpace.ABGR8888, false);
            Assert.Equal(8.0f, loader.w);
            Assert.Equal(6.0f, loader.h);
        }

        [Fact]
        public void Open_Copy_ClonesData()
        {
            var loader = new RawLoader();
            var data = CreateTestImage(4, 4, 0xAABBCCDD);
            Assert.True(loader.Open(data, 4, 4, ColorSpace.ABGR8888, true));
            Assert.True(loader.copy);

            // Modify original data -- loader should have its own copy
            data[0] = 0;
            Assert.NotNull(loader.surface.data);
            Assert.Equal(0xAABBCCDDu, loader.surface.data![0]);
        }

        [Fact]
        public void Open_NoCopy_SharesData()
        {
            var loader = new RawLoader();
            var data = CreateTestImage(4, 4, 0xAABBCCDD);
            Assert.True(loader.Open(data, 4, 4, ColorSpace.ABGR8888, false));
            Assert.False(loader.copy);

            // Same array reference
            Assert.Same(data, loader.surface.data);
        }

        // ---- Surface setup tests ----

        [Fact]
        public void Open_SetsSurfaceProperties()
        {
            var loader = new RawLoader();
            var data = CreateTestImage(10, 5);
            loader.Open(data, 10, 5, ColorSpace.ARGB8888, false);

            Assert.Equal(10u, loader.surface.stride);
            Assert.Equal(10u, loader.surface.w);
            Assert.Equal(5u, loader.surface.h);
            Assert.Equal(ColorSpace.ARGB8888, loader.surface.cs);
            Assert.Equal(4, loader.surface.channelSize);
        }

        [Fact]
        public void Open_ABGR8888_SetsPremultiplied()
        {
            var loader = new RawLoader();
            var data = CreateTestImage(2, 2);
            loader.Open(data, 2, 2, ColorSpace.ABGR8888, false);
            Assert.True(loader.surface.premultiplied);
        }

        [Fact]
        public void Open_ARGB8888_SetsPremultiplied()
        {
            var loader = new RawLoader();
            var data = CreateTestImage(2, 2);
            loader.Open(data, 2, 2, ColorSpace.ARGB8888, false);
            Assert.True(loader.surface.premultiplied);
        }

        [Fact]
        public void Open_ABGR8888S_NotPremultiplied()
        {
            var loader = new RawLoader();
            var data = CreateTestImage(2, 2);
            loader.Open(data, 2, 2, ColorSpace.ABGR8888S, false);
            Assert.False(loader.surface.premultiplied);
        }

        // ---- Read tests ----

        [Fact]
        public void Read_ReturnsTrue()
        {
            var loader = new RawLoader();
            Assert.True(loader.Read());
        }

        // ---- FileType test ----

        [Fact]
        public void Type_IsRaw()
        {
            var loader = new RawLoader();
            Assert.Equal(FileType.Raw, loader.type);
        }
    }
}
