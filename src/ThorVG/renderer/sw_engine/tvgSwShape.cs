// Ported from ThorVG/src/renderer/sw_engine/tvgSwShape.cpp

namespace ThorVG
{
    public static unsafe class SwShapeOps
    {
        private static bool _outlineBegin(SwOutline* outline)
        {
            if (outline->input.Empty()) return false;
            outline->cntrs.Push(outline->input.count - 1);
            outline->closed.Push(false);
            outline->input.Push(outline->input[outline->cntrs.Last()]);
            outline->types.Push(SwConstants.SW_CURVE_TYPE_POINT);
            return false;
        }

        private static bool _outlineEnd(SwOutline* outline)
        {
            if (outline->input.Empty()) return false;
            outline->cntrs.Push(outline->input.count - 1);
            outline->closed.Push(false);
            return false;
        }

        private static bool _outlineMoveTo(SwOutline* outline, in Point to, bool closed = false)
        {
            if (!closed) _outlineEnd(outline);
            outline->input.Push(to);
            outline->types.Push(SwConstants.SW_CURVE_TYPE_POINT);
            return false;
        }

        private static void _outlineLineTo(SwOutline* outline, in Point to)
        {
            outline->input.Push(to);
            outline->types.Push(SwConstants.SW_CURVE_TYPE_POINT);
        }

        private static void _outlineCubicTo(SwOutline* outline, in Point ctrl1, in Point ctrl2, in Point to)
        {
            outline->input.Push(ctrl1);
            outline->types.Push(SwConstants.SW_CURVE_TYPE_CUBIC);

            outline->input.Push(ctrl2);
            outline->types.Push(SwConstants.SW_CURVE_TYPE_CUBIC);

            outline->input.Push(to);
            outline->types.Push(SwConstants.SW_CURVE_TYPE_POINT);
        }

        private static bool _outlineClose(SwOutline* outline)
        {
            uint i;
            if (outline->cntrs.count > 0) i = outline->cntrs.Last() + 1;
            else i = 0;

            if (outline->input.count == i) return false;

            outline->input.Push(outline->input[i]);
            outline->cntrs.Push(outline->input.count - 1);
            outline->types.Push(SwConstants.SW_CURVE_TYPE_POINT);
            outline->closed.Push(true);

            return true;
        }

        // =====================================================================
        //  Dash stroke functions
        // =====================================================================

        private static void _drawPoint(SwDashStroke dash, SwOutline* outline, in Point start)
        {
            if (dash.move || dash.pattern![dash.curIdx] < MathConstants.FLOAT_EPSILON)
            {
                _outlineMoveTo(outline, start);
                dash.move = false;
            }
            _outlineLineTo(outline, start);
        }

        private static void _dashLineTo(SwDashStroke dash, SwOutline* outline, in Point to, bool validPoint)
        {
            var cur = new Line { pt1 = dash.ptCur, pt2 = to };
            var len = cur.Length();
            if (TvgMath.Zero(len))
            {
                _outlineMoveTo(outline, dash.ptCur);
            }
            // draw the current line fully
            else if (len <= dash.curLen)
            {
                dash.curLen -= len;
                if (!dash.curOpGap)
                {
                    if (dash.move)
                    {
                        _outlineMoveTo(outline, dash.ptCur);
                        dash.move = false;
                    }
                    _outlineLineTo(outline, to);
                }
            }
            // draw the current line partially
            else
            {
                while (len - dash.curLen > RenderHelper.DASH_PATTERN_THRESHOLD)
                {
                    Line left, right;
                    if (dash.curLen > 0)
                    {
                        len -= dash.curLen;
                        cur.Split(dash.curLen, out left, out right);
                        if (!dash.curOpGap)
                        {
                            if (dash.move || dash.pattern![dash.curIdx] - dash.curLen < MathConstants.FLOAT_EPSILON)
                            {
                                _outlineMoveTo(outline, left.pt1);
                                dash.move = false;
                            }
                            _outlineLineTo(outline, left.pt2);
                        }
                    }
                    else
                    {
                        if (validPoint && !dash.curOpGap) _drawPoint(dash, outline, cur.pt1);
                        right = cur;
                    }
                    dash.curIdx = (dash.curIdx + 1) % (int)dash.cnt;
                    dash.curLen = dash.pattern![dash.curIdx];
                    dash.curOpGap = !dash.curOpGap;
                    cur = right;
                    dash.ptCur = cur.pt1;
                    dash.move = true;
                }
                // leftovers
                dash.curLen -= len;
                if (!dash.curOpGap)
                {
                    if (dash.move)
                    {
                        _outlineMoveTo(outline, cur.pt1);
                        dash.move = false;
                    }
                    _outlineLineTo(outline, cur.pt2);
                }
                if (dash.curLen < 1.0f && !TvgMath.Zero(len))
                {
                    // move to next dash
                    dash.curIdx = (dash.curIdx + 1) % (int)dash.cnt;
                    dash.curLen = dash.pattern![dash.curIdx];
                    dash.curOpGap = !dash.curOpGap;
                }
            }
            dash.ptCur = to;
        }

