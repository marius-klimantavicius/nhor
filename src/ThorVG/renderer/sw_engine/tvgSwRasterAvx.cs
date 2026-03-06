// Ported from ThorVG/src/renderer/sw_engine/tvgSwRasterAvx.h
// Direct port of AVX/SSE2 intrinsics — same operations, same order as C++.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using static ThorVG.SwHelper;

namespace ThorVG
{
    public static unsafe partial class SwRaster
    {
        private const int N_32BITS_IN_128REG = 4;
        private const int N_32BITS_IN_256REG = 8;

        // =================================================================
        //  ALPHA_BLEND — SSE2 128-bit (4 pixels at once)
        //  Mirrors C++ ALPHA_BLEND(__m128i c, __m128i a)
        // =================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<int> ALPHA_BLEND_SSE(Vector128<int> c, Vector128<int> a)
        {
            //1. set the masks for the A/G and R/B channels
            var AG = Vector128.Create(unchecked((int)0xff00ff00));
            var RB = Vector128.Create(0x00ff00ff);

            //2. mask the alpha vector - originally quartet [a, a, a, a]
            var aAG = Sse2.And(a, AG);
            var aRB = Sse2.And(a, RB);

            //3. calculate the alpha blending of the 2nd and 4th channel (R/B)
            //- mask the color vector
            //- multiply it by the masked alpha vector
            //- add the correction to compensate bit shifting used instead of dividing by 255
            //- shift bits - corresponding to division by 256
            var even = Sse2.And(c, RB);
            even = Sse2.MultiplyLow(even.AsInt16(), aRB.AsInt16()).AsInt32();
            even = Sse2.Add(even.AsInt16(), RB.AsInt16()).AsInt32();
            even = Sse2.ShiftRightLogical(even.AsUInt16(), 8).AsInt32();

            //4. calculate the alpha blending of the 1st and 3rd channel (A/G):
            //- mask the color vector
            //- multiply it by the corresponding masked alpha vector and store the high bits of the result
            //- add the correction to compensate division by 256 instead of by 255 (next step)
            //- remove the low 8 bits to mimic the division by 256
            var odd = Sse2.And(c, AG);
            odd = Sse2.MultiplyHigh(odd.AsUInt16(), aAG.AsUInt16()).AsInt32();
            odd = Sse2.Add(odd.AsInt16(), RB.AsInt16()).AsInt32();
            odd = Sse2.And(odd, AG);

            //5. the final result
            return Sse2.Or(odd, even);
        }

        // =================================================================
        //  avxRasterGrayscale8
        //  Mirrors C++ avxRasterGrayscale8(uint8_t* dst, uint8_t val,
        //                                   uint32_t offset, int32_t len)
        // =================================================================

        internal static void avxRasterGrayscale8(byte* dst, byte val, uint offset, int len)
        {
            dst += offset;

            var vecVal = Vector256.Create(val);

            int i = 0;
            for (; i <= len - 32; i += 32)
            {
                Avx.Store(dst + i, vecVal);
            }

            for (; i < len; ++i)
            {
                dst[i] = val;
            }
        }

        // =================================================================
        //  avxRasterPixel32
        //  Mirrors C++ avxRasterPixel32(uint32_t *dst, uint32_t val,
        //                                uint32_t offset, int32_t len)
        // =================================================================

        internal static void avxRasterPixel32(uint* dst, uint val, uint offset, int len)
        {
            //1. calculate how many iterations we need to cover the length
            uint iterations = (uint)len / N_32BITS_IN_256REG;
            uint avxFilled = iterations * N_32BITS_IN_256REG;

            //2. set the beginning of the array
            dst += offset;

            //3. fill the octets
            var vecVal = Vector256.Create(val);
            for (uint i = 0; i < iterations; ++i, dst += N_32BITS_IN_256REG)
            {
                Avx.Store(dst, vecVal);
            }

            //4. fill leftovers
            int leftovers = len - (int)avxFilled;
            while (leftovers-- > 0) *dst++ = val;
        }

        // =================================================================
        //  avxRasterTranslucentRect
        //  Mirrors C++ avxRasterTranslucentRect(SwSurface* surface,
        //            const RenderRegion& bbox, const RenderColor& c)
        // =================================================================

