using Xunit;

namespace ThorVG.Tests
{
    public class tvgMathTests
    {
        private const float Eps = MathConstants.FLOAT_EPSILON;

        // ---- General helpers -------------------------------------------

        [Fact]
        public void Deg2Rad_And_Rad2Deg_Roundtrip()
        {
            var rad = TvgMath.Deg2Rad(180.0f);
            Assert.True(TvgMath.Equal(rad, MathConstants.MATH_PI));
            var deg = TvgMath.Rad2Deg(MathConstants.MATH_PI);
            Assert.True(TvgMath.Equal(deg, 180.0f));
        }

        [Fact]
        public void Zero_And_Equal()
        {
            Assert.True(TvgMath.Zero(0.0f));
            Assert.True(TvgMath.Zero(Eps * 0.5f));
            Assert.False(TvgMath.Zero(1.0f));
            Assert.True(TvgMath.Equal(1.0f, 1.0f));
            Assert.True(TvgMath.Equal(1.0f, 1.0f + Eps * 0.5f));
            Assert.False(TvgMath.Equal(1.0f, 2.0f));
        }

        [Fact]
        public void Clamp_InRange_And_OutOfRange()
        {
            Assert.Equal(5, TvgMath.Clamp(5, 0, 10));
            Assert.Equal(0, TvgMath.Clamp(-1, 0, 10));
            Assert.Equal(10, TvgMath.Clamp(15, 0, 10));
        }

        [Fact]
        public void Atan2_BasicQuadrants()
        {
            Assert.True(TvgMath.Equal(TvgMath.Atan2(0, 0), 0.0f));
            // Positive x-axis
            var a = TvgMath.Atan2(0, 1);
            Assert.True(System.MathF.Abs(a) < 0.01f);
            // 90 degrees
            var b = TvgMath.Atan2(1, 0);
            Assert.True(System.MathF.Abs(b - MathConstants.MATH_PI2) < 0.02f);
        }

        // ---- Matrix functions ------------------------------------------

        [Fact]
        public void Identity_Matrix_IsIdentity()
        {
            var m = TvgMath.Identity();
            Assert.True(TvgMath.IsIdentity(m));
        }

        [Fact]
        public void SetIdentity_Works()
        {
            TvgMath.SetIdentity(out Matrix m);
            Assert.True(TvgMath.IsIdentity(m));
        }

        [Fact]
        public void Multiply_Identity_ReturnsSame()
        {
            var id = TvgMath.Identity();
            var m = new Matrix(1, 2, 3, 4, 5, 6, 7, 8, 9);
            var result = TvgMath.Multiply(m, id);
            Assert.True(TvgMath.MatrixEqual(m, result));
        }

        [Fact]
        public void MatrixEqual_DetectsEquality()
        {
            var a = new Matrix(1, 2, 3, 4, 5, 6, 7, 8, 9);
            var b = new Matrix(1, 2, 3, 4, 5, 6, 7, 8, 9);
            Assert.True(TvgMath.MatrixEqual(a, b));
            b.e11 = 99;
            Assert.False(TvgMath.MatrixEqual(a, b));
        }

        [Fact]
        public void Inverse_OfIdentity_IsIdentity()
        {
            var id = TvgMath.Identity();
            Assert.True(TvgMath.Inverse(id, out Matrix inv));
            Assert.True(TvgMath.MatrixEqual(id, inv));
        }

        [Fact]
        public void Inverse_OfSingular_ReturnsFalse()
        {
            var singular = new Matrix(0, 0, 0, 0, 0, 0, 0, 0, 0);
            Assert.False(TvgMath.Inverse(singular, out _));
        }

        [Fact]
        public void Rotate_90Degrees()
        {
            var m = TvgMath.Identity();
            TvgMath.Rotate(ref m, 90.0f);
            // After 90-degree rotation: e11 ~ 0, e21 ~ 1
            Assert.True(System.MathF.Abs(m.e11) < 0.001f);
            Assert.True(System.MathF.Abs(m.e21 - 1.0f) < 0.001f);
        }

        [Fact]
        public void Scaling_ReturnsCorrectValue()
        {
            var m = TvgMath.Identity();
            m.e11 = 3.0f;
            m.e21 = 4.0f;
            Assert.True(System.MathF.Abs(TvgMath.Scaling(m) - 5.0f) < 0.001f);
        }

        [Fact]
        public void Translate_And_TranslateR()
        {
            var m = TvgMath.Identity();
            TvgMath.Translate(ref m, new Point(10, 20));
            Assert.True(TvgMath.Equal(m.e13, 10.0f));
            Assert.True(TvgMath.Equal(m.e23, 20.0f));
        }