        private static void _dashCubicTo(SwDashStroke dash, SwOutline* outline, in Point ctrl1, in Point ctrl2, in Point to, bool validPoint)
        {
            var cur = new Bezier(dash.ptCur, ctrl1, ctrl2, to);
            var len = cur.Length();

            // draw the current line fully
            if (TvgMath.Zero(len))
            {
                _outlineMoveTo(outline, dash.ptCur);
            }
            else if (len <= dash.curLen)
            {
                dash.curLen -= len;
                if (!dash.curOpGap)
                {
                    if (dash.move)
                    {
                        _outlineMoveTo(outline, dash.ptCur);
                        dash.move = false;
                    }
                    _outlineCubicTo(outline, ctrl1, ctrl2, to);
                }
            }
            // draw the current line partially
            else
            {
                while ((len - dash.curLen) > RenderHelper.DASH_PATTERN_THRESHOLD)
                {
                    Bezier left, right;
                    if (dash.curLen > 0)
                    {
                        len -= dash.curLen;
                        cur.Split(dash.curLen, out left, out right);
                        if (!dash.curOpGap)
                        {
                            if (dash.move || dash.pattern![dash.curIdx] - dash.curLen < MathConstants.FLOAT_EPSILON)
                            {
                                _outlineMoveTo(outline, left.start);
                                dash.move = false;
                            }
                            _outlineCubicTo(outline, left.ctrl1, left.ctrl2, left.end);
                        }
                    }
                    else
                    {
                        if (validPoint && !dash.curOpGap) _drawPoint(dash, outline, cur.start);
                        right = cur;
                    }
                    dash.curIdx = (dash.curIdx + 1) % (int)dash.cnt;
                    dash.curLen = dash.pattern![dash.curIdx];
                    dash.curOpGap = !dash.curOpGap;
                    cur = right;
                    dash.ptCur = right.start;
                    dash.move = true;
                }
                // leftovers
                dash.curLen -= len;
                if (!dash.curOpGap)
                {
                    if (dash.move)
                    {
                        _outlineMoveTo(outline, cur.start);
                        dash.move = false;
                    }
                    _outlineCubicTo(outline, cur.ctrl1, cur.ctrl2, cur.end);
                }
                if (dash.curLen < 0.1f && !TvgMath.Zero(len))
                {
                    // move to next dash
                    dash.curIdx = (dash.curIdx + 1) % (int)dash.cnt;
                    dash.curLen = dash.pattern![dash.curIdx];
                    dash.curOpGap = !dash.curOpGap;
                }
            }
            dash.ptCur = to;
        }

        private static void _dashClose(SwDashStroke dash, SwOutline* outline, bool validPoint)
        {
            _dashLineTo(dash, outline, dash.ptStart, validPoint);
        }

        private static void _dashMoveTo(SwDashStroke dash, uint offIdx, float offset, in Point pts)
        {
            dash.curIdx = (int)(offIdx % dash.cnt);
            dash.curLen = dash.pattern![dash.curIdx] - offset;
            dash.curOpGap = (offIdx % 2) != 0;
            dash.ptStart = dash.ptCur = pts;
            dash.move = true;
        }

