using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Marius.Winter.Playground.Scenarios;

/// <summary>Bounded deterministic project data; no filesystem access.</summary>
public sealed class ProjectBrowserScenario : IDisposable
{
    private sealed record Item(string Key, string Name, Item[]? Children = null, bool Lazy = false);
    private const string FolderIcon = "<svg xmlns='http://www.w3.org/2000/svg' width='16' height='16' viewBox='0 0 16 16'><path d='M1 3h5l2 2h7v9H1z' fill='#d9a34a'/></svg>";
    private const string FileIcon = "<svg xmlns='http://www.w3.org/2000/svg' width='16' height='16' viewBox='0 0 16 16'><path d='M3 1h7l3 3v11H3z' fill='#718eb9'/></svg>";
    private readonly Window _window;
    private readonly Panel _root;
    private readonly TreeController<Item> _controller;
    private readonly DataTree<Item> _tree;
    private readonly Label _selection = new(), _status = new(), _result = new("Ready. Reveal the selected note or open a folder.");
    private bool _renamed, _reordered, _large, _disposed;
    private int _attempts, _cancelled, _generation;

    public ProjectBrowserScenario(Window window)
    {
        _window = window;
        _controller = new(i => i.Key, i => i.Name, i => i.Children ?? [], iconSelector: i => i.Lazy || i.Children != null ? FolderIcon : FileIcon, mayHaveChildren: i => i.Lazy, loadChildren: Load);
        _tree = new(_controller) { LayoutData = new FlexItem { Grow = 1, Basis = 0 } };
        _root = new Panel { Layout = new FlexLayout { Direction = Orientation.Vertical, Gap = 8 }, Padding = new Thickness(8) };
        _root.AddChild(new Label("Project browser") { FontSize = 22 });
        _root.AddChild(new Label("Open src, empty, or packages. Packages fails once: select its message and press Enter or double-click to retry.") { TextWrapping = ThorVG.TextWrap.Word });
        var actions = new Panel { Layout = new FlexLayout { Wrap = FlexWrap.Wrap, Gap = 6 }, Padding = new Thickness(0) };
        actions.AddChild(Action("Reveal selected", () => { if (_controller.SelectedKey is { } key) { _tree.Reveal(key); _window.SetFocus(_tree); } }));
        actions.AddChild(Action("Rename", () => { _renamed = !_renamed; Replace("Renamed the selected note; its key is unchanged."); }));
        actions.AddChild(Action("Reorder", () => { _reordered = !_reordered; Replace("Reordered the project; selection follows its key."); }));
        actions.AddChild(Action("Refresh src", () => { _ = _tree.RefreshAsync("src"); _result.Text = "Refreshed src. Open it to load again."; }));
        actions.AddChild(Action("Reset", Reset));
        actions.AddChild(Action("Light / dark", () => _window.Theme = ReferenceEquals(_window.Theme, Theme.Dark) ? Theme.Light : Theme.Dark));
        actions.AddChild(Action("Larger text", () => { _large = !_large; _tree.FontSize = _large ? 26 : 14; }));
        _root.AddChild(actions); _root.AddChild(_selection); _root.AddChild(_tree); _root.AddChild(_status); _root.AddChild(_result);
        _controller.Changed += Changed;
        _controller.Requested += Requested;
        _window.Closing += Closing;
        Reset();
    }

    public Element Content => _root;
    private static Button Action(string text, Action action) => new(text) { Clicked = action };
    private Item[] Items()
    {
        var notes = new[] { new Item("note-a", "notes.md"), new Item("note-b", _renamed ? "Review 👩🏽‍💻é.md" : "notes.md") }
            .Concat(Enumerable.Range(1, 32).Select(i => new Item("sample-" + i, $"Sample {i:00} — international production notes.md"))).ToArray();
        if (_reordered) Array.Reverse(notes);
        Item[] children = [new("readme", "README.md"), new("src", "src", Lazy: true), new("empty", "empty", Lazy: true), new("retry", "packages", Lazy: true), new("archive", "Archive", notes)];
        if (_reordered) Array.Reverse(children);
        return [new("project", "Winter project", children)];
    }
    private async Task<IReadOnlyList<Item>> Load(Item item, CancellationToken cancellation)
    {
        int generation = Volatile.Read(ref _generation);
        try
        {
            await Task.Delay(1200, cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            return await _window.Dispatcher.InvokeAsync<IReadOnlyList<Item>>(() =>
            {
                if (_disposed || generation != _generation) throw new OperationCanceledException(cancellation);
                if (item.Key == "retry" && ++_attempts == 1) throw new InvalidOperationException("Packages temporarily unavailable");
                return item.Key == "empty" ? [] : [new(item.Key + "/same-a", "index.cs"), new(item.Key + "/same-b", "index.cs"), new(item.Key + "/unicode", "Žąsis 👩🏽‍💻é.cs")];
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await _window.Dispatcher.InvokeAsync(() =>
            {
                if (!_disposed && generation == _generation) { _cancelled++; UpdateStatus(); }
            }).ConfigureAwait(false);
            throw;
        }
    }
    private void Requested(TreeRequest<Item> request) { _result.Text = $"{request.Kind}: {request.Item.Name} [{request.Key}]"; UpdateStatus(); }
    private void Changed()
    {
        _ = _window.Dispatcher.InvokeAsync(() => { if (!_disposed) UpdateStatus(); });
    }
    private void UpdateStatus()
    {
        static IEnumerable<DataTreeNode<Item>> Walk(IReadOnlyList<DataTreeNode<Item>> nodes) => nodes.SelectMany(n => new[] { n }.Concat(Walk(n.Children)));
        var nodes = Walk(_controller.Roots).ToArray();
        var selected = _controller.SelectedKey is { } key ? _controller.GetNode(key) : null;
        _selection.Text = selected == null ? "Selected: none" : $"Selected: {selected.Text} [{selected.Key}]";
        _status.Text = $"Loading: {nodes.Count(n => n.LoadState == TreeLoadState.Loading)} · Errors: {nodes.Count(n => n.LoadState == TreeLoadState.Error)} · Cancelled loads: {_cancelled}";
    }
    private void Replace(string result) { _controller.SetItems(Items()); _result.Text = result; }
    private void Reset()
    {
        Interlocked.Increment(ref _generation);
        _renamed = _reordered = _large = false; _attempts = _cancelled = 0; _tree.FontSize = 14;
        _controller.SetItems(Items(), "note-b", new HashSet<string> { "project" });
        _result.Text = "Ready. Reveal the selected note or open a folder."; UpdateStatus();
    }
    private ValueTask Closing() { Dispose(); return ValueTask.CompletedTask; }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; Interlocked.Increment(ref _generation); _window.Closing -= Closing;
        _controller.Changed -= Changed; _controller.Requested -= Requested;
        _controller.Dispose(); _root.Dispose();
    }
}
