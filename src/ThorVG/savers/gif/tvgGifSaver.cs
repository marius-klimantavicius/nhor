// Ported from ThorVG/src/savers/gif/tvgGifSaver.h and tvgGifSaver.cpp

using System;
using System.Runtime.InteropServices;

namespace ThorVG
{
    public class GifSaver : SaveModule, IDisposable
    {
        private unsafe uint* nativeBuffer;
        private Animation? animation;
        private Paint? bg;
        private string? path;
        private Float2 vsize;
        private float fps;

        /************************************************************************/
        /* Internal Class Implementation                                        */
        /************************************************************************/

        private unsafe void Run()
        {
            var canvas = SwCanvas.Gen();
            if (canvas == null) return;

            var w = (uint)vsize[0];
            var h = (uint)vsize[1];

            FreeBuffer();
            nativeBuffer = (uint*)NativeMemory.AllocZeroed((nuint)(w * h) * (nuint)sizeof(uint));
            canvas.Target(nativeBuffer, w, w, h, ColorSpace.ABGR8888S);
            if (bg != null) canvas.Add(bg);
            canvas.Add(animation!.GetPicture());

            // use the default fps
            if (fps > 60.0f) fps = 60.0f;   // just in case
            else if (TvgMath.Zero(fps) || fps < 0.0f)
            {
                fps = (animation.TotalFrame() / animation.Duration());
            }

            var delay = (1.0f / fps);
            var transparent = bg != null ? false : true;

            var writer = new GifWriter();
            if (!GifEncoder.GifBegin(writer, path!, w, h, (uint)(delay * 100.0f)))
            {
                TvgCommon.TVGERR("GIF_SAVER", "Failed gif encoding");
                return;
            }

            var duration = animation!.Duration();

            for (var p = 0.0f; p < duration; p += delay)
            {
                var frameNo = animation.TotalFrame() * (p / duration);
                animation.Frame(frameNo);
                canvas.Update();
                if (canvas.Draw(true) == Result.Success)
                {
                    canvas.Sync();
                }
                if (!GifEncoder.GifWriteFrame(writer, (byte*)nativeBuffer, w, h, (uint)(delay * 100.0f), transparent))
                {
                    TvgCommon.TVGERR("GIF_SAVER", "Failed gif encoding");
                    break;
                }
            }

            if (!GifEncoder.GifEnd(writer)) TvgCommon.TVGERR("GIF_SAVER", "Failed gif encoding");

            if (bg != null)
            {
                bg.Unref();
                bg = null;
            }
        }

        /************************************************************************/
        /* External Class Implementation                                        */
        /************************************************************************/

        ~GifSaver()
        {
            CloseInternal();
        }

        private unsafe void FreeBuffer()
        {
            if (nativeBuffer != null)
            {
                NativeMemory.Free(nativeBuffer);
                nativeBuffer = null;
            }
        }

        private void CloseInternal()
        {
            if (bg != null) bg.Unref();
            bg = null;

            // animation holds the picture, it must be 1 at the bottom.
            if (animation != null && animation.GetPicture().RefCnt() <= 1)
            {
                // In C# we let GC handle cleanup
            }
            animation = null;

            path = null;
            FreeBuffer();
        }

        public override bool Close()
        {
            // In C++, this calls this->done() which waits for the task to finish.
            // In C#, we run synchronously so no waiting needed.

            CloseInternal();
            return true;
        }

        public override bool Save(Paint paint, Paint? bg, string filename, uint quality)
        {
            TvgCommon.TVGLOG("GIF_SAVER", "Paint is not supported.");
            return false;
        }

        public override bool Save(Animation animation, Paint? bg, string filename, uint quality, uint fps)
        {
            Close();

            var picture = animation.GetPicture();
            float x, y;
            picture.Bounds(out x, out y, out vsize[0], out vsize[1]);

            // cut off the negative space
            if (x < 0) vsize[0] += x;
            if (y < 0) vsize[1] += y;

            if (vsize[0] < MathConstants.FLOAT_EPSILON || vsize[1] < MathConstants.FLOAT_EPSILON)
            {
                TvgCommon.TVGLOG("GIF_SAVER", "Saving animation has zero view size.");
                return false;
            }

            this.path = filename;
            this.animation = animation;

            if (bg != null)
            {
                bg.Ref();
                this.bg = bg;
            }
            this.fps = (float)fps;

            // In C++, this dispatches to TaskScheduler::request(this).
            // In C#, we run synchronously for simplicity.
            Run();

            return true;
        }

        public void Dispose()
        {
            CloseInternal();
            GC.SuppressFinalize(this);
        }
    }
}
