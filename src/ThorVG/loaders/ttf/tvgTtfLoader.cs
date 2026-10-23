// Ported from ThorVG/src/loaders/ttf/tvgTtfLoader.h and tvgTtfLoader.cpp

using System;
using System.Collections.Generic;
using System.IO;

namespace ThorVG
{
    /// <summary>Extended font metrics for TTF. Mirrors C++ TtfMetrics.</summary>
    public class TtfMetrics
    {
        public float baseWidth;  // Use as the reference glyph width for italic transform
        internal TtfGlyphMetrics? baseGlyph;
    }

    /// <summary>
    /// TTF font loader. Mirrors C++ TtfLoader.
    /// </summary>
    public partial class TtfLoader : FontLoader
    {
        private const uint LINE_FEED_CODEPOINT = 0x0A;
        private const uint DOT_CODEPOINT = 0x2E;

        // Match the pinned word-wrap algorithm's SPACE_GLYPH_IDX boundary.
        private static bool IsWordBreak(uint code) => code == 0x20;

        public TtfReader reader = new TtfReader();
        public Dictionary<uint, TtfGlyphMetrics> glyphs = new Dictionary<uint, TtfGlyphMetrics>();

        public TtfLoader() : base(FileType.Sfnt)
        {
        }

        // ----------------------------------------------------------------
        //  UTF-8 codepoint decoding
        // ----------------------------------------------------------------

        private static uint DecodeCodepoint(ReadOnlySpan<byte> text, ref int idx, int end)
        {
            if (idx >= end) return 0;
            int p = idx;
            uint c = text[p];
            if ((c & 0x80) == 0) { ++idx; return c; }
            if ((c & 0xe0) == 0xc0 && p + 1 < end)
            { idx += 2; return ((c & 0x1f) << 6) + (uint)(text[p + 1] & 0x3f); }
            if ((c & 0xf0) == 0xe0 && p + 2 < end)
            { idx += 3; return ((c & 0x0f) << 12) + (uint)((text[p + 1] & 0x3f) << 6) + (uint)(text[p + 2] & 0x3f); }
            if ((c & 0xf8) == 0xf0 && p + 3 < end)
            { idx += 4; return ((c & 7) << 18) + (uint)((text[p + 1] & 0x3f) << 12) + (uint)((text[p + 2] & 0x3f) << 6) + (uint)(text[p + 3] & 0x3f); }
            TvgCommon.TVGERR("SFNT", "Corrupted UTF8??");
            ++idx; return 0;
        }

        // ----------------------------------------------------------------
        //  Build / Align helpers (mirrors C++ static functions)
        // ----------------------------------------------------------------

        private static unsafe void Build(RenderPath input, in Point cursor, in Point kerning, RenderPath output)
        {
            output.cmds.Push(input.cmds);
            output.pts.Grow(input.pts.count);
            var p = input.pts.Begin();
            var end = input.pts.End();
            while (p < end)
            {
                var pt = new Point((p->x + cursor.x) + kerning.x, (p->y + cursor.y) + kerning.y);
                output.pts.Push(pt);
                p++;
            }
        }

        private static unsafe void Align(in Point align, in Point box, in Point cursor, uint begin, uint end, RenderPath output)
        {
            if (align.x > 0.0f || align.y > 0.0f)
            {
                var shiftX = (box.x - cursor.x) * align.x;
                var shiftY = (box.y - cursor.y) * align.y;
                for (var p = output.pts.data + begin; p < output.pts.data + end; p++)
                {
                    p->x += shiftX;
                    p->y += shiftY;
                }
            }
        }

        private static unsafe void AlignX(float align, float box, float x, uint begin, uint end, RenderPath output)
        {
            if (align > 0.0f)
            {
                var shift = (box - x) * align;
                for (var p = output.pts.data + begin; p < output.pts.data + end; p++)
                {
                    p->x += shift;
                }
            }
        }

        private static unsafe void AlignY(float align, float box, float y, uint begin, uint end, RenderPath output)
        {
            if (align > 0.0f)
            {
                var shift = (box - y) * align;
                for (var p = output.pts.data + begin; p < output.pts.data + end; p++)
                {
                    p->y += shift;
                }
            }
        }

        // ----------------------------------------------------------------
        //  Private helpers
        // ----------------------------------------------------------------

        private static void SetBaseGlyph(FontMetrics fm, TtfGlyphMetrics glyph)
        {
            var metrics = (TtfMetrics)fm.engine!;
            metrics.baseGlyph = glyph;
            var bounds = glyph.NativeBounds;
            metrics.baseWidth = bounds.max.x - bounds.min.x;
        }

