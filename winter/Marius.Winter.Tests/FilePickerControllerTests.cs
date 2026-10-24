using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Marius.Winter.Tests;

public class FilePickerControllerTests
{
    [Theory]
    [InlineData("cancel")]
    [InlineData("reset")]
    [InlineData("replace")]
    [InlineData("filter")]
    [InlineData("dispose")]
    public async Task EverySessionBoundaryCancelsAndSuppressesLateListings(string boundary)
    {
        var provider = new DelayedProvider(); var picker = new FilePickerController(provider);
        var completions = new List<FilePickerResult>(); picker.Completed += completions.Add;
        var pending = picker.OpenAsync(FilePickerMode.OpenFile, "/first");
        Task next = Task.CompletedTask;
        switch (boundary)
        {
            case "cancel": picker.Cancel(); break;
            case "reset": picker.Reset(); break;
            case "replace": next = picker.ReplaceProviderAsync(new MemoryProvider()); break;
            case "filter": next = picker.SetFilterAsync(new("SVG", ".svg")); break;
            case "dispose": picker.Dispose(); break;
        }
        Assert.True(provider.Requests[0].Token.IsCancellationRequested);
        if (boundary == "filter") provider.Requests[1].Completion.SetResult([]);
        await next;
        provider.Requests[0].Completion.SetResult([new(new("/first/stale.svg"), "stale.svg", false)]);
        await pending;
        Assert.DoesNotContain(picker.Items, i => i.Name == "stale.svg");
        Assert.Equal(boundary == "cancel" ? 1 : 0, completions.Count);
        if (completions.Count > 0) Assert.False(completions[0].Accepted);
        picker.Dispose();
    }

    [Fact]
    public async Task OpenSaveFolderFilterNameBackUpAndOverwriteAreTypedAndDoNotWrite()
    {
        var provider = new MemoryProvider(); using var picker = new FilePickerController(provider);
        var results = new List<FilePickerResult>(); picker.Completed += results.Add;
        var svg = new FilePickerFilter("SVG", "*.svg");
        await picker.OpenAsync(FilePickerMode.OpenFile, "/", svg);
        Assert.Equal(new[] { "Documents", "image.svg" }, picker.Items.Select(i => i.Name));
        picker.SetFileName("missing.svg"); await picker.AcceptAsync(); Assert.Contains("no longer exists", picker.State.Error); Assert.Empty(results);
        picker.SetFileName("notes.txt"); await picker.AcceptAsync(); Assert.Contains("extension", picker.State.Error);
        picker.Select(new("/image.svg")); await picker.AcceptAsync();
        Assert.Equal(new PickerPath("/image.svg"), Assert.Single(results).Path); Assert.False(picker.State.IsOpen);
        await picker.OpenAsync(FilePickerMode.SaveFile, "/", svg, "image.svg"); await picker.AcceptAsync();
        var first = Assert.IsType<FilePickerOverwrite>(picker.State.Overwrite); Assert.Single(results);
        picker.ConfirmOverwrite(first.Id, false); Assert.Null(picker.State.Overwrite); Assert.True(picker.State.IsOpen);
        await picker.AcceptAsync(); var newer = Assert.IsType<FilePickerOverwrite>(picker.State.Overwrite);
        picker.ConfirmOverwrite(first.Id, true); Assert.True(picker.State.IsOpen); Assert.Equal(newer, picker.State.Overwrite);
        picker.ConfirmOverwrite(newer.Id, true); Assert.Equal(2, results.Count); Assert.True(results[1].Overwrite);
        await picker.OpenAsync(FilePickerMode.SaveFile, "/", svg, "Žąsis 👩🏽‍💻é.svg"); await picker.AcceptAsync();
        Assert.Equal("/Žąsis 👩🏽‍💻é.svg", results[2].Path?.Value);
        await picker.OpenAsync(FilePickerMode.SelectFolder, "/"); Assert.All(picker.Items, i => Assert.True(i.IsDirectory));
        await picker.ActivateAsync(new("/Documents")); Assert.True(picker.State.CanGoBack); Assert.Empty(picker.Items);
        await picker.BackAsync(); Assert.Equal("/", picker.CurrentDirectory.Value);
        await picker.NavigateAsync(new("/Documents")); await picker.UpAsync(); Assert.Equal("/", picker.CurrentDirectory.Value);
        picker.Select(new("/Documents")); await picker.AcceptAsync(); Assert.Equal(new PickerPath("/Documents"), results[3].Path);
        Assert.Equal(0, provider.Writes);
    }

