// Ported from ThorVG/src/loaders/lottie/tvgLottieModifier.h and tvgLottieModifier.cpp

using System;

namespace ThorVG
{
    public abstract class LottieModifier
    {
        public enum ModifierType : byte { Roundness = 0, Offset, PuckerBloat }

        public LottieModifier? next;
        public RenderPath? buffer;
        public ModifierType type;

        protected LottieModifier(RenderPath? buffer, ModifierType type)
        {
            this.buffer = buffer;
            this.type = type;
        }

        public abstract unsafe void Path(PathCommand[] inCmds, int inCmdsCnt, Point[] inPts, int inPtsCnt, Matrix* transform, RenderPath @out);
        public abstract void Polystar(RenderPath @in, RenderPath @out, float outerRoundness, bool hasRoundness);
        public abstract void Rect(RenderPath @in, RenderPath @out, Point pos, Point size, float r, bool clockwise);
        public abstract void Ellipse(RenderPath @in, RenderPath @out, Point center, Point radius, bool clockwise);

        public LottieModifier Decorate(LottieModifier next)
        {
            // let the offset modifier be at the end in this chain
            // roundness doesn't handle lines so far, so roundness should be handled earlier
            // remove this trick once roundness has full coverage.
            // see LottieRoundnessModifier.Modify()
            var p = this;
            while (p != null)
            {
                if (p.next == null && next.type == ModifierType.Offset)
                {
                    p.next = next;
                    return this;
                }
                p = p.next;
            }

            next.next = this;
            return next;
        }
    }

    /************************************************************************/
    /* Internal helper functions                                            */
    /************************************************************************/

    internal static class LottieModifierHelpers
    {
        internal static bool Colinear(Point[] pts, int idx)
        {
            return TvgMath.Zero(TvgMath.PointSub(pts[idx], pts[idx + 1])) &&
                   TvgMath.Zero(TvgMath.PointSub(pts[idx + 2], pts[idx + 3]));
        }
    }

    /************************************************************************/
    /* LottieRoundnessModifier                                              */
    /************************************************************************/

    public class LottieRoundnessModifier : LottieModifier
    {
        public const float ROUNDNESS_EPSILON = 1.0f;

        public float r;

        public LottieRoundnessModifier(RenderPath? buffer, float r) : base(buffer, ModifierType.Roundness)
        {
            this.r = r;
        }

        private Point Rounding(RenderPath @out, Point prev, Point curr, Point nextPt, float r)
        {
            var lenPrev = TvgMath.PointLength(TvgMath.PointSub(prev, curr));
            var rPrev = lenPrev > 0.0f ? 0.5f * MathF.Min(lenPrev * 0.5f, r) / lenPrev : 0.0f;
            var lenNext = TvgMath.PointLength(TvgMath.PointSub(nextPt, curr));
            var rNext = lenNext > 0.0f ? 0.5f * MathF.Min(lenNext * 0.5f, r) / lenNext : 0.0f;
            var dPrev = TvgMath.PointMul(rPrev, TvgMath.PointSub(curr, prev));
            var dNext = TvgMath.PointMul(rNext, TvgMath.PointSub(curr, nextPt));

            @out.LineTo(TvgMath.PointSub(curr, TvgMath.PointMul(2.0f, dPrev)));
            var ret = TvgMath.PointSub(curr, TvgMath.PointMul(2.0f, dNext));
            @out.CubicTo(TvgMath.PointSub(curr, dPrev), TvgMath.PointSub(curr, dNext), ret);
            return ret;
        }

