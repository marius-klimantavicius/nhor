// Ported from ThorVG/src/loaders/ttf/tvgTtfReader.h and tvgTtfReader.cpp

using System.Runtime.CompilerServices;

namespace ThorVG
{
    /// <summary>Glyph metrics. Mirrors C++ TtfGlyph.</summary>
    public struct TtfGlyph
    {
        public uint idx;        // glyph index
        public float advance;   // advance width/height
        public float lsb;       // left side bearing
        public float x, y, w, h; // bounding box
    }

    /// <summary>Glyph metrics with outline path. Mirrors C++ TtfGlyphMetrics.</summary>
    public class TtfGlyphMetrics
    {
        // Embedded glyph fields (mirrors C++ struct inheritance from TtfGlyph)
        public uint idx;
        public float advance;
        public float lsb;
        public float x, y, w, h;

        public RenderPath path = new RenderPath();

        /// <summary>Copy glyph base fields to a TtfGlyph.</summary>
        public TtfGlyph ToGlyph()
        {
            return new TtfGlyph { idx = idx, advance = advance, lsb = lsb, x = x, y = y, w = w, h = h };
        }

        /// <summary>Copy glyph base fields from a TtfGlyph.</summary>
        public void FromGlyph(in TtfGlyph g)
        {
            idx = g.idx; advance = g.advance; lsb = g.lsb; x = g.x; y = g.y; w = g.w; h = g.h;
        }
    }

    /// <summary>
    /// Low-level TTF binary parser. Mirrors C++ TtfReader.
    /// </summary>
    public partial class TtfReader
    {
        public const uint INVALID_GLYPH = unchecked((uint)-1);

        public byte[]? data;
        public uint size;

        public struct MetricsData
        {
            public TextMetrics hhea;       // horizontal header info
            public ushort unitsPerEm;
            public ushort numHmtx;         // the number of Horizontal metrics table
            public byte locaFormat;        // 0 for short offsets, 1 for long
        }

        public MetricsData metrics;

        // Table offsets (mirrors C++ atomic<uint32_t> — atomicity not needed in C# port)
        private uint _cmap;
        private uint _hmtx;
        private uint _loca;
        private uint _glyf;
        private uint _kern;
        private uint _maxp;

