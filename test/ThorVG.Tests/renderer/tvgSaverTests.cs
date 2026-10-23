using Xunit;

namespace ThorVG.Tests
{
    public class tvgSaverTests
    {
        [Fact]
        public void Saver_Gen_ReturnsInstance()
        {
            using var saver = Saver.Gen();
            Assert.NotNull(saver);
        }

        [Fact]
        public void Saver_Save_NullPaint_InvalidArgs()
        {
            using var saver = Saver.Gen();
            Assert.Equal(Result.InvalidArguments, saver.Save((Paint?)null, "test.gif"));
        }

        [Fact]
        public void Saver_Save_Paint_GifNotSupported()
        {
            // GIF saver does not support saving Paint objects (only Animation)
            using var saver = Saver.Gen();
            var shape = Shape.Gen();
            Assert.Equal(Result.Unknown, saver.Save(shape, "test.gif"));
        }

        [Fact]
        public void Saver_Save_Paint_UnknownFormat_NonSupport()
        {
            // No save module registered for unknown format
            using var saver = Saver.Gen();
            var shape = Shape.Gen();
            Assert.Equal(Result.NonSupport, saver.Save(shape, "test.xyz"));
        }

        [Fact]
        public void Saver_Save_Animation_NullInvalidArgs()
        {
            using var saver = Saver.Gen();
            Assert.Equal(Result.InvalidArguments, saver.Save((Animation?)null, "test.gif"));
        }

        [Fact]
        public void Saver_Save_Animation_InsufficientCondition()
        {
            // Animation with zero total frames
            using var saver = Saver.Gen();
            using var anim = Animation.Gen();
            Assert.Equal(Result.InsufficientCondition, saver.Save(anim, "test.gif"));
        }

        [Fact]
        public void Saver_Sync_NoModule_InsufficientCondition()
        {
            using var saver = Saver.Gen();
            Assert.Equal(Result.InsufficientCondition, saver.Sync());
        }

        [Fact]
        public void Saver_Background()
        {
            using var saver = Saver.Gen();
            var bg = Shape.Gen();
            Assert.Equal(Result.Success, saver.Background(bg));
        }

        [Fact]
        public void Saver_Background_Null_InvalidArgs()
        {
            using var saver = Saver.Gen();
            Assert.Equal(Result.InvalidArguments, saver.Background(null!));
        }
    }
}
