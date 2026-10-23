// Ported from ThorVG/src/renderer/gpu_engine/tvgGpuCommon.h and tvgGpuCommon.cpp
using System;
using System.Runtime.InteropServices;

namespace ThorVG
{
    /************************************************************************/
    /* GpuCommon — static GPU utility functions                             */
    /************************************************************************/
    public static unsafe class GpuCommon
    {
        public static Matrix GpuConicTransform(in Point center, float angle)
        {
            float radian = TvgMath.Deg2Rad(angle);
            float c = MathF.Cos(radian), s = MathF.Sin(radian);
            // ARM FNMADD negates both operands before the addition. Negating
            // the final sum instead changes the sign of an exact zero.
            var translationX = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? TvgMath.MultiplyAdd(-c, center.x, -(s * center.y)) : -(c * center.x + s * center.y);
            return new Matrix
            {
                e11 = c,
                e12 = s,
                e13 = translationX,
                e21 = -s,
                e22 = c,
                e23 = TvgMath.MultiplyAdd(s, center.x, -c * center.y),
                e31 = 0,
                e32 = 0,
                e33 = 1
            };
        }

        private struct ThinPathTracker
        {
            private const float Tolerance = 0.25f;
            private const float CoverageQuantum = 1.0f / 256.0f;
            private const float MaxPixelSpan = 1.41421356237f;
            private InlineBuffer8<Point> preAxisPoints;
            private Array<Point> preAxisOverflow;
            private uint preAxisCount;
            private Point axisStart;
            private Point axisVec;
            private float axisLen;
            private float axisLenInv;
            private float axisLenSqInv;
            private float minT;
            private float maxT;
            private float minDist;
            private float maxDist;
            public bool ready;
            public bool candidate;
            public ThinPathTracker()
            {
                this = default;
                candidate = true;
            }

            public void Dispose() => preAxisOverflow.Dispose();
            private void ClearPreAxisPoints()
            {
                preAxisCount = 0;
                preAxisOverflow.Clear();
            }

            private void Remember(in Point point)
            {
                if (!candidate || ready)
                    return;
                if (preAxisCount < 8)
                    preAxisPoints[(int)preAxisCount++] = point;
                else
                    preAxisOverflow.Push(point);
            }

            public void Disable()
            {
                candidate = false;
                ready = false;
                ClearPreAxisPoints();
            }

            public void TrackLine(in Point start, in Point end, bool closed)
            {
                if (!candidate)
                    return;
                if (!ready)
                {
                    if (closed)
                        Remember(start);
                    else
                        InitAxis(start, end);
                    return;
                }

                Update(end);
            }

            public void TrackClosedCubic(in Point start, in Point ctrl1, in Point ctrl2, in Point end)
            {
                if (!candidate)
                    return;
                if (!ready)
                {
                    Remember(start);
                    Remember(ctrl1);
                    Remember(ctrl2);
                    return;
                }

                Update(ctrl1);
                Update(ctrl2);
                Update(end);
            }

            public void TrackFlatCubic(in Point start, in Point ctrl1, in Point ctrl2, in Point end)
            {
                if (!candidate)
                    return;
                if (!ready)
                    InitAxis(start, end);
                else
                    Update(end);
                Update(ctrl1);
                Update(ctrl2);
            }

            public void TrackClose(in Point start, in Point end, bool closed)
            {
                if (!candidate)
                    return;
                if (!ready)
                {
                    if (closed)
                        Remember(start);
                    else
                        InitAxis(start, end);
                    return;
                }

                Update(end);
            }

            public bool TooThin()
            {
                var span = (maxT - minT) * axisLen;
                var thickness = maxDist - minDist;
                return thickness * MathF.Min(span, MaxPixelSpan) < CoverageQuantum;
            }

