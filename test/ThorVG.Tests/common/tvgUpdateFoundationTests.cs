using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ThorVG.Tests
{
    public class tvgUpdateFoundationTests
    {
        [Theory]
        [InlineData(0.0f, 0)]
        [InlineData(0.5f, 128)]
        [InlineData(1.0f, 255)]
        public void Remap255_UsesNearestEven(float value, int expected)
        {
            Assert.Equal((byte)expected, TvgMath.Remap255(value));
            Assert.Equal(expected, TvgMath.Remap255<int>(value));
        }

        [Fact]
        public void Remap255_HalfwayAndWideValues()
        {
            Assert.Equal(2, TvgMath.Remap255<int>(2.5f / 255.0f));
            Assert.Equal(4, TvgMath.Remap255<int>(3.5f / 255.0f));
            Assert.Equal(-255, TvgMath.Remap255<int>(-1.0f));
            Assert.Equal(510, TvgMath.Remap255<int>(2.0f));
        }

        [Fact]
        public void ColorStopLerp_TruncatesAndClampsChannels()
        {
            var start = new Fill.ColorStop(0.25f, 0, 255, 50, 1);
            var end = new Fill.ColorStop(0.75f, 255, 0, 51, 254);
            var middle = TvgMath.Lerp(start, end, 0.5f);
            Assert.Equal(0.5f, middle.offset);
            Assert.Equal((byte)127, middle.r);
            Assert.Equal((byte)127, middle.g);
            Assert.Equal((byte)50, middle.b);
            Assert.Equal((byte)127, middle.a);
            var outside = TvgMath.Lerp(start, end, 2.0f);
            Assert.Equal((byte)255, outside.r);
            Assert.Equal((byte)0, outside.g);
        }

        [Fact]
        public void StrictKey_SerializesWithoutWorkerThreads()
        {
            var previous = ScopedLock.ThreadingEnabled;
            ScopedLock.ThreadingEnabled = false;
            try
            {
                var key = new StrictKey();
                int value = 0;
                Parallel.For(0, 8, _ =>
                {
                    for (int n = 0; n < 1000; n++)
                    {
                        using var held = new ScopedLock(key);
                        int current = value;
                        Thread.SpinWait(20);
                        value = current + 1;
                    }
                });
                Assert.Equal(8000, value);
            }
            finally { ScopedLock.ThreadingEnabled = previous; }
        }

        [Fact]
        public void Initializer_ConcurrentReferenceCounting()
        {
            int previous = TvgCommon.engineInit;
            Parallel.For(0, 128, _ => Assert.Equal(Result.Success, Initializer.Init()));
            Assert.Equal(previous + 128, TvgCommon.engineInit);
            Parallel.For(0, 128, _ => Assert.Equal(Result.Success, Initializer.Term()));
            Assert.Equal(previous, TvgCommon.engineInit);
        }

        [Fact]
        public void BBox_InsideIncludesEdges()
        {
            var box = new BBox { min = new Point(-2, -4), max = new Point(6, 8) };
            Assert.True(box.Inside(new Point(-2, 8)));
            Assert.True(box.Inside(new Point(6, -4)));
            Assert.False(box.Inside(new Point(6.001f, 0)));
        }
    }
}
