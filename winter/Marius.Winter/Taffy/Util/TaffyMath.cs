// Ported from taffy/src/util/math.rs
// Contains numerical helper extension methods for MaybeMath operations.

using System;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// Extension methods implementing the MaybeMath trait for float?, float, AvailableSpace, and Size types.
    ///
    /// If the left-hand value is null, these operations return null.
    /// If the right-hand value is null, it is treated as "no constraint" (identity).
    /// </summary>
    public static class MaybeMath
    {
        // =====================================================================
        // float? with float? (Option<f32> with Option<f32> -> Option<f32>)
        // =====================================================================

        /// <summary>Returns the minimum of self and rhs. None if self is None. rhs None = no constraint.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? MaybeMin(this float? self, float? rhs)
        {
            return (self, rhs) switch
            {
                (float l, float r) => MathF.Min(l, r),
                (float _, null) => self,
                (null, _) => null,
            };
        }

        /// <summary>Returns the maximum of self and rhs. None if self is None. rhs None = no constraint.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? MaybeMax(this float? self, float? rhs)
        {
            return (self, rhs) switch
            {
                (float l, float r) => MathF.Max(l, r),
                (float _, null) => self,
                (null, _) => null,
            };
        }

        /// <summary>Returns self clamped between min and max. None if self is None.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? MaybeClamp(this float? self, float? min, float? max)
        {
            return (self, min, max) switch
            {
                (float b, float mn, float mx) => MathF.Max(MathF.Min(b, mx), mn),
                (float b, null, float mx) => MathF.Min(b, mx),
                (float b, float mn, null) => MathF.Max(b, mn),
                (float _, null, null) => self,
                (null, _, _) => null,
            };
        }

        /// <summary>Adds self and rhs. None if self is None. rhs None = no change.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? MaybeAdd(this float? self, float? rhs)
        {
            return (self, rhs) switch
            {
                (float l, float r) => l + r,
                (float _, null) => self,
                (null, _) => null,
            };
        }

        /// <summary>Subtracts rhs from self. None if self is None. rhs None = no change.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? MaybeSub(this float? self, float? rhs)
        {
            return (self, rhs) switch
            {
                (float l, float r) => l - r,
                (float _, null) => self,
                (null, _) => null,
            };
        }

        // =====================================================================
        // float? with float (Option<f32> with f32 -> Option<f32>)
        // =====================================================================

        /// <summary>Returns the minimum of self and rhs. None if self is None.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? MaybeMin(this float? self, float rhs)
        {
            return self.HasValue ? MathF.Min(self.Value, rhs) : null;
        }

        /// <summary>Returns the maximum of self and rhs. None if self is None.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? MaybeMax(this float? self, float rhs)
        {
            return self.HasValue ? MathF.Max(self.Value, rhs) : null;
        }

        /// <summary>Returns self clamped between min and max. None if self is None.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? MaybeClamp(this float? self, float min, float max)
        {
            return self.HasValue ? MathF.Max(MathF.Min(self.Value, max), min) : null;
        }

        /// <summary>Adds self and rhs. None if self is None.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? MaybeAdd(this float? self, float rhs)
        {
            return self.HasValue ? self.Value + rhs : null;
        }

        /// <summary>Subtracts rhs from self. None if self is None.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? MaybeSub(this float? self, float rhs)
        {
            return self.HasValue ? self.Value - rhs : null;
        }

        // =====================================================================
        // float with float? (f32 with Option<f32> -> f32)
        // =====================================================================

        /// <summary>Returns the minimum of self and rhs. rhs null = no constraint (returns self).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MaybeMin(this float self, float? rhs)
        {
            return rhs.HasValue ? MathF.Min(self, rhs.Value) : self;
        }

        /// <summary>Returns the maximum of self and rhs. rhs null = no constraint (returns self).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MaybeMax(this float self, float? rhs)
        {
            return rhs.HasValue ? MathF.Max(self, rhs.Value) : self;
        }

        /// <summary>Returns self clamped between min and max. Null min/max = no constraint.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MaybeClamp(this float self, float? min, float? max)
        {
            return (min, max) switch
            {
                (float mn, float mx) => MathF.Max(MathF.Min(self, mx), mn),
                (null, float mx) => MathF.Min(self, mx),
                (float mn, null) => MathF.Max(self, mn),
                (null, null) => self,
            };
        }

        /// <summary>Adds self and rhs. rhs null = no change (returns self).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MaybeAdd(this float self, float? rhs)
        {
            return rhs.HasValue ? self + rhs.Value : self;
        }

        /// <summary>Subtracts rhs from self. rhs null = no change (returns self).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MaybeSub(this float self, float? rhs)
        {
            return rhs.HasValue ? self - rhs.Value : self;
        }

        // =====================================================================
        // AvailableSpace with float -> AvailableSpace
        // =====================================================================

        /// <summary>Returns the minimum of self and rhs.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AvailableSpace MaybeMin(this AvailableSpace self, float rhs)
        {
            if (self.IsDefinite())
                return AvailableSpace.Definite(MathF.Min(self.Unwrap(), rhs));
            // MinContent and MaxContent both become Definite(rhs)
            return AvailableSpace.Definite(rhs);
        }

        /// <summary>Returns the maximum of self and rhs.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AvailableSpace MaybeMax(this AvailableSpace self, float rhs)
        {
            if (self.IsDefinite())
                return AvailableSpace.Definite(MathF.Max(self.Unwrap(), rhs));
            // MinContent and MaxContent stay as-is
            return self;
        }

        /// <summary>Returns self clamped between min and max.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AvailableSpace MaybeClamp(this AvailableSpace self, float min, float max)
        {
            if (self.IsDefinite())
                return AvailableSpace.Definite(MathF.Max(MathF.Min(self.Unwrap(), max), min));
            // MinContent and MaxContent stay as-is
            return self;
        }

        /// <summary>Adds self and rhs.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AvailableSpace MaybeAdd(this AvailableSpace self, float rhs)
        {
            if (self.IsDefinite())
                return AvailableSpace.Definite(self.Unwrap() + rhs);
            return self;
        }

        /// <summary>Subtracts rhs from self.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AvailableSpace MaybeSub(this AvailableSpace self, float rhs)
        {
            if (self.IsDefinite())
                return AvailableSpace.Definite(self.Unwrap() - rhs);
            return self;
        }

        // =====================================================================
        // AvailableSpace with float? -> AvailableSpace
        // =====================================================================

        /// <summary>Returns the minimum of self and rhs. rhs null = no constraint.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AvailableSpace MaybeMin(this AvailableSpace self, float? rhs)
        {
            if (!rhs.HasValue)
            {
                // No constraint: Definite stays, MinContent stays, MaxContent stays
                return self;
            }

            float r = rhs.Value;
            if (self.IsDefinite())
                return AvailableSpace.Definite(MathF.Min(self.Unwrap(), r));
            // MinContent/MaxContent with Some(rhs) => Definite(rhs) for MinContent, Definite(rhs) for MaxContent
            // But wait: looking at the Rust source more carefully:
            // MinContent + Some(rhs) => Definite(rhs)
            // MaxContent + Some(rhs) => Definite(rhs)
            if (self == AvailableSpace.MinContent)
                return AvailableSpace.Definite(r);
            // MaxContent
            return AvailableSpace.Definite(r);
        }

        /// <summary>Returns the maximum of self and rhs. rhs null = no constraint.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AvailableSpace MaybeMax(this AvailableSpace self, float? rhs)
        {
            if (self.IsDefinite())
            {
                if (rhs.HasValue)
                    return AvailableSpace.Definite(MathF.Max(self.Unwrap(), rhs.Value));
                return self;
            }
            // MinContent and MaxContent stay as-is regardless of rhs
            return self;
        }

        /// <summary>Returns self clamped between min and max. Null min/max = no constraint.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AvailableSpace MaybeClamp(this AvailableSpace self, float? min, float? max)
        {
            if (self.IsDefinite())
            {
                float val = self.Unwrap();
                return (min, max) switch
                {
                    (float mn, float mx) => AvailableSpace.Definite(MathF.Max(MathF.Min(val, mx), mn)),
                    (null, float mx) => AvailableSpace.Definite(MathF.Min(val, mx)),
                    (float mn, null) => AvailableSpace.Definite(MathF.Max(val, mn)),
                    (null, null) => self,
                };
            }
            // MinContent and MaxContent stay as-is
            return self;
        }

        /// <summary>Adds self and rhs. rhs null = no change.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AvailableSpace MaybeAdd(this AvailableSpace self, float? rhs)
        {
            if (self.IsDefinite() && rhs.HasValue)
                return AvailableSpace.Definite(self.Unwrap() + rhs.Value);
            if (self.IsDefinite())
                return self;
            return self;
        }

        /// <summary>Subtracts rhs from self. rhs null = no change.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AvailableSpace MaybeSub(this AvailableSpace self, float? rhs)
        {
            if (self.IsDefinite() && rhs.HasValue)
                return AvailableSpace.Definite(self.Unwrap() - rhs.Value);
            if (self.IsDefinite())
                return self;
            return self;
        }

        // =====================================================================
        // Size<float?> with Size<float?> -> Size<float?>
        // =====================================================================

        /// <summary>Returns the element-wise minimum of two sizes.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float?> MaybeMin(this Size<float?> self, Size<float?> rhs)
        {
            return new Size<float?>(self.Width.MaybeMin(rhs.Width), self.Height.MaybeMin(rhs.Height));
        }

        /// <summary>Returns the element-wise maximum of two sizes.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float?> MaybeMax(this Size<float?> self, Size<float?> rhs)
        {
            return new Size<float?>(self.Width.MaybeMax(rhs.Width), self.Height.MaybeMax(rhs.Height));
        }

        /// <summary>Returns self clamped element-wise between min and max.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float?> MaybeClamp(this Size<float?> self, Size<float?> min, Size<float?> max)
        {
            return new Size<float?>(
                self.Width.MaybeClamp(min.Width, max.Width),
                self.Height.MaybeClamp(min.Height, max.Height));
        }

        /// <summary>Adds two sizes element-wise.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float?> MaybeAdd(this Size<float?> self, Size<float?> rhs)
        {
            return new Size<float?>(self.Width.MaybeAdd(rhs.Width), self.Height.MaybeAdd(rhs.Height));
        }

        /// <summary>Subtracts rhs from self element-wise.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float?> MaybeSub(this Size<float?> self, Size<float?> rhs)
        {
            return new Size<float?>(self.Width.MaybeSub(rhs.Width), self.Height.MaybeSub(rhs.Height));
        }

        // =====================================================================
        // Size<float> with Size<float?> -> Size<float>
        // =====================================================================

        /// <summary>Returns the element-wise minimum of a Size of float and a Size of float?.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float> MaybeMin(this Size<float> self, Size<float?> rhs)
        {
            return new Size<float>(self.Width.MaybeMin(rhs.Width), self.Height.MaybeMin(rhs.Height));
        }

        /// <summary>Returns the element-wise maximum of a Size of float and a Size of float?.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float> MaybeMax(this Size<float> self, Size<float?> rhs)
        {
            return new Size<float>(self.Width.MaybeMax(rhs.Width), self.Height.MaybeMax(rhs.Height));
        }

        /// <summary>Returns self clamped element-wise between min and max.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float> MaybeClamp(this Size<float> self, Size<float?> min, Size<float?> max)
        {
            return new Size<float>(
                self.Width.MaybeClamp(min.Width, max.Width),
                self.Height.MaybeClamp(min.Height, max.Height));
        }

        /// <summary>Adds a Size of float? to a Size of float element-wise.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float> MaybeAdd(this Size<float> self, Size<float?> rhs)
        {
            return new Size<float>(self.Width.MaybeAdd(rhs.Width), self.Height.MaybeAdd(rhs.Height));
        }

        /// <summary>Subtracts a Size of float? from a Size of float element-wise.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float> MaybeSub(this Size<float> self, Size<float?> rhs)
        {
            return new Size<float>(self.Width.MaybeSub(rhs.Width), self.Height.MaybeSub(rhs.Height));
        }

        // =====================================================================
        // Size<AvailableSpace> with Size<float?> -> Size<AvailableSpace>
        // =====================================================================

        /// <summary>Returns the element-wise minimum.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<AvailableSpace> MaybeMin(this Size<AvailableSpace> self, Size<float?> rhs)
        {
            return new Size<AvailableSpace>(self.Width.MaybeMin(rhs.Width), self.Height.MaybeMin(rhs.Height));
        }

        /// <summary>Returns the element-wise maximum.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<AvailableSpace> MaybeMax(this Size<AvailableSpace> self, Size<float?> rhs)
        {
            return new Size<AvailableSpace>(self.Width.MaybeMax(rhs.Width), self.Height.MaybeMax(rhs.Height));
        }

        /// <summary>Returns self clamped element-wise between min and max.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<AvailableSpace> MaybeClamp(this Size<AvailableSpace> self, Size<float?> min, Size<float?> max)
        {
            return new Size<AvailableSpace>(
                self.Width.MaybeClamp(min.Width, max.Width),
                self.Height.MaybeClamp(min.Height, max.Height));
        }

        /// <summary>Adds element-wise.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<AvailableSpace> MaybeAdd(this Size<AvailableSpace> self, Size<float?> rhs)
        {
            return new Size<AvailableSpace>(self.Width.MaybeAdd(rhs.Width), self.Height.MaybeAdd(rhs.Height));
        }

        /// <summary>Subtracts element-wise.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<AvailableSpace> MaybeSub(this Size<AvailableSpace> self, Size<float?> rhs)
        {
            return new Size<AvailableSpace>(self.Width.MaybeSub(rhs.Width), self.Height.MaybeSub(rhs.Height));
        }

        // =====================================================================
        // Size<AvailableSpace> with Size<float> -> Size<AvailableSpace>
        // =====================================================================

        /// <summary>Returns the element-wise minimum.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<AvailableSpace> MaybeMin(this Size<AvailableSpace> self, Size<float> rhs)
        {
            return new Size<AvailableSpace>(self.Width.MaybeMin(rhs.Width), self.Height.MaybeMin(rhs.Height));
        }

        /// <summary>Returns the element-wise maximum.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<AvailableSpace> MaybeMax(this Size<AvailableSpace> self, Size<float> rhs)
        {
            return new Size<AvailableSpace>(self.Width.MaybeMax(rhs.Width), self.Height.MaybeMax(rhs.Height));
        }

        /// <summary>Returns self clamped element-wise between min and max.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<AvailableSpace> MaybeClamp(this Size<AvailableSpace> self, Size<float> min, Size<float> max)
        {
            return new Size<AvailableSpace>(
                self.Width.MaybeClamp(min.Width, max.Width),
                self.Height.MaybeClamp(min.Height, max.Height));
        }

        /// <summary>Adds element-wise.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<AvailableSpace> MaybeAdd(this Size<AvailableSpace> self, Size<float> rhs)
        {
            return new Size<AvailableSpace>(self.Width.MaybeAdd(rhs.Width), self.Height.MaybeAdd(rhs.Height));
        }

        /// <summary>Subtracts element-wise.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<AvailableSpace> MaybeSub(this Size<AvailableSpace> self, Size<float> rhs)
        {
            return new Size<AvailableSpace>(self.Width.MaybeSub(rhs.Width), self.Height.MaybeSub(rhs.Height));
        }
    }
}
