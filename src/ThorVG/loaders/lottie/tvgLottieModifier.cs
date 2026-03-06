// Ported from ThorVG/src/loaders/lottie/tvgLottieModifier.h and tvgLottieModifier.cpp

using System;

namespace ThorVG
{
    public abstract class LottieModifier
    {
        public enum ModifierType : byte { Roundness = 0, Offset }

        public LottieModifier? next;
        public ModifierType type;

        public abstract unsafe bool ModifyPath(PathCommand[] inCmds, int inCmdsCnt, Point[] inPts, int inPtsCnt, Matrix* transform, RenderPath @out);
        public abstract bool ModifyPolystar(RenderPath @in, RenderPath @out, float outerRoundness, bool hasRoundness);

        public LottieModifier Decorate(LottieModifier next)
        {
            // roundness -> offset ordering
            if (next.type == ModifierType.Roundness)
            {
                next.next = this;
                return next;
            }
            this.next = next;
            return this;
        }
    }

    /************************************************************************/
    /* Internal helper functions                                            */
    /************************************************************************/

    internal static class LottieModifierHelpers
    {
        internal static bool Colinear(Point[] pts, int idx)
        {
            // C++: zero(*p - *(p + 1)) && zero(*(p + 2) - *(p + 3))
            // p points to inPts[iPts - 1], so p[0..3] = inPts[iPts-1..iPts+2]
            return TvgMath.Zero(TvgMath.PointSub(pts[idx], pts[idx + 1])) &&
                   TvgMath.Zero(TvgMath.PointSub(pts[idx + 2], pts[idx + 3]));
        }

        internal static Point RoundCorner(RenderPath @out, Point prev, Point curr, Point next, float r)
        {
            var lenPrev = TvgMath.PointLength(TvgMath.PointSub(prev, curr));
            var rPrev = lenPrev > 0.0f ? 0.5f * MathF.Min(lenPrev * 0.5f, r) / lenPrev : 0.0f;
            var lenNext = TvgMath.PointLength(TvgMath.PointSub(next, curr));
            var rNext = lenNext > 0.0f ? 0.5f * MathF.Min(lenNext * 0.5f, r) / lenNext : 0.0f;
            var dPrev = TvgMath.PointMul(rPrev, TvgMath.PointSub(curr, prev));
            var dNext = TvgMath.PointMul(rNext, TvgMath.PointSub(curr, next));

            @out.LineTo(TvgMath.PointSub(curr, TvgMath.PointMul(2.0f, dPrev)));
            var ret = TvgMath.PointSub(curr, TvgMath.PointMul(2.0f, dNext));
            @out.CubicTo(TvgMath.PointSub(curr, dPrev), TvgMath.PointSub(curr, dNext), ret);
            return ret;
        }

        internal static bool Intersect(ref Line line1, ref Line line2, out Point intersection, out bool inside)
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

        internal static Line Offset(Point p1, Point p2, float offset)
        {
            var scaledNormal = TvgMath.PointMul(TvgMath.Normal(p1, p2), offset);
            return new Line { pt1 = TvgMath.PointAdd(p1, scaledNormal), pt2 = TvgMath.PointAdd(p2, scaledNormal) };
        }

        internal static bool Clockwise(Point[] pts, int ptsCnt)
        {
            var area = 0.0f;
            for (int i = 0; i < ptsCnt - 1; i++)
            {
                area += TvgMath.Cross(pts[i], pts[i + 1]);
            }
            area += TvgMath.Cross(pts[ptsCnt - 1], pts[0]);
            return area < 0.0f;
        }
    }

    public class LottieRoundnessModifier : LottieModifier
    {
        public const float ROUNDNESS_EPSILON = 1.0f;

        public RenderPath? buffer;
        public float r;

        public LottieRoundnessModifier(RenderPath? buffer, float r)
        {
            this.buffer = buffer;
            this.r = r;
            type = ModifierType.Roundness;
        }

