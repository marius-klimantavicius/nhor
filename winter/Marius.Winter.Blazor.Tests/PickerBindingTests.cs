using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Marius.Winter.Blazor.Tests;

public sealed class PickerBindingProvider : IFilePickerProvider, IDisposable
{
    public bool Disposed;
    public bool Delay;
    public CancellationToken PendingToken;
    public TaskCompletionSource<IReadOnlyList<FilePickerEntry>> Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public PickerPath Normalize(string path) => new(path);
    public PickerPath? Parent(PickerPath path) => path.Value == "/" ? null : new("/");
    public PickerPath Child(PickerPath directory, string name) => new(directory.Value.TrimEnd('/') + "/" + name);
    public Task<IReadOnlyList<FilePickerEntry>> ListAsync(PickerPath path, CancellationToken token)
    {
        PendingToken = token;
        return Delay ? Pending.Task : Task.FromResult<IReadOnlyList<FilePickerEntry>>([new(new("/image.svg"), "image.svg", false), new(new("/folder"), "folder", true)]);
    }
    public Task<FilePickerEntry?> GetEntryAsync(PickerPath path, CancellationToken token) => Task.FromResult<FilePickerEntry?>(new(path, path.Value, path.Value == "/folder"));
    public void Dispose() => Disposed = true;
}