        [Fact]
        public void Skewed_DetectsSkew()
        {
            var m = TvgMath.Identity();
            Assert.False(TvgMath.Skewed(m));
            m.e12 = 1.0f;
            m.e21 = -1.0f; // sum = 0 => not skewed
            Assert.False(TvgMath.Skewed(m));
            m.e21 = 1.0f; // sum = 2 => skewed
            Assert.True(TvgMath.Skewed(m));
        }

        // ---- Point functions -------------------------------------------

        [Fact]
        public void PointEqual_Works()
        {
            var a = new Point(1.0f, 2.0f);
            var b = new Point(1.0f, 2.0f);
            Assert.True(TvgMath.PointEqual(a, b));
        }

        [Fact]
        public void PointArithmetic()
        {
            var a = new Point(3.0f, 4.0f);
            var b = new Point(1.0f, 2.0f);

            var sub = TvgMath.PointSub(a, b);
            Assert.True(TvgMath.Equal(sub.x, 2.0f));
            Assert.True(TvgMath.Equal(sub.y, 2.0f));

            var add = TvgMath.PointAdd(a, b);
            Assert.True(TvgMath.Equal(add.x, 4.0f));
            Assert.True(TvgMath.Equal(add.y, 6.0f));

            var mul = TvgMath.PointMul(a, 2.0f);
            Assert.True(TvgMath.Equal(mul.x, 6.0f));
            Assert.True(TvgMath.Equal(mul.y, 8.0f));

            var div = TvgMath.PointDiv(a, 2.0f);
            Assert.True(TvgMath.Equal(div.x, 1.5f));
            Assert.True(TvgMath.Equal(div.y, 2.0f));
        }

        [Fact]
        public void Dot_And_Cross()
        {
            var a = new Point(1, 0);
            var b = new Point(0, 1);
            Assert.True(TvgMath.Equal(TvgMath.Dot(a, b), 0.0f));
            Assert.True(TvgMath.Equal(TvgMath.Cross(a, b), 1.0f));
        }

        [Fact]
        public void PointLength_Euclidean()
        {
            var p = new Point(3, 4);
            Assert.True(System.MathF.Abs(TvgMath.PointLength(p) - 5.0f) < 0.001f);
        }

        [Fact]
        public void Normalize_UnitVector()
        {
            var p = new Point(3, 4);
            TvgMath.Normalize(ref p);
            Assert.True(System.MathF.Abs(TvgMath.PointLength(p) - 1.0f) < 0.001f);
        }

        [Fact]
        public void Normal_PerpendicularToDirection()
        {
            var p1 = new Point(0, 0);
            var p2 = new Point(1, 0);
            var n = TvgMath.Normal(p1, p2);
            // normal should be (0, 1) or (0, -1)
            Assert.True(TvgMath.Zero(n.x));
            Assert.True(System.MathF.Abs(System.MathF.Abs(n.y) - 1.0f) < 0.001f);
        }

        [Fact]
        public void Transform_Point_ByIdentity_Unchanged()
        {
            var pt = new Point(5, 10);
            var id = TvgMath.Identity();
            var result = TvgMath.Transform(pt, id);
            Assert.True(TvgMath.PointEqual(pt, result));
        }

        [Fact]
        public void Orientation_Clockwise_CounterClockwise()
        {
            var p1 = new Point(0, 0);
            var p2 = new Point(1, 0);
            var p3cw = new Point(1, 1);
            Assert.Equal(Orientation.Clockwise, TvgMath.GetOrientation(p1, p2, p3cw));

            var p3ccw = new Point(1, -1);
            Assert.Equal(Orientation.CounterClockwise, TvgMath.GetOrientation(p1, p2, p3ccw));

            var p3lin = new Point(2, 0);
            Assert.Equal(Orientation.Linear, TvgMath.GetOrientation(p1, p2, p3lin));
        }

        [Fact]
        public void Closed_ProximityCheck()
        {
            var a = new Point(0, 0);
            var b = new Point(0.001f, 0);
            Assert.True(TvgMath.Closed(a, b, 0.01f));
            Assert.False(TvgMath.Closed(a, b, 0.0001f));
        }

        [Fact]
        public void ArcSegmentsCnt_SmallRadius_Returns2()
        {
            Assert.Equal(2u, TvgMath.ArcSegmentsCnt(1.0f, 0.0f));
        }

