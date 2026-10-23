// Tests for LottieAnimation (thorvg_lottie.cs)

using Xunit;

namespace ThorVG.Tests
{
    public class LottieAnimationTests
    {
        // ---- Gen ----

        [Fact]
        public void Gen_CreatesInstance()
        {
            using var anim = LottieAnimation.Gen();
            Assert.NotNull(anim);
        }

        [Fact]
        public void Gen_HasPicture()
        {
            using var anim = LottieAnimation.Gen();
            Assert.NotNull(anim.GetPicture());
        }

        // ---- MarkersCnt without loaded animation ----

        [Fact]
        public void MarkersCnt_NoLoader_ReturnsZero()
        {
            using var anim = LottieAnimation.Gen();
            Assert.Equal(0u, anim.MarkersCnt());
        }

        // ---- Marker without loaded animation ----

        [Fact]
        public void Marker_NoLoader_ReturnsNull()
        {
            using var anim = LottieAnimation.Gen();
            Assert.Null(anim.Marker(0, out _, out _));
        }

        // ---- Segment without loaded animation ----

        [Fact]
        public void Segment_NoLoader_ReturnsInsufficientCondition()
        {
            using var anim = LottieAnimation.Gen();
            Assert.Equal(Result.InsufficientCondition, anim.Segment("test"));
        }

        // ---- Tween without loaded animation ----

        [Fact]
        public void Tween_NoLoader_ReturnsInsufficientCondition()
        {
            using var anim = LottieAnimation.Gen();
            Assert.Equal(Result.InsufficientCondition, anim.Tween(0f, 30f, 0.5f));
        }

        // ---- Quality ----

        [Fact]
        public void Quality_ValidValue_ReturnsSuccess()
        {
            using var anim = LottieAnimation.Gen();
            // Even without loader, quality check for value > 100 happens first
            Assert.Equal(Result.InsufficientCondition, anim.Quality(50));
        }

        [Fact]
        public void Quality_InvalidValue_ReturnsInvalidArguments()
        {
            using var anim = LottieAnimation.Gen();
            Assert.Equal(Result.InvalidArguments, anim.Quality(101));
        }
    }
}