    [Theory]
    [InlineData("navigate")]
    [InlineData("filter")]
    [InlineData("name")]
    [InlineData("reset")]
    [InlineData("replace")]
    [InlineData("cancel")]
    public async Task LateStatCannotPublishOrConfirmANewerIdentity(string boundary)
    {
        var provider = new MemoryProvider { StatGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var picker = new FilePickerController(provider); var results = new List<FilePickerResult>(); picker.Completed += results.Add;
        await picker.OpenAsync(FilePickerMode.SaveFile, "/", fileName: "image.svg"); var pending = picker.AcceptAsync();
        Assert.True(picker.State.IsChecking);
        switch (boundary)
        {
            case "navigate": await picker.NavigateAsync(new("/Documents")); break;
            case "filter": await picker.SetFilterAsync(new("SVG", ".svg")); break;
            case "name": picker.SetFileName("new.svg"); break;
            case "reset": picker.Reset(); break;
            case "replace": await picker.ReplaceProviderAsync(new MemoryProvider()); break;
            case "cancel": picker.Cancel(); break;
        }
        Assert.True(provider.StatToken.IsCancellationRequested);
        provider.StatGate.SetResult(new(new("/image.svg"), "image.svg", false)); await pending;
        Assert.Null(picker.State.Overwrite); Assert.DoesNotContain(results, r => r.Accepted);
    }

    [Fact]
    public async Task OrdinaryListAndStatFailuresAreVisibleAndRetryable()
    {
        var provider = new MemoryProvider { FailListing = true }; using var picker = new FilePickerController(provider);
        await picker.OpenAsync(FilePickerMode.OpenFile, "/");
        Assert.Equal(FilePickerLoadState.Error, picker.State.LoadState); Assert.Contains("unavailable", picker.State.Error);
        provider.FailListing = false; await picker.RetryAsync(); Assert.Equal(FilePickerLoadState.Ready, picker.State.LoadState);
        provider.FailStat = true; picker.SetFileName("image.svg"); await picker.AcceptAsync();
        Assert.Contains("read permission", picker.State.Error); Assert.True(picker.State.IsOpen); Assert.False(picker.State.IsChecking);
        provider.FailStat = false; await picker.AcceptAsync(); Assert.False(picker.State.IsOpen);
    }

    [Fact]
    public async Task LocalProviderUsesActualOwnedFilesAndReportsOrdinaryNonDirectoryError()
    {
        var evidence = Environment.GetEnvironmentVariable("WINTER_C07_EVIDENCE") ?? System.IO.Path.Combine(Environment.CurrentDirectory, "tmp", "winter-components");
        var directory = System.IO.Path.Combine(evidence, "local-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); Directory.CreateDirectory(System.IO.Path.Combine(directory, "empty"));
        var input = System.IO.Path.Combine(directory, "Žąsis.svg"); await File.WriteAllTextAsync(input, "<svg/>");
        var provider = new LocalFilePickerProvider(directory); using var picker = new FilePickerController(provider);
        await picker.OpenAsync(FilePickerMode.OpenFile, directory, new("SVG", ".svg")); Assert.Equal(2, picker.Items.Count);
        Assert.Null(provider.Parent(provider.Normalize(directory)));
        Assert.Throws<IOException>(() => provider.Child(provider.Normalize(directory), "../outside.svg"));
        picker.Select(new(input)); FilePickerResult? result = null; picker.Completed += r => result = r; await picker.AcceptAsync();
        Assert.Equal(new PickerPath(input), result!.Path);
        await picker.OpenAsync(FilePickerMode.SaveFile, directory, fileName: "new.svg"); await picker.AcceptAsync();
        Assert.False(File.Exists(result!.Path!.Value.Value)); Assert.Equal("<svg/>", await File.ReadAllTextAsync(input));
        await picker.OpenAsync(FilePickerMode.SelectFolder, input); Assert.Equal(FilePickerLoadState.Error, picker.State.LoadState); Assert.False(string.IsNullOrWhiteSpace(picker.State.Error));
        await picker.NavigateAsync(new(directory)); Assert.Equal(FilePickerLoadState.Ready, picker.State.LoadState);
        // Owned fixture cleanup only.
        Directory.Delete(directory, true);
    }

    private sealed class MemoryProvider : IFilePickerProvider
    {
        public bool FailListing, FailStat;
        public int Writes => 0;
        public TaskCompletionSource<FilePickerEntry?>? StatGate;
        public CancellationToken StatToken;
        private readonly FilePickerEntry[] _entries = [new(new("/Documents"), "Documents", true), new(new("/image.svg"), "image.svg", false), new(new("/notes.txt"), "notes.txt", false)];
        public PickerPath Normalize(string path) => new(path);
        public PickerPath? Parent(PickerPath path) => path.Value == "/" ? null : new("/");
        public PickerPath Child(PickerPath path, string name) => new(path.Value.TrimEnd('/') + "/" + name);
        public Task<IReadOnlyList<FilePickerEntry>> ListAsync(PickerPath directory, CancellationToken token) => FailListing
            ? Task.FromException<IReadOnlyList<FilePickerEntry>>(new IOException("Folder temporarily unavailable"))
            : Task.FromResult<IReadOnlyList<FilePickerEntry>>(directory.Value == "/" ? _entries : []);
        public Task<FilePickerEntry?> GetEntryAsync(PickerPath path, CancellationToken token)
        {
            StatToken = token;
            if (StatGate != null) return StatGate.Task;
            if (FailStat) return Task.FromException<FilePickerEntry?>(new IOException("Missing read permission"));
            return Task.FromResult(path.Value == "/" ? new FilePickerEntry(path, "/", true) : _entries.FirstOrDefault(i => i.Path == path));
        }
    }
    [Fact]
    public async Task NavigationCancelsPendingListingAndIgnoresProviderIgnoringCancellation()
    {
        var provider = new DelayedProvider();
        using var picker = new FilePickerController(provider);
        var first = picker.OpenAsync(FilePickerMode.OpenFile, "/first");
        var second = picker.NavigateAsync(new PickerPath("/second"));
        Assert.True(provider.Requests[0].Token.IsCancellationRequested);
        provider.Requests[1].Completion.SetResult([new(new("/second/new.svg"), "new.svg", false)]);
        await second;
        provider.Requests[0].Completion.SetResult([new(new("/first/old.svg"), "old.svg", false)]);
        await first;
        Assert.Equal(new PickerPath("/second"), picker.CurrentDirectory);
        Assert.Equal("new.svg", Assert.Single(picker.Items).Name);
    }

    internal sealed class DelayedProvider : IFilePickerProvider
    {
        internal sealed record Request(CancellationToken Token, TaskCompletionSource<IReadOnlyList<FilePickerEntry>> Completion);
        internal List<Request> Requests { get; } = new();
        public PickerPath Normalize(string path) => new(path);
        public PickerPath? Parent(PickerPath path) => path.Value == "/" ? null : new("/");
        public PickerPath Child(PickerPath directory, string name) => new(directory.Value.TrimEnd('/') + "/" + name);
        public Task<IReadOnlyList<FilePickerEntry>> ListAsync(PickerPath path, CancellationToken token)
        {
            var request = new Request(token, new(TaskCreationOptions.RunContinuationsAsynchronously));
            Requests.Add(request); return request.Completion.Task;
        }
        public Task<FilePickerEntry?> GetEntryAsync(PickerPath path, CancellationToken token) => Task.FromResult<FilePickerEntry?>(new(path, "file.svg", false));
    }
}
