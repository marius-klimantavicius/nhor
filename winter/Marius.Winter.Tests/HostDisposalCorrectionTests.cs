using System;
using System.Threading;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Marius.Winter.Tests;

public class HostDisposalCorrectionTests
{
    [Fact]
    public async Task OffThreadCooperativeDisposalCompletesThroughWindowClose()
    {
        WinterBlazorHost host = null!;
        int owner = 0;
        using var pump = new NativePump(w =>
        {
            owner = Environment.CurrentManagedThreadId;
            host = w.UseBlazor(s => s.AddSingleton<IntegrationReviewRegressionTests.CooperativeDisposalService>());
        });
        var component = await host.AddComponent<IntegrationReviewRegressionTests.ServiceConsumer>();
        var disposal = host.DisposeAsync().AsTask();
        await component.Service.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        pump.Window.Close();
        await pump.Ended.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(disposal.IsCompletedSuccessfully, "Off-thread callers must observe completed cleanup rather than shutdown cancellation.");
        Assert.Equal(owner, component.Service.DisposedOn);
        Assert.True(component.Service.NativeRuntimeAlive);
        await host.DisposeAsync();
    }

    public sealed class QueuedService(Window window) : IAsyncDisposable
    {
        public int Calls, DisposedOn;
        public bool NativeRuntimeAlive;
        public async ValueTask DisposeAsync()
        {
            Calls++;
            await window.Dispatcher.InvokeAsync(async () =>
            {
                // Queue a cleanup continuation BEFORE BeginShutdown, then use
                // the dispatcher again while shutdown is pumping that work.
                await Task.Yield();
                await window.Dispatcher.InvokeAsync(async () => await Task.Yield());
                DisposedOn = Environment.CurrentManagedThreadId;
                NativeRuntimeAlive = !window.IsDisposed && window.GlfwWindow != null;
            });
        }
    }

    public sealed class QueuedConsumer : ComponentBase
    {
        [Inject] public QueuedService Service { get; set; } = null!;
    }

    [Fact]
    public async Task ShutdownPreservesQueuedDisposalWorkAndCancelsOrdinaryWork()
    {
        WinterBlazorHost host = null!;
        int owner = 0;
        using var pump = new NativePump(w =>
        {
            owner = Environment.CurrentManagedThreadId;
            host = w.UseBlazor(s => s.AddSingleton<QueuedService>());
        });
        var component = await host.AddComponent<QueuedConsumer>();
        Task disposal = Task.CompletedTask, ordinary = Task.CompletedTask;
        bool ordinaryResumed = false, legacyRan = false;
        await pump.Call(w =>
        {
            ordinary = w.Dispatcher.InvokeAsync(async () => { await Task.Yield(); ordinaryResumed = true; });
            w.DispatcherQueue.Enqueue(() => legacyRan = true);
            var previous = SynchronizationContext.Current;
            disposal = host.DisposeAsync().AsTask();
            Assert.Same(previous, SynchronizationContext.Current);
            w.Close();
        });
        await pump.Ended.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(disposal.IsCompletedSuccessfully, "Already queued cleanup and its dispatcher task must survive BeginShutdown.");
        Assert.True(ordinary.IsCanceled);
        Assert.False(ordinaryResumed);
        Assert.False(legacyRan);
        Assert.Equal(1, component.Service.Calls);
        Assert.Equal(owner, component.Service.DisposedOn);
        Assert.True(component.Service.NativeRuntimeAlive);
    }

    public sealed class CompletingService(Window window, bool fail, bool synchronous) : IAsyncDisposable
    {
        public int Calls, DisposedOn;
        public bool NativeRuntimeAlive;
        public ValueTask DisposeAsync()
        {
            Calls++;
            if (synchronous && fail) throw new InvalidOperationException("synchronous service cleanup");
            return new ValueTask(CompleteAsync());
        }
        private async Task CompleteAsync()
        {
            await Task.Delay(15);
            DisposedOn = Environment.CurrentManagedThreadId;
            NativeRuntimeAlive = !window.IsDisposed && window.GlfwWindow != null;
            if (fail) throw new InvalidOperationException("asynchronous service cleanup");
        }
    }

    public sealed class CompletingConsumer : ComponentBase
    {
        [Inject] public CompletingService Service { get; set; } = null!;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ExplicitDisposalRestoresContextAndUnsubscribesAfterSuccessOrFailure(bool fail, bool synchronous)
    {
        WinterBlazorHost host = null!;
        int owner = 0;
        using var pump = new NativePump(w =>
        {
            owner = Environment.CurrentManagedThreadId;
            host = w.UseBlazor(s => s.AddSingleton(_ => new CompletingService(w, fail, synchronous)));
        });
        var component = await host.AddComponent<CompletingConsumer>();
        Task disposal = Task.CompletedTask;
        await pump.Call(_ =>
        {
            var previous = SynchronizationContext.Current;
            disposal = host.DisposeAsync().AsTask();
            Assert.Same(previous, SynchronizationContext.Current);
        });
        if (fail)
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => disposal.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(synchronous ? "synchronous service cleanup" : "asynchronous service cleanup", error.Message);
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.DisposeAsync().AsTask());
        }
        else
        {
            await disposal.WaitAsync(TimeSpan.FromSeconds(5));
            await host.DisposeAsync();
        }
        if (!synchronous)
        {
            Assert.Equal(owner, component.Service.DisposedOn);
            Assert.True(component.Service.NativeRuntimeAlive);
        }
        await pump.Close();
        await pump.Ended.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, component.Service.Calls);
    }
}
