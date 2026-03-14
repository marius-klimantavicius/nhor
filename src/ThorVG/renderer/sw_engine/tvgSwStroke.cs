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
        private static long SIDE_TO_ROTATE(int s)
        {
            return (SwConstants.SW_ANGLE_PI2 - (long)s * SwConstants.SW_ANGLE_PI);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SCALE(SwStroke stroke, ref SwPoint pt)
        {
            pt.x = (int)(pt.x * stroke.sx);
            pt.y = (int)(pt.y * stroke.sy);
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

        private static void _borderCubicTo(SwStrokeBorder border, SwPoint ctrl1, SwPoint ctrl2, SwPoint to)
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

        private static void _borderArcTo(SwStrokeBorder border, SwPoint center, long radius, long angleStart, long angleDiff, SwStroke stroke)
        {
            long ARC_CUBIC_ANGLE = SwConstants.SW_ANGLE_PI / 2;
            var a = new SwPoint((int)radius, 0);
            SwMath.mathRotate(ref a, angleStart);
            SCALE(stroke, ref a);
            a = a + center;

            var total = angleDiff;
            var angle = angleStart;
            var rotate = (angleDiff >= 0) ? SwConstants.SW_ANGLE_PI2 : -SwConstants.SW_ANGLE_PI2;

            while (total != 0)
            {
                var step = total;
                if (step > ARC_CUBIC_ANGLE) step = ARC_CUBIC_ANGLE;
                else if (step < -ARC_CUBIC_ANGLE) step = -ARC_CUBIC_ANGLE;

                var next = angle + step;
                var theta = step;
                if (theta < 0) theta = -theta;
                theta >>= 1;

                var b = new SwPoint((int)radius, 0);
                SwMath.mathRotate(ref b, next);
                SCALE(stroke, ref b);
                b = b + center;

                var length = SwMath.mathMulDiv(radius, SwMath.mathSin(theta) * 4, (0x10000L + SwMath.mathCos(theta)) * 3);

                var a2 = new SwPoint((int)length, 0);
                SwMath.mathRotate(ref a2, angle + rotate);
                SCALE(stroke, ref a2);
                a2 = a2 + a;

                var b2 = new SwPoint((int)length, 0);
                SwMath.mathRotate(ref b2, next - rotate);
                SCALE(stroke, ref b2);
                b2 = b2 + b;

                _borderCubicTo(border, a2, b2, b);

                a = b;
                total -= step;
                angle = next;
            }
        }

        private static void _borderLineTo(SwStrokeBorder border, SwPoint to, bool movable)
        {
            if (border.movable)
            {
                border.pts.Last() = to;
            }
            else
            {
                if (!border.pts.Empty() && (border.pts.Last() - to).Tiny()) return;
                _growBorder(border, 1);
                border.tags[border.pts.count] = SW_STROKE_TAG_POINT;
                border.pts.Push(to);
            }
            border.movable = movable;
        }

        private static void _borderMoveTo(SwStrokeBorder border, SwPoint to)
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
            var total = SwMath.mathDiff(stroke.angleIn, stroke.angleOut);
            if (total == SwConstants.SW_ANGLE_PI) total = -rotate * 2;
            _borderArcTo(border, stroke.center, stroke.width, stroke.angleIn + rotate, total, stroke);
            border.movable = false;
        }

        private static void _outside(SwStroke stroke, int side, long lineLength)
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
                long phi = 0;
                long thcos = 0;

                if (!bevel)
                {
                    var theta = SwMath.mathDiff(stroke.angleIn, stroke.angleOut);
                    if (theta == SwConstants.SW_ANGLE_PI)
                    {
                        theta = rotate;
                        phi = stroke.angleIn;
                    }
                    else
                    {
                        theta /= 2;
                        phi = stroke.angleIn + theta + rotate;
                    }

                    thcos = SwMath.mathCos(theta);
                    var sigma = SwMath.mathMultiply(stroke.miterlimit, thcos);
                    if (sigma < 0x10000L) bevel = true;
                }

                if (bevel)
                {
                    var delta = new SwPoint((int)stroke.width, 0);
                    SwMath.mathRotate(ref delta, stroke.angleOut + rotate);
                    SCALE(stroke, ref delta);
                    delta = delta + stroke.center;
                    border.movable = false;
                    _borderLineTo(border, delta, false);
                }
                else
                {
                    var length = SwMath.mathDivide(stroke.width, thcos);
                    var delta = new SwPoint((int)length, 0);
                    SwMath.mathRotate(ref delta, phi);
                    SCALE(stroke, ref delta);
                    delta = delta + stroke.center;
                    _borderLineTo(border, delta, false);

                    if (lineLength == 0)
                    {
                        delta = new SwPoint((int)stroke.width, 0);
                        SwMath.mathRotate(ref delta, stroke.angleOut + rotate);
                        SCALE(stroke, ref delta);
                        delta = delta + stroke.center;
                        _borderLineTo(border, delta, false);
                    }
                }
            }
        }

        private static void _inside(SwStroke stroke, int side, long lineLength)
        {
            var border = stroke.borders[side];
            var theta = SwMath.mathDiff(stroke.angleIn, stroke.angleOut) / 2;
            SwPoint delta;
            bool intersect = false;

            if (border.movable && lineLength > 0)
            {
                long minLength = Math.Abs(SwMath.mathMultiply(stroke.width, SwMath.mathTan(theta)));
                if (stroke.lineLength >= minLength && lineLength >= minLength) intersect = true;
            }

            var rotate = SIDE_TO_ROTATE(side);

            if (!intersect)
            {
                delta = new SwPoint((int)stroke.width, 0);
                SwMath.mathRotate(ref delta, stroke.angleOut + rotate);
                SCALE(stroke, ref delta);
                delta = delta + stroke.center;
                border.movable = false;
            }
            else
            {
                var phi = stroke.angleIn + theta;
                var thcos = SwMath.mathCos(theta);
                delta = new SwPoint((int)SwMath.mathDivide(stroke.width, thcos), 0);
                SwMath.mathRotate(ref delta, phi + rotate);
                SCALE(stroke, ref delta);
                delta = delta + stroke.center;
            }

            _borderLineTo(border, delta, false);
        }

        private static void _processCorner(SwStroke stroke, long lineLength)
        {
            var turn = SwMath.mathDiff(stroke.angleIn, stroke.angleOut);
            if (turn == 0) return;

            int inside = 0;
            if (turn < 0) inside = 1;

            _inside(stroke, inside, lineLength);
            _outside(stroke, 1 - inside, lineLength);
        }

        private static void _firstSubPath(SwStroke stroke, long startAngle, long lineLength)
        {
            var delta = new SwPoint((int)stroke.width, 0);
            SwMath.mathRotate(ref delta, startAngle + SwConstants.SW_ANGLE_PI2);
            SCALE(stroke, ref delta);

            var pt = stroke.center + delta;
            _borderMoveTo(stroke.borders[0], pt);

            pt = stroke.center - delta;
            _borderMoveTo(stroke.borders[1], pt);

            stroke.subPathAngle = startAngle;
            stroke.firstPt = false;
            stroke.subPathLineLength = lineLength;
        }

        private static void _lineTo(SwStroke stroke, SwPoint to)
        {
            var delta = to - stroke.center;

            if (delta.Zero())
            {
                if (stroke.firstPt && stroke.cap != StrokeCap.Butt) _firstSubPath(stroke, 0, 0);
                return;
            }

            delta.x = (int)(delta.x / stroke.sx);
            delta.y = (int)(delta.y / stroke.sy);
            var lineLength = SwMath.mathLength(delta);
            var angle = SwMath.mathAtan(delta);

            delta = new SwPoint((int)stroke.width, 0);
            SwMath.mathRotate(ref delta, angle + SwConstants.SW_ANGLE_PI2);
            SCALE(stroke, ref delta);

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
                _borderLineTo(stroke.borders[side], to + delta, true);
                delta.x = -delta.x;
                delta.y = -delta.y;
            }

            stroke.angleIn = angle;
            stroke.center = to;
            stroke.lineLength = lineLength;
        }

        private static void _cubicTo(SwStroke stroke, SwPoint ctrl1, SwPoint ctrl2, SwPoint to)
        {
            var bezStack = stackalloc SwPoint[37];
            var limit = bezStack + 32;
            var arc = bezStack;
            var firstArc = true;
            arc[0] = to;
            arc[1] = ctrl2;
            arc[2] = ctrl1;
            arc[3] = stroke.center;

            while (arc >= bezStack)
            {
                long angleIn, angleOut, angleMid;
                angleIn = angleOut = angleMid = stroke.angleIn;

                var valid = SwMath.mathCubicAngle(arc, out angleIn, out angleMid, out angleOut);

                if (valid > 0 && arc < limit)
                {
                    if (stroke.firstPt) stroke.angleIn = angleIn;
                    SwMath.mathSplitCubic(arc);
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
                else if (Math.Abs(SwMath.mathDiff(stroke.angleIn, angleIn)) > (SwConstants.SW_ANGLE_PI / 8) / 4)
                {
                    stroke.center = arc[3];
                    stroke.angleOut = angleIn;
                    stroke.join = StrokeJoin.Round;
                    _processCorner(stroke, 0);
                    stroke.join = stroke.joinSaved;
                }

                var theta1 = SwMath.mathDiff(angleIn, angleMid) / 2;
                var theta2 = SwMath.mathDiff(angleMid, angleOut) / 2;
                var phi1 = SwMath.mathMean(angleIn, angleMid);
                var phi2 = SwMath.mathMean(angleMid, angleOut);
                var length1 = SwMath.mathDivide(stroke.width, SwMath.mathCos(theta1));
                var length2 = SwMath.mathDivide(stroke.width, SwMath.mathCos(theta2));
                long alpha0 = 0;

                if (stroke.handleWideStrokes)
                {
                    alpha0 = SwMath.mathAtan(arc[0] - arc[3]);
                }

                for (int side = 0; side < 2; ++side)
                {
                    var border = stroke.borders[side];
                    var rotate = SIDE_TO_ROTATE(side);

                    var _ctrl1 = new SwPoint((int)length1, 0);
                    SwMath.mathRotate(ref _ctrl1, phi1 + rotate);
                    SCALE(stroke, ref _ctrl1);
                    _ctrl1 = _ctrl1 + arc[2];

                    var _ctrl2 = new SwPoint((int)length2, 0);
                    SwMath.mathRotate(ref _ctrl2, phi2 + rotate);
                    SCALE(stroke, ref _ctrl2);
                    _ctrl2 = _ctrl2 + arc[1];

                    var end = new SwPoint((int)stroke.width, 0);
                    SwMath.mathRotate(ref end, angleOut + rotate);
                    SCALE(stroke, ref end);
                    end = end + arc[0];

                    if (stroke.handleWideStrokes)
                    {
                        var start = border.pts.Last();
                        var alpha1 = SwMath.mathAtan(end - start);

                        if (Math.Abs(SwMath.mathDiff(alpha0, alpha1)) > SwConstants.SW_ANGLE_PI / 2)
                        {
                            var beta = SwMath.mathAtan(arc[3] - start);
                            var gamma = SwMath.mathAtan(arc[0] - end);
                            var bvec = end - start;
                            var blen = SwMath.mathLength(bvec);
                            var sinA = Math.Abs(SwMath.mathSin(alpha1 - gamma));
                            var sinB = Math.Abs(SwMath.mathSin(beta - gamma));
                            var alen = SwMath.mathMulDiv(blen, sinA, sinB);

                            var dd = new SwPoint((int)alen, 0);
                            SwMath.mathRotate(ref dd, beta);
                            dd = dd + start;

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

        private static void _addCap(SwStroke stroke, long angle, int side)
        {
            if (stroke.cap == StrokeCap.Square)
            {
                var rotate = SIDE_TO_ROTATE(side);
                var border = stroke.borders[side];

                var delta = new SwPoint((int)stroke.width, 0);
                SwMath.mathRotate(ref delta, angle);
                SCALE(stroke, ref delta);

                var delta2 = new SwPoint((int)stroke.width, 0);
                SwMath.mathRotate(ref delta2, angle + rotate);
                SCALE(stroke, ref delta2);
                delta = delta + stroke.center + delta2;
                _borderLineTo(border, delta, false);

                delta = new SwPoint((int)stroke.width, 0);
                SwMath.mathRotate(ref delta, angle);
                SCALE(stroke, ref delta);

                delta2 = new SwPoint((int)stroke.width, 0);
                SwMath.mathRotate(ref delta2, angle - rotate);
                SCALE(stroke, ref delta2);
                delta = delta + delta2 + stroke.center;
                _borderLineTo(border, delta, false);
            }
            else if (stroke.cap == StrokeCap.Round)
            {
                stroke.angleIn = angle;
                stroke.angleOut = angle + SwConstants.SW_ANGLE_PI;
                _arcTo(stroke, side);
            }
            else
            {
                var rotate = SIDE_TO_ROTATE(side);
                var border = stroke.borders[side];

                var delta = new SwPoint((int)stroke.width, 0);
                SwMath.mathRotate(ref delta, angle + rotate);
                SCALE(stroke, ref delta);
                delta = delta + stroke.center;
                _borderLineTo(border, delta, false);

                delta = new SwPoint((int)stroke.width, 0);
                SwMath.mathRotate(ref delta, angle - rotate);
                SCALE(stroke, ref delta);
                delta = delta + stroke.center;
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

        private static void _beginSubPath(SwStroke stroke, SwPoint to, bool closed)
        {
            stroke.firstPt = true;
            stroke.center = to;
            stroke.closedSubPath = closed;

            if ((stroke.join != StrokeJoin.Round) || (!stroke.closedSubPath && stroke.cap == StrokeCap.Butt))
                stroke.handleWideStrokes = true;
            else
                stroke.handleWideStrokes = false;

            stroke.ptStartSubPath = to;
            stroke.angleIn = 0;
        }

        private static void _endSubPath(SwStroke stroke)
        {
            if (stroke.closedSubPath)
            {
                if (stroke.center != stroke.ptStartSubPath)
                    _lineTo(stroke, stroke.ptStartSubPath);

                stroke.angleOut = stroke.subPathAngle;
                var turn = SwMath.mathDiff(stroke.angleIn, stroke.angleOut);

                if (turn != 0)
                {
                    int inside = 0;
                    if (turn < 0) inside = 1;
                    _inside(stroke, inside, stroke.subPathLineLength);
                    _outside(stroke, 1 - inside, stroke.subPathLineLength);
                }

                _borderClose(stroke.borders[0], false);
                _borderClose(stroke.borders[1], true);
            }
            else
            {
                var right = stroke.borders[0];

                _addCap(stroke, stroke.angleIn, 0);
                _addReverseLeft(stroke, true);

                stroke.center = stroke.ptStartSubPath;
                _addCap(stroke, stroke.subPathAngle + SwConstants.SW_ANGLE_PI, 0);

                _borderClose(right, false);
            }
        }

        private static void _exportBorderOutline(SwStroke stroke, SwOutline* outline, uint side)
        {
            var border = stroke.borders[side];
            if (border.pts.Empty()) return;

            var src = border.tags;
            var idx = outline->pts.count;

            for (uint i = 0; i < border.pts.count; i++)
            {
                if ((src[i] & SW_STROKE_TAG_POINT) != 0) outline->types.Push(SwConstants.SW_CURVE_TYPE_POINT);
                else if ((src[i] & SW_STROKE_TAG_CUBIC) != 0) outline->types.Push(SwConstants.SW_CURVE_TYPE_CUBIC);
                if ((src[i] & SW_STROKE_TAG_END) != 0) outline->cntrs.Push(idx);
                ++idx;
            }
            outline->pts.Push(border.pts);
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
            stroke.sx = MathF.Sqrt(transform.e11 * transform.e11 + transform.e21 * transform.e21);
            stroke.sy = MathF.Sqrt(transform.e12 * transform.e12 + transform.e22 * transform.e22);
            stroke.width = SwHelper.HALF_STROKE(rshape.StrokeWidth());
            stroke.cap = rshape.StrokeCap();
            stroke.miterlimit = (long)(rshape.StrokeMiterlimit() * 65536.0f);
            stroke.joinSaved = stroke.join = rshape.StrokeJoin();

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
                var limit = outline.pts.data + last;
                ++i;

                if (last <= first) { first = last + 1; continue; }

                var start = outline.pts[first];
                var pt = outline.pts.data + first;
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
            outline->pts.Reserve(reserve);
            outline->types.Reserve(reserve);
            outline->fillRule = FillRule.NonZero;

            _exportBorderOutline(stroke, outline, 0);
            _exportBorderOutline(stroke, outline, 1);

            return outline;
        }
    }
}
