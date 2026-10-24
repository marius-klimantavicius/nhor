using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using ThorVG;
using Xunit;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace Marius.Winter.Tests;

// Execute every case on an owned Xvfb display, including the hidden Windows.
// This references the shipped Razor App, rather than maintaining a smaller copy.
public class PlaygroundShutdownTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ActualPlaygroundTreeReleasesEverySceneAndRuntime(bool explicitHostDisposal, bool switchTabs)
    {
        WindowRegressionOwner.Run(() =>
        {
            var window = new Window(900, 650, "Playground shutdown regression", Theme.Light, RenderBackend.SW);
            var host = window.UseBlazor(s => s.AddSingleton<ShutdownProbe>());
            var app = host.AddComponent<Blazor.Playground.App>();
            var probe = host.AddComponent<ProbeComponent>();
            Assert.True(app.IsCompletedSuccessfully);
            Assert.True(probe.IsCompletedSuccessfully);
            WindowRegressionOwner.Flush(window);
            Draw(window);
            var elements = Descendants(window).ToArray();
            var tabs = Assert.Single(elements.OfType<TabWidget>());
            Assert.Equal(9, tabs.TabCount);
            Assert.Contains(elements, e => e is ScrollPanel);
            Assert.Contains(elements, e => e is TreeView);
            Assert.Contains(elements, e => e is SvgImage);
            if (switchTabs)
            {
                // Exercise the real native tree's layout/render transitions,
                // including Tree, Controls, Rich and the initially selected tab.
                foreach (int index in new[] { 6, 2, 8, 0 })
                {
                    tabs.ActiveTab = index;
                    WindowRegressionOwner.Flush(window);
                    Draw(window);
                }
            }
            var paints = PaintTree(window.Scene).Distinct().ToArray();
            Exception? failure;
            if (explicitHostDisposal)
            {
                Task disposal = host.DisposeAsync().AsTask();
                failure = Record.Exception(() => Complete(window, disposal));
                var closeFailure = Record.Exception(window.Dispose);
                output.WriteLine("Host failure: {0}; Window failure: {1}", failure, closeFailure);
                Assert.Null(failure);
                Assert.Null(closeFailure);
            }
            else
            {
                failure = Record.Exception(window.Dispose);
                output.WriteLine("Window shutdown: {0}", failure);
                Assert.Null(failure);
            }
            Assert.All(elements, e => Assert.True(e.IsDisposed, e.GetType().Name));
            Assert.All(elements, e => Assert.Empty(e.Scene.Paints()));
            Assert.All(paints, p => Assert.Equal((ushort)0, p.RefCnt()));
            Assert.All(paints, p => Assert.Null(Renderer(p)));
            Assert.Empty(window.Children);
            Assert.Equal(1, probe.Result.Probe.Calls);
            Assert.Equal(Environment.CurrentManagedThreadId, probe.Result.Probe.Owner);
            Assert.True(probe.Result.Probe.RuntimeAlive);
            Assert.True(host.DisposeAsync().IsCompletedSuccessfully);
            Assert.Equal(0, RuntimeCount());
            Assert.Equal(0, LiveSoftwareRenderers());
        });
    }

    [Theory]
    [InlineData("scroll")]
    [InlineData("tree")]
    [InlineData("log")]
    public void ScrollbarReorderingRetainsPreparedPaintsUntilTheirSceneReleasesThem(string control)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(240, 140, "Scrollbar owner regression");
            Element element = control switch
            {
                "scroll" => new ScrollPanel(),
                "tree" => new TreeView(),
                _ => new LogView()
            };
            window.AddChild(element);
            element.Arrange(new RectF(0, 0, 160, 100));
            Draw(window);
            var scrollbars = element.Scene.Paints().OfType<Shape>().ToArray();
            Assert.Equal(control == "log" ? 2 : 4, scrollbars.Length);
            var renderers = scrollbars.Select(Renderer).ToArray();
            Assert.All(renderers, Assert.NotNull);
            element.Arrange(new RectF(0, 0, 170, 110));
            // No intervening canvas update may resurrect a paint destroyed by
            // an unbalanced remove/add. A scene owner remains exactly one.
            for (int i = 0; i < scrollbars.Length; ++i)
            {
                Assert.Equal((ushort)1, scrollbars[i].RefCnt());
                Assert.Same(renderers[i], Renderer(scrollbars[i]));
                Assert.Same(element.Scene, scrollbars[i].Parent());
            }
            window.Dispose();
            Assert.All(scrollbars, p => Assert.Null(Renderer(p)));
            Assert.All(scrollbars, p => Assert.Equal((ushort)0, p.RefCnt()));
            Assert.Equal(0, RuntimeCount());
            Assert.Equal(0, LiveSoftwareRenderers());
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RenderedScrollPanelCanLoseAChildThenDetachAndBeReused(bool tabWrapper)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(240, 140, "Panel and tab detach regression");
            var parent = new Panel();
            if (tabWrapper)
            {
                var tabs = new TabWidget();
                window.AddChild(tabs); tabs.AddTab("Content", parent);
                tabs.Arrange(new RectF(0, 0, 230, 130));
            }
            else window.AddChild(parent);
            var scroll = new ScrollPanel(); var label = new Label("retained content");
            scroll.AddChild(label); parent.AddChild(scroll);
            parent.Arrange(new RectF(0, 0, 210, 120));
            scroll.Arrange(new RectF(0, 0, 180, 100));
            Draw(window);
            scroll.RemoveChild(label); label.Dispose();
            Assert.Null(Record.Exception(() => parent.RemoveChild(scroll)));
            Assert.Equal((ushort)1, scroll.Scene.RefCnt());
            Assert.Null(scroll.Parent);
            parent.AddChild(scroll);
            Draw(window);
            window.Dispose();
            Assert.True(scroll.IsDisposed);
            Assert.Equal((ushort)0, scroll.Scene.RefCnt());
            Assert.Equal(0, LiveSoftwareRenderers());
        });
    }

    [Fact]
    public void PlainWindowReleasesItsRuntimeWithoutBlazorOrScrollbarMutation()
    {
        WindowRegressionOwner.Run(() =>
        {
            var window = new Window(120, 80, "Final runtime owner regression");
            Draw(window);
            var paints = PaintTree(window.Scene).ToArray();
            window.Dispose();
            Assert.All(paints, p => Assert.Equal((ushort)0, p.RefCnt()));
            Assert.All(paints, p => Assert.Null(Renderer(p)));
            Assert.Equal(0, RuntimeCount());
            Assert.Equal(0, LiveSoftwareRenderers());
        });
    }

    [Fact]
    public void NativeSceneRemovalRequiresAnIndependentReferenceToReusePreparedPaint()
    {
        Assert.Equal(Result.Success, Initializer.Init());
        var canvas = SwCanvas.Gen()!;
        var scene = Scene.Gen();
        var shape = Shape.Gen(); shape.AppendRect(0, 0, 12, 12); shape.SetFill(255, 0, 0);
        Assert.Equal((ushort)0, shape.RefCnt());
        scene.Add(shape); canvas.Add(scene);
        canvas.Target(new uint[24 * 24], 24, 24, 24, ColorSpace.ABGR8888);
        Contracts.Draw(canvas, true);
        var renderer = Renderer(shape); Assert.NotNull(renderer);
        Assert.Equal((ushort)1, shape.RefCnt());
        shape.Ref();
        Assert.Equal(Result.Success, scene.Remove(shape));
        Assert.Equal((ushort)1, shape.RefCnt());
        Assert.Same(renderer, Renderer(shape));
        Assert.Equal(Result.Success, scene.Add(shape));
        Assert.Equal((ushort)1, shape.Unref());
        Assert.Same(renderer, Renderer(shape));
        Assert.Equal(Result.Success, scene.Remove(shape));
        Assert.Equal((ushort)0, shape.RefCnt());
        Assert.Null(Renderer(shape));
        canvas.Dispose();
        Assert.Equal(Result.Success, Initializer.Term());
    }

    public sealed class ShutdownProbe(Window window) : IAsyncDisposable
    {
        public int Calls, Owner;
        public bool RuntimeAlive;
        public async ValueTask DisposeAsync()
        {
            Calls++;
            await Task.Yield();
            Owner = Environment.CurrentManagedThreadId;
            RuntimeAlive = !window.IsDisposed && window.GlfwWindow != null;
        }
    }
    public sealed class ProbeComponent : ComponentBase
    {
        [Inject] public ShutdownProbe Probe { get; set; } = null!;
    }

    private static IEnumerable<Element> Descendants(Element element)
    {
        yield return element;
        foreach (var child in element.Children)
            foreach (var descendant in Descendants(child)) yield return descendant;
    }
    private static IEnumerable<Paint> PaintTree(Paint paint)
    {
        yield return paint;
        if (paint is Scene scene)
            foreach (var child in scene.Paints())
                foreach (var descendant in PaintTree(child)) yield return descendant;
    }
    internal static object? Renderer(Paint paint)
    {
        var impl = typeof(Paint).GetField("pImpl", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(paint)!;
        return impl.GetType().GetField("renderer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(impl);
    }
    private static int RuntimeCount() => (int)typeof(Initializer).Assembly.GetType("ThorVG.TvgCommon")!
        .GetField("engineInit", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
    private static int LiveSoftwareRenderers() => Math.Max(0, (int)typeof(SwCanvas).Assembly.GetType("ThorVG.SwRenderer")!
        .GetField("rendererCnt", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!);
    private static void Draw(Window window) => Contracts.Draw((Canvas)typeof(Window)
        .GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!, true);
    private static void Complete(Window window, Task task)
    {
        typeof(Window).GetMethod("CompleteTeardown", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, new object[] { new ValueTask(task) });
    }
}
