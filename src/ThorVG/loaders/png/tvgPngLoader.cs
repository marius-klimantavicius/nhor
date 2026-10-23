// Ported from ThorVG/src/loaders/png/tvgPngLoader.h and tvgPngLoader.cpp

using System;

namespace ThorVG
{
    /// <summary>
    /// PNG image loader. Mirrors C++ PngLoader.
    /// Uses the ported LodePNG decoder for decoding.
    /// </summary>
    public class PngLoader : BitmapLoader
    {
        private readonly LodePNGState state = LodePng.StateInit();
        private byte[]? data;
        private int size;
        private readonly LoaderTask task;

        public PngLoader() : base(FileType.Png)
        {
            task = new LoaderTask(Run);
        }

        /************************************************************************/
        /* Internal Implementation                                              */
        /************************************************************************/

        private void Run(uint tid)
        {
            state.info_raw.colortype = LodePNGColorType.LCT_RGBA;
            uint[]? pixels = null;
            uint error = LodePng.DecodeBytes(data!, size, state, out var rawBytes, out var width, out var height);
            if (error != 0)
            {
                TvgCommon.TVGERR("PNG", "Failed to decode image");
            }
            else
            {
                if (state.info_png.iccp_defined != 0 && LodePng.ToSrgb(rawBytes!, rawBytes!, width, height, state) != 0)
                    TvgCommon.TVGERR("PNG", "Unsupported ICC color profile");

                long numPixels = (long)width * height;
                pixels = new uint[numPixels];
                for (long i = 0; i < numPixels; i++)
                {
                    pixels[i] = (uint)(rawBytes![i * 4] | (rawBytes[i * 4 + 1] << 8) |
                        (rawBytes[i * 4 + 2] << 16) | (rawBytes[i * 4 + 3] << 24));
                }
            }

            surface.Setup(pixels!, width, width, height, sizeof(uint), ColorSpace.ABGR8888S);
        }

        /************************************************************************/
        /* External Implementation                                              */
        /************************************************************************/

        public override Result OpenResult(string path, LoaderOps? ops = null)
            => Open(path, ops) ? Result.Success : Result.InvalidArguments;

        public override Result OpenResult(byte[] data, uint size, LoaderOps? ops)
            => Open(data, size, ops, ops?.owner == Ownership.Copy) ? Result.Success : Result.InvalidArguments;

        public override bool Open(string path, LoaderOps? ops = null)
        {
            task.Done();
            var fileData = ReadFile(path);
            if (fileData == null || fileData.Length == 0) return false;

            data = fileData;
            size = fileData.Length;
            owner = Ownership.Transfer;

            if (LodePng.Inspect(data, size, state, out var width, out var height) != 0)
                return false;

            w = width;
            h = height;
            return true;
        }

        public override bool Open(byte[] data, uint size, LoaderOps? ops, bool copy)
        {
            task.Done();
            if (data == null || size == 0 || size > data.Length) return false;
            if (LodePng.Inspect(data, (int)size, state, out var width, out var height) != 0)
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
            owner = copy ? Ownership.Copy : ops?.owner ?? Ownership.Borrow;

            return true;
        }

        public override bool Read()
        {
            if (data == null || w == 0 || h == 0) return false;

            if (!base.Read()) return true;

            if (TaskScheduler.Threads() == 0) Run(0);
            else TaskScheduler.Request(task);

            return true;
        }

        public override unsafe RenderSurface? Bitmap()
        {
            task.Done();
            return base.Bitmap();
        }

        internal override void ReleaseResources()
        {
            task.Done();
            data = null;
            LodePng.StateCleanup(state);
            base.ReleaseResources();
        }
    }
}