            private void InitAxis(in Point start, in Point end)
            {
                axisStart = start;
                axisVec = TvgMath.PointSub(end, start);
                var lenSq = TvgMath.Dot(axisVec, axisVec);
                axisLen = MathF.Sqrt(lenSq);
                axisLenInv = 1.0f / axisLen;
                axisLenSqInv = 1.0f / lenSq;
                minT = minDist = maxDist = 0.0f;
                maxT = 1.0f;
                ready = true;
                for (uint i = 0; i < preAxisCount && candidate; ++i)
                    Update(preAxisPoints[(int)i]);
                for (uint i = 0; i < preAxisOverflow.count && candidate; ++i)
                    Update(preAxisOverflow[i]);
                ClearPreAxisPoints();
            }

            private void Update(in Point point)
            {
                var offset = TvgMath.PointSub(point, axisStart);
                var signedDist = TvgMath.Cross(axisVec, offset) * axisLenInv;
                if (MathF.Abs(signedDist) > Tolerance)
                {
                    Disable();
                    return;
                }

                var t = TvgMath.Dot(offset, axisVec) * axisLenSqInv;
                if (t < minT)
                    minT = t;
                if (t > maxT)
                    maxT = t;
                if (signedDist < minDist)
                    minDist = signedDist;
                if (signedDist > maxDist)
                    maxDist = signedDist;
            }
        }