        private float Height(uint loc, float spacing)
        {
            return TvgMath.MultiplyAdd(reader.metrics.hhea.advance, loc, -reader.metrics.hhea.linegap) * spacing;
        }

        private uint FeedLine(FontMetrics fm, float box, float x, uint begin, uint end, ref Point cursor, RenderPath output)
        {
            AlignX(fm.align.x, box, x, begin, end, output);
            cursor.x = 0.0f;
            cursor.y = TvgMath.MultiplyAdd(reader.metrics.hhea.advance, fm.spacing.y, cursor.y);
            ++fm.lines;
            return output.pts.count;
        }

        private float _hintPixelSize; // [Hinting] Set by Get() for the current render pass
        private readonly StrictKey glyphKey = new();

        private TtfGlyphMetrics? Request(uint code)
        {
            using var guard = new ScopedLock(glyphKey);
            if (code == 0) return null;
            // [Hinting] Route through hinted path when pixel size is set
            if (_hintPixelSize > 0 && reader is not OtfReader)
                return RequestHinted(code, _hintPixelSize);

            return RequestUnhinted(code);
        }

        private TtfGlyphMetrics? RequestUnhinted(uint code)
        {
            if (code == 0) return null;

            if (glyphs.TryGetValue(code, out var existing))
            {
                return existing;
            }

            var tgm = new TtfGlyphMetrics();
            // Native Map::operator[] owns the record before conversion,
            // including any partially constructed path on failure.
            glyphs[code] = tgm;
            var glyphOffset = reader.Glyph(code, tgm);
            if (tgm.idx == TtfReader.INVALID_GLYPH) return null;
            if (!reader.Convert(tgm.path, tgm, glyphOffset, new Point(0.0f, 0.0f), 1))
            {
                return null;
            }
            return tgm;
        }

        // ----------------------------------------------------------------
        //  Wrapping modes
        // ----------------------------------------------------------------

        private void WrapNone(FontMetrics fm, in Point box, ReadOnlySpan<byte> utf8, int len, RenderPath output)
        {
            TtfGlyphMetrics? ltgm = null;
            var cursor = new Point(0, 0);
            uint line = 0;
            int idx = 0;

            while (idx < len)
            {
                var code = DecodeCodepoint(utf8, ref idx, len);
                if (code == LINE_FEED_CODEPOINT)
                {
                    line = FeedLine(fm, box.x, cursor.x, line, output.pts.count, ref cursor, output);
                    continue;
                }
                var rtgm = Request(code);
                if (rtgm == null) continue;

                var kerning = new Point(0, 0);
                if (ltgm != null) reader.Kerning(ltgm.idx, rtgm.idx, ref kerning);

                Build(rtgm.path, cursor, kerning, output);
                cursor.x = TvgMath.MultiplyAdd(rtgm.advance + kerning.x, fm.spacing.x, cursor.x);

                if (cursor.x > fm.size.x) fm.size.x = cursor.x;

                // store the base glyph width for italic transform
                if (ltgm == null) SetBaseGlyph(fm, rtgm);
                ltgm = rtgm;
            }
            fm.size.y = Height(fm.lines, fm.spacing.y);
            AlignY(fm.align.y, box.y, fm.size.y, 0, line, output);
            Align(fm.align, box, new Point(cursor.x, fm.size.y), line, output.pts.count, output);
        }

        private void WrapChar(FontMetrics fm, in Point box, ReadOnlySpan<byte> utf8, int len, RenderPath output)
        {
            TtfGlyphMetrics? ltgm = null;
            uint line = 0;
            var cursor = new Point(0, 0);
            int idx = 0;

            while (idx < len)
            {
                var code = DecodeCodepoint(utf8, ref idx, len);
                if (code == LINE_FEED_CODEPOINT)
                {
                    line = FeedLine(fm, box.x, cursor.x, line, output.pts.count, ref cursor, output);
                    continue;
                }
                var rtgm = Request(code);
                if (rtgm == null) continue;

                var kerning = new Point(0, 0);
                if (ltgm != null) reader.Kerning(ltgm.idx, rtgm.idx, ref kerning);

                var xadv = (rtgm.advance + kerning.x) * fm.spacing.x;

                // normal scenario
                if (xadv < box.x)
                {
                    if (cursor.x + xadv > box.x)
                    {
                        line = FeedLine(fm, box.x, cursor.x, line, output.pts.count, ref cursor, output);
                    }
                    Build(rtgm.path, cursor, kerning, output);
                    cursor.x += xadv;
                }
                // not enough layout space, force pushing
                else
                {
                    Build(rtgm.path, cursor, kerning, output);
                    line = FeedLine(fm, box.x, cursor.x, line, output.pts.count, ref cursor, output);
                }

                if (cursor.x > fm.size.x) fm.size.x = cursor.x;

                if (ltgm == null) SetBaseGlyph(fm, rtgm);
                ltgm = rtgm;
            }
            fm.size.y = Height(fm.lines, fm.spacing.y);
            AlignY(fm.align.y, box.y, fm.size.y, 0, line, output);
            Align(fm.align, box, new Point(cursor.x, fm.size.y), line, output.pts.count, output);
        }

