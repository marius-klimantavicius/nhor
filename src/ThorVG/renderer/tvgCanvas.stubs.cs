// Concrete canvas implementations.
// SwCanvas is fully ported. GlCanvas remains a stub until Batch 9 (GL Engine).

namespace ThorVG
{
    /// <summary>Software-rendered canvas using SwRenderer.</summary>
    public class SwCanvas : Canvas
    {
        public enum MempoolPolicy { Default, Shareable, Individual }

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
            if (cs == ColorSpace.Unknown) return Result.InvalidArguments;
            if (cs == ColorSpace.Grayscale8) return Result.NonSupport;

            if (status == CanvasStatus.Updating || status == CanvasStatus.Drawing)
            {
                return Result.InsufficientCondition;
            }

            var swRenderer = (SwRenderer)renderer!;
            if (!swRenderer.Target(buffer, stride, w, h, cs)) return Result.InvalidArguments;
            vport = new RenderRegion(0, 0, (int)w, (int)h);
            swRenderer.Viewport(vport);

            // FIXME: The value must be associated with an individual canvas instance.
            ImageLoader.cs = cs;

            // Paints must be updated again with this new target.
            status = CanvasStatus.Damaged;

            return Result.Success;
        }

        public unsafe Result Target(uint* buffer, uint stride, uint w, uint h, ColorSpace cs)
        {
            if (cs == ColorSpace.Unknown) return Result.InvalidArguments;
            if (cs == ColorSpace.Grayscale8) return Result.NonSupport;

            if (status == CanvasStatus.Updating || status == CanvasStatus.Drawing)
            {
                return Result.InsufficientCondition;
            }

            var swRenderer = (SwRenderer)renderer!;
            if (!swRenderer.Target(buffer, stride, w, h, cs)) return Result.InvalidArguments;
            vport = new RenderRegion(0, 0, (int)w, (int)h);
            swRenderer.Viewport(vport);

            ImageLoader.cs = cs;

            status = CanvasStatus.Damaged;

            return Result.Success;
        }

        public Result Mempool(MempoolPolicy policy)
        {
            if (renderer == null) return Result.InsufficientCondition;
            var swRenderer = (SwRenderer)renderer;
            switch (policy)
            {
                case MempoolPolicy.Individual:
                    swRenderer.SetMempoolIndividual();
                    break;
                case MempoolPolicy.Shareable:
                    swRenderer.SetMempoolShared();
                    break;
                case MempoolPolicy.Default:
                default:
                    break;
            }
            return Result.Success;
        }
    }

    /// <summary>GL-rendered canvas using GlRenderer.</summary>
    public class GlCanvas : Canvas
    {
        private GlCanvas() { }

        ~GlCanvas()
        {
            GlRenderer.Term();
        }

        public static GlCanvas? Gen(EngineOption op = EngineOption.Default)
        {
            if (TvgCommon.engineInit <= 0) return null;

            if (op == EngineOption.SmartRender)
            {
                TvgCommon.TVGLOG("RENDERER", "GlCanvas doesn't support Smart Rendering");
            }

            var r = GlRenderer.Gen(TaskScheduler.Threads());
            if (r == null) return null;
            r.Ref();
            var canvas = new GlCanvas();
            canvas.renderer = r;
            return canvas;
        }

        public Result Target(nint display, nint surface, nint context, int id, uint w, uint h, ColorSpace cs)
        {
            if (cs != ColorSpace.ABGR8888S) return Result.NonSupport;

            if (status == CanvasStatus.Updating || status == CanvasStatus.Drawing)
            {
                return Result.InsufficientCondition;
            }

            var glRenderer = (GlRenderer)renderer!;
            if (!glRenderer.Target(display, surface, context, id, w, h, cs))
                return Result.Unknown;

            vport = new RenderRegion(0, 0, (int)w, (int)h);
            glRenderer.Viewport(vport);

            // Paints must be updated again with this new target.
            status = CanvasStatus.Damaged;

            return Result.Success;
        }
    }
}
