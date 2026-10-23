// Ported from pinned ThorVG/src/renderer/cpu_engine/tvgSwPostEffect.cpp.
using System;

namespace ThorVG
{
    internal class SwMotionBlur
    {
        public const int MAX_TAPS = 4 * 257;
        public struct Tap { public int x, y; public uint weight; }
        public readonly Tap[] taps = new Tap[MAX_TAPS];
        public int count, tapCount;
        public SwPoint extent;
        public static readonly System.Collections.Generic.IComparer<Tap> CoordinateOrder =
            System.Collections.Generic.Comparer<Tap>.Create((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
    }

    public static unsafe partial class SwPostEffect
    {
        public static void effectMotionBlurUpdate(RenderEffectMotionBlur effect, in Matrix transform)
        {
            if (!effect.Update(transform, out var offset, out int samples)) return;
            var data = effect.rd as SwMotionBlur ?? new SwMotionBlur();
            effect.rd = data;
            data.extent = new SwPoint((int)MathF.Ceiling(MathF.Abs(offset.x) * .5f), (int)MathF.Ceiling(MathF.Abs(offset.y) * .5f));
            data.count = samples;
            data.tapCount = 0;
            Span<uint> weights = stackalloc uint[4];
            for (int i = 0; i < samples; ++i)
            {
                var pt = TvgMath.PointMul(offset, (i + .5f) / samples - .5f);
                int x = (int)MathF.Floor(pt.x), y = (int)MathF.Floor(pt.y);
                uint fx = (uint)((pt.x - x) * 256 + .5f), fy = (uint)((pt.y - y) * 256 + .5f);
                weights[0] = (256 - fx) * (256 - fy); weights[1] = fx * (256 - fy);
                weights[2] = (256 - fx) * fy; weights[3] = fx * fy;
                for (int k = 0; k < 4; ++k)
                    if (weights[k] != 0) data.taps[data.tapCount++] = new SwMotionBlur.Tap { x = x + (k & 1), y = y + (k >> 1), weight = weights[k] };
            }
            System.Array.Sort(data.taps, 0, data.tapCount, SwMotionBlur.CoordinateOrder);
            int merged = 0;
            for (int i = 0; i < data.tapCount; ++i)
            {
                var tap = data.taps[i];
                if (merged > 0 && data.taps[merged - 1].x == tap.x && data.taps[merged - 1].y == tap.y) data.taps[merged - 1].weight += tap.weight;
                else data.taps[merged++] = tap;
            }
            data.tapCount = merged;
            effect.valid = true;
        }

        public static bool effectMotionBlurRegion(RenderEffectMotionBlur effect)
        {
            var data = (SwMotionBlur)effect.rd!;
            effect.extend = new RenderRegion(-data.extent.x, -data.extent.y, data.extent.x, data.extent.y);
            return true;
        }

        public static bool effectMotionBlur(SwCompositor cmp, SwSurface surface, RenderEffectMotionBlur effect)
        {
            var buffer = surface.compositor!.image;
            var data = (SwMotionBlur)effect.rd!;
            var box = cmp.bbox;
            int width = box.max.x - box.min.x, height = box.max.y - box.min.y;
            uint stride = cmp.image.stride;
            ulong divisor = (ulong)data.count * 65536;
            uint* src = cmp.image.buf32 + box.min.y * stride + box.min.x;
            uint* dst = buffer.buf32 + box.min.y * stride + box.min.x;
#if !THORVG_PARALLEL_EFFECTS
            uint** rows = stackalloc uint*[SwMotionBlur.MAX_TAPS];
            Span<ulong> acc = stackalloc ulong[4];
#endif
            #if THORVG_PARALLEL_EFFECTS
            System.Threading.Tasks.Parallel.For(0, height, SwPostEffect.ParallelOptions(), y =>
            {
                uint** rows = stackalloc uint*[SwMotionBlur.MAX_TAPS];
                Span<ulong> acc = stackalloc ulong[4];
                for (int i = 0; i < data.tapCount; ++i) rows[i] = src + Math.Clamp(y + data.taps[i].y, 0, height - 1) * stride;
                byte* row = (byte*)(dst + y * stride);
                for (int x = 0; x < width; ++x)
                {
                    acc.Clear();
                    for (int i = 0; i < data.tapCount; ++i)
                    {
                        var tap = data.taps[i];
                        byte* pixel = (byte*)(rows[i] + Math.Clamp(x + tap.x, 0, width - 1));
                        for (int c = 0; c < 4; ++c) acc[c] += (ulong)pixel[c] * tap.weight;
                    }
                    for (int c = 0; c < 4; ++c) row[x * 4 + c] = (byte)((acc[c] + divisor / 2) / divisor);
                }

            });
#else
            for (int y = 0; y < height; ++y)
            {
                for (int i = 0; i < data.tapCount; ++i) rows[i] = src + Math.Clamp(y + data.taps[i].y, 0, height - 1) * stride;
                byte* row = (byte*)(dst + y * stride);
                for (int x = 0; x < width; ++x)
                {
                    acc.Clear();
                    for (int i = 0; i < data.tapCount; ++i)
                    {
                        var tap = data.taps[i];
                        byte* pixel = (byte*)(rows[i] + Math.Clamp(x + tap.x, 0, width - 1));
                        for (int c = 0; c < 4; ++c) acc[c] += (ulong)pixel[c] * tap.weight;
                    }
                    for (int c = 0; c < 4; ++c) row[x * 4 + c] = (byte)((acc[c] + divisor / 2) / divisor);
                }
            }
#endif
            uint* swap = cmp.image.buf32; cmp.image.buf32 = buffer.buf32; buffer.buf32 = swap;
            return true;
        }
    }
}
