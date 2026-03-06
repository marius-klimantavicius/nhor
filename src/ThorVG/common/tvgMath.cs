// Ported from ThorVG/src/common/tvgMath.h and tvgMath.cpp

using System;
using System.Runtime.CompilerServices;

namespace ThorVG
{
    // =====================================================================
    //  Constants
    // =====================================================================
    public static class MathConstants
    {
        public const float MATH_PI  = 3.14159265358979323846f;
        public const float MATH_PI2 = 1.57079632679489661923f;
        public const float FLOAT_EPSILON = 1.0e-06f;
        public const float PATH_KAPPA = 0.552284f;
    }

    // =====================================================================
    //  Orientation enum (from tvgMath.h)
    // =====================================================================
    public enum Orientation
    {
        Linear = 0,
        Clockwise,
        CounterClockwise,
    }

    // =====================================================================
    //  Line struct
    // =====================================================================
    public struct Line
    {
        public Point pt1;
        public Point pt2;

        public float Length()
        {
            return TvgMath.LineLength(pt1, pt2);
        }

        public void Split(float at, out Line left, out Line right)
        {
            var len = Length();
            var dx = ((pt2.x - pt1.x) / len) * at;
            var dy = ((pt2.y - pt1.y) / len) * at;
            left.pt1 = pt1;
            left.pt2.x = left.pt1.x + dx;
            left.pt2.y = left.pt1.y + dy;
            right.pt1 = left.pt2;
            right.pt2 = pt2;
        }
    }

    // =====================================================================
    //  BBox struct
    // =====================================================================
    public struct BBox
    {
        public Point min;
        public Point max;

        public void Init()
        {
            min = new Point(float.MaxValue, float.MaxValue);
            max = new Point(-float.MaxValue, -float.MaxValue);
        }
    }

    // =====================================================================
    //  Bezier struct
    // =====================================================================
    public struct Bezier
    {
        public Point start;
        public Point ctrl1;
        public Point ctrl2;
        public Point end;

        private const float BEZIER_EPSILON = 1e-2f;

        public Bezier(Point p0, Point p1, Point p2, Point p3)
        {
            start = p0; ctrl1 = p1; ctrl2 = p2; end = p3;
        }

        /// <summary>
        /// Constructor that approximates a quarter-circle arc between
        /// <paramref name="st"/> and <paramref name="ed"/> with given
        /// <paramref name="radius"/>.
        /// </summary>
        public Bezier(Point st, Point ed, float radius)
        {
            var angle = TvgMath.Atan2(ed.y - st.y, ed.x - st.x);
            var c = radius * MathConstants.PATH_KAPPA;
            start = new Point(st.x, st.y);
            ctrl1 = new Point(st.x + radius * MathF.Cos(angle), st.y + radius * MathF.Sin(angle));
            ctrl2 = new Point(ed.x - c * MathF.Cos(angle), ed.y - c * MathF.Sin(angle));
            end = new Point(ed.x, ed.y);
        }

        // ---- Split (modifying, t-based) ---------------------------------
        public void Split(float t, out Bezier left)
        {
            left.start = start;

            left.ctrl1.x = start.x + t * (ctrl1.x - start.x);
            left.ctrl1.y = start.y + t * (ctrl1.y - start.y);

            left.ctrl2.x = ctrl1.x + t * (ctrl2.x - ctrl1.x);   // temporary
            left.ctrl2.y = ctrl1.y + t * (ctrl2.y - ctrl1.y);

            ctrl2.x = ctrl2.x + t * (end.x - ctrl2.x);
            ctrl2.y = ctrl2.y + t * (end.y - ctrl2.y);

            ctrl1.x = left.ctrl2.x + t * (ctrl2.x - left.ctrl2.x);
            ctrl1.y = left.ctrl2.y + t * (ctrl2.y - left.ctrl2.y);

            left.ctrl2.x = left.ctrl1.x + t * (left.ctrl2.x - left.ctrl1.x);
            left.ctrl2.y = left.ctrl1.y + t * (left.ctrl2.y - left.ctrl1.y);

            left.end.x = start.x = left.ctrl2.x + t * (ctrl1.x - left.ctrl2.x);
            left.end.y = start.y = left.ctrl2.y + t * (ctrl1.y - left.ctrl2.y);
        }

