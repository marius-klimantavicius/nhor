// Scalar row primitives split out by pinned upstream tvgSwRasterCore.cpp.
using System;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;
using static ThorVG.SwHelper;

namespace ThorVG
{
    public static unsafe partial class SwRaster
    {
        internal static void cRasterPremultiply(uint* buffer, uint width)
        {
            for (uint i = 0; i < width; ++i) { uint c = buffer[i]; if (A(c) != 255) buffer[i] = PREMULTIPLY(c, A(c)); }
        }
        internal static void cRasterUnpremultiply(uint* buffer, uint width)
        {
            for (uint i = 0; i < width; ++i) buffer[i] = rasterUnpremultiply(buffer[i]);
        }
        internal static void cRasterGaussianFilterRow(byte* dst, byte* src, int width, int dimension, float inverse)
        {
            int end = width - 1, i = 0, l = -(dimension + 1), r = dimension;
            Span<int> acc = stackalloc int[4];
            acc.Clear();
            for (int x = l; x < r; ++x)
            {
                int id = Math.Clamp(x, 0, end) * 4;
                acc[0] += src[id++]; acc[1] += src[id++]; acc[2] += src[id++]; acc[3] += src[id];
            }
            for (int x = 0; x < width; ++x, ++r, ++l)
            {
                int rid = Math.Clamp(r, 0, end) * 4, lid = Math.Clamp(l, 0, end) * 4;
                acc[0] += src[rid++] - src[lid++]; acc[1] += src[rid++] - src[lid++];
                acc[2] += src[rid++] - src[lid++]; acc[3] += src[rid] - src[lid];
                dst[i++] = (byte)(acc[0] * inverse); dst[i++] = (byte)(acc[1] * inverse);
                dst[i++] = (byte)(acc[2] * inverse); dst[i++] = (byte)(acc[3] * inverse);
            }
        }

        public static void rasterGaussianFilterRow(byte* dst, byte* src, int width, int dimension, float inverse)
        {
            if (width <= 0) return;
#if ENABLE_SIMD
            if (Avx2.IsSupported) { avx2RasterGaussianFilterRow(dst, src, width, dimension, inverse); return; }
            if (AdvSimd.IsSupported) { neonRasterGaussianFilterRow(dst, src, width, dimension, inverse); return; }
#endif
            cRasterGaussianFilterRow(dst, src, width, dimension, inverse);
        }
    }
}
