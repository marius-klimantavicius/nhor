// Concrete software and OpenGL canvas implementations.

namespace ThorVG
{
    internal interface ISwTargetResult
    {
        Result TargetResult(uint[] buffer, uint stride, uint w, uint h, ColorSpace cs);
        unsafe Result TargetResult(uint* buffer, uint stride, uint w, uint h, ColorSpace cs);
    }

    internal interface IGlTargetResult
    {
        Result TargetResult(nint display, nint surface, nint context, int id, uint w, uint h, ColorSpace cs);
    }

    /// <summary>Software-rendered canvas using SwRenderer.</summary>
    public class SwCanvas : Canvas
    {
        private SwCanvas() { }

        ~SwCanvas()
        {
            SwRenderer.Term();
        }

        public static SwCanvas Gen(EngineOption op = EngineOption.Default)
        {
            var r = new SwRenderer(TaskScheduler.Threads(), op);
            r.Ref();
            var canvas = new SwCanvas();
            canvas.renderer = r;
            return canvas;
        }

        public Result Target(uint[] buffer, uint stride, uint w, uint h, ColorSpace cs)
        {
            if (status == CanvasStatus.Updating || status == CanvasStatus.Drawing)
            {
                return Result.InsufficientCondition;
            }

            var swRenderer = (SwRenderer)renderer!;
            var ret = swRenderer is ISwTargetResult targetResult
                ? targetResult.TargetResult(buffer, stride, w, h, cs)
                : LegacyTarget(swRenderer, buffer, stride, w, h, cs);
            if (ret != Result.Success) return ret;
            vport = new RenderRegion(0, 0, (int)w, (int)h);
            renderer!.Viewport(vport);

            status = CanvasStatus.Damaged;

            // FIXME: The value must be associated with an individual canvas instance.
            ImageLoader.cs = cs;

            return Result.Success;
        }

        public unsafe Result Target(uint* buffer, uint stride, uint w, uint h, ColorSpace cs)
        {
            if (status == CanvasStatus.Updating || status == CanvasStatus.Drawing)
            {
                return Result.InsufficientCondition;
            }

            var swRenderer = (SwRenderer)renderer!;
            var ret = swRenderer is ISwTargetResult targetResult
                ? targetResult.TargetResult(buffer, stride, w, h, cs)
                : LegacyTarget(swRenderer, buffer, stride, w, h, cs);
            if (ret != Result.Success) return ret;
            vport = new RenderRegion(0, 0, (int)w, (int)h);
            renderer!.Viewport(vport);

            status = CanvasStatus.Damaged;

            ImageLoader.cs = cs;

            return Result.Success;
        }

        private static Result LegacyTarget(SwRenderer renderer, uint[] buffer, uint stride, uint w, uint h, ColorSpace cs)
        {
            if (cs == ColorSpace.Unknown) return Result.InvalidArguments;
            if (cs == ColorSpace.Grayscale8) return Result.NonSupport;
            return renderer.Target(buffer, stride, w, h, cs) ? Result.Success : Result.InvalidArguments;
        }

        private static unsafe Result LegacyTarget(SwRenderer renderer, uint* buffer, uint stride, uint w, uint h, ColorSpace cs)
        {
            if (cs == ColorSpace.Unknown) return Result.InvalidArguments;
            if (cs == ColorSpace.Grayscale8) return Result.NonSupport;
            return renderer.Target(buffer, stride, w, h, cs) ? Result.Success : Result.InvalidArguments;
        }

    }

    /// <summary>GL-rendered canvas using GlRenderer.</summary>
    public class GlCanvas : Canvas, System.IDisposable
    {
        private bool disposed;

        private GlCanvas() { }

        ~GlCanvas()
        {
            (renderer as GlRenderer)?.Abandon();
            GlRenderer.Term();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            if (renderer is GlRenderer glRenderer)
            {
                glRenderer.Sync();
                scene.Unref();
                glRenderer.Dispose();
                glRenderer.Unref();
                renderer = null;
            }
            else scene.Unref();
            GlRenderer.Term();
            System.GC.SuppressFinalize(this);
        }

        public static GlCanvas? Gen(EngineOption op = EngineOption.Default)
        {
            if (TvgCommon.engineInit <= 0) return null;

            if ((op & EngineOption.SmartRender) != 0)
            {
                TvgCommon.TVGLOG("RENDERER", "GlCanvas doesn't support Smart Rendering");
            }
            if ((op & EngineOption.Aliased) != 0)
            {
                TvgCommon.TVGLOG("RENDERER", "GlCanvas doesn't support Aliased");
            }

            var r = GlRenderer.Gen(TaskScheduler.Threads(), op);
            if (r == null) return null;
            r.Ref();
            var canvas = new GlCanvas();
            canvas.renderer = r;
            return canvas;
        }

        public Result Target(nint display, nint surface, nint context, int id, uint w, uint h, ColorSpace cs)
        {
            if (status == CanvasStatus.Updating || status == CanvasStatus.Drawing)
            {
                return Result.InsufficientCondition;
            }

            var glRenderer = (GlRenderer)renderer!;
            var ret = glRenderer is IGlTargetResult targetResult
                ? targetResult.TargetResult(display, surface, context, id, w, h, cs)
                : LegacyTarget(glRenderer, display, surface, context, id, w, h, cs);
            if (ret != Result.Success) return ret;

            vport = new RenderRegion(0, 0, (int)w, (int)h);
            renderer!.Viewport(vport);

            // Paints must be updated again with this new target.
            status = CanvasStatus.Damaged;

            // FIXME: The value must be associated with an individual canvas instance.
            ImageLoader.cs = cs;

            return Result.Success;
        }

        /// <summary>Targets the OpenGL context currently active on this thread.</summary>
        public Result Target(int id, uint w, uint h, ColorSpace cs)
        {
            if (!GL.TryGetCurrentTarget(out var display, out var surface, out var context))
                return Result.InsufficientCondition;
            return Target(display, surface, context, id, w, h, cs);
        }

        private static Result LegacyTarget(GlRenderer renderer, nint display, nint surface, nint context, int id, uint w, uint h, ColorSpace cs)
        {
            if (cs != ColorSpace.ABGR8888S) return Result.NonSupport;
            return renderer.Target(display, surface, context, id, w, h, cs) ? Result.Success : Result.Unknown;
        }
    }
}