        /// <summary>
        /// Optimize path in screen space by collapsing zero length lines
        /// and removing unnecessary cubic beziers. Mirrors C++ gpuOptimize().
        /// </summary>
        public static void GpuOptimize(in RenderPath @in, RenderPath @out, RenderPath? localOut, in Matrix matrix, out bool thin, out bool skipFill)
        {
            const float Tolerance = 0.25f;
            thin = false;
            skipFill = false;
            @out.Clear();
            localOut?.Clear();
            if (@in.Empty())
                return;
            @out.cmds.Reserve(@in.cmds.count);
            @out.pts.Reserve(@in.pts.count);
            localOut?.cmds.Reserve(@in.cmds.count);
            localOut?.pts.Reserve(@in.pts.count);
            var pts = @in.pts.data;
            Point lastOutT = default, lastInT = default, subpathStartT = default;
            uint drawableSubpathCnt = 0;
            bool subpathOpen = false, subpathHasSegment = false;
            var tracker = new ThinPathTracker();
            static void FinalizeSubpath(ref bool subpathHasSegment, ref uint drawableSubpathCnt, ref ThinPathTracker tracker)
            {
                if (!subpathHasSegment)
                    return;
                if (++drawableSubpathCnt > 1)
                    tracker.Disable();
                subpathHasSegment = false;
            }

            static void ValidateCubic(in Point start, in Point ctrl1, in Point ctrl2, in Point end, out float maxDist, out float minT, out float maxT, out float vecLen)
            {
                var vec = TvgMath.PointSub(end, start);
                vecLen = MathF.Sqrt(TvgMath.MultiplyAdd(vec.x, vec.x, vec.y * vec.y));
                var vecLenInv = 1.0f / vecLen;
                var vecLenSqInv = vecLenInv * vecLenInv;
                maxDist = 0.0f;
                minT = float.MaxValue;
                maxT = 1.17549435E-38f; // C++ FLT_MIN: smallest positive normal.
                Point2Line(ctrl1, start, vec, vecLenInv, vecLenSqInv, ref maxDist, ref minT, ref maxT);
                Point2Line(ctrl2, start, vec, vecLenInv, vecLenSqInv, ref maxDist, ref minT, ref maxT);
            }

            static void Point2Line(in Point point, in Point start, in Point vec, float vecLenInv, float vecLenSqInv, ref float maxDist, ref float minT, ref float maxT)
            {
                var offset = TvgMath.PointSub(point, start);
                var dist = MathF.Abs(TvgMath.Cross(vec, offset)) * vecLenInv;
                if (dist > maxDist)
                    maxDist = dist;
                var t = TvgMath.Dot(offset, vec) * vecLenSqInv;
                if (t < minT)
                    minT = t;
                if (t > maxT)
                    maxT = t;
            }

            static void AddLine(RenderPath @out, RenderPath? localOut, in Point local, in Point transformed, ref Point lastOutT)
            {
                @out.LineTo(transformed);
                localOut?.LineTo(local);
                lastOutT = transformed;
            }

            try
            {
                for (uint i = 0; i < @in.cmds.count; ++i)
                {
                    switch (@in.cmds[i])
                    {
                        case PathCommand.MoveTo:
                        {
                            FinalizeSubpath(ref subpathHasSegment, ref drawableSubpathCnt, ref tracker);
                            var point = *pts++;
                            var transformed = TvgMath.Transform(point, matrix);
                            @out.MoveTo(transformed);
                            localOut?.MoveTo(point);
                            lastOutT = lastInT = subpathStartT = transformed;
                            subpathOpen = true;
                            break;
                        }

                        case PathCommand.LineTo:
                        {
                            var point = *pts;
                            var transformed = TvgMath.Transform(point, matrix);
                            var closedIn = TvgMath.Closed(lastInT, transformed, Tolerance);
                            if (!closedIn)
                                subpathHasSegment = true;
                            tracker.TrackLine(lastInT, transformed, closedIn);
                            lastInT = transformed;
                            if (!TvgMath.Closed(lastOutT, transformed, Tolerance))
                                AddLine(@out, localOut, point, transformed, ref lastOutT);
                            ++pts;
                            break;
                        }

                        case PathCommand.CubicTo:
                        {
                            var ctrl1T = TvgMath.Transform(pts[0], matrix);
                            var ctrl2T = TvgMath.Transform(pts[1], matrix);
                            var endT = TvgMath.Transform(pts[2], matrix);
                            if (tracker.candidate)
                            {
                                if (TvgMath.Closed(lastInT, endT, Tolerance))
                                    tracker.TrackClosedCubic(lastInT, ctrl1T, ctrl2T, endT);
                                else
                                {
                                    ValidateCubic(lastInT, ctrl1T, ctrl2T, endT, out var maxDist, out var minT, out var maxT, out var vecLen);
                                    var tEps = Tolerance / vecLen;
                                    if (maxDist <= Tolerance && minT >= -tEps && maxT <= 1.0f + tEps)
                                        tracker.TrackFlatCubic(lastInT, ctrl1T, ctrl2T, endT);
                                    else
                                        tracker.Disable();
                                }
                            }

                            if (!TvgMath.Closed(lastOutT, endT, Tolerance))
                            {
                                ValidateCubic(lastOutT, ctrl1T, ctrl2T, endT, out var maxDist, out var minT, out var maxT, out var vecLen);
                                var tEps = Tolerance / vecLen;
                                subpathHasSegment = true;
                                if (maxDist <= Tolerance && minT >= -tEps && maxT <= 1.0f + tEps)
                                    AddLine(@out, localOut, pts[2], endT, ref lastOutT);
                                else
                                {
                                    @out.CubicTo(ctrl1T, ctrl2T, endT);
                                    localOut?.CubicTo(pts[0], pts[1], pts[2]);
                                    lastOutT = endT;
                                    tracker.Disable();
                                }
                            }

                            lastInT = endT;
                            pts += 3;
                            break;
                        }

                        case PathCommand.Close:
                        {
                            if (subpathOpen)
                            {
                                var closedIn = TvgMath.Closed(lastInT, subpathStartT, Tolerance);
                                if (!closedIn)
                                    subpathHasSegment = true;
                                tracker.TrackClose(lastInT, subpathStartT, closedIn);
                            }

                            @out.Close();
                            localOut?.Close();
                            lastOutT = lastInT = subpathStartT;
                            break;
                        }
                    }
                }

                FinalizeSubpath(ref subpathHasSegment, ref drawableSubpathCnt, ref tracker);
                thin = tracker.candidate && tracker.ready && drawableSubpathCnt == 1;
                if (thin && tracker.TooThin())
                {
                    thin = false;
                    skipFill = true;
                }
            }
            finally
            {
                tracker.Dispose();
            }
        }

        public static void GpuOptimize(in RenderPath @in, RenderPath @out, in Matrix matrix, out bool thin, out bool skipFill)
        {
            GpuOptimize(@in, @out, null, matrix, out thin, out skipFill);
        }