[Collection("Native rendering")]
public class PickerBindingTests
{
    [Fact]
    public void AcceptedBrowseFlushesEarlierScalarBeforeTheDiscreteResult() => OnOwner(() =>
    {
        using var window = new Window(640, 480, "C07 path commit", backend: RenderBackend.SW);
        using var renderer = new PickerRenderer(window); var root = new Panel(); window.AddChild(root);
        var probe = renderer.AddComponent<PickerBindingProbe>(TestRenderer.CreateRootHandler(renderer, root)).GetAwaiter().GetResult();
        renderer.Dispatcher.InvokeAsync(probe.Reset).GetAwaiter().GetResult();
        var path = root.Children.OfType<PathPicker>().Single();
        renderer.Dispatcher.InvokeAsync(() =>
        {
            path.TextEditor.OnKeyDown(269, 0, false); path.TextEditor.OnTextInput("draft");
            path.BrowseAsync().GetAwaiter().GetResult();
            path.Picker.Controller!.Select(new("/folder")); path.Picker.Controller.AcceptAsync().GetAwaiter().GetResult();
            Drain(window);
        }).GetAwaiter().GetResult();
        Assert.Equal(new PickerPath("/folder"), probe.Path); Assert.Equal("/folder", path.TextEditor.Text);
        Assert.Equal(2, probe.PathChanges); Assert.Equal(new PickerPath("/folder"), Assert.Single(probe.Results).Path);
        renderer.Dispatcher.InvokeAsync(probe.Remove).GetAwaiter().GetResult();
    });
    [Fact]
    public void OrdinaryParentRenderBetweenQueuedKeystrokesPreservesTheEntireEdit() => OnOwner(() =>
    {
        using var window = new Window(640, 480, "C07 queued path", backend: RenderBackend.SW);
        using var renderer = new PickerRenderer(window); var root = new Panel(); window.AddChild(root);
        var probe = renderer.AddComponent<PickerBindingProbe>(TestRenderer.CreateRootHandler(renderer, root)).GetAwaiter().GetResult();
        renderer.Dispatcher.InvokeAsync(probe.Reset).GetAwaiter().GetResult();
        var path = root.Children.OfType<PathPicker>().Single();
        renderer.Dispatcher.InvokeAsync(() =>
        {
            path.TextEditor.OnKeyDown(269, 0, false); path.TextEditor.OnTextInput("abc");
            probe.Refresh();
            path.TextEditor.OnTextInput("d"); Drain(window);
        }).GetAwaiter().GetResult();
        Assert.Equal("/resetabcd", probe.Path?.Value); Assert.Equal("/resetabcd", path.TextEditor.Text);
        Assert.Equal(1, probe.PathChanges); Assert.Equal(0, probe.LegacyPaths);
        renderer.Dispatcher.InvokeAsync(probe.Remove).GetAwaiter().GetResult();
    });
    [Fact]
    public void ActualCompiledBindingsResetRejectAndDiscretePickerResults() => OnOwner(() =>
    {
        using var window = new Window(800, 620, "C07 binding", backend: RenderBackend.SW);
        using var renderer = new PickerRenderer(window); var root = new Panel(); window.AddChild(root);
        var probe = renderer.AddComponent<PickerBindingProbe>(TestRenderer.CreateRootHandler(renderer, root)).GetAwaiter().GetResult();
        var path = root.Children.OfType<PathPicker>().Single(); var picker = root.Children.OfType<FilePicker>().Single();
        Assert.True(picker.Dialog.IsOpen); Assert.Equal(0, probe.OpenChanges); Assert.Equal(0, probe.PathChanges);
        renderer.Dispatcher.InvokeAsync(() =>
        {
            path.TextEditor.OnKeyDown(269, 0, false); path.TextEditor.OnTextInput(" 👩🏽‍💻é"); Drain(window);
        }).GetAwaiter().GetResult();
        Assert.Equal("/original 👩🏽‍💻é", probe.Path?.Value); Assert.Equal(1, probe.PathChanges); Assert.Equal(0, probe.LegacyPaths);
        probe.Reject = true;
        renderer.Dispatcher.InvokeAsync(() => { path.TextEditor.OnTextInput(" rejected"); Drain(window); }).GetAwaiter().GetResult();
        Assert.Equal(probe.Path, path.Value); Assert.DoesNotContain("rejected", path.TextEditor.Text);
        // A same-value parent reset invalidates queued input before delivery.
        renderer.AttributeUpdates.Clear(); var before = probe.PathChanges;
        renderer.Dispatcher.InvokeAsync(() => { path.TextEditor.OnTextInput(" stale"); probe.Reset(); Drain(window); }).GetAwaiter().GetResult();
        Assert.Equal(before, probe.PathChanges); Assert.Equal("/reset", path.TextEditor.Text); Assert.False(picker.Dialog.IsOpen);
        Assert.Contains(renderer.AttributeUpdates, a => a.Name == "Configuration");
        probe.Reject = false;
        renderer.Dispatcher.InvokeAsync(probe.Replace).GetAwaiter().GetResult();
        picker.Controller!.Select(new("/image.svg")); picker.Controller.AcceptAsync().GetAwaiter().GetResult();
        Assert.False(probe.Open); Assert.Equal(1, probe.OpenChanges); Assert.Equal(0, probe.LegacyOpens);
        Assert.Equal(new PickerPath("/image.svg"), Assert.Single(probe.Results).Path);
        probe.Reject = true; renderer.Dispatcher.InvokeAsync(probe.Replace).GetAwaiter().GetResult();
        var session = picker.Controller.State.SessionId; picker.Controller.Cancel();
        Assert.True(probe.Open); Assert.True(picker.Dialog.IsOpen); Assert.True(picker.Controller.State.SessionId > session);
        Assert.Equal(2, probe.Results.Count);
        var provider = probe.Provider;
        renderer.Dispatcher.InvokeAsync(probe.Remove).GetAwaiter().GetResult();
        Assert.True(path.IsDisposed); Assert.True(picker.IsDisposed); Assert.False(provider.Disposed); Assert.Empty(root.Children);
    });

    [Fact]
    public void ProviderReplacementAndUnmountCancelPendingListingWithoutOwningService() => OnOwner(() =>
    {
        using var window = new Window(640, 480, "C07 replace", backend: RenderBackend.SW);
        using var renderer = new PickerRenderer(window); var root = new Panel(); window.AddChild(root);
        var probe = renderer.AddComponent<PickerBindingProbe>(TestRenderer.CreateRootHandler(renderer, root)).GetAwaiter().GetResult();
        var picker = root.Children.OfType<FilePicker>().Single(); var old = probe.Provider; old.Delay = true;
        var pending = picker.Controller!.RetryAsync();
        renderer.Dispatcher.InvokeAsync(probe.Replace).GetAwaiter().GetResult();
        Assert.True(old.PendingToken.IsCancellationRequested); Assert.False(old.Disposed);
        old.Pending.SetResult([new(new("/stale.svg"), "stale.svg", false)]); Wait(window, pending);
        Assert.DoesNotContain(picker.Controller.Items, i => i.Name == "stale.svg");
        var current = probe.Provider; current.Delay = true; pending = picker.Controller.RetryAsync();
        renderer.Dispatcher.InvokeAsync(probe.Remove).GetAwaiter().GetResult();
        Assert.True(current.PendingToken.IsCancellationRequested); Assert.False(current.Disposed);
        current.Pending.SetResult([]); Wait(window, pending); Assert.Empty(probe.Results);
    });

