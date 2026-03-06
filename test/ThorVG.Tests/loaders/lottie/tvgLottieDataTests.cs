// Tests for Lottie data types and interpolation

using Xunit;

namespace ThorVG.Tests
{
    public class LottieDataTests
    {
        // ---- RGB32 ----

        [Fact]
        public void RGB32_DefaultIsZero()
        {
            var c = new RGB32();
            Assert.Equal(0, c.r);
            Assert.Equal(0, c.g);
            Assert.Equal(0, c.b);
        }

        [Fact]
        public void RGB32_FieldAssignment()
        {
            var c = new RGB32 { r = 255, g = 128, b = 64 };
            Assert.Equal(255, c.r);
            Assert.Equal(128, c.g);
            Assert.Equal(64, c.b);
        }

        // ---- TextDocument ----

        [Fact]
        public void TextDocument_DefaultFields()
        {
            var doc = new TextDocument();
            Assert.Null(doc.text);
            Assert.Equal(0f, doc.size);
            Assert.Null(doc.name);
        }

        [Fact]
        public void TextDocument_FieldAssignment()
        {
            var doc = new TextDocument
            {
                text = "Hello",
                size = 24.0f,
                name = "Arial"
            };
            Assert.Equal("Hello", doc.text);
            Assert.Equal(24.0f, doc.size);
            Assert.Equal("Arial", doc.name);
        }

        // ---- LottieInterpolator ----

        [Fact]
        public void LottieInterpolator_LinearProgress()
        {
            // Linear: control points at (0,0) and (1,1) => outTangent, inTangent
            var interp = new LottieInterpolator();
            interp.Set(null, new Point(1f, 1f), new Point(0f, 0f));
            // For linear: progress(0) ~ 0, progress(1) ~ 1
            Assert.InRange(interp.Progress(0f), -0.01f, 0.01f);
            Assert.InRange(interp.Progress(1f), 0.99f, 1.01f);
        }

        [Fact]
        public void LottieInterpolator_MidpointProgress()
        {
            var interp = new LottieInterpolator();
            interp.Set(null, new Point(1f, 1f), new Point(0f, 0f));
            // At t=0.5, linear should give ~0.5
            var mid = interp.Progress(0.5f);
            Assert.InRange(mid, 0.4f, 0.6f);
        }

        [Fact]
        public void LottieInterpolator_EaseIn()
        {
            var interp = new LottieInterpolator();
            interp.Set(null, new Point(1f, 1f), new Point(0.42f, 0f));
            var mid = interp.Progress(0.5f);
            // Ease-in: progress at t=0.5 should be less than 0.5
            Assert.InRange(mid, 0.0f, 0.55f);
        }

        // ---- Tween struct ----

        [Fact]
        public void Tween_DefaultInactive()
        {
            var t = new Tween();
            Assert.False(t.active);
            Assert.Equal(0f, t.frameNo);
            Assert.Equal(0f, t.progress);
        }

        // ---- PathSet ----

        [Fact]
        public void PathSet_DefaultFields()
        {
            var ps = new PathSet();
            Assert.Null(ps.pts);
            Assert.Null(ps.cmds);
            Assert.Equal((ushort)0, ps.ptsCnt);
            Assert.Equal((ushort)0, ps.cmdsCnt);
        }
    }
}
