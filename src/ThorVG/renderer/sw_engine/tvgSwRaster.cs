// Ported from ThorVG/src/renderer/sw_engine/tvgSwRaster.cpp

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using static ThorVG.SwHelper;
using static ThorVG.SwBlendOps;
using static ThorVG.SwFillOps;

namespace ThorVG
{
    // Delegate for the gradient fill dispatch (replaces C++ template<typename fillMethod>)
    internal unsafe delegate void GradientFillRect32(SwFill fill, uint* dst, uint y, uint x, uint len, SwBlenderA op, byte a);
    internal unsafe delegate void GradientFillRect8(SwFill fill, byte* dst, uint y, uint x, uint len, SwMask op, byte a);
    internal unsafe delegate void GradientFillMatted(SwFill fill, uint* dst, uint y, uint x, uint len, byte* cmp, SwAlpha alpha, byte csize, byte opacity);
    internal unsafe delegate void GradientFillMaskedComposite(SwFill fill, byte* dst, uint y, uint x, uint len, SwMask op, byte a);
    internal unsafe delegate void GradientFillMaskedDirect(SwFill fill, byte* dst, uint y, uint x, uint len, byte* cmp, SwMask op, byte a);
    internal unsafe delegate void GradientFillBlending(SwFill fill, uint* dst, uint y, uint x, uint len, SwBlenderA op, SwBlender op2, byte a);

    public static unsafe partial class SwRaster
    {
        private const float DOWN_SCALE_TOLERANCE = 0.5f;