        private unsafe RenderPath Modify(PathCommand[] inCmds, int inCmdsCnt, Point[] inPts, int inPtsCnt, Matrix* transform, RenderPath @out)
        {
            buffer!.Clear();

            var path = (next != null) ? buffer : @out;
            path.cmds.Reserve((uint)(inCmdsCnt * 2));
            path.pts.Reserve((uint)(inPtsCnt * 1.5f));
            var pivot = path.pts.count;
            uint startIndex = 0;
            var rounded = false;
            var roundTo = default(Point);

            // TODO: the line case is omitted.

            for (int iCmds = 0, iPts = 0; iCmds < inCmdsCnt; ++iCmds)
            {
                switch (inCmds[iCmds])
                {
                    case PathCommand.MoveTo:
                    {
                        startIndex = path.pts.count;
                        path.MoveTo(inPts[iPts++]);
                        break;
                    }
                    case PathCommand.CubicTo:
                    {
                        if (iCmds < inCmdsCnt - 1 && LottieModifierHelpers.Colinear(inPts, iPts - 1))
                        {
                            var prev = inPts[iPts - 1];
                            var curr = inPts[iPts + 2];
                            if (inCmds[iCmds + 1] == PathCommand.CubicTo && LottieModifierHelpers.Colinear(inPts, iPts + 2))
                            {
                                roundTo = Rounding(path, prev, curr, inPts[iPts + 5], r);
                                iPts += 3;
                                rounded = true;
                                continue;
                            }
                            else if (inCmds[iCmds + 1] == PathCommand.Close)
                            {
                                roundTo = Rounding(path, prev, curr, inPts[2], r);
                                path.pts[startIndex] = path.pts.Last();
                                iPts += 3;
                                rounded = true;
                                continue;
                            }
                        }
                        path.CubicTo(rounded ? roundTo : inPts[iPts], inPts[iPts + 1], inPts[iPts + 2]);
                        iPts += 3;
                        break;
                    }
                    case PathCommand.Close:
                    {
                        path.Close();
                        break;
                    }
                    default:
                        break;
                }
                rounded = false;
            }

            if (transform != null)
            {
                for (var i = pivot; i < path.pts.count; ++i)
                {
                    TvgMath.TransformInPlace(ref path.pts[i], *transform);
                }
            }

            return path;
        }

        public override unsafe void Path(PathCommand[] inCmds, int inCmdsCnt, Point[] inPts, int inPtsCnt, Matrix* transform, RenderPath @out)
        {
            var result = Modify(inCmds, inCmdsCnt, inPts, inPtsCnt, transform, @out);
            if (next != null) next.Path(result.cmds.ToArray(), (int)result.cmds.count, result.pts.ToArray(), (int)result.pts.count, null, @out);
        }

