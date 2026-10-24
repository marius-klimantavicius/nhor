using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Marius.Winter;

public enum TreeLoadState { Idle, Loading, Loaded, Error }
public enum TreeRequestKind { Select, Expand, Collapse, Activate, Retry, Reveal }

/// <summary>An immutable user action; state and item identity are captured when requested.</summary>
public sealed record TreeRequest<TItem>(TreeRequestKind Kind, string Key, TItem Item,
    string? SelectedKey, IReadOnlySet<string> ExpandedKeys);

/// <summary>A read-only presentation snapshot. Keys use ordinal, case-sensitive equality.</summary>
public sealed record DataTreeNode<TItem>(string Key, TItem Item, string Text, string? Icon,
    bool IsExpanded, bool MayHaveChildren, TreeLoadState LoadState, string? Error,
    IReadOnlyList<DataTreeNode<TItem>> Children);

/// <summary>Frontend-independent single-selection tree data and cancellation-aware lazy loading.
/// Does not own selectors, loaders or their services. Notifications run on the mutating thread;
/// presentations must dispatch to their UI owner. All public state operations are serialized.</summary>
public sealed class TreeController<TItem> : IDisposable
{
    private readonly object _gate = new();
    private readonly Func<TItem, string> _key;
    private readonly Func<TItem, string> _text;
    private readonly Func<TItem, IEnumerable<TItem>> _children;
    private readonly Func<TItem, string?>? _icon;
    private readonly Func<TItem, bool>? _mayHaveChildren;
    private readonly Func<TItem, CancellationToken, Task<IReadOnlyList<TItem>>>? _load;
    private Dictionary<string, Entry> _nodes = new(StringComparer.Ordinal);
    private List<Entry> _roots = new();
    private HashSet<string> _expanded = new(StringComparer.Ordinal);
    private string? _selected;
    private bool _disposed;
    private long _revision;

    private sealed class Entry(string key, TItem item, string text, string? icon, Entry? parent, bool lazy)
    {
        public readonly string Key = key;
        public readonly TItem Item = item;
        public readonly string Text = text;
        public readonly string? Icon = icon;
        public readonly Entry? Parent = parent;
        public readonly bool Lazy = lazy;
        public List<Entry> Children = new();
        public TreeLoadState State = lazy ? TreeLoadState.Idle : TreeLoadState.Loaded;
        public string? Error;
        public CancellationTokenSource? Cancellation;
        public long Generation;
        public Task Pending = Task.CompletedTask;
    }

    public TreeController(Func<TItem, string> keySelector, Func<TItem, string> textSelector,
        Func<TItem, IEnumerable<TItem>>? childrenSelector = null, Func<TItem, string?>? iconSelector = null,
        Func<TItem, bool>? mayHaveChildren = null,
        Func<TItem, CancellationToken, Task<IReadOnlyList<TItem>>>? loadChildren = null)
    {
        _key = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        _text = textSelector ?? throw new ArgumentNullException(nameof(textSelector));
        _children = childrenSelector ?? (_ => Array.Empty<TItem>());
        _icon = iconSelector;
        _mayHaveChildren = mayHaveChildren;
        _load = loadChildren;
    }

    public event Action? Changed;
    public event Action<TreeRequest<TItem>>? Requested;
    public long Revision { get { lock (_gate) return _revision; } }
    public string? SelectedKey { get { lock (_gate) return _selected; } }
    public IReadOnlySet<string> ExpandedKeys { get { lock (_gate) return _expanded.ToFrozenSet(StringComparer.Ordinal); } }
    public IReadOnlyList<DataTreeNode<TItem>> Roots { get { lock (_gate) return Array.AsReadOnly(_roots.Select(Snapshot).ToArray()); } }
    public DataTreeNode<TItem>? GetNode(string key) { lock (_gate) return _nodes.TryGetValue(key, out var n) ? Snapshot(n) : null; }

    private DataTreeNode<TItem> Snapshot(Entry n) => new(n.Key, n.Item, n.Text, n.Icon,
        _expanded.Contains(n.Key), n.Children.Count > 0 || (n.Lazy && n.State != TreeLoadState.Loaded),
        n.State, n.Error, Array.AsReadOnly(n.Children.Select(Snapshot).ToArray()));

    /// <summary>Atomically replaces the data. Surviving keys retain selection/expansion;
    /// all old loads and lazy caches are invalidated, even for equal keys.</summary>
    public void SetItems(IEnumerable<TItem> items) => ReplaceItems(items, true, null, null);

