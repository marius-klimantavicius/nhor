// Ported from ThorVG/src/renderer/tvgCanvas.h and ThorVG/inc/thorvg.h

using System.Collections.Generic;

namespace ThorVG
{
    /// <summary>Canvas status. Mirrors C++ Status enum.</summary>
    public enum CanvasStatus : byte
    {
        Synced = 0,
        Painting,
        Updating,
        Drawing,
        Damaged
    }

    /// <summary>
    /// Abstract canvas for drawing graphical elements.
    /// Mirrors C++ tvg::Canvas / Canvas::Impl.
    /// </summary>
    public abstract class Canvas
    {
        internal Scene scene;
        internal RenderMethod? renderer;
        internal RenderRegion vport = new RenderRegion(0, 0, int.MaxValue, int.MaxValue);
        internal CanvasStatus status = CanvasStatus.Synced;

        protected Canvas()
        {
            scene = Scene.Gen();
            scene.Ref();
        }

        /// <summary>
        /// Destructor cleanup chain. Mirrors C++ Canvas::Impl::~Impl().
        /// Ensures deferred jobs are completed, scene is unreferenced, and
        /// renderer is properly cleaned up.
        /// </summary>
        ~Canvas()
        {
            // make it sure any deferred jobs
            renderer?.Sync();
            scene.Unref();
            if (renderer != null && renderer.Unref() == 0)
            {
                // In C++ this would delete the renderer.
                // In C# the GC handles the actual deallocation.
            }
        }

        public IReadOnlyList<Paint> GetPaints() => scene.Paints();

        public Result Add(Paint target, Paint? at = null)
        {
            if (target == null) return Result.InvalidArguments;

            if (target.pImpl.renderer != null && target.pImpl.renderer != renderer)
            {
                return Result.InsufficientCondition;
            }

            if (status == CanvasStatus.Drawing) return Result.InsufficientCondition;
            status = CanvasStatus.Painting;
            return scene.Add(target, at);
        }

        public Result Remove(Paint? paint = null)
        {
            if (status == CanvasStatus.Drawing) return Result.InsufficientCondition;
            status = CanvasStatus.Painting;
            return scene.Remove(paint);
        }

        public Result Update()
        {
            if (status == CanvasStatus.Updating) return Result.Success;
            if (status == CanvasStatus.Drawing) return Result.InsufficientCondition;

            var clips = new List<object?>();
            var flag = RenderUpdateFlag.None;

            // TODO: All is too harsh, can be optimized.
            if (status == CanvasStatus.Damaged) flag = RenderUpdateFlag.All;

            if (renderer == null || !renderer.PreUpdate()) return Result.InsufficientCondition;

            var m = TvgMath.Identity();
            scene.pImpl.Update(renderer, m, clips, 255, flag);

            if (!renderer.PostUpdate()) return Result.InsufficientCondition;

            status = CanvasStatus.Updating;
            return Result.Success;
        }

        public Result Draw(bool clear = false)
        {
            if (status == CanvasStatus.Drawing) return Result.InsufficientCondition;
            if (status == CanvasStatus.Painting || status == CanvasStatus.Damaged) Update();
            if (status != CanvasStatus.Updating) return Result.InsufficientCondition;
            if (renderer == null) return Result.InsufficientCondition;
            if (clear && !renderer.Clear()) return Result.InsufficientCondition;
            if (!renderer.PreRender()) return Result.InsufficientCondition;
            if (!scene.pImpl.Render(renderer) || !renderer.PostRender()) return Result.InsufficientCondition;

            status = CanvasStatus.Drawing;
            return Result.Success;
        }

        public Result Viewport(int x, int y, int w, int h)
        {
            if (status == CanvasStatus.Synced || status == CanvasStatus.Damaged)
            {
                var val = new RenderRegion(x, y, x + w, y + h);
                // intersect if the target buffer is already set
                if (renderer != null)
                {
                    var surface = renderer.MainSurface();
                    if (surface != null && surface.w > 0 && surface.h > 0)
                    {
                        val.IntersectWith(new RenderRegion(0, 0, (int)surface.w, (int)surface.h));
                    }
                }
                if (vport == val) return Result.Success;
                renderer?.Viewport(val);
                vport = val;
                status = CanvasStatus.Damaged;
                return Result.Success;
            }
            return Result.InsufficientCondition;
        }

        public Result Sync()
        {
            if (status == CanvasStatus.Synced) return Result.Success;
            if (renderer != null && renderer.Sync())
            {
                status = CanvasStatus.Synced;
                return Result.Success;
            }
            // If no renderer, just sync the status
            if (renderer == null)
            {
                status = CanvasStatus.Synced;
                return Result.Success;
            }
            return Result.Unknown;
        }
    }
}
