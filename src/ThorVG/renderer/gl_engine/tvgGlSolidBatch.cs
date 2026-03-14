// Ported from ThorVG/src/renderer/gl_engine/tvgGlSolidBatch.h and tvgGlSolidBatch.cpp

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ThorVG
{
    /// <summary>
    /// Batching system that merges consecutive solid-color draw calls into a single
    /// GL draw call with per-vertex color attributes stored in the auxiliary buffer.
    /// </summary>
    public unsafe class GlSolidBatch
    {
        private GlRenderPass? pass;
        private GlRenderTask? task;
        private GlShape? shape;
        private RGBA color;
        private RenderUpdateFlag flag = RenderUpdateFlag.None;
        private int depth;
        private uint vertexCount;
        private uint indexOffset;
        private uint indexCount;
        private bool promoted;

        public void Clear()
        {
            pass = null;
            task = null;
            shape = null;
            color = default;
            flag = RenderUpdateFlag.None;
            depth = 0;
            vertexCount = 0;
            indexOffset = 0;
            indexCount = 0;
            promoted = false;
        }

        public void Draw(GlRenderer renderer, GlShape sdata, in RGBA c, int depth, in RenderRegion viewRegion)
        {
            var currentPass = renderer.CurrentPass();
            var buffer = sdata.geometry.fill;

            var vCount = buffer.vertex.count / 2;
            var iCount = buffer.index.count;
            if (vCount == 0 || iCount == 0) return;

            if (!Appendable(renderer, currentPass, viewRegion))
            {
                EmitSingle(renderer, currentPass!, sdata, c, depth, viewRegion, vCount, iCount);
                return;
            }

            var batchColor = SolidColor(sdata, c, RenderUpdateFlag.Color);
            if (!promoted)
            {
                if (Promote(renderer, currentPass!, batchColor, depth, viewRegion, buffer, vCount, iCount)) return;
                EmitSingle(renderer, currentPass!, sdata, c, depth, viewRegion, vCount, iCount);
                return;
            }

            Append(renderer, batchColor, viewRegion, buffer, vCount, iCount, depth);
        }

        private bool Appendable(GlRenderer renderer, GlRenderPass? pass, in RenderRegion viewRegion)
        {
            if (this.pass != pass) return false;
            if (pass!.LastTask() != task) return false;
            if (task!.GetProgram() != renderer.mPrograms[(int)GlRenderer.RenderTypes.RT_Color]) return false;
            if (!(task.GetViewport() == viewRegion)) return false;
            return true;
        }

        private void EmitSingle(GlRenderer renderer, GlRenderPass pass, GlShape sdata, in RGBA c, int depth, in RenderRegion viewRegion, uint vertexCount, uint indexCount)
        {
            var drawTask = new GlRenderTask(renderer.mPrograms[(int)GlRenderer.RenderTypes.RT_Color]);
            drawTask.SetViewMatrix(pass.GetViewMatrix());
            drawTask.SetDrawDepth(depth);

            if (!sdata.geometry.Draw(drawTask, renderer.mGpuBuffer, RenderUpdateFlag.Color))
            {
                Clear();
                return;
            }

            var taskColor = SolidColor(sdata, c, RenderUpdateFlag.Color);
            drawTask.SetVertexColor(taskColor.r / 255f, taskColor.g / 255f, taskColor.b / 255f, taskColor.a / 255f);
            drawTask.SetViewport(viewRegion);
            pass.AddRenderTask(drawTask);

            this.pass = pass;
            task = drawTask;
            shape = sdata;
            color = c;
            flag = RenderUpdateFlag.Color;
            this.depth = depth;
            this.vertexCount = vertexCount;
            indexOffset = drawTask.GetIndexOffset();
            this.indexCount = indexCount;
            promoted = false;
        }

        private bool Promote(GlRenderer renderer, GlRenderPass pass, in RGBA solidColor, int depth, in RenderRegion viewRegion, GlGeometryBuffer buffer, uint vertexCount, uint indexCount)
        {
            var firstVertexCount = this.vertexCount;
            var firstIndexCount = this.indexCount;
            if (firstVertexCount == 0 || firstIndexCount == 0) return false;

            var firstColor = SolidColor(shape!, color, flag);
            var totalVertexCount = firstVertexCount + vertexCount;
            var totalIndexCount = firstIndexCount + indexCount;

            // Promotion starts from a plain solid task: position-only attribute.
            ref var layouts = ref task!.GetVertexLayout();
            if (layouts.count != 1) return false;
            ref var posLayout = ref layouts[0];
            if (posLayout.size != 2 || posLayout.stride != 2 * sizeof(float)) return false;

            float* newPositions = null;
            RGBA* colors = null;
            uint* newIndices = null;
            // appendable() guarantees we are still extending the current pass tail task,
            // so the new vertex/index reservations must stay contiguous here.
            var newPosOffset = renderer.mGpuBuffer.Reserve(vertexCount * 2 * sizeof(float), (void**)&newPositions);
            var expectedPosOffset = posLayout.offset + firstVertexCount * 2 * sizeof(float);
            var newIdxOffset = renderer.mGpuBuffer.ReserveIndex(indexCount * sizeof(uint), (void**)&newIndices);
            var expectedIdxOffset = indexOffset + firstIndexCount * sizeof(uint);
            Debug.Assert(newPosOffset == expectedPosOffset);
            Debug.Assert(newIdxOffset == expectedIdxOffset);
            if (newPosOffset != expectedPosOffset || newIdxOffset != expectedIdxOffset) return false;

            var colorOffset = renderer.mGpuBuffer.ReserveAux(totalVertexCount * (uint)sizeof(RGBA), (void**)&colors);

            // Build full color stream: old vertices first, then the incoming shape.
            BuildPositions(newPositions, buffer, vertexCount);
            BuildColors(colors, firstVertexCount, firstColor);
            BuildColors(colors + firstVertexCount, vertexCount, solidColor);
            BuildIndices(newIndices, buffer, firstVertexCount);

            // Upgrade the same task to per-vertex color mode (no task replacement).
            task.SetViewMatrix(pass.GetViewMatrix());
            task.SetDrawDepth(depth);
            task.AddVertexLayout(new GlVertexLayout
            {
                index = 1,
                size = 4,
                stride = (uint)sizeof(RGBA),
                offset = colorOffset,
                type = GL.GL_UNSIGNED_BYTE,
                normalized = GL.GL_TRUE,
                arrayBufferId = renderer.mGpuBuffer.GetAuxBufferId()
            });
            task.SetDrawRange(indexOffset, totalIndexCount);

            var merged = task.GetViewport();
            merged.AddWith(viewRegion);
            task.SetViewport(merged);

            shape = null;
            this.depth = depth;
            this.vertexCount = totalVertexCount;
            this.indexCount = totalIndexCount;
            promoted = true;
            return true;
        }

        private void Append(GlRenderer renderer, in RGBA solidColor, in RenderRegion viewRegion, GlGeometryBuffer buffer, uint vertexCount, uint indexCount, int depth)
        {
            float* positions = null;
            RGBA* colors = null;
            uint* indices = null;
            renderer.mGpuBuffer.Reserve(vertexCount * 2 * sizeof(float), (void**)&positions);
            renderer.mGpuBuffer.ReserveAux(vertexCount * (uint)sizeof(RGBA), (void**)&colors);
            renderer.mGpuBuffer.ReserveIndex(indexCount * sizeof(uint), (void**)&indices);

            BuildPositions(positions, buffer, vertexCount);
            BuildColors(colors, vertexCount, solidColor);
            BuildIndices(indices, buffer, this.vertexCount);

            this.vertexCount += vertexCount;
            this.indexCount += indexCount;
            task!.SetDrawRange(indexOffset, this.indexCount);
            task.SetDrawDepth(depth);
            this.depth = depth;

            var merged = task.GetViewport();
            merged.AddWith(viewRegion);
            task.SetViewport(merged);
        }

        private static RGBA SolidColor(GlShape sdata, in RGBA c, RenderUpdateFlag flag)
        {
            var result = c;
            var a = RenderHelper.Multiply(c.a, (byte)sdata.opacity);

            if ((flag & RenderUpdateFlag.Stroke) != 0)
            {
                var strokeWidth = sdata.geometry.strokeRenderWidth;
                if (strokeWidth < GlConstants.MIN_GL_STROKE_WIDTH)
                {
                    var alpha = strokeWidth / GlConstants.MIN_GL_STROKE_WIDTH;
                    a = RenderHelper.Multiply(a, (byte)(alpha * 255));
                }
            }

            result.a = a;
            return result;
        }

        private static void BuildPositions(float* dst, GlGeometryBuffer src, uint count)
        {
            for (uint i = 0; i < count; ++i)
            {
                dst[i * 2 + 0] = src.vertex[i * 2 + 0];
                dst[i * 2 + 1] = src.vertex[i * 2 + 1];
            }
        }

        private static void BuildColors(RGBA* dst, uint count, in RGBA c)
        {
            for (uint i = 0; i < count; ++i)
            {
                dst[i] = new RGBA { r = c.r, g = c.g, b = c.b, a = c.a };
            }
        }

        private static void BuildIndices(uint* dst, GlGeometryBuffer src, uint baseVertex)
        {
            for (uint i = 0; i < src.index.count; ++i)
                dst[i] = src.index[i] + baseVertex;
        }
    }
}
