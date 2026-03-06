// Ported from ThorVG/src/loaders/jpg/tvgJpgLoader.h and tvgJpgLoader.cpp

using System;

namespace ThorVG
{
    /// <summary>
    /// JPEG image loader. Mirrors C++ JpgLoader.
    /// Uses JpegDecoder (ported line-by-line from jpgd) for decoding.
    /// </summary>
    public class JpgLoader : ImageLoader
    {
        private JpegDecoder? decoder;
        private byte[]? data;

        public JpgLoader() : base(FileType.Jpg)
        {
        }

        /************************************************************************/
        /* Internal Implementation                                              */
        /************************************************************************/

        private void Clear()
        {
            decoder = null;
            data = null;
        }

        private void Run()
        {
            if (decoder == null) return;

            surface.cs = ImageLoader.cs;
            var pixels = decoder.Decompress(surface.cs);
            if (pixels == null) return;

            surface.data = pixels;
            surface.Pin();

            surface.stride = (uint)w;
            surface.w = (uint)w;
            surface.h = (uint)h;
            surface.channelSize = sizeof(uint);
            surface.premultiplied = true;

            Clear();
        }

        /************************************************************************/
        /* External Implementation                                              */
        /************************************************************************/

        public override bool Open(string path)
        {
            decoder = JpegDecoder.FromFile(path, out var width, out var height);
            if (decoder == null) return false;

            w = width;
            h = height;

            return true;
        }

        public override bool Open(byte[] data, uint size, string? rpath, bool copy)
        {
            if (copy)
            {
                this.data = new byte[size];
                Array.Copy(data, this.data, size);
            }
            else
            {
                this.data = data;
            }

            decoder = JpegDecoder.FromData(this.data, (int)size, out var width, out var height);
            if (decoder == null) return false;

            w = width;
            h = height;

            return true;
        }

        public override bool Read()
        {
            if (!base.Read()) return true;

            if (decoder == null || w == 0 || h == 0) return false;

            // Synchronous decode (mirrors TaskScheduler::request in single-threaded mode)
            Run();

            return true;
        }

        public override bool Close()
        {
            if (!base.Close()) return false;
            return true;
        }

        public override unsafe RenderSurface? Bitmap()
        {
            return base.Bitmap();
        }
    }
}