        public override void Polystar(RenderPath @in, RenderPath @out, float outerRoundness, bool hasRoundness)
        {
            const float ROUNDED_POLYSTAR_MAGIC_NUMBER = 0.47829f;

            buffer!.Clear();

            var path = (next != null) ? buffer : @out;

            var len = TvgMath.PointLength(TvgMath.PointSub(@in.pts[1], @in.pts[2]));
            var rr = len > 0.0f ? ROUNDED_POLYSTAR_MAGIC_NUMBER * MathF.Min(len * 0.5f, this.r) / len : 0.0f;

            if (hasRoundness)
            {
                path.cmds.Grow((uint)(1.5f * @in.cmds.count));
                path.pts.Grow((uint)(4.5f * @in.cmds.count));

                int start = 3 * (TvgMath.Zero(outerRoundness) ? 1 : 0);
                path.MoveTo(@in.pts[(uint)start]);

                for (uint i = (uint)(1 + start); i < @in.pts.count; i += 6)
                {
                    var prev = @in.pts[i];
                    var curr = @in.pts[i + 2];
                    var nextPt = (i < @in.pts.count - (uint)start) ? @in.pts[i + 4] : @in.pts[2];
                    var nextCtrl = (i < @in.pts.count - (uint)start) ? @in.pts[i + 5] : @in.pts[3];
                    var dNext = TvgMath.PointMul(rr, TvgMath.PointSub(curr, nextPt));
                    var dPrev = TvgMath.PointMul(rr, TvgMath.PointSub(curr, prev));

                    var p0 = TvgMath.PointSub(curr, TvgMath.PointMul(2.0f, dPrev));
                    var p1 = TvgMath.PointSub(curr, dPrev);
                    var p2 = TvgMath.PointSub(curr, dNext);
                    var p3 = TvgMath.PointSub(curr, TvgMath.PointMul(2.0f, dNext));

                    path.CubicTo(prev, p0, p0);
                    path.CubicTo(p1, p2, p3);
                    path.CubicTo(p3, nextPt, nextCtrl);
                }
            }
            else
            {
                path.cmds.Grow(2 * @in.cmds.count);
                path.pts.Grow(4 * @in.cmds.count);

                var dPrev = TvgMath.PointMul(rr, TvgMath.PointSub(@in.pts[1], @in.pts[0]));
                var p = TvgMath.PointAdd(@in.pts[0], TvgMath.PointMul(2.0f, dPrev));
                path.MoveTo(p);

                for (uint i = 1; i < @in.pts.count; ++i)
                {
                    var curr = @in.pts[i];
                    var nextPt = (i == @in.pts.count - 1) ? @in.pts[1] : @in.pts[i + 1];
                    var dNext = TvgMath.PointMul(rr, TvgMath.PointSub(curr, nextPt));

                    var p0 = TvgMath.PointSub(curr, TvgMath.PointMul(2.0f, dPrev));
                    var p1 = TvgMath.PointSub(curr, dPrev);
                    var p2 = TvgMath.PointSub(curr, dNext);
                    var p3 = TvgMath.PointSub(curr, TvgMath.PointMul(2.0f, dNext));

                    path.LineTo(p0);
                    path.CubicTo(p1, p2, p3);

                    dPrev = TvgMath.PointMul(-1.0f, dNext);
                }
            }
            path.cmds.Push(PathCommand.Close);

            if (next != null) next.Polystar(path, @out, outerRoundness, hasRoundness);
        }

        public override unsafe void Rect(RenderPath @in, RenderPath @out, Point pos, Point size, float r, bool clockwise)
        {
            buffer!.Clear();

            var path = (next != null) ? buffer : @out;

            if (r == 0.0f) r = MathF.Min(this.r, MathF.Max(size.x, size.y) * 0.5f);

            // we know this is the first request in the chain because other modifiers would not trigger Rect() call
            path.AddRect(pos.x, pos.y, size.x, size.y, r, r, clockwise);

            if (next != null) next.Path(path.cmds.ToArray(), (int)path.cmds.count, path.pts.ToArray(), (int)path.pts.count, null, @out);
        }

        public override void Ellipse(RenderPath @in, RenderPath @out, Point center, Point radius, bool clockwise)
        {
            // bypass because it's already a circle.
            if (next != null) next.Ellipse(@in, @out, center, radius, clockwise);
            else
            {
                @out.cmds.Push(@in.cmds);
                @out.pts.Push(@in.pts);
            }
        }
    }

    /************************************************************************/
    /* LottieOffsetModifier                                                 */
    /************************************************************************/

    public class LottieOffsetModifier : LottieModifier
    {
        public struct State
        {
            public Line line;
            public Line firstLine;
            public uint movetoOutIndex;
            public int movetoInIndex;
            public bool moveto;
        }

        public float offset;
        public float miterLimit;
        public StrokeJoin join;

        public LottieOffsetModifier(RenderPath? buffer, float offset, float miter = 4.0f, StrokeJoin join = StrokeJoin.Round) : base(buffer, ModifierType.Offset)
        {
            this.offset = offset;
            this.miterLimit = miter;
            this.join = join;
        }

