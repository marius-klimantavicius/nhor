// Ported from ThorVG/test/testSavers.cpp

using System;
using System.IO;
using Xunit;

namespace ThorVG.Tests
{
    public class testSavers
    {
        private static readonly string TEST_DIR = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ref", "ThorVG", "test", "resources"));

        [Fact]
        public void SaverCreation()
        {
            var saver = Saver.Gen();
            Assert.NotNull(saver);
        }

        [Fact]
        public void SaveLottieIntoGif()
        {
            Assert.Equal(Result.Success, Initializer.Init());
            try
            {
                var animation = Animation.Gen();
                var picture = animation.GetPicture();
                Assert.Equal(Result.Success, picture.Load(Path.Combine(TEST_DIR, "test.lot")));
                Assert.Equal(Result.Success, picture.SetSize(100, 100));

                var bg = Shape.Gen();
                Assert.Equal(Result.Success, bg.SetFill(255, 255, 255));
                Assert.Equal(Result.Success, bg.AppendRect(0, 0, 100, 100));

                var saver = Saver.Gen();
                Assert.NotNull(saver);

                Assert.Equal(Result.Success, saver.Background(bg));
                Assert.Equal(Result.Success, saver.Save(animation, Path.Combine(TEST_DIR, "test.gif")));
                Assert.Equal(Result.Success, saver.Sync());
            }
            finally
            {
                try { File.Delete(Path.Combine(TEST_DIR, "test.gif")); } catch { }
                Initializer.Term();
            }
        }
    }
}
