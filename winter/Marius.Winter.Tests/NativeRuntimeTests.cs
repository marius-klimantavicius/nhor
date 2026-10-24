using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ThorVG;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Marius.Winter.Tests;

// Every Window is created, run and disposed on one dedicated thread. The test
// command owns Xvfb; no test attaches a window to the user's display.
internal sealed class NativePump : IDisposable
{
    private readonly Thread _thread;
    private readonly TaskCompletionSource<Window> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Window Window => _ready.Task.GetAwaiter().GetResult();
    internal Task Ended => _ended.Task;
    internal NativePump(Action<Window>? setup = null, RenderBackend backend = RenderBackend.SW)
    {
        _thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                window = new Window(200, 120, "Winter runtime regression", backend: backend);
                setup?.Invoke(window);
                window.DispatcherQueue.Enqueue(() => _ready.TrySetResult(window));
                window.Run(); _ended.TrySetResult();
            }
            catch (Exception ex) { _ready.TrySetException(ex); _ended.TrySetException(ex); }
            finally { window?.Dispose(); }
        }) { IsBackground = true };
        _thread.Start(); _ready.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
    }
    internal Task Call(Action<Window> action)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Window.DispatcherQueue.Enqueue(() => { try { action(Window); done.SetResult(); } catch (Exception ex) { done.SetException(ex); } });
        return done.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
    internal Task Close() => Call(w => Glfw.Glfw.glfwSetWindowShouldClose(w.GlfwWindow!, 1));
    public void Dispose()
    {
        if (!_ended.Task.IsCompleted) Close().GetAwaiter().GetResult();
        _ended.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        Assert.True(_thread.Join(1000), "Native Window owner must terminate");
    }
}

