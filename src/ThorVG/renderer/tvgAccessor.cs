// Ported from ThorVG/src/renderer/tvgAccessor.cpp and ThorVG/inc/thorvg.h

using System;

namespace ThorVG
{
    /// <summary>
    /// Provides tree-traversal access over paint hierarchies.
    /// Mirrors C++ tvg::Accessor.
    /// </summary>
    public class Accessor
    {
        private Accessor() { }

        public static Accessor Gen() => new Accessor();

        /// <summary>
        /// Traverse all paints in the tree, calling func for each.
        /// If func returns false, traversal stops.
        /// </summary>
        public Result Set(Paint paint, Func<Paint, object?, bool> func, object? data = null)
        {
            if (paint == null || func == null) return Result.InvalidArguments;

            paint.Ref();

            if (!func(paint, data))
            {
                paint.Unref(false);
                return Result.Success;
            }

            var it = IteratorAccessor.GetIterator(paint);
            if (it != null) AccessChildren(it, func, data);

            paint.Unref(false);
            return Result.Success;
        }

        public static uint Id(string? name) => (uint)TvgCompressor.Djb2Encode(name);

        private static bool AccessChildren(Iterator it, Func<Paint, object?, bool> func, object? data)
        {
            Paint? child;
            while ((child = it.Next()) != null)
            {
                if (!func(child, data)) return false;

                var childIt = IteratorAccessor.GetIterator(child);
                if (childIt != null)
                {
                    if (!AccessChildren(childIt, func, data)) return false;
                }
            }
            return true;
        }
    }
}
