// Ported from ThorVG/src/renderer/sw_engine/tvgSwRasterNeon.h
// Direct port of ARM NEON intrinsics — same operations, same order as C++.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using static ThorVG.SwHelper;

namespace ThorVG
{
    public static unsafe partial class SwRaster
    {
        // =================================================================
        //  ALPHA_BLEND — NEON 64-bit (2 pixels / 8 bytes at once)
        //  Mirrors C++ ALPHA_BLEND(uint8x8_t c, uint8x8_t a)
        // =================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector64<byte> ALPHA_BLEND_NEON(Vector64<byte> c, Vector64<byte> a)
        {
            Vector128<ushort> t = AdvSimd.MultiplyWideningLower(c, a);
            return AdvSimd.ShiftRightLogicalNarrowingLower(t, 8);
        }

        // =================================================================
        //  neonRasterGrayscale8
        //  Mirrors C++ neonRasterGrayscale8(uint8_t* dst, uint8_t val,
        //                                    uint32_t offset, int32_t len)
        // =================================================================

        internal static void neonRasterGrayscale8(byte* dst, byte val, uint offset, int len)
        {
            dst += offset;

            int i = 0;
            var valVec = Vector128.Create(val);

            // On AArch64, C++ uses vst1q_u8_x4 to process 64 bytes at a time.
            // .NET doesn't have a direct x4 store, so we unroll 4x vst1q_u8.
            for (; i <= len - 16 * 4; i += 16 * 4)
            {
                AdvSimd.Store(dst + i, valVec);
                AdvSimd.Store(dst + i + 16, valVec);
                AdvSimd.Store(dst + i + 32, valVec);
                AdvSimd.Store(dst + i + 48, valVec);
            }

            for (; i <= len - 16; i += 16)
            {
                AdvSimd.Store(dst + i, valVec);
            }

            for (; i < len; i++)
            {
                dst[i] = val;
            }
        }

        // =================================================================
        //  neonRasterPixel32
        //  Mirrors C++ neonRasterPixel32(uint32_t *dst, uint32_t val,
        //                                 uint32_t offset, int32_t len)
        // =================================================================

        internal static void neonRasterPixel32(uint* dst, uint val, uint offset, int len)
        {
            dst += offset;

            var vectorVal = Vector128.Create(val);

            // AArch64 path: C++ uses vst4q_u32 for 16 elements at once.
            // .NET doesn't have vst4q_u32, so unroll 4x vst1q_u32.
            uint iterations = (uint)len / 16;
            uint neonFilled = iterations * 16;
            for (uint i = 0; i < iterations; ++i)
            {
                AdvSimd.Store(dst, vectorVal);
                AdvSimd.Store(dst + 4, vectorVal);
                AdvSimd.Store(dst + 8, vectorVal);
                AdvSimd.Store(dst + 12, vectorVal);
                dst += 16;
            }

            // Process remaining groups of 4
            int remaining = len - (int)neonFilled;
            while (remaining >= 4)
            {
                AdvSimd.Store(dst, vectorVal);
                dst += 4;
                remaining -= 4;
            }

            // Leftovers
            while (remaining-- > 0) *dst++ = val;
        }

        // =================================================================
        //  neonRasterTranslucentRle
        //  Mirrors C++ neonRasterTranslucentRle(SwSurface* surface,
        //            const SwRle* rle, const RenderRegion& bbox,
        //            const RenderColor& c)
        // =================================================================

        internal static bool neonRasterTranslucentRle(SwSurface surface, SwRle* rle, in RenderRegion bbox, in RenderColor c)
        {
            SwSpan* end;
            int x, len;

            //32bit channels
            if (surface.channelSize == sizeof(uint))
            {
                var color = surface.join!(c.r, c.g, c.b, c.a);
                uint src;
                int align;

                for (var span = rle->Fetch(bbox, out end); span < end; ++span)
                {
                    if (!span->Fetch(bbox, out x, out len)) continue;
                    if (span->coverage < 255) src = ALPHA_BLEND(color, span->coverage);
                    else src = color;

                    var dst = &surface.buf32[span->y * surface.stride + x];
                    var ialpha = IA(src);

                    Vector64<byte>* vDst;
                    if (((nuint)dst & 0x7) != 0)
                    {
                        //fill not aligned byte
                        *dst = src + ALPHA_BLEND(*dst, ialpha);
                        vDst = (Vector64<byte>*)(dst + 1);
                        align = 1;
                    }
                    else
                    {
                        vDst = (Vector64<byte>*)dst;
                        align = 0;
                    }

                    var vSrc = Vector64.Create(src).AsByte();
                    var vIalpha = Vector64.Create((byte)ialpha);

                    for (int xi = 0; xi < (len - align) / 2; ++xi)
                        vDst[xi] = AdvSimd.Add(vSrc, ALPHA_BLEND_NEON(vDst[xi], vIalpha));

                    var leftovers = (len - align) % 2;
                    if (leftovers > 0) dst[len - 1] = src + ALPHA_BLEND(dst[len - 1], ialpha);
                }
            }
            //8bit grayscale
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
                    for (int xi = 0; xi < len; ++xi, ++dst)
                    {
                        *dst = (byte)(src + MULTIPLY(*dst, ialpha));
                    }
                }
            }
            return true;
        }

        // =================================================================
        //  neonRasterTranslucentRect
        //  Mirrors C++ neonRasterTranslucentRect(SwSurface* surface,
        //            const RenderRegion& bbox, const RenderColor& c)
        // =================================================================

        internal static bool neonRasterTranslucentRect(SwSurface surface, in RenderRegion bbox, in RenderColor c)
        {
            var h = bbox.H();
            var w = bbox.W();

            //32bits channels
            if (surface.channelSize == sizeof(uint))
            {
                var color = surface.join!(c.r, c.g, c.b, c.a);
                var buffer = surface.buf32 + (bbox.min.y * surface.stride) + bbox.min.x;
                var ialpha = (byte)(255 - c.a);

                var vColor = Vector64.Create(color).AsByte();
                var vIalpha = Vector64.Create(ialpha);

                Vector64<byte>* vDst;
                uint align;

                for (uint y = 0; y < h; ++y)
                {
                    var dst = &buffer[y * surface.stride];

                    if (((nuint)dst & 0x7) != 0)
                    {
                        //fill not aligned byte
                        *dst = color + ALPHA_BLEND(*dst, ialpha);
                        vDst = (Vector64<byte>*)(dst + 1);
                        align = 1;
                    }
                    else
                    {
                        vDst = (Vector64<byte>*)dst;
                        align = 0;
                    }

                    for (uint xi = 0; xi < (w - align) / 2; ++xi)
                        vDst[xi] = AdvSimd.Add(vColor, ALPHA_BLEND_NEON(vDst[xi], vIalpha));

                    var leftovers = (w - align) % 2;
                    if (leftovers > 0) dst[w - 1] = color + ALPHA_BLEND(dst[w - 1], ialpha);
                }
            }
            //8bit grayscale
            else if (surface.channelSize == sizeof(byte))
            {
                var buffer = surface.buf8 + (bbox.min.y * surface.stride) + bbox.min.x;
                var ialpha = (byte)~c.a;
                for (uint y = 0; y < h; ++y)
                {
                    var dst = &buffer[y * surface.stride];
                    for (uint xi = 0; xi < w; ++xi, ++dst)
                    {
                        *dst = (byte)(c.a + MULTIPLY(*dst, ialpha));
                    }
                }
            }
            return true;
        }
    }
}
