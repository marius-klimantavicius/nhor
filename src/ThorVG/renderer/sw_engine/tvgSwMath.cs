// Ported from ThorVG/src/renderer/sw_engine/tvgSwMath.cpp

using System;
using System.Runtime.CompilerServices;

namespace ThorVG
{
    public static class SwMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float TO_RADIAN(long angle)
        {
            return ((float)angle / 65536.0f) * (MathConstants.MATH_PI / 180.0f);
        }

        public static long mathMean(long angle1, long angle2)
        {
            return angle1 + mathDiff(angle1, angle2) / 2;
        }

        public static int mathCubicAngle(ReadOnlySpan<SwPoint> baseArr, out long angleIn, out long angleMid, out long angleOut)
        {
            var d1 = baseArr[2] - baseArr[3];
            var d2 = baseArr[1] - baseArr[2];
            var d3 = baseArr[0] - baseArr[1];

            angleIn = angleMid = angleOut = 0;

            if (d1.Tiny())
            {
                if (d2.Tiny())
                {
                    if (d3.Tiny())
                    {
                        return -1; // ignoreable
                    }
                    else
                    {
                        angleIn = angleMid = angleOut = mathAtan(d3);
                    }
                }
                else
                {
                    if (d3.Tiny())
                    {
                        angleIn = angleMid = angleOut = mathAtan(d2);
                    }
                    else
                    {
                        angleIn = angleMid = mathAtan(d2);
                        angleOut = mathAtan(d3);
                    }
                }
            }
            else
            {
                if (d2.Tiny())
                {
                    if (d3.Tiny())
                    {
                        angleIn = angleMid = angleOut = mathAtan(d1);
                    }
                    else
                    {
                        angleIn = mathAtan(d1);
                        angleOut = mathAtan(d3);
                        angleMid = mathMean(angleIn, angleOut);
                    }
                }
                else
                {
                    if (d3.Tiny())
                    {
                        angleIn = mathAtan(d1);
                        angleMid = angleOut = mathAtan(d2);
                    }
                    else
                    {
                        angleIn = mathAtan(d1);
                        angleMid = mathAtan(d2);
                        angleOut = mathAtan(d3);
                    }
                }
            }

            var theta1 = Math.Abs(mathDiff(angleIn, angleMid));
            var theta2 = Math.Abs(mathDiff(angleMid, angleOut));

            if ((theta1 < (SwConstants.SW_ANGLE_PI / 8)) && (theta2 < (SwConstants.SW_ANGLE_PI / 8))) return 0; // small size
            return 1;
        }

        public unsafe static int mathCubicAngle(SwPoint* basePtr, out long angleIn, out long angleMid, out long angleOut)
        {
            var span = new ReadOnlySpan<SwPoint>(basePtr, 4);
            return mathCubicAngle(span, out angleIn, out angleMid, out angleOut);
        }

        public static long mathMultiply(long a, long b)
        {
            int s = 1;

            if (a < 0) { a = -a; s = -s; }
            if (b < 0) { b = -b; s = -s; }

            long c = (a * b + 0x8000L) >> 16;
            return (s > 0) ? c : -c;
        }

        public static long mathDivide(long a, long b)
        {
            int s = 1;

            if (a < 0) { a = -a; s = -s; }
            if (b < 0) { b = -b; s = -s; }

            long q = b > 0 ? ((a << 16) + (b >> 1)) / b : 0x7FFFFFFFL;
            return (s < 0 ? -q : q);
        }

        public static long mathMulDiv(long a, long b, long c)
        {
            int s = 1;

            if (a < 0) { a = -a; s = -s; }
            if (b < 0) { b = -b; s = -s; }
            if (c < 0) { c = -c; s = -s; }

            long d = c > 0 ? (a * b + (c >> 1)) / c : 0x7FFFFFFFL;
            return (s > 0 ? d : -d);
        }

        public static void mathRotate(ref SwPoint pt, long angle)
        {
            if (angle == 0 || pt.Zero()) return;

            var v = pt.ToPoint();
            var radian = TO_RADIAN(angle);
            var cosv = MathF.Cos(radian);
            var sinv = MathF.Sin(radian);

            pt.x = (int)MathF.Round(((v.x * cosv - v.y * sinv) * 64.0f), MidpointRounding.ToEven);
            pt.y = (int)MathF.Round(((v.x * sinv + v.y * cosv) * 64.0f), MidpointRounding.ToEven);
        }

        public static long mathTan(long angle)
        {
            if (angle == 0) return 0;
            return (long)(MathF.Tan(TO_RADIAN(angle)) * 65536.0f);
        }

        public static long mathAtan(SwPoint pt)
        {
            if (pt.Zero()) return 0;
            return (long)(TvgMath.Atan2(SwHelper.TO_FLOAT(pt.y), SwHelper.TO_FLOAT(pt.x)) * (180.0f / MathConstants.MATH_PI) * 65536.0f);
        }

