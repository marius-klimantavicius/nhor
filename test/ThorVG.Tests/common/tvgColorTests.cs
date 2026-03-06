using Xunit;

namespace ThorVG.Tests
{
    public class tvgColorTests
    {
        [Fact]
        public void Hsl2Rgb_ZeroSaturation_ReturnsGray()
        {
            TvgColor.Hsl2Rgb(0, 0, 0.5f, out byte r, out byte g, out byte b);
            Assert.Equal(r, g);
            Assert.Equal(g, b);
            Assert.Equal(128, r); // round(0.5 * 255)
        }

        [Fact]
        public void Hsl2Rgb_Red()
        {
            // Pure red: H=0, S=1, L=0.5
            TvgColor.Hsl2Rgb(0, 1.0f, 0.5f, out byte r, out byte g, out byte b);
            Assert.Equal(255, r);
            Assert.Equal(0, g);
            Assert.Equal(0, b);
        }

        [Fact]
        public void Hsl2Rgb_Green()
        {
            // Pure green: H=120, S=1, L=0.5
            TvgColor.Hsl2Rgb(120, 1.0f, 0.5f, out byte r, out byte g, out byte b);
            Assert.Equal(0, r);
            Assert.Equal(255, g);
            Assert.Equal(0, b);
        }

        [Fact]
        public void Hsl2Rgb_Blue()
        {
            // Pure blue: H=240, S=1, L=0.5
            TvgColor.Hsl2Rgb(240, 1.0f, 0.5f, out byte r, out byte g, out byte b);
            Assert.Equal(0, r);
            Assert.Equal(0, g);
            Assert.Equal(255, b);
        }

        [Fact]
        public void Hsl2Rgb_White()
        {
            TvgColor.Hsl2Rgb(0, 0, 1.0f, out byte r, out byte g, out byte b);
            Assert.Equal(255, r);
            Assert.Equal(255, g);
            Assert.Equal(255, b);
        }

        [Fact]
        public void Hsl2Rgb_Black()
        {
            TvgColor.Hsl2Rgb(0, 0, 0.0f, out byte r, out byte g, out byte b);
            Assert.Equal(0, r);
            Assert.Equal(0, g);
            Assert.Equal(0, b);
        }

        [Fact]
        public void Hsl2Rgb_360_WrapsToZero()
        {
            TvgColor.Hsl2Rgb(360, 1.0f, 0.5f, out byte r1, out byte g1, out byte b1);
            TvgColor.Hsl2Rgb(0, 1.0f, 0.5f, out byte r2, out byte g2, out byte b2);
            Assert.Equal(r1, r2);
            Assert.Equal(g1, g2);
            Assert.Equal(b1, b2);
        }

        [Fact]
        public void Hsl2Rgb_NegativeHue_Handled()
        {
            // -60 should be equivalent to 300
            TvgColor.Hsl2Rgb(-60, 1.0f, 0.5f, out byte r1, out byte g1, out byte b1);
            TvgColor.Hsl2Rgb(300, 1.0f, 0.5f, out byte r2, out byte g2, out byte b2);
            Assert.Equal(r1, r2);
            Assert.Equal(g1, g2);
            Assert.Equal(b1, b2);
        }

        [Fact]
        public void RGB_Struct()
        {
            var c = new RGB(10, 20, 30);
            Assert.Equal(10, c.r);
            Assert.Equal(20, c.g);
            Assert.Equal(30, c.b);
        }

        [Fact]
        public void RGBA_Struct()
        {
            var c = new RGBA(10, 20, 30, 255);
            Assert.Equal(10, c.r);
            Assert.Equal(255, c.a);
        }

        [Fact]
        public void HSL_Struct()
        {
            var c = new HSL(120, 0.5f, 0.5f);
            Assert.Equal(120f, c.h);
        }
    }
}
