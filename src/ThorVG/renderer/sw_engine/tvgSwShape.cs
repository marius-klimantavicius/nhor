// Ported from ThorVG/src/renderer/sw_engine/tvgSwShape.cpp

namespace ThorVG
{
    public static unsafe class SwShapeOps
    {
        private static bool _outlineBegin(SwOutline* outline)
        {
            if (outline->pts.Empty()) return false;
            outline->cntrs.Push(outline->pts.count - 1);
            outline->closed.Push(false);
            outline->pts.Push(outline->pts[outline->cntrs.Last()]);
            outline->types.Push(SwConstants.SW_CURVE_TYPE_POINT);
            return false;
        }

        private static bool _outlineEnd(SwOutline* outline)
        {
            if (outline->pts.Empty()) return false;
            outline->cntrs.Push(outline->pts.count - 1);
            outline->closed.Push(false);
            return false;
        }

        private static bool _outlineMoveTo(SwOutline* outline, in Point to, in Matrix transform, bool closed = false)
        {
            if (!closed) _outlineEnd(outline);
            outline->pts.Push(SwMath.mathTransform(to, transform));
            outline->types.Push(SwConstants.SW_CURVE_TYPE_POINT);
            return false;
        }

        private static void _outlineLineTo(SwOutline* outline, in Point to, in Matrix transform)
        {
            outline->pts.Push(SwMath.mathTransform(to, transform));
            outline->types.Push(SwConstants.SW_CURVE_TYPE_POINT);
        }

        private static void _outlineCubicTo(SwOutline* outline, in Point ctrl1, in Point ctrl2, in Point to, in Matrix transform)
        {
            outline->pts.Push(SwMath.mathTransform(ctrl1, transform));
            outline->types.Push(SwConstants.SW_CURVE_TYPE_CUBIC);

            outline->pts.Push(SwMath.mathTransform(ctrl2, transform));
            outline->types.Push(SwConstants.SW_CURVE_TYPE_CUBIC);

            outline->pts.Push(SwMath.mathTransform(to, transform));
            outline->types.Push(SwConstants.SW_CURVE_TYPE_POINT);
        }

        private static bool _outlineClose(SwOutline* outline)
        {
            uint i;
            if (outline->cntrs.count > 0) i = outline->cntrs.Last() + 1;
            else i = 0;

            if (outline->pts.count == i) return false;

            outline->pts.Push(outline->pts[i]);
            outline->cntrs.Push(outline->pts.count - 1);
            outline->types.Push(SwConstants.SW_CURVE_TYPE_POINT);
            outline->closed.Push(true);

            return true;
        }

        // =====================================================================
        //  Dash stroke functions
        // =====================================================================

        private static void _drawPoint(SwDashStroke dash, SwOutline* outline, in Point start, in Matrix transform)
        {
            if (dash.move || dash.pattern![dash.curIdx] < MathConstants.FLOAT_EPSILON)
            {
                _outlineMoveTo(outline, start, transform);
                dash.move = false;
            }
            _outlineLineTo(outline, start, transform);
        }

