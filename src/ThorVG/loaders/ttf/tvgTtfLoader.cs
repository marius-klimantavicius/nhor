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
    }

    /// <summary>
    /// TTF font loader. Mirrors C++ TtfLoader.
    /// </summary>
    public class TtfLoader : FontLoader
    {
        private const uint LINE_FEED_CODEPOINT = 0x0A;
        private const uint DOT_CODEPOINT = 0x2E;

        private static bool IsWordBreak(uint code) => code switch
        {
            0x20   => true,  // space
            0x09   => true,  // tab
            0xA0   => true,  // no-break space
            0x1680 => true,  // ogham space mark
            >= 0x2000 and <= 0x200A => true,  // en/em/thin/hair spaces etc.
            0x202F => true,  // narrow no-break space
            0x205F => true,  // medium mathematical space
            0x3000 => true,  // ideographic space
            _ => false,
        };

        public TtfReader reader = new TtfReader();
        public Dictionary<uint, TtfGlyphMetrics> glyphs = new Dictionary<uint, TtfGlyphMetrics>();

        public TtfLoader() : base(FileType.Ttf)
        {
        }

        // ----------------------------------------------------------------
        //  UTF-8 codepoint decoding
        // ----------------------------------------------------------------

        private static uint DecodeCodepoint(string text, ref int idx)
        {
            if (idx >= text.Length) return 0;

            char c = text[idx];
            // Handle surrogate pairs for codepoints > 0xFFFF
            if (char.IsHighSurrogate(c) && idx + 1 < text.Length && char.IsLowSurrogate(text[idx + 1]))
            {
                var cp = (uint)char.ConvertToUtf32(c, text[idx + 1]);
                idx += 2;
                return cp;
            }
            idx++;
            return c;
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
                var pt = new Point(p->x + cursor.x + kerning.x, p->y + cursor.y + kerning.y);
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

        private float Height(uint loc, float spacing)
        {
            return (reader.metrics.hhea.advance * loc - reader.metrics.hhea.linegap) * spacing;
        }

        private uint FeedLine(FontMetrics fm, float box, float x, uint begin, uint end, ref Point cursor, RenderPath output)
        {
            AlignX(fm.align.x, box, x, begin, end, output);
            cursor.x = 0.0f;
            cursor.y += reader.metrics.hhea.advance * fm.spacing.y;
            ++fm.lines;
            return output.pts.count;
        }

        private TtfGlyphMetrics? Request(uint code)
        {
            if (code == 0) return null;

            if (glyphs.TryGetValue(code, out var existing))
            {
                return existing;
            }

            var tgm = new TtfGlyphMetrics();
            glyphs[code] = tgm;
            var glyphOffset = reader.Glyph(code, tgm);
            if (!reader.Convert(tgm.path, tgm, glyphOffset, new Point(0.0f, 0.0f), 1))
            {
                glyphs.Remove(code);
                return null;
            }
            return tgm;
        }

        // ----------------------------------------------------------------
        //  Wrapping modes
        // ----------------------------------------------------------------

        private void WrapNone(FontMetrics fm, in Point box, string utf8, RenderPath output)
        {
            TtfGlyphMetrics? ltgm = null;
            var cursor = new Point(0, 0);
            uint line = 0;
            int idx = 0;

            while (idx < utf8.Length)
            {
                var code = DecodeCodepoint(utf8, ref idx);
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
                cursor.x += (rtgm.advance + kerning.x) * fm.spacing.x;

                if (cursor.x > fm.size.x) fm.size.x = cursor.x;

                // store the base glyph width for italic transform
                if (ltgm == null && rtgm.w > 0.0f)
                    ((TtfMetrics)fm.engine!).baseWidth = rtgm.w;
                ltgm = rtgm;
            }
            fm.size.y = Height(fm.lines, fm.spacing.y);
            AlignY(fm.align.y, box.y, fm.size.y, 0, line, output);
            Align(fm.align, box, new Point(cursor.x, fm.size.y), line, output.pts.count, output);
        }

        private void WrapChar(FontMetrics fm, in Point box, string utf8, RenderPath output)
        {
            TtfGlyphMetrics? ltgm = null;
            uint line = 0;
            var cursor = new Point(0, 0);
            int idx = 0;

            while (idx < utf8.Length)
            {
                var code = DecodeCodepoint(utf8, ref idx);
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

                if (ltgm == null && rtgm.w > 0.0f)
                    ((TtfMetrics)fm.engine!).baseWidth = rtgm.w;
                ltgm = rtgm;
            }
            fm.size.y = Height(fm.lines, fm.spacing.y);
            AlignY(fm.align.y, box.y, fm.size.y, 0, line, output);
            Align(fm.align, box, new Point(cursor.x, fm.size.y), line, output.pts.count, output);
        }

        private unsafe void WrapWord(FontMetrics fm, in Point box, string utf8, RenderPath output, bool smart)
        {
            TtfGlyphMetrics? ltgm = null;
            uint line = 0;
            uint word = 0;
            float wadv = 0.0f;
            var hadv = reader.metrics.hhea.advance * fm.spacing.y;
            var cursor = new Point(0, 0);
            int idx = 0;

            while (idx < utf8.Length)
            {
                var code = DecodeCodepoint(utf8, ref idx);
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

                if (ltgm == null && rtgm.w > 0.0f)
                    ((TtfMetrics)fm.engine!).baseWidth = rtgm.w;
                ltgm = rtgm;
            }
            fm.size.y = Height(fm.lines, fm.spacing.y);
            AlignY(fm.align.y, box.y, fm.size.y, 0, line, output);
            Align(fm.align, box, new Point(cursor.x, fm.size.y), line, output.pts.count, output);
        }

        private void WrapEllipsis(FontMetrics fm, in Point box, string utf8, RenderPath output)
        {
            TtfGlyphMetrics? ltgm = null;
            uint line = 0;
            var cursor = new Point(0, 0);
            uint capturePts = 0;
            uint captureCmds = 0;
            float captureXadv = 0.0f;
            bool stop = false;
            int idx = 0;

            while (idx < utf8.Length)
            {
                var code = DecodeCodepoint(utf8, ref idx);
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
                    if (cursor.x + (rtgm.advance + kerning.x) * 3 > box.x)
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

                if (ltgm == null && rtgm.w > 0.0f)
                    ((TtfMetrics)fm.engine!).baseWidth = rtgm.w;

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

        public override bool Open(string path)
        {
            name = TvgStr.Filename(path);

            try
            {
                reader.data = File.ReadAllBytes(path);
                reader.size = (uint)reader.data.Length;
            }
            catch
            {
                return false;
            }
            return reader.Header();
        }

        /// <summary>Open from raw data buffer (with resource path).</summary>
        public override bool Open(byte[] data, uint size, string? rpath, bool copy)
        {
            return Open(data, size, copy);
        }

        /// <summary>Open from raw data buffer.</summary>
        public bool Open(byte[] rawData, uint dataSize, bool copy)
        {
            if (copy)
            {
                reader.data = new byte[dataSize];
                Buffer.BlockCopy(rawData, 0, reader.data, 0, (int)dataSize);
            }
            else
            {
                reader.data = rawData;
            }
            reader.size = dataSize;
            return reader.Header();
        }

        public override void Transform(Paint paint, FontMetrics fm, float italicShear)
        {
            var scale = 1.0f / fm.scale;
            var m = new Matrix(
                scale, -italicShear * scale, italicShear * ((TtfMetrics)fm.engine!).baseWidth * scale,
                0, scale, reader.metrics.hhea.ascent * scale,
                0, 0, 1);
            paint.Transform(m);
        }

        public override bool Get(FontMetrics fm, string? text, RenderPath output)
        {
            output.Clear();

            if (string.IsNullOrEmpty(text) || fm.fontSize == 0.0f) return false;

            fm.scale = reader.metrics.unitsPerEm / (fm.fontSize * DPI);
            fm.size = new Point(0, 0);
            fm.lines = 1;
            if (fm.engine == null) fm.engine = new TtfMetrics();

            var box = new Point(fm.box.x * fm.scale, fm.box.y * fm.scale);

            if (fm.wrap == TextWrap.None || fm.box.x == 0.0f) WrapNone(fm, box, text, output);
            else if (fm.wrap == TextWrap.Character) WrapChar(fm, box, text, output);
            else if (fm.wrap == TextWrap.Word) WrapWord(fm, box, text, output, false);
            else if (fm.wrap == TextWrap.Smart) WrapWord(fm, box, text, output, true);
            else if (fm.wrap == TextWrap.Ellipsis) WrapEllipsis(fm, box, text, output);
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
            output.wrap = input.wrap;
            if (input.engine != null)
            {
                output.engine = new TtfMetrics { baseWidth = ((TtfMetrics)input.engine).baseWidth };
            }
        }

        public override bool GlyphMetrics(FontMetrics fm, string ch, out ThorVG.GlyphMetrics output)
        {
            output = default;
            if (string.IsNullOrEmpty(ch)) return false;
            int idx = 0;
            var code = DecodeCodepoint(ch, ref idx);
            var glyph = Request(code);
            if (glyph == null) return false;
            var scale = (fm.fontSize * DPI) / reader.metrics.unitsPerEm;
            output.advance = glyph.advance * scale;
            output.bearing = glyph.lsb * scale;
            output.min = new Point(glyph.x * scale, glyph.y * scale);
            output.max = new Point((glyph.w + glyph.x - 1) * scale, (glyph.h + glyph.y - 1) * scale);
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
