using System.Collections.Generic;
using Xunit;

namespace ThorVG.Tests;

public unsafe class tvgGlTaskLifetimeUpdateTests
{
    [Fact]
    public void PassTransfersAndNestedTaskOwnersReleaseTheirStorage()
    {
        // Pinned native GlRenderTask/GlRenderPass destructors own these child
        // graphs. Their CPU storage must be released at sync, before GL teardown.
        var all = new List<GlRenderTask>();
        T Store<T>(T task) where T : GlRenderTask
        {
            task.AddVertexLayout(new GlVertexLayout { index = 0, size = 2, stride = 8, arrayBufferId = 1 });
            task.AddBindResource(new GlBindingResource(0, 1, 0));
            all.Add(task);
            return task;
        }
        GlRenderTask Leaf() => Store(new GlRenderTask(null));

        using var target = new GlRenderTarget { width = 96, height = 64, viewport = new RenderRegion(0, 0, 96, 64) };
        using var source = new GlRenderPass(target);
        var cover = Store(new GlStencilCoverTask(Leaf(), Leaf(), GlStencilMode.FillEvenOdd));
        var clip = Store(new GlClipTask(Leaf(), Leaf()));
        var compose = Store(new GlComposeTask(null, 0, target, new List<GlRenderTask> { cover, clip }));
        var complex = Store(new GlComplexBlendTask(null, target, target, Leaf(), compose));
        var blur = Store(new GlGaussianBlurTask(target, target, target) { horzTask = Leaf(), vertTask = Leaf() });
        var shadow = Store(new GlEffectDropShadowTask(null, target, target, target) { horzTask = Leaf(), vertTask = Leaf() });
        source.AddRenderTask(complex);
        source.AddRenderTask(blur);
        source.AddRenderTask(shadow);
        using var moved = new GlRenderPass(source);
        source.Dispose();
        Assert.Null(source.LastTask());
        foreach (var task in all) Assert.True(task.mVertexLayout.data != null && task.mBindingResources.data != null);

        using var root = Store(moved.EndRenderPass<GlDrawBlitTask>(null, 0));
        root.SetPrevTask(Store(new GlSceneBlendTask(null, 0, target, new List<GlRenderTask> { Leaf() })));
        moved.Dispose();
        Assert.Null(moved.LastTask());
        foreach (var task in all) Assert.True(task.mVertexLayout.data != null && task.mBindingResources.data != null);
        root.Dispose();
        root.Dispose();
        foreach (var task in all)
        {
            Assert.True(task.mVertexLayout.data == null && task.mBindingResources.data == null);
            Assert.Equal(0u, task.mVertexLayout.count);
            Assert.Equal(0u, task.mBindingResources.count);
        }

        var abandoned = Leaf();
        using var unfinished = new GlRenderPass(target);
        unfinished.AddRenderTask(abandoned);
        unfinished.Dispose();
        Assert.True(abandoned.mVertexLayout.data == null && abandoned.mBindingResources.data == null);
        Assert.Equal(96u, target.width); // render targets are borrowed by tasks/passes
        Assert.Equal(64u, target.height);
    }
}
