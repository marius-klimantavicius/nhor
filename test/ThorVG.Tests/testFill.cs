// Ported from ThorVG/test/testFill.cpp

using System;
using System.IO;
using Xunit;

namespace ThorVG.Tests
{
    public class testFill
    {
        private static readonly string TEST_DIR = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ThorVG", "test", "resources"));

        private const float MARGIN = 0.000001f;

        private static void AssertApprox(float expected, float actual)
        {
            Assert.InRange(actual, expected - MARGIN, expected + MARGIN);
        }

        [Fact]
        public void FillingCreation()
        {
            var linear = LinearGradient.Gen();
            Assert.NotNull(linear);
            Assert.Equal(Type.LinearGradient, linear.GetFillType());

            var radial = RadialGradient.Gen();
            Assert.NotNull(radial);
            Assert.Equal(Type.RadialGradient, radial.GetFillType());
        }

        [Fact]
        public void CommonFilling()
        {
            var fill = LinearGradient.Gen();
            Assert.NotNull(fill);

            // Options
            Assert.Equal(FillSpread.Pad, fill.GetSpread());
            Assert.Equal(Result.Success, fill.SetSpread(FillSpread.Pad));
            Assert.Equal(Result.Success, fill.SetSpread(FillSpread.Reflect));
            Assert.Equal(Result.Success, fill.SetSpread(FillSpread.Repeat));
            Assert.Equal(FillSpread.Repeat, fill.GetSpread());

            // ColorStops
            Assert.Equal(0u, fill.GetColorStops(out var cs));
            Assert.Null(cs);

            var cs2 = new Fill.ColorStop[]
            {
                new() { offset = 0.0f, r = 0, g = 0, b = 0, a = 0 },
                new() { offset = 0.2f, r = 50, g = 25, b = 50, a = 25 },
                new() { offset = 0.5f, r = 100, g = 100, b = 100, a = 125 },
                new() { offset = 1.0f, r = 255, g = 255, b = 255, a = 255 }
            };

            Assert.Equal(Result.InvalidArguments, fill.SetColorStops(null, 4));
            Assert.Equal(Result.InvalidArguments, fill.SetColorStops(cs2, 0));
            Assert.Equal(Result.Success, fill.SetColorStops(cs2, 4));
            Assert.Equal(4u, fill.GetColorStops(out cs));

            for (int i = 0; i < 4; ++i)
            {
                Assert.Equal(cs2[i].offset, cs![i].offset);
                Assert.Equal(cs2[i].r, cs[i].r);
                Assert.Equal(cs2[i].g, cs[i].g);
                Assert.Equal(cs2[i].b, cs[i].b);
            }

            // Reset ColorStop
            Assert.Equal(Result.Success, fill.SetColorStops(null, 0));
            Assert.Equal(0u, fill.GetColorStops(out cs));

            // Set to Shape
            var shape = Shape.Gen();
            Assert.NotNull(shape);

            Assert.Equal(Result.Success, shape.SetFill(fill));
            Assert.Same(fill, shape.GetFill());

            Paint.Rel(shape);
        }

        [Fact]
        public void FillTransformation()
        {
            var fill = LinearGradient.Gen();
            Assert.NotNull(fill);

            // no transformation (identity)
            ref var mGet = ref fill.GetTransform();
            AssertApprox(1.0f, mGet.e11);
            AssertApprox(0.0f, mGet.e12);
            AssertApprox(0.0f, mGet.e13);
            AssertApprox(0.0f, mGet.e21);
            AssertApprox(1.0f, mGet.e22);
            AssertApprox(0.0f, mGet.e23);
            AssertApprox(0.0f, mGet.e31);
            AssertApprox(0.0f, mGet.e32);
            AssertApprox(1.0f, mGet.e33);

            var mSet = new Matrix { e11 = 1.1f, e12 = 2.2f, e13 = 3.3f, e21 = 4.4f, e22 = 5.5f, e23 = 6.6f, e31 = -7.7f, e32 = -8.8f, e33 = -9.9f };
            Assert.Equal(Result.Success, fill.SetTransform(mSet));

            // transformation was set
            mGet = ref fill.GetTransform();
            AssertApprox(mSet.e11, mGet.e11);
            AssertApprox(mSet.e12, mGet.e12);
            AssertApprox(mSet.e13, mGet.e13);
            AssertApprox(mSet.e21, mGet.e21);
            AssertApprox(mSet.e22, mGet.e22);
            AssertApprox(mSet.e23, mGet.e23);
            AssertApprox(mSet.e31, mGet.e31);
            AssertApprox(mSet.e32, mGet.e32);
            AssertApprox(mSet.e33, mGet.e33);
        }