    /// <summary>Atomically replaces data and authoritative state, without briefly loading old expansion.</summary>
    public void SetItems(IEnumerable<TItem> items, string? selectedKey, IEnumerable<string>? expandedKeys)
        => ReplaceItems(items, false, selectedKey, expandedKeys);

    private void ReplaceItems(IEnumerable<TItem> items, bool preserveState, string? selectedKey, IEnumerable<string>? expandedKeys)
    {
        ArgumentNullException.ThrowIfNull(items);
        lock (_gate)
        {
            CheckAlive();
            var map = new Dictionary<string, Entry>(StringComparer.Ordinal);
            var roots = Build(items, null, map); // Validate before touching current data.
            foreach (var n in _nodes.Values) Cancel(n);
            _revision++;
            _nodes = map; _roots = roots;
            if (!preserveState)
            {
                _selected = selectedKey;
                _expanded = new HashSet<string>(expandedKeys ?? Array.Empty<string>(), StringComparer.Ordinal);
            }
            _expanded.IntersectWith(map.Keys);
            if (_selected != null && !map.ContainsKey(_selected)) _selected = null;
            Changed?.Invoke();
            LoadVisible();
        }
    }

    private List<Entry> Build(IEnumerable<TItem> items, Entry? parent, Dictionary<string, Entry> map)
    {
        var entries = new List<Entry>();
        foreach (var item in items)
        {
            string key = _key(item);
            if (key == null || map.ContainsKey(key)) throw new ArgumentException("Tree keys must be non-null and globally unique (ordinal equality).");
            var entry = new Entry(key, item, _text(item) ?? "", _icon?.Invoke(item), parent, _mayHaveChildren?.Invoke(item) == true);
            map.Add(key, entry);
            entry.Children = Build(_children(item), entry, map);
            if (entry.Children.Count > 0) entry.State = TreeLoadState.Loaded;
            entries.Add(entry);
        }
        return entries;
    }

    /// <summary>Applies authoritative parent state without user notifications. Unknown keys are dropped.</summary>
    public void Synchronize(string? selectedKey, IEnumerable<string>? expandedKeys)
    {
        lock (_gate)
        {
            CheckAlive();
            var expanded = new HashSet<string>((expandedKeys ?? Array.Empty<string>()).Where(_nodes.ContainsKey), StringComparer.Ordinal);
            var selected = selectedKey != null && _nodes.ContainsKey(selectedKey) ? selectedKey : null;
            if (_selected == selected && _expanded.SetEquals(expanded)) { LoadVisible(); return; }
            foreach (var n in _nodes.Values)
                if (!expanded.Contains(n.Key) || !AncestorsExpanded(n, expanded)) Cancel(n);
            _selected = selected;
            _expanded = expanded;
            Changed?.Invoke();
            LoadVisible();
        }
    }

    public void Select(string? key)
    {
        lock (_gate)
        {
            CheckAlive();
            if (key != null && !_nodes.ContainsKey(key)) return;
            if (_selected == key) return;
            var previous = _selected;
            _selected = key;
            Changed?.Invoke();
            // Clearing selection still carries the identity of the deselected node.
            if ((key ?? previous) is { } identity) Request(TreeRequestKind.Select, _nodes[identity]);
        }
    }

    public Task ExpandAsync(string key)
    {
        lock (_gate)
        {
            CheckAlive();
            if (!_nodes.TryGetValue(key, out var n)) return Task.CompletedTask;
            if (_expanded.Add(key)) { Changed?.Invoke(); Request(TreeRequestKind.Expand, n); }
            LoadVisible();
            return n.Pending;
        }
    }

    public void Collapse(string key)
    {
        lock (_gate)
        {
            CheckAlive();
            if (!_nodes.TryGetValue(key, out var n) || !_expanded.Contains(key)) return;
            foreach (var child in Descendants(n)) { _expanded.Remove(child.Key); Cancel(child); }
            Changed?.Invoke(); Request(TreeRequestKind.Collapse, n);
        }
    }

    public Task RetryAsync(string key)
    {
        lock (_gate)
        {
            CheckAlive();
            if (!_nodes.TryGetValue(key, out var n) || n.State != TreeLoadState.Error) return Task.CompletedTask;
            _expanded.Add(key); Request(TreeRequestKind.Retry, n);
            return StartLoad(n);
        }
    }

