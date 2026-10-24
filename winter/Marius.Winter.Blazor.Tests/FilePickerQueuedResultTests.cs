using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Marius.Winter.Blazor.Tests;

// Ignores cancellation deliberately, as a legitimate slow provider may do.
public sealed class QueuedPickerProvider : IFilePickerProvider, IDisposable
{
    public TaskCompletionSource<FilePickerEntry?> Stat = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public CancellationToken StatToken;
    public bool Disposed;
    public PickerPath Normalize(string path) => new(path);
    public PickerPath? Parent(PickerPath path) => null;
    public PickerPath Child(PickerPath directory, string name) => new(directory.Value.TrimEnd('/') + "/" + name);
    public Task<IReadOnlyList<FilePickerEntry>> ListAsync(PickerPath directory, CancellationToken token) =>
        Task.FromResult<IReadOnlyList<FilePickerEntry>>([new(new("/image.svg"), "image.svg", false)]);
    public Task<FilePickerEntry?> GetEntryAsync(PickerPath path, CancellationToken token) { StatToken = token; return Stat.Task; }
    public void Dispose() => Disposed = true;
}

[Collection("Native rendering")]
public class FilePickerQueuedResultTests
{
    [Theory]
    [InlineData("default", true, false)] [InlineData("default", true, true)]
    [InlineData("default", false, false)] [InlineData("default", false, true)]
    [InlineData("method", true, false)] [InlineData("method", true, true)]
    [InlineData("method", false, false)] [InlineData("method", false, true)]
    [InlineData("receiver", true, false)] [InlineData("receiver", true, true)]
    [InlineData("receiver", false, false)] [InlineData("receiver", false, true)]
    public void CompletedResultSurvivesRepeatedCompiledParentRenders(string binding, bool accepted, bool render) => Run((window, renderer, root, probe, picker) =>
    {
        probe.Binding = binding; probe.Refresh(); picker = root.Children.OfType<FilePicker>().Single();
        var session = Queue(probe, picker, accepted);
        var revision = picker.Controller!.Revision;
        Assert.True(window.DispatcherQueue.Count >= 2); Assert.False(picker.Controller.State.IsOpen);
        Assert.Empty(probe.Model.Order);
        renderer.AttributeUpdates.Clear();
        if (render)
            for (int i = 0; i < 3; i++)
            {
                probe.Refresh();
                // This is before any owner-queue drain, not a later render.
                Assert.Equal(session, picker.Controller.State.SessionId);
                Assert.Equal(revision, picker.Controller.Revision); Assert.False(picker.Controller.State.IsOpen);
                Assert.Equal("Unrelated label " + probe.Tick, root.Children.OfType<Label>().Single().Text);
            }
        if (render)
        {
            Assert.Contains(renderer.AttributeUpdates, a => a.Name == "Configuration");
            Assert.Contains(renderer.AttributeUpdates, a => a.Name == "Text" && Equals(a.Value, "Unrelated label " + probe.Tick));
        }
        Drain(window);
        Assert.Equal(new[] { "open:False", accepted ? "selected" : "canceled" }, probe.Model.Order);
        Assert.Equal(new FilePickerResult(session, FilePickerMode.OpenFile, accepted ? new PickerPath("/image.svg") : null), Assert.Single(probe.Model.Results));
        Assert.False(probe.Model.Open); Assert.False(picker.Dialog.IsOpen); Assert.Equal(session, picker.Controller.State.SessionId);
        Assert.Equal(0, probe.LegacyChanges);
        for (int i = 0; i < 3; i++) { probe.Refresh(); Drain(window); }
        Assert.Equal(2, probe.Model.Order.Count); Assert.Equal(session, picker.Controller.State.SessionId);
    });

