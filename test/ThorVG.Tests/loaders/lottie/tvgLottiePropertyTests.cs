// Tests for Lottie property types

using Xunit;

namespace ThorVG.Tests
{
    public class LottiePropertyTests
    {
        // ---- LottieFloat ----

        [Fact]
        public void LottieFloat_StaticValue()
        {
            var prop = new LottieFloat();
            prop.value = 42.0f;
            // Without keyframes, Evaluate should return the static value
            var result = prop.Evaluate(0f);
            Assert.Equal(42.0f, result);
        }

        [Fact]
        public void LottieFloat_DefaultValue()
        {
            var prop = new LottieFloat();
            Assert.Equal(0f, prop.value);
        }

        [Fact]
        public void LottieFloat_NewFrame_CreatesFrame()
        {
            var prop = new LottieFloat();
            var frame = prop.NewFrame();
            Assert.NotNull(frame);
            Assert.NotNull(prop.frames);
            Assert.Single(prop.frames);
        }

        [Fact]
        public void LottieFloat_NextFrame_SetsNextReady()
        {
            var prop = new LottieFloat();
            var frame = prop.NextFrame();
            Assert.NotNull(frame);
            // After NextFrame, NewFrame should return the same last frame
            var frame2 = prop.NewFrame();
            Assert.Same(frame, frame2);
        }

        // ---- LottieInteger ----

        [Fact]
        public void LottieInteger_StaticValue()
        {
            var prop = new LottieInteger();
            prop.value = 7;
            Assert.Equal(7, prop.Evaluate(0f));
        }

        // ---- LottieScalar ----

        [Fact]
        public void LottieScalar_StaticValue()
        {
            var prop = new LottieScalar();
            prop.value = new Point(3.14f, 2.72f);
            var result = prop.Evaluate(0f);
            Assert.Equal(3.14f, result.x);
            Assert.Equal(2.72f, result.y);
        }

        // ---- LottieVector ----

        [Fact]
        public void LottieVector_StaticValue()
        {
            var prop = new LottieVector();
            prop.value = new Point(10f, 20f);
            var result = prop.Evaluate(0f);
            Assert.Equal(10f, result.x);
            Assert.Equal(20f, result.y);
        }

        [Fact]
        public void LottieVector_DefaultValue()
        {
            var prop = new LottieVector();
            Assert.Equal(0f, prop.value.x);
            Assert.Equal(0f, prop.value.y);
        }

        // ---- LottieColor ----

        [Fact]
        public void LottieColor_StaticValue()
        {
            var prop = new LottieColor();
            prop.value = new RGB32 { r = 255, g = 128, b = 64 };
            var result = prop.Evaluate(0f);
            Assert.Equal(255, result.r);
            Assert.Equal(128, result.g);
            Assert.Equal(64, result.b);
        }

        // ---- LottieOpacity ----

        [Fact]
        public void LottieOpacity_StaticValue()
        {
            var prop = new LottieOpacity();
            prop.value = 200;
            Assert.Equal(200, prop.Evaluate(0f));
        }

        [Fact]
        public void LottieOpacity_DefaultValue()
        {
            var prop = new LottieOpacity();
            // Default opacity is 255 (fully opaque)
            Assert.Equal(255, prop.value);
        }

        // ---- LottiePathSet ----

        [Fact]
        public void LottiePathSet_DefaultValue()
        {
            var prop = new LottiePathSet();
            Assert.Null(prop.value.pts);
            Assert.Null(prop.value.cmds);
        }

        [Fact]
        public void LottiePathSet_NewFrame_CreatesFrame()
        {
            var prop = new LottiePathSet();
            var frame = prop.NewFrame();
            Assert.NotNull(frame);
        }

        // ---- LottieTextDoc ----

        [Fact]
        public void LottieTextDoc_DefaultValue()
        {
            var prop = new LottieTextDoc();
            Assert.Null(prop.value.text);
        }

        [Fact]
        public void LottieTextDoc_NewFrame_CreatesFrame()
        {
            var prop = new LottieTextDoc();
            var frame = prop.NewFrame();
            Assert.NotNull(frame);
        }
    }
}
