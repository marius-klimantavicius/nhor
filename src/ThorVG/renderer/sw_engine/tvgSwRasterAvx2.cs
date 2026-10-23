// Ported from ThorVG/src/renderer/cpu_engine/tvgSwRasterSimd.cpp (AVX2).
// Copyright (c) 2026 ThorVG project. MIT license, see ref/ThorVG/LICENSE.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using static ThorVG.SwHelper;

namespace ThorVG
{
    public static unsafe partial class SwRaster
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<uint> avx2ScalePixels(Vector256<uint> pixels, Vector256<uint> factors)
        {
            var mask = Vector256.Create(0x00ff00ffu);
            factors = Avx2.Or(factors, Avx2.ShiftLeftLogical(factors, 16));
            var even = Avx2.MultiplyLow(Avx2.And(pixels, mask).AsUInt16(), factors.AsUInt16());
            var odd = Avx2.MultiplyLow(Avx2.ShiftRightLogical(pixels.AsUInt16(), 8), factors.AsUInt16());
            return Avx2.Or(Avx2.ShiftRightLogical(even, 8).AsUInt32(), Avx2.AndNot(mask, odd.AsUInt32()));
        }

        internal static uint avx2RasterDownScaler(uint* image, uint stride, uint w, uint h, float sx, float sy, int miny, int maxy, int n)
        {
            var sum = Vector128<ushort>.Zero;
            int minx = Math.Max(0, (int)sx - n), maxx = Math.Min((int)w, (int)sx + n);
            int increment = n / 2 + 1, samples = 0;
            uint* src = image + minx + miny * stride;
            for (int y = miny; y < maxy; y += increment)
            {
                uint* p = src;
                for (int x = minx; x < maxx; x += increment, p += increment)
                {
                    var pixel = Vector128.CreateScalar(*p).AsByte();
                    sum = Sse2.Add(sum, Sse41.ConvertToVector128Int16(pixel).AsUInt16());
                    ++samples;
                }
                src += stride * increment;
            }
            if (samples == 0) return 0;
            uint c0 = (uint)(sum.GetElement(0) / samples), c1 = (uint)(sum.GetElement(1) / samples);
            uint c2 = (uint)(sum.GetElement(2) / samples), c3 = (uint)(sum.GetElement(3) / samples);
            return (c3 << 24) | (c2 << 16) | (c1 << 8) | c0;
        }

        internal static void avx2RasterTranslucentPixels(uint* dst, uint* src, uint length, byte opacity)
        {
            var opacityVector = Vector256.Create((uint)opacity + 1);
            var full = Vector256.Create(256u);
            uint i = 0;
            for (; length - i >= 8; i += 8)
            {
                var source = Avx.LoadVector256(src + i);
                if (opacity != 255) source = avx2ScalePixels(source, opacityVector);
                var inverse = Avx2.Subtract(full, Avx2.ShiftRightLogical(source, 24));
                var target = Avx.LoadVector256(dst + i);
                Avx.Store(dst + i, Avx2.Add(source, avx2ScalePixels(target, inverse)));
            }
            cRasterTranslucentPixels(dst + i, src + i, length - i, opacity);
        }