    public static IEnumerable<object[]> Replacements()
    {
        foreach (string transition in new[] { "reset", "close", "reopen", "controller", "provider", "mode", "directory", "name", "filters", "target", "method", "receiver", "clear", "unmount" })
            foreach (bool accepted in new[] { false, true }) yield return [transition, accepted];
    }
    [Theory] [MemberData(nameof(Replacements))]
    public void GenuineReplacementBeforeOwnerDrainInvalidatesOldCompletion(string transition, bool accepted) => Run((window, renderer, root, probe, picker) =>
    {
        probe.Binding = "receiver"; probe.Refresh(); picker = root.Children.OfType<FilePicker>().Single();
        var model = probe.Model; var provider = probe.Provider; var oldController = picker.Controller!;
        Queue(probe, picker, accepted);
        Replace(probe, transition); Drain(window);
        Assert.Empty(model.Order); Assert.Empty(model.Results); Assert.Empty(probe.Model.Results); Assert.Equal(0, probe.LegacyChanges);
        Assert.False(provider.Disposed);
        if (transition == "unmount") Assert.True(picker.IsDisposed);
        else if (transition == "close") Assert.False(picker.Controller!.State.IsOpen);
        else Assert.True(picker.Controller!.State.IsOpen);
        // A borrowed replacement controller/provider survives view removal.
        probe.Show = false; probe.Refresh(); Drain(window);
        if (probe.Controller != null) { probe.Controller.Reset(); probe.Controller.Dispose(); }
        Assert.False(probe.Provider.Disposed);
    });

    [Theory]
    [InlineData("reset")] [InlineData("reopen")] [InlineData("controller")]
    [InlineData("provider")] [InlineData("target")] [InlineData("unmount")]
    public void UncooperativeAsyncStatCannotCompleteReplacedSession(string transition) => Run((window, renderer, root, probe, picker) =>
    {
        var provider = probe.Provider; var model = probe.Model; var controller = picker.Controller!;
        controller.Select(new("/image.svg")); var pending = controller.AcceptAsync();
        Assert.True(controller.State.IsChecking); Replace(probe, transition);
        Assert.True(provider.StatToken.IsCancellationRequested);
        Task.Run(() => provider.Stat.SetResult(new(new("/image.svg"), "image.svg", false))).GetAwaiter().GetResult();
        Assert.True(pending.Wait(TimeSpan.FromSeconds(3))); Drain(window);
        Assert.Empty(model.Order); Assert.Empty(model.Results); Assert.Empty(probe.Model.Results);
        probe.Show = false; probe.Refresh();
        if (probe.Controller != null) probe.Controller.Dispose();
        Assert.False(provider.Disposed);
    });

    [Theory] [InlineData(true)] [InlineData(false)]
    public void RejectedCloseReopensOnceAndRetainsOriginatingDiscreteResult(bool accepted) => Run((window, renderer, root, probe, picker) =>
    {
        probe.Model.Reject = true;
        var session = Queue(probe, picker, accepted); Drain(window);
        Assert.Equal(new[] { "open:False", accepted ? "selected" : "canceled" }, probe.Model.Order);
        Assert.Equal(session, Assert.Single(probe.Model.Results).SessionId);
        Assert.True(probe.Model.Open); Assert.True(picker.Dialog.IsOpen);
        var reopened = picker.Controller!.State.SessionId; Assert.True(reopened > session);
        for (int i = 0; i < 3; i++) { probe.Refresh(); Drain(window); }
        Assert.Equal(reopened, picker.Controller.State.SessionId); Assert.Equal(2, probe.Model.Order.Count);
    });

    [Theory]
    [InlineData("reset", true)] [InlineData("reset", false)]
    [InlineData("provider", true)] [InlineData("provider", false)]
    [InlineData("target", true)] [InlineData("target", false)]
    [InlineData("unmount", true)] [InlineData("unmount", false)]
    public void ReplacementRequestedByOpenCallbackRetainsCapturedResultWithoutRetargetingAfterAwait(string transition, bool accepted) => Run((window, renderer, root, probe, picker) =>
    {
        var model = probe.Model;
        model.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        model.DuringOpen = () => { model.Open = true; Replace(probe, transition); };
        var session = Queue(probe, picker, accepted); Drain(window);
        // StateHasChanged during a renderer event requests a subsequent batch.
        // Both captured callbacks have already begun before that batch applies.
        Assert.Equal(new[] { "open:False", accepted ? "selected" : "canceled" }, model.Order);
        Assert.Equal(session, Assert.Single(model.Results).SessionId);
        model.Gate.SetResult();
        var until = DateTime.UtcNow.AddSeconds(3);
        while (!model.Finished.Task.IsCompleted && DateTime.UtcNow < until) { Drain(window); Thread.Yield(); }
        Assert.True(model.Finished.Task.IsCompletedSuccessfully); Drain(window);
        Assert.Equal(2, model.Order.Count); Assert.Single(model.Results);
        if (transition == "target") { Assert.Empty(probe.Model.Results); Assert.Empty(probe.Model.Order); }
        if (transition != "unmount") { Assert.True(probe.Model.Open); Assert.True(picker.Dialog.IsOpen); }
    });

