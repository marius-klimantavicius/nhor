using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Marius.Winter;

/// <summary>Native presentation of a typed tree controller. A supplied controller is caller-owned
/// unless ownsController is true. Detached views stop observing asynchronous changes.</summary>
public class DataTree<TItem> : TreeView
{
    private TreeController<TItem>? _controller;
    private bool _ownsController, _synchronizing, _observing;
    private readonly Dictionary<string, TreeNode> _rows = new(StringComparer.Ordinal);
    private sealed record Status(string Key, bool Retry);
    public event Action<TreeRequest<TItem>>? Requested;

    public DataTree()
    {
        SelectionChanged = node => { if (!_synchronizing && node?.Tag is string key) _controller?.Select(key); };
        NodeExpanded = node => { if (!_synchronizing && node.Tag is string key) _ = _controller?.ExpandAsync(key); };
        NodeCollapsed = node => { if (!_synchronizing && node.Tag is string key) _controller?.Collapse(key); };
        NodeDoubleClicked = ActivateRow;
        NodeActivated = ActivateRow;
    }

    public DataTree(TreeController<TItem> controller, bool ownsController = false) : this() => SetController(controller, ownsController);
    public TreeController<TItem>? Controller => _controller;
    public string? SelectedKey { get => _controller?.SelectedKey; set => _controller?.Synchronize(value, _controller.ExpandedKeys); }
    public IReadOnlySet<string> ExpandedKeys => _controller?.ExpandedKeys ?? new HashSet<string>();

    public void SetController(TreeController<TItem> controller, bool ownsController = false)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(controller);
        if (ReferenceEquals(_controller, controller)) return;
        StopObserving();
        if (_ownsController) _controller?.Dispose();
        _controller = controller; _ownsController = ownsController;
        Observe(); SynchronizeRows();
    }

    public void Synchronize(string? selectedKey, IEnumerable<string>? expandedKeys) => _controller?.Synchronize(selectedKey, expandedKeys);
    public Task ExpandAsync(string key) => _controller?.ExpandAsync(key) ?? Task.CompletedTask;
    public void Collapse(string key) => _controller?.Collapse(key);
    public Task RetryAsync(string key) => _controller?.RetryAsync(key) ?? Task.CompletedTask;
    public Task RefreshAsync(string key) => _controller?.RefreshAsync(key) ?? Task.CompletedTask;
    public bool Reveal(string key)
    {
        if (_controller?.Reveal(key) != true) return false;
        if (_rows.TryGetValue(key, out var row)) EnsureVisible(row);
        return true;
    }

    private void ActivateRow(TreeNode row)
    {
        if (_synchronizing) return;
        if (row.Tag is Status { Retry: true } status) _ = RetryAsync(status.Key);
        else if (row.Tag is string key)
        {
            if (_controller?.GetNode(key)?.LoadState == TreeLoadState.Error) _ = RetryAsync(key);
            else _controller?.Activate(key);
        }
    }

    public override void OnDoubleClick()
    {
        if (SelectedNode is { } row && (row.Tag is Status || row.Tag is string key && _controller?.GetNode(key)?.LoadState == TreeLoadState.Error)) ActivateRow(row);
        else base.OnDoubleClick();
    }

    private void Observe()
    {
        if (_observing || _controller == null) return;
        _observing = true;
        _controller.Changed += Changed;
        _controller.Requested += ForwardRequest;
    }
    private void StopObserving()
    {
        if (!_observing || _controller == null) return;
        _observing = false;
        _controller.Changed -= Changed;
        _controller.Requested -= ForwardRequest;
    }
    private void ForwardRequest(TreeRequest<TItem> request)
    {
        if (request.Kind == TreeRequestKind.Reveal && _rows.TryGetValue(request.Key, out var row)) EnsureVisible(row);
        Requested?.Invoke(request);
    }
    private void Changed()
    {
        var controller = _controller;
        var window = OwnerWindow;
        if (window != null && !window.Dispatcher.CheckAccess())
        {
            _ = window.Dispatcher.InvokeAsync(() =>
            {
                if (!IsDisposed && _observing && ReferenceEquals(controller, _controller) && ReferenceEquals(window, OwnerWindow)) SynchronizeRows();
            });
        }
        else if (!IsDisposed && _observing) SynchronizeRows();
    }

    private void SynchronizeRows()
    {
        if (_controller == null || _synchronizing) return;
        _synchronizing = true;
        BeginUpdate();
        try
        {
            var retained = new HashSet<string>(StringComparer.Ordinal);
            List<TreeNode> Build(IReadOnlyList<DataTreeNode<TItem>> items, TreeNode? parent)
            {
                var result = new List<TreeNode>();
                foreach (var item in items)
                {
                    retained.Add(item.Key);
                    if (!_rows.TryGetValue(item.Key, out var row)) _rows[item.Key] = row = new TreeNode(item.Text);
                    row.Text = item.Text; row.IconSvg = item.Icon; row.Tag = item.Key; row.Parent = parent;
                    row.IsExpanded = item.IsExpanded; row.MayHaveChildren = item.MayHaveChildren;
                    row.Children.Clear(); row.Children.AddRange(Build(item.Children, row));
                    if (item.LoadState == TreeLoadState.Loading)
                        row.Children.Add(new TreeNode("Loading…") { Parent = row, Tag = new Status(item.Key, false) });
                    else if (item.LoadState == TreeLoadState.Error)
                        row.Children.Add(new TreeNode($"{item.Error} — Retry: Enter or double-click") { Parent = row, Tag = new Status(item.Key, true) });
                    else if (!item.MayHaveChildren && item.IsExpanded)
                        row.Children.Add(new TreeNode("Empty folder") { Parent = row, Tag = new Status(item.Key, false) });
                    result.Add(row);
                }
                return result;
            }
            Nodes.Clear(); Nodes.AddRange(Build(_controller.Roots, null));
            foreach (var key in _rows.Keys.Where(key => !retained.Contains(key)).ToArray()) _rows.Remove(key);
            SelectedNode = _controller.SelectedKey is { } selected && _rows.TryGetValue(selected, out var selectedRow) ? selectedRow : null;
        }
        finally { EndUpdate(); _synchronizing = false; }
        InvalidateMeasure(); MarkDirty();
    }

    protected override void OnAttached() { Observe(); SynchronizeRows(); base.OnAttached(); }
    protected override void OnDetaching() { StopObserving(); base.OnDetaching(); }
    protected override void OnDisposed()
    {
        StopObserving();
        if (_ownsController) _controller?.Dispose();
        _controller = null; _rows.Clear(); Requested = null;
        SelectionChanged = null; NodeExpanded = null; NodeCollapsed = null; NodeDoubleClicked = null; NodeActivated = null;
        base.OnDisposed();
    }
}
