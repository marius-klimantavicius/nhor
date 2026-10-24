using System;
using System.Threading;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Marius.Winter.Tests;

public class IntegrationReviewRegressionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposingEditedMultilineControlBalancesTemporaryFontLeases(bool rich)
    {
        using var pump = new NativePump();
        await pump.Call(window =>
        {
            var font = Assert.IsAssignableFrom<ThorVG.FontLoader>(ThorVG.LoaderMgr.Font("default"));
            try
            {
                int before = font.sharing;
                using Element editor = rich ? new RichTextBox("Winter") : new TextArea("Winter");
                window.AddChild(editor);
                editor.Arrange(new RectF(0, 0, 180, 90));
                for (int i = 0; i < 6; i++) editor.OnTextInput("e\u0301");
                editor.Dispose();

                Assert.Equal(before, font.sharing);
            }
            finally { ThorVG.LoaderMgr.Retrieve(font); }
        });
    }

    public sealed class CooperativeDisposalService(Window window) : IAsyncDisposable
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int DisposedOn { get; private set; }
        public bool NativeRuntimeAlive { get; private set; }

        public async ValueTask DisposeAsync()
        {
            Started.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, window.ShutdownToken); }
            catch (OperationCanceledException) when (window.ShutdownToken.IsCancellationRequested) { }
            DisposedOn = Environment.CurrentManagedThreadId;
            NativeRuntimeAlive = !window.IsDisposed && window.GlfwWindow != null;
        }
    }

    public sealed class ServiceConsumer : ComponentBase
    {
        [Inject] public CooperativeDisposalService Service { get; set; } = null!;
    }

    [Fact]
    public async Task WindowCloseFinishesAlreadyStartedCooperativeHostDisposalOnLiveUiRuntime()
    {
        WinterBlazorHost host = null!;
        int owner = 0;
        using var pump = new NativePump(window =>
        {
            owner = Environment.CurrentManagedThreadId;
            host = window.UseBlazor(services => services.AddSingleton<CooperativeDisposalService>());
        });
        var component = await host.AddComponent<ServiceConsumer>().WaitAsync(TimeSpan.FromSeconds(5));
        Task disposal = Task.CompletedTask;
        await pump.Call(_ => disposal = host.DisposeAsync().AsTask());
        await component.Service.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        pump.Window.Close();
        await pump.Ended.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.WhenAny(disposal, Task.Delay(1000));

        Assert.True(disposal.IsCompleted, "Window shutdown must finish an already-started host disposal, including its cooperative service continuation.");
        await disposal;
        Assert.Equal(owner, component.Service.DisposedOn);
        Assert.True(component.Service.NativeRuntimeAlive, "Service disposal must finish before native teardown.");
    }
}
