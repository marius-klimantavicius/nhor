using Xunit;

namespace ThorVG.Tests
{
    public class tvgInitializerTests
    {
        [Fact]
        public void Initializer_InitAndTerm()
        {
            // Reset state
            TvgCommon.engineInit = 0;

            Assert.Equal(Result.Success, Initializer.Init());
            Assert.Equal(1, TvgCommon.engineInit);

            // Double init should succeed
            Assert.Equal(Result.Success, Initializer.Init());
            Assert.Equal(2, TvgCommon.engineInit);

            // Term once
            Assert.Equal(Result.Success, Initializer.Term());
            Assert.Equal(1, TvgCommon.engineInit);

            // Term again to fully terminate
            Assert.Equal(Result.Success, Initializer.Term());
            Assert.Equal(0, TvgCommon.engineInit);
        }

        [Fact]
        public void Initializer_Term_WithoutInit_Fails()
        {
            TvgCommon.engineInit = 0;
            Assert.Equal(Result.InsufficientCondition, Initializer.Term());
        }

        [Fact]
        public void Initializer_Version_String()
        {
            var ver = Initializer.Version();
            Assert.NotNull(ver);
            Assert.Contains(".", ver);
        }

        [Fact]
        public void Initializer_Version_Components()
        {
            var ver = Initializer.Version(out uint major, out uint minor, out uint micro);
            Assert.NotNull(ver);
            Assert.Equal(0u, major);
            Assert.Equal(15u, minor);
            Assert.Equal(7u, micro);
        }

        [Fact]
        public void Initializer_VersionString_Constant()
        {
            Assert.Equal("0.15.7", Initializer.VersionString);
        }
    }
}