    [Fact]
    public void PathBrowseCancelReadOnlyAndAwaitingChangeCannotOverwriteReset() => OnOwner(() =>
    {
        using var window = new Window(640, 480, "C07 path", backend: RenderBackend.SW);
        using var renderer = new PickerRenderer(window); var root = new Panel(); window.AddChild(root);
        var probe = renderer.AddComponent<PickerBindingProbe>(TestRenderer.CreateRootHandler(renderer, root)).GetAwaiter().GetResult();
        renderer.Dispatcher.InvokeAsync(probe.Reset).GetAwaiter().GetResult();
        var path = root.Children.OfType<PathPicker>().Single();
        path.BrowseAsync().GetAwaiter().GetResult(); Assert.True(path.Picker.Dialog.IsOpen);
        path.Picker.Controller!.Cancel(); Assert.Equal(new PickerPath("/reset"), probe.Path); Assert.Equal(0, probe.PathChanges);
        probe.ReadOnly = true; renderer.Dispatcher.InvokeAsync(probe.Refresh).GetAwaiter().GetResult();
        path.BrowseAsync().GetAwaiter().GetResult(); Assert.False(path.Picker.Dialog.IsOpen);
        path.TextEditor.OnTextInput("blocked"); Drain(window); Assert.Equal("/reset", path.TextEditor.Text);
        probe.ReadOnly = false; probe.AwaitChange = new(TaskCreationOptions.RunContinuationsAsynchronously);
        renderer.Dispatcher.InvokeAsync(probe.Refresh).GetAwaiter().GetResult();
        renderer.Dispatcher.InvokeAsync(() => { path.TextEditor.OnKeyDown(269, 0, false); path.TextEditor.OnTextInput(" pending"); Drain(window); }).GetAwaiter().GetResult();
        Assert.Equal(1, probe.PathChanges);
        renderer.Dispatcher.InvokeAsync(probe.Reset).GetAwaiter().GetResult(); probe.AwaitChange.SetResult();
        Wait(window, probe.AfterChange.Task); Assert.Equal("/reset", path.TextEditor.Text); Assert.Equal(new PickerPath("/reset"), probe.Path);
        renderer.Dispatcher.InvokeAsync(probe.Remove).GetAwaiter().GetResult();
    });
    private static void Drain(Window w) { while (w.DispatcherQueue.TryDequeue(out var action)) action(); }
    private static void Wait(Window w, Task task)
    { var until = DateTime.UtcNow.AddSeconds(3); while (!task.IsCompleted && DateTime.UtcNow < until) { Drain(w); Thread.Yield(); } task.GetAwaiter().GetResult(); Drain(w); }
    private static void OnOwner(Action action)
    {
        Exception? error = null; var thread = new Thread(() => { try { action(); } catch (Exception e) { error = e; } }) { IsBackground = true };
        thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(12))); if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
    // Use the production ownership contract while retaining real renderer diff capture.
    private sealed class PickerRenderer(Window window) : TestRenderer
    { public override Microsoft.AspNetCore.Components.Dispatcher Dispatcher { get; } = new OwnerDispatcher(window); }
    private sealed class OwnerDispatcher(Window window) : Microsoft.AspNetCore.Components.Dispatcher
    {
        public override bool CheckAccess() => window.Dispatcher.CheckAccess();
        public override Task InvokeAsync(Action action) => window.Dispatcher.InvokeAsync(action);
        public override Task InvokeAsync(Func<Task> action) => window.Dispatcher.InvokeAsync(action);
        public override Task<T> InvokeAsync<T>(Func<T> action) => window.Dispatcher.InvokeAsync(action);
        public override Task<T> InvokeAsync<T>(Func<Task<T>> action) => window.Dispatcher.InvokeAsync(action);
    }
}