        // ---- Split (half, const) ----------------------------------------
        public readonly void Split(out Bezier left, out Bezier right)
        {
            var c = (ctrl1.x + ctrl2.x) * 0.5f;
            left.ctrl1.x = (start.x + ctrl1.x) * 0.5f;
            right.ctrl2.x = (ctrl2.x + end.x) * 0.5f;
            left.start.x = start.x;
            right.end.x = end.x;
            left.ctrl2.x = (left.ctrl1.x + c) * 0.5f;
            right.ctrl1.x = (right.ctrl2.x + c) * 0.5f;
            left.end.x = right.start.x = (left.ctrl2.x + right.ctrl1.x) * 0.5f;

            c = (ctrl1.y + ctrl2.y) * 0.5f;
            left.ctrl1.y = (start.y + ctrl1.y) * 0.5f;
            right.ctrl2.y = (ctrl2.y + end.y) * 0.5f;
            left.start.y = start.y;
            right.end.y = end.y;
            left.ctrl2.y = (left.ctrl1.y + c) * 0.5f;
            right.ctrl1.y = (right.ctrl2.y + c) * 0.5f;
            left.end.y = right.start.y = (left.ctrl2.y + right.ctrl1.y) * 0.5f;
        }

        // ---- Split (at length) ------------------------------------------
        public readonly void Split(float at, out Bezier left, out Bezier right)
        {
            right = this;
            var t = right.At(at, right.Length());
            right.Split(t, out left);
        }

        // ---- Line length strategy interface (JIT monomorphizes struct impls) --
        internal interface ILineLength
        {
            static abstract float Calc(Point a, Point b);
        }

        internal struct LineLengthExact : ILineLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float Calc(Point a, Point b)
            {
                var dx = b.x - a.x;
                var dy = b.y - a.y;
                return MathF.Sqrt(dx * dx + dy * dy);
            }
        }

