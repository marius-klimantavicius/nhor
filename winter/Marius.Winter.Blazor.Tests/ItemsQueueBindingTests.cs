using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Asset = Marius.Winter.Blazor.Tests.ItemsQueueBindingProbe.Asset;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native rendering")]
public class ItemsQueueBindingTests
{
    private static void Drain(Window window) { while (window.DispatcherQueue.TryDequeue(out var action)) action(); }
    private static ListView<Asset> List(Panel root) => root.Children[0] is BasicTable<Asset> table ? table.Body : (ListView<Asset>)root.Children[0];

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public async Task ChangedParentSelectionSupersedesQueuedUserInput(bool tableMode, bool defaultBinding)
    {
        using var renderer = new TestRenderer(); using var window = new Window(400, 280, "C12 authoritative selection");
        var root = new Panel(); window.AddChild(root);
        var probe = await renderer.AddComponent<ItemsQueueBindingProbe>(TestRenderer.CreateRootHandler(renderer, root),
            new() { ["TableMode"] = tableMode, ["DefaultBinding"] = defaultBinding });
        var list = List(root);
        await renderer.Dispatcher.InvokeAsync(() => list.Controller.Select("b", true));
        Assert.Equal("a", probe.Selected);
        await renderer.Dispatcher.InvokeAsync(() => probe.ParentSelect("c"));
        Assert.Equal("c", list.SelectedKey);
        await renderer.Dispatcher.InvokeAsync(() => Drain(window));
        Assert.Empty(probe.Selections); Assert.Equal("c", probe.Selected); Assert.Equal("c", list.SelectedKey);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ChangedParentSortSupersedesQueuedUserSort(bool defaultBinding)
    {
        using var renderer = new TestRenderer(); using var window = new Window(400, 280, "C12 authoritative sort");
        var root = new Panel(); window.AddChild(root);
        var probe = await renderer.AddComponent<ItemsQueueBindingProbe>(TestRenderer.CreateRootHandler(renderer, root),
            new() { ["TableMode"] = true, ["DefaultBinding"] = defaultBinding });
        var table = (BasicTable<Asset>)root.Children[0];
        await renderer.Dispatcher.InvokeAsync(() => table.ToggleSort("name"));
        var forced = new TableSort("name", SortDirection.Descending);
        await renderer.Dispatcher.InvokeAsync(() => probe.ParentSort(forced));
        Assert.Equal(forced, table.Sort);
        await renderer.Dispatcher.InvokeAsync(() => Drain(window));
        Assert.Empty(probe.Sorts); Assert.Equal(forced, probe.Sort); Assert.Equal(forced, table.Sort);
        Assert.Equal(new[] { "c", "b", "a" }, table.Controller.Items.Select(i => i.Key));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public async Task DiscreteSortFeedbackKeepsEverySnapshotAndFinalOrder(bool sortedSource, bool defaultBinding)
    {
        using var renderer = new TestRenderer(); using var window = new Window(400, 280, "C12 ordered sort feedback");
        var root = new Panel(); window.AddChild(root);
        var probe = await renderer.AddComponent<ItemsQueueBindingProbe>(TestRenderer.CreateRootHandler(renderer, root),
            new() { ["TableMode"] = true, ["SortedSource"] = sortedSource, ["DefaultBinding"] = defaultBinding });
        var table = (BasicTable<Asset>)root.Children[0]; var row = table.Body.FindRow("b");
        await renderer.Dispatcher.InvokeAsync(() => { table.ToggleSort("name"); table.ToggleSort("name"); table.ToggleSort("name"); });
        await renderer.Dispatcher.InvokeAsync(probe.RenderAgain);
        await renderer.Dispatcher.InvokeAsync(() => Drain(window));
        Assert.Equal(new[] { SortDirection.Ascending, SortDirection.Descending, SortDirection.Ascending }, probe.Sorts.Select(s => s!.Direction));
        Assert.Equal(new TableSort("name", SortDirection.Ascending), probe.Sort); Assert.Equal(probe.Sort, table.Sort);
        Assert.Equal(new[] { "a", "b", "c" }, table.Controller.Items.Select(i => i.Key)); Assert.Same(row, table.Body.FindRow("b"));
    }

    [Theory]
    [InlineData("item")] [InlineData("source")] [InlineData("selector")] [InlineData("reset")] [InlineData("unmount")]
    public async Task GenuineReplacementCancelsQueuedSortSelectionAndActivation(string reason)
    {
        using var renderer = new TestRenderer(); using var window = new Window(400, 280, "C12 obsolete actions");
        var root = new Panel(); window.AddChild(root);
        var probe = await renderer.AddComponent<ItemsQueueBindingProbe>(TestRenderer.CreateRootHandler(renderer, root), new() { ["TableMode"] = true, ["SortedSource"] = true });
        var table = (BasicTable<Asset>)root.Children[0];
        await renderer.Dispatcher.InvokeAsync(() => { table.Controller.Activate("b"); table.ToggleSort("name"); table.ToggleSort("name"); });
        await renderer.Dispatcher.InvokeAsync(() => probe.Invalidate(reason));
        await renderer.Dispatcher.InvokeAsync(() => Drain(window));
        Assert.Empty(probe.Selections); Assert.Empty(probe.Activations); Assert.Empty(probe.Sorts);
        Assert.Equal("a", probe.Selected); Assert.Null(probe.Sort);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ChangedParentScalarPreservesIndependentQueuedActions(bool changeSort)
    {
        using var renderer = new TestRenderer(); using var window = new Window(400, 280, "C12 independent actions");
        var root = new Panel(); window.AddChild(root);
        var probe = await renderer.AddComponent<ItemsQueueBindingProbe>(TestRenderer.CreateRootHandler(renderer, root), new() { ["TableMode"] = true, ["SortedSource"] = true });
        var table = (BasicTable<Asset>)root.Children[0];
        await renderer.Dispatcher.InvokeAsync(() => { table.Controller.Activate("b"); table.ToggleSort("name"); table.ToggleSort("name"); });
        var forced = new TableSort("name", SortDirection.Descending);
        await renderer.Dispatcher.InvokeAsync(() => { if (changeSort) probe.ParentSort(forced); else probe.ParentSelect("c"); });
        await renderer.Dispatcher.InvokeAsync(() => Drain(window));
        Assert.Equal("b", Assert.Single(probe.Activations).Key);
        if (changeSort)
        {
            Assert.Equal("b", Assert.Single(probe.Selections)); Assert.Empty(probe.Sorts);
            Assert.Equal(forced, table.Sort); Assert.Equal("b", table.SelectedKey);
        }
        else
        {
            Assert.Empty(probe.Selections); Assert.Equal("c", table.SelectedKey);
            Assert.Equal(new[] { SortDirection.Ascending, SortDirection.Descending }, probe.Sorts.Select(s => s!.Direction));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PureSourceReorderPreservesSelectionAndActivationSnapshots(bool tableMode)
    {
        using var renderer = new TestRenderer(); using var window = new Window(400, 280, "C12 keyed order feedback");
        var root = new Panel(); window.AddChild(root);
        var probe = await renderer.AddComponent<ItemsQueueBindingProbe>(TestRenderer.CreateRootHandler(renderer, root), new() { ["TableMode"] = tableMode });
        var list = List(root); var row = list.FindRow("b");
        await renderer.Dispatcher.InvokeAsync(() => { list.Controller.Activate("b"); list.Controller.Activate("c"); list.Controller.Activate("a"); });
        await renderer.Dispatcher.InvokeAsync(probe.Reorder);
        await renderer.Dispatcher.InvokeAsync(() => Drain(window));
        Assert.Equal(new[] { "b", "c", "a" }, probe.Activations.Select(a => a.Key));
        Assert.Equal(new[] { "b", "c", "a" }, probe.Selections); Assert.Equal("a", list.SelectedKey);
        Assert.Same(row, list.FindRow("b")); Assert.Equal(new[] { "b", "a", "c" }, list.Controller.Items.Select(i => i.Key));
    }

    [Fact]
    public async Task AuthoritativeSelectionInOneRendererDoesNotCancelAnotherRenderersPendingFeedback()
    {
        using var firstRenderer = new TestRenderer(); using var secondRenderer = new TestRenderer();
        using var window = new Window(400, 280, "C12 renderer ownership");
        var firstRoot = new Panel(); var secondRoot = new Panel(); window.AddChild(firstRoot); window.AddChild(secondRoot);
        var first = await firstRenderer.AddComponent<ItemsQueueBindingProbe>(TestRenderer.CreateRootHandler(firstRenderer, firstRoot));
        var second = await secondRenderer.AddComponent<ItemsQueueBindingProbe>(TestRenderer.CreateRootHandler(secondRenderer, secondRoot));
        await firstRenderer.Dispatcher.InvokeAsync(() => List(firstRoot).Controller.Select("b", true));
        await secondRenderer.Dispatcher.InvokeAsync(() => List(secondRoot).Controller.Select("b", true));
        await firstRenderer.Dispatcher.InvokeAsync(() => first.ParentSelect("c"));
        await secondRenderer.Dispatcher.InvokeAsync(second.RenderAgain);
        Drain(window);
        Assert.Empty(first.Selections); Assert.Equal("c", List(firstRoot).SelectedKey);
        Assert.Equal("b", Assert.Single(second.Selections)); Assert.Equal("b", second.Selected); Assert.Equal("b", List(secondRoot).SelectedKey);
    }
}
