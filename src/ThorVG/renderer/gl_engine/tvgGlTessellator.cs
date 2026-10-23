// Ported from ThorVG/src/renderer/gl_engine/tvgGlTessellator.h and tvgGlTessellator.cpp

using System;

namespace ThorVG
{
    public unsafe class Stroker
    {
        private struct State
        {
            public Point firstPt;
            public Point firstPtDir;
            public Point prevPt;
            public Point prevPtDir;
            public float firstLength, prevLength;
            public uint firstIndex, prevIndex;
        }

        private GlGeometryBuffer mBuffer;
        private float mWidth;
        private float mMiterLimit;
        private float mQualityScale;
        private StrokeCap mCap;
        private StrokeJoin mJoin;
        private State mState;
        private Point mLeftTop;
        private Point mRightBottom;
        private bool mThinFill;
        private byte mDirections;
        private GpuConvexProbe mProbe;
        public bool overlapFree;

        public Stroker(GlGeometryBuffer buffer, float strokeWidth, StrokeCap cap, StrokeJoin join, float miterLimit = 4.0f, float qualityScale = 1.0f)
        {
            mBuffer = buffer;
            mWidth = strokeWidth;
            mMiterLimit = miterLimit;
            mQualityScale = qualityScale;
            mCap = cap;
            mJoin = join;
        }

        private float Radius() => mWidth * 0.5f;

        private static Point AddProduct(Point p, Point direction, float scale) => new Point(
            TvgMath.MultiplyAdd(direction.x, scale, p.x),
            TvgMath.MultiplyAdd(direction.y, scale, p.y));

        private static Point SubtractProduct(Point p, Point direction, float scale) => new Point(
            TvgMath.MultiplyAdd(-direction.x, scale, p.x),
            TvgMath.MultiplyAdd(-direction.y, scale, p.y));

        public void Run(RenderPath path, bool thinFill = false)
        {
            mThinFill = thinFill;
            overlapFree = !thinFill;
            mProbe = GpuConvexProbe.Create();
            mDirections = 0;
            mBuffer.vertex.Reserve(path.pts.count * 8 + 16);
            mBuffer.index.Reserve(path.pts.count * 6);

            var validStrokeCap = false;
            var pts = path.pts.Begin();

            var cmd = path.cmds.Begin();
            while (cmd < path.cmds.End())
            {
                switch (*cmd)
                {
                    case PathCommand.MoveTo:
                    {
                        if (validStrokeCap)
                        {
                            Cap();
                            validStrokeCap = false;
                        }
                        mState.firstLength = 0;
                        mState.firstPt = mState.prevPt = *pts++;
                        validStrokeCap = false;
                    } break;
                    case PathCommand.LineTo:
                    {
                        validStrokeCap = true;
                        LineTo(*pts);
                        pts++;
                    } break;
                    case PathCommand.CubicTo:
                    {
                        validStrokeCap = true;
                        CubicTo(pts[0], pts[1], pts[2]);
                        pts += 3;
                    } break;
                    case PathCommand.Close:
                    {
                        Close();
                        validStrokeCap = false;
                    } break;
                    default:
                        break;
                }
                cmd++;
            }
            if (validStrokeCap) Cap();
        }

        public RenderRegion Bounds()
        {
            return new RenderRegion(
                (int)MathF.Floor(mLeftTop.x), (int)MathF.Floor(mLeftTop.y),
                (int)MathF.Ceiling(mRightBottom.x), (int)MathF.Ceiling(mRightBottom.y));
        }

        private void Cap()
        {
            bool monotone = (mDirections & 3) != 3 || (mDirections & 12) != 12;
            overlapFree = overlapFree && mState.firstLength > 0 && (mCap == StrokeCap.Butt || !PointEqual(mState.firstPt, mState.prevPt)) &&
                (monotone || (mProbe.convex && mProbe.xDirChanges <= 1 && mProbe.yDirChanges <= 1 &&
                 mProbe.winding * TvgMath.Cross(mState.firstPtDir, mState.prevPtDir) >= 0));
            if (mCap == StrokeCap.Butt) return;

            if (mCap == StrokeCap.Square)
            {
                if (PointEqual(mState.firstPt, mState.prevPt)) SquarePoint(mState.firstPt);
                else
                {
                    Square(mState.firstPt, new Point(-mState.firstPtDir.x, -mState.firstPtDir.y));
                    Square(mState.prevPt, mState.prevPtDir);
                }
            }
            else if (mCap == StrokeCap.Round)
            {
                if (PointEqual(mState.firstPt, mState.prevPt)) RoundPoint(mState.firstPt);
                else
                {
                    RoundCap(mState.firstPt, new Point(-mState.firstPtDir.x, -mState.firstPtDir.y));
                    RoundCap(mState.prevPt, mState.prevPtDir);
                }
            }
        }