        private static bool Intersected(ref Line line1, ref Line line2, out Point intersection, out bool inside)
        {
            intersection = default;
            inside = false;

            if (TvgMath.Zero(TvgMath.PointSub(line1.pt2, line2.pt1)))
            {
                intersection = line1.pt2;
                inside = true;
                return true;
            }

            const float epsilon = 1e-3f;
            float denom = (line1.pt2.x - line1.pt1.x) * (line2.pt2.y - line2.pt1.y) -
                          (line1.pt2.y - line1.pt1.y) * (line2.pt2.x - line2.pt1.x);
            if (MathF.Abs(denom) < epsilon) return false;

            float t = ((line2.pt1.x - line1.pt1.x) * (line2.pt2.y - line2.pt1.y) -
                       (line2.pt1.y - line1.pt1.y) * (line2.pt2.x - line2.pt1.x)) / denom;
            float u = ((line2.pt1.x - line1.pt1.x) * (line1.pt2.y - line1.pt1.y) -
                       (line2.pt1.y - line1.pt1.y) * (line1.pt2.x - line1.pt1.x)) / denom;

            intersection.x = line1.pt1.x + t * (line1.pt2.x - line1.pt1.x);
            intersection.y = line1.pt1.y + t * (line1.pt2.y - line1.pt1.y);
            inside = t >= -epsilon && t <= 1.0f + epsilon && u >= -epsilon && u <= 1.0f + epsilon;

            return true;
        }

        private static Line Shift(Point p1, Point p2, float offset)
        {
            var scaledNormal = TvgMath.PointMul(TvgMath.Normal(p1, p2), offset);
            return new Line { pt1 = TvgMath.PointAdd(p1, scaledNormal), pt2 = TvgMath.PointAdd(p2, scaledNormal) };
        }

        public void Corner(RenderPath @out, ref Line line, ref Line nextLine, uint movetoOutIndex, bool nextClose)
        {
            if (Intersected(ref line, ref nextLine, out var intersect, out var inside))
            {
                if (inside)
                {
                    if (nextClose) @out.pts[movetoOutIndex] = intersect;
                    @out.pts.Push(intersect);
                }
                else
                {
                    @out.pts.Push(line.pt2);
                    if (join == StrokeJoin.Round)
                    {
                        @out.CubicTo(
                            TvgMath.PointMul(TvgMath.PointAdd(line.pt2, intersect), 0.5f),
                            TvgMath.PointMul(TvgMath.PointAdd(nextLine.pt1, intersect), 0.5f),
                            nextLine.pt1);
                    }
                    else if (join == StrokeJoin.Miter)
                    {
                        var norm = TvgMath.Normal(line.pt1, line.pt2);
                        var nextNorm = TvgMath.Normal(nextLine.pt1, nextLine.pt2);
                        var sumNorm = TvgMath.PointAdd(norm, nextNorm);
                        var sumLen = TvgMath.PointLength(sumNorm);
                        var miterDirection = TvgMath.PointDiv(sumNorm, sumLen);
                        if (1.0f <= miterLimit * MathF.Abs(miterDirection.x * norm.x + miterDirection.y * norm.y))
                            @out.LineTo(intersect);
                        @out.LineTo(nextLine.pt1);
                    }
                    else
                    {
                        @out.LineTo(nextLine.pt1);
                    }
                }
            }
            else
            {
                @out.pts.Push(line.pt2);
            }
        }

