// Ported from ThorVG/src/renderer/gpu_engine/tvgGpuCommon.h and tvgGpuCommon.cpp

using System;

namespace ThorVG
{
    /************************************************************************/
    /* GpuCommon — static GPU utility functions                             */
    /************************************************************************/

    public static unsafe class GpuCommon
    {
        /// <summary>
        /// Optimize path in screen space by collapsing zero length lines
        /// and removing unnecessary cubic beziers. Mirrors C++ gpuOptimize().
        /// </summary>
        public static void GpuOptimize(in RenderPath @in, RenderPath @out, in Matrix matrix, out bool thin, out bool skipFill)
        {
            const float PX_TOLERANCE = 0.25f;

            thin = false;
            skipFill = false;
            if (@in.Empty()) return;

            @out.cmds.Clear();
            @out.pts.Clear();
            @out.cmds.Reserve(@in.cmds.count);
            @out.pts.Reserve(@in.pts.count);

            var cmds = @in.cmds.data;
            var cmdCnt = @in.cmds.count;
            var pts = @in.pts.data;

            Point lastOutT = default;
            Point subpathStartT = default;
            Point thinLineStart = default;
            Point thinLineVec = default;
            var drawableSubpathCnt = 0u;
            var thinLineVecLen = 0.0f;
            var thinCandidate = true;
            var thinLineReady = false;
            var subpathOpen = false;
            var subpathHasSegment = false;

            // Local helper: project point onto line (start, start+vec), update maxDist/minT/maxT
            static void Point2Line(in Point point, in Point start, in Point vec, float vecLen, ref float maxDist, ref float minT, ref float maxT)
            {
                var offset = new Point(point.x - start.x, point.y - start.y);
                var dist = MathF.Abs(TvgMath.Cross(vec, offset)) / vecLen;
                if (dist > maxDist) maxDist = dist;
                var t = TvgMath.Dot(offset, vec) / (vecLen * vecLen);
                if (t < minT) minT = t;
                if (t > maxT) maxT = t;
            }

            for (uint i = 0; i < cmdCnt; i++)
            {
                switch (cmds[i])
                {
                    case PathCommand.MoveTo:
                    {
                        // finalizeSubpath
                        if (subpathHasSegment)
                        {
                            ++drawableSubpathCnt;
                            if (drawableSubpathCnt > 1) thinCandidate = false;
                            subpathHasSegment = false;
                        }

                        var ptT = TvgMath.Transform(*pts, matrix);
                        @out.cmds.Push(PathCommand.MoveTo);
                        @out.pts.Push(ptT);
                        lastOutT = ptT;
                        subpathStartT = ptT;
                        subpathOpen = true;
                        pts++;
                        break;
                    }
                    case PathCommand.LineTo:
                    {
                        var startT = lastOutT;
                        var ptT = TvgMath.Transform(*pts, matrix);
                        if (TvgMath.Closed(startT, ptT, PX_TOLERANCE))
                        {
                            pts++;
                            break;
                        }
                        // addLineCmd
                        @out.cmds.Push(PathCommand.LineTo);
                        @out.pts.Push(ptT);
                        lastOutT = ptT;
                        // collectThinSegment(startT, ptT)
                        subpathHasSegment = true;
                        if (thinCandidate)
                        {
                            if (!thinLineReady)
                            {
                                if (!TvgMath.Closed(startT, ptT, PX_TOLERANCE))
                                {
                                    thinLineStart = startT;
                                    thinLineVec = new Point(ptT.x - startT.x, ptT.y - startT.y);
                                    thinLineVecLen = MathF.Sqrt(thinLineVec.x * thinLineVec.x + thinLineVec.y * thinLineVec.y);
                                    if (!TvgMath.Zero(thinLineVecLen)) thinLineReady = true;
                                }
                            }
                            else
                            {
                                // checkThinPoint(startT)
                                var dist0 = MathF.Abs(TvgMath.Cross(thinLineVec, new Point(startT.x - thinLineStart.x, startT.y - thinLineStart.y))) / thinLineVecLen;
                                if (dist0 > PX_TOLERANCE) thinCandidate = false;
                                if (thinCandidate)
                                {
                                    // checkThinPoint(ptT)
                                    var dist1 = MathF.Abs(TvgMath.Cross(thinLineVec, new Point(ptT.x - thinLineStart.x, ptT.y - thinLineStart.y))) / thinLineVecLen;
                                    if (dist1 > PX_TOLERANCE) thinCandidate = false;
                                }
                            }
                        }
                        pts++;
                        break;
                    }
                    case PathCommand.CubicTo:
                    {
                        var ctrl1T = TvgMath.Transform(pts[0], matrix);
                        var ctrl2T = TvgMath.Transform(pts[1], matrix);
                        var endT = TvgMath.Transform(pts[2], matrix);
                        var startT3 = lastOutT;

                        if (!TvgMath.Closed(startT3, endT, PX_TOLERANCE))
                        {
                            // validateCubic
                            var vec3 = new Point(endT.x - startT3.x, endT.y - startT3.y);
                            var vecLen3 = MathF.Sqrt(vec3.x * vec3.x + vec3.y * vec3.y);
                            float maxDist3 = 0.0f;
                            float minT3 = float.MaxValue;
                            float maxT3 = float.MinValue;
                            Point2Line(ctrl1T, startT3, vec3, vecLen3, ref maxDist3, ref minT3, ref maxT3);
                            Point2Line(ctrl2T, startT3, vec3, vecLen3, ref maxDist3, ref minT3, ref maxT3);

                            var flat = maxDist3 <= PX_TOLERANCE;
                            var tEps3 = PX_TOLERANCE / vecLen3;
                            var inSpan = minT3 >= -tEps3 && maxT3 <= 1.0f + tEps3;

                            if (flat && inSpan)
                            {
                                // addLineCmd(startT3, endT)
                                @out.cmds.Push(PathCommand.LineTo);
                                @out.pts.Push(endT);
                                lastOutT = endT;
                                // collectThinSegment(startT3, endT)
                                subpathHasSegment = true;
                                if (thinCandidate)
                                {
                                    if (!thinLineReady)
                                    {
                                        if (!TvgMath.Closed(startT3, endT, PX_TOLERANCE))
                                        {
                                            thinLineStart = startT3;
                                            thinLineVec = new Point(endT.x - startT3.x, endT.y - startT3.y);
                                            thinLineVecLen = MathF.Sqrt(thinLineVec.x * thinLineVec.x + thinLineVec.y * thinLineVec.y);
                                            if (!TvgMath.Zero(thinLineVecLen)) thinLineReady = true;
                                        }
                                    }
                                    else
                                    {
                                        var dist0 = MathF.Abs(TvgMath.Cross(thinLineVec, new Point(startT3.x - thinLineStart.x, startT3.y - thinLineStart.y))) / thinLineVecLen;
                                        if (dist0 > PX_TOLERANCE) thinCandidate = false;
                                        if (thinCandidate)
                                        {
                                            var dist1 = MathF.Abs(TvgMath.Cross(thinLineVec, new Point(endT.x - thinLineStart.x, endT.y - thinLineStart.y))) / thinLineVecLen;
                                            if (dist1 > PX_TOLERANCE) thinCandidate = false;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                @out.cmds.Push(PathCommand.CubicTo);
                                @out.pts.Push(ctrl1T);
                                @out.pts.Push(ctrl2T);
                                @out.pts.Push(endT);
                                lastOutT = endT;
                                subpathHasSegment = true;
                                thinCandidate = false;
                            }
                        }
                        pts += 3;
                        break;
                    }
                    case PathCommand.Close:
                    {
                        if (subpathOpen && !TvgMath.Closed(lastOutT, subpathStartT, PX_TOLERANCE))
                        {
                            // collectThinSegment(lastOutT, subpathStartT)
                            subpathHasSegment = true;
                            if (thinCandidate)
                            {
                                if (!thinLineReady)
                                {
                                    if (!TvgMath.Closed(lastOutT, subpathStartT, PX_TOLERANCE))
                                    {
                                        thinLineStart = lastOutT;
                                        thinLineVec = new Point(subpathStartT.x - lastOutT.x, subpathStartT.y - lastOutT.y);
                                        thinLineVecLen = MathF.Sqrt(thinLineVec.x * thinLineVec.x + thinLineVec.y * thinLineVec.y);
                                        if (!TvgMath.Zero(thinLineVecLen)) thinLineReady = true;
                                    }
                                }
                                else
                                {
                                    var dist0 = MathF.Abs(TvgMath.Cross(thinLineVec, new Point(lastOutT.x - thinLineStart.x, lastOutT.y - thinLineStart.y))) / thinLineVecLen;
                                    if (dist0 > PX_TOLERANCE) thinCandidate = false;
                                    if (thinCandidate)
                                    {
                                        var dist1 = MathF.Abs(TvgMath.Cross(thinLineVec, new Point(subpathStartT.x - thinLineStart.x, subpathStartT.y - thinLineStart.y))) / thinLineVecLen;
                                        if (dist1 > PX_TOLERANCE) thinCandidate = false;
                                    }
                                }
                            }
                        }
                        @out.cmds.Push(PathCommand.Close);
                        lastOutT = subpathStartT;
                        break;
                    }
                    default: break;
                }
            }

            // finalizeSubpath (final)
            if (subpathHasSegment)
            {
                ++drawableSubpathCnt;
                if (drawableSubpathCnt > 1) thinCandidate = false;
            }

            thin = thinCandidate && thinLineReady && (drawableSubpathCnt == 1);
        }

        /// <summary>
        /// Tests whether line segments (p0-p1) and (p2-p3) cross each other.
        /// Mirrors C++ gpuEdgesCross().
        /// </summary>
        public static bool GpuEdgesCross(in Point p0, in Point p1, in Point p2, in Point p3)
        {
            static sbyte OrientSign(in Point a, in Point b, in Point c)
            {
                var value = TvgMath.Cross(TvgMath.PointSub(b, a), TvgMath.PointSub(c, a));
                if (TvgMath.Zero(value)) return 0;
                return value > 0.0f ? (sbyte)1 : (sbyte)-1;
            }

            var s1 = OrientSign(in p0, in p1, in p2) * OrientSign(in p0, in p1, in p3);
            var s2 = OrientSign(in p2, in p3, in p0) * OrientSign(in p2, in p3, in p1);
            return s1 < 0 && s2 < 0;
        }

        /// <summary>
        /// Generate dashed stroke path. Mirrors C++ gpuStrokeDash().
        /// </summary>
        public static bool GpuStrokeDash(RenderShape rs, RenderPath @out, in Matrix? transform)
        {
            if (rs.stroke == null || rs.stroke.dashCount == 0 || rs.stroke.dashLength < RenderHelper.DASH_PATTERN_THRESHOLD) return false;

            @out.cmds.Reserve(20 * rs.path.cmds.count);
            @out.pts.Reserve(20 * rs.path.pts.count);

            var dash = new StrokeDashPath(rs.stroke.dashPattern!, (int)rs.stroke.dashCount, rs.stroke.dashOffset, rs.stroke.dashLength);
            var allowDot = rs.stroke.cap != StrokeCap.Butt;

            if (rs.Trimpath())
            {
                var tpath = new RenderPath();
                if (rs.stroke.trim.Trim(rs.path, tpath))
                    return dash.Gen(tpath, @out, allowDot, transform);
                else
                    return false;
            }
            return dash.Gen(rs.path, @out, allowDot, transform);
        }
    }

    /************************************************************************/
    /* GpuConvexProbe — incremental convexity validator for triangle-fan    */
    /************************************************************************/

    /// <summary>
    /// Conservative triangle-fan safety check.
    /// The fill is emitted as (v0,v1,v2), (v0,v2,v3), (v0,v3,v4), ...
    /// Mirrors C++ GpuConvexProbe.
    /// </summary>
    public struct GpuConvexProbe
    {
        public bool convex;
        sbyte winding;
        Point firstEdge;
        Point prevEdge;
        sbyte prevXDir;
        sbyte prevYDir;
        byte xDirChanges;
        byte yDirChanges;
        byte reversals;
        bool contourHasEdges;

        const byte MaxAxisDirChanges = 3;
        const byte MaxCollinearReversals = 2;

        public static GpuConvexProbe Create()
        {
            return new GpuConvexProbe { convex = true };
        }

        public void NextContour()
        {
            if (contourHasEdges) convex = false;
            ResetContour();
        }

        public void AddEdge(in Point edge)
        {
            if (TvgMath.Zero(edge)) return;

            contourHasEdges = true;
            if (!convex) return;

            if (TvgMath.Zero(firstEdge)) firstEdge = edge;

            UpdateDir(edge.x, ref prevXDir, ref xDirChanges);
            UpdateDir(edge.y, ref prevYDir, ref yDirChanges);
            if (!convex) return;

            if (TvgMath.Zero(prevEdge))
            {
                prevEdge = edge;
                return;
            }

            var turn = TvgMath.Cross(prevEdge, edge);
            if (TvgMath.Zero(turn))
            {
                if (TvgMath.Dot(prevEdge, edge) < 0.0f && ++reversals > MaxCollinearReversals)
                    convex = false;
            }
            else
            {
                var sign = turn > 0.0f ? (sbyte)1 : (sbyte)-1;
                if (winding == 0) winding = sign;
                else if (sign != winding) convex = false;
            }

            prevEdge = edge;
        }

        public void AddContourClose(in Point edge)
        {
            AddEdge(in edge);
            if (convex && !TvgMath.Zero(firstEdge)) AddEdge(in firstEdge);
        }

        void ResetContour()
        {
            firstEdge = default;
            prevEdge = default;
            prevXDir = prevYDir = 0;
            xDirChanges = yDirChanges = 0;
            reversals = 0;
            contourHasEdges = false;
        }

        void UpdateDir(float value, ref sbyte prevDir, ref byte changes)
        {
            sbyte dir = 0;
            if (!TvgMath.Zero(value)) dir = value > 0.0f ? (sbyte)1 : (sbyte)-1;
            if (dir == 0 || !convex) return;

            if (prevDir != 0 && prevDir != dir && ++changes > MaxAxisDirChanges)
            {
                convex = false;
                return;
            }
            prevDir = dir;
        }
    }

    /************************************************************************/
    /* StrokeDashPath — internal helper for gpuStrokeDash                   */
    /************************************************************************/

    internal struct StrokeDashPath
    {
        float[] dashPattern;
        int dashCnt;
        float dashOffset;
        float dashLength;
        float curLen;
        int curIdx;
        Point curPos;
        bool opGap;
        bool move;
        Matrix? transform;
        bool applyTransform;

        const float MIN_CURR_LEN_THRESHOLD = 0.1f;

        public StrokeDashPath(float[] pattern, int count, float offset, float length)
        {
            dashPattern = pattern;
            dashCnt = count;
            dashOffset = offset;
            dashLength = length;
            curLen = 0.0f;
            curIdx = 0;
            curPos = default;
            opGap = false;
            move = true;
            transform = null;
            applyTransform = false;
        }

        Point Map(Point pt)
        {
            return applyTransform ? TvgMath.Transform(pt, transform!.Value) : pt;
        }

        void DashPoint(RenderPath @out, Point p)
        {
            if (move || dashPattern[curIdx] < MathConstants.FLOAT_EPSILON)
            {
                @out.MoveTo(Map(p));
                move = false;
            }
            @out.LineTo(Map(p));
        }

        public unsafe bool Gen(RenderPath @in, RenderPath @out, bool allowDot, in Matrix? transform)
        {
            this.transform = transform;
            this.applyTransform = transform.HasValue && !TvgMath.IsIdentity(transform.Value);

            int idx = 0;
            var offset = dashOffset;
            var gap = false;
            if (!TvgMath.Zero(dashOffset))
            {
                var length = (dashCnt % 2 != 0) ? dashLength * 2 : dashLength;
                offset = offset % length;
                if (offset < 0) offset += length;

                for (uint i = 0; i < (uint)dashCnt * (uint)(dashCnt % 2 + 1); ++i, ++idx)
                {
                    var curPattern = dashPattern[i % (uint)dashCnt];
                    if (offset < curPattern) break;
                    offset -= curPattern;
                    gap = !gap;
                }
                idx = idx % dashCnt;
            }

            var pts = @in.pts.data;
            Point start = default;

            for (uint ci = 0; ci < @in.cmds.count; ci++)
            {
                switch (@in.cmds.data[ci])
                {
                    case PathCommand.Close:
                    {
                        LineTo(@out, start, allowDot);
                        break;
                    }
                    case PathCommand.MoveTo:
                    {
                        // reset the dash state
                        curIdx = idx;
                        curLen = dashPattern[idx] - offset;
                        opGap = gap;
                        move = true;
                        start = curPos = *pts;
                        pts++;
                        break;
                    }
                    case PathCommand.LineTo:
                    {
                        LineTo(@out, *pts, allowDot);
                        pts++;
                        break;
                    }
                    case PathCommand.CubicTo:
                    {
                        CubicTo(@out, pts[0], pts[1], pts[2], allowDot);
                        pts += 3;
                        break;
                    }
                    default: break;
                }
            }
            return true;
        }

        void LineTo(RenderPath @out, Point to, bool allowDot)
        {
            var line = new Line { pt1 = curPos, pt2 = to };
            var diff = new Point(to.x - curPos.x, to.y - curPos.y);
            var len = TvgMath.PointLength(diff);

            if (TvgMath.Zero(len))
            {
                @out.MoveTo(Map(curPos));
            }
            else if (len <= curLen)
            {
                curLen -= len;
                if (!opGap)
                {
                    if (move)
                    {
                        @out.MoveTo(Map(curPos));
                        move = false;
                    }
                    @out.LineTo(Map(line.pt2));
                }
            }
            else
            {
                Line left, right = default;
                while (len - curLen > RenderHelper.DASH_PATTERN_THRESHOLD)
                {
                    if (curLen > 0.0f)
                    {
                        line.Split(curLen, out left, out right);
                        len -= curLen;
                        if (!opGap)
                        {
                            if (move || dashPattern[curIdx] - curLen < MathConstants.FLOAT_EPSILON)
                            {
                                @out.MoveTo(Map(left.pt1));
                                move = false;
                            }
                            @out.LineTo(Map(left.pt2));
                        }
                    }
                    else
                    {
                        if (allowDot && !opGap) DashPoint(@out, line.pt1);
                        right = line;
                    }

                    curIdx = (curIdx + 1) % dashCnt;
                    curLen = dashPattern[curIdx];
                    opGap = !opGap;
                    line = right;
                    curPos = line.pt1;
                    move = true;
                }
                curLen -= len;
                if (!opGap)
                {
                    if (move)
                    {
                        @out.MoveTo(Map(line.pt1));
                        move = false;
                    }
                    @out.LineTo(Map(line.pt2));
                }
                if (curLen < MIN_CURR_LEN_THRESHOLD)
                {
                    curIdx = (curIdx + 1) % dashCnt;
                    curLen = dashPattern[curIdx];
                    opGap = !opGap;
                }
            }
            curPos = to;
        }

        void CubicTo(RenderPath @out, Point cnt1, Point cnt2, Point end, bool allowDot)
        {
            var curve = new Bezier { start = curPos, ctrl1 = cnt1, ctrl2 = cnt2, end = end };
            var len = curve.Length();

            if (TvgMath.Zero(len))
            {
                @out.MoveTo(Map(curPos));
            }
            else if (len <= curLen)
            {
                curLen -= len;
                if (!opGap)
                {
                    if (move)
                    {
                        @out.MoveTo(Map(curPos));
                        move = false;
                    }
                    @out.CubicTo(Map(curve.ctrl1), Map(curve.ctrl2), Map(curve.end));
                }
            }
            else
            {
                Bezier left, right = default;
                while (len - curLen > RenderHelper.DASH_PATTERN_THRESHOLD)
                {
                    if (curLen > 0.0f)
                    {
                        curve.Split(curLen, out left, out right);
                        len -= curLen;
                        if (!opGap)
                        {
                            if (move || dashPattern[curIdx] - curLen < MathConstants.FLOAT_EPSILON)
                            {
                                @out.MoveTo(Map(left.start));
                                move = false;
                            }
                            @out.CubicTo(Map(left.ctrl1), Map(left.ctrl2), Map(left.end));
                        }
                    }
                    else
                    {
                        if (allowDot && !opGap) DashPoint(@out, curve.start);
                        right = curve;
                    }

                    curIdx = (curIdx + 1) % dashCnt;
                    curLen = dashPattern[curIdx];
                    opGap = !opGap;
                    curve = right;
                    curPos = curve.start;
                    move = true;
                }
                curLen -= len;
                if (!opGap)
                {
                    if (move)
                    {
                        @out.MoveTo(Map(curve.start));
                        move = false;
                    }
                    @out.CubicTo(Map(curve.ctrl1), Map(curve.ctrl2), Map(curve.end));
                }
                if (curLen < MIN_CURR_LEN_THRESHOLD)
                {
                    curIdx = (curIdx + 1) % dashCnt;
                    curLen = dashPattern[curIdx];
                    opGap = !opGap;
                }
            }
            curPos = end;
        }
    }
}
