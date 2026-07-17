// Ported from ThorVG/src/renderer/sw_engine/tvgSwStroke.cpp

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ThorVG
{
    public static unsafe class SwStrokeOps
    {
        private const byte SW_STROKE_TAG_POINT = 1;
        private const byte SW_STROKE_TAG_CUBIC = 2;
        private const byte SW_STROKE_TAG_BEGIN = 4;
        private const byte SW_STROKE_TAG_END = 8;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SIDE_TO_ROTATE(int s)
        {
            return MathConstants.MATH_PI2 - s * MathConstants.MATH_PI;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Point Add(Point a, Point b) => new Point(a.x + b.x, a.y + b.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Point Sub(Point a, Point b) => new Point(a.x - b.x, a.y - b.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool Tiny(Point p)
        {
            const float epsilon = 2.0f / 64.0f;
            return MathF.Abs(p.x) < epsilon && MathF.Abs(p.y) < epsilon;
        }

        private static float Diff(float angle1, float angle2)
        {
            var delta = (angle2 - angle1) % MathConstants.MATH_2PI;
            if (delta < 0) delta += MathConstants.MATH_2PI;
            if (delta > MathConstants.MATH_PI) delta -= MathConstants.MATH_2PI;
            return delta;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Mean(float angle1, float angle2) => angle1 + Diff(angle1, angle2) * 0.5f;

        private static void Rotate(ref Point p, float angle)
        {
            if (TvgMath.Zero(angle)) return;
            var cos = MathF.Cos(angle);
            var sin = MathF.Sin(angle);
            var x = p.x * cos - p.y * sin;
            p.y = p.x * sin + p.y * cos;
            p.x = x;
        }

        private static void _growBorder(SwStrokeBorder border, uint newPts)
        {
            if (border.pts.count + newPts <= border.pts.reserved) return;
            border.pts.Grow(newPts * 20);
            border.tags = (byte*)NativeMemory.Realloc(border.tags, (nuint)border.pts.reserved);
        }

        private static void _borderClose(SwStrokeBorder border, bool reverse)
        {
            var start = border.start;
            var count = (int)border.pts.count;

            if (count <= start + 1)
            {
                border.pts.count = (uint)start;
            }
            else
            {
                border.pts.count = (uint)(--count);
                border.pts[(uint)start] = border.pts[(uint)count];

                if (reverse)
                {
                    var pt1 = border.pts.data + start + 1;
                    var pt2 = border.pts.data + count - 1;
                    while (pt1 < pt2)
                    {
                        var tmp = *pt1; *pt1 = *pt2; *pt2 = tmp;
                        ++pt1; --pt2;
                    }

                    var tag1 = border.tags + start + 1;
                    var tag2 = border.tags + count - 1;
                    while (tag1 < tag2)
                    {
                        var tmp = *tag1; *tag1 = *tag2; *tag2 = tmp;
                        ++tag1; --tag2;
                    }
                }

                border.tags[start] |= SW_STROKE_TAG_BEGIN;
                border.tags[count - 1] |= SW_STROKE_TAG_END;
            }

            border.start = -1;
            border.movable = false;
        }

        private static void _borderCubicTo(SwStrokeBorder border, Point ctrl1, Point ctrl2, Point to)
        {
            _growBorder(border, 3);

            var tag = border.tags + border.pts.count;

            border.pts.Push(ctrl1);
            border.pts.Push(ctrl2);
            border.pts.Push(to);

            tag[0] = SW_STROKE_TAG_CUBIC;
            tag[1] = SW_STROKE_TAG_CUBIC;
            tag[2] = SW_STROKE_TAG_POINT;

            border.movable = false;
        }

        private static void _borderArcTo(SwStrokeBorder border, Point center, float radius, float angleStart, float angleDiff)
        {
            var a = new Point(radius, 0);
            Rotate(ref a, angleStart);
            a = Add(a, center);

            var total = angleDiff;
            var angle = angleStart;
            var rotate = angleDiff >= 0 ? MathConstants.MATH_PI2 : -MathConstants.MATH_PI2;

            while (total != 0)
            {
                var step = total;
                if (step > MathConstants.MATH_PI2) step = MathConstants.MATH_PI2;
                else if (step < -MathConstants.MATH_PI2) step = -MathConstants.MATH_PI2;

                var next = angle + step;
                var theta = step;
                if (theta < 0) theta = -theta;
                theta *= 0.5f;

                var b = new Point(radius, 0);
                Rotate(ref b, next);
                b = Add(b, center);

                var length = radius * (4.0f / 3.0f) * MathF.Tan(theta * 0.5f);

                var a2 = new Point(length, 0);
                Rotate(ref a2, angle + rotate);
                a2 = Add(a2, a);

                var b2 = new Point(length, 0);
                Rotate(ref b2, next - rotate);
                b2 = Add(b2, b);

                _borderCubicTo(border, a2, b2, b);

                a = b;
                total -= step;
                angle = next;
            }
        }

        private static void _borderLineTo(SwStrokeBorder border, Point to, bool movable)
        {
            if (border.movable)
            {
                border.pts.Last() = to;
            }
            else
            {
                if (!border.pts.Empty() && Tiny(Sub(border.pts.Last(), to))) return;
                _growBorder(border, 1);
                border.tags[border.pts.count] = SW_STROKE_TAG_POINT;
                border.pts.Push(to);
            }
            border.movable = movable;
        }

        private static void _borderMoveTo(SwStrokeBorder border, Point to)
        {
            if (border.start >= 0) _borderClose(border, false);
            border.start = (int)border.pts.count;
            border.movable = false;
            _borderLineTo(border, to, false);
        }

        private static void _arcTo(SwStroke stroke, int side)
        {
            var border = stroke.borders[side];
            var rotate = SIDE_TO_ROTATE(side);
            var total = Diff(stroke.angleIn, stroke.angleOut);
            if (TvgMath.Equal(total, MathConstants.MATH_PI)) total = -rotate * 2;
            _borderArcTo(border, stroke.center, stroke.width, stroke.angleIn + rotate, total);
            border.movable = false;
        }

        private static void _outside(SwStroke stroke, int side, float lineLength)
        {
            var border = stroke.borders[side];

            if (stroke.join == StrokeJoin.Round)
            {
                _arcTo(stroke, side);
            }
            else
            {
                var rotate = SIDE_TO_ROTATE(side);
                var bevel = stroke.join == StrokeJoin.Bevel;
                float phi = 0;
                float thcos = 0;

                if (!bevel)
                {
                    var theta = Diff(stroke.angleIn, stroke.angleOut);
                    if (TvgMath.Equal(theta, MathConstants.MATH_PI))
                    {
                        theta = rotate;
                        phi = stroke.angleIn;
                    }
                    else
                    {
                        theta /= 2;
                        phi = stroke.angleIn + theta + rotate;
                    }

                    thcos = MathF.Cos(theta);
                    if (stroke.miterlimit * thcos < 1.0f) bevel = true;
                }

                if (bevel)
                {
                    var delta = new Point(stroke.width, 0);
                    Rotate(ref delta, stroke.angleOut + rotate);
                    delta = Add(delta, stroke.center);
                    border.movable = false;
                    _borderLineTo(border, delta, false);
                }
                else
                {
                    var delta = new Point(stroke.width / thcos, 0);
                    Rotate(ref delta, phi);
                    delta = Add(delta, stroke.center);
                    _borderLineTo(border, delta, false);

                    if (lineLength == 0)
                    {
                        delta = new Point(stroke.width, 0);
                        Rotate(ref delta, stroke.angleOut + rotate);
                        delta = Add(delta, stroke.center);
                        _borderLineTo(border, delta, false);
                    }
                }
            }
        }

        private static void _inside(SwStroke stroke, int side, float lineLength)
        {
            var border = stroke.borders[side];
            var theta = Diff(stroke.angleIn, stroke.angleOut) * 0.5f;
            Point delta;
            bool intersect = false;

            if (border.movable && lineLength > 0)
            {
                var minLength = MathF.Abs(stroke.width * MathF.Tan(theta));
                if (stroke.length >= minLength && lineLength >= minLength) intersect = true;
            }

            var rotate = SIDE_TO_ROTATE(side);

            if (!intersect)
            {
                delta = new Point(stroke.width, 0);
                Rotate(ref delta, stroke.angleOut + rotate);
                delta = Add(delta, stroke.center);
                border.movable = false;
            }
            else
            {
                var phi = stroke.angleIn + theta;
                var thcos = MathF.Cos(theta);
                delta = new Point(stroke.width / thcos, 0);
                Rotate(ref delta, phi + rotate);
                delta = Add(delta, stroke.center);
            }

            _borderLineTo(border, delta, false);
        }

        private static void _processCorner(SwStroke stroke, float lineLength)
        {
            var turn = Diff(stroke.angleIn, stroke.angleOut);
            if (TvgMath.Zero(turn)) return;

            int inside = 0;
            if (turn < 0) inside = 1;

            _inside(stroke, inside, lineLength);
            _outside(stroke, 1 - inside, lineLength);
        }

        private static void _firstSubPath(SwStroke stroke, float startAngle, float lineLength)
        {
            var delta = new Point(stroke.width, 0);
            Rotate(ref delta, startAngle + MathConstants.MATH_PI2);

            var pt = Add(stroke.center, delta);
            _borderMoveTo(stroke.borders[0], pt);

            pt = Sub(stroke.center, delta);
            _borderMoveTo(stroke.borders[1], pt);

            stroke.subPathAngle = startAngle;
            stroke.firstPt = false;
            stroke.subPathLength = lineLength;
        }

        private static void _lineTo(SwStroke stroke, Point to)
        {
            var delta = Sub(to, stroke.center);

            if (TvgMath.Zero(delta))
            {
                if (stroke.firstPt && stroke.cap != StrokeCap.Butt) _firstSubPath(stroke, 0, 0);
                return;
            }

            var lineLength = TvgMath.PointLength(delta);
            var angle = TvgMath.Atan2(delta.y, delta.x);

            delta = new Point(stroke.width, 0);
            Rotate(ref delta, angle + MathConstants.MATH_PI2);

            if (stroke.firstPt)
            {
                _firstSubPath(stroke, angle, lineLength);
            }
            else
            {
                stroke.angleOut = angle;
                _processCorner(stroke, lineLength);
            }

            for (int side = 0; side < 2; ++side)
            {
                _borderLineTo(stroke.borders[side], Add(to, delta), true);
                delta.x = -delta.x;
                delta.y = -delta.y;
            }

            stroke.angleIn = angle;
            stroke.center = to;
            stroke.length = lineLength;
        }

        private static int _cubicAngle(Point* p, out float angleIn, out float angleMid, out float angleOut)
        {
            var d1 = Sub(p[2], p[3]);
            var d2 = Sub(p[1], p[2]);
            var d3 = Sub(p[0], p[1]);
            angleIn = angleMid = angleOut = 0;
            if (Tiny(d1))
            {
                if (Tiny(d2))
                {
                    if (Tiny(d3)) return -1;
                    angleIn = angleMid = angleOut = TvgMath.Atan2(d3.y, d3.x);
                }
                else if (Tiny(d3)) angleIn = angleMid = angleOut = TvgMath.Atan2(d2.y, d2.x);
                else { angleIn = angleMid = TvgMath.Atan2(d2.y, d2.x); angleOut = TvgMath.Atan2(d3.y, d3.x); }
            }
            else if (Tiny(d2))
            {
                if (Tiny(d3)) angleIn = angleMid = angleOut = TvgMath.Atan2(d1.y, d1.x);
                else { angleIn = TvgMath.Atan2(d1.y, d1.x); angleOut = TvgMath.Atan2(d3.y, d3.x); angleMid = Mean(angleIn, angleOut); }
            }
            else if (Tiny(d3))
            {
                angleIn = TvgMath.Atan2(d1.y, d1.x); angleMid = angleOut = TvgMath.Atan2(d2.y, d2.x);
            }
            else
            {
                angleIn = TvgMath.Atan2(d1.y, d1.x); angleMid = TvgMath.Atan2(d2.y, d2.x); angleOut = TvgMath.Atan2(d3.y, d3.x);
            }
            return MathF.Abs(Diff(angleIn, angleMid)) < MathConstants.MATH_PI / 8 && MathF.Abs(Diff(angleMid, angleOut)) < MathConstants.MATH_PI / 8 ? 0 : 1;
        }

        private static void _splitCubic(Point* p)
        {
            var p01 = new Point((p[0].x + p[1].x) * 0.5f, (p[0].y + p[1].y) * 0.5f);
            var p12 = new Point((p[1].x + p[2].x) * 0.5f, (p[1].y + p[2].y) * 0.5f);
            var p23 = new Point((p[2].x + p[3].x) * 0.5f, (p[2].y + p[3].y) * 0.5f);
            var p012 = new Point((p01.x + p12.x) * 0.5f, (p01.y + p12.y) * 0.5f);
            var p123 = new Point((p12.x + p23.x) * 0.5f, (p12.y + p23.y) * 0.5f);
            p[6] = p[3]; p[1] = p01; p[2] = p012;
            p[3] = new Point((p012.x + p123.x) * 0.5f, (p012.y + p123.y) * 0.5f);
            p[4] = p123; p[5] = p23;
        }

        private static void _cubicTo(SwStroke stroke, Point ctrl1, Point ctrl2, Point to)
        {
            var bezStack = stackalloc Point[37];
            var limit = bezStack + 32;
            var arc = bezStack;
            var firstArc = true;
            arc[0] = to;
            arc[1] = ctrl2;
            arc[2] = ctrl1;
            arc[3] = stroke.center;
            var join = stroke.join;

            while (arc >= bezStack)
            {
                float angleIn, angleOut, angleMid;
                angleIn = angleOut = angleMid = stroke.angleIn;

                var valid = _cubicAngle(arc, out angleIn, out angleMid, out angleOut);

                if (valid > 0 && arc < limit)
                {
                    if (stroke.firstPt) stroke.angleIn = angleIn;
                    _splitCubic(arc);
                    arc += 3;
                    continue;
                }

                if (valid < 0 && arc == bezStack)
                {
                    stroke.center = to;
                    if (stroke.firstPt && stroke.cap != StrokeCap.Butt) _firstSubPath(stroke, 0, 0);
                    return;
                }

                if (firstArc)
                {
                    firstArc = false;
                    if (stroke.firstPt)
                    {
                        _firstSubPath(stroke, angleIn, 0);
                    }
                    else
                    {
                        stroke.angleOut = angleIn;
                        _processCorner(stroke, 0);
                    }
                }
                else if (MathF.Abs(Diff(stroke.angleIn, angleIn)) > (MathConstants.MATH_PI / 8) / 4)
                {
                    stroke.center = arc[3];
                    stroke.angleOut = angleIn;
                    stroke.join = StrokeJoin.Round;
                    _processCorner(stroke, 0);
                    stroke.join = join;
                }

                var theta1 = Diff(angleIn, angleMid) * 0.5f;
                var theta2 = Diff(angleMid, angleOut) * 0.5f;
                var phi1 = Mean(angleIn, angleMid);
                var phi2 = Mean(angleMid, angleOut);
                var length1 = stroke.width / MathF.Cos(theta1);
                var length2 = stroke.width / MathF.Cos(theta2);
                var alpha0 = 0.0f;

                if (stroke.handleWideStrokes)
                {
                    var v = Sub(arc[0], arc[3]);
                    alpha0 = TvgMath.Atan2(v.y, v.x);
                }

                for (int side = 0; side < 2; ++side)
                {
                    var border = stroke.borders[side];
                    var rotate = SIDE_TO_ROTATE(side);

                    var _ctrl1 = new Point(length1, 0);
                    Rotate(ref _ctrl1, phi1 + rotate);
                    _ctrl1 = Add(_ctrl1, arc[2]);

                    var _ctrl2 = new Point(length2, 0);
                    Rotate(ref _ctrl2, phi2 + rotate);
                    _ctrl2 = Add(_ctrl2, arc[1]);

                    var end = new Point(stroke.width, 0);
                    Rotate(ref end, angleOut + rotate);
                    end = Add(end, arc[0]);

                    if (stroke.handleWideStrokes)
                    {
                        var start = border.pts.Last();
                        var direction = Sub(end, start);
                        var alpha1 = TvgMath.Atan2(direction.y, direction.x);

                        if (MathF.Abs(Diff(alpha0, alpha1)) > MathConstants.MATH_PI2)
                        {
                            var betaVector = Sub(arc[3], start);
                            var gammaVector = Sub(arc[0], end);
                            var beta = TvgMath.Atan2(betaVector.y, betaVector.x);
                            var gamma = TvgMath.Atan2(gammaVector.y, gammaVector.x);
                            var blen = TvgMath.PointLength(Sub(end, start));
                            var alen = blen * MathF.Abs(MathF.Sin(alpha1 - gamma)) / MathF.Abs(MathF.Sin(beta - gamma));

                            var dd = new Point(alen, 0);
                            Rotate(ref dd, beta);
                            dd = Add(dd, start);

                            border.movable = false;
                            _borderLineTo(border, dd, false);
                            _borderLineTo(border, end, false);
                            _borderCubicTo(border, _ctrl2, _ctrl1, start);
                            _borderLineTo(border, end, false);
                            continue;
                        }
                    }
                    _borderCubicTo(border, _ctrl1, _ctrl2, end);
                }
                arc -= 3;
                stroke.angleIn = angleOut;
            }
            stroke.center = to;
        }

        private static void _addCap(SwStroke stroke, float angle, int side)
        {
            if (stroke.cap == StrokeCap.Square)
            {
                var rotate = SIDE_TO_ROTATE(side);
                var border = stroke.borders[side];

                var delta = new Point(stroke.width, 0);
                Rotate(ref delta, angle);

                var delta2 = new Point(stroke.width, 0);
                Rotate(ref delta2, angle + rotate);
                delta = Add(Add(delta, stroke.center), delta2);
                _borderLineTo(border, delta, false);

                delta = new Point(stroke.width, 0);
                Rotate(ref delta, angle);

                delta2 = new Point(stroke.width, 0);
                Rotate(ref delta2, angle - rotate);
                delta = Add(Add(delta, delta2), stroke.center);
                _borderLineTo(border, delta, false);
            }
            else if (stroke.cap == StrokeCap.Round)
            {
                stroke.angleIn = angle;
                stroke.angleOut = angle + MathConstants.MATH_PI;
                _arcTo(stroke, side);
            }
            else
            {
                var rotate = SIDE_TO_ROTATE(side);
                var border = stroke.borders[side];

                var delta = new Point(stroke.width, 0);
                Rotate(ref delta, angle + rotate);
                delta = Add(delta, stroke.center);
                _borderLineTo(border, delta, false);

                delta = new Point(stroke.width, 0);
                Rotate(ref delta, angle - rotate);
                delta = Add(delta, stroke.center);
                _borderLineTo(border, delta, false);
            }
        }

        private static void _addReverseLeft(SwStroke stroke, bool opened)
        {
            var right = stroke.borders[0];
            var left = stroke.borders[1];
            var newPts = (int)left.pts.count - left.start;

            if (newPts <= 0) return;

            _growBorder(right, (uint)newPts);

            var dstTag = right.tags + right.pts.count;
            var srcPt = left.pts.End() - 1;
            var srcTag = left.tags + left.pts.count - 1;

            while (srcPt >= left.pts.data + left.start)
            {
                right.pts.Push(*srcPt);
                *dstTag = *srcTag;

                if (opened)
                {
                    dstTag[0] &= unchecked((byte)~(SW_STROKE_TAG_BEGIN | SW_STROKE_TAG_END));
                }
                else
                {
                    var ttag = (byte)(dstTag[0] & (SW_STROKE_TAG_BEGIN | SW_STROKE_TAG_END));
                    if (ttag == SW_STROKE_TAG_BEGIN || ttag == SW_STROKE_TAG_END)
                        dstTag[0] ^= (byte)(SW_STROKE_TAG_BEGIN | SW_STROKE_TAG_END);
                }
                --srcPt;
                --srcTag;
                ++dstTag;
            }

            left.pts.count = (uint)left.start;
            right.movable = false;
            left.movable = false;
        }

        private static void _beginSubPath(SwStroke stroke, Point to, bool closed)
        {
            stroke.firstPt = true;
            stroke.center = to;
            stroke.closedSubPath = closed;

            if ((stroke.join != StrokeJoin.Round) || (!stroke.closedSubPath && stroke.cap == StrokeCap.Butt))
                stroke.handleWideStrokes = true;
            else
                stroke.handleWideStrokes = false;

            stroke.subPathStart = to;
            stroke.angleIn = 0;
        }

        private static void _endSubPath(SwStroke stroke)
        {
            if (stroke.closedSubPath)
            {
                if (TvgMath.PointNotEqual(stroke.center, stroke.subPathStart))
                    _lineTo(stroke, stroke.subPathStart);

                stroke.angleOut = stroke.subPathAngle;
                var turn = Diff(stroke.angleIn, stroke.angleOut);

                if (turn != 0)
                {
                    int inside = 0;
                    if (turn < 0) inside = 1;
                    _inside(stroke, inside, stroke.subPathLength);
                    _outside(stroke, 1 - inside, stroke.subPathLength);
                }

                _borderClose(stroke.borders[0], false);
                _borderClose(stroke.borders[1], true);
            }
            else
            {
                var right = stroke.borders[0];

                _addCap(stroke, stroke.angleIn, 0);
                _addReverseLeft(stroke, true);

                stroke.center = stroke.subPathStart;
                _addCap(stroke, stroke.subPathAngle + MathConstants.MATH_PI, 0);

                _borderClose(right, false);
            }
        }

        private static void _exportBorderOutline(SwStroke stroke, SwOutline* outline, uint side)
        {
            var border = stroke.borders[side];
            if (border.pts.Empty()) return;

            var src = border.tags;
            var idx = outline->input.count;

            for (uint i = 0; i < border.pts.count; i++)
            {
                if ((src[i] & SW_STROKE_TAG_POINT) != 0) outline->types.Push(SwConstants.SW_CURVE_TYPE_POINT);
                else if ((src[i] & SW_STROKE_TAG_CUBIC) != 0) outline->types.Push(SwConstants.SW_CURVE_TYPE_CUBIC);
                if ((src[i] & SW_STROKE_TAG_END) != 0) outline->cntrs.Push(idx);
                ++idx;
            }
            outline->input.Push(border.pts);
        }

        // Public API

        public static void strokeFree(SwStroke? stroke)
        {
            if (stroke == null) return;
            if (stroke.fill != null)
            {
                stroke.fill = null;
            }
        }

        public static void strokeReset(SwStroke stroke, RenderShape rshape, in Matrix transform, SwMpool mpool, uint tid)
        {
            stroke.width = rshape.StrokeWidth() * 0.5f;
            stroke.cap = rshape.StrokeCap();
            stroke.miterlimit = rshape.StrokeMiterlimit();
            stroke.join = rshape.StrokeJoin();

            stroke.borders[0] = mpool.StrokeLBorder(tid);
            stroke.borders[1] = mpool.StrokeRBorder(tid);
        }

        public static bool strokeParseOutline(SwStroke stroke, in SwOutline outline, SwMpool mpool, uint tid)
        {
            uint first = 0;
            uint i = 0;

            for (uint ci = 0; ci < outline.cntrs.count; ci++)
            {
                var last = outline.cntrs[ci];
                var limit = outline.input.data + last;
                ++i;

                if (last <= first) { first = last + 1; continue; }

                var start = outline.input[first];
                var pt = outline.input.data + first;
                var types = outline.types.data + first;
                var type = types[0];

                if (type == SwConstants.SW_CURVE_TYPE_CUBIC) return false;
                ++types;

                var closed = outline.closed.data != null ? outline.closed.data[i - 1] : false;

                _beginSubPath(stroke, start, closed);

                while (pt < limit)
                {
                    if (types[0] == SwConstants.SW_CURVE_TYPE_POINT)
                    {
                        ++pt;
                        ++types;
                        _lineTo(stroke, *pt);
                    }
                    else
                    {
                        pt += 3;
                        types += 3;
                        if (pt <= limit) _cubicTo(stroke, pt[-2], pt[-1], pt[0]);
                        else if (pt - 1 == limit) _cubicTo(stroke, pt[-2], pt[-1], start);
                        else goto close;
                    }
                }
            close:
                if (!stroke.firstPt) _endSubPath(stroke);
                first = last + 1;
            }
            return true;
        }

        public static SwOutline* strokeExportOutline(SwStroke stroke, SwMpool mpool, uint tid)
        {
            var reserve = stroke.borders[0].pts.count + stroke.borders[1].pts.count;
            var outline = mpool.Outline(tid);
            outline->input.Reserve(reserve);
            outline->types.Reserve(reserve);
            outline->fillRule = FillRule.NonZero;

            _exportBorderOutline(stroke, outline, 0);
            _exportBorderOutline(stroke, outline, 1);

            return outline;
        }
    }
}
