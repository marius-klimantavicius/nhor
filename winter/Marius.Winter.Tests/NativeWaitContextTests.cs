using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using Xunit.Abstractions;
using B = Marius.Winter.Blazor.Elements;
using Task = System.Threading.Tasks.Task;

namespace Marius.Winter.Tests;

public class NativeWaitContextTests(ITestOutputHelper output)
{
    public sealed class ClickApp : ComponentBase
    {
        [Parameter] public Func<Task> Click { get; set; } = null!;
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<B.Button>(0);
            builder.AddAttribute(1, "Text", "Click");
            builder.AddAttribute(2, "OnClick", EventCallback.Factory.Create(this, Click));
            builder.CloseComponent();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IdleX11ClickKeepsAsyncCallbackOnOwner(bool blazor) => RunClick(blazor, "idle");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TimedWaitX11ClickKeepsAsyncCallbackOnOwner(bool blazor) => RunClick(blazor, "timed");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PollEventsX11ClickKeepsAsyncCallbackOnOwner(bool blazor) => RunClick(blazor, "poll");

    private void RunClick(bool blazor, string delivery)
    {
        int owner = 0, before = 0, after = 0;
        SynchronizationContext? entry = null, continuation = null;
        string stack = "";
        bool clicked = false;
        WindowRegressionOwner.Run(() =>
        {
            owner = Environment.CurrentManagedThreadId;
            using var window = new Window(200, 100, $"Winter W2-01 {delivery} {blazor}");
            async Task Click()
            {
                clicked = true;
                before = Environment.CurrentManagedThreadId;
                entry = SynchronizationContext.Current;
                stack = new StackTrace().ToString();
                await Task.Delay(50);
                after = Environment.CurrentManagedThreadId;
                continuation = SynchronizationContext.Current;
                window.Close();
            }
            if (blazor)
            {
                var host = window.UseBlazor();
                host.AddComponent<ClickApp>(new Dictionary<string, object> { ["Click"] = (Func<Task>)Click }).GetAwaiter().GetResult();
            }
            else
            {
                var button = new Button("Click") { Clicked = async () => await Click() };
                window.AddChild(button);
                button.Arrange(new RectF(0, 0, 150, 70));
            }
            if (delivery == "timed")
                window.Animator.Start(new Animation { Duration = 10, Apply = _ => { } });

            if (delivery == "poll")
            {
                WindowRegressionOwner.Flush(window);
                Glfw.Glfw.glfwShowWindow(window.GlfwWindow!);
            }
            Task sender = X11ButtonInput.Click(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
            // Accumulate real native events before Run's scoped PollEvents.
            if (delivery == "poll") sender.WaitAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
            using var watchdog = new CancellationTokenSource();
            Task timeout = Task.Run(async () =>
            {
                try { await Task.Delay(5000, watchdog.Token); window.Close(); }
                catch (OperationCanceledException) { }
            });
            try { window.Run(); }
            finally { watchdog.Cancel(); timeout.GetAwaiter().GetResult(); }
            sender.WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
        });
        output.WriteLine($"delivery={delivery} blazor={blazor} clicked={clicked} owner={owner} before={before} after={after} context={entry?.GetType().FullName ?? "NULL"}");
        output.WriteLine(stack);
        Assert.True(clicked, "No native click delivered: prerequisite failure, not affinity evidence.");
        Assert.Contains("Glfw.Glfw.processEvent", stack);
        // Small GLFW wrappers may be inlined in Release. WaitForWork is the
        // stable boundary; the long animation above selects its finite branch.
        if (delivery == "poll") Assert.DoesNotContain("Window.WaitForWork()", stack);
        else Assert.Contains("Window.WaitForWork()", stack);
        Assert.Equal(owner, before);
        Assert.Equal(owner, after);
        Assert.NotNull(entry);
        Assert.Same(entry, continuation);
    }

    public sealed class CoalescedApp : ComponentBase
    {
        [Parameter] public Func<float, Task> Changed { get; set; } = null!;
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<B.Slider>(0);
            builder.AddAttribute(1, "OnValueChanged", EventCallback.Factory.Create(this, Changed));
            builder.CloseComponent();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueuedAndCoalescedEventsRetainContextAcrossAwait(bool coalesced)
    {
        WinterBlazorHost host = null!;
        int owner = 0, calls = 0;
        var done = new TaskCompletionSource<(int before, int after, float value, SynchronizationContext? entry, SynchronizationContext? continuation)>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Observe(float value)
        {
            calls++;
            int before = Environment.CurrentManagedThreadId;
            var entry = SynchronizationContext.Current;
            await Task.Delay(50);
            done.TrySetResult((before, Environment.CurrentManagedThreadId, value, entry, SynchronizationContext.Current));
        }
        using var pump = new NativePump(window => { owner = Environment.CurrentManagedThreadId; host = window.UseBlazor(); });
        if (coalesced)
            await host.AddComponent<CoalescedApp>(new Dictionary<string, object> { ["Changed"] = (Func<float, Task>)Observe });
        else
            await host.AddComponent<ClickApp>(new Dictionary<string, object> { ["Click"] = (Func<Task>)(() => Observe(1)) });
        await pump.Call(window =>
        {
            if (coalesced)
            {
                var slider = Assert.IsType<Slider>(Assert.Single(window.Children));
                slider.Value = 0.2f; slider.Value = 0.4f; slider.Value = 0.8f;
            }
            else Assert.IsType<Button>(Assert.Single(window.Children)).OnClick();
        });
        var result = await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(owner, result.before);
        Assert.Equal(owner, result.after);
        Assert.Equal(coalesced ? 0.8f : 1, result.value);
        Assert.NotNull(result.entry);
        Assert.Same(result.entry, result.continuation);
        await pump.Call(_ => Assert.Equal(1, calls));
    }

    [Fact]
    public void RunAndDedicatedCleanupRestoreTheCallersContext()
    {
        WindowRegressionOwner.Run(() =>
        {
            var caller = new SynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(caller);
            try
            {
                int owner = Environment.CurrentManagedThreadId, calls = 0;
                using var window = new Window(100, 80, "Winter cleanup context");
                SynchronizationContext? application = null, cleanup = null;
                Task ordinary = Task.CompletedTask;
                window.Closing += async () =>
                {
                    calls++;
                    cleanup = SynchronizationContext.Current;
                    Assert.NotNull(cleanup);
                    Assert.NotSame(application, cleanup);
                    await Task.Delay(25);
                    await Task.Yield();
                    Assert.Equal(owner, Environment.CurrentManagedThreadId);
                    Assert.Same(cleanup, SynchronizationContext.Current);
                    Assert.False(window.IsDisposed);
                    Assert.NotNull(window.GlfwWindow);
                };
                window.DispatcherQueue.Enqueue(() =>
                {
                    application = SynchronizationContext.Current;
                    ordinary = window.Dispatcher.InvokeAsync(async () => await Task.Yield());
                    window.Close();
                });
                window.Run();
                Assert.Equal(1, calls);
                Assert.True(ordinary.IsCanceled);
                Assert.Same(caller, SynchronizationContext.Current);
            }
            finally { SynchronizationContext.SetSynchronizationContext(null); }
        });
    }
}