        private static SwOutline* _genDashOutline(RenderShape rshape, SwMpool mpool, uint tid, bool trimmed)
        {
            PathCommand* cmds;
            Point* pts;
            uint cmdCnt, ptsCnt;
            RenderPath? trimmedPath = null;

            if (trimmed)
            {
                trimmedPath = new RenderPath();
                if (!rshape.stroke!.trim.Trim(rshape.path, trimmedPath)) return null;
                cmds = trimmedPath.cmds.data;
                cmdCnt = trimmedPath.cmds.count;
                pts = trimmedPath.pts.data;
                ptsCnt = trimmedPath.pts.count;
            }
            else
            {
                cmds = rshape.path.cmds.data;
                cmdCnt = rshape.path.cmds.count;
                pts = rshape.path.pts.data;
                ptsCnt = rshape.path.pts.count;
            }

            // No actual shape data
            if (cmdCnt == 0 || ptsCnt == 0) return null;

            var dash = new SwDashStroke();
            dash.pattern = rshape.stroke!.dashPattern;
            dash.cnt = rshape.stroke.dashCount;
            var offset = rshape.stroke.dashOffset;

            // offset
            uint offIdx = 0;
            if (!TvgMath.Zero(offset))
            {
                var length = rshape.stroke.dashLength;
                bool isOdd = (dash.cnt % 2) != 0;
                if (isOdd) length *= 2;

                offset = offset % length;
                if (offset < 0) offset += length;

                for (uint i = 0; i < dash.cnt * (uint)(1 + (isOdd ? 1 : 0)); ++i, ++offIdx)
                {
                    var curPattern = dash.pattern![i % dash.cnt];
                    if (offset < curPattern) break;
                    offset -= curPattern;
                }
            }

            var outline = mpool.Outline(tid);

            // must begin with moveTo
            if (cmds[0] == PathCommand.MoveTo)
            {
                _dashMoveTo(dash, offIdx, offset, *pts);
                cmds++;
                pts++;
            }

            // zero length segment with non-butt cap still should be rendered as a point
            var validPoint = rshape.stroke.cap != StrokeCap.Butt;
            while (--cmdCnt > 0)
            {
                switch (*cmds)
                {
                    case PathCommand.Close:
                        _dashClose(dash, outline, validPoint);
                        break;
                    case PathCommand.MoveTo:
                        _dashMoveTo(dash, offIdx, offset, *pts);
                        ++pts;
                        break;
                    case PathCommand.LineTo:
                        _dashLineTo(dash, outline, *pts, validPoint);
                        ++pts;
                        break;
                    case PathCommand.CubicTo:
                        _dashCubicTo(dash, outline, pts[0], pts[1], pts[2], validPoint);
                        pts += 3;
                        break;
                }
                ++cmds;
            }

            _outlineEnd(outline);

            outline->fillRule = rshape.rule;

            return outline;
        }

        // =====================================================================
        //  Outline generation
        // =====================================================================

        private static bool _axisAlignedRect(SwOutline* outline)
        {
            if (outline->output.count != 5) return false;
            if (outline->types[2] == SwConstants.SW_CURVE_TYPE_CUBIC) return false;

            var pt1 = outline->output.data + 0;
            var pt2 = outline->output.data + 1;
            var pt3 = outline->output.data + 2;
            var pt4 = outline->output.data + 3;

            var a = new SwPoint(pt1->x, pt3->y);
            var b = new SwPoint(pt3->x, pt1->y);

            if ((*pt2 == a && *pt4 == b) || (*pt2 == b && *pt4 == a)) return true;

            return false;
        }

