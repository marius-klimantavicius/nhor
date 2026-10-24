using System;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using Xunit;

namespace Marius.Winter.Tests;

public class TypedTreeTests
{
    private sealed record Item(string Key, string Name, Item[]? Children = null, bool Lazy = false);
    private static TreeController<Item> Controller(Func<Item, CancellationToken, Task<IReadOnlyList<Item>>>? load = null)
        => new(i => i.Key, i => i.Name, i => i.Children ?? [], mayHaveChildren: i => i.Lazy, loadChildren: load);

    [Fact]
    public void TreeParticipatesInTabTraversal()
    {
        using var tree = new TreeView();
        Assert.True(tree.Focusable);
    }

    [Fact]
    public void KeysSurviveRenameReorderAndParentReplacementAndRemovalClearsSelection()
    {
        using var c = Controller();
        c.SetItems([new("p", "Parent", [new("a", "same"), new("b", "same")])]);
        c.Synchronize("b", new HashSet<string> { "p" });
        c.SetItems([new("q", "New parent", [new("b", "renamed 👩🏽‍💻é"), new("a", "same")])]);
        Assert.Equal("b", c.SelectedKey);
        Assert.Equal("renamed 👩🏽‍💻é", c.GetNode("b")!.Text);
        Assert.Empty(c.ExpandedKeys);
        Assert.Throws<ArgumentException>(() => c.SetItems([new("a", "one"), new("a", "two")]));
        Assert.Equal("b", c.SelectedKey); // Duplicate replacement is atomic.
        c.SetItems([new("a", "same")]);
        Assert.Null(c.SelectedKey);
    }

    [Fact]
    public async Task CollapseRefreshReplacementRemovalAndDisposalRejectStaleCompletions()
    {
        var loads = new List<(TaskCompletionSource<IReadOnlyList<Item>> Result, CancellationToken Token)>();
        var c = Controller((_, token) => { var t = new TaskCompletionSource<IReadOnlyList<Item>>(TaskCreationOptions.RunContinuationsAsynchronously); loads.Add((t, token)); return t.Task; });
        c.SetItems([new("p", "Folder", Lazy: true)]);
        var old = c.ExpandAsync("p");
        Assert.Equal(TreeLoadState.Loading, c.GetNode("p")!.LoadState);
        c.Collapse("p"); Assert.True(loads[0].Token.IsCancellationRequested);
        var current = c.ExpandAsync("p");
        loads[1].Result.SetResult([new("new", "new")]); await current;
        loads[0].Result.SetResult([new("old", "old")]); await old;
        Assert.NotNull(c.GetNode("new")); Assert.Null(c.GetNode("old"));
        var refresh = c.RefreshAsync("p");
        c.SetItems([new("p", "Replacement", Lazy: true)]);
        Assert.True(loads[2].Token.IsCancellationRequested);
        loads[2].Result.SetResult([new("late", "late")]); await refresh;
        Assert.Null(c.GetNode("late"));
        var removed = c.ExpandAsync("p"); c.SetItems([]);
        Assert.True(loads[3].Token.IsCancellationRequested);
        loads[3].Result.SetResult([new("removed", "removed")]); await removed;
        Assert.Empty(c.Roots);
        c.SetItems([new("p", "Folder", Lazy: true)]);
        var disposed = c.ExpandAsync("p"); c.Dispose();
        Assert.True(loads[4].Token.IsCancellationRequested);
        loads[4].Result.SetResult([new("disposed", "disposed")]); await disposed;
        Assert.Null(c.GetNode("disposed"));
    }

    [Fact]
    public async Task FailureRetryEmptyCachingAndRevealHaveDiscreteKeys()
    {
        var requests = new List<TreeRequest<Item>>(); int calls = 0;
        using var c = Controller((_, _) => ++calls == 1 ? Task.FromException<IReadOnlyList<Item>>(new InvalidOperationException("Folder temporarily unavailable")) : Task.FromResult<IReadOnlyList<Item>>([]));
        c.Requested += requests.Add;
        c.SetItems([new("p", "Folder", Lazy: true), new("q", "Parent", [new("leaf", "same")])]);
        await c.ExpandAsync("p");
        Assert.Equal(TreeLoadState.Error, c.GetNode("p")!.LoadState);
        Assert.Contains("temporarily", c.GetNode("p")!.Error);
        await c.RetryAsync("p");
        Assert.Equal(TreeLoadState.Loaded, c.GetNode("p")!.LoadState);
        Assert.False(c.GetNode("p")!.MayHaveChildren);
        c.Collapse("p"); await c.ExpandAsync("p"); Assert.Equal(2, calls);
        c.Select("leaf"); c.Reveal("leaf"); c.Activate("leaf");
        Assert.Contains("q", c.ExpandedKeys);
        Assert.Equal(new[] { ("p", TreeRequestKind.Expand), ("p", TreeRequestKind.Retry), ("p", TreeRequestKind.Collapse), ("p", TreeRequestKind.Expand), ("leaf", TreeRequestKind.Select), ("leaf", TreeRequestKind.Reveal), ("leaf", TreeRequestKind.Activate) }, requests.Select(r => (r.Key, r.Kind)));
        var count = requests.Count;
        c.Synchronize(null, new HashSet<string>()); Assert.Null(c.SelectedKey); Assert.Empty(c.ExpandedKeys);
        Assert.Equal(count, requests.Count);
    }

