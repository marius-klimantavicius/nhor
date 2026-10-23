using System;
using System.IO;
using Xunit;

namespace ThorVG.Tests
{
    public class tvgLoaderUpdateTests
    {
        private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", name);

        [Theory]
        [InlineData("image/png", Result.InvalidArguments)]
        [InlineData("png", Result.InvalidArguments)]
        [InlineData("webp", Result.NonSupport)]
        [InlineData("mp4", Result.NonSupport)]
        [InlineData(null, Result.InvalidArguments)]
        public void Picture_InvalidData_PreservesNativeResult(string? mime, Result expected)
        {
            var picture = Picture.Gen();
            Assert.Equal(expected, picture.Load(new byte[] { 0 }, 1, mime));
            Paint.Rel(picture);
        }

        [Fact]
        public void RawLoader_OpaqueFormatAndInvalidLength()
        {
            var loader = new RawLoader();
            Assert.False(loader.Open(new uint[1], 2, 2, ColorSpace.ABGR8888, false));
            Assert.True(loader.Open(new uint[4], 2, 2, ColorSpace.XBGR8888, false));
            Assert.Equal(ColorSpace.ABGR8888, loader.surface.cs);
            Assert.True(loader.surface.opaque);
            Assert.True(loader.surface.premultiplied);
            var other = new RawLoader();
            Assert.True(other.Open(new uint[4], 2, 2, ColorSpace.XRGB8888, false));
            Assert.Equal(ColorSpace.ARGB8888, other.surface.cs);
            Assert.True(other.surface.opaque);
            loader.surface.Dispose();
            other.surface.Dispose();
        }

        [Fact]
        public unsafe void PictureRelease_UnpinsOnlyAfterLastSharedLoader()
        {
            var pixels = new uint[16];
            var loader = Assert.IsType<RawLoader>(LoaderMgr.Loader(pixels, 4, 4, ColorSpace.ABGR8888, false));
            var picture = Picture.Gen();
            Assert.Equal(Result.Success, picture.Load(pixels, 4, 4, ColorSpace.ABGR8888));
            Paint.Rel(picture);
            Assert.True(loader.surface.buf32 != null);
            Assert.True(LoaderMgr.Retrieve(loader));
            Assert.True(loader.surface.buf32 == null);
        }

        [Theory]
        [InlineData("none")]
        [InlineData("linear")]
        [InlineData("gamma")]
        [InlineData("lut")]
        [InlineData("invalid")]
        public void PngLoader_ProfilesPreserveAlpha(string profile)
        {
            var loader = new PngLoader();
            Assert.True(loader.Open(Fixture("icc-" + profile + ".png")));
            Assert.True(loader.Read());
            var bitmap = Assert.IsType<RenderSurface>(loader.Bitmap());
            Assert.Equal(32u, bitmap.w);
            Assert.Equal(4u, bitmap.h);
            for (int y = 0; y < 4; ++y)
                for (int x = 0; x < 32; ++x)
                    Assert.Equal((uint)(y * 85), bitmap.data![y * 32 + x] >> 24);
            bitmap.Dispose();
        }

        [Fact]
        public void PngLoader_WorkerDecodingMatchesSynchronousOutput()
        {
            // Direct scheduler setup isolates worker selection from shared initializer references.
            TaskScheduler.Term();
            TaskScheduler.Init(2);
            try
            {
                var asyncLoader = new PngLoader();
                Assert.True(asyncLoader.Open(Fixture("icc-gamma.png")));
                Assert.True(asyncLoader.Read());
                var actual = Assert.IsType<RenderSurface>(asyncLoader.Bitmap()).data!;
                TaskScheduler.Term();
                TaskScheduler.Init(0);
                var syncLoader = new PngLoader();
                Assert.True(syncLoader.Open(Fixture("icc-gamma.png")));
                Assert.True(syncLoader.Read());
                var expected = Assert.IsType<RenderSurface>(syncLoader.Bitmap()).data!;
                Assert.Equal(expected, actual);
                asyncLoader.surface.Dispose();
                syncLoader.surface.Dispose();
            }
            finally { TaskScheduler.Term(); TaskScheduler.Init(0); }
        }

        [Fact]
        public void JpegDecoder_DuplicateSosComponentRejected()
        {
            var data = File.ReadAllBytes(Fixture("duplicate-sos-component.jpg"));
            var decoder = JpegDecoder.FromData(data, data.Length, out _, out _);
            // A malformed scan may be discovered during header processing or decoding.
            if (decoder != null) Assert.Null(decoder.Decompress());
        }

        [Fact]
        public void LoaderData_DeclaredLengthCannotExceedManagedBuffer()
        {
            var bytes = new byte[8];
            Assert.Equal(Result.InvalidArguments, Picture.Gen().Load(bytes, 100, "png"));
            Assert.NotEqual(0u, LodePng.Inspect(bytes, 100, out _, out _));
            Assert.NotEqual(0u, LodePng.Decode(bytes, -1, out _, out _, out _));
            Assert.False(new JpgLoader().Open(bytes, 100, null, true));
        }
    }
}