        // =====================================================================
        //  Alpha helpers
        // =====================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte _alpha(byte* a) => *a;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte _ialpha(byte* a) => (byte)~(*a);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte _abgrLuma(byte* c)
        {
            var v = *(uint*)c;
            return (byte)((((v & 0xff) * 54) + (((v >> 8) & 0xff) * 182) + (((v >> 16) & 0xff) * 19)) >> 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte _argbLuma(byte* c)
        {
            var v = *(uint*)c;
            return (byte)((((v & 0xff) * 19) + (((v >> 8) & 0xff) * 182) + (((v >> 16) & 0xff) * 54)) >> 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte _abgrInvLuma(byte* c) => (byte)~_abgrLuma(c);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte _argbInvLuma(byte* c) => (byte)~_argbLuma(c);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint _abgrJoin(byte r, byte g, byte b, byte a) => ((uint)a << 24) | ((uint)b << 16) | ((uint)g << 8) | r;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint _argbJoin(byte r, byte g, byte b, byte a) => ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;

        // =====================================================================
        //  Compositing / blending helpers
        // =====================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool _blending(SwSurface surface) => surface.blender != null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool _compositing(SwSurface surface)
        {
            if (surface.compositor == null || surface.compositor.method == MaskMethod.None) return false;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool _matting(SwSurface surface)
        {
            return (int)surface.compositor!.method < (int)MaskMethod.Add;
        }

        // =====================================================================
        //  Mask operations
        // =====================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte _opMaskNone(byte s, byte d, byte a) => s;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte _opMaskAdd(byte s, byte d, byte a) => (byte)(s + MULTIPLY(d, a));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte _opMaskSubtract(byte s, byte d, byte a) => (byte)MULTIPLY(s, 255 - d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte _opMaskIntersect(byte s, byte d, byte a) => (byte)MULTIPLY(s, d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte _opMaskDifference(byte s, byte d, byte a) => (byte)(MULTIPLY(s, 255 - d) + MULTIPLY(d, a));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte _opMaskLighten(byte s, byte d, byte a) => (s > d) ? s : d;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte _opMaskDarken(byte s, byte d, byte a) => (s < d) ? s : d;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool _direct(MaskMethod method)
        {
            return method == MaskMethod.Subtract || method == MaskMethod.Intersect || method == MaskMethod.Darken;
        }

        private static SwMask? _getMaskOp(MaskMethod method)
        {
            return method switch
            {
                MaskMethod.Add => _opMaskAdd,
                MaskMethod.Subtract => _opMaskSubtract,
                MaskMethod.Difference => _opMaskDifference,
                MaskMethod.Intersect => _opMaskIntersect,
                MaskMethod.Lighten => _opMaskLighten,
                MaskMethod.Darken => _opMaskDarken,
                _ => null
            };
        }

        // =====================================================================
        //  Composite mask image
        // =====================================================================

        private static bool _compositeMaskImage(SwSurface surface, SwImage image, in RenderRegion bbox)
        {
            var dbuffer = &surface.buf8[bbox.min.y * surface.stride + bbox.min.x];
            var sbuffer = image.buf8 + (bbox.min.y + image.oy) * image.stride + (bbox.min.x + image.ox);

            for (var y = bbox.min.y; y < bbox.max.y; ++y)
            {
                var dst = dbuffer;
                var src = sbuffer;
                for (var x = bbox.min.x; x < bbox.max.x; x++, dst++, src++)
                {
                    *dst = (byte)(*src + MULTIPLY(*dst, ~*src));
                }
                dbuffer += surface.stride;
                sbuffer += image.stride;
            }
            return true;
        }

        // =====================================================================
        //  Scaled image helpers
        // =====================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint _sampleSize(float scale)
        {
            var sampleSize = (uint)(0.5f / scale);
            if (sampleSize == 0) sampleSize = 1;
            return sampleSize;
        }

        // Nearest-neighbor interpolation
        private static uint _interpNoScaler(uint* img, uint stride, uint w, uint h, float sx, float sy, int miny, int maxy, int n)
        {
            return img[(uint)sx + (uint)sy * stride];
        }

        // Bilinear interpolation (upscaler)
        private static uint _interpUpScaler(uint* img, uint stride, uint w, uint h, float sx, float sy, int miny, int maxy, int n)
        {
            var rx = (uint)sx;
            var ry = (uint)sy;
            var rx2 = rx + 1;
            if (rx2 >= w) rx2 = w - 1;
            var ry2 = ry + 1;
            if (ry2 >= h) ry2 = h - 1;

            var dx = (sx > 0.0f) ? (byte)((sx - rx) * 255.0f) : (byte)0;
            var dy = (sy > 0.0f) ? (byte)((sy - ry) * 255.0f) : (byte)0;

            var c1 = img[rx + ry * stride];
            var c2 = img[rx2 + ry * stride];
            var c3 = img[rx + ry2 * stride];
            var c4 = img[rx2 + ry2 * stride];

            return INTERPOLATE(INTERPOLATE(c4, c3, dx), INTERPOLATE(c2, c1, dx), dy);
        }

        // 2n x 2n Mean Kernel (downscaler)
        private static uint _interpDownScaler(uint* img, uint stride, uint w, uint h, float sx, float sy, int miny, int maxy, int n)
        {
            long c0 = 0, c1 = 0, c2 = 0, c3 = 0;

            var minx = (int)sx - n;
            if (minx < 0) minx = 0;

            var maxx = (int)sx + n;
            if (maxx >= (int)w) maxx = (int)w;

            var inc = (n / 2) + 1;
            n = 0;

            var src = img + minx + miny * stride;

            for (var y = miny; y < maxy; y += inc)
            {
                var p = src;
                for (var x = minx; x < maxx; x += inc, p += inc)
                {
                    c0 += A(*p);
                    c1 += C1(*p);
                    c2 += C2(*p);
                    c3 += C3(*p);
                    ++n;
                }
                src += (stride * (uint)inc);
            }

            if (n > 0) { c0 /= n; c1 /= n; c2 /= n; c3 /= n; }

            return (uint)((c0 << 24) | (c1 << 16) | (c2 << 8) | c3);
        }

        // Scale method selector
        private delegate uint ScaleMethod(uint* img, uint stride, uint w, uint h, float sx, float sy, int miny, int maxy, int n);

        private static ScaleMethod _scaleMethod(SwImage image)
        {
            if (image.filter == FilterMethod.Bilinear) return image.scale < DOWN_SCALE_TOLERANCE ? _interpDownScaler : _interpUpScaler;
            return _interpNoScaler;
        }

        // =====================================================================
        //  Rect
        // =====================================================================

        private static bool _rasterCompositeMaskedRect(SwSurface surface, in RenderRegion bbox, SwMask maskOp, byte a)
        {
            var cstride = surface.compositor!.image.stride;
            var cbuffer = surface.compositor.image.buf8 + (bbox.min.y * cstride + bbox.min.x);
            var ialpha = (byte)(255 - a);

            for (uint y = 0; y < bbox.H(); ++y)
            {
                var cmp = cbuffer;
                for (uint x = 0; x < bbox.W(); ++x, ++cmp)
                {
                    *cmp = maskOp(a, *cmp, ialpha);
                }
                cbuffer += cstride;
            }
            return _compositeMaskImage(surface, surface.compositor.image, surface.compositor.bbox);
        }

        private static bool _rasterDirectMaskedRect(SwSurface surface, in RenderRegion bbox, SwMask maskOp, byte a)
        {
            var cbuffer = surface.compositor!.image.buf8 + (bbox.min.y * surface.compositor.image.stride + bbox.min.x);
            var dbuffer = surface.buf8 + (bbox.min.y * surface.stride + bbox.min.x);

            for (uint y = 0; y < bbox.H(); ++y)
            {
                var cmp = cbuffer;
                var dst = dbuffer;
                for (uint x = 0; x < bbox.W(); ++x, ++cmp, ++dst)
                {
                    var tmp = maskOp(a, *cmp, 0);
                    *dst = (byte)(tmp + MULTIPLY(*dst, ~tmp));
                }
                cbuffer += surface.compositor.image.stride;
                dbuffer += surface.stride;
            }
            return true;
        }

        private static bool _rasterMaskedRect(SwSurface surface, in RenderRegion bbox, in RenderColor c)
        {
            if (surface.channelSize != sizeof(byte)) return false;

            var maskOp = _getMaskOp(surface.compositor!.method);
            if (maskOp == null) return false;
            if (_direct(surface.compositor.method)) return _rasterDirectMaskedRect(surface, bbox, maskOp, c.a);
            else return _rasterCompositeMaskedRect(surface, bbox, maskOp, c.a);
        }

        private static bool _rasterMattedRect(SwSurface surface, in RenderRegion bbox, in RenderColor c)
        {
            var csize = surface.compositor!.image.channelSize;
            var cbuffer = surface.compositor.image.buf8 + ((bbox.min.y * surface.compositor.image.stride + bbox.min.x) * csize);
            var alpha = surface.Alpha(surface.compositor.method);

            // 32bit channels
            if (surface.channelSize == sizeof(uint))
            {
                var color = surface.join!(c.r, c.g, c.b, c.a);
                var buffer = surface.buf32 + (bbox.min.y * surface.stride) + bbox.min.x;
                for (uint y = 0; y < bbox.H(); ++y)
                {
                    var dst = &buffer[y * surface.stride];
                    var cmp = &cbuffer[y * surface.compositor.image.stride * csize];
                    for (uint x = 0; x < bbox.W(); ++x, ++dst, cmp += csize)
                    {
                        var tmp = ALPHA_BLEND(color, alpha!(cmp));
                        *dst = tmp + ALPHA_BLEND(*dst, IA(tmp));
                    }
                }
            }
            // 8bit grayscale
            else if (surface.channelSize == sizeof(byte))
            {
                var buffer = surface.buf8 + (bbox.min.y * surface.stride) + bbox.min.x;
                for (uint y = 0; y < bbox.H(); ++y)
                {
                    var dst = &buffer[y * surface.stride];
                    var cmp = &cbuffer[y * surface.compositor.image.stride * csize];
                    for (uint x = 0; x < bbox.W(); ++x, ++dst, cmp += csize)
                    {
                        *dst = INTERPOLATE8(c.a, *dst, alpha!(cmp));
                    }
                }
            }
            return true;
        }

        private static bool _rasterBlendingRect(SwSurface surface, in RenderRegion bbox, in RenderColor c)
        {
            if (surface.channelSize != sizeof(uint)) return false;

            var color = surface.join!(c.r, c.g, c.b, c.a);
            var buffer = surface.buf32 + (bbox.min.y * surface.stride) + bbox.min.x;

            for (uint y = 0; y < bbox.H(); ++y)
            {
                var dst = &buffer[y * surface.stride];
                for (uint x = 0; x < bbox.W(); ++x, ++dst)
                {
                    *dst = surface.blender!(color, *dst);
                }
            }
            return true;
        }

        private static bool _rasterTranslucentRect(SwSurface surface, in RenderRegion bbox, in RenderColor c)
        {
#if ENABLE_SIMD
            if (Sse2.IsSupported)
                return avxRasterTranslucentRect(surface, bbox, c);
            if (AdvSimd.IsSupported)
                return neonRasterTranslucentRect(surface, bbox, c);
#endif
            return cRasterTranslucentRect(surface, bbox, c);
        }

        private static bool _rasterSolidRect(SwSurface surface, in RenderRegion bbox, in RenderColor c)
        {
            // 32bit channels
            if (surface.channelSize == sizeof(uint))
            {
                var color = surface.join!(c.r, c.g, c.b, 255);
                var buffer = surface.buf32 + (bbox.min.y * surface.stride);
                for (uint y = 0; y < bbox.H(); ++y)
                {
                    rasterPixel32(buffer + y * surface.stride, color, (uint)bbox.min.x, (int)bbox.W());
                }
                return true;
            }
            // 8bit grayscale
            if (surface.channelSize == sizeof(byte))
            {
                for (uint y = 0; y < bbox.H(); ++y)
                {
                    rasterGrayscale8(surface.buf8, 255, (y + (uint)bbox.min.y) * surface.stride + (uint)bbox.min.x, (int)bbox.W());
                }
                return true;
            }
            return false;
        }

        private static bool _rasterRect(SwSurface surface, in RenderRegion bbox, in RenderColor c)
        {
            if (_compositing(surface))
            {
                if (_matting(surface)) return _rasterMattedRect(surface, bbox, c);
                else return _rasterMaskedRect(surface, bbox, c);
            }
            else if (_blending(surface))
            {
                return _rasterBlendingRect(surface, bbox, c);
            }
            else
            {
                if (c.a == 255) return _rasterSolidRect(surface, bbox, c);
                else return _rasterTranslucentRect(surface, bbox, c);
            }
        }

        // =====================================================================
        //  RLE
        // =====================================================================

        private static bool _rasterCompositeMaskedRle(SwSurface surface, SwRle* rle, in RenderRegion bbox, SwMask maskOp, byte a)
        {
            var cbuffer = surface.compositor!.image.buf8;
            var cstride = surface.compositor.image.stride;
            SwSpan* end;
            int x, len;
            byte src;

            for (var span = rle->Fetch(bbox, out end); span < end; ++span)
            {
                if (!span->Fetch(bbox, out x, out len)) continue;
                var cmp = &cbuffer[span->y * cstride + x];
                if (span->coverage == 255) src = a;
                else src = (byte)MULTIPLY(a, span->coverage);
                var ialpha = (byte)(255 - src);
                for (var xi = 0; xi < len; ++xi, ++cmp)
                {
                    *cmp = maskOp(src, *cmp, ialpha);
                }
            }
            return _compositeMaskImage(surface, surface.compositor.image, surface.compositor.bbox);
        }

        private static bool _rasterDirectMaskedRle(SwSurface surface, SwRle* rle, in RenderRegion bbox, SwMask maskOp, byte a)
        {
            var cbuffer = surface.compositor!.image.buf8;
            var cstride = surface.compositor.image.stride;
            SwSpan* end;
            int x, len;
            byte src;

            for (var span = rle->Fetch(bbox, out end); span < end; ++span)
            {
                if (!span->Fetch(bbox, out x, out len)) continue;
                var cmp = &cbuffer[span->y * cstride + x];
                var dst = &surface.buf8[span->y * surface.stride + x];
                if (span->coverage == 255) src = a;
                else src = (byte)MULTIPLY(a, span->coverage);
                for (var xi = 0; xi < len; ++xi, ++cmp, ++dst)
                {
                    var tmp = maskOp(src, *cmp, 0);
                    *dst = (byte)(tmp + MULTIPLY(*dst, ~tmp));
                }
            }
            return true;
        }

        private static bool _rasterMaskedRle(SwSurface surface, SwRle* rle, in RenderRegion bbox, in RenderColor c)
        {
            if (surface.channelSize != sizeof(byte)) return false;

            var maskOp = _getMaskOp(surface.compositor!.method);
            if (maskOp == null) return false;
            if (_direct(surface.compositor.method)) return _rasterDirectMaskedRle(surface, rle, bbox, maskOp, c.a);
            else return _rasterCompositeMaskedRle(surface, rle, bbox, maskOp, c.a);
        }

        private static bool _rasterMattedRle(SwSurface surface, SwRle* rle, in RenderRegion bbox, in RenderColor c)
        {
            var cbuffer = surface.compositor!.image.buf8;
            var csize = surface.compositor.image.channelSize;
            var alpha = surface.Alpha(surface.compositor.method);
            SwSpan* end;
            int x, len;

            // 32bit channels
            if (surface.channelSize == sizeof(uint))
            {
                uint src;
                var color = surface.join!(c.r, c.g, c.b, c.a);
                for (var span = rle->Fetch(bbox, out end); span < end; ++span)
                {
                    if (!span->Fetch(bbox, out x, out len)) continue;
                    var dst = &surface.buf32[span->y * surface.stride + x];
                    var cmp = &cbuffer[(span->y * surface.compositor.image.stride + x) * csize];
                    if (span->coverage == 255) src = color;
                    else src = ALPHA_BLEND(color, span->coverage);
                    for (var xi = 0; xi < len; ++xi, ++dst, cmp += csize)
                    {
                        var tmp = ALPHA_BLEND(src, alpha!(cmp));
                        *dst = tmp + ALPHA_BLEND(*dst, IA(tmp));
                    }
                }
            }
            // 8bit grayscale
            else if (surface.channelSize == sizeof(byte))
            {
                byte src;
                for (var span = rle->Fetch(bbox, out end); span < end; ++span)
                {
                    if (!span->Fetch(bbox, out x, out len)) continue;
                    var dst = &surface.buf8[span->y * surface.stride + x];
                    var cmp = &cbuffer[(span->y * surface.compositor.image.stride + x) * csize];
                    if (span->coverage == 255) src = c.a;
                    else src = (byte)MULTIPLY(c.a, span->coverage);
                    for (var xi = 0; xi < len; ++xi, ++dst, cmp += csize)
                    {
                        *dst = INTERPOLATE8(src, *dst, alpha!(cmp));
                    }
                }
            }
            return true;
        }

        private static bool _rasterBlendingRle(SwSurface surface, SwRle* rle, in RenderRegion bbox, in RenderColor c)
        {
            if (surface.channelSize != sizeof(uint)) return false;

            var color = surface.join!(c.r, c.g, c.b, c.a);
            SwSpan* end;
            int x, len;

            for (var span = rle->Fetch(bbox, out end); span < end; ++span)
            {
                if (!span->Fetch(bbox, out x, out len)) continue;
                var dst = &surface.buf32[span->y * surface.stride + x];
                if (span->coverage == 255)
                {
                    for (var xi = 0; xi < len; ++xi, ++dst)
                    {
                        *dst = surface.blender!(color, *dst);
                    }
                }
                else
                {
                    for (var xi = 0; xi < len; ++xi, ++dst)
                    {
                        *dst = INTERPOLATE(surface.blender!(color, *dst), *dst, span->coverage);
                    }
                }
            }
            return true;
        }

        private static bool _rasterTranslucentRle(SwSurface surface, SwRle* rle, in RenderRegion bbox, in RenderColor c)
        {
#if ENABLE_SIMD
            if (Sse2.IsSupported)
                return avxRasterTranslucentRle(surface, rle, bbox, c);
            if (AdvSimd.IsSupported)
                return neonRasterTranslucentRle(surface, rle, bbox, c);
#endif
            return cRasterTranslucentRle(surface, rle, bbox, c);
        }

        private static bool _rasterSolidRle(SwSurface surface, SwRle* rle, in RenderRegion bbox, in RenderColor c)
        {
            SwSpan* end;
            int x, len;

            // 32bit channels
            if (surface.channelSize == sizeof(uint))
            {
                var color = surface.join!(c.r, c.g, c.b, 255);
                for (var span = rle->Fetch(bbox, out end); span < end; ++span)
                {
                    if (!span->Fetch(bbox, out x, out len)) continue;
                    if (span->coverage == 255)
                    {
                        rasterPixel32(surface.buf32 + span->y * surface.stride, color, (uint)x, len);
                    }
                    else
                    {
                        var dst = &surface.buf32[span->y * surface.stride + x];
                        var src = ALPHA_BLEND(color, span->coverage);
                        var ialpha = (byte)(255 - span->coverage);
                        for (var xi = 0; xi < len; ++xi, ++dst)
                        {
                            *dst = src + ALPHA_BLEND(*dst, ialpha);
                        }
                    }
                }
            }
            // 8bit grayscale
            else if (surface.channelSize == sizeof(byte))
            {
                for (var span = rle->Fetch(bbox, out end); span < end; ++span)
                {
                    if (!span->Fetch(bbox, out x, out len)) continue;
                    if (span->coverage == 255)
                    {
                        rasterGrayscale8(surface.buf8, span->coverage, (uint)(span->y * surface.stride + x), len);
                    }
                    else
                    {
                        var dst = &surface.buf8[span->y * surface.stride + x];
                        var ialpha = (byte)(255 - span->coverage);
                        for (var xi = 0; xi < len; ++xi, ++dst)
                        {
                            *dst = (byte)(span->coverage + MULTIPLY(*dst, ialpha));
                        }
                    }
                }
            }
            return true;
        }

        private static bool _rasterRle(SwSurface surface, SwRle* rle, in RenderRegion bbox, in RenderColor c)
        {
            if (rle == null || rle->Invalid()) return false;

            if (_compositing(surface))
            {
                if (_matting(surface)) return _rasterMattedRle(surface, rle, bbox, c);
                else return _rasterMaskedRle(surface, rle, bbox, c);
            }
            else if (_blending(surface))
            {
                return _rasterBlendingRle(surface, rle, bbox, c);
            }
            else
            {
                if (c.a == 255) return _rasterSolidRle(surface, rle, bbox, c);
                else return _rasterTranslucentRle(surface, rle, bbox, c);
            }
        }

        // =====================================================================
        //  RLE Scaled Image
        // =====================================================================

        private static bool _rasterScaledMaskedRleImage(SwSurface surface, SwImage image, in Matrix itransform, in RenderRegion bbox, byte opacity)
        {
            // Not supported
            return false;
        }

        private static bool _rasterScaledMattedRleImage(SwSurface surface, SwImage image, in Matrix itransform, in RenderRegion bbox, byte opacity)
        {
            var csize = surface.compositor!.image.channelSize;
            var alpha = surface.Alpha(surface.compositor.method);
            ScaleMethod scaleMethod = _scaleMethod(image);
            var sampleSize = (int)_sampleSize(image.scale);
            int miny = 0, maxy = 0;
            var rle = image.rle;

            for (uint i = 0; i < rle.spans.count; ++i)
            {
                var span = &rle.spans.data[i];
                // SCALED_IMAGE_RANGE_Y
                var sy = span->y * itransform.e22 + itransform.e23 - 0.49f;
                if (sy <= -0.5f || (uint)(sy + 0.5f) >= image.h) continue;
                if (scaleMethod == (ScaleMethod)_interpDownScaler)
                {
                    var my = (int)MathF.Round(sy, MidpointRounding.ToEven);
                    miny = my - sampleSize; if (miny < 0) miny = 0;
                    maxy = my + sampleSize; if (maxy >= (int)image.h) maxy = (int)image.h;
                }

                var dst = &surface.buf32[span->y * surface.stride + span->x];
                var cmp = &surface.compositor.image.buf8[(span->y * surface.compositor.image.stride + span->x) * csize];
                var a = (byte)MULTIPLY(span->coverage, opacity);
                for (uint x = span->x; x < span->x + span->len; ++x, ++dst, cmp += csize)
                {
                    var sx = x * itransform.e11 + itransform.e13 - 0.49f;
                    if (sx <= -0.5f || (uint)(sx + 0.5f) >= image.w) continue;
                    var src = scaleMethod(image.buf32, image.stride, image.w, image.h, sx, sy, miny, maxy, sampleSize);
                    src = ALPHA_BLEND(src, (a == 255) ? alpha!(cmp) : (byte)MULTIPLY(alpha!(cmp), a));
                    *dst = src + ALPHA_BLEND(*dst, IA(src));
                }
            }
            return true;
        }

        private static bool _rasterScaledBlendingRleImage(SwSurface surface, SwImage image, in Matrix itransform, in RenderRegion bbox, byte opacity)
        {
            ScaleMethod scaleMethod = _scaleMethod(image);
            var sampleSize = (int)_sampleSize(image.scale);
            int miny = 0, maxy = 0;
            var rle = image.rle;

            for (uint i = 0; i < rle.spans.count; ++i)
            {
                var span = &rle.spans.data[i];
                var sy = span->y * itransform.e22 + itransform.e23 - 0.49f;
                if (sy <= -0.5f || (uint)(sy + 0.5f) >= image.h) continue;
                if (scaleMethod == (ScaleMethod)_interpDownScaler)
                {
                    var my = (int)MathF.Round(sy, MidpointRounding.ToEven);
                    miny = my - sampleSize; if (miny < 0) miny = 0;
                    maxy = my + sampleSize; if (maxy >= (int)image.h) maxy = (int)image.h;
                }

                var dst = &surface.buf32[span->y * surface.stride + span->x];
                var a = (byte)MULTIPLY(span->coverage, opacity);
                if (a == 255)
                {
                    for (uint x = span->x; x < span->x + span->len; ++x, ++dst)
                    {
                        var sx = x * itransform.e11 + itransform.e13 - 0.49f;
                        if (sx <= -0.5f || (uint)(sx + 0.5f) >= image.w) continue;
                        var src = scaleMethod(image.buf32, image.stride, image.w, image.h, sx, sy, miny, maxy, sampleSize);
                        *dst = INTERPOLATE(surface.blender!(rasterUnpremultiply(src), *dst), *dst, A(src));
                    }
                }
                else
                {
                    for (uint x = span->x; x < span->x + span->len; ++x, ++dst)
                    {
                        var sx = x * itransform.e11 + itransform.e13 - 0.49f;
                        if (sx <= -0.5f || (uint)(sx + 0.5f) >= image.w) continue;
                        var src = scaleMethod(image.buf32, image.stride, image.w, image.h, sx, sy, miny, maxy, sampleSize);
                        *dst = INTERPOLATE(surface.blender!(rasterUnpremultiply(src), *dst), *dst, (byte)MULTIPLY(a, A(src)));
                    }
                }
            }
            return true;
        }

        private static bool _rasterScaledRleImage(SwSurface surface, SwImage image, in Matrix itransform, in RenderRegion bbox, byte opacity)
        {
            ScaleMethod scaleMethod = _scaleMethod(image);
            var sampleSize = (int)_sampleSize(image.scale);
            int miny = 0, maxy = 0;
            var rle = image.rle;

            for (uint i = 0; i < rle.spans.count; ++i)
            {
                var span = &rle.spans.data[i];
                var sy = span->y * itransform.e22 + itransform.e23 - 0.49f;
                if (sy <= -0.5f || (uint)(sy + 0.5f) >= image.h) continue;
                if (scaleMethod == (ScaleMethod)_interpDownScaler)
                {
                    var my = (int)MathF.Round(sy, MidpointRounding.ToEven);
                    miny = my - sampleSize; if (miny < 0) miny = 0;
                    maxy = my + sampleSize; if (maxy >= (int)image.h) maxy = (int)image.h;
                }

                var dst = &surface.buf32[span->y * surface.stride + span->x];
                var a = (byte)MULTIPLY(span->coverage, opacity);
                for (uint x = span->x; x < span->x + span->len; ++x, ++dst)
                {
                    var sx = x * itransform.e11 + itransform.e13 - 0.49f;
                    if (sx <= -0.5f || (uint)(sx + 0.5f) >= image.w) continue;
                    var src = scaleMethod(image.buf32, image.stride, image.w, image.h, sx, sy, miny, maxy, sampleSize);
                    if (a < 255) src = ALPHA_BLEND(src, a);
                    *dst = src + ALPHA_BLEND(*dst, IA(src));
                }
            }
            return true;
        }

        // =====================================================================
        //  RLE Direct Image
        // =====================================================================

        private static bool _rasterDirectMattedRleImage(SwSurface surface, SwImage image, in RenderRegion bbox, byte opacity)
        {
            var csize = surface.compositor!.image.channelSize;
            var cbuffer = surface.compositor.image.buf8;
            var alpha = surface.Alpha(surface.compositor.method);
            SwSpan* end;
            int x, len;
            var rle = image.rle;

            for (var span = rle.Fetch(bbox, out end); span < end; ++span)
            {
                if (!span->Fetch(bbox, out x, out len)) continue;
                var dst = &surface.buf32[span->y * surface.stride + x];
                var cmp = &cbuffer[(span->y * surface.compositor.image.stride + x) * csize];
                var img = image.buf32 + (span->y + image.oy) * image.stride + (x + image.ox);
                var a = (byte)MULTIPLY(span->coverage, opacity);
                if (a == 255)
                {
                    for (var xi = 0; xi < len; ++xi, ++dst, ++img, cmp += csize)
                    {
                        var tmp = ALPHA_BLEND(*img, alpha!(cmp));
                        *dst = tmp + ALPHA_BLEND(*dst, IA(tmp));
                    }
                }
                else
                {
                    for (var xi = 0; xi < len; ++xi, ++dst, ++img, cmp += csize)
                    {
                        var tmp = ALPHA_BLEND(*img, (uint)MULTIPLY(a, alpha!(cmp)));
                        *dst = tmp + ALPHA_BLEND(*dst, IA(tmp));
                    }
                }
            }
            return true;
        }

        private static bool _rasterDirectBlendingRleImage(SwSurface surface, SwImage image, in RenderRegion bbox, byte opacity)
        {
            SwSpan* end;
            int x, len;
            var rle = image.rle;

            for (var span = rle.Fetch(bbox, out end); span < end; ++span)
            {
                if (!span->Fetch(bbox, out x, out len)) continue;
                var dst = &surface.buf32[span->y * surface.stride + x];
                var src = image.buf32 + (span->y + image.oy) * image.stride + (x + image.ox);
                var a = (byte)MULTIPLY(span->coverage, opacity);
                if (a == 255)
                {
                    for (var xi = 0; xi < len; ++xi, ++dst, ++src)
                    {
                        *dst = surface.blender!(rasterUnpremultiply(*src), *dst);
                    }
                }
                else
                {
                    for (var xi = 0; xi < len; ++xi, ++dst, ++src)
                    {
                        *dst = INTERPOLATE(surface.blender!(rasterUnpremultiply(*src), *dst), *dst, (byte)MULTIPLY(a, A(*src)));
                    }
                }
            }
            return true;
        }

        private static bool _rasterDirectRleImage(SwSurface surface, SwImage image, in RenderRegion bbox, byte opacity)
        {
            SwSpan* end;
            int x, len;
            var rle = image.rle;

            for (var span = rle.Fetch(bbox, out end); span < end; ++span)
            {
                if (!span->Fetch(bbox, out x, out len)) continue;
                var dst = &surface.buf32[span->y * surface.stride + x];
                var img = image.buf32 + (span->y + image.oy) * image.stride + (x + image.ox);
                var a = (byte)MULTIPLY(span->coverage, opacity);
                rasterTranslucentPixel32(dst, img, (uint)len, a);
            }
            return true;
        }

        private static bool _rasterDirectMaskedRleImage(SwSurface surface, SwImage image, in RenderRegion bbox, byte opacity)
        {
            // Not supported
            return false;
        }

        // =====================================================================
        //  Scaled Image
        // =====================================================================

        private static bool _rasterScaledMaskedImage(SwSurface surface, SwImage image, in Matrix itransform, in RenderRegion bbox, byte opacity)
        {
            // Not supported
            return false;
        }

        private static bool _rasterScaledMattedImage(SwSurface surface, SwImage image, in Matrix itransform, in RenderRegion bbox, byte opacity)
        {
            if (surface.channelSize == sizeof(byte)) return false;

            var dbuffer = surface.buf32 + (bbox.min.y * surface.stride + bbox.min.x);
            var csize = surface.compositor!.image.channelSize;
            var cbuffer = surface.compositor.image.buf8 + (bbox.min.y * surface.compositor.image.stride + bbox.min.x) * csize;
            var alpha = surface.Alpha(surface.compositor.method);

            ScaleMethod scaleMethod = _scaleMethod(image);
            var sampleSize = (int)_sampleSize(image.scale);
            int miny = 0, maxy = 0;

            for (var y = bbox.min.y; y < bbox.max.y; ++y)
            {
                var sy = y * itransform.e22 + itransform.e23 - 0.49f;
                if (sy <= -0.5f || (uint)(sy + 0.5f) >= image.h) { dbuffer += surface.stride; cbuffer += surface.compositor.image.stride * csize; continue; }
                if (scaleMethod == (ScaleMethod)_interpDownScaler)
                {
                    var my = (int)MathF.Round(sy, MidpointRounding.ToEven);
                    miny = my - sampleSize; if (miny < 0) miny = 0;
                    maxy = my + sampleSize; if (maxy >= (int)image.h) maxy = (int)image.h;
                }
                var dst = dbuffer;
                var cmp = cbuffer;
                for (var x = bbox.min.x; x < bbox.max.x; ++x, ++dst, cmp += csize)
                {
                    var sx = x * itransform.e11 + itransform.e13 - 0.49f;
                    if (sx <= -0.5f || (uint)(sx + 0.5f) >= image.w) continue;
                    var src = scaleMethod(image.buf32, image.stride, image.w, image.h, sx, sy, miny, maxy, sampleSize);
                    var tmp = ALPHA_BLEND(src, opacity == 255 ? alpha!(cmp) : (byte)MULTIPLY(opacity, alpha!(cmp)));
                    *dst = tmp + ALPHA_BLEND(*dst, IA(tmp));
                }
                dbuffer += surface.stride;
                cbuffer += surface.compositor.image.stride * csize;
            }
            return true;
        }

        private static bool _rasterScaledBlendingImage(SwSurface surface, SwImage image, in Matrix itransform, in RenderRegion bbox, byte opacity)
        {
            if (surface.channelSize == sizeof(byte)) return false;

            var dbuffer = surface.buf32 + (bbox.min.y * surface.stride + bbox.min.x);
            ScaleMethod scaleMethod = _scaleMethod(image);
            var sampleSize = (int)_sampleSize(image.scale);
            int miny = 0, maxy = 0;

            for (var y = bbox.min.y; y < bbox.max.y; ++y, dbuffer += surface.stride)
            {
                var sy = y * itransform.e22 + itransform.e23 - 0.49f;
                if (sy <= -0.5f || (uint)(sy + 0.5f) >= image.h) continue;
                if (scaleMethod == (ScaleMethod)_interpDownScaler)
                {
                    var my = (int)MathF.Round(sy, MidpointRounding.ToEven);
                    miny = my - sampleSize; if (miny < 0) miny = 0;
                    maxy = my + sampleSize; if (maxy >= (int)image.h) maxy = (int)image.h;
                }
                var dst = dbuffer;
                for (var x = bbox.min.x; x < bbox.max.x; ++x, ++dst)
                {
                    var sx = x * itransform.e11 + itransform.e13 - 0.49f;
                    if (sx <= -0.5f || (uint)(sx + 0.5f) >= image.w) continue;
                    var src = scaleMethod(image.buf32, image.stride, image.w, image.h, sx, sy, miny, maxy, sampleSize);
                    *dst = INTERPOLATE(surface.blender!(rasterUnpremultiply(src), *dst), *dst, (byte)MULTIPLY(opacity, A(src)));
                }
            }
            return true;
        }

        private static bool _rasterScaledImage(SwSurface surface, SwImage image, in Matrix itransform, in RenderRegion bbox, byte opacity)
        {
            ScaleMethod scaleMethod = _scaleMethod(image);
            var sampleSize = (int)_sampleSize(image.scale);
            int miny = 0, maxy = 0;

            // 32bit channels
            if (surface.channelSize == sizeof(uint))
            {
                var buffer = surface.buf32 + (bbox.min.y * surface.stride + bbox.min.x);
                for (var y = bbox.min.y; y < bbox.max.y; ++y, buffer += surface.stride)
                {
                    var sy = y * itransform.e22 + itransform.e23 - 0.49f;
                    if (sy <= -0.5f || (uint)(sy + 0.5f) >= image.h) continue;
                    if (scaleMethod == (ScaleMethod)_interpDownScaler)
                    {
                        var my = (int)MathF.Round(sy, MidpointRounding.ToEven);
                        miny = my - sampleSize; if (miny < 0) miny = 0;
                        maxy = my + sampleSize; if (maxy >= (int)image.h) maxy = (int)image.h;
                    }
                    var dst = buffer;
                    for (var x = bbox.min.x; x < bbox.max.x; ++x, ++dst)
                    {
                        var sx = x * itransform.e11 + itransform.e13 - 0.49f;
                        if (sx <= -0.5f || (uint)(sx + 0.5f) >= image.w) continue;
                        var src = scaleMethod(image.buf32, image.stride, image.w, image.h, sx, sy, miny, maxy, sampleSize);
                        if (opacity < 255) src = ALPHA_BLEND(src, opacity);
                        *dst = src + ALPHA_BLEND(*dst, IA(src));
                    }
                }
            }
            else if (surface.channelSize == sizeof(byte))
            {
                var buffer = surface.buf8 + (bbox.min.y * surface.stride + bbox.min.x);
                for (var y = bbox.min.y; y < bbox.max.y; ++y, buffer += surface.stride)
                {
                    var sy = y * itransform.e22 + itransform.e23 - 0.49f;
                    if (sy <= -0.5f || (uint)(sy + 0.5f) >= image.h) continue;
                    if (scaleMethod == (ScaleMethod)_interpDownScaler)
                    {
                        var my = (int)MathF.Round(sy, MidpointRounding.ToEven);
                        miny = my - sampleSize; if (miny < 0) miny = 0;
                        maxy = my + sampleSize; if (maxy >= (int)image.h) maxy = (int)image.h;
                    }
                    var dst = buffer;
                    for (var x = bbox.min.x; x < bbox.max.x; ++x, ++dst)
                    {
                        var sx = x * itransform.e11 + itransform.e13 - 0.49f;
                        if (sx <= -0.5f || (uint)(sx + 0.5f) >= image.w) continue;
                        var src = scaleMethod(image.buf32, image.stride, image.w, image.h, sx, sy, miny, maxy, sampleSize);
                        *dst = (byte)MULTIPLY(A(src), opacity);
                    }
                }
            }
            return true;
        }

        // =====================================================================
        //  Direct Image
        // =====================================================================

        private static bool _rasterDirectMaskedImage(SwSurface surface, SwImage image, in RenderRegion bbox, int w, int h, byte opacity)
        {
            // Not supported
            return false;
        }

        private static bool _rasterDirectMattedImage(SwSurface surface, SwImage image, in RenderRegion bbox, int w, int h, byte opacity)
        {
            var csize = surface.compositor!.image.channelSize;
            var alpha = surface.Alpha(surface.compositor.method);
            var sbuffer = image.buf32 + (bbox.min.y + image.oy) * image.stride + (bbox.min.x + image.ox);
            var cbuffer = surface.compositor.image.buf8 + (bbox.min.y * surface.compositor.image.stride + bbox.min.x) * csize;

            // 32 bits
            if (surface.channelSize == sizeof(uint))
            {
                var dbuffer = surface.buf32 + (bbox.min.y * surface.stride) + bbox.min.x;
                for (var y = 0; y < h; ++y, dbuffer += surface.stride, sbuffer += image.stride)
                {
                    var cmp = cbuffer;
                    var src = sbuffer;
                    if (opacity == 255)
                    {
                        for (var dst = dbuffer; dst < dbuffer + w; ++dst, ++src, cmp += csize)
                        {
                            var tmp = ALPHA_BLEND(*src, alpha!(cmp));
                            *dst = tmp + ALPHA_BLEND(*dst, IA(tmp));
                        }
                    }
                    else
                    {
                        for (var dst = dbuffer; dst < dbuffer + w; ++dst, ++src, cmp += csize)
                        {
                            var tmp = ALPHA_BLEND(*src, (uint)MULTIPLY(opacity, alpha!(cmp)));
                            *dst = tmp + ALPHA_BLEND(*dst, IA(tmp));
                        }
                    }
                    cbuffer += surface.compositor.image.stride * csize;
                }
            }
            // 8 bits
            else if (surface.channelSize == sizeof(byte))
            {
                var dbuffer = surface.buf8 + (bbox.min.y * surface.stride) + bbox.min.x;
                for (var y = 0; y < h; ++y, dbuffer += surface.stride, sbuffer += image.stride)
                {
                    var cmp = cbuffer;
                    var src = sbuffer;
                    if (opacity == 255)
                    {
                        for (var dst = dbuffer; dst < dbuffer + w; ++dst, ++src, cmp += csize)
                        {
                            var tmp = (byte)MULTIPLY(A(*src), alpha!(cmp));
                            *dst = (byte)(tmp + MULTIPLY(*dst, 255 - tmp));
                        }
                    }
                    else
                    {
                        for (var dst = dbuffer; dst < dbuffer + w; ++dst, ++src, cmp += csize)
                        {
                            var tmp = (byte)MULTIPLY(A(*src), MULTIPLY(opacity, alpha!(cmp)));
                            *dst = (byte)(tmp + MULTIPLY(*dst, 255 - tmp));
                        }
                    }
                    cbuffer += surface.compositor.image.stride * csize;
                }
            }
            return true;
        }

        private static bool _rasterDirectBlendingImage(SwSurface surface, SwImage image, in RenderRegion bbox, int w, int h, byte opacity)
        {
            if (surface.channelSize == sizeof(byte)) return false;

            // fast-track: mix the blending & masking composition
            static bool injecting(SwCompositor? cmp)
            {
                return cmp != null && cmp.recoverCmp != null && cmp.recoverCmp.method != MaskMethod.None && _matting(cmp.recoverSfc!) && !_blending(cmp.recoverSfc!);
            }

            if (injecting(surface.compositor)) return _rasterDirectMattedBlendingImage(surface, image, surface.compositor!.recoverCmp!, bbox, w, h, opacity);

            var dbuffer = &surface.buf32[bbox.min.y * surface.stride + bbox.min.x];
            var sbuffer = image.buf32 + (bbox.min.y + image.oy) * image.stride + (bbox.min.x + image.ox);

            for (var y = 0; y < h; ++y, dbuffer += surface.stride, sbuffer += image.stride)
            {
                var src = sbuffer;
                if (opacity == 255)
                {
                    for (var dst = dbuffer; dst < dbuffer + w; dst++, src++)
                    {
                        *dst = INTERPOLATE(surface.blender!(rasterUnpremultiply(*src), *dst), *dst, A(*src));
                    }
                }
                else
                {
                    for (var dst = dbuffer; dst < dbuffer + w; dst++, src++)
                    {
                        *dst = INTERPOLATE(surface.blender!(rasterUnpremultiply(*src), *dst), *dst, (byte)MULTIPLY(opacity, A(*src)));
                    }
                }
            }
            return true;
        }

        private static bool _rasterDirectImage(SwSurface surface, SwImage image, in RenderRegion bbox, int w, int h, byte opacity)
        {
            var sbuffer = image.buf32 + (bbox.min.y + image.oy) * image.stride + (bbox.min.x + image.ox);

            // 32bit channels
            if (surface.channelSize == sizeof(uint))
            {
                var dbuffer = &surface.buf32[bbox.min.y * surface.stride + bbox.min.x];
                for (var y = 0; y < h; ++y, dbuffer += surface.stride, sbuffer += image.stride)
                {
                    rasterTranslucentPixel32(dbuffer, sbuffer, (uint)w, opacity);
                }
            }
            // 8bit grayscale
            else if (surface.channelSize == sizeof(byte))
            {
                var dbuffer = &surface.buf8[bbox.min.y * surface.stride + bbox.min.x];
                for (var y = 0; y < h; ++y, dbuffer += surface.stride, sbuffer += image.stride)
                {
                    var src = sbuffer;
                    if (opacity == 255)
                    {
                        for (var dst = dbuffer; dst < dbuffer + w; dst++, src++)
                        {
                            *dst = (byte)(A(*src) + MULTIPLY(*dst, IA(*src)));
                        }
                    }
                    else
                    {
                        for (var dst = dbuffer; dst < dbuffer + w; dst++, src++)
                        {
                            *dst = INTERPOLATE8(A(*src), *dst, opacity);
                        }
                    }
                }
            }
            return true;
        }

        private static bool _rasterDirectMattedBlendingImage(SwSurface surface, SwImage image, SwCompositor compositor, in RenderRegion bbox, int w, int h, byte opacity)
        {
            if (surface.channelSize == sizeof(byte)) return false;

            var csize = compositor.image.channelSize;
            var alpha = surface.Alpha(compositor.method);
            var sbuffer = image.buf32 + (bbox.min.y + image.oy) * image.stride + (bbox.min.x + image.ox);
            var cbuffer = compositor.image.buf8 + (bbox.min.y * compositor.image.stride + bbox.min.x) * csize;
            var dbuffer = surface.buf32 + (bbox.min.y * surface.stride) + bbox.min.x;

            for (var y = 0; y < h; ++y, dbuffer += surface.stride, sbuffer += image.stride)
            {
                var cmp = cbuffer;
                var src = sbuffer;
                if (opacity == 255)
                {
                    for (var dst = dbuffer; dst < dbuffer + w; ++dst, ++src, cmp += csize)
                    {
                        *dst = INTERPOLATE(surface.blender!(*src, *dst), *dst, (byte)MULTIPLY(A(*src), alpha!(cmp)));
                    }
                }
                else
                {
                    for (var dst = dbuffer; dst < dbuffer + w; ++dst, ++src, cmp += csize)
                    {
                        *dst = INTERPOLATE(surface.blender!(*src, *dst), *dst, (byte)MULTIPLY(MULTIPLY(A(*src), alpha!(cmp)), opacity));
                    }
                }
                cbuffer += compositor.image.stride * csize;
            }
            return true;
        }

        // =====================================================================
        //  Rect Gradient  (replaces C++ template<typename fillMethod>)
        // =====================================================================

        private static bool _rasterCompositeGradientMaskedRect(SwSurface surface, in RenderRegion bbox, SwFill fill, SwMask maskOp, GradientFillMaskedComposite fillFn)
        {
            var cstride = surface.compositor!.image.stride;
            var cbuffer = surface.compositor.image.buf8 + (bbox.min.y * cstride + bbox.min.x);

            for (uint y = 0; y < bbox.H(); ++y)
            {
                fillFn(fill, cbuffer, (uint)bbox.min.y + y, (uint)bbox.min.x, bbox.W(), maskOp, 255);
                cbuffer += surface.stride;
            }
            return _compositeMaskImage(surface, surface.compositor.image, surface.compositor.bbox);
        }

        private static bool _rasterDirectGradientMaskedRect(SwSurface surface, in RenderRegion bbox, SwFill fill, SwMask maskOp, GradientFillMaskedDirect fillFn)
        {
            var cstride = surface.compositor!.image.stride;
            var cbuffer = surface.compositor.image.buf8 + (bbox.min.y * cstride + bbox.min.x);
            var dbuffer = surface.buf8 + (bbox.min.y * surface.stride + bbox.min.x);

            for (uint y = 0; y < bbox.H(); ++y)
            {
                fillFn(fill, dbuffer, (uint)bbox.min.y + y, (uint)bbox.min.x, bbox.W(), cbuffer, maskOp, 255);
                cbuffer += cstride;
                dbuffer += surface.stride;
            }
            return true;
        }

        private static bool _rasterGradientMaskedRect(SwSurface surface, in RenderRegion bbox, SwFill fill, GradientFillMaskedComposite compositeFn, GradientFillMaskedDirect directFn)
        {
            var method = surface.compositor!.method;
            var maskOp = _getMaskOp(method);
            if (maskOp == null) return false;

            if (_direct(method)) return _rasterDirectGradientMaskedRect(surface, bbox, fill, maskOp, directFn);
            else return _rasterCompositeGradientMaskedRect(surface, bbox, fill, maskOp, compositeFn);
        }

        private static bool _rasterGradientMattedRect(SwSurface surface, in RenderRegion bbox, SwFill fill, GradientFillMatted fillFn)
        {
            var buffer = surface.buf32 + (bbox.min.y * surface.stride) + bbox.min.x;
            var csize = surface.compositor!.image.channelSize;
            var cbuffer = surface.compositor.image.buf8 + (bbox.min.y * surface.compositor.image.stride + bbox.min.x) * csize;
            var alpha = surface.Alpha(surface.compositor.method);

            for (uint y = 0; y < bbox.H(); ++y)
            {
                fillFn(fill, buffer, (uint)bbox.min.y + y, (uint)bbox.min.x, bbox.W(), cbuffer, alpha!, csize, 255);
                buffer += surface.stride;
                cbuffer += surface.stride * csize;
            }
            return true;
        }

        private static bool _rasterBlendingGradientRect(SwSurface surface, in RenderRegion bbox, SwFill fill, GradientFillBlending fillFn)
        {
            var buffer = surface.buf32 + (bbox.min.y * surface.stride) + bbox.min.x;

            if (fill.translucent)
            {
                for (uint y = 0; y < bbox.H(); ++y)
                {
                    fillFn(fill, buffer + y * surface.stride, (uint)bbox.min.y + y, (uint)bbox.min.x, bbox.W(), opBlendPreNormal, surface.blender!, 255);
                }
            }
            else
            {
                for (uint y = 0; y < bbox.H(); ++y)
                {
                    fillFn(fill, buffer + y * surface.stride, (uint)bbox.min.y + y, (uint)bbox.min.x, bbox.W(), opBlendSrcOver, surface.blender!, 255);
                }
            }
            return true;
        }

        private static bool _rasterTranslucentGradientRect<TGrad>(SwSurface surface, in RenderRegion bbox, SwFill fill) where TGrad : struct, IGradientFill
        {
            // 32 bits
            if (surface.channelSize == sizeof(uint))
            {
                var buffer = surface.buf32 + (bbox.min.y * surface.stride) + bbox.min.x;
                for (uint y = 0; y < bbox.H(); ++y)
                {
                    TGrad.Fill<BlendPreNormal>(fill, buffer, (uint)bbox.min.y + y, (uint)bbox.min.x, bbox.W(), 255);
                    buffer += surface.stride;
                }
            }
            // 8 bits
            else if (surface.channelSize == sizeof(byte))
            {
                var buffer = surface.buf8 + (bbox.min.y * surface.stride) + bbox.min.x;
                for (uint y = 0; y < bbox.H(); ++y)
                {
                    TGrad.FillMask<MaskAdd>(fill, buffer, (uint)bbox.min.y + y, (uint)bbox.min.x, bbox.W(), 255);
                    buffer += surface.stride;
                }
            }
            return true;
        }

        private static bool _rasterSolidGradientRect<TGrad>(SwSurface surface, in RenderRegion bbox, SwFill fill) where TGrad : struct, IGradientFill
        {
            // 32 bits
            if (surface.channelSize == sizeof(uint))
            {
                var buffer = surface.buf32 + (bbox.min.y * surface.stride) + bbox.min.x;
                for (uint y = 0; y < bbox.H(); ++y)
                {
                    TGrad.Fill<BlendSrcOver>(fill, buffer, (uint)bbox.min.y + y, (uint)bbox.min.x, bbox.W(), 255);
                    buffer += surface.stride;
                }
            }
            // 8 bits
            else if (surface.channelSize == sizeof(byte))
            {
                var buffer = surface.buf8 + (bbox.min.y * surface.stride) + bbox.min.x;
                for (uint y = 0; y < bbox.H(); ++y)
                {
                    TGrad.FillMask<MaskNone>(fill, buffer, (uint)bbox.min.y + y, (uint)bbox.min.x, bbox.W(), 255);
                    buffer += surface.stride;
                }
            }
            return true;
        }

        private static bool _rasterLinearGradientRect(SwSurface surface, in RenderRegion bbox, SwFill fill)
        {
            if (_compositing(surface))
            {
                if (_matting(surface)) return _rasterGradientMattedRect(surface, bbox, fill, fillLinear);
                else return _rasterGradientMaskedRect(surface, bbox, fill,
                    (SwFill f, byte* d, uint y, uint x, uint l, SwMask op, byte a) => fillLinear(f, d, y, x, l, op, a),
                    (SwFill f, byte* d, uint y, uint x, uint l, byte* cmp, SwMask op, byte a) => fillLinear(f, d, y, x, l, cmp, op, a));
            }
            else if (_blending(surface))
            {
                return _rasterBlendingGradientRect(surface, bbox, fill, fillLinear);
            }
            else
            {
                if (fill.translucent) return _rasterTranslucentGradientRect<LinearGrad>(surface, bbox, fill);
                else return _rasterSolidGradientRect<LinearGrad>(surface, bbox, fill);
            }
        }

        private static bool _rasterRadialGradientRect(SwSurface surface, in RenderRegion bbox, SwFill fill)
        {
            if (_compositing(surface))
            {
                if (_matting(surface)) return _rasterGradientMattedRect(surface, bbox, fill, fillRadial);
                else return _rasterGradientMaskedRect(surface, bbox, fill,
                    (SwFill f, byte* d, uint y, uint x, uint l, SwMask op, byte a) => fillRadial(f, d, y, x, l, op, a),
                    (SwFill f, byte* d, uint y, uint x, uint l, byte* cmp, SwMask op, byte a) => fillRadial(f, d, y, x, l, cmp, op, a));
            }
            else if (_blending(surface))
            {
                return _rasterBlendingGradientRect(surface, bbox, fill, fillRadial);
            }
            else
            {
                if (fill.translucent) return _rasterTranslucentGradientRect<RadialGrad>(surface, bbox, fill);
                else return _rasterSolidGradientRect<RadialGrad>(surface, bbox, fill);
            }
        }

        // =====================================================================
        //  RLE Gradient
        // =====================================================================

        private static bool _rasterCompositeGradientMaskedRle(SwSurface surface, SwRle rle, SwFill fill, SwMask maskOp, GradientFillMaskedComposite fillFn)
        {
            var span = rle.Data();
            var cstride = surface.compositor!.image.stride;
            var cbuffer = surface.compositor.image.buf8;

            for (uint i = 0; i < rle.Size(); ++i, ++span)
            {
                var cmp = &cbuffer[span->y * cstride + span->x];
                fillFn(fill, cmp, (uint)span->y, span->x, span->len, maskOp, span->coverage);
            }
            return _compositeMaskImage(surface, surface.compositor.image, surface.compositor.bbox);
        }

        private static bool _rasterDirectGradientMaskedRle(SwSurface surface, SwRle rle, SwFill fill, SwMask maskOp, GradientFillMaskedDirect fillFn)
        {
            var span = rle.Data();
            var cstride = surface.compositor!.image.stride;
            var cbuffer = surface.compositor.image.buf8;
            var dbuffer = surface.buf8;

            for (uint i = 0; i < rle.Size(); ++i, ++span)
            {
                var cmp = &cbuffer[span->y * cstride + span->x];
                var dst = &dbuffer[span->y * surface.stride + span->x];
                fillFn(fill, dst, (uint)span->y, span->x, span->len, cmp, maskOp, span->coverage);
            }
            return true;
        }

        private static bool _rasterGradientMaskedRle(SwSurface surface, SwRle rle, SwFill fill, GradientFillMaskedComposite compositeFn, GradientFillMaskedDirect directFn)
        {
            var method = surface.compositor!.method;
            var maskOp = _getMaskOp(method);
            if (maskOp == null) return false;

            if (_direct(method)) return _rasterDirectGradientMaskedRle(surface, rle, fill, maskOp, directFn);
            else return _rasterCompositeGradientMaskedRle(surface, rle, fill, maskOp, compositeFn);
        }

        private static bool _rasterGradientMattedRle(SwSurface surface, SwRle rle, SwFill fill, GradientFillMatted fillFn)
        {
            var span = rle.Data();
            var csize = surface.compositor!.image.channelSize;
            var cbuffer = surface.compositor.image.buf8;
            var alpha = surface.Alpha(surface.compositor.method);

            for (uint i = 0; i < rle.Size(); ++i, ++span)
            {
                var dst = &surface.buf32[span->y * surface.stride + span->x];
                var cmp = &cbuffer[(span->y * surface.compositor.image.stride + span->x) * csize];
                fillFn(fill, dst, (uint)span->y, span->x, span->len, cmp, alpha!, csize, span->coverage);
            }
            return true;
        }

        private static bool _rasterBlendingGradientRle(SwSurface surface, SwRle rle, SwFill fill, GradientFillBlending fillFn)
        {
            var span = rle.Data();

            for (uint i = 0; i < rle.Size(); ++i, ++span)
            {
                var dst = &surface.buf32[span->y * surface.stride + span->x];
                fillFn(fill, dst, (uint)span->y, span->x, span->len, opBlendPreNormal, surface.blender!, span->coverage);
            }
            return true;
        }

        private static bool _rasterTranslucentGradientRle<TGrad>(SwSurface surface, SwRle rle, SwFill fill) where TGrad : struct, IGradientFill
        {
            var span = rle.Data();

            // 32 bits
            if (surface.channelSize == sizeof(uint))
            {
                for (uint i = 0; i < rle.Size(); ++i, ++span)
                {
                    var dst = &surface.buf32[span->y * surface.stride + span->x];
                    if (span->coverage == 255) TGrad.Fill<BlendPreNormal>(fill, dst, (uint)span->y, span->x, span->len, 255);
                    else TGrad.Fill<BlendNormal>(fill, dst, (uint)span->y, span->x, span->len, span->coverage);
                }
            }
            // 8 bits
            else if (surface.channelSize == sizeof(byte))
            {
                for (uint i = 0; i < rle.Size(); ++i, ++span)
                {
                    var dst = &surface.buf8[span->y * surface.stride + span->x];
                    TGrad.FillMask<MaskAdd>(fill, dst, (uint)span->y, span->x, span->len, span->coverage);
                }
            }
            return true;
        }

        private static bool _rasterSolidGradientRle<TGrad>(SwSurface surface, SwRle rle, SwFill fill) where TGrad : struct, IGradientFill
        {
            var span = rle.Data();

            // 32 bits
            if (surface.channelSize == sizeof(uint))
            {
                for (uint i = 0; i < rle.Size(); ++i, ++span)
                {
                    var dst = &surface.buf32[span->y * surface.stride + span->x];
                    if (span->coverage == 255) TGrad.Fill<BlendSrcOver>(fill, dst, (uint)span->y, span->x, span->len, 255);
                    else TGrad.Fill<BlendInterp>(fill, dst, (uint)span->y, span->x, span->len, span->coverage);
                }
            }
            // 8 bits
            else if (surface.channelSize == sizeof(byte))
            {
                for (uint i = 0; i < rle.Size(); ++i, ++span)
                {
                    var dst = &surface.buf8[span->y * surface.stride + span->x];
                    if (span->coverage == 255) TGrad.FillMask<MaskNone>(fill, dst, (uint)span->y, span->x, span->len, 255);
                    else TGrad.FillMask<MaskAdd>(fill, dst, (uint)span->y, span->x, span->len, span->coverage);
                }
            }
            return true;
        }

        private static bool _rasterLinearGradientRle(SwSurface surface, SwRle rle, SwFill fill)
        {
            if (_compositing(surface))
            {
                if (_matting(surface)) return _rasterGradientMattedRle(surface, rle, fill, fillLinear);
                else return _rasterGradientMaskedRle(surface, rle, fill,
                    (SwFill f, byte* d, uint y, uint x, uint l, SwMask op, byte a) => fillLinear(f, d, y, x, l, op, a),
                    (SwFill f, byte* d, uint y, uint x, uint l, byte* cmp, SwMask op, byte a) => fillLinear(f, d, y, x, l, cmp, op, a));
            }
            else if (_blending(surface))
            {
                return _rasterBlendingGradientRle(surface, rle, fill, fillLinear);
            }
            else
            {
                if (fill.translucent) return _rasterTranslucentGradientRle<LinearGrad>(surface, rle, fill);
                else return _rasterSolidGradientRle<LinearGrad>(surface, rle, fill);
            }
        }

        private static bool _rasterRadialGradientRle(SwSurface surface, SwRle rle, SwFill fill)
        {
            if (_compositing(surface))
            {
                if (_matting(surface)) return _rasterGradientMattedRle(surface, rle, fill, fillRadial);
                else return _rasterGradientMaskedRle(surface, rle, fill,
                    (SwFill f, byte* d, uint y, uint x, uint l, SwMask op, byte a) => fillRadial(f, d, y, x, l, op, a),
                    (SwFill f, byte* d, uint y, uint x, uint l, byte* cmp, SwMask op, byte a) => fillRadial(f, d, y, x, l, cmp, op, a));
            }
            else if (_blending(surface))
            {
                return _rasterBlendingGradientRle(surface, rle, fill, fillRadial);
            }
            else
            {
                if (fill.translucent) return _rasterTranslucentGradientRle<RadialGrad>(surface, rle, fill);
                else return _rasterSolidGradientRle<RadialGrad>(surface, rle, fill);
            }
        }

        // =====================================================================
        //  Public API
        // =====================================================================

        public static void rasterTranslucentPixel32(uint* dst, uint* src, uint len, byte opacity)
        {
            cRasterTranslucentPixels(dst, src, len, opacity);
        }

        public static void rasterPixel32(uint* dst, uint* src, uint len, byte opacity)
        {
            cRasterPixels(dst, src, len, opacity);
        }

        public static void rasterGrayscale8(byte* dst, byte val, uint offset, int len)
        {
#if ENABLE_SIMD
            if (Avx2.IsSupported)
                avxRasterGrayscale8(dst, val, offset, len);
            else if (AdvSimd.IsSupported)
                neonRasterGrayscale8(dst, val, offset, len);
            else
#endif
                cRasterPixels(dst, val, offset, len);
        }

        public static void rasterPixel32(uint* dst, uint val, uint offset, int len)
        {
#if ENABLE_SIMD
            if (Avx2.IsSupported)
                avxRasterPixel32(dst, val, offset, len);
            else if (AdvSimd.IsSupported)
                neonRasterPixel32(dst, val, offset, len);
            else
#endif
                cRasterPixels(dst, val, offset, len);
        }

        public static bool rasterCompositor(SwSurface surface)
        {
            // See MaskMethod: Alpha:1, InvAlpha:2, Luma:3, InvLuma:4
            surface.alphas[0] = _alpha;
            surface.alphas[1] = _ialpha;

            if (surface.cs == ColorSpace.ABGR8888 || surface.cs == ColorSpace.ABGR8888S)
            {
                surface.join = _abgrJoin;
                surface.alphas[2] = _abgrLuma;
                surface.alphas[3] = _abgrInvLuma;
            }
            else if (surface.cs == ColorSpace.ARGB8888 || surface.cs == ColorSpace.ARGB8888S)
            {
                surface.join = _argbJoin;
                surface.alphas[2] = _argbLuma;
                surface.alphas[3] = _argbInvLuma;
            }
            else
            {
                return false;
            }
            return true;
        }

        public static bool rasterClear(SwSurface surface, uint x, uint y, uint w, uint h)
        {
            if (surface == null || surface.buf32 == null || surface.stride == 0 || surface.w == 0 || surface.h == 0) return false;

            // 32 bits
            if (surface.channelSize == sizeof(uint))
            {
                uint val = 0;
                if (w == surface.stride)
                {
                    rasterPixel32(surface.buf32, val, surface.stride * y, (int)(w * h));
                }
                else
                {
                    for (uint i = 0; i < h; i++)
                    {
                        rasterPixel32(surface.buf32, val, (surface.stride * y + x) + (surface.stride * i), (int)w);
                    }
                }
            }
            // 8 bits
            else if (surface.channelSize == sizeof(byte))
            {
                if (w == surface.stride)
                {
                    rasterGrayscale8(surface.buf8, 0x00, surface.stride * y, (int)(w * h));
                }
                else
                {
                    for (uint i = 0; i < h; i++)
                    {
                        rasterGrayscale8(surface.buf8, 0x00, (surface.stride * y + x) + (surface.stride * i), (int)w);
                    }
                }
            }
            return true;
        }

        public static uint rasterUnpremultiply(uint data)
        {
            var a = A(data);
            if (a == 255 || a == 0) return data;

            byte r = (byte)Math.Min(C1(data) * 255u / a, 255u);
            byte g = (byte)Math.Min(C2(data) * 255u / a, 255u);
            byte b = (byte)Math.Min(C3(data) * 255u / a, 255u);

            return JOIN(a, r, g, b);
        }

        public static void rasterUnpremultiply(RenderSurface surface)
        {
            if (surface.channelSize != sizeof(uint)) return;

            for (uint y = 0; y < surface.h; y++)
            {
                var buffer = surface.buf32 + surface.stride * y;
                for (uint x = 0; x < surface.w; ++x)
                {
                    buffer[x] = rasterUnpremultiply(buffer[x]);
                }
            }
            surface.premultiplied = false;
        }

        public static void rasterPremultiply(RenderSurface surface)
        {
            using var lk = new ScopedLock(surface.key);
            if (surface.premultiplied || (surface.channelSize != sizeof(uint))) return;
            surface.premultiplied = true;

            var buffer = surface.buf32;
            for (uint y = 0; y < surface.h; ++y, buffer += surface.stride)
            {
                var dst = buffer;
                for (uint x = 0; x < surface.w; ++x, ++dst)
                {
                    var c = *dst;
                    if (A(c) == 255) continue;
                    *dst = PREMULTIPLY(c, A(c));
                }
            }
        }

        public static bool rasterScaledImage(SwSurface surface, SwImage image, in Matrix transform, in RenderRegion bbox, byte opacity)
        {
            if (!TvgMath.Inverse(transform, out var itransform)) return true;

            if (_compositing(surface))
            {
                if (_matting(surface)) return _rasterScaledMattedImage(surface, image, itransform, bbox, opacity);
                else return _rasterScaledMaskedImage(surface, image, itransform, bbox, opacity);
            }
            else if (_blending(surface))
            {
                return _rasterScaledBlendingImage(surface, image, itransform, bbox, opacity);
            }
            else
            {
                return _rasterScaledImage(surface, image, itransform, bbox, opacity);
            }
        }

        public static bool rasterDirectImage(SwSurface surface, SwImage image, in RenderRegion bbox, byte opacity)
        {
            var w = Math.Min(bbox.max.x - bbox.min.x, (int)image.w - (bbox.min.x + image.ox));
            var h = Math.Min(bbox.max.y - bbox.min.y, (int)image.h - (bbox.min.y + image.oy));

            if (_compositing(surface))
            {
                if (_matting(surface))
                {
                    if (_blending(surface)) return _rasterDirectMattedBlendingImage(surface, image, surface.compositor!, bbox, w, h, opacity);
                    else return _rasterDirectMattedImage(surface, image, bbox, w, h, opacity);
                }
                else return _rasterDirectMaskedImage(surface, image, bbox, w, h, opacity);
            }
            else if (_blending(surface))
            {
                return _rasterDirectBlendingImage(surface, image, bbox, w, h, opacity);
            }
            else
            {
                return _rasterDirectImage(surface, image, bbox, w, h, opacity);
            }
        }

        public static bool rasterScaledRleImage(SwSurface surface, SwImage image, in Matrix transform, in RenderRegion bbox, byte opacity)
        {
            if (surface.channelSize == sizeof(byte)) return false;

            if (!TvgMath.Inverse(transform, out var itransform)) return true;

            if (_compositing(surface))
            {
                if (_matting(surface)) return _rasterScaledMattedRleImage(surface, image, itransform, bbox, opacity);
                else return _rasterScaledMaskedRleImage(surface, image, itransform, bbox, opacity);
            }
            else if (_blending(surface))
            {
                return _rasterScaledBlendingRleImage(surface, image, itransform, bbox, opacity);
            }
            else
            {
                return _rasterScaledRleImage(surface, image, itransform, bbox, opacity);
            }
        }

        public static bool rasterDirectRleImage(SwSurface surface, SwImage image, in RenderRegion bbox, byte opacity)
        {
            if (surface.channelSize == sizeof(byte)) return false;

            if (_compositing(surface))
            {
                if (_matting(surface)) return _rasterDirectMattedRleImage(surface, image, bbox, opacity);
                else return _rasterDirectMaskedRleImage(surface, image, bbox, opacity);
            }
            else if (_blending(surface))
            {
                return _rasterDirectBlendingRleImage(surface, image, bbox, opacity);
            }
            else
            {
                return _rasterDirectRleImage(surface, image, bbox, opacity);
            }
        }

        public static bool rasterGradientShape(SwSurface surface, SwShape shape, in RenderRegion bbox, Fill fdata, byte opacity)
        {
            if (shape.fill == null) return false;

            var color = SwFillOps.fillFetchSolid(shape.fill, fdata);
            if (color.HasValue)
            {
                var a = (byte)MULTIPLY(color.Value.a, opacity);
                var c = new RenderColor(color.Value.r, color.Value.g, color.Value.b, a);
                return a > 0 ? rasterShape(surface, shape, bbox, c) : true;
            }

            var type = fdata.GetFillType();
            if (shape.fastTrack)
            {
                if (type == Type.LinearGradient) return _rasterLinearGradientRect(surface, bbox, shape.fill);
                else if (type == Type.RadialGradient) return _rasterRadialGradientRect(surface, bbox, shape.fill);
            }
            else if (shape.hasRle)
            {
                if (shape.rle.Valid())
                {
                    if (type == Type.LinearGradient) return _rasterLinearGradientRle(surface, shape.rle, shape.fill);
                    else if (type == Type.RadialGradient) return _rasterRadialGradientRle(surface, shape.rle, shape.fill);
                }
            }
            return false;
        }

        public static bool rasterGradientStroke(SwSurface surface, SwShape shape, in RenderRegion bbox, Fill fdata, byte opacity)
        {
            if (shape.stroke == null || shape.stroke.fill == null || !shape.hasStrokeRle || shape.strokeRle.Invalid()) return false;

            var color = SwFillOps.fillFetchSolid(shape.stroke.fill, fdata);
            if (color.HasValue)
            {
                var c = new RenderColor(color.Value.r, color.Value.g, color.Value.b, color.Value.a);
                c.a = (byte)MULTIPLY(c.a, opacity);
                return c.a > 0 ? rasterStroke(surface, shape, bbox, c) : true;
            }

            var rle = shape.strokeRle;
            var type = fdata.GetFillType();
            if (type == Type.LinearGradient) return _rasterLinearGradientRle(surface, rle, shape.stroke.fill);
            else if (type == Type.RadialGradient) return _rasterRadialGradientRle(surface, rle, shape.stroke.fill);
            return false;
        }

        public static bool rasterShape(SwSurface surface, SwShape shape, in RenderRegion bbox, RenderColor c)
        {
            if (c.a < 255)
            {
                c.r = (byte)MULTIPLY(c.r, c.a);
                c.g = (byte)MULTIPLY(c.g, c.a);
                c.b = (byte)MULTIPLY(c.b, c.a);
            }
            if (shape.fastTrack) return _rasterRect(surface, bbox, c);
            else
            {
                if (!shape.hasRle) return false;
                var rle = shape.rle;
                return _rasterRle(surface, &rle, bbox, c);
            }
        }

        public static bool rasterStroke(SwSurface surface, SwShape shape, in RenderRegion bbox, RenderColor c)
        {
            if (c.a < 255)
            {
                c.r = (byte)MULTIPLY(c.r, c.a);
                c.g = (byte)MULTIPLY(c.g, c.a);
                c.b = (byte)MULTIPLY(c.b, c.a);
            }

            if (!shape.hasStrokeRle) return false;
            var rle2 = shape.strokeRle;
            return _rasterRle(surface, &rle2, bbox, c);
        }

        public static bool rasterConvertCS(RenderSurface surface, ColorSpace to)
        {
            using var lk = new ScopedLock(surface.key);
            if (surface.cs == to) return true;

            var from = surface.cs;

            if (((from == ColorSpace.ABGR8888) || (from == ColorSpace.ABGR8888S)) && ((to == ColorSpace.ARGB8888) || (to == ColorSpace.ARGB8888S)))
            {
                surface.cs = to;
                return cRasterABGRtoARGB(surface);
            }
            if (((from == ColorSpace.ARGB8888) || (from == ColorSpace.ARGB8888S)) && ((to == ColorSpace.ABGR8888) || (to == ColorSpace.ABGR8888S)))
            {
                surface.cs = to;
                return cRasterARGBtoABGR(surface);
            }
            return false;
        }

        public static void rasterXYFlip(uint* src, uint* dst, int stride, int w, int h, in RenderRegion bbox, bool flipped)
        {
            const int BLOCK = 8;

            if (flipped)
            {
                src += ((bbox.min.x * stride) + bbox.min.y);
                dst += ((bbox.min.y * stride) + bbox.min.x);
            }
            else
            {
                src += ((bbox.min.y * stride) + bbox.min.x);
                dst += ((bbox.min.x * stride) + bbox.min.y);
            }

            for (int x = 0; x < w; x += BLOCK)
            {
                var bx = Math.Min(w, x + BLOCK) - x;
                var inPtr = &src[x];
                var outPtr = &dst[x * stride];
                for (int y = 0; y < h; y += BLOCK)
                {
                    var p = &inPtr[y * stride];
                    var q = &outPtr[y];
                    var by = Math.Min(h, y + BLOCK) - y;
                    for (int xx = 0; xx < bx; ++xx)
                    {
                        for (int yy = 0; yy < by; ++yy)
                        {
                            *q = *p;
                            p += stride;
                            ++q;
                        }
                        p += 1 - by * stride;
                        q += stride - by;
                    }
                }
            }
        }

        public static void rasterRGB2HSL(byte r, byte g, byte b, out float h, out float s, out float l)
        {
            // Delegates to SwHelper which already has this implementation
            SwHelper.rasterRGB2HSL(r, g, b, out h, out s, out l);
        }
    }
}
