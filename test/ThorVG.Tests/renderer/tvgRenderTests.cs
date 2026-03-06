using Xunit;

namespace ThorVG.Tests
{
    public class tvgRenderTests
    {
        // ---- RenderRegion --------------------------------------------------

        [Fact]
        public void RenderRegion_Constructor()
        {
            var r = new RenderRegion(10, 20, 110, 70);
            Assert.Equal(10, r.min.x);
            Assert.Equal(20, r.min.y);
            Assert.Equal(110, r.max.x);
            Assert.Equal(70, r.max.y);
        }

        [Fact]
        public void RenderRegion_Valid_And_Invalid()
        {
            var valid = new RenderRegion(0, 0, 100, 100);
            Assert.True(valid.Valid());
            Assert.False(valid.Invalid());

            var invalid = new RenderRegion(0, 0, 0, 0);
            Assert.False(invalid.Valid());
            Assert.True(invalid.Invalid());
        }

        [Fact]
        public void RenderRegion_Intersect()
        {
            var a = new RenderRegion(0, 0, 100, 100);
            var b = new RenderRegion(50, 50, 150, 150);
            var r = RenderRegion.Intersect(a, b);
            Assert.Equal(50, r.min.x);
            Assert.Equal(50, r.min.y);
            Assert.Equal(100, r.max.x);
            Assert.Equal(100, r.max.y);
        }

        [Fact]
        public void RenderRegion_Add()
        {
            var a = new RenderRegion(10, 20, 100, 100);
            var b = new RenderRegion(0, 0, 50, 50);
            var r = RenderRegion.Add(a, b);
            Assert.Equal(0, r.min.x);
            Assert.Equal(0, r.min.y);
            Assert.Equal(100, r.max.x);
            Assert.Equal(100, r.max.y);
        }

        [Fact]
        public void RenderRegion_Contained()
        {
            var outer = new RenderRegion(0, 0, 200, 200);
            var inner = new RenderRegion(50, 50, 100, 100);
            Assert.True(outer.Contained(inner));
            Assert.False(inner.Contained(outer));
        }

        [Fact]
        public void RenderRegion_Intersected()
        {
            var a = new RenderRegion(0, 0, 100, 100);
            var b = new RenderRegion(50, 50, 150, 150);
            Assert.True(a.Intersected(b));

            var c = new RenderRegion(200, 200, 300, 300);
            Assert.False(a.Intersected(c));
        }

        [Fact]
        public void RenderRegion_Equality()
        {
            var a = new RenderRegion(1, 2, 3, 4);
            var b = new RenderRegion(1, 2, 3, 4);
            var c = new RenderRegion(0, 0, 5, 5);
            Assert.True(a == b);
            Assert.False(a != b);
            Assert.True(a != c);
        }

        [Fact]
        public void RenderRegion_SizeAccessors()
        {
            var r = new RenderRegion(10, 20, 110, 120);
            Assert.Equal(10, r.Sx());
            Assert.Equal(20, r.Sy());
            Assert.Equal(100, r.Sw());
            Assert.Equal(100, r.Sh());
        }

        [Fact]
        public void RenderRegion_Reset()
        {
            var r = new RenderRegion(10, 20, 110, 120);
            r.Reset();
            Assert.Equal(0, r.min.x);
            Assert.Equal(0, r.max.x);
        }

        [Fact]
        public void RenderRegion_IntersectWith()
        {
            var a = new RenderRegion(0, 0, 100, 100);
            var b = new RenderRegion(50, 50, 150, 150);
            a.IntersectWith(b);
            Assert.Equal(50, a.min.x);
            Assert.Equal(100, a.max.x);
        }

        [Fact]
        public void RenderRegion_AddWith()
        {
            var a = new RenderRegion(10, 10, 50, 50);
            var b = new RenderRegion(0, 0, 100, 100);
            a.AddWith(b);
            Assert.Equal(0, a.min.x);
            Assert.Equal(100, a.max.x);
        }

        // ---- RenderSurface -------------------------------------------------

        [Fact]
        public void RenderSurface_CopyConstructor()
        {
            var orig = new RenderSurface { w = 800, h = 600, cs = ColorSpace.ARGB8888, stride = 800, premultiplied = true };
            var copy = new RenderSurface(orig);
            Assert.Equal(800u, copy.w);
            Assert.Equal(600u, copy.h);
            Assert.Equal(ColorSpace.ARGB8888, copy.cs);
            Assert.True(copy.premultiplied);
        }

        // ---- RenderPath ----------------------------------------------------

        [Fact]
        public unsafe void RenderPath_Empty()
        {
            var path = new RenderPath();
            Assert.True(path.Empty());
        }

        [Fact]
        public unsafe void RenderPath_MoveToLineTo()
        {
            var path = new RenderPath();
            path.MoveTo(new Point(0, 0));
            path.LineTo(new Point(100, 100));
            Assert.False(path.Empty());
            Assert.Equal(2u, path.pts.count);
            Assert.Equal(2u, path.cmds.count);
            path.cmds.Dispose();
            path.pts.Dispose();
        }

        [Fact]
        public unsafe void RenderPath_Close_Dedup()
        {
            var path = new RenderPath();
            path.MoveTo(new Point(0, 0));
            path.Close();
            path.Close(); // Should not add another Close
            Assert.Equal(2u, path.cmds.count);
            path.cmds.Dispose();
            path.pts.Dispose();
        }

        [Fact]
        public unsafe void RenderPath_Clear()
        {
            var path = new RenderPath();
            path.MoveTo(new Point(0, 0));
            path.LineTo(new Point(100, 100));
            path.Clear();
            Assert.True(path.Empty());
            path.cmds.Dispose();
            path.pts.Dispose();
        }

        // ---- RenderTrimPath ------------------------------------------------

