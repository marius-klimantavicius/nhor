using Xunit;

namespace ThorVG.Tests
{
    public class tvgPictureTests
    {
        [Fact]
        public void Picture_Gen_ReturnsInstance()
        {
            var pic = Picture.Gen();
            Assert.NotNull(pic);
            Assert.Equal(Type.Picture, pic.PaintType());
        }

        [Fact]
        public void Picture_SetAndGetSize()
        {
            var pic = Picture.Gen();
            Assert.Equal(Result.Success, pic.SetSize(100.0f, 200.0f));
            // Without a loader, GetSize returns InsufficientCondition (matching C++ behavior)
            var result = pic.GetSize(out float w, out float h);
            Assert.Equal(Result.InsufficientCondition, result);
        }

        [Fact]
        public void Picture_SetAndGetSize_WithRawLoader()
        {
            var pic = Picture.Gen();
            var pixels = new uint[4 * 4];
            Assert.Equal(Result.Success, pic.Load(pixels, 4, 4, ColorSpace.ARGB8888, true));
            pic.SetSize(100.0f, 200.0f);
            var result = pic.GetSize(out float w, out float h);
            Assert.Equal(Result.Success, result);
            Assert.Equal(100.0f, w);
            Assert.Equal(200.0f, h);
        }

        [Fact]
        public void Picture_LoadFile_NonExistent_ReturnsInvalidArguments()
        {
            var pic = Picture.Gen();
            // A non-existent file is tried against all loaders and none can open it,
            // so it's reported as invalid (matching C++ behavior).
            Assert.Equal(Result.InvalidArguments, pic.Load("test.svg"));
        }

        [Fact]
        public void Picture_LoadData_ReturnsNonSupport()
        {
            var pic = Picture.Gen();
            Assert.Equal(Result.NonSupport, pic.Load(new byte[] { 0 }, 1, "image/png"));
        }

        [Fact]
        public void Picture_LoadRawData()
        {
            var pic = Picture.Gen();
            var pixels = new uint[10 * 10];
            Assert.Equal(Result.Success, pic.Load(pixels, 10, 10, ColorSpace.ARGB8888, true));
        }

        [Fact]
        public void Picture_Duplicate()
        {
            var pic = Picture.Gen();
            // Load some raw data so the picture has a loader and size
            var pixels = new uint[50 * 60];
            Assert.Equal(Result.Success, pic.Load(pixels, 50, 60, ColorSpace.ARGB8888, true));

            var dup = (Picture)pic.Duplicate();
            dup.GetSize(out float w, out float h);
            Assert.Equal(50.0f, w);
            Assert.Equal(60.0f, h);
        }

        [Fact]
        public void PictureIterator_NullVector()
        {
            var pic = Picture.Gen();
            var it = IteratorAccessor.GetIterator(pic);
            Assert.NotNull(it);
            Assert.Equal(0u, it!.Count());
            Assert.Null(it.Next());
        }

        [Fact]
        public void Picture_SetAndGetOrigin()
        {
            var pic = Picture.Gen();
            Assert.Equal(Result.Success, pic.SetOrigin(0.5f, 0.3f));
            pic.GetOrigin(out float x, out float y);
            Assert.Equal(0.5f, x);
            Assert.Equal(0.3f, y);
        }
    }
}