    [Fact]
    public async Task CollapseAncestorCancelsDescendantLoadAndInvalidLazyDataIsAnOrdinaryRetryableError()
    {
        var source = new TaskCompletionSource<IReadOnlyList<Item>>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        using var c = Controller((_, t) => { token = t; return source.Task; });
        c.SetItems([new("root", "Root", [new("p", "Folder", Lazy: true)])]);
        await c.ExpandAsync("root"); var load = c.ExpandAsync("p"); c.Collapse("root");
        Assert.True(token.IsCancellationRequested);
        source.SetResult([new("late", "late")]); await load; Assert.Null(c.GetNode("late"));
    }

    [Fact]
    public async Task DuplicateLazyKeysAreRetryableAndDoNotPartiallyPopulateData()
    {
        int calls = 0;
        using var c = Controller((_, _) => Task.FromResult<IReadOnlyList<Item>>(++calls == 1 ? [new("a", "one"), new("a", "two")] : [new("b", "valid")]));
        c.SetItems([new("p", "Folder", Lazy: true)]);
        await c.ExpandAsync("p");
        Assert.Equal(TreeLoadState.Error, c.GetNode("p")!.LoadState);
        Assert.Null(c.GetNode("a")); Assert.Empty(c.GetNode("p")!.Children);
        await c.RetryAsync("p"); Assert.Equal("valid", c.GetNode("b")!.Text);
    }

    [Fact]
    public async Task RefreshInvalidatesAnEarlierRefreshEvenWhenLoaderIgnoresCancellation()
    {
        var loads = new List<TaskCompletionSource<IReadOnlyList<Item>>>();
        using var c = Controller((_, _) => { var t = new TaskCompletionSource<IReadOnlyList<Item>>(TaskCreationOptions.RunContinuationsAsynchronously); loads.Add(t); return t.Task; });
        c.SetItems([new("p", "Folder", Lazy: true)]);
        var first = c.ExpandAsync("p");
        var second = c.RefreshAsync("p");
        loads[1].SetResult([new("new", "new")]); await second;
        loads[0].SetException(new InvalidOperationException("stale error")); await first;
        Assert.Equal(TreeLoadState.Loaded, c.GetNode("p")!.LoadState);
        Assert.NotNull(c.GetNode("new")); Assert.Null(c.GetNode("p")!.Error);
    }