        private unsafe void WrapWord(FontMetrics fm, in Point box, ReadOnlySpan<byte> utf8, int len, RenderPath output, bool smart)
        {
            TtfGlyphMetrics? ltgm = null;
            uint line = 0;
            uint word = 0;
            float wadv = 0.0f;
            var hadv = reader.metrics.hhea.advance * fm.spacing.y;
            var cursor = new Point(0, 0);
            int idx = 0;

            while (idx < len)
            {
                var code = DecodeCodepoint(utf8, ref idx, len);
                if (code == LINE_FEED_CODEPOINT)
                {
                    line = FeedLine(fm, box.x, cursor.x, line, output.pts.count, ref cursor, output);
                    continue;
                }
                var rtgm = Request(code);
                if (rtgm == null) continue;

                var kerning = new Point(0, 0);
                if (ltgm != null) reader.Kerning(ltgm.idx, rtgm.idx, ref kerning);

                var xadv = (rtgm.advance + kerning.x) * fm.spacing.x;

                // try line-wrap
                if (cursor.x + xadv > box.x)
                {
                    // wrapping only if enough space for the current word
                    if ((cursor.x - wadv) + xadv < box.x)
                    {
                        AlignX(fm.align.x, box.x, wadv, line, word, output);
                        // shift the wrapping word to the next line
                        for (var p = output.pts.data + word; p < output.pts.End(); p++)
                        {
                            p->x -= wadv;
                            p->y += hadv;
                        }
                        cursor.x -= wadv;
                        cursor.y += hadv;
                        line = word;
                        wadv = 0;
                        ++fm.lines;
                    }
                    // not enough space, line wrap by character
                    else if (smart)
                    {
                        line = FeedLine(fm, box.x, cursor.x, line, output.pts.count, ref cursor, output);
                    }
                }
                Build(rtgm.path, cursor, kerning, output);
                cursor.x += xadv;

                // capture the word start
                if (IsWordBreak(code))
                {
                    word = output.pts.count;
                    wadv = cursor.x;
                }

                if (cursor.x > fm.size.x) fm.size.x = cursor.x;

                if (ltgm == null) SetBaseGlyph(fm, rtgm);
                ltgm = rtgm;
            }
            fm.size.y = Height(fm.lines, fm.spacing.y);
            AlignY(fm.align.y, box.y, fm.size.y, 0, line, output);
            Align(fm.align, box, new Point(cursor.x, fm.size.y), line, output.pts.count, output);
        }

        private void WrapEllipsis(FontMetrics fm, in Point box, ReadOnlySpan<byte> utf8, int len, RenderPath output)
        {
            TtfGlyphMetrics? ltgm = null;
            uint line = 0;
            var cursor = new Point(0, 0);
            uint capturePts = 0;
            uint captureCmds = 0;
            float captureXadv = 0.0f;
            bool stop = false;
            int idx = 0;

            while (idx < len)
            {
                var code = DecodeCodepoint(utf8, ref idx, len);
                if (code == LINE_FEED_CODEPOINT)
                {
                    line = FeedLine(fm, box.x, cursor.x, line, output.pts.count, ref cursor, output);
                    continue;
                }
                var rtgm = Request(code);
                if (rtgm == null) continue;

                var kerning = new Point(0, 0);
                if (ltgm != null) reader.Kerning(ltgm.idx, rtgm.idx, ref kerning);

                var xadv = (rtgm.advance + kerning.x) * fm.spacing.x;

                // normal case
                if (cursor.x + xadv < box.x)
                {
                    capturePts = output.pts.count;
                    captureCmds = output.cmds.count;
                    captureXadv = xadv;
                    Build(rtgm.path, cursor, kerning, output);
                    cursor.x += xadv;
                }
                // ellipsis
                else
                {
                    rtgm = Request(DOT_CODEPOINT);
                    if (rtgm == null) return;

                    kerning = new Point(0, 0);
                    reader.Kerning(rtgm.idx, rtgm.idx, ref kerning);
                    // not enough space, revert one character back
                    if (TvgMath.MultiplyAdd(rtgm.advance + kerning.x, 3, cursor.x) > box.x)
                    {
                        output.pts.count = capturePts;
                        output.cmds.count = captureCmds;
                        cursor.x -= captureXadv;
                    }
                    // append ...
                    var tmp = (rtgm.advance + kerning.x) * fm.spacing.x;
                    for (int i = 0; i < 3; ++i)
                    {
                        Build(rtgm.path, cursor, kerning, output);
                        cursor.x += tmp;
                    }
                    stop = true;
                }

                if (cursor.x > fm.size.x) fm.size.x = cursor.x;

                if (ltgm == null) SetBaseGlyph(fm, rtgm);

                if (stop) break;

                ltgm = rtgm;
            }
            fm.size.y = Height(fm.lines, fm.spacing.y);
            AlignY(fm.align.y, box.y, fm.size.y, 0, line, output);
            Align(fm.align, box, new Point(cursor.x, fm.size.y), line, output.pts.count, output);
        }