        public override unsafe bool ModifyPath(PathCommand[] inCmds, int inCmdsCnt, Point[] inPts, int inPtsCnt, Matrix* transform, RenderPath @out)
        {
            buffer!.Clear();

            var path = (next != null) ? buffer : @out;
            path.cmds.Reserve((uint)(inCmdsCnt * 2));
            path.pts.Reserve((uint)(inPtsCnt * 1.5f));
            var pivot = path.pts.count;
            uint startIndex = 0;
            var rounded = false;
            var roundTo = default(Point);

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
                                roundTo = LottieModifierHelpers.RoundCorner(path, prev, curr, inPts[iPts + 5], r);
                                iPts += 3;
                                rounded = true;
                                continue;
                            }
                            else if (inCmds[iCmds + 1] == PathCommand.Close)
                            {
                                roundTo = LottieModifierHelpers.RoundCorner(path, prev, curr, inPts[2], r);
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

            if (next != null) return next.ModifyPath(path.cmds.ToArray(), (int)path.cmds.count, path.pts.ToArray(), (int)path.pts.count, transform, @out);

            return true;
        }

        public override bool ModifyPolystar(RenderPath @in, RenderPath @out, float outerRoundness, bool hasRoundness)
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

            if (next != null) return next.ModifyPolystar(path, @out, outerRoundness, hasRoundness);

            return true;
        }