        // ---- Lerp ------------------------------------------------------

        [Fact]
        public void Lerp_Float_And_Byte()
        {
            Assert.True(TvgMath.Equal(TvgMath.Lerp(0.0f, 10.0f, 0.5f), 5.0f));
            Assert.Equal((byte)127, TvgMath.Lerp((byte)0, (byte)255, 0.5f));
        }

        // ---- Line -------------------------------------------------------

        [Fact]
        public void Line_Length()
        {
            var line = new Line { pt1 = new Point(0, 0), pt2 = new Point(3, 4) };
            Assert.True(System.MathF.Abs(line.Length() - 5.0f) < 0.001f);
        }

        [Fact]
        public void Line_Split()
        {
            var line = new Line { pt1 = new Point(0, 0), pt2 = new Point(10, 0) };
            line.Split(5.0f, out Line left, out Line right);
            Assert.True(TvgMath.Equal(left.pt2.x, 5.0f));
            Assert.True(TvgMath.Equal(right.pt1.x, 5.0f));
        }

        // ---- BBox -------------------------------------------------------

        [Fact]
        public void BBox_Init()
        {
            var box = new BBox();
            box.Init();
            Assert.Equal(float.MaxValue, box.min.x);
            Assert.Equal(-float.MaxValue, box.max.x);
        }

        // ---- Bezier -----------------------------------------------------

        [Fact]
        public void Bezier_AtEndpoints()
        {
            var bz = new Bezier(new Point(0, 0), new Point(1, 2), new Point(3, 2), new Point(4, 0));
            var p0 = bz.At(0.0f);
            var p1 = bz.At(1.0f);
            Assert.True(TvgMath.Equal(p0.x, 0.0f) && TvgMath.Equal(p0.y, 0.0f));
            Assert.True(TvgMath.Equal(p1.x, 4.0f) && TvgMath.Equal(p1.y, 0.0f));
        }

        [Fact]
        public void Bezier_Length_NonNegative()
        {
            var bz = new Bezier(new Point(0, 0), new Point(1, 2), new Point(3, 2), new Point(4, 0));
            Assert.True(bz.Length() > 0);
        }

        [Fact]
        public void Bezier_LengthApprox_NonNegative()
        {
            var bz = new Bezier(new Point(0, 0), new Point(1, 2), new Point(3, 2), new Point(4, 0));
            Assert.True(bz.LengthApprox() > 0);
        }

        [Fact]
        public void Bezier_Flatten_StraightLine()
        {
            var bz = new Bezier(new Point(0, 0), new Point(1, 0), new Point(2, 0), new Point(3, 0));
            Assert.True(bz.Flatten());
        }

        [Fact]
        public void Bezier_Segments_GreaterThanZero()
        {
            var bz = new Bezier(new Point(0, 0), new Point(1, 2), new Point(3, 2), new Point(4, 0));
            Assert.True(bz.Segments() > 0);
        }

        [Fact]
        public void Bezier_SplitHalf_EndpointsPreserved()
        {
            var bz = new Bezier(new Point(0, 0), new Point(1, 2), new Point(3, 2), new Point(4, 0));
            bz.Split(out Bezier left, out Bezier right);
            Assert.True(TvgMath.PointEqual(left.start, bz.start));
            Assert.True(TvgMath.PointEqual(right.end, bz.end));
            Assert.True(TvgMath.PointEqual(left.end, right.start));
        }

        [Fact]
        public void Bezier_Angle_AtMidpoint()
        {
            var bz = new Bezier(new Point(0, 0), new Point(1, 0), new Point(2, 0), new Point(3, 0));
            // Straight horizontal line: angle should be close to 0
            var angle = bz.Angle(0.5f);
            Assert.True(System.MathF.Abs(angle) < 1.0f);
        }

        [Fact]
        public void Bezier_Bounds()
        {
            var box = new BBox();
            box.Init();
            Bezier.Bounds(ref box, new Point(0, 0), new Point(1, 2), new Point(3, 2), new Point(4, 0));
            Assert.True(box.min.x <= 0.0f);
            Assert.True(box.max.x >= 4.0f);
        }

        [Fact]
        public void Bezier_Transform_Identity()
        {
            var bz = new Bezier(new Point(0, 0), new Point(1, 2), new Point(3, 2), new Point(4, 0));
            var id = TvgMath.Identity();
            var result = bz.Transform(id);
            Assert.True(TvgMath.PointEqual(bz.start, result.start));
            Assert.True(TvgMath.PointEqual(bz.end, result.end));
        }
    }
}
