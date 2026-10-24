using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native rendering")]
public class ModalBindingTests
{
    [Fact]
    public void CompiledBindingSlotsResultParentRejectionAndProgrammaticReset() => OnOwner(() =>
    {
        using var window = new Window(480, 320, "C06 compiled Razor", backend: RenderBackend.SW);
        using var renderer = new TestRenderer();
        var root = new Panel(); window.AddChild(root);
        var probe = renderer.AddComponent<ModalBindingProbe>(TestRenderer.CreateRootHandler(renderer, root)).GetAwaiter().GetResult();
        var modal = Assert.IsType<ModalDialog>(Assert.Single(root.Children));
        Assert.True(modal.IsOpen); Assert.Equal(0, probe.Changes);
        var body = modal.Body; var footer = modal.Footer; var session = modal.SessionId;
        renderer.Dispatcher.InvokeAsync(probe.Slots).GetAwaiter().GetResult();
        Assert.Same(body, modal.Body); Assert.Same(footer, modal.Footer);
        Assert.Equal("Changed profile", modal.Title);
        Assert.Equal("Accept profile", Assert.IsType<Button>(modal.Footer!.Children.Single()).Text);
        renderer.Dispatcher.InvokeAsync(() =>
        {
            var input = Assert.IsType<TextBox>(modal.Body!.Children.Single());
            input.OnKeyDown(269, 0, false);
            input.OnTextInput(" Žąsis");
            // Real C01 field commit synchronously flushes the queued scalar.
            input.CommitEdit(TextCommitReason.Enter);
        }).GetAwaiter().GetResult();
        Assert.Equal("Marius Žąsis", probe.Name);
        Assert.True(modal.RequestCloseAsync(DialogCloseReason.Accepted).GetAwaiter().GetResult());
        Assert.False(probe.Open); Assert.False(modal.IsOpen); Assert.Equal(probe.Name, probe.AcceptedName);
        Assert.Equal(1, probe.Changes); Assert.Equal(0, probe.LegacyCalls); Assert.Single(probe.Results);
        renderer.AttributeUpdates.Clear();
        renderer.Dispatcher.InvokeAsync(() => probe.Reset(true)).GetAwaiter().GetResult();
        Assert.True(modal.IsOpen); Assert.True(modal.SessionId > session); Assert.Equal(1, probe.Changes);
        Assert.Contains(renderer.AttributeUpdates, a => a.Name == "Configuration");
        probe.RejectClose = true;
        Assert.True(modal.RequestCloseAsync(DialogCloseReason.Cancel).GetAwaiter().GetResult());
        Assert.True(probe.Open); Assert.True(modal.IsOpen); Assert.Equal(2, probe.Changes); Assert.Equal(2, probe.Results.Count);
        renderer.Dispatcher.InvokeAsync(() => probe.Reset(false)).GetAwaiter().GetResult();
        Assert.False(modal.IsOpen); Assert.Equal(2, probe.Changes); Assert.Equal(2, probe.Results.Count);
        renderer.Dispatcher.InvokeAsync(probe.Remove).GetAwaiter().GetResult();
        Assert.True(modal.IsDisposed); Assert.True(body!.IsDisposed); Assert.True(footer!.IsDisposed); Assert.Empty(root.Children);
    });

    [Fact]
    public void PendingValidationCannotCloseResetReopenedOrUnmountedRazorIdentity() => OnOwner(() =>
    {
        using var window = new Window(400, 300, "C06 stale Razor", backend: RenderBackend.SW);
        using var renderer = new TestRenderer(); var root = new Panel(); window.AddChild(root);
        var probe = renderer.AddComponent<ModalBindingProbe>(TestRenderer.CreateRootHandler(renderer, root)).GetAwaiter().GetResult();
        var modal = (ModalDialog)root.Children[0];
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        probe.Policy = (_, ct) => { token = ct; return new ValueTask<bool>(gate.Task); };
        var pending = modal.RequestCloseAsync();
        renderer.Dispatcher.InvokeAsync(() => probe.Reset(true)).GetAwaiter().GetResult();
        Assert.True(token.IsCancellationRequested);
        gate.SetResult(true); Drain(window, pending);
        Assert.False(pending.GetAwaiter().GetResult()); Assert.True(modal.IsOpen); Assert.Empty(probe.Results);
        gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        pending = modal.RequestCloseAsync();
        renderer.Dispatcher.InvokeAsync(probe.Remove).GetAwaiter().GetResult();
        Assert.True(token.IsCancellationRequested); gate.SetResult(true); Drain(window, pending);
        Assert.False(pending.GetAwaiter().GetResult()); Assert.Empty(probe.Results); Assert.Equal(0, probe.Changes);
    });

    [Fact]
    public void AwaitedChangedCallbackPreservesResultSnapshotAndNewParentSession() => OnOwner(() =>
    {
        using var window = new Window(400, 300, "C06 awaited binding", backend: RenderBackend.SW);
        using var renderer = new TestRenderer(); var root = new Panel(); window.AddChild(root);
        var probe = renderer.AddComponent<ModalBindingProbe>(TestRenderer.CreateRootHandler(renderer, root)).GetAwaiter().GetResult();
        var modal = (ModalDialog)root.Children[0]; var session = modal.SessionId;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var after = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        probe.AfterChanged = async () => { await gate.Task; after.SetResult(); };
        Assert.True(modal.RequestCloseAsync().GetAwaiter().GetResult());
        Assert.Equal(session, Assert.Single(probe.Results).SessionId);
        renderer.Dispatcher.InvokeAsync(() => probe.Reset(true)).GetAwaiter().GetResult();
        Assert.True(modal.SessionId > session);
        gate.SetResult(); Drain(window, after.Task);
        Assert.True(modal.IsOpen); Assert.True(probe.Open); Assert.Single(probe.Results);
        renderer.Dispatcher.InvokeAsync(probe.Remove).GetAwaiter().GetResult();
    });

    private static void Drain(Window window, Task task)
    {
        var until = DateTime.UtcNow.AddSeconds(3);
        while (!task.IsCompleted && DateTime.UtcNow < until)
        { if (window.DispatcherQueue.TryDequeue(out var action)) action(); else Thread.Yield(); }
        Assert.True(task.IsCompleted, "Owned dispatcher did not finish the guarded request.");
    }
    private static void OnOwner(Action action)
    {
        Exception? error = null;
        var owner = new Thread(() => { try { action(); } catch (Exception e) { error = e; } }) { IsBackground = true };
        owner.Start(); Assert.True(owner.Join(TimeSpan.FromSeconds(12)), "C06 owner timed out.");
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
