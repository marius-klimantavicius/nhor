using System;
using Xunit;

namespace ThorVG.Tests
{
    public unsafe class tvgCandidateRegressionTests : IDisposable
    {
        public tvgCandidateRegressionTests() { Assert.Equal(Result.Success, Initializer.Init(0)); }
        public void Dispose() { Assert.Equal(Result.Success, Initializer.Term()); }

        [Fact]
        public void SwRenderer_BoundsThenUpdate_ZeroAlphaStrokePreservesStrokeRle()
        {
            using var renderer = SwRenderer.Gen();
            var buffer = new uint[100 * 100];
            Assert.True(renderer.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888));
            renderer.Viewport(new RenderRegion(0, 0, 100, 100));

            var rshape = new RenderShape
            {
                stroke = new RenderStroke
                {
                    width = 12.0f,
                    color = new RGBA(255, 0, 0, 0)
                }
            };
            rshape.path.MoveTo(new Point(10, 50));
            rshape.path.LineTo(new Point(90, 50));

            var clips = new ValueList<object?>();
            var transform = TvgMath.Identity();
            var data = renderer.Prepare(rshape, null, transform, ref clips, 255, RenderUpdateFlag.All, false);
            var task = Assert.IsType<SwShapeTask>(data);
            Assert.Null(task.shape.stroke);

            var bounds = new Point[4];
            Assert.True(renderer.Bounds(data, bounds, transform));
            Assert.NotNull(task.shape.stroke);
            Assert.False(task.shape.hasStrokeRle);

            rshape.stroke.color = new RGBA(255, 0, 0, 255);
            data = renderer.Prepare(rshape, data, transform, ref clips, 255, RenderUpdateFlag.Stroke, false);
            task = Assert.IsType<SwShapeTask>(data);
            Assert.True(task.shape.hasStrokeRle);
            Assert.True(task.shape.strokeRle.Valid());

            var retainedData = (nint)task.shape.strokeRle.Data();
            var retainedSize = task.shape.strokeRle.Size();

            Assert.True(renderer.Bounds(data, bounds, transform));
            Assert.True(task.shape.hasStrokeRle);
            Assert.Equal(retainedData, (nint)task.shape.strokeRle.Data());
            Assert.Equal(retainedSize, task.shape.strokeRle.Size());

            renderer.Dispose(data);
            renderer.Sync();
            rshape.path.cmds.Dispose();
            rshape.path.pts.Dispose();
        }

        [Fact]
        public unsafe void RenderSurface_Setup_PopulatesMetadataAndPinsPixels()
        {
            var pixels = new uint[8];
            using var surface = new RenderSurface();

            surface.Setup(pixels, 4, 3, 2, 4, ColorSpace.ARGB8888, true);

            Assert.Same(pixels, surface.data);
            Assert.Equal(4u, surface.stride);
            Assert.Equal(3u, surface.w);
            Assert.Equal(2u, surface.h);
            Assert.Equal(4, surface.channelSize);
            Assert.Equal(ColorSpace.ARGB8888, surface.cs);
            Assert.True(surface.premultiplied);
            Assert.True(surface.alphaIgnored);

            var pointer = (nint)surface.buf32;
            Assert.NotEqual(nint.Zero, pointer);
            surface.buf32[5] = 0x12345678;
            Assert.Equal(0x12345678u, pixels[5]);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.Equal(pointer, (nint)surface.buf32);
            Assert.Equal(0x12345678u, surface.buf32[5]);

            surface.Dispose();
            Assert.Equal(nint.Zero, (nint)surface.buf32);
        }
    }
}
