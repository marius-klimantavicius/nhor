// Ported from pinned ThorVG/src/renderer/cpu_engine/tvgSwRasterSimd.cpp.
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;

namespace ThorVG
{
    public static unsafe partial class SwRaster
    {
        private static Vector64<byte> neonScalePixels(Vector64<byte> pixels, Vector64<uint> factors)
        {
            var expanded = Vector128.Create(Vector64.Create((ushort)factors.GetElement(0)), Vector64.Create((ushort)factors.GetElement(1)));
            return AdvSimd.ShiftRightLogicalNarrowingLower(AdvSimd.Multiply(AdvSimd.ZeroExtendWideningLower(pixels), expanded), 8);
        }

        internal static void neonRasterUnpremultiply(uint* buffer, uint width)
        {
            var mask = Vector128.Create(255u); var one = Vector128.Create(1u);
            uint x = 0;
            for (; width - x >= 4; x += 4)
            {
                var pixels = AdvSimd.LoadVector128(buffer + x);
                var alpha = AdvSimd.ShiftRightLogical(pixels, 24);
                var unchanged = AdvSimd.Or(AdvSimd.CompareEqual(alpha, mask), AdvSimd.CompareEqual(alpha, Vector128<uint>.Zero));
                if (AdvSimd.And(unchanged.GetLower(), unchanged.GetUpper()).AsUInt64().GetElement(0) == ulong.MaxValue) continue;
                var divisor = AdvSimd.Max(alpha, one);
                var divisorFloat = AdvSimd.ConvertToSingle(divisor);
                var reciprocal = AdvSimd.ReciprocalEstimate(divisorFloat);
                reciprocal = AdvSimd.Multiply(reciprocal, AdvSimd.ReciprocalStep(divisorFloat, reciprocal));
                reciprocal = AdvSimd.Multiply(reciprocal, AdvSimd.ReciprocalStep(divisorFloat, reciprocal));
                var result = AdvSimd.ShiftLeftLogical(alpha, 24);
                for (int shift = 0; shift < 24; shift += 8)
                {
                    var bits = Vector128.Create(shift);
                    var channel = AdvSimd.And(AdvSimd.ShiftLogical(pixels, AdvSimd.Negate(bits)), mask);
                    var numerator = AdvSimd.Multiply(channel, mask);
                    var quotient = AdvSimd.ConvertToUInt32RoundToZero(AdvSimd.Multiply(AdvSimd.ConvertToSingle(numerator), reciprocal));
                    var product = AdvSimd.Multiply(quotient, divisor);
                    quotient = AdvSimd.Subtract(quotient, AdvSimd.And(AdvSimd.CompareGreaterThan(product, numerator), one));
                    product = AdvSimd.Multiply(quotient, divisor);
                    quotient = AdvSimd.Add(quotient, AdvSimd.And(AdvSimd.CompareGreaterThanOrEqual(numerator, AdvSimd.Add(product, divisor)), one));
                    result = AdvSimd.Or(result, AdvSimd.ShiftLogical(AdvSimd.Min(quotient, mask), bits));
                }
                AdvSimd.Store(buffer + x, AdvSimd.BitwiseSelect(unchanged, pixels, result));
            }
            cRasterUnpremultiply(buffer + x, width - x);
        }

        internal static void neonRasterPremultiply(uint* buffer, uint width)
        {
            var mask = Vector128.Create(255u);
            uint x = 0;
            for (; width - x >= 4; x += 4)
            {
                var pixels = AdvSimd.LoadVector128(buffer + x);
                var alpha = AdvSimd.ShiftRightLogical(pixels, 24);
                var result = AdvSimd.ShiftLeftLogical(alpha, 24);
                for (int shift = 0; shift < 24; shift += 8)
                {
                    var bits = Vector128.Create(shift);
                    var channel = AdvSimd.And(AdvSimd.ShiftLogical(pixels, AdvSimd.Negate(bits)), mask);
                    var scaled = AdvSimd.ShiftRightLogical(AdvSimd.Multiply(channel, alpha), 8);
                    result = AdvSimd.Or(result, AdvSimd.ShiftLogical(scaled, bits));
                }
                AdvSimd.Store(buffer + x, AdvSimd.BitwiseSelect(AdvSimd.CompareEqual(alpha, mask), pixels, result));
            }
            cRasterPremultiply(buffer + x, width - x);
        }

        internal static uint neonRasterDownScaler(uint* image, uint stride, uint width, uint height, float sx, float sy, int miny, int maxy, int n)
        {
            var sum = Vector128<ushort>.Zero;
            int minx = Math.Max(0, (int)sx - n), maxx = Math.Min((int)width, (int)sx + n);
            int increment = n / 2 + 1, samples = 0;
            uint* src = image + minx + miny * stride;
            for (int y = miny; y < maxy; y += increment)
            {
                uint* p = src;
                for (int x = minx; x < maxx; x += increment, p += increment)
                {
                    sum = AdvSimd.AddWideningLower(sum, Vector64.Create(*p).AsByte());
                    ++samples;
                }
                src += stride * increment;
            }
            if (samples == 0) return 0;
            // The source reverses each group of four lanes before widening. Extracting
            // the four low channel sums gives the same packed byte order directly.
            return ((uint)(sum.GetElement(3) / samples) << 24) | ((uint)(sum.GetElement(2) / samples) << 16) |
                   ((uint)(sum.GetElement(1) / samples) << 8) | (uint)(sum.GetElement(0) / samples);
        }

