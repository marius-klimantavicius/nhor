// Ported from taffy/src/compute/common/alignment.rs
// Generic CSS alignment code shared between Flexbox and CSS Grid algorithms.

using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// Generic CSS alignment utilities shared between both the Flexbox and CSS Grid algorithms.
    /// </summary>
    public static class AlignmentUtils
    {
        /// <summary>
        /// Implement fallback alignment.
        ///
        /// In addition to the spec at https://www.w3.org/TR/css-align-3/ this implementation follows
        /// the resolution of https://github.com/w3c/csswg-drafts/issues/10154
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AlignContent ApplyAlignmentFallback(
            float freeSpace,
            int numItems,
            AlignContent alignmentMode,
            bool isSafe)
        {
            // Fallback occurs in two cases:

            // 1. If there is only a single item being aligned and alignment is a distributed alignment keyword
            //    https://www.w3.org/TR/css-align-3/#distribution-values
            if (numItems <= 1 || freeSpace <= 0f)
            {
                switch (alignmentMode)
                {
                    case AlignContent.Stretch:
                        alignmentMode = AlignContent.FlexStart;
                        isSafe = true;
                        break;
                    case AlignContent.SpaceBetween:
                        alignmentMode = AlignContent.FlexStart;
                        isSafe = true;
                        break;
                    case AlignContent.SpaceAround:
                        alignmentMode = AlignContent.Center;
                        isSafe = true;
                        break;
                    case AlignContent.SpaceEvenly:
                        alignmentMode = AlignContent.Center;
                        isSafe = true;
                        break;
                }
            }

            // 2. If free space is negative the "safe" alignment variants all fallback to Start alignment
            if (freeSpace <= 0f && isSafe)
            {
                alignmentMode = AlignContent.Start;
            }

            return alignmentMode;
        }

        /// <summary>
        /// Generic alignment function that is used:
        ///   - For both align-content and justify-content alignment
        ///   - For both the Flexbox and CSS Grid algorithms
        ///
        /// CSS Grid does not apply gaps as part of alignment, so the gap parameter should
        /// always be set to zero for CSS Grid.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ComputeAlignmentOffset(
            float freeSpace,
            int numItems,
            float gap,
            AlignContent alignmentMode,
            bool layoutIsFlexReversed,
            bool isFirst)
        {
            if (isFirst)
            {
                return alignmentMode switch
                {
                    AlignContent.Start => 0f,
                    AlignContent.FlexStart => layoutIsFlexReversed ? freeSpace : 0f,
                    AlignContent.End => freeSpace,
                    AlignContent.FlexEnd => layoutIsFlexReversed ? 0f : freeSpace,
                    AlignContent.Center => freeSpace / 2f,
                    AlignContent.Stretch => 0f,
                    AlignContent.SpaceBetween => 0f,
                    AlignContent.SpaceAround => freeSpace >= 0f
                        ? (freeSpace / numItems) / 2f
                        : freeSpace / 2f,
                    AlignContent.SpaceEvenly => freeSpace >= 0f
                        ? freeSpace / (numItems + 1)
                        : freeSpace / 2f,
                    _ => 0f,
                };
            }
            else
            {
                float clampedFreeSpace = freeSpace > 0f ? freeSpace : 0f;
                return gap + alignmentMode switch
                {
                    AlignContent.Start => 0f,
                    AlignContent.FlexStart => 0f,
                    AlignContent.End => 0f,
                    AlignContent.FlexEnd => 0f,
                    AlignContent.Center => 0f,
                    AlignContent.Stretch => 0f,
                    AlignContent.SpaceBetween => clampedFreeSpace / (numItems - 1),
                    AlignContent.SpaceAround => clampedFreeSpace / numItems,
                    AlignContent.SpaceEvenly => clampedFreeSpace / (numItems + 1),
                    _ => 0f,
                };
            }
        }
    }
}
