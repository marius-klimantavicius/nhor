using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Marius.Winter.Blazor.Converter;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using ThorVG;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Marius.Winter.Tests;

public class WorkerGraphicsLifetimeTests
{
    internal static string Artifacts
    {
        get
        {
            var path = new DirectoryInfo(AppContext.BaseDirectory);
            while (path != null && !File.Exists(Path.Combine(path.FullName, "Winter.slnx"))) path = path.Parent;
            string folder = Path.Combine(path!.FullName, "tmp", "winter-fixes", "test-artifacts");
            Directory.CreateDirectory(folder); return folder;
        }
    }

    [Fact]
    public async Task OrdinaryConverterWorkBalancesItsGraphicsLeaseAndCanvasLifetime()
    {
        using var pump = new NativePump();
        int initial = TvgCommon.engineInit;
        string path = Path.Combine(Artifacts, "runtime-conversion.svg");
        File.WriteAllText(path, "<svg xmlns='http://www.w3.org/2000/svg' width='16' height='12'><rect width='16' height='12' fill='red'/></svg>");
        for (int i = 0; i < 3; i++)
        {
            byte[] png = await Task.Run(() => ConverterService.ConvertSvgToPng(path));
            Assert.Equal(new byte[] { 137, 80, 78, 71 }, png.Take(4));
            Assert.Equal(initial, TvgCommon.engineInit);
        }
    }

    [Fact]
    public async Task WindowClosureDoesNotTerminateAnAcquiredWorkerRenderer()
    {
        using var pump = new NativePump();
        var acquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = Task.Run(async () =>
        {
            Assert.Equal(Result.Success, Initializer.Init());
            using var canvas = SwCanvas.Gen()!;
            try
            {
                var pixels = new uint[16 * 16]; canvas.Target(pixels, 16, 16, 16, ColorSpace.ARGB8888);
                var shape = Shape.Gen(); shape.AppendRect(0, 0, 16, 16, 0, 0); shape.SetFill(255, 0, 0); canvas.Add(shape);
                acquired.SetResult(); await release.Task;
                Contracts.Draw(canvas, true); Assert.Equal(0xffff0000u, pixels[80]);
            }
            finally { canvas.Dispose(); Assert.Equal(Result.Success, Initializer.Term()); }
        });
        await acquired.Task.WaitAsync(TimeSpan.FromSeconds(5)); await pump.Close(); await pump.Ended;
        release.SetResult(); await worker.WaitAsync(TimeSpan.FromSeconds(5));
    }
}