        internal static void neonRasterTranslucentPixels(uint* dst, uint* src, uint length, byte opacity)
        {
            var opacityVector = Vector64.Create((uint)opacity + 1);
            uint i = 0;
            for (; length - i >= 2; i += 2)
            {
                var source = AdvSimd.LoadVector64((byte*)(src + i));
                if (opacity != 255) source = neonScalePixels(source, opacityVector);
                var inverse = AdvSimd.Subtract(Vector64.Create(256u), AdvSimd.ShiftRightLogical(source.AsUInt32(), 24));
                var target = AdvSimd.LoadVector64((byte*)(dst + i));
                AdvSimd.Store((byte*)(dst + i), AdvSimd.Add(source, neonScalePixels(target, inverse)));
            }
            cRasterTranslucentPixels(dst + i, src + i, length - i, opacity);
        }

        internal static void neonRasterSolidPixels(uint* dst, uint* src, uint length, byte opacity)
        {
            if (opacity == 255) NativeMemory.Copy(src, dst, (nuint)length * sizeof(uint));
            else neonRasterTranslucentPixels(dst, src, length, opacity);
        }

        private static Vector128<byte> neonBlendGrayscale(Vector128<byte> pixels, byte source, byte inverse)
        {
            var bias = Vector128.Create((ushort)255); var alpha = Vector64.Create(inverse);
            var low = AdvSimd.MultiplyWideningLowerAndAdd(bias, pixels.GetLower(), alpha);
            var high = AdvSimd.MultiplyWideningLowerAndAdd(bias, pixels.GetUpper(), alpha);
            var scaled = Vector128.Create(AdvSimd.ShiftRightLogicalNarrowingLower(low, 8), AdvSimd.ShiftRightLogicalNarrowingLower(high, 8));
            return AdvSimd.Add(Vector128.Create(source), scaled);
        }

        internal static bool neonRasterABGRtoARGB(RenderSurface surface)
        {
            var mask = Vector128.Create(0xff00ff00u);
            uint end = surface.w & ~3u;
            for (uint y = 0; y < surface.h; ++y)
            {
                uint* dst = surface.buf32 + y * surface.stride;
                uint x = 0;
                for (; x < end; x += 4)
                {
                    var pixels = AdvSimd.LoadVector128(dst + x);
                    var swapped = AdvSimd.ReverseElement16(pixels);
                    AdvSimd.Store(dst + x, AdvSimd.BitwiseSelect(mask, pixels, swapped));
                }
                for (; x < surface.w; ++x) { uint c = dst[x]; dst[x] = (c & 0xff00ff00u) | ((c & 0x00ff0000u) >> 16) | ((c & 255) << 16); }
            }
            return true;
        }

        private static Vector128<uint> neonChannels(uint pixel) => AdvSimd.ZeroExtendWideningLower(AdvSimd.ZeroExtendWideningLower(Vector64.CreateScalar(pixel).AsByte()).GetLower());

        internal static void neonRasterGaussianFilterRow(byte* dst, byte* src, int width, int dimension, float inverse)
        {
            int end = width - 1, i = 0, l = -(dimension + 1), r = dimension;
            var acc = Vector128<int>.Zero; var scale = Vector128.Create(inverse);
            for (int x = l; x < r; ++x) acc = AdvSimd.Add(acc, neonChannels(Unsafe.ReadUnaligned<uint>(src + Math.Clamp(x, 0, end) * 4)).AsInt32());
            for (int x = 0; x < width; ++x, ++r, ++l)
            {
                var added = neonChannels(Unsafe.ReadUnaligned<uint>(src + Math.Clamp(r, 0, end) * 4));
                var removed = neonChannels(Unsafe.ReadUnaligned<uint>(src + Math.Clamp(l, 0, end) * 4));
                acc = AdvSimd.Add(acc, AdvSimd.Subtract(added.AsInt32(), removed.AsInt32()));
                var values = AdvSimd.ConvertToInt32RoundToZero(AdvSimd.Multiply(AdvSimd.ConvertToSingle(acc), scale));
                var packed = AdvSimd.ExtractNarrowingLower(Vector128.Create(AdvSimd.ExtractNarrowingSaturateUnsignedLower(values), Vector64<ushort>.Zero));
                Unsafe.WriteUnaligned(dst + i, packed.AsUInt32().GetElement(0)); i += 4;
            }
        }

        internal static void neonRasterGaussianXYFlipBlock(uint* src, uint* dst, int stride)
        {
            for (int x = 0; x < 8; x += 4) for (int y = 0; y < 8; y += 4)
            {
                uint* s = src + x + y * stride; uint* d = dst + x * stride + y;
                var r0 = AdvSimd.LoadVector128(s); var r1 = AdvSimd.LoadVector128(s + stride);
                var r2 = AdvSimd.LoadVector128(s + 2 * stride); var r3 = AdvSimd.LoadVector128(s + 3 * stride);
                var t00 = AdvSimd.Arm64.TransposeEven(r0, r1); var t01 = AdvSimd.Arm64.TransposeOdd(r0, r1);
                var t10 = AdvSimd.Arm64.TransposeEven(r2, r3); var t11 = AdvSimd.Arm64.TransposeOdd(r2, r3);
                AdvSimd.Store(d, Vector128.Create(t00.GetLower(), t10.GetLower()));
                AdvSimd.Store(d + stride, Vector128.Create(t01.GetLower(), t11.GetLower()));
                AdvSimd.Store(d + 2 * stride, Vector128.Create(t00.GetUpper(), t10.GetUpper()));
                AdvSimd.Store(d + 3 * stride, Vector128.Create(t01.GetUpper(), t11.GetUpper()));
            }
        }
    }
}