    [Theory] [InlineData(true)] [InlineData(false)]
    public void SaveOverwriteCompletionRetainsCapturedModePathPermissionAndSession(bool overwrite) => Run((window, renderer, root, probe, picker) =>
    {
        probe.Mode = FilePickerMode.SaveFile; probe.Name = "image.svg"; probe.Refresh();
        var controller = picker.Controller!; long session = controller.State.SessionId;
        var pending = controller.AcceptAsync();
        Task.Run(() => probe.Provider.Stat.SetResult(new(new("/image.svg"), "image.svg", false))).GetAwaiter().GetResult();
        Assert.True(pending.Wait(TimeSpan.FromSeconds(3)));
        Assert.NotNull(controller.State.Overwrite);
        Task.Run(() => { if (overwrite) controller.ConfirmOverwrite(controller.State.Overwrite!.Id, true); else controller.Cancel(); }).GetAwaiter().GetResult();
        probe.Refresh(); probe.Refresh(); Assert.Equal(session, controller.State.SessionId); Drain(window);
        Assert.Equal(new FilePickerResult(session, FilePickerMode.SaveFile, overwrite ? new PickerPath("/image.svg") : null, overwrite), Assert.Single(probe.Model.Results));
        Assert.Equal(new[] { "open:False", overwrite ? "selected" : "canceled" }, probe.Model.Order);
    });

    private static void Replace(FilePickerQueuedProbe probe, string transition)
    {
        switch (transition)
        {
            case "reset": probe.Version++; break;
            case "close": probe.Model.Open = false; break;
            case "reopen": probe.Model.Open = false; probe.Refresh(); probe.Model.Open = true; break;
            case "controller": probe.Controller = new(probe.Provider); break;
            case "provider": probe.Provider = new(); break;
            case "mode": probe.Mode = FilePickerMode.SaveFile; break;
            case "directory": probe.Directory = "/folder"; break;
            case "name": probe.Name = "other.svg"; break;
            case "filters": probe.Filters = [new("SVG", ".svg")]; break;
            case "target": probe.Model = new(); break;
            case "method": probe.OtherMethod = true; break;
            case "receiver": probe.Receiver = new(probe.Refresh); break;
            case "clear": probe.Clear = true; break;
            case "unmount": probe.Show = false; break;
            default: throw new ArgumentOutOfRangeException(nameof(transition));
        }
        probe.Refresh();
    }
    internal static long Queue(FilePickerQueuedProbe probe, FilePicker picker, bool accepted)
    {
        var controller = picker.Controller!; long session = controller.State.SessionId;
        if (accepted)
        {
            controller.Select(new("/image.svg")); var pending = controller.AcceptAsync();
            Task.Run(() => probe.Provider.Stat.SetResult(new(new("/image.svg"), "image.svg", false))).GetAwaiter().GetResult();
            Assert.True(pending.Wait(TimeSpan.FromSeconds(3)));
        }
        else Task.Run(controller.Cancel).GetAwaiter().GetResult();
        return session;
    }
    internal static void Drain(Window window) { while (window.DispatcherQueue.TryDequeue(out var action)) action(); }
    private static void Run(Action<Window, TestRenderer, Panel, FilePickerQueuedProbe, FilePicker> action) => PathPickerCallbackTests.OnOwner(() =>
    {
        using var window = new Window(640, 480, "C07 queued completion", backend: RenderBackend.SW);
        using var renderer = new PathPickerCallbackTests.OwnerRenderer(window); var root = new Panel(); window.AddChild(root);
        var probe = renderer.AddComponent<FilePickerQueuedProbe>(TestRenderer.CreateRootHandler(renderer, root)).GetAwaiter().GetResult();
        var picker = root.Children.OfType<FilePicker>().Single();
        try { action(window, renderer, root, probe, picker); }
        finally { probe.Show = false; probe.Refresh(); }
    });
}
