// Tests for the LoaderMgr (image loader registration and lookup)

using System.IO;
using Xunit;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ThorVG.Tests
{
    public class LoaderMgrTests
    {
        // ---- Find tests ----

        [Fact]
        public void Find_Png_ReturnsPngLoader()
        {
            var loader = LoaderMgr.Find(FileType.Png);
            Assert.NotNull(loader);
            Assert.IsType<PngLoader>(loader);
        }

        [Fact]
        public void Find_Jpg_ReturnsJpgLoader()
        {
            var loader = LoaderMgr.Find(FileType.Jpg);
            Assert.NotNull(loader);
            Assert.IsType<JpgLoader>(loader);
        }

        [Fact]
        public void Find_Webp_ReturnsNull()
        {
            var loader = LoaderMgr.Find(FileType.Webp);
            Assert.Null(loader);
        }

        [Fact]
        public void Find_Raw_ReturnsRawLoader()
        {
            var loader = LoaderMgr.Find(FileType.Raw);
            Assert.NotNull(loader);
            Assert.IsType<RawLoader>(loader);
        }

        [Fact]
        public void Find_Unknown_ReturnsNull()
        {
            var loader = LoaderMgr.Find(FileType.Unknown);
            Assert.Null(loader);
        }

        // ---- Loader from raw pixel data ----

        [Fact]
        public void Loader_RawData_ReturnsRawLoader()
        {
            var data = new uint[16]; // 4x4
            var loader = LoaderMgr.Loader(data, 4, 4, ColorSpace.ABGR8888, false);
            Assert.NotNull(loader);
            Assert.IsType<RawLoader>(loader);
        }

        // ---- Init / Term ----

        [Fact]
        public void Init_ReturnsTrue()
        {
            Assert.True(LoaderMgr.Init());
        }

        [Fact]
        public void Term_ReturnsTrue()
        {
            Assert.True(LoaderMgr.Term());
        }

        // ---- Loader from file path by extension ----

        [Fact]
        public void Loader_ByPath_JpegExtension()
        {
            // Create a temp JPEG file
            using var image = new Image<Rgba32>(2, 2);
            var tmpFile = Path.GetTempFileName() + ".jpg";
            try
            {
                image.SaveAsJpeg(tmpFile);
                var loader = LoaderMgr.Loader(tmpFile, out var invalid);
                Assert.NotNull(loader);
                Assert.False(invalid);
                Assert.IsType<JpgLoader>(loader);
            }
            finally
            {
                if (File.Exists(tmpFile)) File.Delete(tmpFile);
            }
        }

        [Fact]
        public void Loader_ByPath_InvalidPath_ReturnsNull()
        {
            var loader = LoaderMgr.Loader("/nonexistent/file.png", out var invalid);
            Assert.Null(loader);
            Assert.True(invalid);
        }

        // ---- Loader from data with MIME type ----

        [Fact]
        public void Loader_FromData_JpegMime()
        {
            using var image = new Image<Rgba32>(2, 2);
            using var ms = new MemoryStream();
            image.SaveAsJpeg(ms);
            var data = ms.ToArray();

            var loader = LoaderMgr.Loader(data, (uint)data.Length, "jpg", null, true);
            Assert.NotNull(loader);
            Assert.IsType<JpgLoader>(loader);
        }

        [Fact]
        public void Loader_FromData_PngMime()
        {
            using var image = new Image<Rgba32>(2, 2);
            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            var data = ms.ToArray();

            var loader = LoaderMgr.Loader(data, (uint)data.Length, "png", null, true);
            Assert.NotNull(loader);
            Assert.IsType<PngLoader>(loader);
        }

        [Fact]
        public void Loader_FromData_WebpMime_ReturnsNull()
        {
            // WebP loader removed for now
            var data = new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }; // RIFF....WEBP
            var loader = LoaderMgr.Loader(data, (uint)data.Length, "webp", null, true);
            Assert.Null(loader);
        }

        [Fact]
        public void Loader_FromData_NoMime_AutoDetects()
        {
            using var image = new Image<Rgba32>(2, 2);
            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            var data = ms.ToArray();

            // No MIME type -- should auto-detect as PNG
            var loader = LoaderMgr.Loader(data, (uint)data.Length, null, null, true);
            Assert.NotNull(loader);
            Assert.IsType<PngLoader>(loader);
        }
    }
}