        private void LineTo(Point curr)
        {
            var dir = TvgMath.PointSub(curr, mState.prevPt);
            float length = TvgMath.PointLength(dir);
            if (TvgMath.Zero(length) || !float.IsFinite(length)) return;
            dir = TvgMath.PointMul(dir, 1 / length);
            if (overlapFree)
            {
                if (mState.firstLength == 0) overlapFree &= !mProbe.contourHasEdges;
                mDirections |= (byte)((dir.x > 0 ? 1 : 0) | (dir.x < 0 ? 2 : 0) | (dir.y > 0 ? 4 : 0) | (dir.y < 0 ? 8 : 0));
                mProbe.AddEdge(dir);
            }

            var normal = new Point(-dir.y, dir.x);
            var a = AddProduct(mState.prevPt, normal, Radius());
            var b = SubtractProduct(mState.prevPt, normal, Radius());
            var c = AddProduct(curr, normal, Radius());
            var d = SubtractProduct(curr, normal, Radius());

            var ia = PushVertex(ref mBuffer.vertex, a.x, a.y);
            var ib = PushVertex(ref mBuffer.vertex, b.x, b.y);
            var ic = PushVertex(ref mBuffer.vertex, c.x, c.y);
            var id = PushVertex(ref mBuffer.vertex, d.x, d.y);

            PushTriangle(ia, ib, ic);

            PushTriangle(ib, id, ic);

            if (mState.firstLength == 0)
            {
                mState.firstPtDir = dir;
                mState.firstLength = length;
                mState.firstIndex = ia;
            }
            else Join(dir, length, ia);
            mState.prevPtDir = dir;
            mState.prevPt = curr;
            mState.prevLength = length;
            mState.prevIndex = ia;

            if (ia == 0)
            {
                mRightBottom.x = mLeftTop.x = curr.x;
                mRightBottom.y = mLeftTop.y = curr.y;
            }

            mLeftTop.x = MathF.Min(mLeftTop.x, MathF.Min(MathF.Min(a.x, b.x), MathF.Min(c.x, d.x)));
            mLeftTop.y = MathF.Min(mLeftTop.y, MathF.Min(MathF.Min(a.y, b.y), MathF.Min(c.y, d.y)));
            mRightBottom.x = MathF.Max(mRightBottom.x, MathF.Max(MathF.Max(a.x, b.x), MathF.Max(c.x, d.x)));
            mRightBottom.y = MathF.Max(mRightBottom.y, MathF.Max(MathF.Max(a.y, b.y), MathF.Max(c.y, d.y)));
        }

        private void CubicTo(Point cnt1, Point cnt2, Point end)
        {
            var curve = new Bezier(mState.prevPt, cnt1, cnt2, end);

            var count = curve.Segments(mQualityScale);
            uint joins = count - (mState.firstLength == 0 ? 1u : 0u);
            uint vertices = count * 8, indices = count * 6;
            if (mJoin == StrokeJoin.Bevel) { vertices += joins * 6; indices += joins * 3; }
            else if (mJoin == StrokeJoin.Miter) { vertices += joins * 8; indices += joins * 6; }
            mBuffer.vertex.Grow(vertices); mBuffer.index.Grow(indices);
            var step = 1f / count;

            for (uint i = 1; i <= count; i++)
            {
                LineTo(curve.At(step * i));
            }
        }

        private void Close()
        {
            if (mState.firstLength == 0) return;
            var delta = TvgMath.PointSub(mState.prevPt, mState.firstPt);
            if (TvgMath.Dot(delta, delta) > 0.015625f * 0.015625f)
            {
                LineTo(mState.firstPt);
            }
            float length = PointEqual(mState.prevPt, mState.firstPt) ? mState.firstLength : 0;
            if (length == 0) overlapFree = false;
            if (overlapFree) mProbe.AddEdge(mState.firstPtDir);
            Join(mState.firstPtDir, length, mState.firstIndex);
            overlapFree &= mProbe.convex;
            mState.firstLength = 0;
            mState.prevPt = mState.firstPt;
        }