    [Fact]
    public async Task NativeFacadeMarshalsCompletionAndDetachedCallerControllerSurvives()
    {
        var result = new TaskCompletionSource<IReadOnlyList<Item>>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var c = Controller((_, _) => result.Task);
        c.SetItems([new("p", "Folder", Lazy: true)]);
        DataTree<Item> view = null!; Task loading = Task.CompletedTask;
        using var pump = new NativePump(w =>
        {
            var root = new Panel { Layout = new FlexLayout() };
            view = new(c) { LayoutData = new FlexItem { Grow = 1 } };
            root.AddChild(view); w.AddChild(root); w.SetAutoLayout(root);
        });
        await pump.Call(_ => loading = view.ExpandAsync("p"));
        result.SetResult([new("child", "loaded")]); await loading;
        await pump.Call(_ =>
        {
            Assert.Equal("loaded", view.Nodes[0].Children[0].Text);
            var paints = Paints(view.Scene).ToArray();
            view.Dispose();
            Assert.All(paints, p => Assert.Equal((ushort)0, p.RefCnt()));
            c.SetItems([new("other", "caller still alive")]);
            Assert.Null(view.Controller); Assert.True(view.IsDisposed);
            Assert.Equal("caller still alive", c.Roots[0].Text);
        });
    }
    private static IEnumerable<ThorVG.Paint> Paints(ThorVG.Paint paint)
    {
        yield return paint;
        if (paint.GetClipper() is { } clip) yield return clip;
        if (paint is ThorVG.Scene scene) foreach (var child in scene.Paints()) foreach (var p in Paints(child)) yield return p;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScenarioCloseCancelsPendingLoadAndReleasesOwnedContent(bool blazor)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(900, 720, "Project browser shutdown");
            Element content;
            if (blazor)
            {
                var host = Marius.Winter.Blazor.WindowExtensions.UseBlazor(window);
                var component = host.AddComponent<Marius.Winter.Blazor.Playground.Scenarios.ProjectBrowser>();
                Assert.True(component.IsCompletedSuccessfully);
                content = window.Children[0];
            }
            else
            {
                var scenario = new Playground.Scenarios.ProjectBrowserScenario(window);
                content = scenario.Content; window.AddChild(content); window.SetAutoLayout(content);
            }
            WindowRegressionOwner.Flush(window);
            var tree = FindTree(content);
            tree.SelectedNode = tree.Nodes[0].Children.Single(n => Equals(n.Tag, "src"));
            tree.OnKeyDown(262, 0, false);
            Assert.Equal("Loading…", tree.Nodes[0].Children.Single(n => Equals(n.Tag, "src")).Children[0].Text);
            window.Dispose();
            Assert.True(content.IsDisposed); Assert.True(tree.IsDisposed);
        });
    }
    private static TreeView FindTree(Element element) => element is TreeView t ? t : element.Children.Select(FindTreeOrNull).First(t => t != null)!;
    private static TreeView? FindTreeOrNull(Element element) => element as TreeView ?? element.Children.Select(FindTreeOrNull).FirstOrDefault(t => t != null);

    [Fact]
    public void RazorScenarioRevealAcceptsExpansionAfterExplicitReset()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(900, 720, "Project browser reveal");
            var host = Marius.Winter.Blazor.WindowExtensions.UseBlazor(window);
            Assert.True(host.AddComponent<Marius.Winter.Blazor.Playground.Scenarios.ProjectBrowser>().IsCompletedSuccessfully);
            WindowRegressionOwner.Flush(window);
            static IEnumerable<Element> Walk(Element e) => new[] { e }.Concat(e.Children.SelectMany(Walk));
            void Drain() { int i = 0; while (window.DispatcherQueue.TryDequeue(out var work)) { Assert.True(++i < 100, "Unexpected render loop"); work(); } }
            var buttons = Walk(window).OfType<Button>().ToDictionary(b => b.Text);
            buttons["Reset"].OnClick(); Drain();
            buttons["Reveal selected"].OnClick(); Drain();
            var tree = FindTree(window);
            Assert.True(tree.Nodes[0].Children.Single(n => Equals(n.Tag, "archive")).IsExpanded);
            Assert.Equal("note-b", tree.SelectedNode?.Tag);
        });
    }

    [Fact]
    public void PointerSelectionAndExpansionStartOneLoadAcrossQueuedBindingNotifications()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(500, 320, "Tree pointer binding ordering");
            var host = Marius.Winter.Blazor.WindowExtensions.UseBlazor(window);
            var result = new TaskCompletionSource<IReadOnlyList<Item>>(TaskCreationOptions.RunContinuationsAsynchronously);
            int loads = 0; CancellationToken token = default;
            var component = host.AddComponent<Marius.Winter.Blazor.Elements.DataTree<Item>>(new()
            {
                ["Items"] = new Item[] { new("p", "Folder", Lazy: true) },
                ["KeySelector"] = (Func<Item, string>)(i => i.Key), ["TextSelector"] = (Func<Item, string>)(i => i.Name),
                ["MayHaveChildren"] = (Func<Item, bool>)(i => i.Lazy),
                ["LoadChildren"] = (Func<Item, CancellationToken, Task<IReadOnlyList<Item>>>)((_, t) => { loads++; token = t; return result.Task; })
            });
            Assert.True(component.IsCompletedSuccessfully); WindowRegressionOwner.Flush(window);
            var tree = Assert.IsType<DataTree<Item>>(window.Children[0]);
            tree.OnMouseDown(0, tree.Bounds.X + 8, tree.Bounds.Y + 12);
            Assert.Equal(1, loads);
            int actions = 0;
            while (window.DispatcherQueue.TryDequeue(out var action)) { Assert.True(++actions < 100); action(); }
            Assert.Equal(1, loads); Assert.False(token.IsCancellationRequested);
            result.SetResult([]);
        });
    }
}