        internal static void avx2RasterSolidPixels(uint* dst, uint* src, uint length, byte opacity)
        {
            if (opacity == 255) NativeMemory.Copy(src, dst, (nuint)length * sizeof(uint));
            else avx2RasterTranslucentPixels(dst, src, length, opacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<byte> avx2BlendGrayscale(Vector256<byte> pixels, byte source, byte inverse)
        {
            var mask = Vector256.Create((ushort)255);
            var alpha = Vector256.Create((ushort)inverse);
            var even = Avx2.MultiplyLow(Avx2.And(pixels.AsUInt16(), mask), alpha);
            var odd = Avx2.MultiplyLow(Avx2.ShiftRightLogical(pixels.AsUInt16(), 8), alpha);
            even = Avx2.ShiftRightLogical(Avx2.Add(even, mask), 8);
            odd = Avx2.AndNot(mask, Avx2.Add(odd, mask));
            return Avx2.Add(Vector256.Create(source), Avx2.Or(even, odd).AsByte());
        }

        internal static bool avx2RasterTranslucentRect(SwSurface surface, in RenderRegion box, in RenderColor color)
        {
            uint w = box.W(), h = box.H();
            if (surface.channelSize == sizeof(uint))
            {
                uint c = surface.join!(color.r, color.g, color.b, color.a);
                uint* buffer = surface.buf32 + box.min.y * surface.stride + box.min.x;
                uint inverse = (uint)(255 - color.a);
                var source = Vector256.Create(c);
                var alpha = Vector256.Create(inverse + 1);
                for (uint y = 0; y < h; ++y)
                {
                    uint* dst = buffer + y * surface.stride;
                    uint x = 0;
                    for (; w - x >= 8; x += 8) Avx.Store(dst + x, Avx2.Add(source, avx2ScalePixels(Avx.LoadVector256(dst + x), alpha)));
                    for (; x < w; ++x) dst[x] = c + ALPHA_BLEND(dst[x], inverse);
                }
            }
            else if (surface.channelSize == sizeof(byte))
            {
                byte* buffer = surface.buf8 + box.min.y * surface.stride + box.min.x;
                byte inverse = (byte)~color.a;
                for (uint y = 0; y < h; ++y)
                {
                    byte* dst = buffer + y * surface.stride;
                    uint x = 0;
                    for (; w - x >= 32; x += 32) Avx.Store(dst + x, avx2BlendGrayscale(Avx.LoadVector256(dst + x), color.a, inverse));
                    for (; x < w; ++x) dst[x] = (byte)(color.a + MULTIPLY(dst[x], inverse));
                }
            }
            return true;
        }

        internal static bool avx2RasterTranslucentRle(SwSurface surface, SwRle* rle, in RenderRegion box, in RenderColor color)
        {
            SwSpan* end;
            if (surface.channelSize == sizeof(uint))
            {
                uint c = surface.join!(color.r, color.g, color.b, color.a);
                for (var span = rle->Fetch(box, out end); span < end; ++span)
                {
                    if (!span->Fetch(box, out int x, out int length)) continue;
                    uint source = span->coverage < 255 ? ALPHA_BLEND(c, span->coverage) : c;
                    uint* dst = surface.buf32 + span->y * surface.stride + x;
                    uint inverse = IA(source);
                    var sourceVector = Vector256.Create(source);
                    var alpha = Vector256.Create(inverse + 1);
                    int i = 0;
                    for (; i <= length - 8; i += 8) Avx.Store(dst + i, Avx2.Add(sourceVector, avx2ScalePixels(Avx.LoadVector256(dst + i), alpha)));
                    for (; i < length; ++i) dst[i] = source + ALPHA_BLEND(dst[i], inverse);
                }
            }
            else if (surface.channelSize == sizeof(byte))
            {
                byte inverse = (byte)~color.a;
                for (var span = rle->Fetch(box, out end); span < end; ++span)
                {
                    if (!span->Fetch(box, out int x, out int length)) continue;
                    byte* dst = surface.buf8 + span->y * surface.stride + x;
                    byte source = span->coverage < 255 ? (byte)MULTIPLY(span->coverage, color.a) : color.a;
                    int i = 0;
                    for (; i <= length - 32; i += 32) Avx.Store(dst + i, avx2BlendGrayscale(Avx.LoadVector256(dst + i), source, inverse));
                    for (; i < length; ++i) dst[i] = (byte)(source + MULTIPLY(dst[i], inverse));
                }
            }
            return true;
        }

        internal static void avx2RasterUnpremultiply(uint* buffer, uint width)
        {
            var mask = Vector256.Create(255u);
            var full = Vector256.Create(255);
            var one = Vector256.Create(1);
            uint x = 0;
            for (; width - x >= 8; x += 8)
            {
                var pixels = Avx.LoadVector256(buffer + x);
                var alpha = Avx2.ShiftRightLogical(pixels, 24).AsInt32();
                var unchanged = Avx2.Or(Avx2.CompareEqual(alpha, full), Avx2.CompareEqual(alpha, Vector256<int>.Zero));
                if (Avx2.MoveMask(unchanged.AsSByte()) == -1) continue;
                var divisor = Avx.ConvertToVector256Single(Avx2.Max(alpha, one));
                var result = Avx2.ShiftLeftLogical(alpha.AsUInt32(), 24);
                for (byte shift = 0; shift < 24; shift += 8)
                {
                    var bits = Vector128.CreateScalar((uint)shift);
                    var channel = Avx2.And(Avx2.ShiftRightLogical(pixels, bits), mask).AsInt32();
                    var scaled = Avx2.MultiplyLow(channel, full);
                    var quotient = Avx.ConvertToVector256Int32WithTruncation(Avx.Divide(Avx.ConvertToVector256Single(scaled), divisor));
                    result = Avx2.Or(result, Avx2.ShiftLeftLogical(Avx2.Min(quotient, full).AsUInt32(), bits));
                }
                Avx.Store(buffer + x, Avx2.BlendVariable(result.AsByte(), pixels.AsByte(), unchanged.AsByte()).AsUInt32());
            }
            cRasterUnpremultiply(buffer + x, width - x);
        }

        internal static void avx2RasterPremultiply(uint* buffer, uint width)
        {
            var full = Vector256.Create(255u);
            var alphaMask = Vector256.Create(0xff000000u);
            uint x = 0;
            for (; width - x >= 8; x += 8)
            {
                var pixels = Avx.LoadVector256(buffer + x);
                var alpha = Avx2.ShiftRightLogical(pixels, 24);
                var factors = Avx2.Subtract(alpha, Avx2.CompareEqual(alpha, full));
                var result = avx2ScalePixels(pixels, factors);
                Avx.Store(buffer + x, Avx2.BlendVariable(result.AsByte(), pixels.AsByte(), alphaMask.AsByte()).AsUInt32());
            }
            cRasterPremultiply(buffer + x, width - x);
        }

        internal static bool avx2RasterABGRtoARGB(RenderSurface surface)
        {
            var shuffle = Vector256.Create(Vector128.Create((byte)2, 1, 0, 3, 6, 5, 4, 7, 10, 9, 8, 11, 14, 13, 12, 15),
                                           Vector128.Create((byte)2, 1, 0, 3, 6, 5, 4, 7, 10, 9, 8, 11, 14, 13, 12, 15));
            uint end = surface.w & ~7u;
            for (uint y = 0; y < surface.h; ++y)
            {
                uint* dst = surface.buf32 + y * surface.stride;
                uint x = 0;
                for (; x < end; x += 8) Avx.Store(dst + x, Avx2.Shuffle(Avx.LoadVector256(dst + x).AsByte(), shuffle).AsUInt32());
                for (; x < surface.w; ++x) { uint c = dst[x]; dst[x] = (c & 0xff00ff00u) | ((c & 0x00ff0000u) >> 16) | ((c & 255) << 16); }
            }
            return true;
        }

        internal static void avx2RasterGaussianFilterRow(byte* dst, byte* src, int width, int dimension, float inverse)
        {
            int end = width - 1, i = 0, l = -(dimension + 1), r = dimension;
            var acc = Vector128<int>.Zero;
            var zero = Vector128<int>.Zero;
            var scale = Vector128.Create(inverse);
            for (int x = l; x < r; ++x)
            {
                uint pixel = Unsafe.ReadUnaligned<uint>(src + Math.Clamp(x, 0, end) * 4);
                acc = Sse2.Add(acc, Sse41.ConvertToVector128Int32(Vector128.CreateScalar(pixel).AsByte()));
            }
            for (int x = 0; x < width; ++x, ++r, ++l)
            {
                uint right = Unsafe.ReadUnaligned<uint>(src + Math.Clamp(r, 0, end) * 4);
                uint left = Unsafe.ReadUnaligned<uint>(src + Math.Clamp(l, 0, end) * 4);
                var added = Sse41.ConvertToVector128Int32(Vector128.CreateScalar(right).AsByte());
                var removed = Sse41.ConvertToVector128Int32(Vector128.CreateScalar(left).AsByte());
                acc = Sse2.Add(acc, Sse2.Subtract(added, removed));
                var values = Sse2.ConvertToVector128Int32WithTruncation(Sse.Multiply(Sse2.ConvertToVector128Single(acc), scale));
                var packed = Sse2.PackUnsignedSaturate(Sse2.PackSignedSaturate(values, zero), zero.AsInt16());
                Unsafe.WriteUnaligned(dst + i, packed.AsUInt32().GetElement(0)); i += 4;
            }
        }

        internal static void avx2RasterGaussianXYFlipBlock(uint* src, uint* dst, int stride)
        {
            for (int x = 0; x < 8; x += 4)
            {
                for (int y = 0; y < 8; y += 4)
                {
                    uint* s = src + x + y * stride; uint* d = dst + x * stride + y;
                    var r0 = Sse2.LoadVector128(s); var r1 = Sse2.LoadVector128(s + stride);
                    var r2 = Sse2.LoadVector128(s + 2 * stride); var r3 = Sse2.LoadVector128(s + 3 * stride);
                    var t0 = Sse2.UnpackLow(r0, r1); var t1 = Sse2.UnpackHigh(r0, r1);
                    var t2 = Sse2.UnpackLow(r2, r3); var t3 = Sse2.UnpackHigh(r2, r3);
                    Sse2.Store(d, Sse2.UnpackLow(t0.AsUInt64(), t2.AsUInt64()).AsUInt32());
                    Sse2.Store(d + stride, Sse2.UnpackHigh(t0.AsUInt64(), t2.AsUInt64()).AsUInt32());
                    Sse2.Store(d + 2 * stride, Sse2.UnpackLow(t1.AsUInt64(), t3.AsUInt64()).AsUInt32());
                    Sse2.Store(d + 3 * stride, Sse2.UnpackHigh(t1.AsUInt64(), t3.AsUInt64()).AsUInt32());
                }
            }
        }
    }
}