        // ----------------------------------------------------------------
        //  Public API overrides
        // ----------------------------------------------------------------

        public override bool Open(string path, LoaderOps? ops = null)
            => OpenResult(path, ops) == Result.Success;

        public override Result OpenResult(string path, LoaderOps? ops = null)
        {
            try
            {
                var bytes = TvgUtf8.ReadAllBytes(path);
                var result = OpenFont(bytes, (uint)bytes.Length, Ownership.Transfer);
                if (result == Result.Success) name = TvgStr.Filename(path);
                return result;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                return Result.InvalidArguments;
            }
        }

        /// <summary>Open from raw data buffer (with resource path).</summary>
        public override bool Open(byte[] data, uint size, LoaderOps? ops, bool copy)
        {
            if (data == null || size > data.Length) return false;
            return OpenFont(data, size, copy ? Ownership.Copy : ops?.owner ?? Ownership.Borrow) == Result.Success;
        }

        public override Result OpenResult(byte[] data, uint size, LoaderOps? ops)
            => OpenFont(data, size, ops?.owner ?? Ownership.Borrow);

        /// <summary>Open from raw data buffer.</summary>
        public bool Open(byte[] rawData, uint dataSize, bool copy)
            => OpenFont(rawData, dataSize, copy ? Ownership.Copy : Ownership.Borrow) == Result.Success;

        private Result OpenFont(byte[] rawData, uint dataSize, Ownership owner)
        {
            if (rawData == null || dataSize > rawData.Length || dataSize <= 4) return Result.InvalidArguments;
            uint signature = (uint)(rawData[0] << 24 | rawData[1] << 16 | rawData[2] << 8 | rawData[3]);
            TtfReader selected;
            if (signature == 0x00010000 || signature == 0x74727565) selected = new TtfReader();
            else if (signature == 0x4f54544f) selected = new OtfReader();
            else return Result.InvalidArguments;
            if (owner == Ownership.Copy)
            {
                selected.data = new byte[dataSize];
                Buffer.BlockCopy(rawData, 0, selected.data, 0, (int)dataSize);
            }
            else
            {
                selected.data = rawData;
            }
            selected.size = dataSize;
            var result = selected.HeaderResult();
            if (result != Result.Success) return result;
            ClearGlyphs();
            reader = selected;
            this.owner = owner;
            _hinter = null;
            _hintingInitialized = false;
            return Result.Success;
        }

        private void ClearGlyphs()
        {
            foreach (var glyph in glyphs.Values) glyph.Dispose();
            foreach (var glyph in _hintedGlyphs.Values) glyph.Dispose();
            glyphs.Clear();
            _hintedGlyphs.Clear();
        }

        internal override void ReleaseResources()
        {
            ClearGlyphs();
            reader.data = null;
            reader.size = 0;
            _hinter = null;
            name = null;
            base.ReleaseResources();
        }

        public override void Transform(Paint paint, FontMetrics fm, float italicShear)
        {
            var engine = (TtfMetrics)fm.engine!;
            var bounds = engine.baseGlyph?.NativeBounds;
            var baseWidth = bounds.HasValue ? bounds.Value.max.x - bounds.Value.min.x : engine.baseWidth;
            var scale = 1.0f / fm.scale;
            var m = new Matrix(
                scale, -italicShear * scale, italicShear * baseWidth * scale,
                0, scale, reader.metrics.hhea.ascent * scale,
                0, 0, 1);
            paint.Transform(m);
        }

