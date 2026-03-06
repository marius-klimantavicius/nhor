// Ported from ThorVG/src/renderer/sw_engine/tvgSwRasterC.h

using System.Runtime.CompilerServices;
using static ThorVG.SwHelper;

namespace ThorVG
{
    public static unsafe partial class SwRaster
    {
        // =====================================================================
        //  cRasterTranslucentPixels (uint32)
        // =====================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void cRasterTranslucentPixels(uint* dst, uint* src, uint len, uint opacity)
        {
            if (opacity == 255)
            {
                for (uint x = 0; x < len; ++x, ++dst, ++src)
                {
                    *dst = *src + ALPHA_BLEND(*dst, IA(*src));
                }
            }
            else
            {
                for (uint x = 0; x < len; ++x, ++dst, ++src)
                {
                    var tmp = ALPHA_BLEND(*src, opacity);
                    *dst = tmp + ALPHA_BLEND(*dst, IA(tmp));
                }
            }
        }

        // =====================================================================
        //  cRasterPixels (uint32 src array)
        // =====================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void cRasterPixels(uint* dst, uint* src, uint len, uint opacity)
        {
            if (opacity == 255)
            {
                for (uint x = 0; x < len; ++x, ++dst, ++src)
                {
                    *dst = *src;
                }
            }
            else
            {
                cRasterTranslucentPixels(dst, src, len, opacity);
            }
        }

        // =====================================================================
        //  cRasterPixels (uint32 fill)
        // =====================================================================

        internal static void cRasterPixels(uint* dst, uint val, uint offset, int len)
        {
            dst += offset;

            // fix misaligned memory
            var alignOffset = (long)dst % 8;
            if (alignOffset > 0)
            {
                alignOffset /= 4; // sizeof(uint) == 4
                while (alignOffset > 0 && len > 0)
                {
                    *dst++ = val;
                    --len;
                    --alignOffset;
                }
            }

            // 64-bit faster fill
            var val64 = ((ulong)val << 32) | (ulong)val;
            while (len > 1)
            {
                *(ulong*)dst = val64;
                len -= 2;
                dst += 2;
            }

            // leftovers
            while (len-- > 0) *dst++ = val;
        }

        // =====================================================================
        //  cRasterPixels (byte fill)
        // =====================================================================

        internal static void cRasterPixels(byte* dst, byte val, uint offset, int len)
        {
            dst += offset;

            // fix misaligned memory
            var alignOffset = (long)dst % 8;
            if (alignOffset > 0)
            {
                alignOffset = 8 - alignOffset;
                while (alignOffset > 0 && len > 0)
                {
                    *dst++ = val;
                    --len;
                    --alignOffset;
                }
            }

            // 64-bit faster fill
            var val32 = ((uint)val << 24) | ((uint)val << 16) | ((uint)val << 8) | (uint)val;
            var val64 = ((ulong)val32 << 32) | val32;
            while (len > 7)
            {
                *(ulong*)dst = val64;
                len -= 8;
                dst += 8;
            }

            // leftovers
            while (len-- > 0) *dst++ = val;
        }

        // =====================================================================
        //  cRasterTranslucentRle
        // =====================================================================

        internal static bool cRasterTranslucentRle(SwSurface surface, SwRle* rle, in RenderRegion bbox, in RenderColor c)
        {
            SwSpan* end;
            int x, len;

            // 32bit channels
            if (surface.channelSize == sizeof(uint))
            {
                var color = surface.join!(c.r, c.g, c.b, c.a);
                uint src;
                for (var span = rle->Fetch(bbox, out end); span < end; ++span)
                {
                    if (!span->Fetch(bbox, out x, out len)) continue;
                    var dst = &surface.buf32[span->y * surface.stride + x];
                    if (span->coverage < 255) src = ALPHA_BLEND(color, span->coverage);
                    else src = color;
                    var ialpha = IA(src);
                    for (var xi = 0; xi < len; ++xi, ++dst)
                    {
                        *dst = src + ALPHA_BLEND(*dst, ialpha);
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
                    if (span->coverage < 255) src = (byte)MULTIPLY(span->coverage, c.a);
                    else src = c.a;
                    var ialpha = (byte)~c.a;
                    for (var xi = 0; xi < len; ++xi, ++dst)
                    {
                        *dst = (byte)(src + MULTIPLY(*dst, ialpha));
                    }
                }
            }
            return true;
        }

        // =====================================================================
        //  cRasterTranslucentRect
        // =====================================================================

        internal static bool cRasterTranslucentRect(SwSurface surface, in RenderRegion bbox, in RenderColor c)
        {
            // 32bit channels
            if (surface.channelSize == sizeof(uint))
            {
                var color = surface.join!(c.r, c.g, c.b, c.a);
                var buffer = surface.buf32 + (bbox.min.y * surface.stride) + bbox.min.x;
                var ialpha = (byte)(255 - c.a);
                for (uint y = 0; y < bbox.H(); ++y)
                {
                    var dst = &buffer[y * surface.stride];
                    for (uint xi = 0; xi < bbox.W(); ++xi, ++dst)
                    {
                        *dst = color + ALPHA_BLEND(*dst, ialpha);
                    }
                }
            }
            // 8bit grayscale
            else if (surface.channelSize == sizeof(byte))
            {
                var buffer = surface.buf8 + (bbox.min.y * surface.stride) + bbox.min.x;
                var ialpha = (byte)~c.a;
                for (uint y = 0; y < bbox.H(); ++y)
                {
                    var dst = &buffer[y * surface.stride];
                    for (uint xi = 0; xi < bbox.W(); ++xi, ++dst)
                    {
                        *dst = (byte)(c.a + MULTIPLY(*dst, ialpha));
                    }
                }
            }
            return true;
        }

        // =====================================================================
        //  cRasterABGRtoARGB / cRasterARGBtoABGR
        // =====================================================================

        internal static bool cRasterABGRtoARGB(RenderSurface surface)
        {
            // 64-bit faster converting
            if (surface.w % 2 == 0)
            {
                var buffer = (ulong*)surface.buf32;
                for (uint y = 0; y < surface.h; ++y, buffer += surface.stride / 2)
                {
                    var dst = buffer;
                    for (uint x = 0; x < surface.w / 2; ++x, ++dst)
                    {
                        var cv = *dst;
                        // flip Blue, Red channels
                        *dst = (cv & 0xff000000ff000000UL) + ((cv & 0x00ff000000ff0000UL) >> 16) + (cv & 0x0000ff000000ff00UL) + ((cv & 0x000000ff000000ffUL) << 16);
                    }
                }
            }
            // default converting
            else
            {
                var buffer = surface.buf32;
                for (uint y = 0; y < surface.h; ++y, buffer += surface.stride)
                {
                    var dst = buffer;
                    for (uint x = 0; x < surface.w; ++x, ++dst)
                    {
                        var cv = *dst;
                        // flip Blue, Red channels
                        *dst = (cv & 0xff000000u) + ((cv & 0x00ff0000u) >> 16) + (cv & 0x0000ff00u) + ((cv & 0x000000ffu) << 16);
                    }
                }
            }
            return true;
        }

        internal static bool cRasterARGBtoABGR(RenderSurface surface)
        {
            // exactly same as ABGRtoARGB
            return cRasterABGRtoARGB(surface);
        }
    }
}