        public void Line(RenderPath @out, PathCommand[] inCmds, int inCmdsCnt, Point[] inPts, ref int curPt, int curCmd, ref State state, float offsetVal, bool degenerated)
        {
            if (TvgMath.Zero(TvgMath.PointSub(inPts[curPt - 1], inPts[curPt])))
            {
                ++curPt;
                return;
            }

            if (inCmds[curCmd - 1] != PathCommand.LineTo)
                state.line = Shift(inPts[curPt - 1], inPts[curPt], offsetVal);

            if (state.moveto)
            {
                state.movetoOutIndex = @out.pts.count;
                @out.MoveTo(state.line.pt1);
                state.firstLine = state.line;
                state.moveto = false;
            }

            // Lambda equivalent for nonDegeneratedCubic
            bool NonDegeneratedCubic(int cmd, int pt)
            {
                return inCmds[cmd] == PathCommand.CubicTo &&
                       !TvgMath.Zero(TvgMath.PointSub(inPts[pt], inPts[pt + 1])) &&
                       !TvgMath.Zero(TvgMath.PointSub(inPts[pt + 2], inPts[pt + 3]));
            }

            @out.cmds.Push(PathCommand.LineTo);

            int degInt = degenerated ? 1 : 0;

            if (curCmd + 1 == inCmdsCnt || inCmds[curCmd + 1] == PathCommand.MoveTo ||
                NonDegeneratedCubic(curCmd + 1, curPt + degInt))
            {
                @out.pts.Push(state.line.pt2);
                ++curPt;
                return;
            }

            var nextLine = state.firstLine;
            if (inCmds[curCmd + 1] == PathCommand.LineTo)
                nextLine = Shift(inPts[curPt + degInt], inPts[curPt + 1 + degInt], offsetVal);
            else if (inCmds[curCmd + 1] == PathCommand.CubicTo)
                nextLine = Shift(inPts[curPt + 1 + degInt], inPts[curPt + 2 + degInt], offsetVal);
            else if (inCmds[curCmd + 1] == PathCommand.Close &&
                     !TvgMath.Zero(TvgMath.PointSub(inPts[curPt + degInt], inPts[state.movetoInIndex + degInt])))
                nextLine = Shift(inPts[curPt + degInt], inPts[state.movetoInIndex + degInt], offsetVal);

            Corner(@out, ref state.line, ref nextLine, state.movetoOutIndex, inCmds[curCmd + 1] == PathCommand.Close);

            state.line = nextLine;
            ++curPt;
        }

        private void Cubic(RenderPath path, Point[] pts, int ptsOffset, ref State state, float offsetVal, float threshold, ref bool degeneratedLine3)
        {
            var stack = new Array<Bezier>(5);
            bool degeneratedLine1;
            stack.Push(new Bezier(pts[ptsOffset], pts[ptsOffset + 1], pts[ptsOffset + 2], pts[ptsOffset + 3]));

            while (!stack.Empty())
            {
                ref var bezier = ref stack.Last();
                var len = TvgMath.PointLength(TvgMath.PointSub(bezier.start, bezier.ctrl1)) +
                          TvgMath.PointLength(TvgMath.PointSub(bezier.ctrl1, bezier.ctrl2)) +
                          TvgMath.PointLength(TvgMath.PointSub(bezier.ctrl2, bezier.end));

                if (len > threshold * bezier.Length() && len > 1.0f)
                {
                    bezier.Split(0.5f, out Bezier leftHalf);
                    stack.Push(leftHalf);
                    continue;
                }

                var current = bezier;
                stack.Pop();

                degeneratedLine1 = TvgMath.Zero(TvgMath.PointSub(current.start, current.ctrl1));
                var line1 = degeneratedLine1 ? state.line : Shift(current.start, current.ctrl1, offsetVal);
                var line2 = Shift(current.ctrl1, current.ctrl2, offsetVal);

                // line3 from the previous iteration was degenerated to a point - calculate intersection with the last valid line (state.line)
                if (degeneratedLine3)
                {
                    var tempLine = degeneratedLine1 ? line2 : line1;
                    Intersected(ref tempLine, ref state.line, out var intersect, out _);
                    path.pts.Push(intersect);
                    path.pts.Push(intersect);
                }

                degeneratedLine3 = TvgMath.Zero(TvgMath.PointSub(current.ctrl2, current.end));
                var line3 = degeneratedLine3 ? line2 : Shift(current.ctrl2, current.end, offsetVal);
                state.line = line3;

                if (state.moveto)
                {
                    state.movetoOutIndex = path.pts.count;
                    path.MoveTo(line1.pt1);
                    state.firstLine = line1;
                    state.moveto = false;
                }

                if (degeneratedLine1) path.pts.Push(path.pts.Last());
                else
                {
                    Intersected(ref line1, ref line2, out var intersect, out _);
                    path.pts.Push(intersect);
                }

                if (!degeneratedLine3)
                {
                    Intersected(ref line2, ref line3, out var intersect2, out _);
                    path.pts.Push(intersect2);
                    path.pts.Push(line3.pt2);
                }
                path.cmds.Push(PathCommand.CubicTo);
            }
        }

