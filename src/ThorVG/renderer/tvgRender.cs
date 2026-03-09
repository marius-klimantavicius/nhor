// Ported from ThorVG/src/renderer/tvgRender.h and tvgRender.cpp
// RenderMethod abstract class.

using System.Collections.Generic;

namespace ThorVG
{
    /// <summary>
    /// Abstract rendering backend. Mirrors C++ RenderMethod.
    /// Concrete implementations (SW, GL) will be ported in later batches.
    /// </summary>
    public abstract class RenderMethod
    {
        private uint _refCnt;
        private readonly Key _key = new Key();
        protected RenderRegion vport;

        // --- Reference counting ---
        public uint Ref()
        {
            using var lk = new ScopedLock(_key);
            return ++_refCnt;
        }

        public uint Unref()
        {
            using var lk = new ScopedLock(_key);
            return --_refCnt;
        }

        public RenderRegion Viewport() => vport;

        public bool Viewport(in RenderRegion vp)
        {
            vport = vp;
            return true;
        }

        // --- Main features (abstract) ---
        public abstract bool PreUpdate();
        public abstract object? Prepare(RenderShape rshape, object? data, in Matrix transform, ref ValueList<object?> clips, byte opacity, RenderUpdateFlag flags, bool clipper);
        public abstract object? Prepare(RenderSurface surface, object? data, in Matrix transform, ref ValueList<object?> clips, byte opacity, FilterMethod filter, RenderUpdateFlag flags);
        public abstract bool PostUpdate();
        public abstract bool PreRender();
        public abstract bool RenderShape(object? data);
        public abstract bool RenderImage(object? data);
        public abstract bool PostRender();
        public abstract void Dispose(object? data);
        public abstract RenderRegion Region(object? data);
        public abstract bool Bounds(object? data, Point[] pt4, in Matrix m);
        public abstract bool Blend(BlendMethod method);
        public abstract ColorSpace ColorSpaceValue();
        public abstract RenderSurface? MainSurface();
        public abstract bool Clear();
        public abstract bool Sync();
        public abstract bool IntersectsShape(object? data, in RenderRegion region);
        public abstract bool IntersectsImage(object? data, in RenderRegion region);

        // --- Composition ---
        public abstract RenderCompositor? Target(in RenderRegion region, ColorSpace cs, CompositionFlag flags);
        public abstract bool BeginComposite(RenderCompositor? cmp, MaskMethod method, byte opacity);
        public abstract bool EndComposite(RenderCompositor? cmp);

        // --- Post effects ---
        public abstract void Prepare(RenderEffect effect, in Matrix transform);
        public abstract bool Region(RenderEffect effect);
        public abstract bool Render(RenderCompositor? cmp, RenderEffect effect, bool direct);
        public abstract void Dispose(RenderEffect effect);

        // --- Partial rendering ---
        public abstract void Damage(object? rd, in RenderRegion region);
        public abstract bool Partial(bool disable);
    }
}
