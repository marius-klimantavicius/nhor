// LCD Subpixel Text Rendering for ThorVG
//
// Adapted from Anti-Grain Geometry (BSD, Maxim Shemanarev) and
// PixelFarm.Typography (MIT, WinterDev).
//
// Technique: Render text glyph outlines at 3x horizontal resolution using
// ThorVG's existing FreeType-based scanline rasterizer, then collapse to
// per-channel RGB values via LCD energy distribution for sharper perceived
// text on LCD displays.
//
// The algorithm:
//   1. Scale outline X coords by 3, run rasterizer → 3x-wide RLE coverage
//   2. For each scanline, write 3x coverage into a grayscale line buffer
//   3. Apply LcdDistributionLut to decompose each sample into 5 weighted
//      energy components (tertiary-secondary-primary-secondary-tertiary)
//   4. Accumulate energy across neighbors via LcdAccumBuffer (circular ring)
//   5. Blend per-channel (R,G,B independently) onto destination pixel
//
// Reference:
//   http://antigrain.com/stuff/lcd_font.zip (AGG LCD rendering, Maxim Shemanarev)
//   http://grc.com/cttech.htm (Steve Gibson's ClearType analysis)

using System;
using System.Runtime.CompilerServices;

namespace ThorVG
{
    // =====================================================================
    //  LcdDistributionLut — sub-pixel energy distribution lookup table
    // =====================================================================

    /// <summary>
    /// LCD sub-pixel energy distribution lookup table.
    /// <para>
    /// Converts a grayscale coverage value [0..255] to three weighted outputs
    /// (primary, secondary, tertiary) whose sum satisfies:<br/>
    ///   <c>primary + 2*secondary + 2*tertiary = 1.0</c>
    /// </para>
    /// </summary>
    public sealed class LcdDistributionLut
    {
        private readonly byte[] _primary;
        private readonly byte[] _secondary;
        private readonly byte[] _tertiary;