    public Task RefreshAsync(string key)
    {
        lock (_gate)
        {
            CheckAlive();
            if (!_nodes.TryGetValue(key, out var n) || !n.Lazy) return Task.CompletedTask;
            _revision++;
            foreach (var child in Descendants(n).Skip(1).ToArray()) { Cancel(child); _nodes.Remove(child.Key); _expanded.Remove(child.Key); }
            Cancel(n); n.Children.Clear(); n.State = TreeLoadState.Idle; n.Error = null;
            if (_selected != null && !_nodes.ContainsKey(_selected)) _selected = null;
            Changed?.Invoke();
            return _expanded.Contains(key) && AncestorsExpanded(n, _expanded) ? StartLoad(n) : Task.CompletedTask;
        }
    }

    /// <summary>Reveals a known key by expanding its ancestors. Does not search unloaded folders.</summary>
    public bool Reveal(string key)
    {
        lock (_gate)
        {
            CheckAlive();
            if (!_nodes.TryGetValue(key, out var n)) return false;
            for (var p = n.Parent; p != null; p = p.Parent) _expanded.Add(p.Key);
            Changed?.Invoke(); Request(TreeRequestKind.Reveal, n); LoadVisible();
            return true;
        }
    }

    public void Activate(string key) { lock (_gate) { CheckAlive(); if (_nodes.TryGetValue(key, out var n)) Request(TreeRequestKind.Activate, n); } }
    private void Request(TreeRequestKind kind, Entry n) => Requested?.Invoke(new(kind, n.Key, n.Item, _selected, _expanded.ToFrozenSet(StringComparer.Ordinal)));
    private static IEnumerable<Entry> Descendants(Entry n) { yield return n; foreach (var child in n.Children) foreach (var descendant in Descendants(child)) yield return descendant; }
    private static bool AncestorsExpanded(Entry n, HashSet<string> expanded) { for (var p = n.Parent; p != null; p = p.Parent) if (!expanded.Contains(p.Key)) return false; return true; }
    private void LoadVisible()
    {
        foreach (var n in _nodes.Values.ToArray())
            if (n.State == TreeLoadState.Idle && _expanded.Contains(n.Key) && AncestorsExpanded(n, _expanded)) _ = StartLoad(n);
    }

    private Task StartLoad(Entry n)
    {
        if (_load == null || !n.Lazy || n.State == TreeLoadState.Loading) return n.Pending;
        Cancel(n);
        var generation = ++n.Generation;
        var cts = n.Cancellation = new CancellationTokenSource();
        n.State = TreeLoadState.Loading; n.Error = null;
        Changed?.Invoke();
        return n.Pending = LoadAsync(n, generation, cts);
    }

    private async Task LoadAsync(Entry n, long generation, CancellationTokenSource cts)
    {
        try
        {
            IReadOnlyList<TItem>? items = null;
            Exception? error = null;
            try { items = await _load!(n.Item, cts.Token).ConfigureAwait(false); }
            catch (Exception ex) { error = ex; }
            lock (_gate)
            {
                if (!IsCurrent(n, generation, cts)) return;
                if (error == null)
                {
                    try
                    {
                        var map = new Dictionary<string, Entry>(_nodes, StringComparer.Ordinal);
                        var children = Build(items!, n, map); // Validate before publishing any children.
                        n.Children = children; _nodes = map; n.State = TreeLoadState.Loaded;
                    }
                    catch (Exception ex) { error = ex; }
                }
                n.Cancellation = null;
                if (error != null)
                {
                    n.State = error is OperationCanceledException ? TreeLoadState.Idle : TreeLoadState.Error;
                    n.Error = error is OperationCanceledException ? null : error.Message;
                }
                Changed?.Invoke();
            }
        }
        finally { cts.Dispose(); }
    }

    private bool IsCurrent(Entry n, long generation, CancellationTokenSource cts) => !_disposed &&
        !cts.IsCancellationRequested && generation == n.Generation && _nodes.TryGetValue(n.Key, out var current) &&
        ReferenceEquals(current, n) && _expanded.Contains(n.Key) && AncestorsExpanded(n, _expanded);
    private static void Cancel(Entry n)
    {
        n.Generation++;
        var cancellation = n.Cancellation; n.Cancellation = null;
        if (n.State == TreeLoadState.Loading) n.State = TreeLoadState.Idle;
        cancellation?.Cancel(); // The completing load owns CTS disposal.
    }
    private void CheckAlive() => ObjectDisposedException.ThrowIf(_disposed, this);
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var n in _nodes.Values) Cancel(n);
            _nodes.Clear(); _roots.Clear(); _expanded.Clear(); _selected = null;
            Changed = null; Requested = null;
        }
    }
}
