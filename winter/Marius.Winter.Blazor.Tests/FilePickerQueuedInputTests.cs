using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native rendering")]
public class FilePickerQueuedInputTests
{
    [Theory]
    [InlineData(RenderBackend.SW, true, false)] [InlineData(RenderBackend.GL, true, false)]
    [InlineData(RenderBackend.SW, false, false)] [InlineData(RenderBackend.GL, false, false)]
    [InlineData(RenderBackend.SW, true, true)] [InlineData(RenderBackend.GL, true, true)]
    public void ActualPickerInputAndParentRenderDuringUndeliveredResult(RenderBackend backend, bool accepted, bool largeDark) => PathPickerCallbackTests.OnOwner(() =>
    {
        var theme = largeDark ? Theme.Dark : Theme.Light;
        if (largeDark) foreach (var style in new[] { theme.Label, theme.Panel, theme.Button, theme.TextBox }) style.FontSize = 22;
        using var window = new Window(900, 720, "C07 completed result input", theme, backend);
        using var provider = new QueuedPickerProvider(); using var controller = new FilePickerController(provider);
        FilePickerQueuedProbe? probe = null;
        int ordinaryRenders = 0; long sessionAtRender = -1, sessionAfterRender = -1, revisionAtRender = -1, revisionAfterRender = -1;
        bool cancelObservedOnOwner = false, cancelObservedClosed = false, cancelObservedBeforeCallbacks = false;
        // Pointer Cancel completes synchronously on the owner. Observe its closed
        // state before the native view receives Completed; never call a handler.
        // Selection below exercises the additional background/Window-queue gap.
        if (!accepted) controller.Completed += result =>
        {
            cancelObservedOnOwner = window.Dispatcher.CheckAccess();
            revisionAtRender = controller.Revision; sessionAtRender = controller.State.SessionId;
            cancelObservedClosed = !controller.State.IsOpen; cancelObservedBeforeCallbacks = probe!.Model.Order.Count == 0;
            probe.Refresh(); ordinaryRenders++;
            revisionAfterRender = controller.Revision; sessionAfterRender = controller.State.SessionId;
        };
        var host = window.UseBlazor();
        probe = host.AddComponent<FilePickerQueuedProbe>(new() { [nameof(FilePickerQueuedProbe.Controller)] = controller, [nameof(FilePickerQueuedProbe.Provider)] = provider }).GetAwaiter().GetResult();
        var picker = PathPickerCallbackTests.Walk(window).OfType<FilePicker>().Single();
        using var finished = new ManualResetEventSlim();
        controller.Completed += _ => finished.Set();
        long session = controller.State.SessionId;
        string stem = $"queued-input-{backend}-{accepted}-{largeDark}";
        var input = Task.Run(async () =>
        {
            using var x = new C07X11(PathPickerCallbackTests.Xid(window));
            try
            {
                x.Focus(); await PathPickerCallbackTests.Capture(window, x, stem + "-first", 900, 720);
                if (accepted)
                {
                    // Actual DataTree keyboard selection and default Open action.
                    x.Key(0xff50); x.Key(0xff0d);
                    await PathPickerCallbackTests.Wait(window, () => controller.State.IsChecking, "Physical Enter starts asynchronous stat");
                    await window.Dispatcher.InvokeAsync(() =>
                    {
                        // Hold this owner turn while the genuine provider completes
                        // off-thread. Native Changed/Completed enqueue on this Window.
                        Task.Run(() => provider.Stat.SetResult(new(new("/image.svg"), "image.svg", false))).GetAwaiter().GetResult();
                        Assert.True(finished.Wait(TimeSpan.FromSeconds(3)));
                        Assert.True(window.DispatcherQueue.Count >= 2);
                        Assert.False(controller.State.IsOpen); Assert.Empty(probe.Model.Order);
                        long revision = controller.Revision; sessionAtRender = controller.State.SessionId;
                        probe.Refresh(); ordinaryRenders++;
                        Assert.Equal(revision, controller.Revision); Assert.Equal(sessionAtRender, controller.State.SessionId);
                        Assert.Equal("Unrelated label " + probe.Tick, PathPickerCallbackTests.Walk(window).OfType<Label>().Single(l => l.Text.StartsWith("Unrelated label")).Text);
                    });
                }
                else
                {
                    await PathPickerCallbackTests.Click(window, x, () => PathPickerCallbackTests.Walk(picker.Dialog.Footer!).OfType<Button>().Single(b => b.Text == "Cancel"));
                    await window.Dispatcher.InvokeAsync(() =>
                    {
                        Assert.True(cancelObservedOnOwner); Assert.True(cancelObservedClosed); Assert.True(cancelObservedBeforeCallbacks);
                        Assert.Equal(revisionAtRender, revisionAfterRender); Assert.Equal(sessionAtRender, sessionAfterRender);
                    });
                }
                await PathPickerCallbackTests.Wait(window, () => probe.Model.Results.Count == 1, "One typed result delivered to bound model");
                await window.Dispatcher.InvokeAsync(() =>
                {
                    Assert.Equal(1, ordinaryRenders); Assert.Equal(session, sessionAtRender);
                    Assert.Equal(session, controller.State.SessionId); Assert.False(controller.State.IsOpen);
                    Assert.False(picker.Dialog.IsOpen); Assert.False(probe.Model.Open);
                    Assert.Equal(new[] { "open:False", accepted ? "selected" : "canceled" }, probe.Model.Order);
                    Assert.Equal(new FilePickerResult(session, FilePickerMode.OpenFile, accepted ? new PickerPath("/image.svg") : null), Assert.Single(probe.Model.Results));
                });
                await PathPickerCallbackTests.Capture(window, x, stem + "-result", 900, 720);
                x.Resize(420, 340);
                await PathPickerCallbackTests.Wait(window, () => window.Bounds.W == 420 && window.Bounds.H == 340, "Native resize applied");
                await PathPickerCallbackTests.Capture(window, x, stem + "-narrow", 420, 340);
            }
            finally { x.CloseNormally(); }
        });
        window.Run(); input.GetAwaiter().GetResult(); Assert.True(window.IsDisposed);
        Assert.False(provider.Disposed); // view and host never own this provider
        Console.WriteLine($"{stem}: physical {(accepted ? "keyboard Open; background stat; queued owner gap" : "pointer Cancel; synchronous owner gap")}; label render={ordinaryRenders}; session={session}; result once; WM_DELETE normal.");
    });
}
