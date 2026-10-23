// Ported from ThorVG/src/loaders/jpg/tvgJpgLoader.h and tvgJpgLoader.cpp

using System;

namespace ThorVG
{
    /// <summary>
    /// JPEG image loader. Mirrors C++ JpgLoader.
    /// Uses JpegDecoder (ported line-by-line from jpgd) for decoding.
    /// </summary>
    public class JpgLoader : BitmapLoader
    {
        private JpegDecoder? decoder;
        private byte[]? data;
        private readonly LoaderTask task;

        public JpgLoader() : base(FileType.Jpg)
        {
            task = new LoaderTask(Run);
        }

        /************************************************************************/
        /* Internal Implementation                                              */
        /************************************************************************/

        private void Clear()
        {
            decoder?.Dispose();
            decoder = null;
            data = null;
            owner = Ownership.Borrow;
        }

        private void Run(uint tid)
        {
            if (decoder == null) return;

            var pixels = decoder.Decompress();
            surface.Setup(pixels!, (uint)w, (uint)w, (uint)h, sizeof(uint), ColorSpace.XBGR8888);

            Clear();
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
            decoder = JpegDecoder.FromFile(path, out var width, out var height);
            if (decoder == null) return false;

            w = width;
            h = height;
            owner = Ownership.Transfer;

            return true;
        }

        public override bool Open(byte[] data, uint size, LoaderOps? ops, bool copy)
        {
            task.Done();
            if (data == null || size == 0 || size > data.Length) return false;
            if (copy)
            {
                this.data = new byte[size];
                Array.Copy(data, this.data, size);
            }
            else
            {
                this.data = data;
            }

            owner = copy ? Ownership.Copy : ops?.owner ?? Ownership.Borrow;
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

            if (TaskScheduler.Threads() == 0) Run(0);
            else TaskScheduler.Request(task);

            return true;
        }

        public override bool Close()
        {
            if (!base.Close()) return false;
            task.Done();
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
            Clear();
            base.ReleaseResources();
        }
    }
}
