// TrueType hinting integration for TtfLoader (partial class extension).
//
// Adds hinted glyph loading: when hinting is available and enabled,
// glyph outlines are grid-fitted by the TrueType bytecode interpreter
// before being converted to RenderPath commands.
//
// Ported from SharpFont (MIT, 2015, Michael Popoloski) via Typography (MIT, WinterDev).

using System;
using System.Numerics;

namespace ThorVG
{
    public partial class TtfLoader
    {
        private TtfHinter? _hinter;
        private bool _hintingInitialized;
        internal bool _hintingEnabled;

        /// <summary>
        /// Initialize hinting for this font. Called lazily on first hinted glyph request.
        /// </summary>
        private bool EnsureHinting()
        {
            if (_hintingInitialized) return _hinter != null;
            _hintingInitialized = true;

            if (reader.InitHinting())
            {
                try
                {
                    _hinter = new TtfHinter(reader);
                }
                catch
                {
                    _hinter = null; // font program is invalid — fall back to unhinted
                }
            }
            return _hinter != null;
        }

        /// <summary>
        /// Load a glyph with TrueType hinting applied at the given pixel size.
        /// Falls back to unhinted outline if hinting fails.
        /// </summary>
        private TtfGlyphMetrics? RequestHinted(uint code, float pixelSize)
        {
            if (code == 0) return null;

            // For hinted glyphs we cache per (code, pixelSize) since hinting is size-dependent.
            // For simplicity in this initial implementation, we skip caching of hinted glyphs
            // and always re-hint. The unhinted cache in the base Request() still works for
            // non-hinted paths.
            //
            // TODO: Add a size-aware cache if profiling shows this is a bottleneck.

            if (!EnsureHinting())
                return Request(code); // fall back to unhinted

            // Look up glyph index and metrics
            var tgm = new TtfGlyphMetrics();
            var glyphOffset = reader.Glyph(code, tgm);
            if (tgm.idx == TtfReader.INVALID_GLYPH) return null;

            // Read raw glyph data for hinting
            if (!reader.ReadGlyphForHinting(tgm.idx,
                out var points, out var contourEndPoints, out var instructions,
                out var advanceWidth, out var leftSideBearing,
                out var minX, out var maxY))
            {
                // Composite or empty glyph — use normal path
                if (glyphOffset != 0)
                {
                    reader.Convert(tgm.path, tgm, glyphOffset, new Point(0, 0), 1);
                }
                return tgm;
            }

            // Run the hinting interpreter
            GlyphPointF[] hintedPts;
            try
            {
                hintedPts = _hinter!.HintGlyph(
                    advanceWidth, leftSideBearing, minX, maxY,
                    points, contourEndPoints, instructions, pixelSize);
            }
            catch
            {
                // Hinting failed — fall back to unhinted
                if (glyphOffset != 0)
                    reader.Convert(tgm.path, tgm, glyphOffset, new Point(0, 0), 1);
                return tgm;
            }

            // Convert hinted points to RenderPath commands.
            // The hinted points are already in pixel space (scaled by pxScale),
            // so we need to scale them BACK to font units for the normal rendering pipeline.
            float pxScale = pixelSize / reader.metrics.unitsPerEm;
            float invScale = pxScale > 0 ? 1.0f / pxScale : 1.0f;

            // Convert hinted points back to font-unit space.
            // Negate Y to go from interpreter's Y-up back to ThorVG's Y-down.
            int ptCount = points.Length; // original point count (without phantom points)
            var fontPts = new Point[ptCount];
            for (int i = 0; i < ptCount; i++)
            {
                fontPts[i] = new Point(hintedPts[i].X * invScale, -hintedPts[i].Y * invScale);
            }

            // Convert contour endpoints from ushort[] to uint[]
            var endPts = new uint[contourEndPoints.Length];
            for (int i = 0; i < contourEndPoints.Length; i++)
                endPts[i] = contourEndPoints[i];

            // Build path from hinted points (same algorithm as TtfReader.Convert)
            ConvertPointsToPath(tgm.path, fontPts, endPts, points);

            return tgm;
        }

        /// <summary>
        /// Convert glyph points to RenderPath commands. Same algorithm as TtfReader.Convert
        /// but operates on pre-read point arrays instead of raw binary data.
        /// </summary>
        private static void ConvertPointsToPath(RenderPath path, Point[] pts, uint[] endPts, GlyphPointF[] origPts)
        {
            uint ptsCnt = (uint)pts.Length;
            uint cntrsCnt = (uint)endPts.Length;

            path.cmds.Reserve(path.cmds.count + ptsCnt);
            path.pts.Reserve(path.pts.count + ptsCnt);

            uint begin = 0;
            for (uint i = 0; i < cntrsCnt; ++i)
            {
                bool offCurve = !origPts[begin].OnCurve;
                Point ptsBegin;
                if (offCurve)
                {
                    ptsBegin = new Point(
                        (pts[begin].x + pts[endPts[i]].x) * 0.5f,
                        (pts[begin].y + pts[endPts[i]].y) * 0.5f);
                }
                else
                {
                    ptsBegin = pts[begin];
                }
                path.MoveTo(ptsBegin);

                var cnt = endPts[i] - begin + 1;
                for (uint x = 1; x < cnt; ++x)
                {
                    if (origPts[begin + x].OnCurve)
                    {
                        if (offCurve)
                        {
                            var last = path.pts.Last();
                            var prev = pts[begin + x - 1];
                            var cur = pts[begin + x];
                            path.CubicTo(
                                new Point(last.x + (2.0f / 3.0f) * (prev.x - last.x), last.y + (2.0f / 3.0f) * (prev.y - last.y)),
                                new Point(cur.x + (2.0f / 3.0f) * (prev.x - cur.x), cur.y + (2.0f / 3.0f) * (prev.y - cur.y)),
                                cur);
                            offCurve = false;
                        }
                        else
                        {
                            path.LineTo(pts[begin + x]);
                        }
                    }
                    else
                    {
                        if (offCurve)
                        {
                            var end = new Point(
                                (pts[begin + x].x + pts[begin + x - 1].x) * 0.5f,
                                (pts[begin + x].y + pts[begin + x - 1].y) * 0.5f);
                            var last = path.pts.Last();
                            var prev = pts[begin + x - 1];
                            path.CubicTo(
                                new Point(last.x + (2.0f / 3.0f) * (prev.x - last.x), last.y + (2.0f / 3.0f) * (prev.y - last.y)),
                                new Point(end.x + (2.0f / 3.0f) * (prev.x - end.x), end.y + (2.0f / 3.0f) * (prev.y - end.y)),
                                end);
                        }
                        else
                        {
                            offCurve = true;
                        }
                    }
                }

                if (offCurve)
                {
                    var last = path.pts.Last();
                    var prev = pts[begin + cnt - 1];
                    path.CubicTo(
                        new Point(last.x + (2.0f / 3.0f) * (prev.x - last.x), last.y + (2.0f / 3.0f) * (prev.y - last.y)),
                        new Point(ptsBegin.x + (2.0f / 3.0f) * (prev.x - ptsBegin.x), ptsBegin.y + (2.0f / 3.0f) * (prev.y - ptsBegin.y)),
                        ptsBegin);
                }
                path.Close();
                begin = endPts[i] + 1;
            }
        }
    }
}