        private static void _dashLineTo(SwDashStroke dash, SwOutline* outline, in Point to, in Matrix transform, bool validPoint)
        {
            var cur = new Line { pt1 = dash.ptCur, pt2 = to };
            var len = cur.Length();
            if (TvgMath.Zero(len))
            {
                _outlineMoveTo(outline, dash.ptCur, transform);
            }
            // draw the current line fully
            else if (len <= dash.curLen)
            {
                dash.curLen -= len;
                if (!dash.curOpGap)
                {
                    if (dash.move)
                    {
                        _outlineMoveTo(outline, dash.ptCur, transform);
                        dash.move = false;
                    }
                    _outlineLineTo(outline, to, transform);
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
                                _outlineMoveTo(outline, left.pt1, transform);
                                dash.move = false;
                            }
                            _outlineLineTo(outline, left.pt2, transform);
                        }
                    }
                    else
                    {
                        if (validPoint && !dash.curOpGap) _drawPoint(dash, outline, cur.pt1, transform);
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
                        _outlineMoveTo(outline, cur.pt1, transform);
                        dash.move = false;
                    }
                    _outlineLineTo(outline, cur.pt2, transform);
                }
                if (dash.curLen < 1 && SwHelper.TO_SWCOORD(len) > 1)
                {
                    // move to next dash
                    dash.curIdx = (dash.curIdx + 1) % (int)dash.cnt;
                    dash.curLen = dash.pattern![dash.curIdx];
                    dash.curOpGap = !dash.curOpGap;
                }
            }
            dash.ptCur = to;
        }

        private static void _dashCubicTo(SwDashStroke dash, SwOutline* outline, in Point ctrl1, in Point ctrl2, in Point to, in Matrix transform, bool validPoint)
        {
            var cur = new Bezier(dash.ptCur, ctrl1, ctrl2, to);
            var len = cur.Length();

            // draw the current line fully
            if (TvgMath.Zero(len))
            {
                _outlineMoveTo(outline, dash.ptCur, transform);
            }
            else if (len <= dash.curLen)
            {
                dash.curLen -= len;
                if (!dash.curOpGap)
                {
                    if (dash.move)
                    {
                        _outlineMoveTo(outline, dash.ptCur, transform);
                        dash.move = false;
                    }
                    _outlineCubicTo(outline, ctrl1, ctrl2, to, transform);
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
                                _outlineMoveTo(outline, left.start, transform);
                                dash.move = false;
                            }
                            _outlineCubicTo(outline, left.ctrl1, left.ctrl2, left.end, transform);
                        }
                    }
                    else
                    {
                        if (validPoint && !dash.curOpGap) _drawPoint(dash, outline, cur.start, transform);
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
                        _outlineMoveTo(outline, cur.start, transform);
                        dash.move = false;
                    }
                    _outlineCubicTo(outline, cur.ctrl1, cur.ctrl2, cur.end, transform);
                }
                if (dash.curLen < 0.1f && SwHelper.TO_SWCOORD(len) > 1)
                {
                    // move to next dash
                    dash.curIdx = (dash.curIdx + 1) % (int)dash.cnt;
                    dash.curLen = dash.pattern![dash.curIdx];
                    dash.curOpGap = !dash.curOpGap;
                }
            }
            dash.ptCur = to;
        }

        private static void _dashClose(SwDashStroke dash, SwOutline* outline, in Matrix transform, bool validPoint)
        {
            _dashLineTo(dash, outline, dash.ptStart, transform, validPoint);
        }

        private static void _dashMoveTo(SwDashStroke dash, uint offIdx, float offset, in Point pts)
        {
            dash.curIdx = (int)(offIdx % dash.cnt);
            dash.curLen = dash.pattern![dash.curIdx] - offset;
            dash.curOpGap = (offIdx % 2) != 0;
            dash.ptStart = dash.ptCur = pts;
            dash.move = true;
        }

        private static SwOutline* _genDashOutline(RenderShape rshape, in Matrix transform, SwMpool mpool, uint tid, bool trimmed)
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

            var outline = SwMemPool.mpoolReqOutline(mpool, tid);

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
                        _dashClose(dash, outline, transform, validPoint);
                        break;
                    case PathCommand.MoveTo:
                        _dashMoveTo(dash, offIdx, offset, *pts);
                        ++pts;
                        break;
                    case PathCommand.LineTo:
                        _dashLineTo(dash, outline, *pts, transform, validPoint);
                        ++pts;
                        break;
                    case PathCommand.CubicTo:
                        _dashCubicTo(dash, outline, pts[0], pts[1], pts[2], transform, validPoint);
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
            if (outline->pts.count != 5) return false;
            if (outline->types[2] == SwConstants.SW_CURVE_TYPE_CUBIC) return false;

            var pt1 = outline->pts.data + 0;
            var pt2 = outline->pts.data + 1;
            var pt3 = outline->pts.data + 2;
            var pt4 = outline->pts.data + 3;

            var a = new SwPoint(pt1->x, pt3->y);
            var b = new SwPoint(pt3->x, pt1->y);

            if ((*pt2 == a && *pt4 == b) || (*pt2 == b && *pt4 == a)) return true;

            return false;
        }

        private static SwOutline* _genOutline(SwShape shape, RenderShape rshape, in Matrix transform, SwMpool mpool, uint tid, bool hasComposite, bool trimmed = false)
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

            var outline = SwMemPool.mpoolReqOutline(mpool, tid);
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
                        closed = _outlineMoveTo(outline, *pts, transform, closed);
                        ++pts;
                        break;
                    case PathCommand.LineTo:
                        if (closed) closed = _outlineBegin(outline);
                        _outlineLineTo(outline, *pts, transform);
                        ++pts;
                        break;
                    case PathCommand.CubicTo:
                        if (closed) closed = _outlineBegin(outline);
                        _outlineCubicTo(outline, pts[0], pts[1], pts[2], transform);
                        pts += 3;
                        break;
                }
                ++cmds;
            }

            if (!closed) _outlineEnd(outline);

            outline->fillRule = rshape.rule;

            shape.fastTrack = (!hasComposite && _axisAlignedRect(outline));
            return outline;
        }

        // Public API

        public static bool shapePrepare(SwShape shape, RenderShape rshape, in Matrix transform, in RenderRegion clipBox, ref RenderRegion renderBox, SwMpool mpool, uint tid, bool hasComposite)
        {
            var outlinePtr = _genOutline(shape, rshape, transform, mpool, tid, hasComposite, rshape.Trimpath());
            if (outlinePtr != null)
            {
                shape.outline = *outlinePtr;
                shape.hasOutline = true;
                if (SwMath.mathUpdateOutlineBBox(outlinePtr, clipBox, ref renderBox, shape.fastTrack))
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

        public static void shapeDelOutline(SwShape shape, SwMpool mpool, uint tid)
        {
            shape.hasOutline = false;
        }

        public static void shapeReset(SwShape shape)
        {
            if (shape.hasRle)
            {
                SwRleOps.rleReset(ref shape.rle);
            }
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

        public static bool shapeGenStrokeRle(SwShape shape, RenderShape rshape, in Matrix transform, in RenderRegion clipBox, ref RenderRegion renderBox, SwMpool mpool, uint tid)
        {
            SwOutline* shapeOutline = null;

            // Dash style with/without trimming
            if (rshape.stroke!.dashLength > RenderHelper.DASH_PATTERN_THRESHOLD)
            {
                shapeOutline = _genDashOutline(rshape, transform, mpool, tid, rshape.Trimpath());
            }
            // Trimming & Normal style
            else
            {
                // Use existing outline if available, otherwise generate one
                // Copy the outline value into the mpool slot to get a stable pointer
                if (shape.hasOutline)
                {
                    shapeOutline = SwMemPool.mpoolReqOutline(mpool, tid);
                    *shapeOutline = shape.outline;
                }
                else
                {
                    shapeOutline = _genOutline(shape, rshape, transform, mpool, tid, false, rshape.Trimpath());
                }
            }

            if (shapeOutline == null) return false;

            if (!SwStrokeOps.strokeParseOutline(shape.stroke!, *shapeOutline, mpool, tid)) return false;

            var strokeOutline = SwStrokeOps.strokeExportOutline(shape.stroke!, mpool, tid);

            var ret = SwMath.mathUpdateOutlineBBox(strokeOutline, clipBox, ref renderBox, false);
            if (ret)
            {
                shape.hasStrokeRle = SwRleOps.rleRender(ref shape.strokeRle, strokeOutline, renderBox, mpool, tid, true);
            }

            return ret;
        }

        public static bool shapeGenFillColors(SwShape shape, Fill fill, in Matrix transform, SwSurface surface, byte opacity, bool ctable)
        {
            return SwFillOps.fillGenColorTable(shape.fill!, fill, transform, surface, opacity, ctable);
        }

        public static bool shapeGenStrokeFillColors(SwShape shape, Fill fill, in Matrix transform, SwSurface surface, byte opacity, bool ctable)
        {
            return SwFillOps.fillGenColorTable(shape.stroke!.fill!, fill, transform, surface, opacity, ctable);
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
            var outline = _genOutline(shape, rshape, m, mpool, 0, false, rshape.Trimpath());
            if (outline == null) return false;

            if (rshape.StrokeWidth() > 0.0f)
            {
                if (shape.stroke == null) shape.stroke = new SwStroke();
                SwStrokeOps.strokeReset(shape.stroke, rshape, m, mpool, 0);
                SwStrokeOps.strokeParseOutline(shape.stroke, *outline, mpool, 0);

                var min = new SwPoint(int.MaxValue, int.MaxValue);
                var max = new SwPoint(int.MinValue, int.MinValue);

                for (int side = 0; side < 2; ++side)
                {
                    var border = shape.stroke.borders[side];
                    for (uint i = 0; i < border.pts.count; i++)
                    {
                        var pts = border.pts[i];
                        if (pts.x < min.x) min.x = pts.x;
                        if (pts.x > max.x) max.x = pts.x;
                        if (pts.y < min.y) min.y = pts.y;
                        if (pts.y > max.y) max.y = pts.y;
                    }
                }

                pt4[0] = min.ToPoint();
                pt4[1] = new SwPoint(max.x, min.y).ToPoint();
                pt4[2] = max.ToPoint();
                pt4[3] = new SwPoint(min.x, max.y).ToPoint();
            }

            shapeDelOutline(shape, mpool, 0);

            return true;
        }
    }
}