        public static uint GpuArcSegmentsCnt(float arcAngle, float pixelRadius)
        {
            if (pixelRadius < MathConstants.FLOAT_EPSILON)
                return 2;
            const float PxTolerance = 0.25f;
            var segmentAngle = 2.0f * MathF.Sqrt(2.0f * PxTolerance / pixelRadius);
            return (uint)MathF.Ceiling(MathF.Abs(arcAngle) / segmentAngle) + 1;
        }

        public static bool GpuPointInTriangle(in Point p, in Point a, in Point b, in Point c)
        {
            var d1 = TvgMath.Cross(TvgMath.PointSub(p, a), TvgMath.PointSub(p, b));
            var d2 = TvgMath.Cross(TvgMath.PointSub(p, b), TvgMath.PointSub(p, c));
            var d3 = TvgMath.Cross(TvgMath.PointSub(p, c), TvgMath.PointSub(p, a));
            var hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
            var hasPos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(hasNeg && hasPos);
        }

        public static bool GpuPointInQuad(in Point p, ReadOnlySpan<Point> triangles) => GpuPointInTriangle(p, triangles[0], triangles[1], triangles[2]) || GpuPointInTriangle(p, triangles[3], triangles[4], triangles[5]);
        public static bool GpuPointInAnyMesh(in Point p, void* vertices, uint* indices, uint count)
        {
            var points = (Point*)vertices;
            for (uint i = 0; i < count; i += 3)
                if (GpuPointInTriangle(p, points[indices[i]], points[indices[i + 1]], points[indices[i + 2]]))
                    return true;
            return false;
        }