        [Fact]
        public void LinearFilling()
        {
            var fill = LinearGradient.Gen();
            Assert.NotNull(fill);

            // Getter with discards
            fill.Linear(out _, out _, out _, out _);
            Assert.Equal(Result.Success, fill.Linear(0, 0, 0, 0));

            fill.Linear(out var x1, out _, out var x2, out _);
            Assert.Equal(0.0f, x1);
            Assert.Equal(0.0f, x2);

            Assert.Equal(Result.Success, fill.Linear(-1.0f, -1.0f, 100.0f, 100.0f));
            fill.Linear(out x1, out var y1, out x2, out var y2);
            Assert.Equal(-1.0f, x1);
            Assert.Equal(-1.0f, y1);
            Assert.Equal(100.0f, x2);
            Assert.Equal(100.0f, y2);
        }

        [Fact]
        public void RadialFilling()
        {
            var fill = RadialGradient.Gen();
            Assert.NotNull(fill);

            Assert.Equal(Result.InvalidArguments, fill.Radial(0, 0, -1, 0, 0, 0));
            Assert.Equal(Result.InvalidArguments, fill.Radial(0, 0, 0, 0, 0, -1));
            fill.Radial(out _, out _, out _, out _, out _, out _);
            Assert.Equal(Result.Success, fill.Radial(100, 120, 50, 10, 20, 5));

            fill.Radial(out var cx, out _, out var r, out _, out _, out _);
            Assert.Equal(100.0f, cx);
            Assert.Equal(50.0f, r);

            fill.Radial(out _, out var cy, out _, out var fx, out var fy, out var fr);
            Assert.Equal(120.0f, cy);
            Assert.Equal(10.0f, fx);
            Assert.Equal(20.0f, fy);
            Assert.Equal(5.0f, fr);

            Assert.Equal(Result.Success, fill.Radial(0, 0, 0, 0, 0, 0));
            fill.Radial(out cx, out cy, out r, out fx, out fy, out fr);
            Assert.Equal(0.0f, cx);
            Assert.Equal(0.0f, cy);
            Assert.Equal(0.0f, r);
            Assert.Equal(0.0f, fx);
            Assert.Equal(0.0f, fy);
            Assert.Equal(0.0f, fr);
        }