        private static SwOutline* _genOutline(RenderShape rshape, SwMpool mpool, uint tid, bool trimmed = false)
        {
            PathCommand* cmds;
            Point* pts;
            uint cmdCnt, ptsCnt;
            RenderPath? trimmedPath = null;

            if (trimmed)
            {
                trimmedPath = new RenderPath();
                if (!rshape.stroke!.trim.Trim(rshape.path, trimmedPath)) return null;
                cmds = trimmedPath.cmds.data;
                cmdCnt = trimmedPath.cmds.count;
                pts = trimmedPath.pts.data;
                ptsCnt = trimmedPath.pts.count;
            }
            else
            {
                cmds = rshape.path.cmds.data;
                cmdCnt = rshape.path.cmds.count;
                pts = rshape.path.pts.data;
                ptsCnt = rshape.path.pts.count;
            }

            if (cmdCnt == 0 || ptsCnt == 0) return null;

            var outline = mpool.Outline(tid);
            var closed = false;

            var remaining = (int)cmdCnt;
            while (remaining-- > 0)
            {
                switch (*cmds)
                {
                    case PathCommand.Close:
                        if (!closed) closed = _outlineClose(outline);
                        break;
                    case PathCommand.MoveTo:
                        closed = _outlineMoveTo(outline, *pts, closed);
                        ++pts;
                        break;
                    case PathCommand.LineTo:
                        if (closed) closed = _outlineBegin(outline);
                        _outlineLineTo(outline, *pts);
                        ++pts;
                        break;
                    case PathCommand.CubicTo:
                        if (closed) closed = _outlineBegin(outline);
                        _outlineCubicTo(outline, pts[0], pts[1], pts[2]);
                        pts += 3;
                        break;
                }
                ++cmds;
            }

            if (!closed) _outlineEnd(outline);

            outline->fillRule = rshape.rule;

            return outline;
        }

        // Public API

        public static bool shapePrepare(SwShape shape, RenderShape rshape, in Matrix transform, in RenderRegion clipBox, ref RenderRegion renderBox, SwMpool mpool, uint tid, bool hasComposite)
        {
            var outlinePtr = _genOutline(rshape, mpool, tid, rshape.Trimpath());
            if (outlinePtr != null)
            {
                SwUtil.Export(outlinePtr, transform, out var bbox);
                shape.fastTrack = !hasComposite && _axisAlignedRect(outlinePtr);
                shape.outline = *outlinePtr;
                shape.hasOutline = true;
                if (SwUtil.BBox(bbox, clipBox, ref renderBox, shape.fastTrack))
                {
                    shape.outline = *outlinePtr;
                    shape.bbox = renderBox;
                    return true;
                }
            }
            return false;
        }

        public static bool shapeGenRle(SwShape shape, in RenderRegion bbox, SwMpool mpool, uint tid, bool antiAlias)
        {
            if (shape.fastTrack) return true;

            if (shape.hasOutline)
            {
                fixed (SwOutline* outlinePtr = &shape.outline)
                {
                    shape.hasRle = SwRleOps.rleRender(ref shape.rle, outlinePtr, bbox, mpool, tid, antiAlias);
                }
            }
            return shape.hasRle;
        }

        public static void shapeDelOutline(SwShape shape)
        {
            shape.hasOutline = false;
        }

        public static void shapeReset(SwShape shape)
        {
            if (shape.hasRle)
            {
                SwRleOps.rleReset(ref shape.rle);
            }
            shape.hasOutline = false;
            shape.bbox.Reset();
            shape.fastTrack = false;
        }

        public static void shapeFree(SwShape shape)
        {
            shape.hasRle = false;
            shapeDelFill(shape);

            if (shape.stroke != null)
            {
                shape.hasStrokeRle = false;
                SwStrokeOps.strokeFree(shape.stroke);
                shape.stroke = null;
            }
        }

        public static void shapeDelStroke(SwShape shape)
        {
            if (shape.stroke == null) return;
            shape.hasStrokeRle = false;
            SwStrokeOps.strokeFree(shape.stroke);
            shape.stroke = null;
        }

        public static void shapeResetStroke(SwShape shape, RenderShape rshape, in Matrix transform, SwMpool mpool, uint tid)
        {
            if (shape.stroke == null) shape.stroke = new SwStroke();
            SwStrokeOps.strokeReset(shape.stroke, rshape, transform, mpool, tid);
            if (shape.hasStrokeRle)
            {
                SwRleOps.rleReset(ref shape.strokeRle);
            }
        }

