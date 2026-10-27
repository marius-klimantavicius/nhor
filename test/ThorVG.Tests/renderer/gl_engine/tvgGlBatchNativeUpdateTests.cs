using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using ThorVG;

namespace ThorVG.Tests;
public unsafe class tvgGlBatchNativeUpdateTests
{
    [Fact]
    public void OrdinaryStencilBatchesMatchNativeAndReleaseDiscardedOwners()
    {
        using var memory = new MemoryStream();
        using var output = new BinaryWriter(memory);
        bool released = true, capacityReleased = true;
        void Word(uint v) => output.Write(v);
        void Region(RenderRegion r)
        {
            Word((uint)r.min.x);
            Word((uint)r.min.y);
            Word((uint)r.max.x);
            Word((uint)r.max.y);
        }

        var method = typeof(GlStencilCoverBatch).GetMethod("Mergeable", BindingFlags.NonPublic | BindingFlags.Instance)!;
        bool Mergeable(GlStencilCoverBatch b, GlRenderPass p, RenderRegion r, GlGeometryBuffer m) => (bool)method.Invoke(b, new object[] { p, GlStencilMode.FillNonZero, false, r, m })!;
        uint* indices = stackalloc uint[3];
        for (int vertical = 0; vertical < 2; ++vertical)
        {
            using var target = new GlRenderTarget
            {
                width = (uint)(vertical != 0 ? 96 : 2048),
                height = (uint)(vertical != 0 ? 2048 : 96),
                viewport = new(0, 0, vertical != 0 ? 96 : 2048, vertical != 0 ? 2048 : 96)
            };
            using var pass = new GlRenderPass(target);
            var batch = new GlStencilCoverBatch();
            using var mesh = new GlGeometryBuffer();
            foreach (float v in new[]
            {
                0f,
                0f,
                6f,
                0f,
                0f,
                6f
            }

            )
                mesh.vertex.Push(v);
            foreach (uint v in new[]
            {
                0u,
                1u,
                2u
            }

            )
                mesh.index.Push(v);
            var owners = new List<GlRenderTask>();
            try
            {
                for (uint n = 0; n < 150; ++n)
                {
                    uint position = n % 2 == 0 ? n / 2 : 149 - n / 2;
                    int x = vertical != 0 ? 0 : (int)position * 10, y = vertical != 0 ? (int)position * 10 : 0;
                    var box = new RenderRegion(x, y, x + 8, y + 8);
                    var cover = new GlRenderTask(null);
                    owners.Add(cover);
                    cover.SetViewMatrix(pass.GetViewMatrix());
                    cover.SetDrawDepth((int)n + 1);
                    cover.SetViewport(box);
                    cover.mUseDrawArrays = true;
                    cover.mArrayMode = GL.GL_TRIANGLES;
                    cover.mIndexCount = 6;
                    cover.AddVertexLayout(new() { index = 0, size = 2, stride = 12, offset = n * 72, type = GL.GL_FLOAT, normalized = GL.GL_FALSE, arrayBufferId = 11 });
                    cover.AddVertexLayout(new() { index = 1, size = 4, stride = 12, offset = n * 72 + 8, type = GL.GL_UNSIGNED_BYTE, normalized = GL.GL_TRUE, arrayBufferId = 11 });
                    var stencil = new GlRenderTask(null);
                    owners.Add(stencil);
                    stencil.SetViewMatrix(pass.GetViewMatrix());
                    stencil.SetDrawDepth((int)n + 1);
                    stencil.SetViewport(box);
                    stencil.SetDrawRange(n * 12, 3);
                    stencil.AddVertexLayout(new() { index = 0, size = 2, stride = 8, offset = n * 24, type = GL.GL_FLOAT, normalized = GL.GL_FALSE, arrayBufferId = 7 });
                    // Draw rewrites merged indices. Restore the original input
                    // for every draw when reusing the allocation outside the loops.
                    indices[0] = 0;
                    indices[1] = 1;
                    indices[2] = 2;
                    bool merge = Mergeable(batch, pass, box, mesh);
                    batch.Draw(pass, stencil, cover, merge, GlStencilMode.FillNonZero, false, box, target.viewport, mesh, indices);
                    var root = (GlStencilCoverTask)pass.LastTask()!;
                    var s = root.mStencilTasks[0];
                    var c = root.mCoverTasks[0];
                    Word(merge ? 1u : 0u);
                    Word((uint)root.mStencilTasks.Count);
                    Word((uint)root.mCoverTasks.Count);
                    Word(s.mIndexOffset);
                    Word(s.mIndexCount);
                    Word(c.mIndexCount);
                    Word((uint)s.mDrawDepth);
                    Word((uint)c.mDrawDepth);
                    Region(s.mViewport);
                    Region(c.mViewport);
                    for (int i = 0; i < 3; ++i)
                        Word(indices[i]);
                    if (merge)
                        released &= stencil.mVertexLayout.data == null && cover.mVertexLayout.data == null;
                }

                batch.Clear();
                Word(Mergeable(batch, pass, new(0, 0, 8, 8), mesh) ? 1u : 0u);
                var bounds = (List<RenderRegion>)typeof(GlStencilCoverBatch).GetField("bounds", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(batch)!;
                capacityReleased &= bounds.Capacity == 0;
            }
            finally
            {
                foreach (var owner in owners)
                    owner.Dispose();
            }
        }

        output.Flush();
        var native = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "gl-batch-merge-native-golden.bin"));
        Assert.True(memory.ToArray().AsSpan().SequenceEqual(native), "Every native merged index/range/depth/viewport/header must match.");
        Assert.True(released, "Merged incoming task owners must release their native arrays immediately.");
        Assert.True(capacityReleased, "Large per-batch bounds storage must be released at reset.");
    }
}
