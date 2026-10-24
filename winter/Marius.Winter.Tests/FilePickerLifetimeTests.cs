using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Marius.Winter.Tests;

public class FilePickerLifetimeTests
{
    [Fact]
    public void ResetSuppressesAlreadyQueuedResultAndBorrowedControllerSurvivesViewDisposal() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(640, 480, "C07 queued result", backend: RenderBackend.SW);
        var provider = new FilePickerControllerTests.DelayedProvider(); using var controller = new FilePickerController(provider);
        var picker = new FilePicker(controller); window.AddChild(picker);
        var results = new List<FilePickerResult>(); picker.Completed += results.Add;
        var open = controller.OpenAsync(FilePickerMode.OpenFile, "/first");
        provider.Requests[0].Completion.SetResult([new(new("/first/file.svg"), "file.svg", false)]); Settle(open);
        controller.SetFileName("file.svg");
        // A provider completion can queue a result while the parent resets on the
        // owning thread before that UI delivery runs.
        Task.Run(() => controller.AcceptAsync()).GetAwaiter().GetResult(); controller.Reset(); Drain(); Assert.Empty(results);
        var pending = controller.OpenAsync(FilePickerMode.OpenFile, "/second");
        window.RemoveChild(picker); Assert.True(provider.Requests[1].Token.IsCancellationRequested);
        provider.Requests[1].Completion.SetResult([]); Settle(pending); Assert.Empty(results); Assert.False(controller.State.IsOpen);
        picker.Dispose(); Assert.True(picker.IsDisposed); Assert.True(picker.Dialog.IsDisposed);
        var reusable = controller.OpenAsync(FilePickerMode.OpenFile, "/third");
        provider.Requests[2].Completion.SetResult([]); Settle(reusable); Assert.Equal(FilePickerLoadState.Ready, controller.State.LoadState);
        controller.Reset();
        void Drain() { while (window.DispatcherQueue.TryDequeue(out var action)) action(); }
        void Settle(Task task) { var until = DateTime.UtcNow.AddSeconds(2); while (!task.IsCompleted && DateTime.UtcNow < until) { Drain(); Thread.Yield(); } Assert.True(task.IsCompleted); task.GetAwaiter().GetResult(); Drain(); }
    });

    [Fact]
    public void ExternalValueChangeInvalidatesPendingBrowseWithoutEcho() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(640, 480, "C07 parent path reset", backend: RenderBackend.SW);
        var provider = new FilePickerControllerTests.DelayedProvider(); using var path = new PathPicker(provider) { Value = new PickerPath("/first") };
        window.AddChild(path); int changes = 0, results = 0; path.ValueChanged += _ => changes++; path.Picked += _ => results++;
        var pending = path.BrowseAsync(); path.Value = new("/parent-reset"); Assert.True(provider.Requests[0].Token.IsCancellationRequested);
        provider.Requests[0].Completion.SetResult([]);
        var until = DateTime.UtcNow.AddSeconds(2);
        while (!pending.IsCompleted && DateTime.UtcNow < until) { while (window.DispatcherQueue.TryDequeue(out var action)) action(); Thread.Yield(); }
        pending.GetAwaiter().GetResult(); while (window.DispatcherQueue.TryDequeue(out var action)) action();
        Assert.False(path.Picker.Dialog.IsOpen); Assert.Equal(new PickerPath("/parent-reset"), path.Value); Assert.Equal(0, changes); Assert.Equal(0, results);
    });
}
