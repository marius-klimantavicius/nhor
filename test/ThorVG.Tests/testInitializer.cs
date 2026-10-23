// Ported from ThorVG/test/testInitializer.cpp

using System;
using System.IO;
using Xunit;

namespace ThorVG.Tests
{
    public class testInitializer
    {
        private static readonly string TEST_DIR = System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", "legacy-upstream", "resources");

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

        [Fact]
        public void NegativeTermination()
        {
            // xunit.runner.json serializes collections. Restore the surrounding
            // reference state after testing the uninitialized public contract.
            int previous = TvgCommon.engineInit;
            uint threads = TaskScheduler.Threads();
            for (int i = 0; i < previous; ++i) Assert.Equal(Result.Success, Initializer.Term());
            try { Assert.Equal(Result.InsufficientCondition, Initializer.Term()); }
            finally { for (int i = 0; i < previous; ++i) Initializer.Init(threads); }
        }
    }
}