        private void Join(Point dir, float length, uint index)
        {
            float turn = TvgMath.Cross(mState.prevPtDir, dir);

            if (TvgMath.Zero(turn))
            {
                overlapFree &= PointEqual(mState.prevPtDir, dir);
                if (PointEqual(mState.prevPtDir, dir)) return;
                if (mJoin != StrokeJoin.Round) return;
                RoundCap(mState.prevPt, dir);
            }
            else
            {
                var normal = new Point(-dir.y, dir.x);
                var prevNormal = new Point(-mState.prevPtDir.y, mState.prevPtDir.x);
                float offset = turn < 0 ? Radius() : -Radius();
                var prevJoin = TvgMath.PointAdd(mState.prevPt, TvgMath.PointMul(prevNormal, offset));
                var currJoin = AddProduct(mState.prevPt, normal, offset);
                var apex = mState.prevPt;
                float cosine = TvgMath.MultiplyAdd(mState.prevPtDir.y, dir.y, mState.prevPtDir.x * dir.x);
                float denom = 1 + cosine;
                bool trimmed = false;
                if (!mThinFill && denom > MathConstants.FLOAT_EPSILON)
                {
                    float setback = Radius() * (cosine < 0 ? (1 - cosine) / MathF.Abs(turn) : MathF.Abs(turn) / denom);
                    float scale = MathF.Max(MathF.Max(MathF.Abs(apex.x), MathF.Abs(apex.y)), MathF.Max(Radius(), MathF.Max(mState.prevLength, length)));
                    if (TvgMath.MultiplyAdd(2, setback, MathConstants.FLOAT_EPSILON * scale) < MathF.Min(mState.prevLength, length))
                    {
                        // The previous normal product also supplies prevJoin;
                        // native rounds it before the final subtraction.
                        var inner = TvgMath.PointSub(SubtractProduct(apex, mState.prevPtDir, setback), TvgMath.PointMul(prevNormal, offset));
                        uint side = turn < 0 ? 1u : 0u;
                        uint prev = (mState.prevIndex + 2 + side) * 2, curr = (index + side) * 2;
                        mBuffer.vertex.data[prev] = mBuffer.vertex.data[curr] = inner.x;
                        mBuffer.vertex.data[prev + 1] = mBuffer.vertex.data[curr + 1] = inner.y;
                        apex = inner; trimmed = true;
                    }
                }
                overlapFree &= trimmed;
                if (mJoin == StrokeJoin.Miter) Miter(prevJoin, currJoin, mState.prevPt, apex);
                else if (mJoin == StrokeJoin.Bevel) Bevel(prevJoin, currJoin, apex);
                else RoundJoin(prevJoin, currJoin, mState.prevPt, apex);
            }
        }

        private void RoundJoin(Point prev, Point curr, Point center, Point apex)
        {
            float turn = TvgMath.Cross(TvgMath.PointSub(center, prev), TvgMath.PointSub(curr, prev));
            if (turn == 0) return;

            mLeftTop.x = MathF.Min(mLeftTop.x, MathF.Min(center.x, MathF.Min(prev.x, curr.x)));
            mLeftTop.y = MathF.Min(mLeftTop.y, MathF.Min(center.y, MathF.Min(prev.y, curr.y)));
            mRightBottom.x = MathF.Max(mRightBottom.x, MathF.Max(center.x, MathF.Max(prev.x, curr.x)));
            mRightBottom.y = MathF.Max(mRightBottom.y, MathF.Max(center.y, MathF.Max(prev.y, curr.y)));

            var startAngle = TvgMath.Atan2(prev.y - center.y, prev.x - center.x);
            var endAngle = TvgMath.Atan2(curr.y - center.y, curr.x - center.x);

            if (turn > 0)
            {
                if (endAngle > startAngle) endAngle -= 2 * MathConstants.MATH_PI;
            }
            else
            {
                if (endAngle < startAngle) endAngle += 2 * MathConstants.MATH_PI;
            }

            var arcAngle = endAngle - startAngle;
            var count = Math.Max(2u, GpuCommon.GpuArcSegmentsCnt(arcAngle, Radius() * mQualityScale));

            var ci = PushVertex(ref mBuffer.vertex, apex.x, apex.y);
            var pi = PushVertex(ref mBuffer.vertex, prev.x, prev.y);
            var step = (endAngle - startAngle) / (count - 1);

            for (uint i = 1; i < count; i++)
            {
                var angle = TvgMath.MultiplyAdd(step, i, startAngle);
                var outPt = i + 1 == count ? curr : AddProduct(center, new Point(MathF.Cos(angle), MathF.Sin(angle)), Radius());
                var oi = PushVertex(ref mBuffer.vertex, outPt.x, outPt.y);

                PushTriangle(ci, pi, oi);

                pi = oi;

                mLeftTop.x = MathF.Min(mLeftTop.x, outPt.x);
                mLeftTop.y = MathF.Min(mLeftTop.y, outPt.y);
                mRightBottom.x = MathF.Max(mRightBottom.x, outPt.x);
                mRightBottom.y = MathF.Max(mRightBottom.y, outPt.y);
            }
        }

