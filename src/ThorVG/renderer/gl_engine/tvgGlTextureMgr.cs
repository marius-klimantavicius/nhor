// Ported from ThorVG/src/renderer/gl_engine/tvgGlTextureMgr.h and tvgGlTextureMgr.cpp

namespace ThorVG
{
    public partial class TextureMgr
    {
        public struct Entry
        {
            public uint texId;
            public uint refCnt;
        }

        public class SurfaceEntry : IInlistNode<SurfaceEntry>
        {
            public SurfaceEntry? Prev { get; set; }
            public SurfaceEntry? Next { get; set; }

            public RenderSurface? surface;
            public Entry bilinear;
            public Entry nearest;
        }

        public Inlist<SurfaceEntry> surfaces = new Inlist<SurfaceEntry>();
        public ushort stamp = 1;  // Non-zero rolling stamp for stale texture ownership checks.

        public SurfaceEntry? Find(RenderSurface? surface)
        {
            var entry = surfaces.Head;
            while (entry != null)
            {
                if (ReferenceEquals(entry.surface, surface)) return entry;
                entry = entry.Next;
            }
            return null;
        }

        public static unsafe void Upload(uint texId, RenderSurface surface, FilterMethod filter)
        {
            // Preserve immediate upload for existing callers. Renderer-owned
            // uploads queue conversion and share one preprocessing pass.
            var manager = new TextureMgr(); var state = new GlStateCache(); int restore;
            GL.glGetIntegerv(GL.GL_FRAMEBUFFER_BINDING, &restore);
            try
            {
                manager.Upload(state, texId, surface, filter, true);
                if (!manager.FlushPreprocess(state, restore)) throw new System.InvalidOperationException("Texture preprocessing failed.");
            }
            finally { manager.ClearPreprocess(); }
        }

        public uint Retain(RenderSurface? surface, FilterMethod filter) => Retain(new GlStateCache(), surface, filter);

        public unsafe uint Retain(GlStateCache state, RenderSurface? surface, FilterMethod filter)
        {
            var surfaceEntry = Find(surface);
            if (surfaceEntry == null)
            {
                surfaceEntry = new SurfaceEntry();
                surfaceEntry.surface = surface;
                surfaces.Back(surfaceEntry);
            }
            ref var entry = ref (filter == FilterMethod.Bilinear ? ref surfaceEntry.bilinear : ref surfaceEntry.nearest);

            if (entry.texId != 0)
            {
                ++entry.refCnt;
                return entry.texId;
            }

            uint texId;
            GL.glGenTextures(1, &texId);
            Upload(state, texId, surface!, filter, true);

            entry.texId = texId;
            entry.refCnt = 1;
            return texId;
        }

        public uint Release(RenderSurface? surface, FilterMethod filter, uint texId)
        {
            var surfaceEntry = Find(surface);
            if (surfaceEntry == null) return 0;
            ref var entry = ref (filter == FilterMethod.Bilinear ? ref surfaceEntry.bilinear : ref surfaceEntry.nearest);
            if (entry.texId != texId) return 0;

            if (entry.refCnt > 0) --entry.refCnt;
            if (entry.refCnt > 0) return 0;

            texId = entry.texId;
            entry.texId = 0;
            entry.refCnt = 0;
            RequestPreprocess(texId, 0, 0, 0);

            if (surfaceEntry.bilinear.texId == 0 && surfaceEntry.nearest.texId == 0)
            {
                surfaces.Remove(surfaceEntry);
            }

            return texId;
        }

        public unsafe void Clear()
        {
            var textures = new Array<uint>();
            textures.Reserve(surfaces.Count * 2);
            var entry = surfaces.Head;
            while (entry != null)
            {
                if (entry.bilinear.texId != 0) textures.Push(entry.bilinear.texId);
                if (entry.nearest.texId != 0) textures.Push(entry.nearest.texId);
                entry = entry.Next;
            }
            surfaces.Free();
            ClearPreprocess();
            stamp++;
            if (stamp == 0) stamp = 1;  // avoid zero stamp, which is used to indicate stale cache.
            if (textures.count > 0)
            {
                GL.glDeleteTextures((int)textures.count, textures.data);
            }
            textures.Dispose();
        }
    }
}