        [Fact]
        public void RenderTrimPath_DefaultValid()
        {
            var trim = new RenderTrimPath();
            Assert.False(trim.Valid()); // default 0..1 is not "valid" (no trim)
        }

        [Fact]
        public void RenderTrimPath_NonDefaultIsValid()
        {
            var trim = new RenderTrimPath { begin = 0.2f, end = 0.8f };
            Assert.True(trim.Valid());
        }

        // ---- RenderShape ---------------------------------------------------

        [Fact]
        public void RenderShape_FillColor()
        {
            var rs = new RenderShape();
            rs.color = new RGBA(128, 64, 32, 200);
            rs.FillColor(out byte r, out byte g, out byte b, out byte a);
            Assert.Equal(128, r);
            Assert.Equal(64, g);
            Assert.Equal(32, b);
            Assert.Equal(200, a);
        }

        [Fact]
        public void RenderShape_Trimpath()
        {
            var rs = new RenderShape();
            Assert.False(rs.Trimpath()); // no stroke

            rs.stroke = new RenderStroke();
            Assert.False(rs.Trimpath()); // stroke exists but trim is default

            rs.stroke.trim = new RenderTrimPath { begin = 0.1f, end = 0.9f };
            Assert.True(rs.Trimpath());
        }

        [Fact]
        public void RenderShape_StrokeWidth_NoStroke_ReturnsZero()
        {
            var rs = new RenderShape();
            Assert.Equal(0.0f, rs.StrokeWidth());
        }

        [Fact]
        public void RenderShape_StrokeFirst()
        {
            var rs = new RenderShape();
            Assert.False(rs.StrokeFirst());
            rs.stroke = new RenderStroke { first = true };
            Assert.True(rs.StrokeFirst());
        }

        // ---- RenderEffects -------------------------------------------------

        [Fact]
        public void RenderEffectGaussianBlur_Gen()
        {
            var fx = RenderEffectGaussianBlur.Gen(5.0f, 0, 0, 50);
            Assert.Equal(SceneEffect.GaussianBlur, fx.type);
            Assert.Equal(5.0f, fx.sigma);
        }

        [Fact]
        public void RenderEffectGaussianBlur_NegativeSigma_ClampedToZero()
        {
            var fx = RenderEffectGaussianBlur.Gen(-3.0f, 0, 0, 50);
            Assert.Equal(0.0f, fx.sigma);
        }

        [Fact]
        public void RenderEffectDropShadow_Gen()
        {
            var fx = RenderEffectDropShadow.Gen(0, 0, 0, 128, 45.0f, 10.0f, 5.0f, 50);
            Assert.Equal(SceneEffect.DropShadow, fx.type);
            Assert.Equal(128, fx.color[3]);
        }

        [Fact]
        public void RenderEffectFill_Gen()
        {
            var fx = RenderEffectFill.Gen(255, 128, 64, 200);
            Assert.Equal(SceneEffect.Fill, fx.type);
            Assert.Equal(255, fx.color[0]);
        }

        [Fact]
        public void RenderEffectTint_Gen()
        {
            var fx = RenderEffectTint.Gen(0, 0, 0, 255, 255, 255, 50.0);
            Assert.Equal(SceneEffect.Tint, fx.type);
        }

        [Fact]
        public void RenderEffectTritone_Gen()
        {
            var fx = RenderEffectTritone.Gen(0, 0, 0, 128, 128, 128, 255, 255, 255, 0);
            Assert.Equal(SceneEffect.Tritone, fx.type);
        }

        // ---- RenderHelper --------------------------------------------------

        [Fact]
        public void MaskRegionMerging_ReturnsFalseForAlpha()
        {
            Assert.False(RenderHelper.MaskRegionMerging(MaskMethod.Alpha));
        }

        [Fact]
        public void MaskRegionMerging_ReturnsTrueForAdd()
        {
            Assert.True(RenderHelper.MaskRegionMerging(MaskMethod.Add));
        }

        [Fact]
        public void ChannelSize_ARGB8888_Returns4()
        {
            Assert.Equal(4, RenderHelper.ChannelSize(ColorSpace.ARGB8888));
        }

        [Fact]
        public void ChannelSize_Grayscale_Returns1()
        {
            Assert.Equal(1, RenderHelper.ChannelSize(ColorSpace.Grayscale8));
        }

        [Fact]
        public void ChannelSize_Unknown_Returns0()
        {
            Assert.Equal(0, RenderHelper.ChannelSize(ColorSpace.Unknown));
        }

        [Fact]
        public void Multiply_Basic()
        {
            Assert.Equal(0, RenderHelper.Multiply(0, 255));
            Assert.Equal(255, RenderHelper.Multiply(255, 255));
        }

        // ---- RenderStroke CopyFrom -----------------------------------------

        [Fact]
        public void RenderStroke_CopyFrom()
        {
            var src = new RenderStroke
            {
                width = 3.0f,
                color = new RGBA(128, 64, 32, 255),
                cap = StrokeCap.Round,
                join = StrokeJoin.Miter,
                miterlimit = 8.0f,
                dashPattern = new float[] { 5.0f, 3.0f },
                dashCount = 2,
                dashOffset = 1.0f
            };

            var dst = new RenderStroke();
            dst.CopyFrom(src);
            Assert.Equal(3.0f, dst.width);
            Assert.Equal(128, dst.color.r);
            Assert.Equal(StrokeCap.Round, dst.cap);
            Assert.Equal(StrokeJoin.Miter, dst.join);
            Assert.Equal(8.0f, dst.miterlimit);
            Assert.NotNull(dst.dashPattern);
            Assert.Equal(5.0f, dst.dashPattern![0]);
            Assert.Equal(3.0f, dst.dashPattern[1]);
        }
    }
}
