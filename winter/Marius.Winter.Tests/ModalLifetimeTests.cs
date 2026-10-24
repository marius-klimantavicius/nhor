using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Marius.Winter.Tests;

public class ModalLifetimeTests
{
    [Fact]
    public async Task RejectionResetReopenAndRemovalInvalidateAwaitingCloseIdentity()
    {
        ModalDialog modal = null!; Task<bool> pending = null!;
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        int requests = 0, rejections = 0, results = 0;
        using var pump = new NativePump(w =>
        {
            modal = new ModalDialog { IsOpen = true };
            modal.CloseRequested += _ => requests++;
            modal.CloseRejected += _ => rejections++;
            modal.Closed += _ => results++;
            modal.CloseGuard = (_, ct) => { token = ct; return new ValueTask<bool>(gate.Task); };
            w.AddChild(modal);
        });
        long session = 0;
        await pump.Call(w =>
        {
            session = modal.SessionId; pending = modal.RequestCloseAsync();
            Assert.True(modal.IsClosing);
            Assert.False(modal.RequestCloseAsync().GetAwaiter().GetResult());
            Assert.Equal(1, requests);
            modal.ResetSession(true);
            Assert.True(token.IsCancellationRequested);
            Assert.True(modal.SessionId > session);
        });
        gate.SetResult(true); Assert.False(await pending.WaitAsync(TimeSpan.FromSeconds(3)));
        await pump.Call(w =>
        {
            Assert.True(modal.IsOpen); Assert.Equal(0, results);
            modal.CloseGuard = (_, _) => ValueTask.FromResult(false);
            Assert.False(modal.RequestCloseAsync(DialogCloseReason.Escape).GetAwaiter().GetResult());
            Assert.Equal(1, rejections); Assert.True(modal.IsOpen);
        });
        gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await pump.Call(w =>
        {
            modal.CloseGuard = (_, ct) => { token = ct; return new ValueTask<bool>(gate.Task); };
            pending = modal.RequestCloseAsync();
            w.RemoveChild(modal); modal.Dispose();
            Assert.True(token.IsCancellationRequested);
        });
        gate.SetResult(true); Assert.False(await pending.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(0, results);
    }

    [Fact]
    public async Task PendingCooperativeCloseIsCancelledOnNormalOwnerShutdown()
    {
        ModalDialog modal = null!; Task<bool> pending = null!; CancellationToken token = default;
        int results = 0;
        using var pump = new NativePump(w =>
        {
            modal = new ModalDialog { IsOpen = true };
            modal.CloseGuard = async (_, ct) => { token = ct; await Task.Delay(10000, ct); return true; };
            modal.Closed += _ => results++;
            w.AddChild(modal);
        });
        await pump.Call(w => pending = modal.RequestCloseAsync());
        await pump.Close(); await pump.Ended;
        Assert.True(token.IsCancellationRequested); Assert.True(modal.IsDisposed); Assert.Equal(0, results);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
    }

    [Fact]
    public void BorrowedContentsAndPreSessionDisposalRemainCallerOwned() => WindowRegressionOwner.Run(() =>
    {
        using var w = new Window(300, 200, "C06 ownership", backend: RenderBackend.SW);
        using var body = new TextBox("Žąsis e\u0301 👨‍👩‍👧‍👦"); using var footer = new Button("Cancel");
        using var modal = new ModalDialog { Body = body, Footer = footer, IsOpen = true };
        w.AddChild(modal); modal.IsOpen = false;
        Assert.False(body.IsDisposed); Assert.False(footer.IsDisposed);
        modal.Dispose(); Assert.Null(body.Parent); Assert.Null(footer.Parent);
        Assert.False(body.IsDisposed); Assert.False(footer.IsDisposed);
        using var unattached = new ModalDialog { Body = body };
        unattached.Dispose(); Assert.False(body.IsDisposed); Assert.Null(body.Parent);
    });

    [Fact]
    public void OneModalInitialEligibilityAndRemovedOpenerFallback() => WindowRegressionOwner.Run(() =>
    {
        using var w = new Window(300, 200, "C06 focus fallback", backend: RenderBackend.SW);
        var opener = new Button("Open"); var fallback = new Button("Background");
        using var body = new TextBox(); using var detached = new TextBox();
        using var modal = new ModalDialog { Body = body, InitialFocus = () => detached };
        using var other = new ModalDialog();
        w.AddChild(opener); w.AddChild(fallback); w.AddChild(modal); w.AddChild(other);
        w.SetFocus(opener); modal.IsOpen = true;
        Assert.Same(body, w.FocusedElement);
        w.SetFocus(fallback); Assert.Same(body, w.FocusedElement);
        Assert.Throws<InvalidOperationException>(() => other.IsOpen = true); Assert.False(other.IsOpen);
        w.RemoveChild(opener); opener.Dispose();
        modal.IsOpen = false; Assert.Same(fallback, w.FocusedElement);
    });
}