        private LcdDistributionLut(byte grayLevels, double prim, double second, double tert)
        {
            int n = grayLevels;
            var p = new byte[n + 1];
            var s = new byte[n + 1];
            var t = new byte[n + 1];

            double norm = (255.0 / n) / (prim + second * 2 + tert * 2);
            prim *= norm;
            second *= norm;
            tert *= norm;

            for (int i = n; i >= 0; --i)
            {
                p[i] = (byte)Math.Floor(prim * i);
                s[i] = (byte)Math.Floor(second * i);
                t[i] = (byte)Math.Floor(tert * i);
            }

            _primary = new byte[256];
            _secondary = new byte[256];
            _tertiary = new byte[256];

            for (int i = 0; i < 256; ++i)
            {
                byte level = (byte)((i / 255f) * n);
                _primary[i] = p[level];
                _secondary[i] = s[level];
                _tertiary[i] = t[level];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte Primary(byte raw) => _primary[raw];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte Secondary(byte raw) => _secondary[raw];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte Tertiary(byte raw) => _tertiary[raw];

        /// <summary>
        /// Create a LUT with unnormalized weights (auto-normalized internally).
        /// <para>Steve Gibson recommends 1/3, 2/9, 1/9 but that is too blurry.
        /// Default 4:3.5:0.5 gives crisp text with minimal color fringing.</para>
        /// </summary>
        public static LcdDistributionLut Create(byte nLevels, float prim, float second, float tert)
        {
            float total = prim + 2 * second + 2 * tert;
            return new LcdDistributionLut(nLevels, prim / total, second / total, tert / total);
        }
    }

    // =====================================================================
    //  LcdAccumBuffer — circular energy accumulation ring
    // =====================================================================

    /// <summary>
    /// Forward-write circular accumulation buffer for LCD energy distribution.
    /// <para>
    /// Each input grayscale sample is expanded into 5 weighted components
    /// distributed across a ring of 5 accumulators:
    /// <c>[tertiary] [secondary] [PRIMARY] [secondary] [tertiary]</c>
    /// </para>
    /// </summary>
    internal struct LcdAccumBuffer
    {
        private int _b0, _b1, _b2, _b3, _b4;
        private int _idx;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteAndRead(byte tert, byte sec, byte prim, out byte readBack)
        {
            // Distribute energy: tert-sec-prim-sec-tert across 5 ring positions
            // Read back the oldest accumulated value, then reset that slot.
            switch (_idx)
            {
                case 0:
                    readBack = Clamp(_b0 + tert); _b0 = 0;
                    _b1 += sec; _b2 += prim; _b3 += sec; _b4 += tert;
                    _idx = 1; break;
                case 1:
                    readBack = Clamp(_b1 + tert); _b1 = 0;
                    _b2 += sec; _b3 += prim; _b4 += sec; _b0 += tert;
                    _idx = 2; break;
                case 2:
                    readBack = Clamp(_b2 + tert); _b2 = 0;
                    _b3 += sec; _b4 += prim; _b0 += sec; _b1 += tert;
                    _idx = 3; break;
                case 3:
                    readBack = Clamp(_b3 + tert); _b3 = 0;
                    _b4 += sec; _b0 += prim; _b1 += sec; _b2 += tert;
                    _idx = 4; break;
                default:
                    readBack = Clamp(_b4 + tert); _b4 = 0;
                    _b0 += sec; _b1 += prim; _b2 += sec; _b3 += tert;
                    _idx = 0; break;
            }
        }

        public void ReadRemaining(out byte v0, out byte v1, out byte v2, out byte v3)
        {
            switch (_idx)
            {
                case 0: v0 = Clamp(_b0); v1 = Clamp(_b1); v2 = Clamp(_b2); v3 = Clamp(_b3); break;
                case 1: v0 = Clamp(_b1); v1 = Clamp(_b2); v2 = Clamp(_b3); v3 = Clamp(_b4); break;
                case 2: v0 = Clamp(_b2); v1 = Clamp(_b3); v2 = Clamp(_b4); v3 = Clamp(_b0); break;
                case 3: v0 = Clamp(_b3); v1 = Clamp(_b4); v2 = Clamp(_b0); v3 = Clamp(_b1); break;
                default: v0 = Clamp(_b4); v1 = Clamp(_b0); v2 = Clamp(_b1); v3 = Clamp(_b2); break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte Clamp(int v) => (byte)Math.Min(255, v);
    }

    // =====================================================================
    //  SwLcdSubpixel — main LCD rendering engine
    // =====================================================================

    /// <summary>
    /// LCD subpixel text rendering engine for ThorVG's software renderer.
    /// <para>
    /// Renders text glyphs at 3x horizontal resolution, then collapses to
    /// per-channel RGB values using LCD energy distribution. Only meaningful
    /// for the SW engine with 32-bit RGBA surfaces on LCD displays.
    /// </para>
    /// </summary>
    public static unsafe class SwLcdSubpixel
    {
        /// <summary>Global switch: when true, all <see cref="Text"/> paints use LCD rendering.</summary>
        public static bool Enabled { get; set; }

        /// <summary>The LCD distribution LUT (replaceable for tuning).</summary>
        public static LcdDistributionLut Lut { get; set; } = LcdDistributionLut.Create(64, 4f, 3.5f, 0.5f);

        /// <summary>
        /// Thread-local flag set by <see cref="Text.PaintUpdate"/> to signal that the
        /// current shape being processed is a text glyph eligible for LCD rendering.
        /// </summary>
        [ThreadStatic]
        internal static bool _textContext;

        // Reusable grayscale line buffer (thread-local to avoid allocation per frame)
        [ThreadStatic]
        private static byte[]? _grayBuf;

        /// <summary>Whether LCD rendering is active for the current shape being processed.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsActive() => _textContext;

        // =================================================================
        //  RLE Generation — scale outline 3x and rasterize
        // =================================================================

        /// <summary>
        /// Generate a 3x-horizontal-resolution RLE from the existing outline.
        /// <para>
        /// Temporarily scales outline X coordinates by 3, runs the existing
        /// scanline rasterizer, then restores the original coordinates (the
        /// outline may still be needed for stroke generation).
        /// </para>
        /// Must be called while <c>shape.hasOutline</c> is true.
        /// </summary>
        internal static void GenerateLcdRle(SwShape shape, in RenderRegion bbox, SwMpool mpool, uint tid)
        {
            if (!shape.hasOutline) return;

            var ptCount = shape.outline.pts.count;
            if (ptCount == 0) return;

            var pts = shape.outline.pts.data;

            // --- scale X * 3 ---
            for (uint i = 0; i < ptCount; i++)
                pts[i].x *= 3;

            // 3x-wide bounding box (Y unchanged)
            var lcdBbox = new RenderRegion(
                bbox.min.x * 3, bbox.min.y,
                bbox.max.x * 3, bbox.max.y);

            // Generate LCD RLE using the existing rasterizer
            fixed (SwOutline* ol = &shape.outline)
            {
                shape.hasLcdRle = SwRleOps.rleRender(ref shape.lcdRle, ol, lcdBbox, mpool, tid, true);
            }

            // --- restore X / 3 --- (exact since we started from integers)
            for (uint i = 0; i < ptCount; i++)
                pts[i].x /= 3;
        }

        /// <summary>Reset LCD RLE data (called during shape reset).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ResetLcdRle(SwShape shape)
        {
            if (shape.hasLcdRle)
            {
                SwRleOps.rleReset(ref shape.lcdRle);
                shape.hasLcdRle = false;
            }
        }

        // =================================================================
        //  Rasterization — LCD per-channel blending
        // =================================================================

        /// <summary>
        /// Render a shape using LCD subpixel blending.
        /// Reads 3x-resolution coverage from <c>shape.lcdRle</c> and blends
        /// per-channel (R,G,B independently) onto the surface.
        /// </summary>
        public static bool RasterLcdShape(SwSurface surface, SwShape shape, in RenderRegion bbox, RenderColor c)
        {
            if (!shape.hasLcdRle || shape.lcdRle.Invalid()) return false;
            if (surface.channelSize != sizeof(uint)) return false;

            var lut = Lut;
            int surfW = (int)surface.w;

            // Detect byte order from color space:
            //   ABGR in memory (LE uint): byte[0]=R byte[1]=G byte[2]=B byte[3]=A
            //   ARGB in memory (LE uint): byte[0]=B byte[1]=G byte[2]=R byte[3]=A
            bool argb = surface.cs == ColorSpace.ARGB8888 || surface.cs == ColorSpace.ARGB8888S;
            byte c0, c1, c2;
            if (argb) { c0 = c.b; c1 = c.g; c2 = c.r; }
            else      { c0 = c.r; c1 = c.g; c2 = c.b; }
            byte cAlpha = c.a;

            // Ensure thread-local grayscale buffer is large enough
            int grayLen = surfW * 3 + 8;
            if (_grayBuf == null || _grayBuf.Length < grayLen)
                _grayBuf = new byte[grayLen];
            var grayBuf = _grayBuf;

            // Fetch LCD RLE spans (3x coordinate space)
            var lcdBbox = new RenderRegion(
                bbox.min.x * 3, bbox.min.y,
                bbox.max.x * 3, bbox.max.y);

            SwSpan* end;
            SwSpan* span = shape.lcdRle.Fetch(lcdBbox, out end);
            if (span >= end) return true;

            int stride = (int)surface.stride;
            int currentY = span->y;

            while (true)
            {
                // --- Collect spans for this scanline into grayscale buffer ---
                int grayMinX = grayLen, grayMaxX = 0;

                while (span < end && span->y == currentY)
                {
                    int sx = span->x;
                    int slen = span->len;

                    // Clamp to buffer bounds
                    if (sx < 0) { slen += sx; sx = 0; }
                    if (sx + slen > grayLen) slen = grayLen - sx;

                    if (slen > 0)
                    {
                        for (int i = 0; i < slen; i++)
                            grayBuf[sx + i] = span->coverage;

                        if (sx < grayMinX) grayMinX = sx;
                        if (sx + slen > grayMaxX) grayMaxX = sx + slen;
                    }
                    ++span;
                }

                // --- Blend scanline ---
                if (grayMaxX > grayMinX && currentY >= bbox.min.y && currentY < bbox.max.y)
                {
                    BlendLcdScanline(
                        surface.buf32, stride, currentY,
                        grayBuf, grayMinX, grayMaxX,
                        bbox.min.x, bbox.max.x,
                        c0, c1, c2, cAlpha, lut);
                }

                // Clear used portion (cheap — only the touched range)
                if (grayMaxX > grayMinX)
                    Array.Clear(grayBuf, grayMinX, grayMaxX - grayMinX);

                if (span >= end) break;
                currentY = span->y;
            }

            return true;
        }

        // =================================================================
        //  Per-scanline LCD blend
        // =================================================================

        /// <summary>
        /// Blend one scanline from the 3x grayscale buffer to the framebuffer
        /// using LCD energy distribution and per-channel alpha compositing.
        /// </summary>
        private static void BlendLcdScanline(
            uint* fb, int stride, int y,
            byte[] grayBuf, int grayMin, int grayMax,
            int pixMinX, int pixMaxX,
            byte c0, byte c1, byte c2, byte alpha,
            LcdDistributionLut lut)
        {
            uint* row = fb + y * stride;

            // Map 3x grayscale range → output pixel range
            int firstPx = Math.Max(grayMin / 3, pixMinX);
            int lastPx = Math.Min((grayMax + 2) / 3 + 2, pixMaxX); // +2 for accumulator tail

            int srcIdx = firstPx * 3;
            int srcEnd = grayMax;

            if (srcIdx + 3 > srcEnd) return;

            // --- Pre-accumulation (first triplet produces no output pixel) ---
            var acc = new LcdAccumBuffer();
            {
                byte g0 = Gs(grayBuf, srcIdx), g1 = Gs(grayBuf, srcIdx + 1), g2 = Gs(grayBuf, srcIdx + 2);
                byte e;
                acc.WriteAndRead(lut.Tertiary(g0), lut.Secondary(g0), lut.Primary(g0), out e);
                acc.WriteAndRead(lut.Tertiary(g1), lut.Secondary(g1), lut.Primary(g1), out e);
                acc.WriteAndRead(lut.Tertiary(g2), lut.Secondary(g2), lut.Primary(g2), out e);
            }
            srcIdx += 3;
            int dstX = firstPx + 1;

            // --- Main loop: one output pixel per iteration ---
            while (srcIdx + 2 < srcEnd && dstX < lastPx)
            {
                byte g0 = Gs(grayBuf, srcIdx), g1 = Gs(grayBuf, srcIdx + 1), g2 = Gs(grayBuf, srcIdx + 2);

                byte e0, e1, e2;
                acc.WriteAndRead(lut.Tertiary(g0), lut.Secondary(g0), lut.Primary(g0), out e0);
                acc.WriteAndRead(lut.Tertiary(g1), lut.Secondary(g1), lut.Primary(g1), out e1);
                acc.WriteAndRead(lut.Tertiary(g2), lut.Secondary(g2), lut.Primary(g2), out e2);

                if (dstX >= pixMinX)
                {
                    // Per-channel blend:  dst = existing + (color − existing) × energy × alpha / 65536
                    // Note: e0↔e2 swap accounts for sub-pixel R-G-B ordering
                    byte* dst = (byte*)(row + dstX);
                    dst[0] = LcdBlend(c0, dst[0], e2, alpha);
                    dst[1] = LcdBlend(c1, dst[1], e1, alpha);
                    dst[2] = LcdBlend(c2, dst[2], e0, alpha);
                    // Alpha channel: max of per-channel energies scaled by source alpha
                    byte maxE = Math.Max(e0, Math.Max(e1, e2));
                    byte blendedA = (byte)((maxE * alpha + 127) >> 8);
                    if (blendedA > dst[3]) dst[3] = blendedA;
                }

                srcIdx += 3;
                dstX++;
            }

            // --- Trailing pixels from accumulator drain ---
            {
                byte r0, r1, r2, r3;
                acc.ReadRemaining(out r0, out r1, out r2, out r3);

                if (dstX >= pixMinX && dstX < lastPx)
                {
                    byte* dst = (byte*)(row + dstX);
                    dst[0] = LcdBlend(c0, dst[0], r2, alpha);
                    dst[1] = LcdBlend(c1, dst[1], r1, alpha);
                    dst[2] = LcdBlend(c2, dst[2], r0, alpha);
                    byte maxE = Math.Max(r0, Math.Max(r1, r2));
                    byte blendedA = (byte)((maxE * alpha + 127) >> 8);
                    if (blendedA > dst[3]) dst[3] = blendedA;
                    dstX++;
                }
                if (dstX >= pixMinX && dstX < lastPx)
                {
                    byte* dst = (byte*)(row + dstX);
                    dst[0] = LcdBlend(c0, dst[0], r3, alpha);
                }
            }
        }

        /// <summary>
        /// Single-channel LCD blend: <c>existing + (color − existing) × energy × alpha / 65536</c>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte LcdBlend(byte color, byte existing, byte energy, byte alpha)
        {
            return (byte)((((color - existing) * (energy * alpha)) + (existing << 16)) >> 16);
        }

        /// <summary>Safe grayscale buffer read (returns 0 for out-of-range indices).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte Gs(byte[] buf, int idx)
        {
            return (uint)idx < (uint)buf.Length ? buf[idx] : (byte)0;
        }
    }
}