        private unsafe RenderPath Modify(PathCommand[] inCmds, int inCmdsCnt, Point[] inPts, int inPtsCnt, Matrix* transform, RenderPath @out)
        {
            bool Clockwise(Point[] pts, int n)
            {
                var area = 0.0f;
                for (int i = 0; i < n - 1; i++)
                {
                    area += TvgMath.Cross(pts[i], pts[i + 1]);
                }
                area += TvgMath.Cross(pts[n - 1], pts[0]);
                return area < 0.0f;
            }

            buffer!.Clear();

            var path = (next != null) ? buffer : @out;
            path.cmds.Reserve((uint)(inCmdsCnt * 2));
            path.pts.Reserve((uint)(inPtsCnt * (join == StrokeJoin.Round ? 4 : 2)));

            var state = new State();
            var offsetVal = Clockwise(inPts, inPtsCnt) ? this.offset : -this.offset;
            var threshold = 1.0f / MathF.Abs(offsetVal) + 1.0f;
            bool degeneratedLine3 = false;

            for (int iCmd = 0, iPt = 0; iCmd < inCmdsCnt; ++iCmd)
            {
                switch (inCmds[iCmd])
                {
                    case PathCommand.MoveTo:
                    {
                        state.moveto = true;
                        state.movetoInIndex = iPt++;
                        break;
                    }
                    case PathCommand.LineTo:
                    {
                        Line(path, inCmds, inCmdsCnt, inPts, ref iPt, iCmd, ref state, offsetVal, false);
                        break;
                    }
                    case PathCommand.CubicTo:
                    {
                        // cubic degenerated to a line (colinear)
                        if (LottieModifierHelpers.Colinear(inPts, iPt - 1))
                        {
                            ++iPt;
                            Line(path, inCmds, inCmdsCnt, inPts, ref iPt, iCmd, ref state, offsetVal, true);
                            ++iPt;
                            continue;
                        }
                        Cubic(path, inPts, iPt - 1, ref state, offsetVal, threshold, ref degeneratedLine3);
                        iPt += 3;
                        break;
                    }
                    default:
                    {
                        if (!TvgMath.Zero(TvgMath.PointSub(inPts[iPt - 1], inPts[state.movetoInIndex])))
                        {
                            path.cmds.Push(PathCommand.LineTo);
                            Corner(path, ref state.line, ref state.firstLine, state.movetoOutIndex, true);
                        }
                        path.cmds.Push(PathCommand.Close);
                        break;
                    }
                }
            }
            return path;
        }

        public override unsafe void Path(PathCommand[] inCmds, int inCmdsCnt, Point[] inPts, int inPtsCnt, Matrix* transform, RenderPath @out)
        {
            var result = Modify(inCmds, inCmdsCnt, inPts, inPtsCnt, transform, @out);
            if (next != null) next.Path(result.cmds.ToArray(), (int)result.cmds.count, result.pts.ToArray(), (int)result.pts.count, null, @out);
        }

        public override void Polystar(RenderPath @in, RenderPath @out, float outerRoundness, bool hasRoundness)
        {
            unsafe
            {
                var result = Modify(@in.cmds.ToArray(), (int)@in.cmds.count, @in.pts.ToArray(), (int)@in.pts.count, null, @out);
                if (next != null) next.Polystar(result, @out, outerRoundness, hasRoundness);
            }
        }

