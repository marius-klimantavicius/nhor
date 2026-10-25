using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using Xunit.Abstractions;

namespace Marius.Winter.Tests;

public class StartupPresentationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, RenderBackend.SW)]
    [InlineData(false, RenderBackend.GL)]
    [InlineData(true, RenderBackend.SW)]
    [InlineData(true, RenderBackend.GL)]
    public void FirstMappedPixelsContainTheFirstDrawIncludingScenarioChooser(bool razor, RenderBackend backend) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(900, 720, "Startup presentation", Theme.Light, backend);
        using var profile = razor ? null : new Playground.Scenarios.ProfileAndVolume(window);
        using var host = razor ? null : new Playground.Scenarios.ScenarioHost(window);
        if (razor)
        {
            // Same eager thirteen-page App and fire-and-forget mount as Program.
            var mount = window.UseBlazor().AddComponent<Blazor.Playground.App>(new Dictionary<string, object> { ["OwnerWindow"] = window });
            Assert.True(mount.IsCompletedSuccessfully);
        }
        else
        {
            host!.AddScenario("Profile and volume", profile!.Content);
            window.AddChild(host.Content);
            window.SetAutoLayout(host.Content);
        }
        var selector = AssetCatalogInteractionTests.Walk(window).OfType<ComboBox>().First();
        Assert.Equal(0, selector.SelectedIndex);
        Assert.Equal("Profile and volume", selector.Items[0]);
        using var presentation = new StartupPresentation(window);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var watchdog = timeout.Token.Register(window.Close);
        void CloseAfterBothObservations()
        {
            if (presentation.MappedPixels != null && presentation.FirstDrawPixels != null) window.Close();
        }
        presentation.Mapped = CloseAfterBothObservations;
        presentation.Swapped = CloseAfterBothObservations;
        window.Run();
        Assert.Null(presentation.Failure);
        Assert.NotNull(presentation.FirstDrawPixels);
        Assert.NotNull(presentation.MappedPixels);
        var drawn = presentation.FirstDrawPixels!;
        var mapped = presentation.MappedPixels!;
        // Require actual ink in the first drawn chooser, then compare the map
        // against that exact frame. No settled/reference redraw or input occurs.
        int chooserInk = 0;
        for (int y = 18; y < 40; y++)
            for (int x = 25; x < 250; x++)
                if (drawn[y * 900 + x] < 0x707070) chooserInk++;
        Assert.True(chooserInk > 50, $"First draw needs scenario chooser glyphs, observed {chooserInk} pixels.");
        int different = drawn.Zip(mapped).Count(p => p.First != p.Second);
        output.WriteLine($"razor={razor} backend={backend} swapsAtMap={presentation.SwapsAtMap} differentPixels={different}/{drawn.Length} mapCorner={mapped[0]:X6} drawCorner={drawn[0]:X6}");
        Assert.True(different == 0, $"First mapped frame exposed {different}/{drawn.Length} pixels different from the first application draw (map corner {mapped[0]:X6}, draw {drawn[0]:X6}).");
    });

    [Theory]
    [InlineData("before-run", RenderBackend.SW)]
    [InlineData("queued-close", RenderBackend.SW)]
    [InlineData("queued-dispose", RenderBackend.SW)]
    [InlineData("before-run", RenderBackend.GL)]
    [InlineData("queued-close", RenderBackend.GL)]
    [InlineData("queued-dispose", RenderBackend.GL)]
    public void ClosingBeforeTheFirstFrameDoesNotExposeANativeWindow(string when, RenderBackend backend) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(200, 120, "Close before presentation", backend: backend);
        using var presentation = new StartupPresentation(window);
        if (when == "before-run") window.Close();
        else window.DispatcherQueue.Enqueue(when == "queued-close" ? window.Close : window.Dispose);
        window.Run();
        Assert.Null(presentation.Failure);
        Assert.True(window.IsDisposed);
        Assert.Null(presentation.MappedPixels);
        Assert.Equal(0, presentation.SwapCount);
    });

    [Theory]
    [InlineData(RenderBackend.SW)]
    [InlineData(RenderBackend.GL)]
    public void CleanEmptyWindowStillPresentsBeforeMapping(RenderBackend backend) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(200, 120, "Clean startup", backend: backend);
        window.Dirty = false;
        using var presentation = new StartupPresentation(window);
        presentation.Mapped = window.Close;
        window.Run();
        Assert.Null(presentation.Failure);
        Assert.NotNull(presentation.MappedPixels);
        Assert.NotNull(presentation.FirstDrawPixels);
        Assert.Equal(1, presentation.SwapsAtMap);
        Assert.Equal(presentation.FirstDrawPixels, presentation.MappedPixels);
    });

    public sealed class AsyncStartup : ComponentBase, IAsyncDisposable
    {
        [Parameter] public Task Gate { get; set; } = null!;
        [Parameter] public Action<int> Initialized { get; set; } = null!;
        [Parameter] public Action<int> Disposed { get; set; } = null!;
        private bool _ready;
        protected override async Task OnInitializedAsync()
        {
            await Gate;
            _ready = true;
            Initialized(Environment.CurrentManagedThreadId);
        }
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<Marius.Winter.Blazor.Elements.Label>(0);
            builder.AddAttribute(1, "Text", _ready ? "Ready" : "Loading");
            builder.CloseComponent();
        }
        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            Disposed(Environment.CurrentManagedThreadId);
        }
    }

    [Theory]
    [InlineData(RenderBackend.SW)]
    [InlineData(RenderBackend.GL)]
    public void FirstFrameDoesNotWaitForAsyncInitializationOrDropQueuedWork(RenderBackend backend) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(200, 120, "Async startup", backend: backend);
        int owner = Environment.CurrentManagedThreadId, initialized = 0, disposed = 0, queuedAtFirstDraw = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var mount = window.UseBlazor().AddComponent<AsyncStartup>(new Dictionary<string, object>
        {
            ["Gate"] = gate.Task,
            ["Initialized"] = (Action<int>)(id => initialized = id),
            ["Disposed"] = (Action<int>)(id => disposed = id)
        });
        Assert.False(mount.IsCompleted);
        var order = new List<int>();
        // More than one bounded dispatcher batch; Run must keep pumping after
        // its first presentation, retaining FIFO work and its UI context.
        for (int i = 0; i < 129; i++)
        {
            int item = i;
            window.DispatcherQueue.Enqueue(() =>
            {
                Assert.Equal(owner, Environment.CurrentManagedThreadId);
                Assert.NotNull(SynchronizationContext.Current);
                order.Add(item);
            });
        }
        using var presentation = new StartupPresentation(window);
        bool pendingAtMap = false;
        presentation.Mapped = () => { pendingAtMap = !mount.IsCompleted; gate.SetResult(); };
        presentation.Swapped = () =>
        {
            if (presentation.SwapCount == 1) queuedAtFirstDraw = order.Count;
            if (mount.IsCompletedSuccessfully && order.Count == 129) window.Close();
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var watchdog = timeout.Token.Register(window.Close);
        window.Run();
        Assert.False(timeout.IsCancellationRequested, "Async startup failed to finish; watchdog is not behavioral evidence.");
        Assert.Null(presentation.Failure);
        Assert.True(pendingAtMap, "A legitimate pending component must be allowed to show its loading state.");
        Assert.InRange(queuedAtFirstDraw, 1, 64);
        Assert.Equal(Enumerable.Range(0, 129), order);
        Assert.Equal(owner, initialized);
        Assert.Equal(owner, disposed);
        Assert.True(mount.IsCompletedSuccessfully);
    });
}