        public bool ModifyRect(ref Point size, ref float r)
        {
            r = MathF.Min(this.r, MathF.Max(size.x, size.y) * 0.5f);
            return true;
        }
    }

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

        public LottieOffsetModifier(float offset, float miter = 4.0f, StrokeJoin join = StrokeJoin.Round)
        {
            this.offset = offset;
            this.miterLimit = miter;
            this.join = join;
            type = ModifierType.Offset;
        }

        public void Corner(RenderPath @out, ref Line line, ref Line nextLine, uint movetoOutIndex, bool nextClose)
        {
            if (LottieModifierHelpers.Intersect(ref line, ref nextLine, out var intersect, out var inside))
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
                state.line = LottieModifierHelpers.Offset(inPts[curPt - 1], inPts[curPt], offsetVal);

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
                nextLine = LottieModifierHelpers.Offset(inPts[curPt + degInt], inPts[curPt + 1 + degInt], offsetVal);
            else if (inCmds[curCmd + 1] == PathCommand.CubicTo)
                nextLine = LottieModifierHelpers.Offset(inPts[curPt + 1 + degInt], inPts[curPt + 2 + degInt], offsetVal);
            else if (inCmds[curCmd + 1] == PathCommand.Close &&
                     !TvgMath.Zero(TvgMath.PointSub(inPts[curPt + degInt], inPts[state.movetoInIndex + degInt])))
                nextLine = LottieModifierHelpers.Offset(inPts[curPt + degInt], inPts[state.movetoInIndex + degInt], offsetVal);

            Corner(@out, ref state.line, ref nextLine, state.movetoOutIndex, inCmds[curCmd + 1] == PathCommand.Close);

            state.line = nextLine;
            ++curPt;
        }

        public override unsafe bool ModifyPath(PathCommand[] inCmds, int inCmdsCnt, Point[] inPts, int inPtsCnt, Matrix* transform, RenderPath @out)
        {
            if (next != null)
            {
                // C++: TVGERR("LOTTIE", "Offset has a next modifier?");
            }

            @out.cmds.Reserve((uint)(inCmdsCnt * 2));
            @out.pts.Reserve((uint)(inPtsCnt * (join == StrokeJoin.Round ? 4 : 2)));

            var stack = new Array<Bezier>(5);
            var state = new State();
            var offsetVal = LottieModifierHelpers.Clockwise(inPts, inPtsCnt) ? this.offset : -this.offset;
            var threshold = 1.0f / MathF.Abs(offsetVal) + 1.0f;

            for (int iCmd = 0, iPt = 0; iCmd < inCmdsCnt; ++iCmd)
            {
                if (inCmds[iCmd] == PathCommand.MoveTo)
                {
                    state.moveto = true;
                    state.movetoInIndex = iPt++;
                }
                else if (inCmds[iCmd] == PathCommand.LineTo)
                {
                    Line(@out, inCmds, inCmdsCnt, inPts, ref iPt, iCmd, ref state, offsetVal, false);
                }
                else if (inCmds[iCmd] == PathCommand.CubicTo)
                {
                    // cubic degenerated to a line
                    if (TvgMath.Zero(TvgMath.PointSub(inPts[iPt - 1], inPts[iPt])) ||
                        TvgMath.Zero(TvgMath.PointSub(inPts[iPt + 1], inPts[iPt + 2])))
                    {
                        ++iPt;
                        Line(@out, inCmds, inCmdsCnt, inPts, ref iPt, iCmd, ref state, offsetVal, true);
                        ++iPt;
                        continue;
                    }

                    stack.Push(new Bezier(inPts[iPt - 1], inPts[iPt], inPts[iPt + 1], inPts[iPt + 2]));
                    while (!stack.Empty())
                    {
                        ref var bezier = ref stack.Last();
                        var len = TvgMath.PointLength(TvgMath.PointSub(bezier.start, bezier.ctrl1)) +
                                  TvgMath.PointLength(TvgMath.PointSub(bezier.ctrl1, bezier.ctrl2)) +
                                  TvgMath.PointLength(TvgMath.PointSub(bezier.ctrl2, bezier.end));

                        if (len > threshold * bezier.Length())
                        {
                            // bezier.Split modifies bezier in-place to become the right half,
                            // and outputs the left half. Push left on top so it's processed first.
                            bezier.Split(0.5f, out Bezier leftHalf);
                            stack.Push(leftHalf);
                            continue;
                        }

                        var current = bezier;
                        stack.Pop();

                        var line1 = LottieModifierHelpers.Offset(current.start, current.ctrl1, offsetVal);
                        var line2 = LottieModifierHelpers.Offset(current.ctrl1, current.ctrl2, offsetVal);
                        var line3 = LottieModifierHelpers.Offset(current.ctrl2, current.end, offsetVal);

                        if (state.moveto)
                        {
                            state.movetoOutIndex = @out.pts.count;
                            @out.MoveTo(line1.pt1);
                            state.firstLine = line1;
                            state.moveto = false;
                        }

                        LottieModifierHelpers.Intersect(ref line1, ref line2, out var intersect1, out _);
                        @out.pts.Push(intersect1);
                        LottieModifierHelpers.Intersect(ref line2, ref line3, out var intersect2, out _);
                        @out.pts.Push(intersect2);
                        @out.pts.Push(line3.pt2);
                        @out.cmds.Push(PathCommand.CubicTo);
                    }

                    iPt += 3;
                }
                else // PathCommand.Close
                {
                    if (!TvgMath.Zero(TvgMath.PointSub(inPts[iPt - 1], inPts[state.movetoInIndex])))
                    {
                        @out.cmds.Push(PathCommand.LineTo);
                        Corner(@out, ref state.line, ref state.firstLine, state.movetoOutIndex, true);
                    }
                    @out.cmds.Push(PathCommand.Close);
                }
            }
            return true;
        }

        public override bool ModifyPolystar(RenderPath @in, RenderPath @out, float outerRoundness, bool hasRoundness)
        {
            unsafe
            {
                return ModifyPath(@in.cmds.ToArray(), (int)@in.cmds.count, @in.pts.ToArray(), (int)@in.pts.count, null, @out);
            }
        }

        public bool ModifyRect(RenderPath @in, RenderPath @out)
        {
            unsafe
            {
                return ModifyPath(@in.cmds.ToArray(), (int)@in.cmds.count, @in.pts.ToArray(), (int)@in.pts.count, null, @out);
            }
        }

        public bool ModifyEllipse(ref Point radius)
        {
            radius.x += offset;
            radius.y += offset;
            if (radius.x < 0) radius.x = 0;
            if (radius.y < 0) radius.y = 0;
            return true;
        }
    }
}