        public override void Rect(RenderPath @in, RenderPath @out, Point pos, Point size, float r, bool clockwise)
        {
            unsafe
            {
                Path(@in.cmds.ToArray(), (int)@in.cmds.count, @in.pts.ToArray(), (int)@in.pts.count, null, @out);
            }
        }

        public override void Ellipse(RenderPath @in, RenderPath @out, Point center, Point radius, bool clockwise)
        {
            buffer!.Clear();
            var path = (next != null) ? buffer : @out;
            // we know this is the first request in the chain because other modifiers would not trigger Ellipse() call
            path.AddCircle(center.x, center.y, radius.x + offset, radius.y + offset, clockwise);
            if (next != null) next.Ellipse(path, @out, center, radius, clockwise);
        }
    }

    /************************************************************************/
    /* LottiePuckerBloatModifier                                            */
    /************************************************************************/

    public class LottiePuckerBloatModifier : LottieModifier
    {
        public float amount;

        public LottiePuckerBloatModifier(RenderPath? buffer, float amount) : base(buffer, ModifierType.PuckerBloat)
        {
            this.amount = amount;
        }

        private static Point Center(PathCommand[] cmds, int cmdsCnt, Point[] pts)
        {
            var center = new Point(0, 0);
            var count = 0;
            var pIdx = 0;
            var startIdx = 0;

            for (int i = 0; i < cmdsCnt; ++i)
            {
                switch (cmds[i])
                {
                    case PathCommand.MoveTo:
                    {
                        startIdx = pIdx;
                        ++pIdx;
                        break;
                    }
                    case PathCommand.CubicTo:
                    {
                        center = TvgMath.PointAdd(center, TvgMath.PointAdd(pts[pIdx - 1], TvgMath.PointAdd(pts[pIdx], TvgMath.PointAdd(pts[pIdx + 1], pts[pIdx + 2]))));
                        pIdx += 3;
                        count += 4;
                        break;
                    }
                    case PathCommand.LineTo:
                    {
                        center = TvgMath.PointAdd(center, TvgMath.PointAdd(pts[pIdx - 1], pts[pIdx]));
                        ++pIdx;
                        count += 2;
                        break;
                    }
                    case PathCommand.Close:
                    {
                        if (!TvgMath.Zero(TvgMath.PointSub(pts[pIdx - 1], pts[startIdx])))
                        {
                            center = TvgMath.PointAdd(center, TvgMath.PointAdd(pts[pIdx - 1], pts[startIdx]));
                            count += 2;
                        }
                        break;
                    }
                }
            }
            return count > 0 ? TvgMath.PointDiv(center, (float)count) : new Point(0, 0);
        }

