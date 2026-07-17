// Ported from ThorVG/src/loaders/lottie/tvgLottieTween.h

using System;
using System.Collections.Generic;

namespace ThorVG
{
    public class LottieTween
    {
        private sealed class ValueSet
        {
            public object? from;
            public object? cur;
            public bool inited;
        }

        private readonly Dictionary<(LottieProperty property, float frameNo, byte channel), ValueSet> data = new();

        public float to;
        public float progress;
        public bool active;
        public bool legacy;

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
            data.Clear();
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
            var set = Set(property, frameNo, channel);
            if (set.cur != null)
            {
                (set.from, set.cur) = (set.cur, set.from);
                return;
            }
            set.from = from;
        }

        public float Run(LottieProperty property, float frameNo, float target, byte channel = 0)
        {
            var set = Set(property, frameNo, channel);
            var value = TvgMath.Lerp((float)set.from!, target, progress);
            set.cur = value;
            return value;
        }

        public sbyte Run(LottieProperty property, float frameNo, sbyte target)
        {
            var set = Set(property, frameNo);
            var value = (sbyte)TvgMath.Clamp((int)((sbyte)set.from! + (target - (sbyte)set.from!) * progress), sbyte.MinValue, sbyte.MaxValue);
            set.cur = value;
            return value;
        }

        public byte Run(LottieProperty property, float frameNo, byte target)
        {
            var set = Set(property, frameNo);
            var value = (byte)TvgMath.Clamp((int)((byte)set.from! + (target - (byte)set.from!) * progress), 0, 255);
            set.cur = value;
            return value;
        }

        public Point Run(LottieProperty property, float frameNo, Point target)
        {
            var set = Set(property, frameNo);
            var from = (Point)set.from!;
            var value = TvgMath.Lerp(from, target, progress);
            set.cur = value;
            return value;
        }

        public Point3 Run(LottieProperty property, float frameNo, Point3 target)
        {
            var set = Set(property, frameNo);
            var from = (Point3)set.from!;
            var value = new Point3(TvgMath.Lerp(from.x, target.x, progress), TvgMath.Lerp(from.y, target.y, progress), TvgMath.Lerp(from.z, target.z, progress));
            set.cur = value;
            return value;
        }

        public RGB32 Run(LottieProperty property, float frameNo, RGB32 target)
        {
            var set = Set(property, frameNo);
            var value = RGB32.Lerp((RGB32)set.from!, target, progress);
            set.cur = value;
            return value;
        }

        public void Capture(LottieProperty property, float frameNo, RenderPath from)
        {
            var set = Set(property, frameNo);
            if (set.cur != null)
            {
                (set.from, set.cur) = (set.cur, set.from);
                return;
            }
            set.from = Copy(from);
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
            set.cur = Copy(output, pivot, from.cmds);
        }

        public void Capture(LottieProperty property, float frameNo, Fill fill)
        {
            var set = Set(property, frameNo);
            if (set.cur != null)
            {
                (set.from, set.cur) = (set.cur, set.from);
                return;
            }
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
            var result = new Fill.ColorStop[count];
            for (var i = 0; i < count; ++i)
            {
                result[i] = new Fill.ColorStop(TvgMath.Lerp(from[i].offset, target[i].offset, progress),
                    TvgMath.Lerp(from[i].r, target[i].r, progress), TvgMath.Lerp(from[i].g, target[i].g, progress),
                    TvgMath.Lerp(from[i].b, target[i].b, progress), TvgMath.Lerp(from[i].a, target[i].a, progress));
            }
            fill.SetColorStops(result, (uint)count);
            set.cur = result;
        }

        private static RenderPath Copy(RenderPath source, uint pivot = 0, Array<PathCommand>? commands = null)
        {
            var copy = new RenderPath();
            for (var i = pivot; i < source.pts.count; ++i) copy.pts.Push(source.pts[i]);
            copy.cmds.Push(commands ?? source.cmds);
            return copy;
        }
    }
}
