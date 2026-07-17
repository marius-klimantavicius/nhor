// Ported from ThorVG/src/renderer/gpu_engine/gl/tvgGlStencilCoverBatch.h and .cpp

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ThorVG
{
    public unsafe class GlStencilCoverBatch
    {
        private const uint BatchRegionMaxCount = 512;
        private const uint BatchVertexMaxCount = 262144;
        private const uint BatchIndexMaxCount = 1048576;
        private const uint CoverVertexCount = 6;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct StencilCoverVertex
        {
            public float x;
            public float y;
            public RGBA color;
        }

        private GlRenderPass? pass;
        private GlStencilCoverTask? task;
        private GlRenderTask? stencilTask;
        private GlStencilMode mode;
        private readonly List<RenderRegion> bounds = new List<RenderRegion>();
        private RenderRegion stencilViewBounds;
        private RenderRegion coverViewBounds;
        private uint vertexCount;
        private uint indexOffset;
        private uint indexCount;
        private bool clipped;
        private bool ySorted;
        private bool open;

        public void Clear()
        {
            pass = null;
            task = null;
            stencilTask = null;
            mode = GlStencilMode.None;
            bounds.Clear();
            stencilViewBounds = default;
            coverViewBounds = default;
            vertexCount = indexOffset = indexCount = 0;
            clipped = ySorted = open = false;
        }

        public GlRenderTask Prepare(GlProgram? stencilProgram, GlRenderPass renderPass, GlRenderTask coverTask,
            GlGeometry geometry, GlStageBuffer gpuBuffer, RenderUpdateFlag flag, GlStencilMode stencilMode,
            bool isClipped, int depth, in Matrix viewMatrix, in RenderRegion passViewport, RGBA? color,
            in RenderRegion viewBounds, out RenderRegion geometryBounds, out GlGeometryBuffer stencilBuffer,
            out uint* stencilIndices, out bool merge)
        {
            var stroke = (flag & (RenderUpdateFlag.Stroke | RenderUpdateFlag.GradientStroke)) != 0;
            var bbox = stroke ? geometry.strokeBounds : geometry.fillBounds;
            geometryBounds = stroke ? GpuCommon.GpuTransformBounds(bbox, geometry.matrix) : bbox;
            geometryBounds.IntersectWith(viewBounds);

            var x = geometryBounds.Sx() - passViewport.Sx();
            var y = geometryBounds.Sy() - passViewport.Sy();
            var w = geometryBounds.Sw();
            var h = geometryBounds.Sh();
            var yGl = passViewport.Sh() - y - h;
            var stencilViewport = new RenderRegion(x, yGl, x + w, yGl + h);
            coverTask.SetViewport(stencilViewport);

            if (color.HasValue) AddSolidLayout(coverTask, gpuBuffer, bbox, color.Value);
            else AddPositionLayout(coverTask, gpuBuffer, bbox);
            coverTask.mUseDrawArrays = true;
            coverTask.mArrayMode = GL.GL_TRIANGLES;
            coverTask.mArrayOffset = 0;
            coverTask.mIndexCount = CoverVertexCount;

            stencilBuffer = stroke ? geometry.stroke : geometry.fill;
            merge = Mergeable(renderPass, stencilMode, isClipped, geometryBounds, stencilBuffer);

            var stencil = new GlRenderTask(stencilProgram);
            stencil.SetViewMatrix(viewMatrix);
            stencil.SetDrawDepth(depth);
            stencilIndices = DrawStencilGeometry(stencil, gpuBuffer, stencilBuffer);
            stencil.SetViewport(stencilViewport);
            return stencil;
        }

        public void Draw(GlRenderPass renderPass, GlRenderTask stencil, GlRenderTask cover, bool merge,
            GlStencilMode stencilMode, bool isClipped, in RenderRegion geometryBounds,
            in RenderRegion viewBounds, GlGeometryBuffer stencilBuffer, uint* stencilIndices)
        {
            if (merge) Append(stencil, cover, geometryBounds, viewBounds, stencilBuffer, stencilIndices);
            else EmitSingle(renderPass, stencil, cover, stencilMode, isClipped, geometryBounds, viewBounds, stencilBuffer);
        }

        private static void AddPositionLayout(GlRenderTask task, GlStageBuffer gpuBuffer, in RenderRegion bbox)
        {
            var vertices = stackalloc float[] {
                bbox.min.x, bbox.min.y, bbox.min.x, bbox.max.y, bbox.max.x, bbox.min.y,
                bbox.max.x, bbox.min.y, bbox.min.x, bbox.max.y, bbox.max.x, bbox.max.y };
            task.AddVertexLayout(new GlVertexLayout {
                index = 0, size = 2, stride = 2 * sizeof(float),
                offset = gpuBuffer.PushAux(vertices, 12 * sizeof(float)),
                type = GL.GL_FLOAT, normalized = GL.GL_FALSE, arrayBufferId = gpuBuffer.GetAuxBufferId()
            });
        }

        private static void AddSolidLayout(GlRenderTask task, GlStageBuffer gpuBuffer, in RenderRegion bbox, in RGBA color)
        {
            var vertices = stackalloc StencilCoverVertex[6];
            vertices[0] = new StencilCoverVertex { x = bbox.min.x, y = bbox.min.y, color = color };
            vertices[1] = new StencilCoverVertex { x = bbox.min.x, y = bbox.max.y, color = color };
            vertices[2] = new StencilCoverVertex { x = bbox.max.x, y = bbox.min.y, color = color };
            vertices[3] = vertices[2];
            vertices[4] = vertices[1];
            vertices[5] = new StencilCoverVertex { x = bbox.max.x, y = bbox.max.y, color = color };
            var offset = gpuBuffer.PushAux(vertices, 6 * (uint)sizeof(StencilCoverVertex));
            var buffer = gpuBuffer.GetAuxBufferId();
            task.AddVertexLayout(new GlVertexLayout { index = 0, size = 2, stride = (uint)sizeof(StencilCoverVertex), offset = offset, type = GL.GL_FLOAT, normalized = GL.GL_FALSE, arrayBufferId = buffer });
            task.AddVertexLayout(new GlVertexLayout { index = 1, size = 4, stride = (uint)sizeof(StencilCoverVertex), offset = offset + 2 * sizeof(float), type = GL.GL_UNSIGNED_BYTE, normalized = GL.GL_TRUE, arrayBufferId = buffer });
        }

        private static uint* DrawStencilGeometry(GlRenderTask task, GlStageBuffer gpuBuffer, GlGeometryBuffer buffer)
        {
            var vertexOffset = gpuBuffer.Push(buffer.vertex.data, buffer.vertex.count * sizeof(float));
            uint* indices = null;
            var indexOffset = gpuBuffer.ReserveIndex(buffer.index.count * sizeof(uint), (void**)&indices);
            for (uint i = 0; i < buffer.index.count; ++i) indices[i] = buffer.index[i];
            task.AddVertexLayout(new GlVertexLayout { index = 0, size = 2, stride = 2 * sizeof(float), offset = vertexOffset });
            task.SetDrawRange(indexOffset, buffer.index.count);
            return indices;
        }

        private bool Mergeable(GlRenderPass renderPass, GlStencilMode stencilMode, bool isClipped, in RenderRegion region, GlGeometryBuffer buffer)
        {
            if (!open || pass != renderPass || renderPass.LastTask() != task || mode != stencilMode || clipped != isClipped || region.Invalid()) return false;
            if ((uint)bounds.Count >= BatchRegionMaxCount) return false;
            var incomingVertices = buffer.vertex.count / 2;
            var incomingIndices = buffer.index.count;
            if (incomingVertices > BatchVertexMaxCount || vertexCount > BatchVertexMaxCount - incomingVertices) return false;
            if (incomingIndices > BatchIndexMaxCount || indexCount > BatchIndexMaxCount - incomingIndices) return false;
            return !Intersects(region);
        }

        private bool Intersects(in RenderRegion region)
        {
            var min = ySorted ? region.min.y : region.min.x;
            var max = ySorted ? region.max.y : region.max.x;
            for (int i = 0; i < bounds.Count; ++i)
            {
                var candidate = bounds[i];
                var candidateMin = ySorted ? candidate.min.y : candidate.min.x;
                if ((ySorted ? candidate.max.y : candidate.max.x) <= min) continue;
                if (candidateMin >= max) break;
                if (candidate.Intersected(region)) return true;
            }
            return false;
        }

        private void AddBounds(in RenderRegion region)
        {
            var min = ySorted ? region.min.y : region.min.x;
            var i = bounds.Count;
            bounds.Add(region);
            while (i > 0 && (ySorted ? bounds[i - 1].min.y : bounds[i - 1].min.x) > min)
            {
                bounds[i] = bounds[i - 1];
                --i;
            }
            bounds[i] = region;
        }

        private void EmitSingle(GlRenderPass renderPass, GlRenderTask stencil, GlRenderTask cover,
            GlStencilMode stencilMode, bool isClipped, in RenderRegion region,
            in RenderRegion viewBounds, GlGeometryBuffer buffer)
        {
            var batchTask = new GlStencilCoverTask(stencil, cover, stencilMode);
            renderPass.AddRenderTask(batchTask);
            pass = renderPass;
            task = batchTask;
            mode = stencilMode;
            clipped = isClipped;
            ySorted = renderPass.GetViewport().Sh() > renderPass.GetViewport().Sw();
            coverViewBounds = viewBounds;
            bounds.Clear();
            SetStencilMergeTarget(stencil, viewBounds, buffer);
            open = region.Valid();
            if (open) AddBounds(region);
        }

        private void Append(GlRenderTask stencil, GlRenderTask cover, in RenderRegion region,
            in RenderRegion viewBounds, GlGeometryBuffer buffer, uint* stencilIndices)
        {
            if (!MergeCover(cover, viewBounds))
            {
                task!.mCoverTasks.Add(cover);
                coverViewBounds = viewBounds;
            }
            if (!MergeStencil(stencil, viewBounds, buffer, stencilIndices))
            {
                task!.mStencilTasks.Add(stencil);
                SetStencilMergeTarget(stencil, viewBounds, buffer);
            }
            AddBounds(region);
        }

        private void SetStencilMergeTarget(GlRenderTask stencil, in RenderRegion viewBounds, GlGeometryBuffer buffer)
        {
            stencilTask = stencil;
            stencilViewBounds = viewBounds;
            vertexCount = buffer.vertex.count / 2;
            indexOffset = stencil.mIndexOffset;
            indexCount = stencil.mIndexCount;
        }

        private bool MergeStencil(GlRenderTask stencil, in RenderRegion viewBounds, GlGeometryBuffer buffer, uint* stencilIndices)
        {
            var incomingVertices = buffer.vertex.count / 2;
            if (stencilTask == null || vertexCount == 0 || incomingVertices == 0 || stencilIndices == null) return false;
            if (!(stencilViewBounds == viewBounds) || stencilTask.mProgram != stencil.mProgram) return false;
            if (stencilTask.mUseViewMatrix != stencil.mUseViewMatrix) return false;
            if (stencilTask.mUseViewMatrix && !TvgMath.MatrixEqual(stencilTask.mViewMatrix, stencil.mViewMatrix)) return false;
            if (stencilTask.mVertexLayout.count != 1 || stencil.mVertexLayout.count != 1) return false;
            ref var layout = ref stencilTask.mVertexLayout[0];
            ref var appendLayout = ref stencil.mVertexLayout[0];
            if (layout.index != appendLayout.index || layout.size != appendLayout.size || layout.stride != appendLayout.stride) return false;
            if (layout.offset + vertexCount * layout.stride != appendLayout.offset || layout.type != appendLayout.type || layout.normalized != appendLayout.normalized || layout.arrayBufferId != appendLayout.arrayBufferId) return false;
            if (stencil.mIndexOffset != indexOffset + indexCount * sizeof(uint)) return false;

            for (uint i = 0; i < buffer.index.count; ++i) stencilIndices[i] = buffer.index[i] + vertexCount;
            indexCount += stencil.mIndexCount;
            vertexCount += incomingVertices;
            stencilTask.mIndexOffset = indexOffset;
            stencilTask.mIndexCount = indexCount;
            stencilTask.mDrawDepth = stencil.mDrawDepth;
            stencilTask.mViewport.AddWith(stencil.mViewport);
            return true;
        }

        private bool MergeCover(GlRenderTask cover, in RenderRegion viewBounds)
        {
            if (task == null || task.mCoverTasks.Count == 0 || !(coverViewBounds == viewBounds)) return false;
            var dst = task.mCoverTasks[task.mCoverTasks.Count - 1];
            if (dst.mProgram != cover.mProgram || dst.mBindingResources.count > 0 || cover.mBindingResources.count > 0) return false;
            if (!dst.mUseDrawArrays || !cover.mUseDrawArrays || dst.mArrayMode != GL.GL_TRIANGLES || cover.mArrayMode != GL.GL_TRIANGLES) return false;
            if (dst.mUseVertexColor || cover.mUseVertexColor || dst.mUseViewMatrix != cover.mUseViewMatrix) return false;
            if (dst.mUseViewMatrix && !TvgMath.MatrixEqual(dst.mViewMatrix, cover.mViewMatrix)) return false;
            if (!SolidCoverLayout(ref dst.mVertexLayout) || !SolidCoverLayout(ref cover.mVertexLayout)) return false;

            ref var position = ref dst.mVertexLayout[0];
            ref var coverPosition = ref cover.mVertexLayout[0];
            ref var color = ref dst.mVertexLayout[1];
            ref var coverColor = ref cover.mVertexLayout[1];
            if (position.stride != coverPosition.stride || position.arrayBufferId != coverPosition.arrayBufferId || color.stride != coverColor.stride || color.arrayBufferId != coverColor.arrayBufferId) return false;
            var dstEnd = position.offset + (dst.mArrayOffset + dst.mIndexCount) * position.stride;
            var coverStart = coverPosition.offset + cover.mArrayOffset * coverPosition.stride;
            var colorEnd = color.offset + (dst.mArrayOffset + dst.mIndexCount) * color.stride;
            var coverColorStart = coverColor.offset + cover.mArrayOffset * coverColor.stride;
            Debug.Assert(dstEnd == coverStart && colorEnd == coverColorStart);
            if (dstEnd != coverStart || colorEnd != coverColorStart) return false;
            dst.mIndexCount += cover.mIndexCount;
            dst.mViewport.AddWith(cover.mViewport);
            dst.mDrawDepth = cover.mDrawDepth;
            return true;
        }

        private static bool SolidCoverLayout(ref Array<GlVertexLayout> layouts)
        {
            if (layouts.count != 2) return false;
            ref var position = ref layouts[0];
            ref var color = ref layouts[1];
            return position.index == 0 && position.size == 2 && position.type == GL.GL_FLOAT && position.normalized == GL.GL_FALSE &&
                   color.index == 1 && color.size == 4 && color.type == GL.GL_UNSIGNED_BYTE && color.normalized == GL.GL_TRUE &&
                   position.stride == color.stride && position.arrayBufferId != 0 && position.arrayBufferId == color.arrayBufferId &&
                   color.offset == position.offset + 2 * sizeof(float);
        }
    }
}
