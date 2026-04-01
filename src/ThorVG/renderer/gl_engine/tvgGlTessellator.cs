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
        }

        private GlGeometryBuffer mBuffer;
        private float mWidth;
        private float mMiterLimit;
        private StrokeCap mCap;
        private StrokeJoin mJoin;
        private State mState;
        private Point mLeftTop;
        private Point mRightBottom;

        public Stroker(GlGeometryBuffer buffer, float strokeWidth, StrokeCap cap, StrokeJoin join, float miterLimit = 4.0f)
        {
            mBuffer = buffer;
            mWidth = strokeWidth;
            mMiterLimit = miterLimit;
            mCap = cap;
            mJoin = join;
        }

        private float Radius() => mWidth * 0.5f;

        public void Run(RenderPath path)
        {
            mBuffer.vertex.Reserve(path.pts.count * 4 + 16);
            mBuffer.index.Reserve(path.pts.count * 3);

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
                        mState.firstPt = *pts;
                        mState.firstPtDir = new Point(0.0f, 0.0f);
                        mState.prevPt = *pts;
                        mState.prevPtDir = new Point(0.0f, 0.0f);
                        pts++;
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
            TvgMath.Normalize(ref dir);

            if (dir.x == 0f && dir.y == 0f) return; // same point

            var normal = new Point(-dir.y, dir.x);
            var a = TvgMath.PointAdd(mState.prevPt, TvgMath.PointMul(normal, Radius()));
            var b = TvgMath.PointSub(mState.prevPt, TvgMath.PointMul(normal, Radius()));
            var c = TvgMath.PointAdd(curr, TvgMath.PointMul(normal, Radius()));
            var d = TvgMath.PointSub(curr, TvgMath.PointMul(normal, Radius()));

            var ia = PushVertex(ref mBuffer.vertex, a.x, a.y);
            var ib = PushVertex(ref mBuffer.vertex, b.x, b.y);
            var ic = PushVertex(ref mBuffer.vertex, c.x, c.y);
            var id = PushVertex(ref mBuffer.vertex, d.x, d.y);

            mBuffer.index.Push(ia);
            mBuffer.index.Push(ib);
            mBuffer.index.Push(ic);

            mBuffer.index.Push(ib);
            mBuffer.index.Push(id);
            mBuffer.index.Push(ic);

            if (PointEqual(mState.prevPt, mState.firstPt))
            {
                mState.prevPt = curr;
                mState.prevPtDir = dir;
                mState.firstPtDir = dir;
            }
            else
            {
                Join(dir);
                mState.prevPtDir = dir;
                mState.prevPt = curr;
            }

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

            var count = curve.Segments();
            var step = 1f / count;

            for (uint i = 0; i <= count; i++)
            {
                LineTo(curve.At(step * i));
            }
        }

        private void Close()
        {
            if (TvgMath.PointLength(TvgMath.PointSub(mState.prevPt, mState.firstPt)) > 0.015625f)
            {
                LineTo(mState.firstPt);
            }
            Join(mState.firstPtDir);
        }

        private void Join(Point dir)
        {
            var orient = TvgMath.GetOrientation(
                TvgMath.PointSub(mState.prevPt, mState.prevPtDir),
                mState.prevPt,
                TvgMath.PointAdd(mState.prevPt, dir));

            if (orient == Orientation.Linear)
            {
                if (PointEqual(mState.prevPtDir, dir)) return;
                if (mJoin != StrokeJoin.Round) return;

                var normal = new Point(-dir.y, dir.x);
                var p1 = TvgMath.PointAdd(mState.prevPt, TvgMath.PointMul(normal, Radius()));
                var p2 = TvgMath.PointSub(mState.prevPt, TvgMath.PointMul(normal, Radius()));
                var oc = TvgMath.PointAdd(mState.prevPt, TvgMath.PointMul(dir, Radius()));

                RoundJoin(p1, oc, mState.prevPt);
                RoundJoin(oc, p2, mState.prevPt);
            }
            else
            {
                var normal = new Point(-dir.y, dir.x);
                var prevNormal = new Point(-mState.prevPtDir.y, mState.prevPtDir.x);
                Point prevJoin, currJoin;

                if (orient == Orientation.CounterClockwise)
                {
                    prevJoin = TvgMath.PointAdd(mState.prevPt, TvgMath.PointMul(prevNormal, Radius()));
                    currJoin = TvgMath.PointAdd(mState.prevPt, TvgMath.PointMul(normal, Radius()));
                }
                else
                {
                    prevJoin = TvgMath.PointSub(mState.prevPt, TvgMath.PointMul(prevNormal, Radius()));
                    currJoin = TvgMath.PointSub(mState.prevPt, TvgMath.PointMul(normal, Radius()));
                }

                if (mJoin == StrokeJoin.Miter) Miter(prevJoin, currJoin, mState.prevPt);
                else if (mJoin == StrokeJoin.Bevel) Bevel(prevJoin, currJoin, mState.prevPt);
                else RoundJoin(prevJoin, currJoin, mState.prevPt);
            }
        }

        private void RoundJoin(Point prev, Point curr, Point center)
        {
            var orient = TvgMath.GetOrientation(prev, center, curr);
            if (orient == Orientation.Linear) return;

            mLeftTop.x = MathF.Min(mLeftTop.x, MathF.Min(center.x, MathF.Min(prev.x, curr.x)));
            mLeftTop.y = MathF.Min(mLeftTop.y, MathF.Min(center.y, MathF.Min(prev.y, curr.y)));
            mRightBottom.x = MathF.Max(mRightBottom.x, MathF.Max(center.x, MathF.Max(prev.x, curr.x)));
            mRightBottom.y = MathF.Max(mRightBottom.y, MathF.Max(center.y, MathF.Max(prev.y, curr.y)));

            var startAngle = TvgMath.Atan2(prev.y - center.y, prev.x - center.x);
            var endAngle = TvgMath.Atan2(curr.y - center.y, curr.x - center.x);

            if (orient == Orientation.Clockwise)
            {
                if (endAngle > startAngle) endAngle -= 2 * MathConstants.MATH_PI;
            }
            else
            {
                if (endAngle < startAngle) endAngle += 2 * MathConstants.MATH_PI;
            }

            var arcAngle = endAngle - startAngle;
            var count = TvgMath.ArcSegmentsCnt(arcAngle, Radius());

            var ci = PushVertex(ref mBuffer.vertex, center.x, center.y);
            var pi = PushVertex(ref mBuffer.vertex, prev.x, prev.y);
            var step = (endAngle - startAngle) / (count - 1);

            for (uint i = 1; i < count; i++)
            {
                var angle = startAngle + step * i;
                var outPt = new Point(center.x + MathF.Cos(angle) * Radius(), center.y + MathF.Sin(angle) * Radius());
                var oi = PushVertex(ref mBuffer.vertex, outPt.x, outPt.y);

                mBuffer.index.Push(ci);
                mBuffer.index.Push(pi);
                mBuffer.index.Push(oi);

                pi = oi;

                mLeftTop.x = MathF.Min(mLeftTop.x, outPt.x);
                mLeftTop.y = MathF.Min(mLeftTop.y, outPt.y);
                mRightBottom.x = MathF.Max(mRightBottom.x, outPt.x);
                mRightBottom.y = MathF.Max(mRightBottom.y, outPt.y);
            }
        }

        private void RoundPoint(Point p)
        {
            var count = TvgMath.ArcSegmentsCnt(2.0f * MathConstants.MATH_PI, Radius());
            var ci = PushVertex(ref mBuffer.vertex, p.x, p.y);
            var step = 2.0f * MathConstants.MATH_PI / (count - 1);

            for (uint i = 1; i <= count; i++)
            {
                float angle = i * step;
                var dir = new Point(MathF.Cos(angle), MathF.Sin(angle));
                var outPt = TvgMath.PointAdd(p, TvgMath.PointMul(dir, Radius()));
                var oi = PushVertex(ref mBuffer.vertex, outPt.x, outPt.y);

                if (oi > 1)
                {
                    mBuffer.index.Push(ci);
                    mBuffer.index.Push(oi);
                    mBuffer.index.Push(oi - 1);
                }
            }

            mLeftTop.x = MathF.Min(mLeftTop.x, p.x - Radius());
            mLeftTop.y = MathF.Min(mLeftTop.y, p.y - Radius());
            mRightBottom.x = MathF.Max(mRightBottom.x, p.x + Radius());
            mRightBottom.y = MathF.Max(mRightBottom.y, p.y + Radius());
        }

        private void Miter(Point prev, Point curr, Point center)
        {
            var pp1 = TvgMath.PointSub(prev, center);
            var pp2 = TvgMath.PointSub(curr, center);
            var outDir = TvgMath.PointAdd(pp1, pp2);
            var k = 2f * Radius() * Radius() / (outDir.x * outDir.x + outDir.y * outDir.y);
            var pe = TvgMath.PointMul(outDir, k);

            if (TvgMath.PointLength(pe) >= mMiterLimit * Radius())
            {
                Bevel(prev, curr, center);
                return;
            }

            var joinPt = TvgMath.PointAdd(center, pe);
            var ci = PushVertex(ref mBuffer.vertex, center.x, center.y);
            var cp1 = PushVertex(ref mBuffer.vertex, prev.x, prev.y);
            var cp2 = PushVertex(ref mBuffer.vertex, curr.x, curr.y);
            var e = PushVertex(ref mBuffer.vertex, joinPt.x, joinPt.y);

            mBuffer.index.Push(ci);
            mBuffer.index.Push(cp1);
            mBuffer.index.Push(e);

            mBuffer.index.Push(e);
            mBuffer.index.Push(cp2);
            mBuffer.index.Push(ci);

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

            mBuffer.index.Push(ai);
            mBuffer.index.Push(bi);
            mBuffer.index.Push(ci);
        }

        private void Square(Point p, Point outDir)
        {
            var normal = new Point(-outDir.y, outDir.x);

            var a = TvgMath.PointAdd(p, TvgMath.PointMul(normal, Radius()));
            var b = TvgMath.PointSub(p, TvgMath.PointMul(normal, Radius()));
            var c = TvgMath.PointAdd(a, TvgMath.PointMul(outDir, Radius()));
            var d = TvgMath.PointAdd(b, TvgMath.PointMul(outDir, Radius()));

            var ai = PushVertex(ref mBuffer.vertex, a.x, a.y);
            var bi = PushVertex(ref mBuffer.vertex, b.x, b.y);
            var ci = PushVertex(ref mBuffer.vertex, c.x, c.y);
            var di = PushVertex(ref mBuffer.vertex, d.x, d.y);

            mBuffer.index.Push(ai);
            mBuffer.index.Push(bi);
            mBuffer.index.Push(ci);

            mBuffer.index.Push(ci);
            mBuffer.index.Push(bi);
            mBuffer.index.Push(di);

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

            mBuffer.index.Push(ai);
            mBuffer.index.Push(bi);
            mBuffer.index.Push(ci);

            mBuffer.index.Push(ci);
            mBuffer.index.Push(di);
            mBuffer.index.Push(ai);

            mLeftTop.x = MathF.Min(mLeftTop.x, MathF.Min(MathF.Min(a.x, b.x), MathF.Min(c.x, d.x)));
            mLeftTop.y = MathF.Min(mLeftTop.y, MathF.Min(MathF.Min(a.y, b.y), MathF.Min(c.y, d.y)));
            mRightBottom.x = MathF.Max(mRightBottom.x, MathF.Max(MathF.Max(a.x, b.x), MathF.Max(c.x, d.x)));
            mRightBottom.y = MathF.Max(mRightBottom.y, MathF.Max(MathF.Max(a.y, b.y), MathF.Max(c.y, d.y)));
        }

        private void RoundCap(Point p, Point outDir)
        {
            var normal = new Point(-outDir.y, outDir.x);
            var a = TvgMath.PointAdd(p, TvgMath.PointMul(normal, Radius()));
            var b = TvgMath.PointSub(p, TvgMath.PointMul(normal, Radius()));
            var c = TvgMath.PointAdd(p, TvgMath.PointMul(outDir, Radius()));

            RoundJoin(a, c, p);
            RoundJoin(c, b, p);
        }

        private static uint PushVertex(ref Array<float> array, float x, float y)
        {
            array.Push(x);
            array.Push(y);
            return (array.count - 2) / 2;
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
            ConvexProbe probe = ConvexProbe.Create();
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
                        if (probe.convex && TvgMath.EdgesCross(curve.start, curve.ctrl1, curve.ctrl2, curve.end)) probe.convex = false;

                        var stepCount = curve.Segments();
                        if (stepCount <= 1) stepCount = 2;
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
            mBuffer.vertex.Push(x);
            mBuffer.vertex.Push(y);
            var index = (mBuffer.vertex.count - 2) / 2;
            if (index == 0) bbox.max = bbox.min = new Point(x, y);
            else bbox = new BBox { min = new Point(MathF.Min(bbox.min.x, x), MathF.Min(bbox.min.y, y)), max = new Point(MathF.Max(bbox.max.x, x), MathF.Max(bbox.max.y, y)) };
            return index;
        }

        private void PushTriangle(uint a, uint b, uint c)
        {
            mBuffer.index.Push(a);
            mBuffer.index.Push(b);
            mBuffer.index.Push(c);
        }
    }
}
