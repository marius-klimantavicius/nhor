// Ported from taffy/src/tree/cache.rs
// A cache for storing the results of layout computation

using System;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>Space-optimized cache key that packs bits into as small a size as possible</summary>
    internal readonly struct CacheKey : IEquatable<CacheKey>
    {
        /// <summary><see cref="float.PositiveInfinity"/> as a uint</summary>
        private const uint INFINITY_BITS = 0x7F800000;
        /// <summary><see cref="float.NegativeInfinity"/> as a uint</summary>
        private const uint NEG_INFINITY_BITS = 0xFF800000;

        // Parent sizes are non-negative, so their sign bits encode RequestedAxis.
        private const ulong SIGN_BIT_1 = 1UL << 63;
        private const ulong SIGN_BIT_2 = 1UL << 31;
        private const ulong BOTH_SIGN_BITS_MASK = SIGN_BIT_1 | SIGN_BIT_2;
        private const ulong NON_SIGN_BITS_MASK = ~BOTH_SIGN_BITS_MASK;
        private const ulong X_AXIS_VALUE_MASK = (ulong)uint.MaxValue << 32;

        /// <summary>The known dimensions and available space</summary>
        public readonly ulong KnownDimensionsAvailableSpace;
        /// <summary>The parent size with the requested axis packed into its sign bits</summary>
        public readonly ulong ParentSizeBits;

        private CacheKey(ulong knownDimensionsAvailableSpace, ulong parentSizeBits)
        {
            KnownDimensionsAvailableSpace = knownDimensionsAvailableSpace;
            ParentSizeBits = parentSizeBits;
        }

        /// <summary>Pack a nullable float into a uint</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint OptionCacheKey(float? input)
        {
            return input.HasValue ? BitConverter.SingleToUInt32Bits(input.Value) : INFINITY_BITS;
        }

        /// <summary>Pack nullable dimensions into a ulong</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong SizeOptionCacheKey(Size<float?> input)
        {
            return ((ulong)OptionCacheKey(input.Width) << 32) | OptionCacheKey(input.Height);
        }

        /// <summary>Pack available space into a uint</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint AvailableSpaceCacheKey(AvailableSpace input)
        {
            if (input == AvailableSpace.MinContent)
                return NEG_INFINITY_BITS;
            if (input == AvailableSpace.MaxContent)
                return INFINITY_BITS;
            return BitConverter.SingleToUInt32Bits(-input.Unwrap());
        }

        /// <summary>Pack available-space dimensions into a ulong</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong SizeAvailableSpaceCacheKey(Size<AvailableSpace> input)
        {
            return ((ulong)AvailableSpaceCacheKey(input.Width) << 32) | AvailableSpaceCacheKey(input.Height);
        }

        /// <summary>Encode a known dimension or its available space into one cache-key dimension</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MixedCacheKey(float? knownDimension, AvailableSpace availableSpace)
        {
            return knownDimension.HasValue
                ? BitConverter.SingleToUInt32Bits(knownDimension.Value)
                : AvailableSpaceCacheKey(availableSpace);
        }

        /// <summary>Encode known dimensions and available space into a cache key</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong SizeMixedCacheKey(Size<float?> knownDimensions, Size<AvailableSpace> availableSpace)
        {
            return ((ulong)MixedCacheKey(knownDimensions.Width, availableSpace.Width) << 32)
                | MixedCacheKey(knownDimensions.Height, availableSpace.Height);
        }

        /// <summary>Create a cache key from layout input</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CacheKey From(in LayoutInput input)
        {
            ulong extraBits = input.Axis switch
            {
                RequestedAxis.Horizontal => SIGN_BIT_1,
                RequestedAxis.Vertical => SIGN_BIT_2,
                RequestedAxis.Both => SIGN_BIT_1 | SIGN_BIT_2,
                _ => throw new ArgumentOutOfRangeException(nameof(input.Axis)),
            };

            return new CacheKey(
                SizeMixedCacheKey(input.KnownDimensions, input.AvailableSpace),
                (SizeOptionCacheKey(input.ParentSize) & NON_SIGN_BITS_MASK) | extraBits);
        }

        /// <summary>Return the parent size with the requested-axis bits masked out</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ParentSize()
        {
            return ParentSizeBits & NON_SIGN_BITS_MASK;
        }

        /// <summary>Return the parent size with requested-axis bits and the y-axis value masked out</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong XAxisParentSize()
        {
            return ParentSizeBits & (X_AXIS_VALUE_MASK & NON_SIGN_BITS_MASK);
        }

        public bool Equals(CacheKey other)
        {
            return KnownDimensionsAvailableSpace == other.KnownDimensionsAvailableSpace
                && ParentSizeBits == other.ParentSizeBits;
        }

        public override bool Equals(object? obj) => obj is CacheKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(KnownDimensionsAvailableSpace, ParentSizeBits);

        public static bool operator ==(CacheKey left, CacheKey right) => left.Equals(right);

        public static bool operator !=(CacheKey left, CacheKey right) => !left.Equals(right);
    }

    /// <summary>
    /// Cached intermediate layout results
    /// </summary>
    internal struct CacheEntry<T>
    {
        /// <summary>The key for the cache entry</summary>
        public CacheKey Key;
        /// <summary>The cached size and baselines of the item</summary>
        public T Content;
    }

    /// <summary>
    /// The number of cache entries for each node in the tree
    /// </summary>
    public static class CacheConstants
    {
        /// <summary>The number of cache entries for each node in the tree</summary>
        public const int CACHE_SIZE = 9;
    }

    /// <summary>
    /// Fixed-size inline array of 9 nullable cache entries, avoiding heap allocation.
    /// </summary>
    [InlineArray(CacheConstants.CACHE_SIZE)]
    internal struct MeasureCacheEntries
    {
        private CacheEntry<Size<float>>? _element0;
    }

    /// <summary>
    /// A cache for caching the results of a sizing a Grid Item or Flexbox Item
    /// </summary>
    public struct Cache
    {
        /// <summary>The cache entry for the node's final layout</summary>
        private CacheEntry<LayoutOutput>? _finalLayoutEntry;
        /// <summary>The cache entries for the node's preliminary size measurements</summary>
        private MeasureCacheEntries _measureEntries;
        /// <summary>Tracks if all cache entries are empty</summary>
        private bool _isEmpty;
        /// <summary>Tracks if measure entries have been populated</summary>
        private bool _hasMeasureEntries;

        /// <summary>Create a new empty cache</summary>
        public static Cache New()
        {
            return new Cache
            {
                _finalLayoutEntry = null,
                _measureEntries = default,
                _isEmpty = true,
                _hasMeasureEntries = false,
            };
        }

        /// <summary>
        /// Return the cache slot to cache the current computed result in.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ComputeCacheSlot(Size<float?> knownDimensions, Size<AvailableSpace> availableSpace)
        {
            bool hasKnownWidth = knownDimensions.Width.HasValue;
            bool hasKnownHeight = knownDimensions.Height.HasValue;

            // Slot 0: Both known_dimensions were set
            if (hasKnownWidth && hasKnownHeight)
                return 0;

            // Slot 1-2: width but not height known
            if (hasKnownWidth && !hasKnownHeight)
                return 1 + (availableSpace.Height == AvailableSpace.MinContent ? 1 : 0);

            // Slot 3-4: height but not width known
            if (hasKnownHeight && !hasKnownWidth)
                return 3 + (availableSpace.Width == AvailableSpace.MinContent ? 1 : 0);

            // Slots 5-8: Neither known_dimensions were set
            bool widthIsMinContent = availableSpace.Width == AvailableSpace.MinContent;
            bool heightIsMinContent = availableSpace.Height == AvailableSpace.MinContent;

            if (!widthIsMinContent && !heightIsMinContent) return 5;
            if (!widthIsMinContent && heightIsMinContent) return 6;
            if (widthIsMinContent && !heightIsMinContent) return 7;
            return 8;
        }

        /// <summary>Try to retrieve a cached result from the cache</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LayoutOutput? Get(in LayoutInput input)
        {
            var key = CacheKey.From(input);
            switch (input.RunMode)
            {
                case RunMode.PerformLayout:
                {
                    if (_finalLayoutEntry.HasValue)
                    {
                        var entry = _finalLayoutEntry.Value;
                        if (entry.Key == key)
                            return entry.Content;
                    }
                    return null;
                }
                case RunMode.ComputeSize:
                {
                    if (_hasMeasureEntries)
                    {
                        for (int i = 0; i < CacheConstants.CACHE_SIZE; i++)
                        {
                            ref var slot = ref _measureEntries[i];
                            if (!slot.HasValue)
                                continue;

                            var entry = slot.Value;
                            if (entry.Key.KnownDimensionsAvailableSpace == key.KnownDimensionsAvailableSpace
                                && entry.Key.XAxisParentSize() == key.XAxisParentSize())
                            {
                                return LayoutOutput.FromOuterSize(entry.Content);
                            }
                        }
                    }
                    return null;
                }
                case RunMode.PerformHiddenLayout:
                default:
                    return null;
            }
        }

        /// <summary>Store a computed size in the cache</summary>
        public void Store(in LayoutInput input, LayoutOutput layoutOutput)
        {
            var key = CacheKey.From(input);
            switch (input.RunMode)
            {
                case RunMode.PerformLayout:
                    _isEmpty = false;
                    _finalLayoutEntry = new CacheEntry<LayoutOutput>
                    {
                        Key = key,
                        Content = layoutOutput,
                    };
                    break;
                case RunMode.ComputeSize:
                    _isEmpty = false;
                    _hasMeasureEntries = true;
                    int cacheSlot = ComputeCacheSlot(input.KnownDimensions, input.AvailableSpace);
                    _measureEntries[cacheSlot] = new CacheEntry<Size<float>>
                    {
                        Key = key,
                        Content = layoutOutput.Size,
                    };
                    break;
                case RunMode.PerformHiddenLayout:
                    break;
            }
        }

        /// <summary>Clear all cache entries and report clear operation outcome</summary>
        public ClearState Clear()
        {
            if (_isEmpty)
                return ClearState.AlreadyEmpty;

            _isEmpty = true;
            _finalLayoutEntry = null;
            if (_hasMeasureEntries)
            {
                for (int i = 0; i < CacheConstants.CACHE_SIZE; i++)
                    _measureEntries[i] = null;
                _hasMeasureEntries = false;
            }
            return ClearState.Cleared;
        }

        /// <summary>Returns true if all cache entries are None, else false</summary>
        public bool IsEmpty()
        {
            if (_finalLayoutEntry.HasValue)
                return false;

            if (_hasMeasureEntries)
            {
                for (int i = 0; i < CacheConstants.CACHE_SIZE; i++)
                {
                    if (_measureEntries[i].HasValue)
                        return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Clear operation outcome
    /// </summary>
    public enum ClearState
    {
        /// <summary>Cleared some values</summary>
        Cleared,
        /// <summary>Everything was already cleared</summary>
        AlreadyEmpty,
    }
}
