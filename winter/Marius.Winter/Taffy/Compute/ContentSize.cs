// Ported from taffy/src/compute/common/content_size.rs
// Generic CSS content size code shared between all CSS algorithms.

using System;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// Utilities for computing how much width/height a given node contributes to its parent's content size.
    /// </summary>
    public static class ContentSizeUtils
    {
        /// <summary>
        /// Determine how much width/height a given node contributes to its parent's content size
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float> ComputeContentSizeContribution(
            Point<float> location,
            Size<float> size,
            Size<float> contentSize,
            Point<Overflow> overflow)
        {
            var sizeContentSizeContribution = new Size<float>(
                width: overflow.X == Overflow.Visible
                    ? TaffySys.F32Max(size.Width, contentSize.Width)
                    : size.Width,
                height: overflow.Y == Overflow.Visible
                    ? TaffySys.F32Max(size.Height, contentSize.Height)
                    : size.Height
            );

            if (sizeContentSizeContribution.Width > 0f && sizeContentSizeContribution.Height > 0f)
            {
                var maxX = TaffySys.F32Max(location.X + sizeContentSizeContribution.Width, 0f);
                var minX = TaffySys.F32Min(location.X, 0f);
                var maxY = TaffySys.F32Max(location.Y + sizeContentSizeContribution.Height, 0f);
                var minY = TaffySys.F32Min(location.Y, 0f);
                return new Size<float>(maxX - minX, maxY - minY);
            }

            return SizeExtensions.ZeroF32;
        }
    }
}