        public override unsafe void Path(PathCommand[] inCmds, int inCmdsCnt, Point[] inPts, int inPtsCnt, Matrix* transform, RenderPath @out)
        {
            buffer!.Clear();

            var path = next != null ? buffer : @out;

            // LineTo segments are expanded to CubicTo, so pts capacity can grow up to 3x
            path.cmds.Reserve((uint)inCmdsCnt);
            path.pts.Reserve((uint)(inPtsCnt * 3));

            var center = Center(inCmds, inCmdsCnt, inPts);
            var a = amount * 0.01f;
            var ptsIdx = 0;
            var startPtsIdx = 0;

            for (int i = 0; i < inCmdsCnt; ++i)
            {
                switch (inCmds[i])
                {
                    case PathCommand.MoveTo:
                    {
                        startPtsIdx = ptsIdx;
                        // anchor points move toward center
                        path.pts.Push(TvgMath.PointAdd(inPts[ptsIdx], TvgMath.PointMul(TvgMath.PointSub(center, inPts[ptsIdx]), a)));
                        path.cmds.Push(PathCommand.MoveTo);
                        ++ptsIdx;
                        break;
                    }
                    case PathCommand.CubicTo:
                    {
                        // control points move away from center, end (anchor) moves toward center
                        path.pts.Push(TvgMath.PointSub(inPts[ptsIdx], TvgMath.PointMul(TvgMath.PointSub(center, inPts[ptsIdx]), a)));
                        path.pts.Push(TvgMath.PointSub(inPts[ptsIdx + 1], TvgMath.PointMul(TvgMath.PointSub(center, inPts[ptsIdx + 1]), a)));
                        path.pts.Push(TvgMath.PointAdd(inPts[ptsIdx + 2], TvgMath.PointMul(TvgMath.PointSub(center, inPts[ptsIdx + 2]), a)));
                        path.cmds.Push(PathCommand.CubicTo);
                        ptsIdx += 3;
                        break;
                    }
                    case PathCommand.LineTo:
                    {
                        // convert to CubicTo: prev and curr as control points (away), curr as end (toward)
                        path.pts.Push(TvgMath.PointSub(inPts[ptsIdx - 1], TvgMath.PointMul(TvgMath.PointSub(center, inPts[ptsIdx - 1]), a)));
                        path.pts.Push(TvgMath.PointSub(inPts[ptsIdx], TvgMath.PointMul(TvgMath.PointSub(center, inPts[ptsIdx]), a)));
                        path.pts.Push(TvgMath.PointAdd(inPts[ptsIdx], TvgMath.PointMul(TvgMath.PointSub(center, inPts[ptsIdx]), a)));
                        path.cmds.Push(PathCommand.CubicTo);
                        ++ptsIdx;
                        break;
                    }
                    case PathCommand.Close:
                    {
                        // if last pt != start pt, add implicit closing segment as CubicTo
                        if (!TvgMath.Zero(TvgMath.PointSub(inPts[ptsIdx - 1], inPts[startPtsIdx])))
                        {
                            path.pts.Push(TvgMath.PointSub(inPts[ptsIdx - 1], TvgMath.PointMul(TvgMath.PointSub(center, inPts[ptsIdx - 1]), a)));
                            path.pts.Push(TvgMath.PointSub(inPts[startPtsIdx], TvgMath.PointMul(TvgMath.PointSub(center, inPts[startPtsIdx]), a)));
                            path.pts.Push(TvgMath.PointAdd(inPts[startPtsIdx], TvgMath.PointMul(TvgMath.PointSub(center, inPts[startPtsIdx]), a)));
                            path.cmds.Push(PathCommand.CubicTo);
                        }
                        path.cmds.Push(PathCommand.Close);
                        break;
                    }
                }
            }

            if (transform != null)
            {
                for (uint i = 0; i < path.pts.count; ++i)
                {
                    TvgMath.TransformInPlace(ref path.pts[i], *transform);
                }
            }

            if (next != null) next.Path(path.cmds.ToArray(), (int)path.cmds.count, path.pts.ToArray(), (int)path.pts.count, transform, @out);
        }

        public override void Polystar(RenderPath @in, RenderPath @out, float outerRoundness, bool hasRoundness)
        {
            unsafe { Path(@in.cmds.ToArray(), (int)@in.cmds.count, @in.pts.ToArray(), (int)@in.pts.count, null, @out); }
        }

        public override void Rect(RenderPath @in, RenderPath @out, Point pos, Point size, float r, bool clockwise)
        {
            unsafe { Path(@in.cmds.ToArray(), (int)@in.cmds.count, @in.pts.ToArray(), (int)@in.pts.count, null, @out); }
        }

        public override void Ellipse(RenderPath @in, RenderPath @out, Point center, Point radius, bool clockwise)
        {
            unsafe { Path(@in.cmds.ToArray(), (int)@in.cmds.count, @in.pts.ToArray(), (int)@in.pts.count, null, @out); }
        }
    }
}