        private void RoundPoint(Point p)
        {
            var count = GpuCommon.GpuArcSegmentsCnt(2.0f * MathConstants.MATH_PI, Radius() * mQualityScale);
            var ci = PushVertex(ref mBuffer.vertex, p.x, p.y);
            var step = 2.0f * MathConstants.MATH_PI / (count - 1);

            for (uint i = 1; i <= count; i++)
            {
                float angle = i * step;
                var dir = new Point(MathF.Cos(angle), MathF.Sin(angle));
                var outPt = AddProduct(p, dir, Radius());
                var oi = PushVertex(ref mBuffer.vertex, outPt.x, outPt.y);

                if (oi > 1)
                {
                    PushTriangle(ci, oi, oi - 1);
                }
            }

            mLeftTop.x = MathF.Min(mLeftTop.x, p.x - Radius());
            mLeftTop.y = MathF.Min(mLeftTop.y, p.y - Radius());
            mRightBottom.x = MathF.Max(mRightBottom.x, p.x + Radius());
            mRightBottom.y = MathF.Max(mRightBottom.y, p.y + Radius());
        }

        private void Miter(Point prev, Point curr, Point center, Point apex)
        {
            var pp1 = TvgMath.PointSub(prev, center);
            var pp2 = TvgMath.PointSub(curr, center);
            var outDir = TvgMath.PointAdd(pp1, pp2);
            var k = 2f * Radius() * Radius() / TvgMath.Dot(outDir, outDir);
            var pe = TvgMath.PointMul(outDir, k);

            float limit = mMiterLimit * Radius();
            if (TvgMath.Dot(pe, pe) >= limit * limit)
            {
                Bevel(prev, curr, apex);
                return;
            }

            var joinPt = TvgMath.PointAdd(center, pe);
            var ci = PushVertex(ref mBuffer.vertex, apex.x, apex.y);
            var cp1 = PushVertex(ref mBuffer.vertex, prev.x, prev.y);
            var cp2 = PushVertex(ref mBuffer.vertex, curr.x, curr.y);
            var e = PushVertex(ref mBuffer.vertex, joinPt.x, joinPt.y);

            PushTriangle(ci, cp1, e);

            PushTriangle(e, cp2, ci);

            mLeftTop.x = MathF.Min(mLeftTop.x, joinPt.x);
            mLeftTop.y = MathF.Min(mLeftTop.y, joinPt.y);
            mRightBottom.x = MathF.Max(mRightBottom.x, joinPt.x);
            mRightBottom.y = MathF.Max(mRightBottom.y, joinPt.y);
        }

        private void Bevel(Point prev, Point curr, Point center)
        {
            var ai = PushVertex(ref mBuffer.vertex, prev.x, prev.y);
            var bi = PushVertex(ref mBuffer.vertex, curr.x, curr.y);
            var ci = PushVertex(ref mBuffer.vertex, center.x, center.y);

            PushTriangle(ai, bi, ci);
        }

