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
            var anim = LottieAnimation.Gen();
            Assert.NotNull(anim);
        }

        [Fact]
        public void Gen_HasPicture()
        {
            var anim = LottieAnimation.Gen();
            Assert.NotNull(anim.GetPicture());
        }

        // ---- MarkersCnt without loaded animation ----

        [Fact]
        public void MarkersCnt_NoLoader_ReturnsZero()
        {
            var anim = LottieAnimation.Gen();
            Assert.Equal(0u, anim.MarkersCnt());
        }

        // ---- Marker without loaded animation ----

        [Fact]
        public void Marker_NoLoader_ReturnsNull()
        {
            var anim = LottieAnimation.Gen();
            Assert.Null(anim.Marker(0));
        }

        // ---- Segment without loaded animation ----

        [Fact]
        public void Segment_NoLoader_ReturnsInsufficientCondition()
        {
            var anim = LottieAnimation.Gen();
            Assert.Equal(Result.InsufficientCondition, anim.Segment("test"));
        }

        // ---- Tween without loaded animation ----

        [Fact]
        public void Tween_NoLoader_ReturnsInsufficientCondition()
        {
            var anim = LottieAnimation.Gen();
            Assert.Equal(Result.InsufficientCondition, anim.Tween(0f, 30f, 0.5f));
        }

        // ---- Quality ----

        [Fact]
        public void Quality_ValidValue_ReturnsSuccess()
        {
            var anim = LottieAnimation.Gen();
            // Even without loader, quality check for value > 100 happens first
            Assert.Equal(Result.InsufficientCondition, anim.Quality(50));
        }

        [Fact]
        public void Quality_InvalidValue_ReturnsInvalidArguments()
        {
            var anim = LottieAnimation.Gen();
            Assert.Equal(Result.InvalidArguments, anim.Quality(101));
        }
    }
}