public class DispatcherRuntimeTests
{
    [Fact]
    public async Task LateFaultInReturnedTaskIsObservedEvenAfterWindowShutdown()
    {
        WinterRenderer renderer = null!;
        using var services = new ServiceCollection().BuildServiceProvider();
        using var pump = new NativePump(w => renderer = new WinterRenderer(w, services, NullLoggerFactory.Instance));
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task invocation = renderer.Dispatcher.InvokeAsync(() => { entered.SetResult(); return gate.Task; });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); await pump.Close(); await pump.Ended;
        Assert.True(invocation.IsCanceled);
        bool unobserved = false;
        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, e) =>
        {
            if (e.Exception.InnerExceptions.Any(ex => ex.Message == "late dispatcher fault")) { unobserved = true; e.SetObserved(); }
        };
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += handler;
        try
        {
            var weak = FaultAndForget(ref gate);
            await Task.Delay(40);
            for (int i = 0; i < 4; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
            Assert.False(weak.IsAlive, "Completed external Task should not be retained by the closed pump");
            Assert.False(unobserved, "The dispatcher must observe late faults in returned tasks");
        }
        finally { System.Threading.Tasks.TaskScheduler.UnobservedTaskException -= handler; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference FaultAndForget(ref TaskCompletionSource gate)
    {
        var weak = new WeakReference(gate.Task);
        gate.SetException(new InvalidOperationException("late dispatcher fault")); gate = null!;
        return weak;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealDispatcherPreservesThreadContextResultsAndNestedAsync(bool onOwner)
    {
        WinterRenderer renderer = null!; int owner = 0;
        using var services = new ServiceCollection().BuildServiceProvider();
        using var pump = new NativePump(w => { owner = Environment.CurrentManagedThreadId; renderer = new WinterRenderer(w, services, NullLoggerFactory.Instance); });
        var dispatcher = renderer.Dispatcher;
        Func<Task> work = async () =>
        {
            Assert.True(dispatcher.CheckAccess()); Assert.Equal(owner, Environment.CurrentManagedThreadId);
            Assert.NotNull(SynchronizationContext.Current);
            await Task.Delay(30);
            Assert.True(dispatcher.CheckAccess()); Assert.Equal(owner, Environment.CurrentManagedThreadId);
            int value = await dispatcher.InvokeAsync(async () => { await Task.Delay(20); Assert.Equal(owner, Environment.CurrentManagedThreadId); return 42; });
            Assert.Equal(42, value);
        };
        Task invocation = Task.CompletedTask;
        if (onOwner) await pump.Call(_ => invocation = dispatcher.InvokeAsync(work));
        else invocation = dispatcher.InvokeAsync(work);
        await invocation.WaitAsync(TimeSpan.FromSeconds(5));
        await pump.Call(_ => renderer.Dispose());
    }

    [Fact]
    public async Task ContextScopeRestoresCallerAndAllErrorsAreTaskFailures()
    {
        WinterRenderer renderer = null!;
        using var services = new ServiceCollection().BuildServiceProvider();
        using var pump = new NativePump(w => renderer = new WinterRenderer(w, services, NullLoggerFactory.Instance));
        await pump.Call(window =>
        {
            var previous = new SynchronizationContext(); var original = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(previous);
            try
            {
                Task task = null!;
                Assert.Null(Record.Exception(() => { task = renderer.Dispatcher.InvokeAsync((Action)(() => throw new InvalidOperationException("sync"))); }));
                Assert.True(task.IsFaulted); Assert.Same(previous, SynchronizationContext.Current);
                _ = task.Exception;
                Assert.Equal(7, renderer.Dispatcher.InvokeAsync(() => 7).GetAwaiter().GetResult());
                Assert.Same(previous, SynchronizationContext.Current);
            }
            finally { SynchronizationContext.SetSynchronizationContext(original); }
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => renderer.Dispatcher.InvokeAsync(async () => { await Task.Delay(15); throw new InvalidOperationException("async"); }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => renderer.Dispatcher.InvokeAsync<int>(async () => { await Task.Delay(15); throw new InvalidOperationException("generic"); }));
        Assert.Equal(9, await renderer.Dispatcher.InvokeAsync(() => 9));
        await renderer.Dispatcher.InvokeAsync(() => { });
        await pump.Call(_ => renderer.Dispose());
    }

    [Fact]
    public async Task ShutdownSettlesInFlightAndQueuedTasksAndRejectsFurtherWork()
    {
        WinterRenderer renderer = null!;
        using var services = new ServiceCollection().BuildServiceProvider();
        using var pump = new NativePump(w => renderer = new WinterRenderer(w, services, NullLoggerFactory.Instance));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool resumed = false;
        Task active = renderer.Dispatcher.InvokeAsync(async () => { entered.SetResult(); await gate.Task; resumed = true; });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await pump.Close(); await pump.Ended.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(active.IsCompleted, "Close must settle asynchronous dispatcher invocations");
        Assert.True(active.IsCanceled || active.IsFaulted);
        Task rejected = renderer.Dispatcher.InvokeAsync(() => { });
        Assert.True(rejected.IsCompleted); Assert.True(rejected.IsCanceled || rejected.IsFaulted);
        gate.SetResult(); await Task.Delay(30); Assert.False(resumed);
        _ = active.Exception; _ = rejected.Exception;
    }

    public sealed class AsyncLifecycle : ComponentBase
    {
        [Parameter] public Action<int>? Completed { get; set; }
        protected override async Task OnInitializedAsync() { await Task.Delay(25); Completed?.Invoke(Environment.CurrentManagedThreadId); }
        protected override void BuildRenderTree(RenderTreeBuilder builder) { builder.OpenComponent<Marius.Winter.Blazor.Elements.Label>(0); builder.AddAttribute(1, "Text", "Async ready"); builder.CloseComponent(); }
    }

    [Fact]
    public async Task ActualRendererRunsAsyncComponentLifecycleOnWindowThread()
    {
        WinterRenderer renderer = null!; int owner = 0;
        using var services = new ServiceCollection().BuildServiceProvider();
        using var pump = new NativePump(w => { owner = Environment.CurrentManagedThreadId; renderer = new WinterRenderer(w, services, NullLoggerFactory.Instance); });
        int completed = 0;
        await renderer.Dispatcher.InvokeAsync(() => renderer.AddComponent<AsyncLifecycle>(pump.Window, new Dictionary<string, object> { ["Completed"] = (Action<int>)(id => completed = id) })).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(owner, completed);
        await pump.Call(_ => renderer.Dispose());
    }
}

public class WindowRuntimeTests
{
    [Fact]
    public void ASecondConcurrentWindowFailsWithoutTerminatingTheFirstSession()
    {
        using var window = new Window(200, 120, "Single Window contract");
        Assert.Throws<InvalidOperationException>(() => { using var other = new Window(40, 40, "Second session"); });
    }

    [Fact]
    public async Task IdleWaitConsumesLittleCpuAndPostsWakePromptly()
    {
        using var pump = new NativePump(); await Task.Delay(150);
        using var process = Process.GetCurrentProcess(); var cpu = process.TotalProcessorTime; var wall = Stopwatch.StartNew();
        await Task.Delay(550);
        double cpuMilliseconds = (process.TotalProcessorTime - cpu).TotalMilliseconds;
        double wallMilliseconds = wall.Elapsed.TotalMilliseconds;
        double ratio = cpuMilliseconds / wallMilliseconds;
        var latencies = new List<double>();
        string sample = System.IO.Path.Combine(WorkerGraphicsLifetimeTests.Artifacts, "idle-and-wakeup.json");
        System.IO.File.WriteAllText(sample, System.Text.Json.JsonSerializer.Serialize(new { ratio, cpuMilliseconds, wallMilliseconds }));
        Console.WriteLine($"Idle CPU fraction: {ratio:F4}");
        Assert.True(ratio < 0.35, $"Idle CPU fraction was {ratio:F3}");
        for (int i = 0; i < 12; i++) { var watch = Stopwatch.StartNew(); await pump.Call(_ => { }); latencies.Add(watch.Elapsed.TotalMilliseconds); Assert.True(watch.ElapsedMilliseconds < 400); await Task.Delay(5); }
        System.IO.File.WriteAllText(sample, System.Text.Json.JsonSerializer.Serialize(new { ratio, cpuMilliseconds, wallMilliseconds, postMilliseconds = latencies }));
    }

    [Fact]
    public async Task FinalAnimationFrameIsPaintedAfterActiveSetEmpties()
    {
        Shape paint = null!;
        using var pump = new NativePump(w => { var box = new Box(); paint = box.Paint; paint.SetFill(255, 0, 0); w.AddChild(box); });
        await Task.Delay(40); uint initial = 0;
        await pump.Call(w => initial = Buffer(w)[10 * 200 + 10]);
        await pump.Call(w => w.Animator.Start(new Marius.Winter.Animation { Duration = 0.08f, Apply = t => paint.SetFill(t >= 1 ? (byte)0 : (byte)255, 0, t >= 1 ? (byte)255 : (byte)0) }));
        await Task.Delay(220);
        uint final = 0; await pump.Call(w => { Assert.False(w.Animator.HasActiveAnimations); final = Buffer(w)[10 * 200 + 10]; });
        Assert.NotEqual(initial, final);
    }

    [Fact]
    public void InitialLogicalSizeIsKnownBeforeRunAndSecondWindowIsRejected()
    {
        using var first = new Window(200, 120, "Initial logical size");
        Assert.Equal(200, first.Bounds.W); Assert.Equal(120, first.Bounds.H);
        Assert.Throws<InvalidOperationException>(() => { using var second = new Window(40, 40, "Unsupported second window"); });
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(2f)]
    [InlineData(1.25f)]
    public async Task FramebufferChangesKeepLogicalBoundsAndScaleBothScenes(float scale)
    {
        using var pump = new NativePump();
        await pump.Call(w =>
        {
            var callback = w.GlfwWindow!.Callbacks.Fbsize!;
            callback(w.GlfwWindow, (int)(200 * scale), (int)(120 * scale));
            Assert.Equal(200, w.Bounds.W); Assert.Equal(120, w.Bounds.H);
            var overlay = (Scene)typeof(Window).GetField("_overlayScene", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)!;
            Assert.Equal(scale, w.Scene.Transform().e11, 3); Assert.Equal(scale, overlay.Transform().e11, 3);
            Assert.Equal((int)(200 * scale) * (int)(120 * scale), Buffer(w).Length);
            callback(w.GlfwWindow, 0, 0); Assert.Equal(200, w.Bounds.W);
        });
    }

    [Fact]
    public async Task NativeLayoutFlushesBatchedChangesAndPreservesExplicitRootBounds()
    {
        var root = new Panel { Layout = new StackLayout() }; var first = new Box(); root.AddChild(first);
        using var pump = new NativePump(w => { w.AddChild(root); root.Measure(150, 80); root.Arrange(new RectF(12, 9, 150, 80)); });
        await Task.Delay(30); var added = new Box();
        await pump.Call(_ => { root.AddChild(added); root.AddChild(new Box()); root.Style.Padding = new Thickness(5); });
        await Task.Delay(60);
        await pump.Call(_ => { Assert.True(added.Bounds.W > 0); Assert.True(added.Bounds.H > 0); Assert.False(root.IsMeasureDirty); Assert.Equal(12, root.Bounds.X); Assert.Equal(150, root.Bounds.W); });
        int count = added.Measurements; await Task.Delay(60); await pump.Call(_ => Assert.Equal(count, added.Measurements));
    }

    private sealed class CustomLayout : ILayout
    {
        public System.Numerics.Vector2 Measure(Element container, float width, float height) { foreach (var child in container.Children) child.Measure(width, height); return new(80, 40); }
        public void Arrange(Element container, RectF bounds) { foreach (var child in container.Children) child.Arrange(new RectF(17, 13, 31, 19)); }
    }

    [Fact]
    public void AttachedPanelHonorsCustomLayoutIncludingInsideBuiltInLayout()
    {
        using var window = new Window(200, 120, "Custom layout contract");
        var root = new Panel { Layout = new StackLayout() }; var custom = new Panel { Layout = new CustomLayout() }; var child = new Box();
        custom.AddChild(child); root.AddChild(custom); window.AddChild(root); root.Measure(200, 120); root.Arrange(new RectF(0, 0, 200, 120));
        Assert.Equal(17, child.Bounds.X); Assert.Equal(31, child.Bounds.W);
    }

    internal static uint[] Buffer(Window w) => (uint[])typeof(Window).GetField("_buffer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)!;
}

public class HostLifetimeTests
{
    public sealed class OwnedService : IAsyncDisposable
    {
        public int DisposedOn;
        public async ValueTask DisposeAsync() { await Task.Delay(15); DisposedOn = Environment.CurrentManagedThreadId; }
    }

    public sealed class AsyncDisposableComponent : ComponentBase, IAsyncDisposable
    {
        [Inject] public OwnedService Service { get; set; } = null!;
        [Parameter] public Action<int, bool>? Disposed { get; set; }
        [Inject] public Window Window { get; set; } = null!;
        public async ValueTask DisposeAsync() { await Task.Delay(20); Disposed?.Invoke(Environment.CurrentManagedThreadId, !Window.IsDisposed && Window.GlfwWindow != null); }
        protected override void BuildRenderTree(RenderTreeBuilder b) { b.OpenComponent<Marius.Winter.Blazor.Elements.Label>(0); b.AddAttribute(1, "Text", "Owned component"); b.CloseComponent(); }
    }

    [Fact]
    public async Task WindowClosureDisposesAsyncComponentsThenServicesOnLiveUiRuntime()
    {
        WinterBlazorHost host = null!; OwnedService service = null!; int owner = 0, disposed = 0; bool runtimeAlive = false;
        using var pump = new NativePump(w => { owner = Environment.CurrentManagedThreadId; host = w.UseBlazor(s => s.AddSingleton<OwnedService>()); });
        var component = await host.AddComponent<AsyncDisposableComponent>(new Dictionary<string, object> { ["Disposed"] = (Action<int, bool>)((id, alive) => { disposed = id; runtimeAlive = alive; }) }).WaitAsync(TimeSpan.FromSeconds(5));
        service = component.Service;
        await pump.Close(); await pump.Ended.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(owner, disposed); Assert.True(runtimeAlive); Assert.Equal(owner, service.DisposedOn);
    }

    [Fact]
    public async Task HostSupportsExplicitAsyncDisposalAndUnsubscribesFromWindow()
    {
        WinterBlazorHost host = null!;
        using var pump = new NativePump(w => host = w.UseBlazor());
        var disposable = Assert.IsAssignableFrom<IAsyncDisposable>(host);
        await disposable.DisposeAsync();
        Assert.Empty(pump.Window.Children);
    }
}