        public static bool shapeGenStrokeRle(SwShape shape, RenderShape rshape, in Matrix transform, in RenderRegion clipBox, ref RenderRegion renderBox, SwMpool mpool, uint tid, bool antiAlias)
        {
            shapeResetStroke(shape, rshape, transform, mpool, tid);
            SwOutline* shapeOutline = null;
            SwOutline retainedOutline = default;

            // Dash style with/without trimming
            if (rshape.stroke!.dashLength > RenderHelper.DASH_PATTERN_THRESHOLD)
            {
                shapeOutline = _genDashOutline(rshape, mpool, tid, rshape.Trimpath());
            }
            // Trimming & Normal style
            else
            {
                if (shape.hasOutline)
                {
                    retainedOutline = shape.outline;
                    shapeOutline = &retainedOutline;
                }
                else
                {
                    shapeOutline = _genOutline(rshape, mpool, tid, rshape.Trimpath());
                }
            }

            if (shapeOutline == null) return false;

            if (!SwStrokeOps.strokeParseOutline(shape.stroke!, *shapeOutline, mpool, tid)) return false;

            var strokeOutline = SwStrokeOps.strokeExportOutline(shape.stroke!, mpool, tid);
            SwUtil.Export(strokeOutline, transform, out var bbox);
            if (!SwUtil.BBox(bbox, clipBox, ref renderBox, false)) return false;
            shape.hasStrokeRle = SwRleOps.rleRender(ref shape.strokeRle, strokeOutline, renderBox, mpool, tid, antiAlias);
            return shape.hasStrokeRle;
        }

        public static bool shapeGenFillColors(ref SwFill? output, Fill? fill, in Matrix transform, SwSurface surface, byte opacity, bool ctable)
        {
            if (fill == null) return true;
            if (output == null)
            {
                output = new SwFill();
                ctable = true;
            }
            else if (ctable)
            {
                SwFillOps.fillReset(output);
            }
            return SwFillOps.fillGenColorTable(output, fill, transform, surface, opacity, ctable);
        }

        public static void shapeResetFill(SwShape shape)
        {
            if (shape.fill == null)
            {
                shape.fill = new SwFill();
            }
            SwFillOps.fillReset(shape.fill);
        }

        public static void shapeResetStrokeFill(SwShape shape)
        {
            if (shape.stroke!.fill == null)
            {
                shape.stroke.fill = new SwFill();
            }
            SwFillOps.fillReset(shape.stroke.fill);
        }

        public static void shapeDelFill(SwShape shape)
        {
            if (shape.fill == null) return;
            SwFillOps.fillFree(shape.fill);
            shape.fill = null;
        }

        /// <summary>
        /// Compute the stroke bounding box of a shape in world coordinates.
        /// Returns 4 corner points of the axis-aligned bounding box of the stroked outline.
        /// Mirrors C++ shapeStrokeBBox().
        /// </summary>
        public static bool shapeStrokeBBox(SwShape shape, RenderShape rshape, Point[] pt4, in Matrix m, SwMpool mpool)
        {
            if (rshape.StrokeWidth() <= 0.0f) return false;

            var outline = _genOutline(rshape, mpool, 0, rshape.Trimpath());
            if (outline == null) return false;

            if (shape.stroke == null) shape.stroke = new SwStroke();
            SwStrokeOps.strokeReset(shape.stroke, rshape, m, mpool, 0);
            SwStrokeOps.strokeParseOutline(shape.stroke, *outline, mpool, 0);

            var min = new Point(float.MaxValue, float.MaxValue);
            var max = new Point(-float.MaxValue, -float.MaxValue);

            for (int side = 0; side < 2; ++side)
            {
                var border = shape.stroke.borders[side];
                for (uint i = 0; i < border.pts.count; i++)
                {
                    var pts = TvgMath.Transform(border.pts[i], m);
                    if (pts.x < min.x) min.x = pts.x;
                    if (pts.x > max.x) max.x = pts.x;
                    if (pts.y < min.y) min.y = pts.y;
                    if (pts.y > max.y) max.y = pts.y;
                }
            }

            pt4[0] = min;
            pt4[1] = new Point(max.x, min.y);
            pt4[2] = max;
            pt4[3] = new Point(min.x, max.y);

            shapeDelOutline(shape);

            return true;
        }
    }
}
