using Xunit;

namespace ThorVG.Tests
{
    public class tvgFillTests
    {
        // ---- LinearGradient ------------------------------------------------

        [Fact]
        public void LinearGradient_Gen_ReturnsInstance()
        {
            var grad = LinearGradient.Gen();
            Assert.NotNull(grad);
            Assert.Equal(Type.LinearGradient, grad.GetFillType());
        }

        [Fact]
        public void LinearGradient_SetAndGet()
        {
            var grad = LinearGradient.Gen();
            var result = grad.Linear(10.0f, 20.0f, 30.0f, 40.0f);
            Assert.Equal(Result.Success, result);

            grad.Linear(out float x1, out float y1, out float x2, out float y2);
            Assert.Equal(10.0f, x1);
            Assert.Equal(20.0f, y1);
            Assert.Equal(30.0f, x2);
            Assert.Equal(40.0f, y2);
        }

        [Fact]
        public void LinearGradient_Duplicate_CopiesValues()
        {
            var grad = LinearGradient.Gen();
            grad.Linear(1.0f, 2.0f, 3.0f, 4.0f);
            grad.SetSpread(FillSpread.Reflect);
            var stops = new Fill.ColorStop[]
            {
                new Fill.ColorStop(0.0f, 255, 0, 0, 255),
                new Fill.ColorStop(1.0f, 0, 0, 255, 255),
            };
            grad.SetColorStops(stops, 2);

            var dup = (LinearGradient)grad.Duplicate();
            dup.Linear(out float x1, out float y1, out float x2, out float y2);
            Assert.Equal(1.0f, x1);
            Assert.Equal(2.0f, y1);
            Assert.Equal(3.0f, x2);
            Assert.Equal(4.0f, y2);
            Assert.Equal(FillSpread.Reflect, dup.GetSpread());
            Assert.Equal(2u, dup.GetColorStops(out var dupStops));
            Assert.NotNull(dupStops);
            Assert.Equal(255, dupStops![0].r);
        }

        // ---- RadialGradient ------------------------------------------------

        [Fact]
        public void RadialGradient_Gen_ReturnsInstance()
        {
            var grad = RadialGradient.Gen();
            Assert.NotNull(grad);
            Assert.Equal(Type.RadialGradient, grad.GetFillType());
        }

        [Fact]
        public void RadialGradient_SetAndGet()
        {
            var grad = RadialGradient.Gen();
            var result = grad.Radial(10.0f, 20.0f, 30.0f, 5.0f, 6.0f, 2.0f);
            Assert.Equal(Result.Success, result);

            grad.Radial(out float cx, out float cy, out float r, out float fx, out float fy, out float fr);
            Assert.Equal(10.0f, cx);
            Assert.Equal(20.0f, cy);
            Assert.Equal(30.0f, r);
            Assert.Equal(5.0f, fx);
            Assert.Equal(6.0f, fy);
            Assert.Equal(2.0f, fr);
        }

        [Fact]
        public void RadialGradient_NegativeRadius_ReturnsInvalidArguments()
        {
            var grad = RadialGradient.Gen();
            Assert.Equal(Result.InvalidArguments, grad.Radial(0, 0, -1, 0, 0, 0));
        }

        [Fact]
        public void RadialGradient_Duplicate_CopiesValues()
        {
            var grad = RadialGradient.Gen();
            grad.Radial(10.0f, 20.0f, 30.0f, 5.0f, 6.0f, 2.0f);
            grad.SetSpread(FillSpread.Repeat);

            var dup = (RadialGradient)grad.Duplicate();
            dup.Radial(out float cx, out float cy, out float r, out float fx, out float fy, out float fr);
            Assert.Equal(10.0f, cx);
            Assert.Equal(30.0f, r);
            Assert.Equal(FillSpread.Repeat, dup.GetSpread());
        }

        // ---- ColorStops ----------------------------------------------------

        [Fact]
        public void Fill_SetColorStops_NullWithZeroCount()
        {
            var grad = LinearGradient.Gen();
            Assert.Equal(Result.Success, grad.SetColorStops(null, 0));
            Assert.Equal(0u, grad.GetColorStops(out var stops));
            Assert.Null(stops);
        }

        [Fact]
        public void Fill_SetColorStops_NullWithNonZeroCount_InvalidArgs()
        {
            var grad = LinearGradient.Gen();
            Assert.Equal(Result.InvalidArguments, grad.SetColorStops(null, 3));
        }

        [Fact]
        public void Fill_Spread_DefaultIsPad()
        {
            var grad = LinearGradient.Gen();
            Assert.Equal(FillSpread.Pad, grad.GetSpread());
        }

        [Fact]
        public void Fill_Transform_DefaultIsIdentity()
        {
            var grad = LinearGradient.Gen();
            ref var m = ref grad.GetTransform();
            Assert.Equal(1.0f, m.e11);
            Assert.Equal(0.0f, m.e12);
            Assert.Equal(1.0f, m.e22);
            Assert.Equal(1.0f, m.e33);
        }

        [Fact]
        public void Fill_SetTransform()
        {
            var grad = LinearGradient.Gen();
            var m = new Matrix(2, 0, 10, 0, 2, 20, 0, 0, 1);
            grad.SetTransform(m);
            ref var got = ref grad.GetTransform();
            Assert.Equal(2.0f, got.e11);
            Assert.Equal(10.0f, got.e13);
        }
    }
}