        private void Square(Point p, Point outDir)
        {
            var normal = new Point(-outDir.y, outDir.x);

            var a = AddProduct(p, normal, Radius());
            var b = SubtractProduct(p, normal, Radius());
            var c = AddProduct(a, outDir, Radius());
            var d = AddProduct(b, outDir, Radius());

            var ai = PushVertex(ref mBuffer.vertex, a.x, a.y);
            var bi = PushVertex(ref mBuffer.vertex, b.x, b.y);
            var ci = PushVertex(ref mBuffer.vertex, c.x, c.y);
            var di = PushVertex(ref mBuffer.vertex, d.x, d.y);

            PushTriangle(ai, bi, ci);

            PushTriangle(ci, bi, di);

            mLeftTop.x = MathF.Min(mLeftTop.x, MathF.Min(MathF.Min(a.x, b.x), MathF.Min(c.x, d.x)));
            mLeftTop.y = MathF.Min(mLeftTop.y, MathF.Min(MathF.Min(a.y, b.y), MathF.Min(c.y, d.y)));
            mRightBottom.x = MathF.Max(mRightBottom.x, MathF.Max(MathF.Max(a.x, b.x), MathF.Max(c.x, d.x)));
            mRightBottom.y = MathF.Max(mRightBottom.y, MathF.Max(MathF.Max(a.y, b.y), MathF.Max(c.y, d.y)));
        }

        private void SquarePoint(Point p)
        {
            var offsetX = new Point(Radius(), 0.0f);
            var offsetY = new Point(0.0f, Radius());

            var a = TvgMath.PointAdd(TvgMath.PointAdd(p, offsetX), offsetY);
            var b = TvgMath.PointAdd(TvgMath.PointSub(p, offsetX), offsetY);
            var c = TvgMath.PointSub(TvgMath.PointSub(p, offsetX), offsetY);
            var d = TvgMath.PointSub(TvgMath.PointAdd(p, offsetX), offsetY);

            var ai = PushVertex(ref mBuffer.vertex, a.x, a.y);
            var bi = PushVertex(ref mBuffer.vertex, b.x, b.y);
            var ci = PushVertex(ref mBuffer.vertex, c.x, c.y);
            var di = PushVertex(ref mBuffer.vertex, d.x, d.y);

            PushTriangle(ai, bi, ci);

            PushTriangle(ci, di, ai);

            mLeftTop.x = MathF.Min(mLeftTop.x, MathF.Min(MathF.Min(a.x, b.x), MathF.Min(c.x, d.x)));
            mLeftTop.y = MathF.Min(mLeftTop.y, MathF.Min(MathF.Min(a.y, b.y), MathF.Min(c.y, d.y)));
            mRightBottom.x = MathF.Max(mRightBottom.x, MathF.Max(MathF.Max(a.x, b.x), MathF.Max(c.x, d.x)));
            mRightBottom.y = MathF.Max(mRightBottom.y, MathF.Max(MathF.Max(a.y, b.y), MathF.Max(c.y, d.y)));
        }

        private void RoundCap(Point p, Point outDir)
        {
            var normal = new Point(-outDir.y, outDir.x);
            var a = AddProduct(p, normal, Radius());
            var b = SubtractProduct(p, normal, Radius());
            var c = AddProduct(p, outDir, Radius());

            RoundJoin(a, c, p, p);
            RoundJoin(c, b, p, p);
        }

        private void PushTriangle(uint a, uint b, uint c)
        {
            mBuffer.index.Grow(3);
            mBuffer.index.data[mBuffer.index.count++] = a;
            mBuffer.index.data[mBuffer.index.count++] = b;
            mBuffer.index.data[mBuffer.index.count++] = c;
        }

        private static uint PushVertex(ref Array<float> array, float x, float y)
        {
            uint index = array.count / 2;
            array.Grow(2);
            array.data[array.count++] = x;
            array.data[array.count++] = y;
            return index;
        }

        private static bool PointEqual(Point a, Point b)
        {
            return a.x == b.x && a.y == b.y;
        }
    }

    public unsafe class BWTessellator
    {
        private GlGeometryBuffer mBuffer;
        private BBox bbox;

        public bool convex = true;

        public BWTessellator(GlGeometryBuffer buffer)
        {
            mBuffer = buffer;
        }

