// Ported from ThorVG/src/renderer/sw_engine/tvgSwPostEffect.cpp

using System;
using System.Runtime.CompilerServices;
using static ThorVG.SwHelper;
using static ThorVG.SwRaster;

namespace ThorVG
{
    // =====================================================================
    //  Internal data types for post-effects
    // =====================================================================

    internal class SwGaussianBlur
    {
        public const int MAX_LEVEL = 3;
        public int level;
        public int[] kernel = new int[MAX_LEVEL];
        public int extends;
    }

    internal class SwDropShadow : SwGaussianBlur
    {
        public SwPoint offset;
    }

    // =====================================================================
    //  SwPostEffect - post-processing effects
    // =====================================================================

    public static unsafe class SwPostEffect
    {
        // =====================================================================
        //  Gaussian Blur
        // =====================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int _gaussianEdgeWrap(int end, int idx)
        {
            var r = idx % (end + 1);
            return (r < 0) ? (end + 1) + r : r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int _gaussianEdgeExtend(int end, int idx)
        {
            if (idx < 0) return 0;
            else if (idx > end) return end;
            return idx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int _gaussianRemap(int border, int end, int idx)
        {
            if (border == 1) return _gaussianEdgeWrap(end, idx);
            return _gaussianEdgeExtend(end, idx);
        }

        private static void _gaussianFilter(byte* dst, byte* src, int stride, int w, int h, in RenderRegion bbox, int dimension, bool flipped, int border = 0)
        {
            if (flipped)
            {
                src += (bbox.min.x * stride + bbox.min.y) << 2;
                dst += (bbox.min.x * stride + bbox.min.y) << 2;
            }
            else
            {
                src += (bbox.min.y * stride + bbox.min.x) << 2;
                dst += (bbox.min.y * stride + bbox.min.x) << 2;
            }

            var iarr = 1.0f / (dimension + dimension + 1);
            var end = w - 1;

            for (int y = 0; y < h; ++y)
            {
                var p = y * stride;
                var i = p * 4;
                var l = -(dimension + 1);
                var r = dimension;
                int acc0 = 0, acc1 = 0, acc2 = 0, acc3 = 0;

                // initial accumulation
                for (int x = l; x < r; ++x)
                {
                    var id = (_gaussianRemap(border, end, x) + p) * 4;
                    acc0 += src[id++];
                    acc1 += src[id++];
                    acc2 += src[id++];
                    acc3 += src[id];
                }
                // perform filtering
                for (int x = 0; x < w; ++x, ++r, ++l)
                {
                    var rid = (_gaussianRemap(border, end, r) + p) * 4;
                    var lid = (_gaussianRemap(border, end, l) + p) * 4;
                    acc0 += src[rid++] - src[lid++];
                    acc1 += src[rid++] - src[lid++];
                    acc2 += src[rid++] - src[lid++];
                    acc3 += src[rid] - src[lid];
                    dst[i++] = (byte)(acc0 * iarr);
                    dst[i++] = (byte)(acc1 * iarr);
                    dst[i++] = (byte)(acc2 * iarr);
                    dst[i++] = (byte)(acc3 * iarr);
                }
            }
        }

        // Fast Almost-Gaussian Filtering Method by Peter Kovesi
        private static int _gaussianInit(SwGaussianBlur data, float sigma, int quality)
        {
            if (TvgMath.Zero(sigma)) return 0;

            data.level = (int)(SwGaussianBlur.MAX_LEVEL * ((quality - 1) * 0.01f)) + 1;

            // compute box kernel sizes
            var wl = (int)Math.Sqrt((12 * sigma / SwGaussianBlur.MAX_LEVEL) + 1);
            if (wl % 2 == 0) --wl;
            var wu = wl + 2;
            var mi = (12 * sigma - SwGaussianBlur.MAX_LEVEL * wl * wl - 4 * SwGaussianBlur.MAX_LEVEL * wl - 3 * SwGaussianBlur.MAX_LEVEL) / (-4 * wl - 4);
            var m = (int)(mi + 0.5f);
            var extends_ = 0;

            for (int i = 0; i < data.level; i++)
            {
                data.kernel[i] = ((i < m ? wl : wu) - 1) / 2;
                extends_ += data.kernel[i];
            }

            return extends_;
        }

        public static bool effectGaussianBlurRegion(RenderEffectGaussianBlur p)
        {
            var bbox = p.extend;
            var extra = ((SwGaussianBlur)p.rd!).extends;

            if (p.direction != 2)
            {
                bbox.min.x = -extra;
                bbox.max.x = extra;
            }
            if (p.direction != 1)
            {
                bbox.min.y = -extra;
                bbox.max.y = extra;
            }

            p.extend = bbox;
            return true;
        }

        public static void effectGaussianBlurUpdate(RenderEffectGaussianBlur p, in Matrix transform)
        {
            if (p.rd == null) p.rd = new SwGaussianBlur();
            var rd = (SwGaussianBlur)p.rd;

            var scale = (float)Math.Sqrt(transform.e11 * transform.e11 + transform.e12 * transform.e12);
            rd.extends = _gaussianInit(rd, (float)Math.Pow(p.sigma * scale, 2), p.quality);

            if (rd.extends == 0)
            {
                p.valid = false;
                return;
            }

            p.valid = true;
        }

        public static bool effectGaussianBlur(SwCompositor cmp, SwSurface surface, RenderEffectGaussianBlur p)
        {
            var buffer = surface.compositor!.image;
            var data = (SwGaussianBlur)p.rd!;
            var bbox = cmp.bbox;
            var w = bbox.max.x - bbox.min.x;
            var h = bbox.max.y - bbox.min.y;
            var stride = (int)cmp.image.stride;
            var front = cmp.image.buf32;
            var back = buffer.buf32;
            var swapped = false;

            // horizontal
            if (p.direction != 2)
            {
                for (int i = 0; i < data.level; ++i)
                {
                    _gaussianFilter((byte*)back, (byte*)front, stride, w, h, bbox, data.kernel[i], false, p.border);
                    var tmp = front; front = back; back = tmp;
                    swapped = !swapped;
                }
            }

            // vertical
            if (p.direction != 1)
            {
                rasterXYFlip(front, back, stride, w, h, bbox, false);
                { var tmp = front; front = back; back = tmp; }

                for (int i = 0; i < data.level; ++i)
                {
                    _gaussianFilter((byte*)back, (byte*)front, stride, h, w, bbox, data.kernel[i], true, p.border);
                    var tmp = front; front = back; back = tmp;
                    swapped = !swapped;
                }

                rasterXYFlip(front, back, stride, h, w, bbox, true);
                { var tmp = front; front = back; back = tmp; }
            }

            if (swapped)
            {
                // swap buf32 pointers (buf8 is derived from buf32, so swapping buf32 is sufficient)
                var cmpBuf = cmp.image.buf32;
                cmp.image.buf32 = buffer.buf32;
                buffer.buf32 = cmpBuf;
            }

            return true;
        }

        // =====================================================================
        //  Drop Shadow
        // =====================================================================

        private static void _dropShadowFilter(uint* dst, uint* src, int stride, int w, int h, in RenderRegion bbox, int dimension, uint color, bool flipped)
        {
            if (flipped)
            {
                src += (bbox.min.x * stride + bbox.min.y);
                dst += (bbox.min.x * stride + bbox.min.y);
            }
            else
            {
                src += (bbox.min.y * stride + bbox.min.x);
                dst += (bbox.min.y * stride + bbox.min.x);
            }
            var iarr = 1.0f / (dimension + dimension + 1);
            var end = w - 1;

            for (int y = 0; y < h; ++y)
            {
                var p = y * stride;
                var i = p;
                var l = -(dimension + 1);
                var r = dimension;
                int acc = 0;

                for (int x = l; x < r; ++x)
                {
                    var id = _gaussianEdgeExtend(end, x) + p;
                    acc += (int)A(src[id]);
                }
                for (int x = 0; x < w; ++x, ++r, ++l)
                {
                    var rid = _gaussianEdgeExtend(end, r) + p;
                    var lid = _gaussianEdgeExtend(end, l) + p;
                    acc += (int)A(src[rid]) - (int)A(src[lid]);
                    dst[i++] = ALPHA_BLEND(color, (byte)(acc * iarr));
                }
            }
        }

        private static void _shift(ref uint* dst, ref uint* src, int dstride, int sstride, int wmax, int hmax, in RenderRegion bbox, SwPoint offset, ref SwSize size)
        {
            size.w = bbox.max.x - bbox.min.x;
            size.h = bbox.max.y - bbox.min.y;

            if (offset.x < 0)
            {
                src -= offset.x;
                size.w += offset.x;
            }
            else
            {
                dst += offset.x;
                size.w -= offset.x;
            }

            if (offset.y < 0)
            {
                src -= (offset.y * sstride);
                size.h += offset.y;
            }
            else
            {
                dst += (offset.y * dstride);
                size.h -= offset.y;
            }
        }

        private static void _dropShadowNoFilter(uint* dst, uint* src, int dstride, int sstride, uint dw, uint dh, in RenderRegion bbox, in SwPoint offset, uint color, byte opacity, bool direct)
        {
            src += (bbox.min.y * sstride + bbox.min.x);
            dst += (bbox.min.y * dstride + bbox.min.x);

            var size = new SwSize();
            _shift(ref dst, ref src, dstride, sstride, (int)dw, (int)dh, bbox, offset, ref size);

            for (var y = 0; y < size.h; ++y)
            {
                var s2 = src;
                var d2 = dst;
                for (int x = 0; x < size.w; ++x, ++d2, ++s2)
                {
                    var a = (byte)MULTIPLY(opacity, A(*s2));
                    if (!direct || a == 255) *d2 = ALPHA_BLEND(color, a);
                    else *d2 = INTERPOLATE(color, *d2, a);
                }
                src += sstride;
                dst += dstride;
            }
        }

        private static void _dropShadowNoFilter(SwImage dimg, SwImage simg, in RenderRegion bbox, in SwPoint offset, uint color)
        {
            int dstride = (int)dimg.stride;
            int sstride = (int)simg.stride;

            _dropShadowNoFilter(dimg.buf32, simg.buf32, dstride, sstride, dimg.w, dimg.h, bbox, offset, color, 255, false);

            var src = simg.buf32 + (bbox.min.y * sstride + bbox.min.x);
            var dst = dimg.buf32 + (bbox.min.y * dstride + bbox.min.x);

            for (var y = 0; y < (bbox.max.y - bbox.min.y); ++y)
            {
                var s = src;
                var d = dst;
                for (int x = 0; x < (bbox.max.x - bbox.min.x); ++x, ++d, ++s)
                {
                    *d = *s + ALPHA_BLEND(*d, IA(*s));
                }
                src += sstride;
                dst += dstride;
            }
        }

        private static void _dropShadowShift(uint* dst, uint* src, int dstride, int sstride, uint dw, uint dh, in RenderRegion bbox, in SwPoint offset, int opacity, bool direct)
        {
            src += (bbox.min.y * sstride + bbox.min.x);
            dst += (bbox.min.y * dstride + bbox.min.x);

            var size = new SwSize();
            _shift(ref dst, ref src, dstride, sstride, (int)dw, (int)dh, bbox, offset, ref size);

            for (var y = 0; y < size.h; ++y)
            {
                if (direct) rasterTranslucentPixel32(dst, src, (uint)size.w, (byte)opacity);
                else rasterPixel32(dst, src, (uint)size.w, (byte)opacity);
                src += sstride;
                dst += dstride;
            }
        }

        public static bool effectDropShadowRegion(RenderEffectDropShadow p)
        {
            var bbox = p.extend;
            var rd = (SwDropShadow)p.rd!;
            var extra = rd.extends;

            bbox.min.x = -extra;
            bbox.min.y = -extra;
            bbox.max.x = extra;
            bbox.max.y = extra;

            if (rd.offset.x < 0) bbox.min.x += rd.offset.x;
            else bbox.max.x += rd.offset.x;

            if (rd.offset.y < 0) bbox.min.y += rd.offset.y;
            else bbox.max.y += rd.offset.y;

            p.extend = bbox;
            return true;
        }

        public static void effectDropShadowUpdate(RenderEffectDropShadow p, in Matrix transform)
        {
            if (p.rd == null) p.rd = new SwDropShadow();
            var rd = (SwDropShadow)p.rd;

            var scale = (float)Math.Sqrt(transform.e11 * transform.e11 + transform.e12 * transform.e12);
            rd.extends = _gaussianInit(rd, (float)Math.Pow(p.sigma * scale, 2), p.quality);

            if (p.color[3] == 0)
            {
                p.valid = false;
                return;
            }

            var radian = TvgMath.Deg2Rad(90.0f - p.angle) - TvgMath.Radian(transform);
            rd.offset = new SwPoint((int)((p.distance * scale) * MathF.Cos(radian)), (int)(-1.0f * (p.distance * scale) * MathF.Sin(radian)));

            p.valid = true;
        }

        public static bool effectDropShadow(SwCompositor cmp, SwSurface[] surfaces, RenderEffectDropShadow p, bool direct)
        {
            var data = (SwDropShadow)p.rd!;
            var bbox = cmp.bbox;
            var w = bbox.max.x - bbox.min.x;
            var h = bbox.max.y - bbox.min.y;

            if (Math.Abs(data.offset.x) >= w || Math.Abs(data.offset.y) >= h) return true;

            var buffer0 = surfaces[0].compositor!.image;
            var buffer1 = surfaces[1].compositor!.image;
            var color = cmp.recoverSfc!.join!(p.color[0], p.color[1], p.color[2], 255);
            var stride = (int)cmp.image.stride;
            var front = cmp.image.buf32;
            var back = buffer1.buf32;

            var opacity = direct ? MULTIPLY(p.color[3], cmp.opacity) : (int)p.color[3];

            // no filter required
            if (data.extends == 0)
            {
                if (direct)
                {
                    _dropShadowNoFilter(cmp.recoverSfc.buf32, cmp.image.buf32, (int)cmp.recoverSfc.stride, (int)cmp.image.stride, cmp.recoverSfc.w, cmp.recoverSfc.h, bbox, data.offset, color, (byte)opacity, direct);
                }
                else
                {
                    _dropShadowNoFilter(buffer1, cmp.image, bbox, data.offset, color);
                    var tmp = cmp.image.buf32; cmp.image.buf32 = buffer1.buf32; buffer1.buf32 = tmp;
                }
                return true;
            }

            // saving the original image; first filter pass
            _dropShadowFilter(back, front, stride, w, h, bbox, data.kernel[0], color, false);
            { var tmp = front; front = buffer0.buf32; buffer0.buf32 = tmp; }
            { var tmp = front; front = back; back = tmp; }

            // horizontal
            for (int i = 1; i < data.level; ++i)
            {
                _dropShadowFilter(back, front, stride, w, h, bbox, data.kernel[i], color, false);
                var tmp = front; front = back; back = tmp;
            }

            // vertical
            rasterXYFlip(front, back, stride, w, h, bbox, false);
            { var tmp = front; front = back; back = tmp; }

            for (int i = 0; i < data.level; ++i)
            {
                _dropShadowFilter(back, front, stride, h, w, bbox, data.kernel[i], color, true);
                var tmp = front; front = back; back = tmp;
            }

            rasterXYFlip(front, back, stride, h, w, bbox, true);
            { var tmp = cmp.image.buf32; cmp.image.buf32 = back; back = tmp; }

            // draw to the main surface directly
            if (direct)
            {
                _dropShadowShift(cmp.recoverSfc.buf32, cmp.image.buf32, (int)cmp.recoverSfc.stride, (int)cmp.image.stride, cmp.recoverSfc.w, cmp.recoverSfc.h, bbox, data.offset, opacity, direct);
                { var tmp = cmp.image.buf32; cmp.image.buf32 = buffer0.buf32; buffer0.buf32 = tmp; }
                return true;
            }

            // draw to the intermediate surface
            rasterClear(surfaces[1], (uint)bbox.min.x, (uint)bbox.min.y, (uint)w, (uint)h);
            _dropShadowShift(buffer1.buf32, cmp.image.buf32, (int)buffer1.stride, (int)cmp.image.stride, buffer1.w, buffer1.h, bbox, data.offset, opacity, direct);
            { var tmp = cmp.image.buf32; cmp.image.buf32 = buffer1.buf32; buffer1.buf32 = tmp; }

            // compositing shadow and body
            var s = buffer0.buf32 + (bbox.min.y * buffer0.stride + bbox.min.x);
            var d = cmp.image.buf32 + (bbox.min.y * cmp.image.stride + bbox.min.x);

            for (var y = 0; y < h; ++y)
            {
                rasterTranslucentPixel32(d, s, (uint)w, 255);
                s += buffer0.stride;
                d += cmp.image.stride;
            }

            return true;
        }

        // =====================================================================
        //  Fill
        // =====================================================================

        public static void effectFillUpdate(RenderEffectFill p)
        {
            p.valid = true;
        }

        public static bool effectFill(SwCompositor cmp, RenderEffectFill p, bool direct)
        {
            var opacity = direct ? MULTIPLY(p.color[3], cmp.opacity) : (int)p.color[3];

            var bbox = cmp.bbox;
            var w = bbox.max.x - bbox.min.x;
            var h = bbox.max.y - bbox.min.y;
            var color = cmp.recoverSfc!.join!(p.color[0], p.color[1], p.color[2], 255);

            if (direct)
            {
                var dbuffer = cmp.recoverSfc.buf32 + (bbox.min.y * cmp.recoverSfc.stride + bbox.min.x);
                var sbuffer = cmp.image.buf32 + (bbox.min.y * cmp.image.stride + bbox.min.x);
                for (int y = 0; y < h; ++y)
                {
                    var dst = dbuffer;
                    var src = sbuffer;
                    for (int x = 0; x < w; ++x, ++dst, ++src)
                    {
                        var a = (byte)MULTIPLY(opacity, A(*src));
                        var tmp = ALPHA_BLEND(color, a);
                        *dst = tmp + ALPHA_BLEND(*dst, (uint)(255 - a));
                    }
                    dbuffer += cmp.image.stride;
                    sbuffer += cmp.recoverSfc.stride;
                }
                cmp.valid = true;
            }
            else
            {
                var dbuffer = cmp.image.buf32 + (bbox.min.y * cmp.image.stride + bbox.min.x);
                for (int y = 0; y < h; ++y)
                {
                    var dst = dbuffer;
                    for (int x = 0; x < w; ++x, ++dst)
                    {
                        *dst = ALPHA_BLEND(color, (uint)MULTIPLY(opacity, A(*dst)));
                    }
                    dbuffer += cmp.image.stride;
                }
            }
            return true;
        }

        // =====================================================================
        //  Tint
        // =====================================================================

        public static void effectTintUpdate(RenderEffectTint p)
        {
            p.valid = (p.intensity > 0);
        }

        public static bool effectTint(SwCompositor cmp, RenderEffectTint p, bool direct)
        {
            var bbox = cmp.bbox;
            var w = bbox.max.x - bbox.min.x;
            var h = bbox.max.y - bbox.min.y;
            var black = cmp.recoverSfc!.join!(p.black[0], p.black[1], p.black[2], 255);
            var white = cmp.recoverSfc.join(p.white[0], p.white[1], p.white[2], 255);
            var opacity = cmp.opacity;
            var luma = cmp.recoverSfc.alphas[2]; // luma function

            if (direct)
            {
                var dbuffer = cmp.recoverSfc.buf32 + (bbox.min.y * cmp.recoverSfc.stride + bbox.min.x);
                var sbuffer = cmp.image.buf32 + (bbox.min.y * cmp.image.stride + bbox.min.x);
                for (int y = 0; y < h; ++y)
                {
                    var dst = dbuffer;
                    var src = sbuffer;
                    for (int x = 0; x < w; ++x, ++dst, ++src)
                    {
                        var val = INTERPOLATE(white, black, luma!((byte*)src));
                        if (p.intensity < 255) val = INTERPOLATE(val, *src, p.intensity);
                        *dst = INTERPOLATE(val, *dst, (byte)MULTIPLY(opacity, A(*src)));
                    }
                    dbuffer += cmp.image.stride;
                    sbuffer += cmp.recoverSfc.stride;
                }
                cmp.valid = true;
            }
            else
            {
                var dbuffer = cmp.image.buf32 + (bbox.min.y * cmp.image.stride + bbox.min.x);
                for (int y = 0; y < h; ++y)
                {
                    var dst = dbuffer;
                    for (int x = 0; x < w; ++x, ++dst)
                    {
                        var val = INTERPOLATE(white, black, luma!((byte*)dst));
                        if (p.intensity < 255) val = INTERPOLATE(val, *dst, p.intensity);
                        *dst = ALPHA_BLEND(val, (uint)MULTIPLY(opacity, A(*dst)));
                    }
                    dbuffer += cmp.image.stride;
                }
            }

            return true;
        }

        // =====================================================================
        //  Tritone
        // =====================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint _tritone(uint s, uint m, uint h, int l)
        {
            if (l < 128)
            {
                var a = (uint)Math.Min(l * 2, 255);
                return ALPHA_BLEND(s, 255 - a) + ALPHA_BLEND(m, a);
            }
            else
            {
                var a = (uint)(2 * Math.Max(0, l - 128));
                return ALPHA_BLEND(m, 255 - a) + ALPHA_BLEND(h, a);
            }
        }

        public static void effectTritoneUpdate(RenderEffectTritone p)
        {
            p.valid = (p.blender < 255);
        }

        public static bool effectTritone(SwCompositor cmp, RenderEffectTritone p, bool direct)
        {
            var bbox = cmp.bbox;
            var w = bbox.max.x - bbox.min.x;
            var h = bbox.max.y - bbox.min.y;
            var shadow = cmp.recoverSfc!.join!(p.shadow[0], p.shadow[1], p.shadow[2], 255);
            var midtone = cmp.recoverSfc.join(p.midtone[0], p.midtone[1], p.midtone[2], 255);
            var highlight = cmp.recoverSfc.join(p.highlight[0], p.highlight[1], p.highlight[2], 255);
            var opacity = cmp.opacity;
            var luma = cmp.recoverSfc.alphas[2]; // luma function

            if (direct)
            {
                var dbuffer = cmp.recoverSfc.buf32 + (bbox.min.y * cmp.recoverSfc.stride + bbox.min.x);
                var sbuffer = cmp.image.buf32 + (bbox.min.y * cmp.image.stride + bbox.min.x);
                for (int y = 0; y < h; ++y)
                {
                    var dst = dbuffer;
                    var src = sbuffer;
                    if (p.blender == 0)
                    {
                        for (int x = 0; x < w; ++x, ++dst, ++src)
                        {
                            *dst = INTERPOLATE(_tritone(shadow, midtone, highlight, luma!((byte*)src)), *dst, (byte)MULTIPLY(opacity, A(*src)));
                        }
                    }
                    else
                    {
                        for (int x = 0; x < w; ++x, ++dst, ++src)
                        {
                            *dst = INTERPOLATE(INTERPOLATE(*src, _tritone(shadow, midtone, highlight, luma!((byte*)src)), p.blender), *dst, (byte)MULTIPLY(opacity, A(*src)));
                        }
                    }
                    dbuffer += cmp.image.stride;
                    sbuffer += cmp.recoverSfc.stride;
                }
                cmp.valid = true;
            }
            else
            {
                var dbuffer = cmp.image.buf32 + (bbox.min.y * cmp.image.stride + bbox.min.x);
                for (int y = 0; y < h; ++y)
                {
                    var dst = dbuffer;
                    if (p.blender == 0)
                    {
                        for (int x = 0; x < w; ++x, ++dst)
                        {
                            *dst = ALPHA_BLEND(_tritone(shadow, midtone, highlight, luma!((byte*)dst)), (uint)MULTIPLY(A(*dst), opacity));
                        }
                    }
                    else
                    {
                        for (int x = 0; x < w; ++x, ++dst)
                        {
                            *dst = ALPHA_BLEND(INTERPOLATE(*dst, _tritone(shadow, midtone, highlight, luma!((byte*)dst)), p.blender), (uint)MULTIPLY(A(*dst), opacity));
                        }
                    }
                    dbuffer += cmp.image.stride;
                }
            }

            return true;
        }
    }
}
