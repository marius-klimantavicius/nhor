// Tests for Lottie modifier types

using Xunit;

namespace ThorVG.Tests
{
    public class LottieModifierTests
    {
        // ---- LottieRoundnessModifier ----

        [Fact]
        public void LottieRoundnessModifier_Construction()
        {
            var buffer = new RenderPath();
            var modifier = new LottieRoundnessModifier(buffer, 10.0f);
            Assert.Equal(10.0f, modifier.r);
            Assert.Same(buffer, modifier.buffer);
        }

        [Fact]
        public void LottieRoundnessModifier_TypeIsRoundness()
        {
            var buffer = new RenderPath();
            var modifier = new LottieRoundnessModifier(buffer, 5.0f);
            Assert.Equal(LottieModifier.ModifierType.Roundness, modifier.type);
        }

        // ---- LottieOffsetModifier ----

        [Fact]
        public void LottieOffsetModifier_Construction()
        {
            var modifier = new LottieOffsetModifier(2.0f, 4.0f, StrokeJoin.Round);
            Assert.Equal(2.0f, modifier.offset);
            Assert.Equal(4.0f, modifier.miterLimit);
            Assert.Equal(StrokeJoin.Round, modifier.join);
        }

        // ---- LottieModifier chaining ----

        [Fact]
        public void LottieModifier_Decorate_ChainsTwoModifiers()
        {
            var buffer = new RenderPath();
            var mod1 = new LottieRoundnessModifier(buffer, 5.0f);
            var mod2 = new LottieOffsetModifier(1.0f, 4.0f, StrokeJoin.Miter);

            var chained = mod1.Decorate(mod2);
            Assert.NotNull(chained);
        }

        // ---- LottieExpressions ----

        [Fact]
        public void LottieExpressions_Instance_ReturnsSingleton()
        {
            // Real Jint-based implementation returns a singleton when thread count is 0
            var instance = LottieExpressions.Instance();
            Assert.NotNull(instance);

            // Second call returns the same instance
            var instance2 = LottieExpressions.Instance();
            Assert.Same(instance, instance2);

            // Retrieve releases the reference
            LottieExpressions.Retrieve(instance2);
            LottieExpressions.Retrieve(instance);
        }
    }
}
