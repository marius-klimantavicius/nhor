// Ported from pinned ThorVG/src/renderer/gpu_engine/gl/tvgGlSolidBatch.{h,cpp}.
using System.Runtime.InteropServices;

namespace ThorVG
{
    public unsafe class GlSolidBatch
    {
        public struct DrawData
        {
            public GlGeometry geometry;
            public GlProgram? program;
            public RenderUpdateFlag flag;
            public RGBA color;
            public uint texId, opacity, vertexSize;
        }
        private GlRenderTask? task;
        private RenderRegion viewBounds;
        private RGBA color;
        private uint vertexCount, texId, opacity;

        public void Clear() { task = null; viewBounds = default; color = default; vertexCount = texId = opacity = 0; }

        public void Draw(GlRenderer renderer, GlShape shape, in RGBA color, int depth, in RenderRegion viewRegion, in RenderRegion bounds)
        {
            var adjusted = color; adjusted.a = RenderHelper.Multiply(color.a, (byte)shape.opacity);
            Draw(renderer, new DrawData { geometry = shape.geometry, program = renderer.mPrograms[(int)GlRenderer.RenderTypes.RT_Color], flag = RenderUpdateFlag.Color, color = adjusted, vertexSize = 2 }, depth, viewRegion, bounds);
        }
        public void Draw(GlRenderer renderer, GlShape image, int depth, in RenderRegion viewRegion, in RenderRegion bounds)
        {
            Draw(renderer, new DrawData { geometry = image.geometry, program = renderer.mPrograms[(int)GlRenderer.RenderTypes.RT_Image], flag = RenderUpdateFlag.Image, texId = image.texId, opacity = image.opacity, vertexSize = 4 }, depth, viewRegion, bounds);
        }
        public void Draw(GlRenderer renderer, in DrawData data, int depth, in RenderRegion viewRegion, in RenderRegion bounds)
        {
            var pass = renderer.CurrentPass()!; var buffer = data.geometry.fill;
            uint vertices = buffer.vertex.count / data.vertexSize, indices = buffer.index.count;
            if (vertices == 0 || indices == 0) return;
            if (!Appendable(pass, data, bounds)) { Emit(renderer, pass, data, depth, viewRegion, bounds, vertices); return; }
            if (task!.GetVertexLayout().count == 1) Promote(renderer, data, depth, viewRegion, vertices, indices);
            else Append(renderer, data, depth, viewRegion, vertices, indices);
        }
        private bool Appendable(GlRenderPass pass, in DrawData data, in RenderRegion bounds)
        {
            if (task == null || pass.LastTask() != task || task.GetProgram() != data.program || !(viewBounds == bounds)) return false;
            return data.flag != RenderUpdateFlag.Image || (texId == data.texId && opacity == data.opacity);
        }
        private void Emit(GlRenderer renderer, GlRenderPass pass, in DrawData data, int depth, in RenderRegion viewRegion, in RenderRegion bounds, uint vertices)
        {
            var draw = new GlRenderTask(data.program); draw.SetViewMatrix(pass.GetViewMatrix()); draw.SetDrawDepth(depth);
            if (data.flag == RenderUpdateFlag.Image)
            {
                uint* info = stackalloc uint[4] { data.opacity, 0, 0, 0 };
                draw.AddBindResource(new GlBindingResource(1, data.program!.GetUniformBlockIndex(GlShaderUniformBlock.ColorInfo), renderer.mGpuBuffer.GetBufferId(), renderer.mGpuBuffer.Push(info, 4 * sizeof(uint), true), 4 * sizeof(uint)));
            }
            data.geometry.Draw(draw, renderer.mGpuBuffer, data.flag);
            if (data.flag == RenderUpdateFlag.Image) draw.AddBindResource(new GlBindingResource(0, data.texId, data.program!.GetUniformLocation(GlShaderUniform.Texture)));
            else draw.SetVertexColor(data.color.r / 255f, data.color.g / 255f, data.color.b / 255f, data.color.a / 255f);
            var viewport = viewRegion; viewport.IntersectWith(bounds); draw.SetViewport(viewport); pass.AddRenderTask(draw);
            task = draw; viewBounds = bounds; color = data.color; vertexCount = vertices; texId = data.texId; opacity = data.opacity;
        }
        private void AppendGeometry(GlRenderer renderer, in DrawData data)
        {
            var buffer = data.geometry.fill; float* vertices = null; uint* indices = null;
            renderer.mGpuBuffer.Reserve(buffer.vertex.count * sizeof(float), (void**)&vertices);
            renderer.mGpuBuffer.ReserveIndex(buffer.index.count * sizeof(uint), (void**)&indices);
            NativeMemory.Copy(buffer.vertex.data, vertices, buffer.vertex.count * sizeof(float));
            BuildIndices(indices, buffer, vertexCount);
        }
        private void Promote(GlRenderer renderer, in DrawData data, int depth, in RenderRegion viewRegion, uint vertices, uint indices)
        {
            AppendGeometry(renderer, data); RGBA* colors = null;
            uint offset = renderer.mGpuBuffer.ReserveAux((vertexCount + vertices) * (uint)sizeof(RGBA), (void**)&colors);
            BuildColors(colors, vertexCount, color); BuildColors(colors + vertexCount, vertices, data.color);
            task!.AddVertexLayout(new GlVertexLayout { index = 1, size = 4, stride = (uint)sizeof(RGBA), offset = offset, type = GL.GL_UNSIGNED_BYTE, normalized = GL.GL_TRUE, arrayBufferId = renderer.mGpuBuffer.GetAuxBufferId() });
            Commit(depth, viewRegion, vertices, indices);
        }
        private void Append(GlRenderer renderer, in DrawData data, int depth, in RenderRegion viewRegion, uint vertices, uint indices)
        {
            AppendGeometry(renderer, data);
            if (data.flag == RenderUpdateFlag.Color) { RGBA* colors = null; renderer.mGpuBuffer.ReserveAux(vertices * (uint)sizeof(RGBA), (void**)&colors); BuildColors(colors, vertices, data.color); }
            Commit(depth, viewRegion, vertices, indices);
        }
        private void Commit(int depth, in RenderRegion viewRegion, uint vertices, uint indices)
        {
            vertexCount += vertices; task!.SetDrawRange(task.GetIndexOffset(), task.GetIndexCount() + indices); task.SetDrawDepth(depth);
            var viewport = task.GetViewport(); viewport.AddWith(viewRegion); viewport.IntersectWith(viewBounds); task.SetViewport(viewport);
        }
        private static void BuildColors(RGBA* output, uint count, in RGBA value) { for (uint i = 0; i < count; ++i) output[i] = value; }
        private static void BuildIndices(uint* output, GlGeometryBuffer source, uint vertex) { for (uint i = 0; i < source.index.count; ++i) output[i] = source.index[i] + vertex; }
    }
}
