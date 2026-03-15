// TrueType hinting integration for TtfReader and TtfLoader.
//
// Extends TtfReader (as partial class) to read hinting tables (fpgm, prep, cvt, maxp)
// and provides TtfHinter — a per-font hinting context that wraps TtfInterpreter.
//
// The hinting pipeline:
//   1. TtfReader.InitHinting() reads hinting tables from font binary
//   2. TtfHinter is created per-font with the table data
//   3. TtfHinter.HintGlyph() takes raw glyph points/contours/instructions,
//      runs the bytecode interpreter, returns grid-fitted points
//   4. TtfLoader converts hinted points → RenderPath commands
//
// Ported from SharpFont (MIT, 2015, Michael Popoloski) via Typography (MIT, WinterDev).

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ThorVG
{
    // =================================================================
    //  TtfReader partial — read hinting tables
    // =================================================================

    public partial class TtfReader
    {
        // Hinting table data (populated by InitHinting)
        internal byte[]? fpgmProgram;
        internal byte[]? prepProgram;
        internal int[]? cvtTable;

        // Full maxp fields needed by the interpreter
        internal int maxStackElements;
        internal int maxStorage;
        internal int maxFunctionDefs;
        internal int maxInstructionDefs;
        internal int maxTwilightPoints;

        /// <summary>
        /// Read hinting tables (fpgm, prep, cvt, full maxp) from the font.
        /// Call after Init(). Safe to call multiple times (idempotent).
        /// Returns true if hinting data was found.
        /// </summary>
        public bool InitHinting()
        {
            if (data == null) return false;

            // --- maxp (full version 1.0 fields) ---
            if (_maxp == 0) _maxp = Table("maxp");
            if (_maxp != 0 && Validate(_maxp, 32) && U32(data, _maxp) >= 0x00010000U)
            {
                maxStackElements = U16(data, _maxp + 24);
                maxStorage = U16(data, _maxp + 18);
                maxFunctionDefs = U16(data, _maxp + 20);
                maxInstructionDefs = U16(data, _maxp + 22);
                maxTwilightPoints = U16(data, _maxp + 16);
            }
            else
            {
                return false; // no hinting without maxp v1
            }

            // --- fpgm (font program) ---
            var fpgm = Table("fpgm");
            if (fpgm != 0)
            {
                var fpgmLen = TableLength("fpgm");
                if (fpgmLen > 0 && Validate(fpgm, fpgmLen))
                {
                    fpgmProgram = new byte[fpgmLen];
                    System.Array.Copy(data, fpgm, fpgmProgram, 0, fpgmLen);
                }
            }

            // --- prep (CVT program) ---
            var prep = Table("prep");
            if (prep != 0)
            {
                var prepLen = TableLength("prep");
                if (prepLen > 0 && Validate(prep, prepLen))
                {
                    prepProgram = new byte[prepLen];
                    System.Array.Copy(data, prep, prepProgram, 0, prepLen);
                }
            }

            // --- cvt (control value table) ---
            var cvt = Table("cvt ");
            if (cvt != 0)
            {
                var cvtLen = TableLength("cvt ");
                if (cvtLen >= 2 && Validate(cvt, cvtLen))
                {
                    var count = cvtLen / 2;
                    cvtTable = new int[count];
                    for (uint i = 0; i < count; i++)
                    {
                        cvtTable[i] = I16(data, cvt + i * 2);
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Read raw glyph outline data for hinting: points, contour endpoints, instructions.
        /// Returns false if the glyph has no simple outline (composite or empty).
        /// </summary>
        internal bool ReadGlyphForHinting(uint glyphIdx, out GlyphPointF[] points,
            out ushort[] contourEndPoints, out byte[]? instructions,
            out int advanceWidth, out int leftSideBearing,
            out int minX, out int maxY)
        {
            points = System.Array.Empty<GlyphPointF>();
            contourEndPoints = System.Array.Empty<ushort>();
            instructions = null;
            advanceWidth = 0;
            leftSideBearing = 0;
            minX = 0;
            maxY = 0;

            if (data == null) return false;

            var glyphOffset = OutlineOffset(glyphIdx);
            if (glyphOffset == 0) return false;

            var outlineCnt = I16(data, glyphOffset);
            if (outlineCnt <= 0) return false; // composite or empty — skip hinting

            // Read metrics
            minX = I16(data, glyphOffset + 2);
            // minY = I16(data, glyphOffset + 4); // not needed
            // maxX = I16(data, glyphOffset + 6); // not needed
            maxY = I16(data, glyphOffset + 8);

            // Advance width and LSB from hmtx
            if (_hmtx == 0) _hmtx = Table("hmtx");
            if (glyphIdx < metrics.numHmtx)
            {
                var hmtxOff = _hmtx + 4 * glyphIdx;
                if (Validate(hmtxOff, 4))
                {
                    advanceWidth = U16(data, hmtxOff);
                    leftSideBearing = I16(data, hmtxOff + 2);
                }
            }
            else
            {
                // short metrics — all share last advance
                if (metrics.numHmtx > 0)
                {
                    var lastLong = _hmtx + (uint)(metrics.numHmtx - 1) * 4;
                    if (Validate(lastLong, 4))
                        advanceWidth = U16(data, lastLong);
                    var lsbOff = _hmtx + (uint)metrics.numHmtx * 4 + (glyphIdx - (uint)metrics.numHmtx) * 2;
                    if (Validate(lsbOff, 2))
                        leftSideBearing = I16(data, lsbOff);
                }
            }

            // Read contour endpoints
            var cntrsCnt = (uint)outlineCnt;
            var outline = glyphOffset + 10;
            if (!Validate(outline, cntrsCnt * 2 + 2)) return false;

            contourEndPoints = new ushort[cntrsCnt];
            for (uint i = 0; i < cntrsCnt; ++i)
            {
                contourEndPoints[i] = U16(data, outline);
                outline += 2;
            }

            var ptsCnt = (uint)(contourEndPoints[cntrsCnt - 1] + 1);

            // Read instructions
            var instructionLen = U16(data, outline);
            outline += 2;
            if (instructionLen > 0 && Validate(outline, instructionLen))
            {
                instructions = new byte[instructionLen];
                System.Array.Copy(data, outline, instructions, 0, instructionLen);
            }
            outline += instructionLen;

            // Read flags
            var flagsArr = new byte[ptsCnt];
            if (!ReadFlags(ref outline, flagsArr, ptsCnt)) return false;

            // Read points (as raw integer coordinates, no offset applied)
            var rawPts = new Point[ptsCnt];
            var zeroOffset = new Point(0, 0);
            if (!ReadPoints(outline, flagsArr, rawPts, ptsCnt, zeroOffset)) return false;

            // Convert to GlyphPointF
            const byte ON_CURVE = 0x01;
            points = new GlyphPointF[ptsCnt];
            for (uint i = 0; i < ptsCnt; i++)
            {
                points[i] = new GlyphPointF(rawPts[i].x, rawPts[i].y, (flagsArr[i] & ON_CURVE) != 0);
            }

            return true;
        }

        /// <summary>
        /// Look up a table's length from the table directory.
        /// </summary>
        private uint TableLength(string tag)
        {
            if (data == null) return 0;
            var tableCnt = U16(data, 4);
            if (!Validate(12, (uint)tableCnt * 16)) return 0;

            byte t0 = (byte)tag[0], t1 = (byte)tag[1], t2 = (byte)tag[2], t3 = (byte)tag[3];
            int lo = 0, hi = tableCnt - 1;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                uint entryOff = (uint)(12 + mid * 16);
                int cmp = Compare4(data, entryOff, t0, t1, t2, t3);
                if (cmp == 0)
                    return U32(data, entryOff + 12); // length is at offset 12 in table record
                if (cmp < 0) lo = mid + 1;
                else hi = mid - 1;
            }
            return 0;
        }
    }

    // =================================================================
    //  TtfHinter — per-font hinting context
    // =================================================================

    /// <summary>
    /// Per-font hinting context. Wraps <see cref="TtfInterpreter"/> and
    /// caches font-level data (fpgm, CVT). Thread-unsafe — create one per thread.
    /// </summary>
    internal class TtfHinter
    {
        private readonly TtfInterpreter _interp;
        private readonly int[]? _cvt;
        private readonly byte[]? _prepProgram;
        private readonly float _unitsPerEm;

        public TtfHinter(TtfReader reader)
        {
            _unitsPerEm = reader.metrics.unitsPerEm;
            _cvt = reader.cvtTable;
            _prepProgram = reader.prepProgram;

            _interp = new TtfInterpreter(
                reader.maxStackElements,
                reader.maxStorage,
                reader.maxFunctionDefs,
                reader.maxInstructionDefs,
                reader.maxTwilightPoints);

            if (reader.fpgmProgram != null)
                _interp.InitializeFunctionDefs(reader.fpgmProgram);
        }

        /// <summary>
        /// Hint a glyph's outline points. Returns the grid-fitted points
        /// (including 4 phantom points appended at the end).
        /// </summary>
        public GlyphPointF[] HintGlyph(
            int advanceWidth, int leftSideBearing,
            int minX, int maxY,
            GlyphPointF[] glyphPoints, ushort[] contourEndPoints,
            byte[]? instructions, float pixelSize)
        {
            if (instructions == null || instructions.Length == 0)
                return glyphPoints;

            float pxScale = pixelSize / _unitsPerEm;

            // Phantom points (define glyph extents, can be modified by hinting)
            var pp1 = new GlyphPointF(minX - leftSideBearing, 0, true);
            var pp2 = new GlyphPointF(pp1.X + advanceWidth, 0, true);
            var pp3 = new GlyphPointF(0, maxY, true);
            var pp4 = new GlyphPointF(0, pp3.Y, true);

            // Clone points with 4 phantom points appended
            int orgLen = glyphPoints.Length;
            var pts = new GlyphPointF[orgLen + 4];
            System.Array.Copy(glyphPoints, pts, orgLen);
            pts[orgLen] = pp1;
            pts[orgLen + 1] = pp2;
            pts[orgLen + 2] = pp3;
            pts[orgLen + 3] = pp4;

            // Scale all points to pixel space
            for (int i = pts.Length - 1; i >= 0; --i)
                pts[i].ApplyScale(pxScale);

            // Set up CVT and run prep program for this size
            _interp.SetControlValueTable(_cvt, pxScale, pixelSize, _prepProgram);

            // Run glyph hinting instructions
            _interp.HintGlyph(pts, contourEndPoints, instructions);

            return pts;
        }
    }
}
