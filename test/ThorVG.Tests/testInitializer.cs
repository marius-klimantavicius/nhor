// Ported from ThorVG/test/testInitializer.cpp

using System;
using System.IO;
using Xunit;

namespace ThorVG.Tests
{
    public class testInitializer
    {
        private static readonly string TEST_DIR = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ThorVG", "test", "resources"));

        [Fact]
        public void BasicInitialization()
        {
            Assert.Equal(Result.Success, Initializer.Init());
            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void MultipleInitialization()
        {
            Assert.Equal(Result.Success, Initializer.Init());
            Assert.Equal(Result.Success, Initializer.Init());
            Assert.Equal(Result.Success, Initializer.Term());

            Assert.Equal(Result.Success, Initializer.Init());
            Assert.Equal(Result.Success, Initializer.Term());
            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void Version()
        {
            var versionStr = Initializer.Version();
            Assert.Equal(Initializer.VersionString, versionStr);

            var versionStr2 = Initializer.Version(out var major, out var minor, out var micro);
            Assert.Equal(Initializer.VersionString, versionStr2);

            var curVersion = $"{major}.{minor}.{micro}";
            Assert.Equal(Initializer.VersionString, curVersion);
        }

        [Fact(Skip = "Cannot reliably test in parallel xUnit runner — shared static Init/Term state")]
        public void NegativeTermination()
        {
            Assert.Equal(Result.InsufficientCondition, Initializer.Term());
        }
    }
}
