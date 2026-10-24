using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Marius.Winter;

public enum FilePickerMode { OpenFile, SaveFile, SelectFolder }
public readonly record struct PickerPath(string Value)
{
    public override string ToString() => Value;
}
public sealed record FilePickerEntry(PickerPath Path, string Name, bool IsDirectory);
/// <summary>One named extension group. An empty extension set accepts all files.</summary>
public sealed record FilePickerFilter
{
    public FilePickerFilter(string name, params string[] extensions)
    {
        Name = name;
        Extensions = Array.AsReadOnly(extensions.Select(e => e.Trim().TrimStart('*').ToLowerInvariant())
            .Where(e => e.Length > 0 && e != ".").Select(e => e.StartsWith('.') ? e : "." + e).Distinct().ToArray());
    }
    public string Name { get; }
    public IReadOnlyList<string> Extensions { get; }
    public bool Accepts(string name) => Extensions.Count == 0 || Extensions.Any(e => name.EndsWith(e, StringComparison.OrdinalIgnoreCase));
}
public enum FilePickerLoadState { Idle, Loading, Ready, Error }
public sealed record FilePickerOverwrite(long Id, PickerPath Path);
public sealed record FilePickerResult(long SessionId, FilePickerMode Mode, PickerPath? Path, bool Overwrite = false)
{
    public bool Accepted => Path.HasValue;
}
public sealed record FilePickerState(bool IsOpen, long SessionId, FilePickerMode Mode, PickerPath CurrentDirectory,
    IReadOnlyList<FilePickerEntry> Items, FilePickerLoadState LoadState = FilePickerLoadState.Idle,
    string? Error = null, PickerPath? SelectedPath = null, string FileName = "", FilePickerFilter? Filter = null,
    bool CanGoBack = false, bool IsChecking = false, FilePickerOverwrite? Overwrite = null);

/// <summary>Borrowed filesystem service. Paths are provider identities, not encoded tree tags.
/// Implementations must report ordinary listing/stat errors; no operation writes file contents.</summary>
public interface IFilePickerProvider
{
    PickerPath Normalize(string path);
    PickerPath? Parent(PickerPath path);
    PickerPath Child(PickerPath directory, string name);
    Task<IReadOnlyList<FilePickerEntry>> ListAsync(PickerPath directory, CancellationToken cancellationToken);
    Task<FilePickerEntry?> GetEntryAsync(PickerPath path, CancellationToken cancellationToken);
}

/// <summary>Frontend-independent single-selection picker. Notifications run on the mutating
/// thread; views dispatch to their owner. The provider is always caller owned. Cancellation
/// and generation identity protect all pending work, including uncooperative providers.</summary>
public sealed class FilePickerController : IDisposable
{
    private readonly object _gate = new();
    private IFilePickerProvider _provider;
    private FilePickerState _state = new(false, 0, FilePickerMode.OpenFile, default, Array.Empty<FilePickerEntry>());
    private readonly Stack<PickerPath> _back = new();
    private CancellationTokenSource? _pending;
    private long _generation, _session;
    private bool _disposed;
    public FilePickerController(IFilePickerProvider provider) => _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    public event Action? Changed;
    public event Action<FilePickerResult>? Completed;
    public FilePickerState State { get { lock (_gate) return _state; } }
    public long Revision { get { lock (_gate) return _generation; } }
    public PickerPath CurrentDirectory => State.CurrentDirectory;
    public IReadOnlyList<FilePickerEntry> Items => State.Items;
    public IFilePickerProvider Provider { get { lock (_gate) return _provider; } }