        public static long mathSin(long angle)
        {
            if (angle == 0) return 0;
            return mathCos(SwConstants.SW_ANGLE_PI2 - angle);
        }

        public static long mathCos(long angle)
        {
            return (long)(MathF.Cos(TO_RADIAN(angle)) * 65536.0f);
        }

        public static long mathLength(SwPoint pt)
        {
            if (pt.Zero()) return 0;

            // trivial case (guard against MinValue where Math.Abs throws in C#)
            if (pt.x == 0) return pt.y == int.MinValue ? int.MaxValue : Math.Abs(pt.y);
            if (pt.y == 0) return pt.x == int.MinValue ? int.MaxValue : Math.Abs(pt.x);

            var v = pt.ToPoint();

            // approximate sqrt(x*x + y*y) using alpha max plus beta min algorithm
            if (v.x < 0) v.x = -v.x;
            if (v.y < 0) v.y = -v.y;
            return (long)((v.x > v.y) ? (v.x + v.y * 0.375f) : (v.y + v.x * 0.375f));
        }

        public static unsafe void mathSplitCubic(SwPoint* basePtr)
        {
            int a, b, c, d;

            basePtr[6].x = basePtr[3].x;
            c = basePtr[1].x;
            d = basePtr[2].x;
            basePtr[1].x = a = (basePtr[0].x + c) >> 1;
            basePtr[5].x = b = (basePtr[3].x + d) >> 1;
            c = (c + d) >> 1;
            basePtr[2].x = a = (a + c) >> 1;
            basePtr[4].x = b = (b + c) >> 1;
            basePtr[3].x = (a + b) >> 1;

            basePtr[6].y = basePtr[3].y;
            c = basePtr[1].y;
            d = basePtr[2].y;
            basePtr[1].y = a = (basePtr[0].y + c) >> 1;
            basePtr[5].y = b = (basePtr[3].y + d) >> 1;
            c = (c + d) >> 1;
            basePtr[2].y = a = (a + c) >> 1;
            basePtr[4].y = b = (b + c) >> 1;
            basePtr[3].y = (a + b) >> 1;
        }

        public static unsafe void mathSplitLine(SwPoint* basePtr)
        {
            basePtr[2] = basePtr[1];
            basePtr[1] = new SwPoint((basePtr[0].x >> 1) + (basePtr[1].x >> 1),
                                     (basePtr[0].y >> 1) + (basePtr[1].y >> 1));
        }

        public static long mathDiff(long angle1, long angle2)
        {
            var delta = angle2 - angle1;

            delta %= SwConstants.SW_ANGLE_2PI;
            if (delta < 0) delta += SwConstants.SW_ANGLE_2PI;
            if (delta > SwConstants.SW_ANGLE_PI) delta -= SwConstants.SW_ANGLE_2PI;

            return delta;
        }

        public static SwPoint mathTransform(in Point to, in Matrix transform)
        {
            var tx = to.x * transform.e11 + to.y * transform.e12 + transform.e13;
            var ty = to.x * transform.e21 + to.y * transform.e22 + transform.e23;
            return new SwPoint(SwHelper.TO_SWCOORD(tx), SwHelper.TO_SWCOORD(ty));
        }

        public static unsafe bool mathUpdateOutlineBBox(SwOutline* outline, in RenderRegion clipBox, ref RenderRegion renderBox, bool fastTrack)
        {
            if (outline == null || outline->pts.Empty() || outline->cntrs.Empty())
            {
                renderBox.Reset();
                return false;
            }

            var pt = outline->pts.Begin();
            var xMin = pt->x;
            var xMax = pt->x;
            var yMin = pt->y;
            var yMax = pt->y;

            for (++pt; pt < outline->pts.End(); ++pt)
            {
                if (xMin > pt->x) xMin = pt->x;
                if (xMax < pt->x) xMax = pt->x;
                if (yMin > pt->y) yMin = pt->y;
                if (yMax < pt->y) yMax = pt->y;
            }

            if (fastTrack)
            {
                renderBox.min.x = (int)MathF.Round(xMin / 64.0f, MidpointRounding.AwayFromZero);
                renderBox.min.y = (int)MathF.Round(yMin / 64.0f, MidpointRounding.AwayFromZero);
                renderBox.max.x = (int)MathF.Round(xMax / 64.0f, MidpointRounding.AwayFromZero);
                renderBox.max.y = (int)MathF.Round(yMax / 64.0f, MidpointRounding.AwayFromZero);
            }
            else
            {
                renderBox.min.x = xMin >> 6;
                renderBox.min.y = yMin >> 6;
                renderBox.max.x = (xMax + 63) >> 6;
                renderBox.max.y = (yMax + 63) >> 6;
            }

            renderBox.IntersectWith(clipBox);
            return renderBox.Valid();
        }
    }
}
