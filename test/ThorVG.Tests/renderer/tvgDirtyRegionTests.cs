using System;
using Xunit;

namespace ThorVG.Tests
{
    public class tvgDirtyRegionTests
    {
        [Fact]
        public void DestroyedSceneDamagesItsExplicitlyRetainedChildPaints()
        {
            Assert.Equal(Result.Success, Initializer.Init(2));
            var canvas = SwCanvas.Gen(EngineOption.SmartRender)!;
            var shape = Shape.Gen();
            shape.Ref();
            try
            {
                var pixels = new uint[32 * 32];
                Assert.Equal(Result.Success, canvas.Target(pixels, 32, 32, 32, ColorSpace.ABGR8888));
                var background = Shape.Gen();
                background.AppendRect(0, 0, 32, 32);
                background.SetFill(255, 255, 255);
                canvas.Add(background);
                var scene = Scene.Gen();
                scene.Translate(5, 3);
                shape.AppendRect(4, 6, 12, 10);
                shape.SetFill(255, 0, 0);
                scene.Add(shape);
                canvas.Add(scene);
                Draw();
                Assert.Equal(0xff0000ffu, pixels[10 * 32 + 10]);
                Assert.Equal(Result.Success, canvas.Remove(scene));
                Assert.Empty(scene.Paints());
                Assert.Equal((ushort)1, shape.RefCnt());
                Assert.Null(shape.Parent());
                Assert.Equal(Result.Success, canvas.Update());
                Assert.Equal(Result.Success, canvas.Draw(false));
                Assert.True(canvas.DirtyRegions() > 0);
                Assert.Equal(Result.Success, canvas.Sync());
                Assert.All(pixels, pixel => Assert.Equal(0xffffffffu, pixel));
                Assert.Equal(Result.Success, canvas.Add(shape));
                Draw();
                Assert.Equal(0xff0000ffu, pixels[7 * 32 + 5]);

                void Draw()
                {
                    Assert.Equal(Result.Success, canvas.Update());
                    Assert.Equal(Result.Success, canvas.Draw(false));
                    Assert.Equal(Result.Success, canvas.Sync());
                }
            }
            finally
            {
                canvas.Dispose();
                shape.Unref();
                Assert.Equal(Result.Success, Initializer.Term());
            }
        }

        [Theory]
        [InlineData(EngineOption.SmartRender)]
        [InlineData(EngineOption.None)]
        public void DirtyRegions_FullFrameAndQueryWindow(EngineOption option)
        {
            Initializer.Init();
            try
            {
                using var canvas = SwCanvas.Gen(option);
                Assert.Equal(Result.Success, canvas.Target(new uint[64 * 64], 64, 64, 64, ColorSpace.ABGR8888));
                Assert.Equal(0u, canvas.DirtyRegions());
                var regions = new Region[2];
                Assert.Equal(Result.InsufficientCondition, canvas.DirtyRegions(regions));
                Assert.Equal(Result.InvalidArguments, canvas.DirtyRegions(Span<Region>.Empty));
                Assert.Equal(Result.InvalidArguments, canvas.DirtyRegions(null, 1));
                Assert.Equal(Result.InvalidArguments, canvas.DirtyRegions(regions, 3));
                var shape = Shape.Gen();
                shape.AppendRect(4, 4, 8, 8, 0, 0);
                shape.SetFill(255, 0, 0, 255);
                canvas.Add(shape);
                Assert.Equal(Result.Success, canvas.Draw());
                Assert.Equal(1u, canvas.DirtyRegions());
                Assert.Equal(Result.Success, canvas.DirtyRegions(regions));
                Assert.Equal(0, regions[0].xMin);
                Assert.Equal(0, regions[0].yMin);
                Assert.Equal(64, regions[0].xMax);
                Assert.Equal(64, regions[0].yMax);
                canvas.Sync();
                Assert.Equal(0u, canvas.DirtyRegions());
                Assert.Equal(Result.InsufficientCondition, canvas.DirtyRegions(regions));
                canvas.Remove();
            }
            finally { Initializer.Term(); }
        }

        [Fact]
        public void SmartRender_MovedShapeClearsPreviousPixels()
        {
            Initializer.Init();
            try
            {
                using var canvas = SwCanvas.Gen(EngineOption.SmartRender);
                var pixels = new uint[64 * 64];
                canvas.Target(pixels, 64, 64, 64, ColorSpace.ABGR8888);
                var shape = Shape.Gen();
                shape.AppendRect(4, 4, 8, 8, 0, 0);
                shape.SetFill(255, 0, 0, 255);
                canvas.Add(shape);
                Assert.Equal(Result.Success, canvas.Draw());
                canvas.Sync();
                Assert.Equal(0xff0000ffu, pixels[8 * 64 + 8]);
                shape.Translate(20, 0);
                Assert.Equal(Result.Success, canvas.Update());
                Assert.Equal(Result.Success, canvas.Draw());
                Assert.Equal(0u, pixels[8 * 64 + 8]);
                Assert.Equal(0xff0000ffu, pixels[8 * 64 + 28]);
                Assert.True(canvas.DirtyRegions() > 0);
                var regions = new Region[canvas.DirtyRegions()];
                Assert.Equal(Result.Success, canvas.DirtyRegions(regions));
                Assert.Contains(regions, r => r.xMin <= 4 && r.xMax >= 12 && r.yMin <= 4 && r.yMax >= 12);
                Assert.Contains(regions, r => r.xMin <= 24 && r.xMax >= 32 && r.yMin <= 4 && r.yMax >= 12);
                var limited = new Region[1];
                Assert.Equal(Result.Success, canvas.DirtyRegions(limited));
                Assert.Equal(regions[0].xMin, limited[0].xMin);
                canvas.Sync();
                canvas.Remove();
            }
            finally { Initializer.Term(); }
        }
    }
}
