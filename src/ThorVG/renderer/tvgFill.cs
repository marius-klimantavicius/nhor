// Ported from ThorVG/src/renderer/tvgFill.h and ThorVG/inc/thorvg.h

using System;

namespace ThorVG
{
    /// <summary>
    /// Abstract gradient fill. Mirrors C++ tvg::Fill.
    /// </summary>
    public abstract class Fill
    {
        /// <summary>Color stop definition.</summary>
        public struct ColorStop
        {
            public float offset;
            public byte r, g, b, a;

            public ColorStop(float offset, byte r, byte g, byte b, byte a)
            {
                this.offset = offset;
                this.r = r; this.g = g; this.b = b; this.a = a;
            }
        }

        internal FillImpl pImpl;

        protected Fill()
        {
            pImpl = new FillImpl();
        }

        public Result SetColorStops(ColorStop[]? colorStops, uint cnt)
        {
            return pImpl.Update(colorStops, cnt);
        }

        public Result SetSpread(FillSpread s)
        {
            pImpl.spread = s;
            return Result.Success;
        }

        public Result SetTransform(in Matrix m)
        {
            pImpl.transform = m;
            return Result.Success;
        }

        public uint GetColorStops(out ColorStop[]? colorStops)
        {
            colorStops = pImpl.colorStops;
            return pImpl.cnt;
        }

        public FillSpread GetSpread() => pImpl.spread;

        public ref Matrix GetTransform() => ref pImpl.transform;

        public abstract Fill Duplicate();
        public abstract Type GetFillType();
    }

    /// <summary>Internal fill implementation data. Mirrors C++ Fill::Impl.</summary>
    internal class FillImpl
    {
        public Fill.ColorStop[]? colorStops;
        public Matrix transform = TvgMath.Identity();
        public ushort cnt;
        public FillSpread spread = FillSpread.Pad;

        public void Copy(FillImpl dup)
        {
            cnt = dup.cnt;
            spread = dup.spread;
            if (dup.cnt > 0 && dup.colorStops != null)
            {
                colorStops = new Fill.ColorStop[dup.cnt];
                System.Array.Copy(dup.colorStops, colorStops, dup.cnt);
            }
            else
            {
                colorStops = null;
            }
            transform = dup.transform;
        }

        public Result Update(Fill.ColorStop[]? stops, uint cnt)
        {
            if (stops == null && cnt > 0) return Result.InvalidArguments;
            if (stops != null && cnt == 0) return Result.InvalidArguments;

            if (cnt == 0)
            {
                colorStops = null;
                this.cnt = 0;
                return Result.Success;
            }

            colorStops = new Fill.ColorStop[cnt];
            System.Array.Copy(stops!, colorStops, cnt);
            this.cnt = (ushort)cnt;
            return Result.Success;
        }
    }

    /// <summary>Linear gradient fill. Mirrors C++ tvg::LinearGradient.</summary>
    public class LinearGradient : Fill
    {
        internal Point p1, p2;

        private LinearGradient() { }

        public Result Linear(float x1, float y1, float x2, float y2)
        {
            p1 = new Point(x1, y1);
            p2 = new Point(x2, y2);
            return Result.Success;
        }

        public Result Linear(out float x1, out float y1, out float x2, out float y2)
        {
            x1 = p1.x; y1 = p1.y;
            x2 = p2.x; y2 = p2.y;
            return Result.Success;
        }

        public static LinearGradient Gen() => new LinearGradient();

        public override Type GetFillType() => Type.LinearGradient;

        public override Fill Duplicate()
        {
            var ret = Gen();
            ret.pImpl.Copy(this.pImpl);
            ret.p1 = p1;
            ret.p2 = p2;
            return ret;
        }
    }

    /// <summary>Radial gradient fill. Mirrors C++ tvg::RadialGradient.</summary>
    public class RadialGradient : Fill
    {
        internal Point center, focal;
        internal float r, fr;

        private RadialGradient() { }

        public Result Radial(float cx, float cy, float r, float fx, float fy, float fr)
        {
            if (r < 0 || fr < 0) return Result.InvalidArguments;
            center = new Point(cx, cy);
            this.r = r;
            focal = new Point(fx, fy);
            this.fr = fr;
            return Result.Success;
        }

        public Result Radial(out float cx, out float cy, out float radius,
                             out float fx, out float fy, out float fradius)
        {
            cx = center.x; cy = center.y; radius = r;
            fx = focal.x; fy = focal.y; fradius = fr;
            return Result.Success;
        }

        public static RadialGradient Gen() => new RadialGradient();

        public override Type GetFillType() => Type.RadialGradient;

        /// <summary>
        /// Clamp focal point and shrink start circle if needed to avoid invalid gradient setup.
        /// Mirrors C++ RadialGradient::Impl::correct().
        /// </summary>
        internal bool Correct(ref float fx, ref float fy, ref float fr)
        {
            const float PRECISION = 0.01f;
            if (r < PRECISION) return false; // too small, treated as solid fill

            var dist = TvgMath.PointLength(center, focal);

            // clamp focal point to inside end circle if outside
            if (r - dist < PRECISION)
            {
                var diff = new Point(center.x - focal.x, center.y - focal.y);
                if (dist < PRECISION) { dist = diff.x = PRECISION; }
                var scale = r * (1.0f - PRECISION) / dist;
                diff = new Point(diff.x * scale, diff.y * scale);
                dist *= scale; // update effective dist after scaling
                fx = center.x - diff.x;
                fy = center.y - diff.y;
            }
            else
            {
                fx = focal.x;
                fy = focal.y;
            }

            // ensure start circle radius fr doesn't exceed the difference
            var maxFr = (r - dist) * (1.0f - PRECISION);
            fr = (this.fr > maxFr) ? MathF.Max(0.0f, maxFr) : this.fr;
            return true;
        }

        public override Fill Duplicate()
        {
            var ret = Gen();
            ret.pImpl.Copy(this.pImpl);
            ret.center = center;
            ret.r = r;
            ret.focal = focal;
            ret.fr = fr;
            return ret;
        }
    }
}
