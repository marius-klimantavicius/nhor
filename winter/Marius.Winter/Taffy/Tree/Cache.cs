// Ported from taffy/src/tree/cache.rs
// A cache for storing the results of layout computation

using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// Cached intermediate layout results
    /// </summary>
    internal struct CacheEntry<T>
    {
        /// <summary>The initial cached size of the node itself</summary>
        public Size<float?> KnownDimensions;
        /// <summary>The initial cached size of the parent's node</summary>
        public Size<AvailableSpace> AvailableSpace;
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
            var knownDimensions = input.KnownDimensions;
            var availableSpace = input.AvailableSpace;
            switch (input.RunMode)
            {
                case RunMode.PerformLayout:
                {
                    if (_finalLayoutEntry.HasValue)
                    {
                        var entry = _finalLayoutEntry.Value;
                        var cachedSize = entry.Content.Size;
                        if ((knownDimensions.Width == entry.KnownDimensions.Width
                             || knownDimensions.Width == cachedSize.Width)
                            && (knownDimensions.Height == entry.KnownDimensions.Height
                                || knownDimensions.Height == cachedSize.Height)
                            && (knownDimensions.Width.HasValue
                                || entry.AvailableSpace.Width.IsRoughlyEqual(availableSpace.Width))
                            && (knownDimensions.Height.HasValue
                                || entry.AvailableSpace.Height.IsRoughlyEqual(availableSpace.Height)))
                        {
                            return entry.Content;
                        }
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
                            var cachedSize = entry.Content;

                            if ((knownDimensions.Width == entry.KnownDimensions.Width
                                 || knownDimensions.Width == cachedSize.Width)
                                && (knownDimensions.Height == entry.KnownDimensions.Height
                                    || knownDimensions.Height == cachedSize.Height)
                                && (knownDimensions.Width.HasValue
                                    || entry.AvailableSpace.Width.IsRoughlyEqual(availableSpace.Width))
                                && (knownDimensions.Height.HasValue
                                    || entry.AvailableSpace.Height.IsRoughlyEqual(availableSpace.Height)))
                            {
                                return LayoutOutput.FromOuterSize(cachedSize);
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
            var knownDimensions = input.KnownDimensions;
            var availableSpace = input.AvailableSpace;
            switch (input.RunMode)
            {
                case RunMode.PerformLayout:
                    _isEmpty = false;
                    _finalLayoutEntry = new CacheEntry<LayoutOutput>
                    {
                        KnownDimensions = knownDimensions,
                        AvailableSpace = availableSpace,
                        Content = layoutOutput,
                    };
                    break;
                case RunMode.ComputeSize:
                    _isEmpty = false;
                    _hasMeasureEntries = true;
                    int cacheSlot = ComputeCacheSlot(knownDimensions, availableSpace);
                    _measureEntries[cacheSlot] = new CacheEntry<Size<float>>
                    {
                        KnownDimensions = knownDimensions,
                        AvailableSpace = availableSpace,
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
