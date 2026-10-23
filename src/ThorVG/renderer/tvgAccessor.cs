// Ported from ThorVG/src/renderer/tvgAccessor.cpp and ThorVG/inc/thorvg.h

using System;

namespace ThorVG
{
    public sealed class AccessorEntity
    {
        public uint id;
        public Paint paint = null!;
        public string? name;
    }

    /// <summary>
    /// Provides tree-traversal access over paint hierarchies.
    /// Mirrors C++ tvg::Accessor.
    /// </summary>
    public class Accessor
    {
        private Picture? accessiblePicture;

        private Accessor() { }

        public static Accessor Gen() => new Accessor();

        /// <summary>
        /// Traverse all paints in the tree, calling func for each.
        /// If func returns false, traversal stops.
        /// </summary>
        public Result Set(Paint paint, Func<Paint, object?, bool> func, object? data = null)
        {
            if (paint == null || func == null) return Result.InvalidArguments;

            accessiblePicture = paint is Picture picture && picture.accessible ? picture : null;
            paint.Ref();

            try
            {
                if (accessiblePicture != null)
                {
                    accessiblePicture.Access(func, data);
                    return Result.Success;
                }

                if (!func(paint, data)) return Result.Success;

                var it = paint.pImpl.GetIterator();
                if (it != null) AccessChildren(it, func, data);
                return Result.Success;
            }
            finally
            {
                // Managed callbacks can throw. Release the temporary owner
                // and invalidate callback-only name access on every exit.
                paint.Unref(false);
                accessiblePicture = null;
            }
        }

        public static uint Id(string? name) => (uint)TvgCompressor.Djb2EncodeNative(name);

        public string? Name(uint id)
        {
            if (accessiblePicture == null)
            {
                TvgCommon.TVGLOG("RENDERER", "Did you enable Picture.accessible?");
                return null;
            }

            return accessiblePicture.Access(id)?.name;
        }

        private static bool AccessChildren(Iterator it, Func<Paint, object?, bool> func, object? data)
        {
            Paint? child;
            while ((child = it.Next()) != null)
            {
                if (!func(child, data)) return false;

                var childIt = child?.pImpl.GetIterator();
                if (childIt != null)
                {
                    if (!AccessChildren(childIt, func, data)) return false;
                }
            }
            return true;
        }
    }
}
