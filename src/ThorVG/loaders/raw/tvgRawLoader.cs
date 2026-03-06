// Ported from ThorVG/src/loaders/raw/tvgRawLoader.h and tvgRawLoader.cpp

using System;

namespace ThorVG
{
    /// <summary>
    /// Raw bitmap image loader. Mirrors C++ RawLoader.
    /// Wraps a caller-provided uint32 pixel buffer as an ImageLoader surface.
    /// </summary>
    public class RawLoader : ImageLoader
    {
        public bool copy;

        public RawLoader() : base(FileType.Raw)
        {
        }

        /// <summary>
        /// Open from a raw pixel data buffer.
        /// </summary>
        public bool Open(uint[] data, uint w, uint h, ColorSpace cs, bool copy)
        {
            if (!base.Read()) return true;

            if (data == null || data.Length == 0 || w == 0 || h == 0) return false;

            this.w = w;
            this.h = h;
            this.copy = copy;

            if (copy)
            {
                var len = (int)(w * h);
                surface.data = new uint[len];
                Array.Copy(data, surface.data, len);
            }
            else
            {
                surface.data = data;
            }

            surface.Pin();

            // setup the surface
            surface.stride = w;
            surface.w = w;
            surface.h = h;
            surface.cs = cs;
            surface.channelSize = sizeof(uint);
            surface.premultiplied = (cs == ColorSpace.ABGR8888 || cs == ColorSpace.ARGB8888);

            return true;
        }

        public override bool Read()
        {
            base.Read();
            return true;
        }
    }
}