public class NativeSchedulingIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewAnimationAndTooltipDeadlinesDoNotInheritEarlierIdleTime(bool tooltip)
    {
        var box = new Box { Tooltip = "Details" }; using var pump = new NativePump(w => w.AddChild(box));
        await Task.Delay(650);
        if (!tooltip)
        {
            var first = new TaskCompletionSource<float>(TaskCreationOptions.RunContinuationsAsynchronously);
            await pump.Call(w => w.Animator.Start(new Marius.Winter.Animation { Duration = 0.3f, Apply = t => first.TrySetResult(t) }));
            float firstFrame = await first.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(firstFrame < 0.4f, $"New animation inherited idle time: first frame {firstFrame}");
            return;
        }
        await pump.Call(w => typeof(Window).GetMethod("UpdateTooltipTarget", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(w, new object[] { box, 10f, 10f }));
        await Task.Delay(60);
        await pump.Call(w => Assert.Null(typeof(Window).GetField("_tooltipOverlay", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)));
        await Task.Delay(520);
        await pump.Call(w => Assert.NotNull(typeof(Window).GetField("_tooltipOverlay", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)));
    }

    [Fact]
    public async Task RepostingQueueCannotStarveFinalPaintAndConcurrentCloseSettlesAllTasks()
    {
        var box = new Box(); using var pump = new NativePump(w => w.AddChild(box));
        int posts = 0; bool paintedDuringChain = false; bool stop = false;
        await pump.Call(w =>
        {
            Action again = null!;
            again = () =>
            {
                posts++;
                if (WindowRuntimeTests.Buffer(w)[10 * 200 + 10] == 0xff0000ffu) paintedDuringChain = true;
                if (!stop) w.DispatcherQueue.Enqueue(again);
            };
            box.Paint.SetFill(0, 0, 255); w.Dirty = true; w.DispatcherQueue.Enqueue(again);
        });
        await Task.Delay(80); await pump.Call(_ => stop = true);
        Assert.True(posts > 64); Assert.True(paintedDuringChain, "Rendering must progress while queue work keeps arriving");
        var tasks = Enumerable.Range(0, 80).Select(_ => pump.Window.Dispatcher.InvokeAsync(async () => await Task.Delay(500))).ToArray();
        pump.Window.Close(); await pump.Ended.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.All(tasks, t => Assert.True(t.IsCompleted));
        Assert.Throws<ObjectDisposedException>(() => pump.Window.DispatcherQueue.Enqueue(() => { }));
        foreach (var task in tasks) _ = task.Exception;
    }

    [Fact]
    public async Task FocusedCursorBlinkAndTooltipDeadlineAdvanceUnderWaitLoop()
    {
        TextBox text = null!; using var pump = new NativePump(w => { text = new TextBox("Winter") { Tooltip = "Details" }; w.AddChild(text); text.Arrange(new RectF(0, 0, 150, 32)); w.SetFocus(text); });
        await Task.Delay(30); byte initial = 0;
        await pump.Call(_ => initial = Cursor(text).pImpl.opacity);
        await Task.Delay(550);
        await pump.Call(w =>
        {
            Assert.True(w.Animator.HasActiveAnimations); Assert.NotEqual(initial, Cursor(text).pImpl.opacity);
            typeof(Window).GetMethod("UpdateTooltipTarget", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(w, new object[] { text, 10f, 10f });
        });
        await Task.Delay(600);
        await pump.Call(w => Assert.NotNull(typeof(Window).GetField("_tooltipOverlay", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)));
    }
    private static Shape Cursor(TextBox box) => (Shape)typeof(TextBox).GetField("_cursorShape", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(box)!;

    [Theory]
    [InlineData(1f)]
    [InlineData(2f)]
    [InlineData(1.25f)]
    public async Task ScaleContractRendersRootOverlayAndLogicalPointerAcrossGc(float scale)
    {
        var box = new Box(); var overlay = new Box(0, 255); int clicked = 0;
        using var pump = new NativePump(w =>
        {
            w.AddChild(box); box.Arrange(new RectF(20, 20, 24, 24));
            overlay.Arrange(new RectF(60, 20, 24, 24)); w.ShowOverlay(overlay);
        });
        await pump.Call(w => w.GlfwWindow!.Callbacks.Fbsize!(w.GlfwWindow, (int)(200 * scale), (int)(120 * scale)));
        GC.Collect(); GC.WaitForPendingFinalizers(); await Task.Delay(40);
        await pump.Call(w =>
        {
            var pixels = WindowRuntimeTests.Buffer(w); int stride = (int)(200 * scale), y = (int)(30 * scale);
            uint rootPixel = pixels[y * stride + (int)(30 * scale)], overlayPixel = pixels[y * stride + (int)(70 * scale)];
            Assert.True(((rootPixel >> 16) & 255) > (rootPixel & 255)); Assert.True((overlayPixel & 255) > ((overlayPixel >> 16) & 255));
            Assert.Same(box, w.HitTest(30, 30)); box.WindowToLocal(30, 30, out float x, out float localY); Assert.Equal(10, x); Assert.Equal(10, localY);
            w.GlfwWindow!.Callbacks.CursorPos!(w.GlfwWindow, 30, 30);
            w.GlfwWindow.Callbacks.MouseButton!(w.GlfwWindow, 0, Glfw.GLFW.GLFW_PRESS, 0); Assert.Same(box, w.FocusedElement); clicked++;
        });
        Assert.Equal(1, clicked);
        await pump.Call(w => { w.RemoveOverlay(overlay); overlay.Dispose(); });
    }

    [Fact]
    public async Task ActualNativeResizeAndInputRetargetTheLogicalAndPhysicalClientAreas()
    {
        using var pump = new NativePump();
        await pump.Call(w => Glfw.Glfw.glfwSetWindowSize(w.GlfwWindow!, 260, 150)); await Task.Delay(60);
        await pump.Call(w =>
        {
            Assert.Equal(260, w.Bounds.W); Assert.Equal(150, w.Bounds.H);
            Assert.Equal(260u, w.FramebufferWidth); Assert.Equal(150u, w.FramebufferHeight);
            Assert.Equal(260 * 150, WindowRuntimeTests.Buffer(w).Length);
        });
    }

    [Fact]
    public void WrongThreadRunAndDisposeAreRejectedWithoutDamagingTheOwner()
    {
        using var window = new Window(100, 80, "Thread ownership contract");
        Exception? run = null, dispose = null;
        var thread = new Thread(() => { run = Record.Exception(window.Run); dispose = Record.Exception(window.Dispose); });
        thread.Start(); Assert.True(thread.Join(5000));
        Assert.IsType<InvalidOperationException>(run); Assert.IsType<InvalidOperationException>(dispose);
        Assert.False(window.IsDisposed);
    }

    [Fact]
    public async Task LegacyQueueStillSchedulesDrawForDirectPaintMutation()
    {
        var box = new Box(); using var pump = new NativePump(w => w.AddChild(box));
        await Task.Delay(30); await pump.Call(_ => box.Paint.SetFill(0, 0, 255)); await Task.Delay(40);
        await pump.Call(w => Assert.Equal(0xff0000ffu, WindowRuntimeTests.Buffer(w)[10 * 200 + 10]));
    }

    [Fact]
    public async Task AsyncHostDisposalRemainsIdempotentAfterItsWindowHasClosed()
    {
        WinterBlazorHost host = null!; using var pump = new NativePump(w => host = w.UseBlazor());
        await pump.Close(); await pump.Ended.WaitAsync(TimeSpan.FromSeconds(5));
        await ((IAsyncDisposable)host).DisposeAsync();
    }

    public sealed class DeferredSliderComponent : ComponentBase
    {
        [Parameter] public Action<float, int>? Changed { get; set; }
        protected override void BuildRenderTree(RenderTreeBuilder b)
        {
            b.OpenComponent<Marius.Winter.Blazor.Elements.Slider>(0);
            b.AddAttribute(1, "Min", 10f); b.AddAttribute(2, "Max", 100f); b.AddAttribute(3, "Value", 55f);
            b.AddAttribute(4, "OnValueChanged", EventCallback.Factory.Create<float>(this, async value => { await Task.Delay(20); Changed?.Invoke(value, Environment.CurrentManagedThreadId); }));
            b.CloseComponent();
        }
    }

    [Fact]
    public async Task ActualRendererDefersBatchGeneratedNativeEventsAndKeepsAsyncEventOnUi()
    {
        WinterBlazorHost host = null!; int owner = 0;
        using var pump = new NativePump(w => { owner = Environment.CurrentManagedThreadId; host = w.UseBlazor(); });
        var observed = new TaskCompletionSource<(float value, int thread)>(TaskCreationOptions.RunContinuationsAsynchronously);
        await host.AddComponent<DeferredSliderComponent>(new Dictionary<string, object> { ["Changed"] = (Action<float, int>)((value, id) => observed.TrySetResult((value, id))) });
        var result = await observed.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.Equal(55, result.value); Assert.Equal(owner, result.thread);
        await pump.Call(w => Assert.Single(w.Children));
    }

    [Theory]
    [InlineData("scroll")]
    [InlineData("dialog")]
    [InlineData("tabs")]
    public async Task DirtyDescendantsLayoutInsideManualLeavesAndHiddenTabs(string kind)
    {
        var content = new Panel { Layout = new StackLayout() }; content.AddChild(new Box());
        Element container;
        if (kind == "scroll") { var scroll = new ScrollPanel { Layout = new StackLayout() }; scroll.AddChild(content); container = scroll; }
        else if (kind == "dialog") { var dialog = new DialogWindow { Layout = new StackLayout() }; dialog.AddChild(content); container = dialog; }
        else { var tabs = new TabWidget(); tabs.AddTab("hidden", new Panel()); tabs.AddTab("active", content); tabs.ActiveTab = 1; container = tabs; }
        using var pump = new NativePump(w => { w.AddChild(container); container.Measure(180, 100); container.Arrange(new RectF(5, 5, 180, 100)); });
        var added = new Box(); await pump.Call(_ => content.AddChild(added)); await Task.Delay(60);
        await pump.Call(_ => { Assert.True(added.Bounds.W > 0); Assert.True(added.Bounds.H > 0); Assert.False(content.IsMeasureDirty); });
    }
}

public class WindowFailureContractTests
{
    [Theory]
    [InlineData("callback")]
    [InlineData("animation")]
    [InlineData("canvas")]
    public void RunFailurePropagatesAndDisposesCanvasBeforeNativeContext(string operation)
    {
        int before = TvgCommon.engineInit;
        using var window = new Window(100, 80, "Runtime failure contract");
        var field = typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!;
        if (operation == "callback") window.DispatcherQueue.Enqueue(() => throw new InvalidOperationException("callback failed"));
        else if (operation == "animation") window.Animator.Start(new Marius.Winter.Animation { Duration = 1, Apply = _ => throw new InvalidOperationException("animation failed") });
        else
        {
            ((Canvas)field.GetValue(window)!).Dispose();
            field.SetValue(window, SwCanvas.Gen()); // ordinary untargeted canvas returns a failure Result
        }
        var canvas = (Canvas)field.GetValue(window)!;
        var error = Assert.IsType<InvalidOperationException>(Record.Exception(window.Run));
        Assert.Contains(operation == "canvas" ? "update canvas" : operation + " failed", error.Message);
        Assert.True(window.IsDisposed); Assert.Null(window.GlfwWindow);
        Assert.Null(typeof(Canvas).GetField("renderer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(canvas));
        Assert.Equal(before, TvgCommon.engineInit);
    }

    [Fact]
    public void NativeCreationFailureIsActionableAndReleasesThePartialSession()
    {
        int before = TvgCommon.engineInit;
        int platform = new[] { Glfw.GLFW.GLFW_PLATFORM_COCOA, Glfw.GLFW.GLFW_PLATFORM_WIN32, Glfw.GLFW.GLFW_PLATFORM_X11, Glfw.GLFW.GLFW_PLATFORM_WAYLAND }
            .First(id => !Glfw.Glfw.glfwPlatformSupported(id));
        Glfw.Glfw.glfwInitHint(Glfw.GLFW.GLFW_PLATFORM, platform);
        try
        {
            var error = Assert.IsType<InvalidOperationException>(Record.Exception(() => { using var window = new Window(100, 80, "Unavailable GL context"); }));
            Assert.Contains("GLFW", error.Message); Assert.Contains("initialize", error.Message);
            Assert.Equal(before, TvgCommon.engineInit);
        }
        finally { Glfw.Glfw.glfwInitHint(Glfw.GLFW.GLFW_PLATFORM, Glfw.GLFW.GLFW_ANY_PLATFORM); }
        using var next = new Window(100, 80, "Recovered native session");
        Assert.False(next.IsDisposed);
    }

    [Fact]
    public void UnsupportedGlContextReleasesCanvasAndThePartialNativeWindow()
    {
        int before = TvgCommon.engineInit;
        Glfw.Glfw.glfwInitHint(Glfw.GLFW.GLFW_PLATFORM, Glfw.GLFW.GLFW_PLATFORM_NULL);
        try
        {
            var error = Record.Exception(() => { using var window = new Window(100, 80, "Unsupported GL target", backend: RenderBackend.GL); });
            Assert.NotNull(error); Assert.Contains("GL", error.Message); Assert.Equal(before, TvgCommon.engineInit);
        }
        finally { Glfw.Glfw.glfwInitHint(Glfw.GLFW.GLFW_PLATFORM, Glfw.GLFW.GLFW_ANY_PLATFORM); }
        using var next = new Window(100, 80, "Recovered after partial create");
    }
}