    public Task OpenAsync(FilePickerMode mode, string directory, FilePickerFilter? filter = null, string fileName = "")
    {
        lock (_gate)
        {
            CheckAlive(); Invalidate(); _back.Clear();
            _state = new(true, ++_session, mode, new(directory), Array.Empty<FilePickerEntry>(), FileName: fileName, Filter: filter);
            return NavigateCore(new(directory), false);
        }
    }
    /// <summary>Silent parent reset: invalidates loads, confirmations and result identity.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            CheckAlive(); Invalidate(); _back.Clear();
            _state = _state with { IsOpen = false, SessionId = ++_session, Items = Array.Empty<FilePickerEntry>(), LoadState = FilePickerLoadState.Idle,
                Error = null, SelectedPath = null, FileName = "", IsChecking = false, Overwrite = null, CanGoBack = false };
            Changed?.Invoke();
        }
    }
    public Task ReplaceProviderAsync(IFilePickerProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (_gate)
        {
            CheckAlive(); if (ReferenceEquals(provider, _provider)) return Task.CompletedTask;
            Invalidate(); _provider = provider; _back.Clear();
            _state = _state with { Overwrite = null, IsChecking = false, CanGoBack = false, SelectedPath = null };
            if (_state.IsOpen) return NavigateCore(_state.CurrentDirectory, false);
            Changed?.Invoke(); return Task.CompletedTask;
        }
    }
    public Task NavigateAsync(PickerPath directory)
    { lock (_gate) { CheckAlive(); return _state.IsOpen ? NavigateCore(directory, true) : Task.CompletedTask; } }
    public Task BackAsync()
    {
        lock (_gate)
        {
            CheckAlive(); return _state.IsOpen && _back.Count > 0 ? NavigateCore(_back.Pop(), false) : Task.CompletedTask;
        }
    }
    public Task UpAsync()
    {
        lock (_gate)
        {
            CheckAlive();
            try { return _state.IsOpen && _provider.Parent(_state.CurrentDirectory) is { } parent ? NavigateCore(parent, true) : Task.CompletedTask; }
            catch (Exception error) { Fail(error); return Task.CompletedTask; }
        }
    }
    public Task RetryAsync()
    { lock (_gate) { CheckAlive(); return _state.IsOpen ? NavigateCore(_state.CurrentDirectory, false) : Task.CompletedTask; } }
    public Task SetFilterAsync(FilePickerFilter? filter)
    {
        lock (_gate)
        {
            CheckAlive(); _state = _state with { Filter = filter };
            return _state.IsOpen ? NavigateCore(_state.CurrentDirectory, false) : Task.CompletedTask;
        }
    }
    public void SetFileName(string name)
    {
        lock (_gate)
        {
            CheckAlive(); if (!_state.IsOpen || _state.FileName == name) return;
            // A listing remains useful while the filename changes. Acceptance and
            // overwrite are separate identities and must be invalidated immediately.
            if (_state.IsChecking || _state.Overwrite != null) Invalidate();
            _state = _state with { FileName = name, SelectedPath = null, IsChecking = false, Overwrite = null, Error = null };
            Changed?.Invoke();
        }
    }
    public void Select(PickerPath? path)
    {
        lock (_gate)
        {
            CheckAlive(); if (!_state.IsOpen || _state.LoadState != FilePickerLoadState.Ready) return;
            var item = _state.Items.FirstOrDefault(i => i.Path == path);
            if (path != null && item == null) return;
            if (_state.IsChecking || _state.Overwrite != null) Invalidate();
            _state = _state with { SelectedPath = path, FileName = item is { IsDirectory: false } ? item.Name : _state.FileName,
                Error = null, IsChecking = false, Overwrite = null };
            Changed?.Invoke();
        }
    }
    public Task ActivateAsync(PickerPath path)
    {
        lock (_gate)
        {
            CheckAlive(); if (!_state.IsOpen) return Task.CompletedTask;
            var item = _state.Items.FirstOrDefault(i => i.Path == path);
            if (item == null) return Task.CompletedTask;
            if (item.IsDirectory) return NavigateCore(path, true);
            Select(path); return AcceptAsync();
        }
    }
    public Task AcceptAsync()
    {
        lock (_gate)
        {
            CheckAlive();
            if (!_state.IsOpen || _state.IsChecking || _state.Overwrite != null || _state.LoadState != FilePickerLoadState.Ready) return Task.CompletedTask;
            if (_state.Mode != FilePickerMode.SelectFolder && _state.Items.FirstOrDefault(i => i.Path == _state.SelectedPath && i.IsDirectory) is { } folder)
                return NavigateCore(folder.Path, true);
            PickerPath path;
            try
            {
                if (_state.Mode == FilePickerMode.SelectFolder)
                    path = _state.Items.FirstOrDefault(i => i.Path == _state.SelectedPath && i.IsDirectory)?.Path ?? _state.CurrentDirectory;
                else
                {
                    if (string.IsNullOrWhiteSpace(_state.FileName)) throw new IOException("Enter a file name or select a file.");
                    if (_state.Filter?.Accepts(_state.FileName) == false) throw new IOException("The file name must match the selected extension filter.");
                    path = _provider.Child(_state.CurrentDirectory, _state.FileName);
                }
            }
            catch (Exception error) { Fail(error); return Task.CompletedTask; }
            Invalidate();
            var request = _pending = new CancellationTokenSource(); var token = request.Token;
            var generation = _generation; var provider = _provider; var mode = _state.Mode;
            _state = _state with { IsChecking = true, Error = null }; Changed?.Invoke();
            if (!Current(generation, request)) return Task.CompletedTask;
            return CheckAsync();

            async Task CheckAsync()
            {
                try
                {
                    var entry = await provider.GetEntryAsync(path, token).ConfigureAwait(false);
                    lock (_gate)
                    {
                        if (!Current(generation, request)) return;
                        _pending = null; request.Dispose();
                        _state = _state with { IsChecking = false };
                        if (mode == FilePickerMode.SelectFolder && entry is not { IsDirectory: true }) Fail(new IOException("The selected folder no longer exists."));
                        else if (mode != FilePickerMode.SelectFolder && entry?.IsDirectory == true) Fail(new IOException("Choose a file name, not a directory."));
                        else if (mode == FilePickerMode.OpenFile && entry == null) Fail(new FileNotFoundException("The selected file no longer exists."));
                        else if (mode == FilePickerMode.SaveFile && entry != null)
                        { _state = _state with { Overwrite = new(generation, path) }; Changed?.Invoke(); }
                        else Finish(path, false);
                    }
                }
                catch (Exception error)
                {
                    lock (_gate)
                    {
                        if (!Current(generation, request)) return;
                        _pending = null; request.Dispose(); _state = _state with { IsChecking = false }; Fail(error);
                    }
                }
            }
        }
    }
    /// <summary>The explicit confirmation identity prevents a delayed Yes from accepting a newer file.</summary>
    public void ConfirmOverwrite(long confirmationId, bool overwrite)
    {
        lock (_gate)
        {
            CheckAlive();
            if (!_state.IsOpen || _state.Overwrite is not { } pending || pending.Id != confirmationId || confirmationId != _generation) return;
            if (overwrite) Finish(pending.Path, true);
            else { Invalidate(); _state = _state with { Overwrite = null }; Changed?.Invoke(); }
        }
    }
    public void Cancel()
    { lock (_gate) { CheckAlive(); if (_state.IsOpen) Finish(null, false); } }
    private Task NavigateCore(PickerPath directory, bool remember)
    {
        Invalidate();
        var previous = _state.CurrentDirectory;
        _state = _state with { CurrentDirectory = directory, Items = Array.Empty<FilePickerEntry>(), LoadState = FilePickerLoadState.Loading,
            Error = null, SelectedPath = null, IsChecking = false, Overwrite = null };
        try { directory = _provider.Normalize(directory.Value); }
        catch (Exception error) { _state = _state with { LoadState = FilePickerLoadState.Error }; Fail(error); return Task.CompletedTask; }
        if (remember && previous != directory) _back.Push(previous);
        _state = _state with { CurrentDirectory = directory, CanGoBack = _back.Count > 0 };
        var request = _pending = new CancellationTokenSource(); var token = request.Token;
        var generation = _generation; var provider = _provider; var filter = _state.Filter; var mode = _state.Mode;
        Changed?.Invoke();
        if (!Current(generation, request)) return Task.CompletedTask;
        return LoadAsync();

        async Task LoadAsync()
        {
            try
            {
                var entries = await provider.ListAsync(directory, token).ConfigureAwait(false);
                var items = Array.AsReadOnly(entries.Where(i => i.IsDirectory || (mode != FilePickerMode.SelectFolder && (filter?.Accepts(i.Name) ?? true)))
                    .OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToArray());
                lock (_gate)
                {
                    if (!Current(generation, request)) return;
                    _pending = null; request.Dispose();
                    _state = _state with { Items = items, LoadState = FilePickerLoadState.Ready }; Changed?.Invoke();
                }
            }
            catch (Exception error)
            {
                lock (_gate)
                {
                    if (!Current(generation, request)) return;
                    _pending = null; request.Dispose();
                    _state = _state with { LoadState = FilePickerLoadState.Error }; Fail(error);
                }
            }
        }
    }
    private void Finish(PickerPath? path, bool overwrite)
    {
        var result = new FilePickerResult(_state.SessionId, _state.Mode, path, overwrite);
        var completed = Completed;
        Invalidate();
        _state = _state with { IsOpen = false, IsChecking = false, Overwrite = null, LoadState = FilePickerLoadState.Idle };
        Changed?.Invoke(); completed?.Invoke(result);
    }
    private bool Current(long generation, CancellationTokenSource request) => !_disposed && _state.IsOpen &&
        _generation == generation && ReferenceEquals(_pending, request) && !request.IsCancellationRequested;
    private void Invalidate()
    {
        _generation++;
        var pending = _pending; _pending = null;
        if (pending != null) { try { pending.Cancel(); } finally { pending.Dispose(); } }
    }
    private void Fail(Exception error) { _state = _state with { Error = error.Message }; Changed?.Invoke(); }
    private void CheckAlive() => ObjectDisposedException.ThrowIf(_disposed, this);
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return; _disposed = true; Invalidate();
            _state = _state with { IsOpen = false, IsChecking = false, Overwrite = null };
            Changed = null; Completed = null;
        }
    }
}