        private static void AddFillCrossing(in Point p, in Point a, in Point b, ref int winding)
        {
            if ((a.y < p.y) == (b.y < p.y))
                return;
            float intersection = (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x;
            if (intersection > p.x)
                winding += b.y > a.y ? 1 : -1;
        }

        public static bool GpuPointInFillMesh(in Point p, void* vertices, uint* indices, uint count, FillRule rule)
        {
            int winding = 0;
            var points = (Point*)vertices;
            for (uint i = 0; i < count; i += 3)
            {
                var a = points[indices[i]];
                var b = points[indices[i + 1]];
                var c = points[indices[i + 2]];
                AddFillCrossing(p, a, b, ref winding);
                AddFillCrossing(p, b, c, ref winding);
                AddFillCrossing(p, c, a, ref winding);
            }

            return rule == FillRule.EvenOdd ? winding % 2 != 0 : winding != 0;
        }

        public static RenderRegion GpuTransformBounds(in RenderRegion bounds, in Matrix matrix)
        {
            if (bounds.Invalid())
                return bounds;
            var lt = TvgMath.Transform(new Point(bounds.min.x, bounds.min.y), matrix);
            var lb = TvgMath.Transform(new Point(bounds.min.x, bounds.max.y), matrix);
            var rt = TvgMath.Transform(new Point(bounds.max.x, bounds.min.y), matrix);
            var rb = TvgMath.Transform(new Point(bounds.max.x, bounds.max.y), matrix);
            var minX = MathF.Min(MathF.Min(lt.x, lb.x), MathF.Min(rt.x, rb.x));
            var minY = MathF.Min(MathF.Min(lt.y, lb.y), MathF.Min(rt.y, rb.y));
            var maxX = MathF.Max(MathF.Max(lt.x, lb.x), MathF.Max(rt.x, rb.x));
            var maxY = MathF.Max(MathF.Max(lt.y, lb.y), MathF.Max(rt.y, rb.y));
            return new RenderRegion((int)MathF.Floor(minX), (int)MathF.Floor(minY), (int)MathF.Ceiling(maxX), (int)MathF.Ceiling(maxY));
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
                if (TvgMath.Zero(value))
                    return 0;
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
            if (rs.stroke == null || rs.stroke.dashCount == 0 || rs.stroke.dashLength < RenderHelper.DASH_PATTERN_THRESHOLD)
                return false;
            @out.cmds.Reserve(20 * rs.path.cmds.count);
            @out.pts.Reserve(20 * rs.path.pts.count);
            var dash = new StrokeDashPath(rs.stroke.dashPattern!, (int)rs.stroke.dashCount, rs.stroke.dashOffset, rs.stroke.dashLength);
            var allowDot = rs.stroke.cap != StrokeCap.Butt;
            if (rs.Trimpath())
            {
                var tpath = new RenderPath();
                try
                {
                    if (rs.stroke.trim.Trim(rs.path, tpath))
                        return dash.Gen(tpath, @out, allowDot, transform);
                    return false;
                }
                finally
                {
                    tpath.cmds.Dispose();
                    tpath.pts.Dispose();
                }
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
        public sbyte winding;
        Point firstEdge;
        Point prevEdge;
        sbyte prevXDir;
        sbyte prevYDir;
        public byte xDirChanges;
        public byte yDirChanges;
        byte reversals;
        public bool contourHasEdges;
        const byte MaxAxisDirChanges = 3;
        const byte MaxCollinearReversals = 2;
        public static GpuConvexProbe Create()
        {
            return new GpuConvexProbe
            {
                convex = true
            };
        }

        public void NextContour()
        {
            if (contourHasEdges)
                convex = false;
            ResetContour();
        }

        public void AddEdge(in Point edge)
        {
            if (TvgMath.Zero(edge))
                return;
            contourHasEdges = true;
            if (!convex)
                return;
            if (TvgMath.Zero(firstEdge))
                firstEdge = edge;
            UpdateDir(edge.x, ref prevXDir, ref xDirChanges);
            UpdateDir(edge.y, ref prevYDir, ref yDirChanges);
            if (!convex)
                return;
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
                if (winding == 0)
                    winding = sign;
                else if (sign != winding)
                    convex = false;
            }

            prevEdge = edge;
        }

        public void AddContourClose(in Point edge)
        {
            AddEdge(in edge);
            if (convex && !TvgMath.Zero(firstEdge))
                AddEdge(in firstEdge);
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
            if (!TvgMath.Zero(value))
                dir = value > 0.0f ? (sbyte)1 : (sbyte)-1;
            if (dir == 0 || !convex)
                return;
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
    internal unsafe struct StrokeDashPath
    {
        struct DashPatternState
        {
            public int idx;
            public float offset;
            public bool gap;
        }

        struct DashSubpathState
        {
            public bool closed;
            public bool closeEndsAtStart;
        }

        struct PieceRange
        {
            public uint cmdBegin;
            public uint cmdEnd;
            public uint ptBegin;
            public uint ptEnd;
        }

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
        const float DASH_ENDPOINT_TOLERANCE = RenderHelper.DASH_PATTERN_THRESHOLD;
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

        DashPatternState PatternState()
        {
            var state = new DashPatternState
            {
                offset = dashOffset
            };
            if (TvgMath.Zero(dashOffset))
                return state;
            var oddPattern = dashCnt & 1;
            var length = oddPattern != 0 ? dashLength * 2 : dashLength;
            state.offset %= length;
            if (state.offset < 0)
                state.offset += length;
            for (uint i = 0; i < (uint)dashCnt * (uint)(oddPattern + 1); ++i)
            {
                var curPattern = dashPattern[state.idx];
                if (state.offset < curPattern)
                    break;
                state.offset -= curPattern;
                state.gap = !state.gap;
                if (++state.idx == dashCnt)
                    state.idx = 0;
            }

            return state;
        }

        void BeginSubpath(Point start, in DashPatternState state)
        {
            curIdx = state.idx;
            curLen = dashPattern[state.idx] - state.offset;
            opGap = state.gap;
            move = true;
            curPos = start;
        }

        static void AppendCommand(RenderPath dst, RenderPath src, PathCommand cmd, ref uint ptIdx, bool skipMoveTo)
        {
            switch (cmd)
            {
                case PathCommand.MoveTo:
                    var pt = src.pts[ptIdx++];
                    if (!skipMoveTo)
                        dst.MoveTo(pt);
                    break;
                case PathCommand.LineTo:
                    dst.LineTo(src.pts[ptIdx++]);
                    break;
                case PathCommand.CubicTo:
                    dst.CubicTo(src.pts[ptIdx], src.pts[ptIdx + 1], src.pts[ptIdx + 2]);
                    ptIdx += 3;
                    break;
            }
        }

        static void AppendPiece(RenderPath dst, RenderPath src, in PieceRange piece, bool skipMoveTo)
        {
            var ptIdx = piece.ptBegin;
            for (var i = piece.cmdBegin; i < piece.cmdEnd; ++i)
            {
                AppendCommand(dst, src, src.cmds[i], ref ptIdx, skipMoveTo && i == piece.cmdBegin);
            }
        }

        static void CollectPieces(RenderPath subOut, ref Array<PieceRange> pieces)
        {
            uint ptIdx = 0;
            uint pieceCmdBegin = uint.MaxValue;
            uint piecePtBegin = 0;
            var pieceHasDraw = false;
            for (uint i = 0; i < subOut.cmds.count; ++i)
            {
                if (subOut.cmds[i] == PathCommand.MoveTo)
                {
                    if (pieceCmdBegin != uint.MaxValue && pieceHasDraw)
                    {
                        pieces.Push(new PieceRange { cmdBegin = pieceCmdBegin, cmdEnd = i, ptBegin = piecePtBegin, ptEnd = ptIdx });
                    }

                    pieceCmdBegin = i;
                    piecePtBegin = ptIdx;
                    pieceHasDraw = false;
                }
                else
                    pieceHasDraw = true;
                if (subOut.cmds[i] == PathCommand.CubicTo)
                    ptIdx += 3;
                else if (subOut.cmds[i] != PathCommand.Close)
                    ++ptIdx;
            }

            if (pieceCmdBegin != uint.MaxValue && pieceHasDraw)
            {
                pieces.Push(new PieceRange { cmdBegin = pieceCmdBegin, cmdEnd = subOut.cmds.count, ptBegin = piecePtBegin, ptEnd = ptIdx });
            }
        }

        static void ResetSubpath(RenderPath subOut, ref DashSubpathState state)
        {
            subOut.Clear();
            state = default;
        }

        static bool PreparePieces(RenderPath subOut, ref DashSubpathState state, ref Array<PieceRange> pieces)
        {
            pieces.Clear();
            if (subOut.cmds.count == 0)
            {
                ResetSubpath(subOut, ref state);
                return false;
            }

            pieces.Reserve(subOut.cmds.count);
            CollectPieces(subOut, ref pieces);
            if (pieces.count > 0)
                return true;
            ResetSubpath(subOut, ref state);
            return false;
        }

        static bool AppendClosedSubpath(RenderPath @out, RenderPath subOut, in Array<PieceRange> pieces, Point mappedStart, in DashSubpathState state)
        {
            if (!state.closed)
                return false;
            var first = pieces[0];
            var last = pieces.data[pieces.count - 1];
            var wrapsStart = TvgMath.Closed(subOut.pts[first.ptBegin], mappedStart, DASH_ENDPOINT_TOLERANCE) && TvgMath.Closed(subOut.pts[last.ptEnd - 1], mappedStart, DASH_ENDPOINT_TOLERANCE);
            if (!wrapsStart)
                return false;
            if (pieces.count == 1)
            {
                AppendPiece(@out, subOut, first, false);
                @out.Close();
                return true;
            }

            if (!state.closeEndsAtStart)
                return false;
            AppendPiece(@out, subOut, last, false);
            AppendPiece(@out, subOut, first, true);
            for (uint i = 1; i + 1 < pieces.count; ++i)
                AppendPiece(@out, subOut, pieces[i], false);
            return true;
        }

        static void AppendSubpath(RenderPath @out, RenderPath subOut, Point mappedStart, ref DashSubpathState state, ref Array<PieceRange> pieces)
        {
            if (!state.closed)
            {
                @out.cmds.Push(subOut.cmds);
                @out.pts.Push(subOut.pts);
                ResetSubpath(subOut, ref state);
                return;
            }

            if (!PreparePieces(subOut, ref state, ref pieces))
                return;
            if (!AppendClosedSubpath(@out, subOut, pieces, mappedStart, state))
            {
                @out.cmds.Push(subOut.cmds);
                @out.pts.Push(subOut.pts);
            }

            ResetSubpath(subOut, ref state);
        }

        public unsafe bool Gen(RenderPath @in, RenderPath @out, bool allowDot, in Matrix? transform)
        {
            this.transform = transform;
            this.applyTransform = transform.HasValue && !TvgMath.IsIdentity(transform.Value);
            var initialState = PatternState();
            var pts = @in.pts.data;
            Point start = default;
            var subOut = new RenderPath();
            var subpathState = new DashSubpathState();
            Array<PieceRange> pieces = default;
            try
            {
                for (uint ci = 0; ci < @in.cmds.count; ci++)
                {
                    switch (@in.cmds.data[ci])
                    {
                        case PathCommand.Close:
                        {
                            var prevPtCount = subOut.pts.count;
                            var zeroLength = TvgMath.Zero(TvgMath.PointLength(TvgMath.PointSub(start, curPos)));
                            LineTo(subOut, start, allowDot);
                            subpathState.closed = true;
                            subpathState.closeEndsAtStart = zeroLength || (subOut.pts.count > prevPtCount && TvgMath.Closed(subOut.pts.Last(), Map(start), DASH_ENDPOINT_TOLERANCE));
                            break;
                        }

                        case PathCommand.MoveTo:
                        {
                            AppendSubpath(@out, subOut, Map(start), ref subpathState, ref pieces);
                            start = *pts++;
                            BeginSubpath(start, initialState);
                            break;
                        }

                        case PathCommand.LineTo:
                        {
                            LineTo(subOut, *pts, allowDot);
                            pts++;
                            break;
                        }

                        case PathCommand.CubicTo:
                        {
                            CubicTo(subOut, pts[0], pts[1], pts[2], allowDot);
                            pts += 3;
                            break;
                        }

                        default:
                            break;
                    }
                }

                AppendSubpath(@out, subOut, Map(start), ref subpathState, ref pieces);
                return true;
            }
            finally
            {
                pieces.Dispose();
                subOut.cmds.Dispose();
                subOut.pts.Dispose();
            }
        }

        void LineTo(RenderPath @out, Point to, bool allowDot)
        {
            var line = new Line
            {
                pt1 = curPos,
                pt2 = to
            };
            var diff = new Point(to.x - curPos.x, to.y - curPos.y);
            var len = TvgMath.PointLength(diff);
            if (TvgMath.Zero(len))
            {
                curPos = to;
                return;
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
                        if (allowDot && !opGap)
                            DashPoint(@out, line.pt1);
                        right = line;
                    }

                    if (++curIdx == dashCnt)
                        curIdx = 0;
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
                    if (++curIdx == dashCnt)
                        curIdx = 0;
                    curLen = dashPattern[curIdx];
                    opGap = !opGap;
                    move = true;
                }
            }

            curPos = to;
        }

        void CubicTo(RenderPath @out, Point cnt1, Point cnt2, Point end, bool allowDot)
        {
            var curve = new Bezier
            {
                start = curPos,
                ctrl1 = cnt1,
                ctrl2 = cnt2,
                end = end
            };
            var len = curve.Length();
            if (TvgMath.Zero(len))
            {
                curPos = end;
                return;
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
                        if (allowDot && !opGap)
                            DashPoint(@out, curve.start);
                        right = curve;
                    }

                    if (++curIdx == dashCnt)
                        curIdx = 0;
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
                    if (++curIdx == dashCnt)
                        curIdx = 0;
                    curLen = dashPattern[curIdx];
                    opGap = !opGap;
                    move = true;
                }
            }

            curPos = end;
        }
    }
}
