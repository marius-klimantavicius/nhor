using Xunit;

namespace ThorVG.Tests
{
    public class tvgAnimationTests
    {
        [Fact]
        public void Animation_Gen_ReturnsInstance()
        {
            using var anim = Animation.Gen();
            Assert.NotNull(anim);
        }

        [Fact]
        public void Animation_GetPicture_ReturnsNonNull()
        {
            using var anim = Animation.Gen();
            Assert.NotNull(anim.GetPicture());
            Assert.Equal(Type.Picture, anim.GetPicture().PaintType());
        }

        [Fact]
        public void Animation_Frame_ReturnsNonSupportOrInsufficient()
        {
            using var anim = Animation.Gen();
            // No loader, so should return InsufficientCondition
            var result = anim.Frame(0);
            Assert.True(result == Result.InsufficientCondition || result == Result.NonSupport);
        }

        [Fact]
        public void Animation_CurFrame_DefaultZero()
        {
            using var anim = Animation.Gen();
            Assert.Equal(0, anim.CurFrame());
        }

        [Fact]
        public void Animation_TotalFrame_DefaultZero()
        {
            using var anim = Animation.Gen();
            Assert.Equal(0, anim.TotalFrame());
        }

        [Fact]
        public void Animation_Duration_DefaultZero()
        {
            using var anim = Animation.Gen();
            Assert.Equal(0, anim.Duration());
        }

        [Fact]
        public void Animation_Segment_Set_InsufficientOrNonSupport()
        {
            using var anim = Animation.Gen();
            var result = anim.Segment(0.0f, 1.0f);
            Assert.True(result == Result.InsufficientCondition || result == Result.NonSupport);
        }

        [Fact]
        public void Animation_Segment_Get()
        {
            using var anim = Animation.Gen();
            var result = anim.Segment(out float begin, out float end);
            // Should return insufficient condition since no loader
            Assert.True(result == Result.InsufficientCondition || result == Result.NonSupport);
        }
    }
}
