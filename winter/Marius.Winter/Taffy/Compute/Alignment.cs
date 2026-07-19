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
        /// Resolves the safe/unsafe overflow-position fallback for a self-level alignment value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AlignItemsKeyword ResolveSelfAlignmentSafety(AlignItems alignment, bool overflows)
        {
            return alignment.IsSafe && overflows ? AlignItemsKeyword.Start : alignment.Keyword;
        }

        /// <summary>
        /// Resolve any spec-defined fallbacks for the alignment value.
        ///
        /// In addition to the spec at https://www.w3.org/TR/css-align-3/ this implementation follows
        /// the resolution of https://github.com/w3c/csswg-drafts/issues/10154
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AlignContentKeyword ApplyAlignmentFallback(
            float freeSpace,
            int numItems,
            AlignContent alignmentMode)
        {
            var keyword = alignmentMode.Keyword;
            var isSafe = alignmentMode.IsSafe;

            // Distributed keywords fall back to positional keywords and gain implicit safe semantics.
            //    https://www.w3.org/TR/css-align-3/#distribution-values
            if (numItems <= 1 || freeSpace <= 0f)
            {
                switch (keyword)
                {
                    case AlignContentKeyword.Stretch:
                    case AlignContentKeyword.SpaceBetween:
                        keyword = AlignContentKeyword.FlexStart;
                        isSafe = true;
                        break;
                    case AlignContentKeyword.SpaceAround:
                    case AlignContentKeyword.SpaceEvenly:
                        keyword = AlignContentKeyword.Center;
                        isSafe = true;
                        break;
                }
            }

            // Safe alignment falls back to Start whenever the alignment subject overflows.
            if (freeSpace <= 0f && isSafe)
            {
                keyword = AlignContentKeyword.Start;
            }

            return keyword;
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
            AlignContentKeyword alignmentMode,
            bool layoutIsFlexReversed,
            bool isFirst)
        {
            if (isFirst)
            {
                return alignmentMode switch
                {
                    AlignContentKeyword.Start => 0f,
                    AlignContentKeyword.FlexStart => layoutIsFlexReversed ? freeSpace : 0f,
                    AlignContentKeyword.End => freeSpace,
                    AlignContentKeyword.FlexEnd => layoutIsFlexReversed ? 0f : freeSpace,
                    AlignContentKeyword.Center => freeSpace / 2f,
                    AlignContentKeyword.Stretch => 0f,
                    AlignContentKeyword.SpaceBetween => 0f,
                    AlignContentKeyword.SpaceAround => freeSpace >= 0f
                        ? (freeSpace / numItems) / 2f
                        : freeSpace / 2f,
                    AlignContentKeyword.SpaceEvenly => freeSpace >= 0f
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
                    AlignContentKeyword.Start => 0f,
                    AlignContentKeyword.FlexStart => 0f,
                    AlignContentKeyword.End => 0f,
                    AlignContentKeyword.FlexEnd => 0f,
                    AlignContentKeyword.Center => 0f,
                    AlignContentKeyword.Stretch => 0f,
                    AlignContentKeyword.SpaceBetween => clampedFreeSpace / (numItems - 1),
                    AlignContentKeyword.SpaceAround => clampedFreeSpace / numItems,
                    AlignContentKeyword.SpaceEvenly => clampedFreeSpace / (numItems + 1),
                    _ => 0f,
                };
            }
        }
    }
}
