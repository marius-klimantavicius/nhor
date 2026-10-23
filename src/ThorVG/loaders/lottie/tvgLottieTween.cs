// Ported from ThorVG/src/loaders/lottie/tvgLottieTween.h

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ThorVG
{
    public class LottieTween : IDisposable
    {
        [StructLayout(LayoutKind.Explicit)]
        private struct NumericValue
        {
            [FieldOffset(0)] public float scalar;
            [FieldOffset(0)] public sbyte integer;
            [FieldOffset(0)] public byte opacity;
            [FieldOffset(0)] public Point point;
            [FieldOffset(0)] public Point3 point3;
            [FieldOffset(0)] public RGB32 color;
        }

        private sealed class ValueSet
        {
            public object? from;
            public object? cur;
            public NumericValue fromNumber;
            public NumericValue curNumber;
            public bool captured;
            public bool inited;
        }

        private readonly Dictionary<(LottieProperty property, float frameNo, byte channel), ValueSet> data = new();

        public float to;
        public float progress;
        public bool active;
        public bool legacy;

        ~LottieTween() { Release(); }

        public void Dispose() { Release(); active = legacy = false; GC.SuppressFinalize(this); }

        private void Release()
        {
            foreach (var set in data.Values)
            {
                if (set.from is RenderPath from) { from.pts.Dispose(); from.cmds.Dispose(); }
                if (set.cur is RenderPath cur) { cur.pts.Dispose(); cur.cmds.Dispose(); }
            }
            data.Clear();
        }

        public void On(float to)
        {
            this.to = to;
            active = true;
            legacy = false;
            foreach (var value in data.Values) value.inited = false;
        }

        public void On(float to, float progress)
        {
            this.to = to;
            this.progress = progress;
            active = legacy = true;
        }

        public void Off()
        {
            if (!active) return;
            active = legacy = false;
            Release();
        }

        private ValueSet Set(LottieProperty property, float frameNo, byte channel = 0)
        {
            var key = (property, frameNo, channel);
            if (!data.TryGetValue(key, out var set)) data[key] = set = new ValueSet();
            return set;
        }

        public bool Inited(LottieProperty property, float frameNo, byte channel = 0)
        {
            if (legacy) return false;
            if (!data.TryGetValue((property, frameNo, channel), out var set)) return false;
            var ret = set.inited;
            set.inited = true;
            return ret;
        }

        public void Capture<T>(LottieProperty property, float frameNo, T from, byte channel = 0)
        {
            if (from is Fill fill) { Capture(property, frameNo, fill); return; }
            if (from is RenderPath path) { Capture(property, frameNo, path); return; }
            var set = Set(property, frameNo, channel);
            if (ChainSwap(set)) return;
            switch (from)
            {
                case float value: set.fromNumber.scalar = value; break;
                case sbyte value: set.fromNumber.integer = value; break;
                case byte value: set.fromNumber.opacity = value; break;
                case Point value: set.fromNumber.point = value; break;
                case Point3 value: set.fromNumber.point3 = value; break;
                case RGB32 value: set.fromNumber.color = value; break;
                default: throw new NotSupportedException($"Unsupported Lottie tween value: {typeof(T)}");
            }
        }

        private static bool ChainSwap(ValueSet set)
        {
            if (!set.captured)
            {
                set.captured = true;
                return false;
            }
            (set.from, set.cur) = (set.cur, set.from);
            (set.fromNumber, set.curNumber) = (set.curNumber, set.fromNumber);
            return true;
        }

        public float Run(LottieProperty property, float frameNo, float target, byte channel = 0)
        {
            var set = Set(property, frameNo, channel);
            set.captured = true;
            var value = TvgMath.Lerp(set.fromNumber.scalar, target, progress);
            set.curNumber.scalar = value;
            return value;
        }

        public sbyte Run(LottieProperty property, float frameNo, sbyte target)
        {
            var set = Set(property, frameNo);
            set.captured = true;
            var value = unchecked((sbyte)TvgMath.Lerp((int)set.fromNumber.integer, (int)target, progress));
            set.curNumber.integer = value;
            return value;
        }

        public byte Run(LottieProperty property, float frameNo, byte target)
        {
            var set = Set(property, frameNo);
            set.captured = true;
            var value = TvgMath.Lerp(set.fromNumber.opacity, target, progress);
            set.curNumber.opacity = value;
            return value;
        }

        public Point Run(LottieProperty property, float frameNo, Point target)
        {
            var set = Set(property, frameNo);
            set.captured = true;
            var from = set.fromNumber.point;
            var value = TvgMath.Lerp(from, target, progress);
            set.curNumber.point = value;
            return value;
        }

        public Point3 Run(LottieProperty property, float frameNo, Point3 target)
        {
            var set = Set(property, frameNo);
            set.captured = true;
            var from = set.fromNumber.point3;
            var value = TvgMath.Lerp(from, target, progress);
            set.curNumber.point3 = value;
            return value;
        }

        public RGB32 Run(LottieProperty property, float frameNo, RGB32 target)
        {
            var set = Set(property, frameNo);
            set.captured = true;
            var value = RGB32.Lerp(set.fromNumber.color, target, progress);
            set.curNumber.color = value;
            return value;
        }

        public void Capture(LottieProperty property, float frameNo, RenderPath from)
        {
            var set = Set(property, frameNo);
            if (ChainSwap(set)) return;
            set.from = Copy(null, from);
        }

        public unsafe void Run(LottieProperty property, float frameNo, RenderPath target, RenderPath output, LottieModifier? modifier)
        {
            var set = Set(property, frameNo);
            var from = (RenderPath)set.from!;
            var pivot = output.pts.count;
            var count = Math.Min(from.pts.count, target.pts.count);
            if (modifier != null)
            {
                for (uint i = 0; i < count; ++i) target.pts[i] = TvgMath.Lerp(from.pts[i], target.pts[i], progress);
                modifier.Path(target, output, null);
            }
            else
            {
                for (uint i = 0; i < count; ++i) output.pts.Push(TvgMath.Lerp(from.pts[i], target.pts[i], progress));
                output.cmds.Push(target.cmds);
            }
            set.cur = Copy(set.cur as RenderPath, output, pivot, from.cmds);
        }

        public void Capture(LottieProperty property, float frameNo, Fill fill)
        {
            var set = Set(property, frameNo);
            if (ChainSwap(set)) return;
            fill.GetColorStops(out var stops);
            set.from = stops == null ? System.Array.Empty<Fill.ColorStop>() : (Fill.ColorStop[])stops.Clone();
        }

        public void Run(LottieProperty property, float frameNo, Fill fill)
        {
            var set = Set(property, frameNo);
            var from = (Fill.ColorStop[])set.from!;
            fill.GetColorStops(out var target);
            if (target == null) return;
            var count = Math.Min(from.Length, target.Length);
            var result = (Fill.ColorStop[])target.Clone();
            for (var i = 0; i < count; ++i)
            {
                result[i] = new Fill.ColorStop(TvgMath.Lerp(from[i].offset, target[i].offset, progress),
                    TvgMath.Lerp(from[i].r, target[i].r, progress), TvgMath.Lerp(from[i].g, target[i].g, progress),
                    TvgMath.Lerp(from[i].b, target[i].b, progress), TvgMath.Lerp(from[i].a, target[i].a, progress));
            }
            fill.SetColorStops(result, (uint)result.Length);
            set.cur = result;
        }

        private static unsafe RenderPath Copy(RenderPath? copy, RenderPath source, uint pivot = 0, Array<PathCommand>? commands = null)
        {
            copy ??= new RenderPath();
            copy.pts.Clear();
            for (var i = pivot; i < source.pts.count; ++i) copy.pts.Push(source.pts[i]);
            var cmds = commands ?? source.cmds;
            if (copy.cmds.count != cmds.count) { copy.cmds.Clear(); copy.cmds.Push(cmds); }
            return copy;
        }
    }
}