        internal static bool avxRasterTranslucentRect(SwSurface surface, in RenderRegion bbox, in RenderColor c)
        {
            var h = bbox.H();
            var w = bbox.W();

            //32bits channels
            if (surface.channelSize == sizeof(uint))
            {
                var color = surface.join!(c.r, c.g, c.b, c.a);
                var buffer = surface.buf32 + (bbox.min.y * surface.stride) + bbox.min.x;

                uint ialpha = (uint)(255 - c.a);

                var avxColor = Vector128.Create((int)color);
                var avxIalpha = Vector128.Create((byte)ialpha);

                for (uint y = 0; y < h; ++y)
                {
                    var dst = &buffer[y * surface.stride];

                    //1. fill the not aligned memory (for 128-bit registers a 16-bytes alignment is required)
                    var notAligned = (uint)(((nuint)dst & 0xf) / 4);
                    if (notAligned != 0)
                    {
                        notAligned = (uint)(N_32BITS_IN_128REG - (int)notAligned > (int)w ? w : N_32BITS_IN_128REG - notAligned);
                        for (uint x = 0; x < notAligned; ++x, ++dst)
                        {
                            *dst = color + ALPHA_BLEND(*dst, ialpha);
                        }
                    }

                    //2. fill the aligned memory - N_32BITS_IN_128REG pixels processed at once
                    uint iterations = (uint)(w - notAligned) / N_32BITS_IN_128REG;
                    uint avxFilled = iterations * N_32BITS_IN_128REG;
                    var avxDst = (Vector128<int>*)dst;
                    for (uint x = 0; x < iterations; ++x, ++avxDst)
                    {
                        *avxDst = Sse2.Add(avxColor, ALPHA_BLEND_SSE(*avxDst, avxIalpha.AsInt32()));
                    }

                    //3. fill the remaining pixels
                    int leftovers = (int)(w - notAligned - avxFilled);
                    dst += avxFilled;
                    while (leftovers-- > 0)
                    {
                        *dst = color + ALPHA_BLEND(*dst, ialpha);
                        dst++;
                    }
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

        // =================================================================
        //  avxRasterTranslucentRle
        //  Mirrors C++ avxRasterTranslucentRle(SwSurface* surface,
        //            const SwRle* rle, const RenderRegion& bbox,
        //            const RenderColor& c)
        // =================================================================

        internal static bool avxRasterTranslucentRle(SwSurface surface, SwRle* rle, in RenderRegion bbox, in RenderColor c)
        {
            SwSpan* end;
            int x, len;

            //32bit channels
            if (surface.channelSize == sizeof(uint))
            {
                var color = surface.join!(c.r, c.g, c.b, c.a);
                uint src;

                for (var span = rle->Fetch(bbox, out end); span < end; ++span)
                {
                    if (!span->Fetch(bbox, out x, out len)) continue;
                    if (span->coverage < 255) src = ALPHA_BLEND(color, span->coverage);
                    else src = color;

                    var dst = &surface.buf32[span->y * surface.stride + x];
                    var ialpha = IA(src);

                    //1. fill the not aligned memory (for 128-bit registers a 16-bytes alignment is required)
                    int notAligned = (int)(((nuint)dst & 0xf) / 4);
                    if (notAligned != 0)
                    {
                        notAligned = (N_32BITS_IN_128REG - notAligned > len ? len : N_32BITS_IN_128REG - notAligned);
                        for (int xi = 0; xi < notAligned; ++xi, ++dst)
                        {
                            *dst = src + ALPHA_BLEND(*dst, ialpha);
                        }
                    }

                    //2. fill the aligned memory using avx - N_32BITS_IN_128REG pixels processed at once
                    //In order to avoid unnecessary avx variables declarations a check is made whether there are any iterations at all
                    int iterations = (len - notAligned) / N_32BITS_IN_128REG;
                    int avxFilled = 0;
                    if (iterations > 0)
                    {
                        var avxSrc = Vector128.Create((int)src);
                        var avxIalpha = Vector128.Create((byte)ialpha);

                        avxFilled = iterations * N_32BITS_IN_128REG;
                        var avxDst = (Vector128<int>*)dst;
                        for (int xi = 0; xi < iterations; ++xi, ++avxDst)
                        {
                            *avxDst = Sse2.Add(avxSrc, ALPHA_BLEND_SSE(*avxDst, avxIalpha.AsInt32()));
                        }
                    }

                    //3. fill the remaining pixels
                    var leftovers = len - notAligned - avxFilled;
                    dst += avxFilled;
                    while (leftovers-- > 0)
                    {
                        *dst = src + ALPHA_BLEND(*dst, ialpha);
                        dst++;
                    }
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
    }
}