        // ----------------------------------------------------------------
        //  Binary read helpers
        // ----------------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint U32(byte[] d, uint offset)
        {
            return (uint)(d[offset] << 24 | d[offset + 1] << 16 | d[offset + 2] << 8 | d[offset + 3]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort U16(byte[] d, uint offset)
        {
            return (ushort)(d[offset] << 8 | d[offset + 1]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static short I16(byte[] d, uint offset)
        {
            return (short)U16(d, offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte U8(byte[] d, uint offset)
        {
            return d[offset];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static sbyte I8(byte[] d, uint offset)
        {
            return (sbyte)d[offset];
        }

        // ----------------------------------------------------------------
        //  Validation
        // ----------------------------------------------------------------

        private bool Validate(uint offset, uint margin)
        {
            if (offset > size || size - offset < margin)
            {
                return false;
            }
            return true;
        }

        // ----------------------------------------------------------------
        //  Table lookup (binary search of TTF table directory)
        // ----------------------------------------------------------------

        private uint Table(string tag)
        {
            if (data == null) return 0;
            var tableCnt = U16(data, 4);
            if (!Validate(12, (uint)tableCnt * 16)) return 0;

            // Binary search for the 4-byte tag
            byte t0 = (byte)tag[0], t1 = (byte)tag[1], t2 = (byte)tag[2], t3 = (byte)tag[3];
            int lo = 0, hi = tableCnt - 1;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                uint entryOff = (uint)(12 + mid * 16);
                int cmp = Compare4(data, entryOff, t0, t1, t2, t3);
                if (cmp == 0)
                {
                    return U32(data, entryOff + 8);
                }
                if (cmp < 0) lo = mid + 1;
                else hi = mid - 1;
            }
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Compare4(byte[] d, uint off, byte a, byte b, byte c, byte e)
        {
            if (d[off] != a) return d[off] < a ? -1 : 1;
            if (d[off + 1] != b) return d[off + 1] < b ? -1 : 1;
            if (d[off + 2] != c) return d[off + 2] < c ? -1 : 1;
            if (d[off + 3] != e) return d[off + 3] < e ? -1 : 1;
            return 0;
        }

        // ----------------------------------------------------------------
        //  cmap subtable decoders
        // ----------------------------------------------------------------

        private uint CmapFormat12_13(uint table, uint codepoint, int fmt)
        {
            if (data == null) return INVALID_GLYPH;
            var len = U32(data, table + 4);
            if (len < 16) return INVALID_GLYPH;
            if (!Validate(table, len)) return INVALID_GLYPH;

            var entryCnt = U32(data, table + 12);
            for (uint i = 0; i < entryCnt; ++i)
            {
                var firstCode = U32(data, table + i * 12 + 16);
                var lastCode = U32(data, table + i * 12 + 16 + 4);
                if (codepoint < firstCode || codepoint > lastCode) continue;
                var glyphOffset = U32(data, table + i * 12 + 16 + 8);
                if (fmt == 12) return (codepoint - firstCode) + glyphOffset;
                else return glyphOffset;
            }
            return INVALID_GLYPH;
        }

        private uint CmapFormat4(uint table, uint codepoint)
        {
            if (data == null) return INVALID_GLYPH;
            // cmap format 4 only supports the Unicode BMP.
            if (codepoint > 0xffff) return INVALID_GLYPH;

            if (!Validate(table, 8)) return INVALID_GLYPH;

            var segmentCnt = U16(data, table);
            if ((segmentCnt & 1) != 0 || segmentCnt == 0) return INVALID_GLYPH;

            // find starting positions of the relevant arrays.
            var endCodes = table + 8;
            var startCodes = endCodes + segmentCnt + 2;
            var idDeltas = startCodes + segmentCnt;
            var idRangeOffsets = idDeltas + segmentCnt;

            if (!Validate(idRangeOffsets, segmentCnt)) return INVALID_GLYPH;

            // binary search for the segment containing codepoint
            var n = segmentCnt / 2;
            if (n == 0) return 0;

            byte keyHi = (byte)(codepoint >> 8), keyLo = (byte)codepoint;
            int low = 0, high = (int)n - 1;
            while (low != high)
            {
                int mid = low + (high - low) / 2;
                uint sampleOff = endCodes + (uint)mid * 2;
                // compare big-endian 16-bit
                int cmp;
                if (keyHi != data[sampleOff]) cmp = keyHi.CompareTo(data[sampleOff]);
                else cmp = keyLo.CompareTo(data[sampleOff + 1]);
                if (cmp > 0) low = mid + 1;
                else high = mid;
            }

            uint segmentIdx = (uint)low * 2;

            var startCode = U16(data, startCodes + segmentIdx);
            if (startCode > codepoint) return 0;

            var delta = U16(data, idDeltas + segmentIdx);
            var idRangeOffset = U16(data, idRangeOffsets + segmentIdx);
            // intentional integer under- and overflow.
            if (idRangeOffset == 0) return (uint)((codepoint + delta) & 0xffff);

            var offset = idRangeOffsets + segmentIdx + idRangeOffset + 2U * (uint)(codepoint - startCode);
            if (!Validate(offset, 2)) return INVALID_GLYPH;

            var id = U16(data, offset);
            if (id > 0) return (uint)((id + delta) & 0xffff);
            return 0;
        }

        private uint CmapFormat6(uint table, uint codepoint)
        {
            if (data == null) return INVALID_GLYPH;
            if (codepoint > 0xFFFF) return 0;

            var firstCode = U16(data, table);
            var entryCnt = U16(data, table + 2);
            if (!Validate(table, 4 + 2u * entryCnt)) return INVALID_GLYPH;

            if (codepoint < firstCode) return INVALID_GLYPH;
            codepoint -= firstCode;
            if (codepoint >= entryCnt) return INVALID_GLYPH;
            return U16(data, table + 4 + 2 * codepoint);
        }

        // ----------------------------------------------------------------
        //  Outline offset lookup
        // ----------------------------------------------------------------

        private uint OutlineOffset(uint glyphIdx)
        {
            if (data == null) return 0;

            if (_loca == 0) _loca = Table("loca");
            if (_glyf == 0) _glyf = Table("glyf");

            uint cur, next;
            if (metrics.locaFormat == 0)
            {
                var baseOff = _loca + 2 * glyphIdx;
                if (!Validate(baseOff, 4)) return 0;
                cur = 2U * U16(data, baseOff);
                next = 2U * U16(data, baseOff + 2);
            }
            else
            {
                var baseOff = _loca + 4 * glyphIdx;
                if (!Validate(baseOff, 8)) return 0;
                cur = U32(data, baseOff);
                next = U32(data, baseOff + 4);
            }
            if (cur == next) return 0;
            return _glyf + cur;
        }

        // ----------------------------------------------------------------
        //  Points & flags decoders
        // ----------------------------------------------------------------

        private bool ReadPoints(uint outline, byte[] flags, Point[] pts, uint ptsCnt, in Point offset)
        {
            if (data == null) return false;

            const byte X_CHANGE_IS_SMALL = 0x02;
            const byte X_CHANGE_IS_POSITIVE = 0x10;
            const byte X_CHANGE_IS_ZERO = 0x10;
            const byte Y_CHANGE_IS_SMALL = 0x04;
            const byte Y_CHANGE_IS_ZERO = 0x20;
            const byte Y_CHANGE_IS_POSITIVE = 0x20;

            long accum = 0L;
            for (uint i = 0; i < ptsCnt; ++i)
            {
                if ((flags[i] & X_CHANGE_IS_SMALL) != 0)
                {
                    if (!Validate(outline, 1)) return false;
                    var value = (long)U8(data, outline++);
                    var bit = (byte)(((flags[i] & X_CHANGE_IS_POSITIVE) != 0) ? 1 : 0);
                    accum -= (value ^ -bit) + bit;
                }
                else if ((flags[i] & X_CHANGE_IS_ZERO) == 0)
                {
                    if (!Validate(outline, 2)) return false;
                    accum += I16(data, outline);
                    outline += 2;
                }
                pts[i].x = offset.x + (float)accum;
            }

            accum = 0L;
            for (uint i = 0; i < ptsCnt; ++i)
            {
                if ((flags[i] & Y_CHANGE_IS_SMALL) != 0)
                {
                    if (!Validate(outline, 1)) return false;
                    var value = (long)U8(data, outline++);
                    var bit = (byte)(((flags[i] & Y_CHANGE_IS_POSITIVE) != 0) ? 1 : 0);
                    accum -= (value ^ -bit) + bit;
                }
                else if ((flags[i] & Y_CHANGE_IS_ZERO) == 0)
                {
                    if (!Validate(outline, 2)) return false;
                    accum += I16(data, outline);
                    outline += 2;
                }
                pts[i].y = offset.y - (float)accum;
            }
            return true;
        }

        private bool ReadFlags(ref uint outline, byte[] flags, uint flagsCnt)
        {
            if (data == null) return false;

            const byte REPEAT_FLAG = 0x08;
            var offset = outline;
            byte value = 0;
            byte repeat = 0;

            for (uint i = 0; i < flagsCnt; ++i)
            {
                if (repeat > 0)
                {
                    --repeat;
                }
                else
                {
                    if (!Validate(offset, 1)) return false;
                    value = U8(data, offset++);
                    if ((value & REPEAT_FLAG) != 0)
                    {
                        if (!Validate(offset, 1)) return false;
                        repeat = U8(data, offset++);
                    }
                }
                flags[i] = value;
            }
            outline = offset;
            return true;
        }

        // ----------------------------------------------------------------
        //  Public API
        // ----------------------------------------------------------------

        /// <summary>Parse the TTF header tables. Returns true on success.</summary>
        public bool Header()
        {
            if (data == null || !Validate(0, 12)) return false;

            // verify ttf (scalable font)
            var type = U32(data, 0);
            if (type != 0x00010000 && type != 0x74727565) return false;

            // header
            var head = Table("head");
            if (!Validate(head, 54)) return false;

            metrics.unitsPerEm = U16(data, head + 18);
            metrics.locaFormat = (byte)U16(data, head + 50);

            // horizontal metrics
            var hhea = Table("hhea");
            if (!Validate(hhea, 36)) return false;

            metrics.hhea.ascent = I16(data, hhea + 4);
            metrics.hhea.descent = I16(data, hhea + 6);
            metrics.hhea.linegap = I16(data, hhea + 8);
            metrics.hhea.advance = metrics.hhea.ascent - metrics.hhea.descent + metrics.hhea.linegap;
            metrics.numHmtx = U16(data, hhea + 34);

            // kerning
            _kern = Table("kern");
            if (_kern != 0)
            {
                if (!Validate(_kern, 4)) return false;
                if (U16(data, _kern) != 0) return false;
            }

            return true;
        }

        /// <summary>
        /// Look up a glyph index for a Unicode codepoint using the cmap table.
        /// Returns INVALID_GLYPH on failure.
        /// </summary>
        public uint Glyph(uint codepoint)
        {
            if (data == null) return INVALID_GLYPH;

            if (_cmap == 0)
            {
                _cmap = Table("cmap");
                if (!Validate(_cmap, 4)) return INVALID_GLYPH;
            }

            var entryCnt = U16(data, _cmap + 2);
            if (!Validate(_cmap, 4 + (uint)entryCnt * 8)) return INVALID_GLYPH;

            // full repertory (non-BMP map).
            for (int idx = 0; idx < entryCnt; ++idx)
            {
                var entry = _cmap + 4 + (uint)idx * 8;
                var entryType = U16(data, entry) * 0100 + U16(data, entry + 2);
                // unicode map
                if (entryType == 0004 || entryType == 0312)
                {
                    var tbl = _cmap + U32(data, entry + 4);
                    if (!Validate(tbl, 8)) return INVALID_GLYPH;
                    var format = U16(data, tbl);
                    switch (format)
                    {
                        case 12: return CmapFormat12_13(tbl, codepoint, 12);
                        default: return INVALID_GLYPH;
                    }
                }
            }

            // Try looking for a BMP map.
            for (int idx = 0; idx < entryCnt; ++idx)
            {
                var entry = _cmap + 4 + (uint)idx * 8;
                var entryType = U16(data, entry) * 0100 + U16(data, entry + 2);
                // Unicode BMP
                if (entryType == 0003 || entryType == 0301)
                {
                    var tbl = _cmap + U32(data, entry + 4);
                    if (!Validate(tbl, 6)) return INVALID_GLYPH;
                    switch (U16(data, tbl))
                    {
                        case 4: return CmapFormat4(tbl + 6, codepoint);
                        case 6: return CmapFormat6(tbl + 6, codepoint);
                        default: return INVALID_GLYPH;
                    }
                }
            }
            return INVALID_GLYPH;
        }

        /// <summary>
        /// Look up glyph for a codepoint and populate metrics + index.
        /// Returns outline offset (0 if glyph has no outline).
        /// </summary>
        public uint Glyph(uint codepoint, TtfGlyphMetrics tgm)
        {
            tgm.idx = Glyph(codepoint);
            if (tgm.idx == INVALID_GLYPH) return 0;
            return GlyphMetrics(tgm);
        }

        /// <summary>
        /// Read glyph horizontal metrics and bounding box.
        /// Returns outline offset (0 if glyph has no outline or an error).
        /// </summary>
        public uint GlyphMetrics(TtfGlyphMetrics glyph)
        {
            if (data == null) return 0;

            if (_hmtx == 0) _hmtx = Table("hmtx");

            // glyph is inside long metrics segment.
            if (glyph.idx < metrics.numHmtx)
            {
                var offset = _hmtx + 4 * glyph.idx;
                if (!Validate(offset, 4)) return 0;
                glyph.advance = U16(data, offset);
                glyph.lsb = I16(data, offset + 2);
            }
            else
            {
                // glyph is inside short metrics segment.
                var boundary = _hmtx + 4U * metrics.numHmtx;
                if (boundary < 4) return 0;

                var offset = boundary - 4;
                if (!Validate(offset, 4)) return 0;
                glyph.advance = U16(data, offset);
                offset = boundary + 2 * (glyph.idx - metrics.numHmtx);
                if (!Validate(offset, 2)) return 0;
                glyph.lsb = I16(data, offset);
            }

            var glyphOffset = OutlineOffset(glyph.idx);
            // glyph without outline
            if (glyphOffset == 0)
            {
                glyph.x = glyph.y = glyph.w = glyph.h = 0.0f;
                return 0;
            }
            if (!Validate(glyphOffset, 10)) return 0;

            // read the bounding box from the font file verbatim.
            float bbox0 = I16(data, glyphOffset + 2);
            float bbox1 = I16(data, glyphOffset + 4);
            float bbox2 = I16(data, glyphOffset + 6);
            float bbox3 = I16(data, glyphOffset + 8);

            glyph.x = bbox0;
            glyph.y = bbox1;
            glyph.w = bbox2 - bbox0 + 1;
            glyph.h = bbox3 - bbox1 + 1;

            return glyphOffset;
        }

        /// <summary>
        /// Convert a glyph outline to path commands.
        /// </summary>
        public bool Convert(RenderPath path, TtfGlyphMetrics glyph, uint glyphOffset, in Point offset, ushort depth)
        {
            const byte ON_CURVE = 0x01;

            if (data == null) return false;
            if (glyphOffset == 0) return true;

            var outlineCnt = I16(data, glyphOffset);
            if (outlineCnt == 0) return false;
            if (outlineCnt < 0)
            {
                ushort maxComponentDepth = 1;
                if (_maxp == 0) _maxp = Table("maxp");
                if (Validate(_maxp, 32) && U32(data, _maxp) >= 0x00010000U)
                {
                    maxComponentDepth = U16(data, _maxp + 30);
                }
                if (depth > maxComponentDepth) return false;
                return ConvertComposite(path, glyph, glyphOffset, offset, (ushort)(depth + 1));
            }

            var cntrsCnt = (uint)outlineCnt;
            var outline = glyphOffset + 10;
            if (!Validate(outline, cntrsCnt * 2 + 2)) return false;

            var ptsCnt = (uint)(U16(data, outline + (cntrsCnt - 1) * 2) + 1);
            var endPts = new uint[cntrsCnt];

            for (uint i = 0; i < cntrsCnt; ++i)
            {
                endPts[i] = U16(data, outline);
                outline += 2;
            }
            outline += 2U + U16(data, outline);

            var flagsArr = new byte[ptsCnt];
            if (!ReadFlags(ref outline, flagsArr, ptsCnt)) return false;

            var pts = new Point[ptsCnt];
            if (!ReadPoints(outline, flagsArr, pts, ptsCnt, offset)) return false;

            // generate tvg paths
            path.cmds.Reserve(path.cmds.count + ptsCnt);
            path.pts.Reserve(path.pts.count + ptsCnt);

            uint begin = 0;
            for (uint i = 0; i < cntrsCnt; ++i)
            {
                // contour must start with move to
                var offCurve = (flagsArr[begin] & ON_CURVE) == 0;
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
                    if ((flagsArr[begin + x] & ON_CURVE) != 0)
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
                // contour must end with close
                path.Close();
                begin = endPts[i] + 1;
            }
            return true;
        }

        /// <summary>
        /// Convert a composite glyph to path commands.
        /// </summary>
        private bool ConvertComposite(RenderPath path, TtfGlyphMetrics glyph, uint glyphOffset, in Point offset, ushort depth)
        {
            if (data == null) return false;

            const ushort ARG_1_AND_2_ARE_WORDS = 0x0001;
            const ushort ARGS_ARE_XY_VALUES = 0x0002;
            const ushort WE_HAVE_A_SCALE = 0x0008;
            const ushort MORE_COMPONENTS = 0x0020;
            const ushort WE_HAVE_AN_X_AND_Y_SCALE = 0x0040;
            const ushort WE_HAVE_A_TWO_BY_TWO = 0x0080;

            var compGlyph = new TtfGlyphMetrics();
            Point compOffset;
            ushort flags;
            var pointer = glyphOffset + 10;

            do
            {
                if (!Validate(pointer, 4)) return false;
                flags = U16(data, pointer);
                compGlyph.idx = U16(data, pointer + 2);
                if (compGlyph.idx == INVALID_GLYPH) continue;
                pointer += 4;

                if ((flags & ARG_1_AND_2_ARE_WORDS) != 0)
                {
                    if (!Validate(pointer, 4)) return false;
                    compOffset = ((flags & ARGS_ARE_XY_VALUES) != 0)
                        ? new Point(I16(data, pointer), -I16(data, pointer + 2))
                        : new Point(0.0f, 0.0f);
                    pointer += 4;
                }
                else
                {
                    if (!Validate(pointer, 2)) return false;
                    compOffset = ((flags & ARGS_ARE_XY_VALUES) != 0)
                        ? new Point(I8(data, pointer), -I8(data, pointer + 1))
                        : new Point(0.0f, 0.0f);
                    pointer += 2;
                }

                if ((flags & WE_HAVE_A_SCALE) != 0)
                {
                    if (!Validate(pointer, 2)) return false;
                    pointer += 2;
                }
                else if ((flags & WE_HAVE_AN_X_AND_Y_SCALE) != 0)
                {
                    if (!Validate(pointer, 4)) return false;
                    pointer += 4;
                }
                else if ((flags & WE_HAVE_A_TWO_BY_TWO) != 0)
                {
                    if (!Validate(pointer, 8)) return false;
                    pointer += 8;
                }

                var combinedOffset = new Point(offset.x + compOffset.x, offset.y + compOffset.y);
                var outOff = GlyphMetrics(compGlyph);
                if (!Convert(path, compGlyph, outOff, combinedOffset, depth)) return false;
            } while ((flags & MORE_COMPONENTS) != 0);

            return true;
        }

        /// <summary>
        /// Look up kerning between two glyphs.
        /// </summary>
        public bool Kerning(uint lglyph, uint rglyph, ref Point output)
        {
            if (data == null || _kern == 0) return false;

            const byte HORIZONTAL_KERNING = 0x01;
            const byte MINIMUM_KERNING = 0x02;
            const byte CROSS_STREAM_KERNING = 0x04;

            var kern = _kern;
            var tableCnt = U16(data, kern + 2);
            kern += 4;

            while (tableCnt > 0)
            {
                if (!Validate(kern, 6)) return false;
                var length = U16(data, kern + 2);
                var format = U8(data, kern + 4);
                var kernFlags = U8(data, kern + 5);
                kern += 6;

                if (format == 0 && (kernFlags & HORIZONTAL_KERNING) != 0 && (kernFlags & MINIMUM_KERNING) == 0)
                {
                    if (!Validate(kern, 8)) return false;
                    var pairCnt = U16(data, kern);
                    kern += 8;

                    // Binary search for the kerning pair
                    var key0 = (byte)((lglyph >> 8) & 0xff);
                    var key1 = (byte)(lglyph & 0xff);
                    var key2 = (byte)((rglyph >> 8) & 0xff);
                    var key3 = (byte)(rglyph & 0xff);

                    int lo = 0, hi = pairCnt - 1;
                    while (lo <= hi)
                    {
                        int mid = lo + (hi - lo) / 2;
                        uint entryOff = kern + (uint)mid * 6;
                        int cmp = Compare4(data, entryOff, key0, key1, key2, key3);
                        if (cmp == 0)
                        {
                            var value = I16(data, entryOff + 4);
                            if ((kernFlags & CROSS_STREAM_KERNING) != 0) output.y += value;
                            else output.x += value;
                            break;
                        }
                        if (cmp < 0) lo = mid + 1;
                        else hi = mid - 1;
                    }
                }
                kern += length;
                --tableCnt;
            }
            return true;
        }
    }
}