        [Fact]
        public void LinearFillingDuplication()
        {
            var fill = LinearGradient.Gen();
            Assert.NotNull(fill);

            // Setup
            var cs = new Fill.ColorStop[]
            {
                new() { offset = 0.0f, r = 0, g = 0, b = 0, a = 0 },
                new() { offset = 0.2f, r = 50, g = 25, b = 50, a = 25 },
                new() { offset = 0.5f, r = 100, g = 100, b = 100, a = 125 },
                new() { offset = 1.0f, r = 255, g = 255, b = 255, a = 255 }
            };

            Assert.Equal(Result.Success, fill.SetColorStops(cs, 4));
            Assert.Equal(Result.Success, fill.SetSpread(FillSpread.Reflect));
            Assert.Equal(Result.Success, fill.Linear(-10.0f, 10.0f, 100.0f, 120.0f));

            var m = new Matrix { e11 = 1.1f, e12 = 2.2f, e13 = 3.3f, e21 = 4.4f, e22 = 5.5f, e23 = 6.6f, e31 = -7.7f, e32 = -8.8f, e33 = -9.9f };
            Assert.Equal(Result.Success, fill.SetTransform(m));

            // Duplication
            var dup = (LinearGradient)fill.Duplicate();
            Assert.NotNull(dup);

            Assert.Equal(FillSpread.Reflect, dup.GetSpread());

            fill.Linear(out var x1, out var y1, out var x2, out var y2);
            Assert.Equal(-10.0f, x1);
            Assert.Equal(10.0f, y1);
            Assert.Equal(100.0f, x2);
            Assert.Equal(120.0f, y2);

            var cnt = fill.GetColorStops(out var cs2);
            Assert.Equal(4u, cnt);

            for (int i = 0; i < 4; ++i)
            {
                Assert.Equal(cs[i].offset, cs2![i].offset);
                Assert.Equal(cs[i].r, cs2[i].r);
                Assert.Equal(cs[i].g, cs2[i].g);
                Assert.Equal(cs[i].b, cs2[i].b);
            }

            ref var mDup = ref dup.GetTransform();
            AssertApprox(m.e11, mDup.e11);
            AssertApprox(m.e12, mDup.e12);
            AssertApprox(m.e13, mDup.e13);
            AssertApprox(m.e21, mDup.e21);
            AssertApprox(m.e22, mDup.e22);
            AssertApprox(m.e23, mDup.e23);
            AssertApprox(m.e31, mDup.e31);
            AssertApprox(m.e32, mDup.e32);
            AssertApprox(m.e33, mDup.e33);
        }

        [Fact]
        public void RadialFillingDuplication()
        {
            var fill = RadialGradient.Gen();
            Assert.NotNull(fill);

            // Setup
            var cs = new Fill.ColorStop[]
            {
                new() { offset = 0.0f, r = 0, g = 0, b = 0, a = 0 },
                new() { offset = 0.2f, r = 50, g = 25, b = 50, a = 25 },
                new() { offset = 0.5f, r = 100, g = 100, b = 100, a = 125 },
                new() { offset = 1.0f, r = 255, g = 255, b = 255, a = 255 }
            };

            Assert.Equal(Result.Success, fill.SetColorStops(cs, 4));
            Assert.Equal(Result.Success, fill.SetSpread(FillSpread.Reflect));
            Assert.Equal(Result.Success, fill.Radial(100.0f, 120.0f, 50.0f, 10.0f, 20.0f, 5.0f));

            var m = new Matrix { e11 = 1.1f, e12 = 2.2f, e13 = 3.3f, e21 = 4.4f, e22 = 5.5f, e23 = 6.6f, e31 = -7.7f, e32 = -8.8f, e33 = -9.9f };
            Assert.Equal(Result.Success, fill.SetTransform(m));

            // Duplication
            var dup = (RadialGradient)fill.Duplicate();
            Assert.NotNull(dup);

            Assert.Equal(FillSpread.Reflect, dup.GetSpread());

            dup.Radial(out var cx, out var cy, out var r, out var fx, out var fy, out var fr);
            Assert.Equal(100.0f, cx);
            Assert.Equal(120.0f, cy);
            Assert.Equal(50.0f, r);
            Assert.Equal(10.0f, fx);
            Assert.Equal(20.0f, fy);
            Assert.Equal(5.0f, fr);

            var cnt = fill.GetColorStops(out var cs2);
            Assert.Equal(4u, cnt);

            for (int i = 0; i < 4; ++i)
            {
                Assert.Equal(cs[i].offset, cs2![i].offset);
                Assert.Equal(cs[i].r, cs2[i].r);
                Assert.Equal(cs[i].g, cs2[i].g);
                Assert.Equal(cs[i].b, cs2[i].b);
            }

            ref var mDup = ref dup.GetTransform();
            AssertApprox(m.e11, mDup.e11);
            AssertApprox(m.e12, mDup.e12);
            AssertApprox(m.e13, mDup.e13);
            AssertApprox(m.e21, mDup.e21);
            AssertApprox(m.e22, mDup.e22);
            AssertApprox(m.e23, mDup.e23);
            AssertApprox(m.e31, mDup.e31);
            AssertApprox(m.e32, mDup.e32);
            AssertApprox(m.e33, mDup.e33);
        }
    }
}
