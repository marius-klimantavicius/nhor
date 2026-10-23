using System;
using System.Linq;
using Xunit;

namespace ThorVG.Tests
{
    public class tvgMotionBlurTests
    {
        [Theory]
        [InlineData(0, 1, 15, 0)]
        [InlineData(90, 1, 0, 15)]
        [InlineData(180, 1, 15, 0)]
        [InlineData(-90, 2, 0, 30)]
        public void Transform_UsesLocalVectorWithoutTranslation(float angle, float scale, float width, float height)
        {
            var effect = RenderEffectMotionBlur.Gen(15, angle, 100);
            var matrix = TvgMath.Identity(); matrix.e11 = matrix.e22 = scale; matrix.e13 = 300; matrix.e23 = -200;
            Assert.True(effect.Update(matrix, out var offset, out int samples));
            Assert.Equal(width, MathF.Abs(offset.x), 4); Assert.Equal(height, MathF.Abs(offset.y), 4);
            Assert.True(samples >= 3 && samples <= 257 && (samples & 1) == 1);
            Assert.False(RenderEffectMotionBlur.Gen(-1, 0, 100).Update(matrix, out _, out _));
        }

        [Fact]
        public void SceneDuplicate_ZeroAndPositiveBlurRenderThroughPublicApi()
        {
            Initializer.Init();
            try
            {
                uint[] Render(float distance, bool duplicate)
                {
                    var shape = Shape.Gen(); shape.AppendRect(20, 20, 12, 12); shape.SetFill(240, 80, 20, 180);
                    var scene = Scene.Gen(); scene.Add(shape);
                    Assert.Equal(Result.Success, scene.AddEffect(SceneEffect.MotionBlur, distance, 0, 100));
                    var paint = duplicate ? scene.Duplicate() : scene;
                    if (duplicate) Paint.Rel(scene);
                    var pixels = new uint[64 * 64]; using var canvas = SwCanvas.Gen(EngineOption.None)!;
                    canvas.Target(pixels, 64, 64, 64, ColorSpace.ABGR8888); canvas.Add(paint);
                    Assert.Equal(Result.Success, canvas.Draw(true)); Assert.Equal(Result.Success, canvas.Sync()); canvas.Remove();
                    return pixels;
                }
                var plain = Render(0, false); var blurred = Render(12, false);
                Assert.Equal(0u, plain[24 * 64 + 16]);
                Assert.NotEqual(0u, blurred[24 * 64 + 16]);
                Assert.Equal(blurred, Render(12, true));
                Assert.True(blurred.Count(c => c != 0) > plain.Count(c => c != 0));
            }
            finally { Initializer.Term(); }
        }
    }
}