        public override bool Get(FontMetrics fm, string? text, uint len, RenderPath output)
        {
            using var guard = new ScopedLock(glyphKey);
            _hintPixelSize = fm.hinting ? fm.fontSize * DPI : 0;
            try { return GetOutline(fm, text, len, output); }
            finally { _hintPixelSize = 0; }
        }

        private bool GetOutline(FontMetrics fm, string? text, uint len, RenderPath output)
        {
            output.Clear();
            fm.lines = 1;
            if (text == null || fm.fontSize == 0.0f) return false;

            fm.scale = reader.metrics.unitsPerEm / (fm.fontSize * DPI);
            fm.size = new Point(0, 0);
            fm.lines = 1;
            if (fm.engine == null) fm.engine = new TtfMetrics();

            var box = new Point(fm.box.x * fm.scale, fm.box.y * fm.scale);
            var bytes = TvgUtf8.Encode(text);
            var end = (int)Math.Min(len, bytes.Length);

            if (fm.wrap == TextWrap.None || fm.box.x == 0.0f) WrapNone(fm, box, bytes, end, output);
            else if (fm.wrap == TextWrap.Character) WrapChar(fm, box, bytes, end, output);
            else if (fm.wrap == TextWrap.Word) WrapWord(fm, box, bytes, end, output, false);
            else if (fm.wrap == TextWrap.Smart) WrapWord(fm, box, bytes, end, output, true);
            else if (fm.wrap == TextWrap.Ellipsis) WrapEllipsis(fm, box, bytes, end, output);
            else return false;

            return true;
        }

        public override void Release(FontMetrics fm)
        {
            fm.engine = null;
        }

        public override void Copy(FontMetrics input, FontMetrics output)
        {
            Release(output);
            output.size = input.size;
            output.scale = input.scale;
            output.align = input.align;
            output.box = input.box;
            output.spacing = input.spacing;
            output.fontSize = input.fontSize;
            output.lines = input.lines;
            output.wrap = input.wrap;
            output.hinting = input.hinting;
            if (input.engine != null)
            {
                var engine = (TtfMetrics)input.engine;
                output.engine = new TtfMetrics { baseWidth = engine.baseWidth, baseGlyph = engine.baseGlyph };
            }
        }

        public override bool GlyphMetrics(FontMetrics fm, string ch, out ThorVG.GlyphMetrics output)
        {
            return GlyphMetrics(fm, ch, out output, out _);
        }

        public override unsafe bool GlyphMetrics(FontMetrics fm, string ch, out ThorVG.GlyphMetrics output, out int next)
        {
            using var guard = new ScopedLock(glyphKey);
            output = default;
            next = 0;
            if (string.IsNullOrEmpty(ch)) return false;
            int idx = 0;
            var bytes = TvgUtf8.Encode(TvgStr.Duplicate(ch)!);
            var code = DecodeCodepoint(bytes, ref idx, bytes.Length);
            var glyph = Request(code);
            if (glyph == null) return false;
            var scale = (fm.fontSize * DPI) / reader.metrics.unitsPerEm;
            output.advance = glyph.advance * scale;
            output.bearing = glyph.lsb * scale;

            var storedBounds = glyph.NativeBounds;
            var min = storedBounds.min;
            var max = storedBounds.max;
            if (max.x == 0.0f && max.y == 0.0f)
            {
                var bounds = new BBox();
                if (glyph.path.Bounds(null, ref bounds))
                {
                    min = bounds.min;
                    max = bounds.max;
                    var y = min.y;
                    min.y = -max.y;
                    max.y = -y;
                    glyph.NativeBounds = new BBox { min = min, max = max };
                }
            }
            output.min = new Point(min.x * scale, min.y * scale);
            output.max = new Point(max.x * scale, max.y * scale);
            // The managed public API returns a UTF-16 substring index.
            next = TvgUtf8.Decode(bytes.AsSpan(0, idx)).Length;
            return true;
        }

        public override void Metrics(FontMetrics fm, out TextMetrics output)
        {
            var scale = (fm.fontSize * DPI) / reader.metrics.unitsPerEm;
            output.advance = reader.metrics.hhea.advance * scale;
            output.ascent = reader.metrics.hhea.ascent * scale;
            output.descent = reader.metrics.hhea.descent * scale;
            output.linegap = reader.metrics.hhea.linegap * scale;
        }
    }
}
