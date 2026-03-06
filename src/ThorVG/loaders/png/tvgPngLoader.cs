// Ported from ThorVG/src/loaders/png/tvgPngLoader.h and tvgPngLoader.cpp

using System;

namespace ThorVG
{
    /// <summary>
    /// PNG image loader. Mirrors C++ PngLoader.
    /// Uses the ported LodePNG decoder for decoding.
    /// </summary>
    public class PngLoader : ImageLoader
    {
        private byte[]? data;
        private int size;

        public PngLoader() : base(FileType.Png)
        {
        }

        /************************************************************************/
        /* Internal Implementation                                              */
        /************************************************************************/

        private void Run()
        {
            if (LodePng.Decode(data!, size, out var pixels, out var width, out var height) != 0)
            {
                TvgCommon.TVGERR("PNG", "Failed to decode image");
                return;
            }

            surface.data = pixels;
            surface.Pin();

            // setup the surface
            surface.stride = width;
            surface.w = width;
            surface.h = height;
            surface.cs = ColorSpace.ABGR8888S;
            surface.channelSize = sizeof(uint);
        }

        /************************************************************************/
        /* External Implementation                                              */
        /************************************************************************/

        public override bool Open(string path)
        {
            var fileData = ReadFile(path);
            if (fileData == null || fileData.Length == 0) return false;

            data = fileData;
            size = fileData.Length;

            if (LodePng.Inspect(data, size, out var width, out var height) != 0)
                return false;

            w = width;
            h = height;
            return true;
        }

        public override bool Open(byte[] data, uint size, string? rpath, bool copy)
        {
            if (LodePng.Inspect(data, (int)size, out var width, out var height) != 0)
                return false;

            if (copy)
            {
                this.data = new byte[size];
                Array.Copy(data, this.data, size);
            }
            else
            {
                this.data = data;
            }

            w = width;
            h = height;
            this.size = (int)size;

            return true;
        }

        public override bool Read()
        {
            if (data == null || w == 0 || h == 0) return false;

            if (!base.Read()) return true;

            // Synchronous decode (mirrors TaskScheduler::request in single-threaded mode)
            Run();

            return true;
        }

        public override unsafe RenderSurface? Bitmap()
        {
            return base.Bitmap();
        }
    }
}
