// Ported from ThorVG/src/renderer/sw_engine/tvgSwRle.cpp
// Based on FreeType's scanline converter.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ThorVG
{
    public static unsafe class SwRleOps
    {
        private const int PIXEL_BITS = 8;
        private const int ONE_PIXEL = (1 << PIXEL_BITS);
        private const int BAND_SIZE = 40;

        private struct Band
        {
            public int min, max;
        }

        private struct RleWorker
        {
            public SwRle rle;

            public SwPoint cellPos;
            public SwPoint cellMin;
            public SwPoint cellMax;
            public int cellXCnt;
            public int cellYCnt;

            public long area;
            public int cover;

            public SwCell* cells;
            public long maxCells;
            public long cellsCnt;

            public SwPoint pos;

            public fixed long bezStackX[32 * 3 + 1]; // Using separate arrays since SwPoint is a struct
            public fixed long bezStackY[32 * 3 + 1];
            // We'll use managed arrays instead of fixed buffers for SwPoint arrays
            // since SwPoint contains two ints
            public SwPoint* bezStack;
            public SwPoint* lineStack;
            public int* levStack;

            public SwOutline* outline;

            public int bandSize;
            public int bandShoot;

            public SwCell* buffer;
            public uint bufferSize;

            public SwCell** yCells;
            public int yCnt;

            public bool invalid;
            public bool antiAlias;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static SwPoint UPSCALE(SwPoint pt)
        {
            return new SwPoint((int)((uint)pt.x << (PIXEL_BITS - 6)),
                               (int)((uint)pt.y << (PIXEL_BITS - 6)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int TRUNC(int x)
        {
            return x >> PIXEL_BITS;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static SwPoint TRUNC(SwPoint pt)
        {
            return new SwPoint(TRUNC(pt.x), TRUNC(pt.y));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static SwPoint FRACT(SwPoint pt)
        {
            return new SwPoint(pt.x & (ONE_PIXEL - 1), pt.y & (ONE_PIXEL - 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int HYPOT(SwPoint pt)
        {
            if (pt.x < 0) pt.x = -pt.x;
            if (pt.y < 0) pt.y = -pt.y;
            return ((pt.x > pt.y) ? (pt.x + (3 * pt.y >> 3)) : (pt.y + (3 * pt.x >> 3)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint SAFE_HYPOT(SwPoint pt1, SwPoint pt2)
        {
            var x = (uint)Math.Abs((long)pt1.x - (long)pt2.x);
            var y = (uint)Math.Abs((long)pt1.y - (long)pt2.y);
            return (x > y) ? (x + (3 * y >> 3)) : (y + (3 * x >> 3));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SW_UDIV(long a, long b)
        {
            return (int)(((ulong)a * (ulong)b) >> 32);
        }

        private static void _horizLine(ref RleWorker rw, int x, int y, int area, int aCount)
        {
            x += rw.cellMin.x;
            y += rw.cellMin.y;

            if (y < rw.cellMin.y || y >= rw.cellMax.y) return;

            var coverage = (int)(area >> (PIXEL_BITS * 2 + 1 - 8));
            if (coverage < 0) coverage = -coverage;

            if (rw.outline->fillRule == FillRule.EvenOdd)
            {
                coverage &= 511;
                if (coverage > 255) coverage = 511 - coverage;
            }
            else
            {
                if (coverage > 255) coverage = 255;
            }

            if (coverage == 0) return;

            if (x >= short.MaxValue || y >= short.MaxValue) return;

            if (!rw.antiAlias) coverage = 255;

            // see whether we can add this span to the current list
            if (!rw.rle.spans.Empty())
            {
                ref var span = ref rw.rle.spans.Last();
                if ((span.coverage == coverage) && (span.y == y) && (span.x + span.len == x))
                {
                    int xOver = 0;
                    if (x + aCount >= rw.cellMax.x) xOver -= (x + aCount - rw.cellMax.x);
                    if (x < rw.cellMin.x) xOver -= (rw.cellMin.x - x);
                    span.len = (ushort)(span.len + aCount + xOver);
                    return;
                }
            }

            // Clip x range
            int xo = 0;
            if (x + aCount >= rw.cellMax.x) xo -= (x + aCount - rw.cellMax.x);
            if (x < rw.cellMin.x)
            {
                xo -= (rw.cellMin.x - x);
                x = rw.cellMin.x;
            }

            if (aCount + xo <= 0) return;

            ref var newSpan = ref rw.rle.spans.Next();
            newSpan.x = (ushort)x;
            newSpan.y = (ushort)y;
            newSpan.len = (ushort)(aCount + xo);
            newSpan.coverage = (byte)coverage;
        }

        private static void _sweep(ref RleWorker rw)
        {
            if (rw.cellsCnt == 0) return;

            for (int y = 0; y < rw.yCnt; ++y)
            {
                var cover = 0;
                var x = 0;
                var cell = rw.yCells[y];

                while (cell != null)
                {
                    if (cell->x > x && cover != 0) _horizLine(ref rw, x, y, cover * (ONE_PIXEL * 2), cell->x - x);
                    cover += cell->cover;
                    var area = cover * (ONE_PIXEL * 2) - (int)cell->area;
                    if (area != 0 && cell->x >= 0) _horizLine(ref rw, cell->x, y, area, 1);
                    x = cell->x + 1;
                    cell = cell->next;
                }

                if (cover != 0) _horizLine(ref rw, x, y, cover * (ONE_PIXEL * 2), rw.cellXCnt - x);
            }
        }

        private static SwCell* _findCell(ref RleWorker rw)
        {
            var x = rw.cellPos.x;
            if (x > rw.cellXCnt) x = rw.cellXCnt;

            // Bounds check: cellPos.y must be within the yCells array
            if ((uint)rw.cellPos.y >= (uint)rw.yCnt) return null;

            var pcell = &rw.yCells[rw.cellPos.y];

            while (true)
            {
                var cell = *pcell;
                if (cell == null || cell->x > x) break;
                if (cell->x == x) return cell;
                pcell = &cell->next;
            }

            if (rw.cellsCnt >= rw.maxCells) return null;

            var newCell = rw.cells + rw.cellsCnt++;
            newCell->x = x;
            newCell->area = 0;
            newCell->cover = 0;
            newCell->next = *pcell;
            *pcell = newCell;

            return newCell;
        }

        private static bool _recordCell(ref RleWorker rw)
        {
            if (rw.area != 0 || rw.cover != 0)
            {
                var cell = _findCell(ref rw);
                if (cell == null) return false;
                cell->area += rw.area;
                cell->cover += rw.cover;
            }
            return true;
        }

        private static bool _setCell(ref RleWorker rw, SwPoint pos)
        {
            pos = pos - rw.cellMin;

            if (pos.x < 0) pos.x = -1;
            else if (pos.x > rw.cellMax.x) pos.x = rw.cellMax.x;

            if (pos != rw.cellPos)
            {
                if (!rw.invalid && !_recordCell(ref rw)) return false;
                rw.area = rw.cover = 0;
                rw.cellPos = pos;
            }
            rw.invalid = ((uint)pos.y >= (uint)rw.cellYCnt || pos.x >= rw.cellXCnt);

            return true;
        }

        private static bool _startCell(ref RleWorker rw, SwPoint pos)
        {
            if (pos.x > rw.cellMax.x) pos.x = rw.cellMax.x;
            if (pos.x < rw.cellMin.x) pos.x = rw.cellMin.x - 1;

            rw.area = 0;
            rw.cover = 0;
            rw.cellPos = pos - rw.cellMin;
            rw.invalid = false;

            return _setCell(ref rw, pos);
        }

        private static bool _moveTo(ref RleWorker rw, SwPoint to)
        {
            if (!rw.invalid && !_recordCell(ref rw)) return false;
            if (!_startCell(ref rw, TRUNC(to))) return false;
            rw.pos = to;
            return true;
        }

        private static bool _lineTo(ref RleWorker rw, SwPoint to)
        {
            var e1 = TRUNC(rw.pos);
            var e2 = TRUNC(to);

            // vertical clipping
            if ((e1.y >= rw.cellMax.y && e2.y >= rw.cellMax.y) || (e1.y < rw.cellMin.y && e2.y < rw.cellMin.y))
            {
                rw.pos = to;
                return true;
            }

            var line = rw.lineStack;
            line[0] = to;
            line[1] = rw.pos;

            while (true)
            {
                if (SAFE_HYPOT(line[0], line[1]) > short.MaxValue)
                {
                    SwMath.mathSplitLine(line);
                    ++line;
                    continue;
                }
                var diff = line[0] - line[1];
                e1 = TRUNC(line[1]);
                e2 = TRUNC(line[0]);

                var f1 = FRACT(line[1]);
                SwPoint f2;

                if (e1 == e2)
                {
                    // inside one cell
                }
                else if (diff.y == 0)
                {
                    // horizontal line
                    e1.x = e2.x;
                    if (!_setCell(ref rw, e1)) return false;
                }
                else if (diff.x == 0)
                {
                    if (diff.y > 0)
                    {
                        do
                        {
                            f2.y = ONE_PIXEL;
                            rw.cover += (f2.y - f1.y);
                            rw.area += (long)(f2.y - f1.y) * f1.x * 2;
                            f1.y = 0;
                            ++e1.y;
                            if (!_setCell(ref rw, e1)) return false;
                        } while (e1.y != e2.y);
                    }
                    else
                    {
                        do
                        {
                            f2.y = 0;
                            rw.cover += (f2.y - f1.y);
                            rw.area += (long)(f2.y - f1.y) * f1.x * 2;
                            f1.y = ONE_PIXEL;
                            --e1.y;
                            if (!_setCell(ref rw, e1)) return false;
                        } while (e1.y != e2.y);
                    }
                }
                else
                {
                    long prod = (long)diff.x * f1.y - (long)diff.y * f1.x;
                    var dxr = (e1.x != e2.x) ? (long)0xffffffff / diff.x : 0L;
                    var dyr = (e1.y != e2.y) ? (long)0xffffffff / diff.y : 0L;
                    var px = (long)diff.x * ONE_PIXEL;
                    var py = (long)diff.y * ONE_PIXEL;

                    do
                    {
                        if (prod <= 0 && prod - px > 0)
                        {
                            f2 = new SwPoint(0, SW_UDIV(-prod, -dxr));
                            prod -= py;
                            rw.cover += (f2.y - f1.y);
                            rw.area += (long)(f2.y - f1.y) * (f1.x + f2.x);
                            f1 = new SwPoint(ONE_PIXEL, f2.y);
                            --e1.x;
                        }
                        else if (prod - px <= 0 && prod - px + py > 0)
                        {
                            prod -= px;
                            f2 = new SwPoint(SW_UDIV(-prod, dyr), ONE_PIXEL);
                            rw.cover += (f2.y - f1.y);
                            rw.area += (long)(f2.y - f1.y) * (f1.x + f2.x);
                            f1 = new SwPoint(f2.x, 0);
                            ++e1.y;
                        }
                        else if (prod - px + py <= 0 && prod + py >= 0)
                        {
                            prod += py;
                            f2 = new SwPoint(ONE_PIXEL, SW_UDIV(prod, dxr));
                            rw.cover += (f2.y - f1.y);
                            rw.area += (long)(f2.y - f1.y) * (f1.x + f2.x);
                            f1 = new SwPoint(0, f2.y);
                            ++e1.x;
                        }
                        else
                        {
                            f2 = new SwPoint(SW_UDIV(prod, -dyr), 0);
                            prod += px;
                            rw.cover += (f2.y - f1.y);
                            rw.area += (long)(f2.y - f1.y) * (f1.x + f2.x);
                            f1 = new SwPoint(f2.x, ONE_PIXEL);
                            --e1.y;
                        }

                        if (!_setCell(ref rw, e1)) return false;
                    } while (e1 != e2);
                }

                f2 = FRACT(line[0]);
                rw.cover += (f2.y - f1.y);
                rw.area += (long)(f2.y - f1.y) * (f1.x + f2.x);
                rw.pos = line[0];

                if (line == rw.lineStack) return true;
                --line;
            }
        }

        private static bool _cubicTo(ref RleWorker rw, SwPoint ctrl1, SwPoint ctrl2, SwPoint to)
        {
            var arc = rw.bezStack;
            arc[0] = to;
            arc[1] = ctrl2;
            arc[2] = ctrl1;
            arc[3] = rw.pos;

            var min = arc[0].y;
            var max = arc[0].y;

            for (var i = 1; i < 4; ++i)
            {
                var y = arc[i].y;
                if (y < min) min = y;
                if (y > max) max = y;
            }

            // Initial check - if entirely outside band, just draw a line
            bool shouldDraw = (TRUNC(min) >= rw.cellMax.y || TRUNC(max) < rw.cellMin.y);

            while (true)
            {
                if (!shouldDraw)
                {
                    var diff = arc[3] - arc[0];
                    var L = HYPOT(diff);

                    bool needSplit = false;

                    if (L > short.MaxValue)
                    {
                        needSplit = true;
                    }
                    else
                    {
                        var sLimit = L * (ONE_PIXEL / 6);

                        var diff1 = arc[1] - arc[0];
                        var s = (long)diff.y * diff1.x - (long)diff.x * diff1.y;
                        if (s < 0) s = -s;
                        if (s > sLimit)
                        {
                            needSplit = true;
                        }
                        else
                        {
                            var diff2 = arc[2] - arc[0];
                            s = (long)diff.y * diff2.x - (long)diff.x * diff2.y;
                            if (s < 0) s = -s;
                            if (s > sLimit)
                            {
                                needSplit = true;
                            }
                            else if ((long)diff1.x * (diff1.x - diff.x) + (long)diff1.y * (diff1.y - diff.y) > 0 ||
                                     (long)diff2.x * (diff2.x - diff.x) + (long)diff2.y * (diff2.y - diff.y) > 0)
                            {
                                needSplit = true;
                            }
                        }
                    }

                    if (needSplit)
                    {
                        SwMath.mathSplitCubic(arc);
                        arc += 3;
                        shouldDraw = false;
                        continue;
                    }
                }

                // draw
                if (!_lineTo(ref rw, arc[0])) return false;
                if (arc == rw.bezStack) return true;
                arc -= 3;
                shouldDraw = false;
            }
        }

        private static bool _decomposeOutline(ref RleWorker rw)
        {
            var outline = rw.outline;
            var first = 0u;

            for (uint ci = 0; ci < outline->cntrs.count; ci++)
            {
                var last = outline->cntrs[ci];
                var limit = outline->pts.data + last;
                var start = UPSCALE(outline->pts[first]);
                var pt = outline->pts.data + first;
                var types = outline->types.data + first;
                ++types;

                if (!_moveTo(ref rw, UPSCALE(outline->pts[first]))) return false;

                while (pt < limit)
                {
                    if (types[0] == SwConstants.SW_CURVE_TYPE_POINT)
                    {
                        ++pt;
                        ++types;
                        if (!_lineTo(ref rw, UPSCALE(*pt))) return false;
                    }
                    else
                    {
                        pt += 3;
                        types += 3;
                        if (pt <= limit)
                        {
                            if (!_cubicTo(ref rw, UPSCALE(pt[-2]), UPSCALE(pt[-1]), UPSCALE(pt[0]))) return false;
                        }
                        else if (pt - 1 == limit)
                        {
                            if (!_cubicTo(ref rw, UPSCALE(pt[-2]), UPSCALE(pt[-1]), start)) return false;
                        }
                        else goto close;
                    }
                }
            close:
                if (!_lineTo(ref rw, start)) return false;
                first = last + 1;
            }

            return true;
        }

        private static bool _genRle(ref RleWorker rw)
        {
            if (!_decomposeOutline(ref rw)) return false;
            if (!rw.invalid && !_recordCell(ref rw)) return false;
            return true;
        }

        public static bool rleRender(ref SwRle rle, SwOutline* outline, in RenderRegion bbox, SwMpool mpool, uint tid, bool antiAlias)
        {
            if (outline == null) return false;

            var cellPool = mpool.Cell(tid);
            var reqSize = (uint)(Math.Max(bbox.W(), bbox.H()) * 0.75f) * (uint)sizeof(SwCell);

            if (reqSize > cellPool.size)
            {
                cellPool.size = ((reqSize + (reqSize >> 2)) / (uint)sizeof(SwCell)) * (uint)sizeof(SwCell);
                if (cellPool.buffer != null)
                    NativeMemory.Free(cellPool.buffer);
                cellPool.buffer = (SwCell*)NativeMemory.Alloc(cellPool.size);
            }

            // Allocate managed arrays for the bezStack/lineStack/levStack
            var bezStackMem = stackalloc SwPoint[32 * 3 + 1];
            var lineStackMem = stackalloc SwPoint[32 + 1];
            var levStackMem = stackalloc int[32];

            RleWorker rw;
            rw.buffer = cellPool.buffer;
            rw.bufferSize = cellPool.size;
            rw.yCells = (SwCell**)cellPool.buffer;
            rw.cells = null;
            rw.maxCells = 0;
            rw.cellsCnt = 0;
            rw.area = 0;
            rw.cover = 0;
            rw.invalid = true;
            rw.cellMin = new SwPoint(bbox.min.x, bbox.min.y);
            rw.cellMax = new SwPoint(bbox.max.x, bbox.max.y);
            rw.cellXCnt = rw.cellMax.x - rw.cellMin.x;
            rw.cellYCnt = rw.cellMax.y - rw.cellMin.y;
            rw.outline = outline;
            rw.bandSize = (int)(rw.bufferSize / (sizeof(SwCell) * 2));
            rw.bandShoot = 0;
            rw.antiAlias = antiAlias;
            rw.bezStack = bezStackMem;
            rw.lineStack = lineStackMem;
            rw.levStack = levStackMem;
            rw.pos = default;
            rw.cellPos = default;

            rw.rle = rle;
            rw.rle.spans.Clear();
            rw.rle.spans.Reserve(256);

            // Generate RLE
            var bands = stackalloc Band[BAND_SIZE];
            Band* band;

            var bandCnt = (int)((rw.cellMax.y - rw.cellMin.y) / rw.bandSize);
            if (bandCnt == 0) bandCnt = 1;
            else if (bandCnt >= BAND_SIZE) bandCnt = BAND_SIZE - 1;

            var min = rw.cellMin.y;
            var yMax = rw.cellMax.y;
            int max;

            for (int n = 0; n < bandCnt; ++n, min = max)
            {
                max = min + rw.bandSize;
                if (n == bandCnt - 1 || max > yMax) max = yMax;

                bands[0].min = min;
                bands[0].max = max;
                band = bands;

                while (band >= bands)
                {
                    rw.yCells = (SwCell**)rw.buffer;
                    rw.yCnt = band->max - band->min;

                    var cellStart = sizeof(SwCell*) * rw.yCnt;
                    var cellMod = cellStart % sizeof(SwCell);

                    if (cellMod > 0) cellStart += sizeof(SwCell) - cellMod;

                    var cellsMax = (SwCell*)((byte*)rw.buffer + rw.bufferSize);
                    rw.cells = (SwCell*)((byte*)rw.buffer + cellStart);

                    if (rw.cells >= cellsMax) goto reduce_bands;

                    rw.maxCells = cellsMax - rw.cells;
                    if (rw.maxCells < 2) goto reduce_bands;

                    for (int y = 0; y < rw.yCnt; ++y)
                        rw.yCells[y] = null;

                    rw.cellsCnt = 0;
                    rw.invalid = true;
                    rw.cellMin.y = band->min;
                    rw.cellMax.y = band->max;
                    rw.cellYCnt = band->max - band->min;

                    if (_genRle(ref rw))
                    {
                        _sweep(ref rw);
                        --band;
                        continue;
                    }

                reduce_bands:
                    var bottom = band->min;
                    var top = band->max;
                    var middle = bottom + ((top - bottom) >> 1);

                    if (middle == bottom)
                    {
                        return false;
                    }

                    if (bottom - top >= rw.bandSize) ++rw.bandShoot;

                    band[1].min = bottom;
                    band[1].max = middle;
                    band[0].min = middle;
                    band[0].max = top;
                    ++band;
                }
            }

            if (rw.bandShoot > 8 && rw.bandSize > 16)
            {
                rw.bandSize >>= 1;
            }

            rle = rw.rle;
            return true;
        }

        public static SwRle rleRender(in RenderRegion bbox)
        {
            var rle = new SwRle();
            rle.spans.Reserve(bbox.H());
            rle.spans.count = bbox.H();

            var x = (ushort)bbox.min.x;
            var y = (ushort)bbox.min.y;
            var len = (ushort)bbox.W();

            for (uint i = 0; i < rle.spans.count; i++)
            {
                rle.spans[i] = new SwSpan { x = x, y = (ushort)(y + i), len = len, coverage = 255 };
            }

            return rle;
        }

        public static void rleReset(ref SwRle rle)
        {
            rle.spans.Clear();
        }




        public static bool rleClip(ref SwRle rle, in SwRle clip)
        {
            if (rle.spans.Empty() || clip.spans.Empty()) return false;

            var output = new Array<SwSpan>(0);
            output.Reserve(Math.Max(rle.spans.count, clip.spans.count));

            SwSpan* end;
            var spans = rle.Fetch(clip.spans.First().y, clip.spans.Last().y, out end);

            if (spans >= end)
            {
                rle.spans.Clear();
                return false;
            }

            SwSpan* cend;
            var cspans = clip.Fetch(spans->y, (uint)(end - 1)->y, out cend);

            while (spans < end && cspans < cend)
            {
                if (cspans->y > spans->y) { ++spans; continue; }
                if (spans->y > cspans->y) { ++cspans; continue; }

                var temp = cspans;
                while (temp < cend && temp->y == cspans->y)
                {
                    if ((spans->x + spans->len) < spans->x || (temp->x + temp->len) < temp->x)
                    {
                        ++temp;
                        continue;
                    }
                    var x = Math.Max(spans->x, temp->x);
                    var len = Math.Min((spans->x + spans->len), (temp->x + temp->len)) - x;
                    if (len > 0)
                    {
                        ref var ns = ref output.Next();
                        ns.x = (ushort)x;
                        ns.y = temp->y;
                        ns.len = (ushort)len;
                        ns.coverage = (byte)(((int)spans->coverage * temp->coverage + 0xff) >> 8);
                    }
                    ++temp;
                }
                ++spans;
            }
            output.MoveTo(ref rle.spans);
            return true;
        }

        public static bool rleClip(ref SwRle rle, in RenderRegion clip)
        {
            if (rle.spans.Empty() || clip.Invalid()) return false;

            var output = new Array<SwSpan>(0);
            output.Reserve(rle.spans.count);
            var data = output.data;
            SwSpan* end;
            var p = rle.Fetch(clip, out end);

            while (p < end)
            {
                if (p->y >= clip.max.y) break;
                if (p->y < clip.min.y || p->x >= clip.max.x || (p->x + p->len) <= clip.min.x)
                {
                    ++p;
                    continue;
                }
                ushort x, len;
                if (p->x < clip.min.x)
                {
                    x = (ushort)clip.min.x;
                    len = Math.Min((ushort)(p->len - (x - p->x)), (ushort)(clip.max.x - x));
                }
                else
                {
                    x = p->x;
                    len = Math.Min(p->len, (ushort)(clip.max.x - x));
                }
                if (len > 0)
                {
                    *data = new SwSpan { x = x, y = p->y, len = len, coverage = p->coverage };
                    ++data;
                    ++output.count;
                }
                ++p;
            }
            output.MoveTo(ref rle.spans);
            return true;
        }

        public static bool rleIntersect(in SwRle rle, in RenderRegion region)
        {
            if (rle.spans.Empty()) return false;

            SwSpan* end;
            var p = rle.Fetch(region, out end);

            while (p < end)
            {
                if (p->y >= region.max.y) break;
                if (p->y < region.min.y || p->x >= region.max.x || (p->x + p->len) <= region.min.x)
                {
                    ++p;
                    continue;
                }
                return true;
            }
            return false;
        }

        public static void rleMerge(ref SwRle rle, in SwRle clip1, in SwRle clip2)
        {
            // Simple merge: concatenate spans from both RLEs
            rle.spans.Clear();
            rle.spans.Reserve(clip1.spans.count + clip2.spans.count);
            rle.spans.Push(clip1.spans);
            rle.spans.Push(clip2.spans);
        }
    }
}