        public void Tessellate(RenderPath path)
        {
            var cmds = path.cmds.data;
            var cmdCnt = path.cmds.count;
            var pts = path.pts.data;
            var ptsCnt = path.pts.count;

            if (ptsCnt <= 2) return;

            uint firstIndex = 0;
            uint prevIndex = 0;
            Point firstPt = default;
            Point prevPt = default;
            var probe = GpuConvexProbe.Create();
            bool contourClosed = false;

            mBuffer.vertex.Reserve(ptsCnt * 2);
            mBuffer.index.Reserve((ptsCnt - 2) * 3);

            for (uint i = 0; i < cmdCnt; i++)
            {
                switch (cmds[i])
                {
                    case PathCommand.MoveTo:
                    {
                        // finishContour
                        if (prevIndex != 0 && !contourClosed)
                        {
                            probe.AddContourClose(TvgMath.PointSub(firstPt, prevPt));
                            contourClosed = true;
                        }
                        probe.NextContour();
                        firstIndex = PushVertex(pts->x, pts->y);
                        firstPt = prevPt = *pts;
                        prevIndex = 0;
                        contourClosed = false;
                        pts++;
                    } break;
                    case PathCommand.LineTo:
                    {
                        var edge = TvgMath.PointSub(*pts, prevPt);
                        if (prevIndex == 0)
                        {
                            prevIndex = PushVertex(pts->x, pts->y);
                            probe.AddEdge(edge);
                            prevPt = *pts++;
                        }
                        else
                        {
                            probe.AddEdge(edge);
                            var currIndex = PushVertex(pts->x, pts->y);
                            PushTriangle(firstIndex, prevIndex, currIndex);
                            prevIndex = currIndex;
                            prevPt = *pts++;
                        }
                    } break;
                    case PathCommand.CubicTo:
                    {
                        var curve = new Bezier(pts[-1], pts[0], pts[1], pts[2]);
                        if (probe.convex && GpuCommon.GpuEdgesCross(curve.start, curve.ctrl1, curve.ctrl2, curve.end)) probe.convex = false;

                        var stepCount = curve.Segments();
                        if (stepCount <= 1) stepCount = 2;
                        uint subdivisions = (uint)stepCount;
                        mBuffer.vertex.Grow(subdivisions * 2);
                        mBuffer.index.Grow(subdivisions * 3);
                        float step = 1f / stepCount;
                        var curvePrevPt = prevPt;

                        for (uint s = 1; s <= stepCount; s++)
                        {
                            var pt = curve.At(step * s);
                            probe.AddEdge(TvgMath.PointSub(pt, curvePrevPt));
                            var currIndex = PushVertex(pt.x, pt.y);
                            curvePrevPt = pt;
                            if (prevIndex == 0) { prevIndex = currIndex; continue; }
                            PushTriangle(firstIndex, prevIndex, currIndex);
                            prevIndex = currIndex;
                        }
                        prevPt = curve.end;
                        pts += 3;
                    } break;
                    case PathCommand.Close:
                    {
                        // finishContour
                        if (prevIndex != 0 && !contourClosed)
                        {
                            probe.AddContourClose(TvgMath.PointSub(firstPt, prevPt));
                            contourClosed = true;
                        }
                    } break;
                    default:
                        break;
                }
            }

            // finishContour (final)
            if (prevIndex != 0 && !contourClosed)
            {
                probe.AddContourClose(TvgMath.PointSub(firstPt, prevPt));
            }
            convex = probe.convex;
        }

        public RenderRegion Bounds()
        {
            return new RenderRegion(
                (int)MathF.Floor(bbox.min.x), (int)MathF.Floor(bbox.min.y),
                (int)MathF.Ceiling(bbox.max.x), (int)MathF.Ceiling(bbox.max.y));
        }

        private uint PushVertex(float x, float y)
        {
            var index = mBuffer.vertex.count / 2;
            mBuffer.vertex.Grow(2);
            mBuffer.vertex.data[mBuffer.vertex.count++] = x;
            mBuffer.vertex.data[mBuffer.vertex.count++] = y;
            if (index == 0) bbox.max = bbox.min = new Point(x, y);
            else bbox = new BBox { min = new Point(MathF.Min(bbox.min.x, x), MathF.Min(bbox.min.y, y)), max = new Point(MathF.Max(bbox.max.x, x), MathF.Max(bbox.max.y, y)) };
            return index;
        }

        private void PushTriangle(uint a, uint b, uint c)
        {
            mBuffer.index.Grow(3);
            mBuffer.index.data[mBuffer.index.count++] = a;
            mBuffer.index.data[mBuffer.index.count++] = b;
            mBuffer.index.data[mBuffer.index.count++] = c;
        }
    }
}
