// Ported from ThorVG/src/renderer/tvgIteratorAccessor.h

namespace ThorVG
{
    /// <summary>
    /// Utility class for accessing paint iterators.
    /// Mirrors C++ tvg::IteratorAccessor.
    /// </summary>
    public static class IteratorAccessor
    {
        public static Iterator? GetIterator(Paint? paint)
        {
            if (paint == null) return null;
            return paint.pImpl.GetIterator();
        }
    }
}
