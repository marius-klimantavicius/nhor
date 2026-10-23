// Ported from ThorVG/src/loaders/raw/tvgRawLoader.h and tvgRawLoader.cpp

using System;

namespace ThorVG
{
    /// <summary>
    /// Raw bitmap image loader. Mirrors C++ RawLoader.
    /// Wraps a caller-provided uint32 pixel buffer as an ImageLoader surface.
    /// </summary>
    public class RawLoader : BitmapLoader
    {
        public bool copy;

        public RawLoader() : base(FileType.Raw)
        {
        }

        /// <summary>
        /// Open from a raw pixel data buffer.
        /// </summary>
        public bool Open(uint[] data, uint w, uint h, ColorSpace cs, bool copy)
            => Open(data, w, h, cs, copy ? Ownership.Copy : Ownership.Borrow);

        public bool Open(uint[] data, uint w, uint h, ColorSpace cs, Ownership owner)
        {
            // A managed array supplies a length which the native pointer API
            // cannot check. Reject an undersized first buffer safely without
            // consuming the loader. Otherwise retain native readied ordering:
            // failed null/zero opens consume it; subsequent opens are no-ops.
            if (!readied && data != null && (ulong)w * h > (ulong)data.LongLength) return false;
            if (!base.Read()) return true;
            if (data == null || w == 0 || h == 0) return false;

            this.w = w;
            this.h = h;
            this.owner = owner;
            this.copy = owner == Ownership.Copy;

            if (this.copy)
            {
                var len = (int)(w * h);
                surface.data = new uint[len];
                Array.Copy(data, surface.data, len);
            }
            else
            {
                surface.data = data;
            }

            surface.Setup(surface.data!, w, w, h, sizeof(uint), cs);

            return true;
        }

        public override bool Read()
        {
            base.Read();
            return true;
        }
    }
}
