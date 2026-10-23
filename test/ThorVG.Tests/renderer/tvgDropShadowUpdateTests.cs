using Xunit;

namespace ThorVG.Tests
{
    public class tvgDropShadowUpdateTests
    {
        [Fact]
        public void OffsetUsesTheFullAffineDirectionWithoutTranslation()
        {
            var effect = RenderEffectDropShadow.Gen(0, 0, 0, 255, 90, 10, 2, 50);
            var matrix = new Matrix { e11 = 2, e12 = 1, e13 = 100, e21 = 0.5f, e22 = 3, e23 = 200, e33 = 1 };
            Assert.True(effect.Update(matrix, out var offset));
            Assert.Equal(20, offset.x); Assert.Equal(5, offset.y);
            effect.angle = 180;
            Assert.True(effect.Update(matrix, out offset));
            Assert.Equal(10, offset.x, 4); Assert.Equal(30, offset.y, 4);
            effect.color[3] = 0;
            Assert.False(effect.Update(matrix, out _));
            Assert.False(effect.valid);
        }
    }
}
