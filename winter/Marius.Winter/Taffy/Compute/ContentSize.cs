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
                    ? MathF.Max(size.Width, contentSize.Width)
                    : size.Width,
                height: overflow.Y == Overflow.Visible
                    ? MathF.Max(size.Height, contentSize.Height)
                    : size.Height
            );

            if (sizeContentSizeContribution.Width > 0f && sizeContentSizeContribution.Height > 0f)
            {
                return new Size<float>(
                    location.X + sizeContentSizeContribution.Width,
                    location.Y + sizeContentSizeContribution.Height
                );
            }

            return SizeExtensions.ZeroF32;
        }
    }
}