        internal struct LineLengthApprox : ILineLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float Calc(Point a, Point b)
            {
                var dx = b.x - a.x;
                var dy = b.y - a.y;
                if (dx < 0) dx = -dx;
                if (dy < 0) dy = -dy;
                return (dx > dy) ? (dx + dy * 0.375f) : (dy + dx * 0.375f);
            }
        }

        // ---- Length -----------------------------------------------------
        public readonly float Length()
        {
            return BezLength<LineLengthExact>(this);
        }

        public readonly float LengthApprox()
        {
            return BezLength<LineLengthApprox>(this);
        }

        // ---- At (parameter from arc-length) ----------------------------
        public readonly float At(float at, float length)
        {
            return BezAt<LineLengthExact>(this, at, length);
        }

        public readonly float AtApprox(float at, float length)
        {
            return BezAt<LineLengthApprox>(this, at, length);
        }

        // ---- At (point) ------------------------------------------------
        public readonly Point At(float t)
        {
            var it = 1.0f - t;

            var ax = start.x * it + ctrl1.x * t;
            var bx = ctrl1.x * it + ctrl2.x * t;
            var cx = ctrl2.x * it + end.x * t;
            ax = ax * it + bx * t;
            bx = bx * it + cx * t;
            var px = ax * it + bx * t;

            var ay = start.y * it + ctrl1.y * t;
            var by = ctrl1.y * it + ctrl2.y * t;
            var cy = ctrl2.y * it + end.y * t;
            ay = ay * it + by * t;
            by = by * it + cy * t;
            var py = ay * it + by * t;

            return new Point(px, py);
        }

        // ---- Angle ------------------------------------------------------
        public readonly float Angle(float t)
        {
            if (t < 0 || t > 1) return 0;

            float mt = 1.0f - t;
            float d = t * t;
            float a = -mt * mt;
            float b = 1 - 4 * t + 3 * d;
            float c = 2 * t - 3 * d;

            var ptx = a * start.x + b * ctrl1.x + c * ctrl2.x + d * end.x;
            var pty = a * start.y + b * ctrl1.y + c * ctrl2.y + d * end.y;
            ptx *= 3;
            pty *= 3;

            return TvgMath.Rad2Deg(TvgMath.Atan2(pty, ptx));
        }

        // ---- Flatten ----------------------------------------------------
        public readonly bool Flatten()
        {
            float diff1_x = MathF.Abs((ctrl1.x * 3.0f) - (start.x * 2.0f) - end.x);
            float diff1_y = MathF.Abs((ctrl1.y * 3.0f) - (start.y * 2.0f) - end.y);
            float diff2_x = MathF.Abs((ctrl2.x * 3.0f) - (end.x * 2.0f) - start.x);
            float diff2_y = MathF.Abs((ctrl2.y * 3.0f) - (end.y * 2.0f) - start.y);
            if (diff1_x < diff2_x) diff1_x = diff2_x;
            if (diff1_y < diff2_y) diff1_y = diff2_y;
            return (diff1_x + diff1_y <= 0.5f);
        }

        // ---- Segments ---------------------------------------------------
        public readonly uint Segments()
        {
            const uint MaxSegments = 1u << 10;
            uint segCount = 0;
            var stack = new Array<Bezier>(16);
            try
            {
                stack.Push(this);
                while (!stack.Empty())
                {
                    var current = stack.Last();
                    stack.Pop();
                    if (current.Flatten())
                    {
                        ++segCount;
                        continue;
                    }
                    if (segCount + stack.count + 2 >= MaxSegments)
                    {
                        return MaxSegments;
                    }
                    current.Split(out Bezier leftSeg, out Bezier rightSeg);
                    stack.Push(leftSeg);
                    stack.Push(rightSeg);
                }
            }
            finally
            {
                stack.Dispose();
            }
            return segCount;
        }

        // ---- operator* (Matrix) ----------------------------------------
        public readonly Bezier Transform(in Matrix m)
        {
            return new Bezier(
                TvgMath.Transform(start, m),
                TvgMath.Transform(ctrl1, m),
                TvgMath.Transform(ctrl2, m),
                TvgMath.Transform(end, m));
        }

        // ---- Bounds (static) -------------------------------------------
        public static void Bounds(ref BBox box, Point start, Point ctrl1, Point ctrl2, Point end)
        {
            if (box.min.x > start.x) box.min.x = start.x;
            if (box.min.y > start.y) box.min.y = start.y;
            if (box.min.x > end.x) box.min.x = end.x;
            if (box.min.y > end.y) box.min.y = end.y;

            if (box.max.x < start.x) box.max.x = start.x;
            if (box.max.y < start.y) box.max.y = start.y;
            if (box.max.x < end.x) box.max.x = end.x;
            if (box.max.y < end.y) box.max.y = end.y;

            FindMinMax(start.x, ctrl1.x, ctrl2.x, end.x, ref box.min.x, ref box.max.x);
            FindMinMax(start.y, ctrl1.y, ctrl2.y, end.y, ref box.min.y, ref box.max.y);
        }

        // ---- Private helpers -------------------------------------------

        private static void FindMinMax(float start, float ctrl1, float ctrl2, float end,
                                       ref float min, ref float max)
        {
            var a = -1.0f * start + 3.0f * ctrl1 - 3.0f * ctrl2 + end;
            var b = start - 2.0f * ctrl1 + ctrl2;
            var c = -1.0f * start + ctrl1;
            var h = b * b - a * c;
            if (h <= 0.0f) return;
            h = MathF.Sqrt(h);
            Span<float> t = stackalloc float[] { (-b - h) / a, (-b + h) / a };
            for (int i = 0; i < 2; ++i)
            {
                if (t[i] <= 0.0f || t[i] >= 1.0f) continue;
                var s = 1.0f - t[i];
                var q = s * s * s * start + 3.0f * s * s * t[i] * ctrl1 +
                        3.0f * s * t[i] * t[i] * ctrl2 + t[i] * t[i] * t[i] * end;
                if (q < min) min = q;
                if (q > max) max = q;
            }
        }

        private static float BezLength<T>(in Bezier cur) where T : ILineLength
        {
            var len = T.Calc(cur.start, cur.ctrl1) + T.Calc(cur.ctrl1, cur.ctrl2) + T.Calc(cur.ctrl2, cur.end);
            var chord = T.Calc(cur.start, cur.end);
            if (MathF.Abs(len - chord) > BEZIER_EPSILON)
            {
                cur.Split(out Bezier left, out Bezier right);
                return BezLength<T>(left) + BezLength<T>(right);
            }
            return len;
        }

        private static float BezAt<T>(in Bezier bz, float at, float length) where T : ILineLength
        {
            var biggest = 1.0f;
            var smallest = 0.0f;
            var t = 0.5f;

            if (at <= 0) return 0.0f;
            if (at >= length) return 1.0f;

            while (true)
            {
                var right = bz;
                right.Split(t, out Bezier left);
                length = BezLength<T>(left);
                if (MathF.Abs(length - at) < BEZIER_EPSILON || MathF.Abs(smallest - biggest) < 1e-3f)
                {
                    break;
                }
                if (length < at)
                {
                    smallest = t;
                    t = (t + biggest) * 0.5f;
                }
                else
                {
                    biggest = t;
                    t = (smallest + t) * 0.5f;
                }
            }
            return t;
        }
    }

    // =====================================================================
    //  TvgMath — static helper class
    // =====================================================================
    public static class TvgMath
    {
        // ---- General functions ------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Deg2Rad(float degree)
        {
            return degree * (MathConstants.MATH_PI / 180.0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Rad2Deg(float radian)
        {
            return radian * (180.0f / MathConstants.MATH_PI);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Zero(float a)
        {
            return MathF.Abs(a) <= MathConstants.FLOAT_EPSILON;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equal(float a, float b)
        {
            return Zero(a - b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Clamp<T>(T v, T min, T max) where T : IComparable<T>
        {
            if (v.CompareTo(min) < 0) return min;
            if (v.CompareTo(max) > 0) return max;
            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        // Remez-approximated atan2 (from tvgMath.cpp)
        public static float Atan2(float y, float x)
        {
            if (y == 0.0f && x == 0.0f) return 0.0f;
            var a = MathF.Min(MathF.Abs(x), MathF.Abs(y)) / MathF.Max(MathF.Abs(x), MathF.Abs(y));
            var s = a * a;
            var r = ((-0.0464964749f * s + 0.15931422f) * s - 0.327622764f) * s * a + a;
            if (MathF.Abs(y) > MathF.Abs(x)) r = 1.57079637f - r;
            if (x < 0) r = 3.14159274f - r;
            if (y < 0) return -r;
            return r;
        }

        // Path length (from tvgMath.cpp)
        public static unsafe float Length(PathCommand* cmds, uint cmdsCnt, Point* pts, uint ptsCnt)
        {
            if (ptsCnt < 2) return 0.0f;

            var start = pts;
            var length = 0.0f;

            while (cmdsCnt-- > 0)
            {
                switch (*cmds)
                {
                    case PathCommand.Close:
                        length += PointLength(*(pts - 1), *start);
                        break;
                    case PathCommand.MoveTo:
                        start = pts;
                        ++pts;
                        break;
                    case PathCommand.LineTo:
                        length += PointLength(*(pts - 1), *pts);
                        ++pts;
                        break;
                    case PathCommand.CubicTo:
                        length += new Bezier(*(pts - 1), *pts, *(pts + 1), *(pts + 2)).Length();
                        pts += 3;
                        break;
                }
                ++cmds;
            }
            return length;
        }

        // ---- Matrix functions -------------------------------------------

        public static void Rotate(ref Matrix m, float degree)
        {
            if (degree == 0.0f) return;
            var rad = degree / 180.0f * MathConstants.MATH_PI;
            var cosVal = MathF.Cos(rad);
            var sinVal = MathF.Sin(rad);
            m.e12 = m.e11 * -sinVal;
            m.e11 *= cosVal;
            m.e21 = m.e22 * sinVal;
            m.e22 *= cosVal;
        }

        public static bool Inverse(in Matrix m, out Matrix o)
        {
            var det = m.e11 * (m.e22 * m.e33 - m.e32 * m.e23) -
                      m.e12 * (m.e21 * m.e33 - m.e23 * m.e31) +
                      m.e13 * (m.e21 * m.e32 - m.e22 * m.e31);

            var invDet = 1.0f / det;
            if (float.IsInfinity(invDet))
            {
                o = default;
                return false;
            }

            o.e11 = (m.e22 * m.e33 - m.e32 * m.e23) * invDet;
            o.e12 = (m.e13 * m.e32 - m.e12 * m.e33) * invDet;
            o.e13 = (m.e12 * m.e23 - m.e13 * m.e22) * invDet;
            o.e21 = (m.e23 * m.e31 - m.e21 * m.e33) * invDet;
            o.e22 = (m.e11 * m.e33 - m.e13 * m.e31) * invDet;
            o.e23 = (m.e21 * m.e13 - m.e11 * m.e23) * invDet;
            o.e31 = (m.e21 * m.e32 - m.e31 * m.e22) * invDet;
            o.e32 = (m.e31 * m.e12 - m.e11 * m.e32) * invDet;
            o.e33 = (m.e11 * m.e22 - m.e21 * m.e12) * invDet;
            return true;
        }

        public static bool IsIdentity(in Matrix m)
        {
            return m.e11 == 1.0f && m.e12 == 0.0f && m.e13 == 0.0f &&
                   m.e21 == 0.0f && m.e22 == 1.0f && m.e23 == 0.0f &&
                   m.e31 == 0.0f && m.e32 == 0.0f && m.e33 == 1.0f;
        }

        public static void SetIdentity(out Matrix m)
        {
            m.e11 = 1.0f; m.e12 = 0.0f; m.e13 = 0.0f;
            m.e21 = 0.0f; m.e22 = 1.0f; m.e23 = 0.0f;
            m.e31 = 0.0f; m.e32 = 0.0f; m.e33 = 1.0f;
        }

        public static Matrix Identity()
        {
            return new Matrix(1, 0, 0, 0, 1, 0, 0, 0, 1);
        }

        public static Matrix Multiply(in Matrix lhs, in Matrix rhs)
        {
            Matrix m;
            m.e11 = lhs.e11 * rhs.e11 + lhs.e12 * rhs.e21 + lhs.e13 * rhs.e31;
            m.e12 = lhs.e11 * rhs.e12 + lhs.e12 * rhs.e22 + lhs.e13 * rhs.e32;
            m.e13 = lhs.e11 * rhs.e13 + lhs.e12 * rhs.e23 + lhs.e13 * rhs.e33;
            m.e21 = lhs.e21 * rhs.e11 + lhs.e22 * rhs.e21 + lhs.e23 * rhs.e31;
            m.e22 = lhs.e21 * rhs.e12 + lhs.e22 * rhs.e22 + lhs.e23 * rhs.e32;
            m.e23 = lhs.e21 * rhs.e13 + lhs.e22 * rhs.e23 + lhs.e23 * rhs.e33;
            m.e31 = lhs.e31 * rhs.e11 + lhs.e32 * rhs.e21 + lhs.e33 * rhs.e31;
            m.e32 = lhs.e31 * rhs.e12 + lhs.e32 * rhs.e22 + lhs.e33 * rhs.e32;
            m.e33 = lhs.e31 * rhs.e13 + lhs.e32 * rhs.e23 + lhs.e33 * rhs.e33;
            return m;
        }

        public static bool MatrixEqual(in Matrix lhs, in Matrix rhs)
        {
            return Equal(lhs.e11, rhs.e11) && Equal(lhs.e12, rhs.e12) && Equal(lhs.e13, rhs.e13) &&
                   Equal(lhs.e21, rhs.e21) && Equal(lhs.e22, rhs.e22) && Equal(lhs.e23, rhs.e23) &&
                   Equal(lhs.e31, rhs.e31) && Equal(lhs.e32, rhs.e32) && Equal(lhs.e33, rhs.e33);
        }

        /// <summary>Matrix inequality. Mirrors C++ operator!=(Matrix, Matrix).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool MatrixNotEqual(in Matrix lhs, in Matrix rhs)
        {
            return !MatrixEqual(lhs, rhs);
        }

        /// <summary>Matrix multiply-assign. Mirrors C++ operator*=(Matrix&amp;, Matrix&amp;).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MultiplyAssign(ref Matrix lhs, in Matrix rhs)
        {
            lhs = Multiply(lhs, rhs);
        }

        /// <summary>Nullable-lhs matrix multiply. Mirrors C++ operator*(const Matrix*, const Matrix&amp;).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix MultiplyNullable(in Matrix? lhs, in Matrix rhs)
        {
            return lhs.HasValue ? Multiply(lhs.Value, rhs) : rhs;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Radian(in Matrix m)
        {
            return MathF.Abs(Atan2(m.e21, m.e11));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RightAngle(in Matrix m)
        {
            var rad = Radian(m);
            return Zero(rad) || Zero(rad - MathConstants.MATH_PI2) || Zero(rad - MathConstants.MATH_PI);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Skewed(in Matrix m)
        {
            return !Zero(m.e21 + m.e12);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Scaling(in Matrix m)
        {
            return MathF.Sqrt(m.e11 * m.e11 + m.e21 * m.e21);
        }

        public static void Scale(ref Matrix m, Point p)
        {
            m.e11 *= p.x;
            m.e22 *= p.y;
        }

        public static void ScaleR(ref Matrix m, Point p)
        {
            if (p.x != 1.0f) { m.e11 *= p.x; m.e21 *= p.x; }
            if (p.y != 1.0f) { m.e22 *= p.y; m.e12 *= p.y; }
        }

        public static void Translate(ref Matrix m, Point p)
        {
            m.e13 += p.x;
            m.e23 += p.y;
        }

        public static void TranslateR(ref Matrix m, Point p)
        {
            if (p.x == 0.0f && p.y == 0.0f) return;
            m.e13 += (p.x * m.e11 + p.y * m.e12);
            m.e23 += (p.x * m.e21 + p.y * m.e22);
        }

        // ---- Point functions -------------------------------------------

        /// <summary>Transform a point by a matrix.  Mirrors C++ operator*=(Point, Matrix).</summary>
        public static void TransformInPlace(ref Point pt, in Matrix m)
        {
            var tx = pt.x * m.e11 + pt.y * m.e12 + m.e13;
            var ty = pt.x * m.e21 + pt.y * m.e22 + m.e23;
            pt.x = tx;
            pt.y = ty;
        }

        /// <summary>Transform a point by a matrix.  Mirrors C++ operator*(Point, Matrix).</summary>
        public static Point Transform(Point pt, in Matrix m)
        {
            var tx = pt.x * m.e11 + pt.y * m.e12 + m.e13;
            var ty = pt.x * m.e21 + pt.y * m.e22 + m.e23;
            return new Point(tx, ty);
        }

        /// <summary>Transform-in-place with nullable matrix. Mirrors C++ operator*=(Point&amp;, const Matrix*).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void TransformInPlace(ref Point pt, in Matrix? m)
        {
            if (m.HasValue) TransformInPlace(ref pt, m.Value);
        }

        /// <summary>Transform with nullable matrix. Mirrors C++ operator*(const Point&amp;, const Matrix*).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point Transform(Point pt, in Matrix? m)
        {
            return m.HasValue ? Transform(pt, m.Value) : pt;
        }

        public static Point Normal(Point p1, Point p2)
        {
            var dir = PointSub(p2, p1);
            var len = PointLength(dir);
            if (Zero(len)) return default;
            var unitDir = PointDiv(dir, len);
            return new Point(-unitDir.y, unitDir.x);
        }

        public static void Normalize(ref Point pt)
        {
            if (Zero(pt)) return;
            var ilength = 1.0f / MathF.Sqrt(pt.x * pt.x + pt.y * pt.y);
            pt.x *= ilength;
            pt.y *= ilength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point PointMin(Point lhs, Point rhs)
        {
            return new Point(MathF.Min(lhs.x, rhs.x), MathF.Min(lhs.y, rhs.y));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point PointMax(Point lhs, Point rhs)
        {
            return new Point(MathF.Max(lhs.x, rhs.x), MathF.Max(lhs.y, rhs.y));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Dot(Point lhs, Point rhs)
        {
            return lhs.x * rhs.x + lhs.y * rhs.y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Cross(Point lhs, Point rhs)
        {
            return lhs.x * rhs.y - rhs.x * lhs.y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Zero(Point p)
        {
            return Zero(p.x) && Zero(p.y);
        }

        /// <summary>Approximate length between two points (alpha-max beta-min).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float PointLength(Point a, Point b)
        {
            var dx = b.x - a.x;
            var dy = b.y - a.y;
            if (dx < 0) dx = -dx;
            if (dy < 0) dy = -dy;
            return (dx > dy) ? (dx + 0.375f * dy) : (dy + 0.375f * dx);
        }

        /// <summary>Euclidean length of vector.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float PointLength(Point a)
        {
            return MathF.Sqrt(a.x * a.x + a.y * a.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float PointLength2(Point a)
        {
            return a.x * a.x + a.y * a.y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool PointEqual(Point lhs, Point rhs)
        {
            return Equal(lhs.x, rhs.x) && Equal(lhs.y, rhs.y);
        }

        /// <summary>Point inequality. Mirrors C++ operator!=(Point, Point).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool PointNotEqual(Point lhs, Point rhs)
        {
            return !PointEqual(lhs, rhs);
        }

        // ---- Point arithmetic (mirrors C++ operator overloads) ----------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point PointSub(Point lhs, Point rhs) => new Point(lhs.x - rhs.x, lhs.y - rhs.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point PointSub(Point lhs, float rhs) => new Point(lhs.x - rhs, lhs.y - rhs);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point PointAdd(Point lhs, Point rhs) => new Point(lhs.x + rhs.x, lhs.y + rhs.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point PointAdd(Point lhs, float rhs) => new Point(lhs.x + rhs, lhs.y + rhs);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point PointMul(Point lhs, Point rhs) => new Point(lhs.x * rhs.x, lhs.y * rhs.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point PointMul(Point lhs, float rhs) => new Point(lhs.x * rhs, lhs.y * rhs);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point PointMul(float lhs, Point rhs) => new Point(lhs * rhs.x, lhs * rhs.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point PointDiv(Point lhs, Point rhs) => new Point(lhs.x / rhs.x, lhs.y / rhs.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point PointDiv(Point lhs, float rhs) => new Point(lhs.x / rhs, lhs.y / rhs);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point PointNeg(Point a) => new Point(-a.x, -a.y);

        // ---- Point in-place assign operators (mirrors C++ compound-assign overloads) ----

        /// <summary>Point add-assign. Mirrors C++ operator+=(Point&amp;, Point).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PointAddAssign(ref Point lhs, Point rhs)
        {
            lhs.x += rhs.x;
            lhs.y += rhs.y;
        }

        /// <summary>Point multiply-assign (point-by-point). Mirrors C++ operator*=(Point&amp;, Point).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PointMulAssign(ref Point lhs, Point rhs)
        {
            lhs.x *= rhs.x;
            lhs.y *= rhs.y;
        }

        /// <summary>Point multiply-assign (by scalar). Mirrors C++ operator*=(Point&amp;, float).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PointMulAssign(ref Point lhs, float rhs)
        {
            lhs.x *= rhs;
            lhs.y *= rhs;
        }

        // ---- Orientation ------------------------------------------------

        public static Orientation GetOrientation(Point p1, Point p2, Point p3)
        {
            var val = Cross(PointSub(p2, p1), PointSub(p3, p1));
            if (Zero(val)) return Orientation.Linear;
            return val > 0 ? Orientation.Clockwise : Orientation.CounterClockwise;
        }

        // ---- Closed (proximity check) ----------------------------------
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Closed(Point lhs, Point rhs, float tolerance)
        {
            float dx = lhs.x - rhs.x;
            float dy = lhs.y - rhs.y;
            return (dx * dx + dy * dy) < (tolerance * tolerance);
        }

        // ---- Arc segments count -----------------------------------------
        public static uint ArcSegmentsCnt(float arcAngle, float pixelRadius)
        {
            if (pixelRadius < MathConstants.FLOAT_EPSILON) return 2;
            const float PX_TOLERANCE = 0.25f;
            var segmentAngle = 2.0f * MathF.Sqrt(2.0f * PX_TOLERANCE / pixelRadius);
            return (uint)MathF.Ceiling(MathF.Abs(arcAngle) / segmentAngle) + 1;
        }

        // ---- Interpolation (lerp) --------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Lerp(float start, float end, float t)
        {
            return start + (end - start) * t;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Lerp(byte start, byte end, float t)
        {
            return (byte)Clamp((int)(start + (end - start) * t), 0, 255);
        }

        /// <summary>Integer lerp. Covers the C++ generic template for int types.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Lerp(int start, int end, float t)
        {
            return (int)(start + (end - start) * t);
        }

        /// <summary>Point lerp. Covers the C++ generic template for Point type.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point Lerp(Point start, Point end, float t)
        {
            return new Point(start.x + (end.x - start.x) * t, start.y + (end.y - start.y) * t);
        }

        // ---- Debug logging (mirrors C++ log(Matrix) and log(Point)) ----

        [System.Diagnostics.Conditional("THORVG_LOG")]
        public static void LogMatrix(in Matrix m)
        {
            TvgCommon.TVGLOG("COMMON", "Matrix: [{0} {1} {2}] [{3} {4} {5}] [{6} {7} {8}]",
                m.e11, m.e12, m.e13, m.e21, m.e22, m.e23, m.e31, m.e32, m.e33);
        }

        [System.Diagnostics.Conditional("THORVG_LOG")]
        public static void LogPoint(in Point pt)
        {
            TvgCommon.TVGLOG("COMMON", "Point: [{0} {1}]", pt.x, pt.y);
        }

        // ---- Internal line-length helpers (used by Bezier) -------------

        internal static float LineLength(Point pt1, Point pt2)
        {
            var dx = pt2.x - pt1.x;
            var dy = pt2.y - pt1.y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        internal static float LineLengthApprox(Point pt1, Point pt2)
        {
            var dx = pt2.x - pt1.x;
            var dy = pt2.y - pt1.y;
            if (dx < 0) dx = -dx;
            if (dy < 0) dy = -dy;
            return (dx > dy) ? (dx + dy * 0.375f) : (dy + dx * 0.375f);
        }
    }
}
