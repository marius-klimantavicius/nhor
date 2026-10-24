using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Xunit;
using B = Marius.Winter.Blazor.Elements;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native rendering")]
public class ItemsBindingTests
{
    private static IEnumerable<Element> Walk(Element e) { yield return e; foreach (var c in e.Children) foreach (var item in Walk(c)) yield return item; }
    [Fact]
    public async Task ActualCompiledDefaultAndGetSetBindingsKeepQueuedInputAcrossUnrelatedParentRender()
    {
        using var renderer = new TestRenderer(); using var window = new Window(400, 280, "C12 compiled queued bindings");
        var root = new Panel(); window.AddChild(root);
        var simple = await renderer.AddComponent<ItemsDefaultBindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var list = (ListView<string>)root.Children[0]; var table = (BasicTable<string>)root.Children[1];
        void Drain() { while (window.DispatcherQueue.TryDequeue(out var action)) action(); }
        await renderer.Dispatcher.InvokeAsync(() => { list.Controller.Select("b", true); table.Controller.Select("c", true); table.ToggleSort("name"); });
        Assert.Equal("a", simple.Selected); Assert.Null(simple.Sort);
        await renderer.Dispatcher.InvokeAsync(simple.RenderAgain);
        await renderer.Dispatcher.InvokeAsync(Drain);
        Assert.Equal("b", simple.Selected); Assert.Equal("c", simple.TableSelected); Assert.Equal(new TableSort("name", SortDirection.Ascending), simple.Sort);
        Assert.Equal(new[] { "a", "b", "c" }, table.Controller.Items.Select(i => i.Key));
        var probe = await renderer.AddComponent<ItemsBindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var getset = root.Children.OfType<ListView<ItemsBindingProbe.Asset>>().Single(); var getsetTable = root.Children.OfType<BasicTable<ItemsBindingProbe.Asset>>().Single();
        await renderer.Dispatcher.InvokeAsync(() => { getset.Controller.Select("c", true); getset.Controller.Activate("a"); getset.Controller.Activate("c"); getsetTable.ToggleSort("size"); });
        await renderer.Dispatcher.InvokeAsync(probe.RenderAgain);
        await renderer.Dispatcher.InvokeAsync(Drain);
        Assert.Equal(new[] { "c", "a", "c" }, probe.Selections); Assert.Equal(new[] { "a", "c" }, probe.Activations.Select(a => a.Key));
        Assert.Single(probe.Sorts); Assert.Equal("c", probe.Selected);
    }
    [Fact]
    public async Task CompiledRazorTemplatesBindingsKeyedDiffsSortResetAndRemoval()
    {
        ItemLifetimeProbe.Removed.Clear();
        using var renderer = new TestRenderer(); using var root = new Panel();
        var probe = await renderer.AddComponent<ItemsBindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var list = Assert.IsType<ListView<ItemsBindingProbe.Asset>>(root.Children[0]);
        var table = Assert.IsType<BasicTable<ItemsBindingProbe.Asset>>(root.Children[1]);
        var b = list.FindRow("b")!; var c = list.FindRow("c")!; var content = b.Content;
        var button = Walk(b).OfType<Button>().Single(); var editor = Walk(b).OfType<TextBox>().Single();
        Assert.Equal("b", list.SelectedKey); Assert.Equal("b", table.SelectedKey); Assert.Empty(probe.Selections);
        await renderer.Dispatcher.InvokeAsync(() => list.Controller.Select("c", true));
        Assert.Equal("c", probe.Selected); Assert.Equal("c", table.SelectedKey); Assert.Single(probe.Selections);
        await renderer.Dispatcher.InvokeAsync(() => button.OnClick()); Assert.Equal(new[] { "b" }, probe.Opened); Assert.Empty(probe.Activations);
        await renderer.Dispatcher.InvokeAsync(() => list.Controller.Activate("c"));
        Assert.Equal("c", Assert.Single(probe.Activations).Key);
        renderer.AttributeUpdates.Clear();
        await renderer.Dispatcher.InvokeAsync(() => probe.Replace(new("c", "Renamed 日本語", 2), probe.Assets[1], new("d", "Inserted", 3)));
        Assert.Same(c, list.Rows[0]); Assert.Same(content, b.Content); Assert.Same(editor, Walk(b).OfType<TextBox>().Single());
        Assert.Equal(new[] { "a" }, ItemLifetimeProbe.Removed); Assert.Contains(Walk(c).OfType<Label>(), l => l.Text == "Renamed 日本語");
        Assert.Contains(renderer.AttributeUpdates, a => a.Name == "Text" && Equals(a.Value, "Renamed 日本語"));
        Assert.DoesNotContain(renderer.AttributeUpdates, a => a.Name == "Text" && Equals(a.Value, "Same"));
        await renderer.Dispatcher.InvokeAsync(() => table.ToggleSort("size"));
        Assert.Equal(new[] { "b", "c", "d" }, table.Controller.Items.Select(i => i.Key)); Assert.Single(probe.Sorts);
        await renderer.Dispatcher.InvokeAsync(() => table.ToggleSort("size"));
        Assert.Equal(new[] { "d", "c", "b" }, table.Controller.Items.Select(i => i.Key)); Assert.Equal(2, probe.Sorts.Count);
        await renderer.Dispatcher.InvokeAsync(probe.Reset); Assert.Equal("b", list.SelectedKey); Assert.Null(table.Sort); Assert.Single(probe.Selections);
        probe.Reject = true;
        await renderer.Dispatcher.InvokeAsync(() => list.Controller.Select("c", true)); Assert.Equal("b", list.SelectedKey);
        await renderer.Dispatcher.InvokeAsync(() => probe.Replace());
        Assert.Empty(list.Rows); Assert.Null(list.SelectedKey); Assert.Contains(Walk(list).OfType<Label>(), l => l.Text == "No matching assets" && l.IsEffectivelyVisible);
        Assert.Equal(new[] { "a", "b", "c", "d" }, ItemLifetimeProbe.Removed.Order());
        await renderer.Dispatcher.InvokeAsync(probe.Remove); Assert.Empty(root.Children); Assert.True(button.IsDisposed);
    }

    [Fact]
    public async Task AwaitingActionsKeepSnapshotsAndCannotRestoreResetOrUnmountedState()
    {
        using var renderer = new TestRenderer(); using var root = new Panel();
        var probe = await renderer.AddComponent<ItemsBindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var list = (ListView<ItemsBindingProbe.Asset>)root.Children[0]; var table = (BasicTable<ItemsBindingProbe.Asset>)root.Children[1];
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); int count = 0;
        probe.Wait = async () => { await gate.Task; if (++count == 4) completed.SetResult(); };
        await renderer.Dispatcher.InvokeAsync(() => { list.Controller.Select("a", true); list.Controller.Activate("a"); list.Controller.Activate("c"); table.ToggleSort("size"); });
        await renderer.Dispatcher.InvokeAsync(probe.RenderAgain);
        Assert.Equal(new[] { "a", "c" }, probe.Activations.Select(a => a.Key)); Assert.Single(probe.Sorts);
        await renderer.Dispatcher.InvokeAsync(probe.Reset); await renderer.Dispatcher.InvokeAsync(probe.Remove);
        gate.SetResult(); await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("b", probe.Selected); Assert.Null(probe.Sort); Assert.Empty(root.Children);
        list.Controller.Activate("a"); Assert.Equal(2, probe.Activations.Count);
    }

    [Fact]
    public async Task CompiledTemplateSurvivingEditorKeepsFocusAcrossInsertReorderAndAnotherRowUpdate()
    {
        using var renderer = new TestRenderer(); using var window = new Window(400, 280, "C12 template focus identity");
        var root = new Panel(); window.AddChild(root);
        var probe = await renderer.AddComponent<ItemsBindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var list = root.Children.OfType<ListView<ItemsBindingProbe.Asset>>().Single();
        var row = list.FindRow("b")!; var editor = Walk(row).OfType<TextBox>().Single(); window.SetFocus(editor);
        await renderer.Dispatcher.InvokeAsync(() => probe.Replace(new("c", "Renamed é", 5), new("d", "Inserted", 3), probe.Assets[1], probe.Assets[0]));
        Assert.Same(row, list.FindRow("b")); Assert.Same(editor, window.FocusedElement); Assert.Equal("Same", editor.Text); Assert.Equal("b", list.SelectedKey);
        await renderer.Dispatcher.InvokeAsync(() => probe.Replace(probe.Assets.Where(a => a.Key != "b").ToArray()));
        Assert.True(editor.IsDisposed); Assert.NotSame(editor, window.FocusedElement); Assert.Null(list.SelectedKey);
    }

    [Fact]
    public async Task RemovingTheColumnCancelsItsQueuedSortIdentity()
    {
        using var renderer = new TestRenderer(); using var window = new Window(400, 280, "C12 queued removed column");
        var root = new Panel(); window.AddChild(root);
        var probe = await renderer.AddComponent<ItemsBindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var table = root.Children.OfType<BasicTable<ItemsBindingProbe.Asset>>().Single();
        await renderer.Dispatcher.InvokeAsync(() => table.ToggleSort("size"));
        await renderer.Dispatcher.InvokeAsync(probe.RemoveSizeColumn);
        await renderer.Dispatcher.InvokeAsync(() => { while (window.DispatcherQueue.TryDequeue(out var action)) action(); });
        Assert.Empty(probe.Sorts); Assert.Null(probe.Sort); Assert.Null(table.Sort);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActivationAndSortCaptureTrueCallbackReplacementClearAndLegacyPrecedence(bool sort)
    {
        using var renderer = new TestRenderer(); using var window = new Window(400, 280, "C12 action subscriptions");
        var root = new Panel(); window.AddChild(root); int old = 0, replacement = 0, legacy = 0;
        var items = new[] { "a", "b" }; Func<string,string> key = s => s;
        var columns = new B.BasicTableColumn<string>[] { new("name", "Name", Comparer: StringComparer.Ordinal) };
        var activateOld = EventCallback.Factory.Create<ItemActivation<string>>(this, _ => old++);
        var activateNew = EventCallback.Factory.Create<ItemActivation<string>>(this, _ => replacement++);
        var sortOld = EventCallback.Factory.Create<TableSort?>(this, _ => old++);
        var sortNew = EventCallback.Factory.Create<TableSort?>(this, _ => replacement++);
        RenderFragment View(bool second = false, bool clear = false) => b =>
        {
            b.OpenComponent(0, sort ? typeof(B.BasicTable<string>) : typeof(B.ListView<string>));
            b.AddAttribute(1, "Items", items); b.AddAttribute(2, "KeySelector", key);
            if (sort)
            {
                b.AddAttribute(3, "Columns", columns); b.AddAttribute(4, "SortChanged", clear ? default : second ? sortNew : sortOld);
                b.AddAttribute(5, "OnSortChanged", EventCallback.Factory.Create<TableSort?>(this, _ => legacy++));
            }
            else b.AddAttribute(6, "OnActivate", clear ? default : second ? activateNew : activateOld);
            b.CloseComponent();
        };
        var host = await renderer.AddComponent<InputBindingTests.Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View() });
        void Request() { if (sort) ((BasicTable<string>)root.Children[0]).ToggleSort("name"); else ((ListView<string>)root.Children[0]).Controller.Activate("b"); }
        void Drain() { while (window.DispatcherQueue.TryDequeue(out var action)) action(); }
        await renderer.Dispatcher.InvokeAsync(Request); await renderer.Dispatcher.InvokeAsync(() => host.Render(View(true))); await renderer.Dispatcher.InvokeAsync(Drain);
        Assert.Equal(0, old); Assert.Equal(0, replacement); Assert.Equal(0, legacy);
        await renderer.Dispatcher.InvokeAsync(() => { Request(); Drain(); }); Assert.Equal(1, replacement); Assert.Equal(0, legacy);
        await renderer.Dispatcher.InvokeAsync(Request); await renderer.Dispatcher.InvokeAsync(() => host.Render(View(true, true))); await renderer.Dispatcher.InvokeAsync(Drain);
        Assert.Equal(1, replacement); Assert.Equal(0, legacy);
        await renderer.Dispatcher.InvokeAsync(() => { Request(); Drain(); }); Assert.Equal(sort ? 1 : 0, legacy); Assert.Equal(1, replacement);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task QueuedActionsSnapshotSubscriptionsAndExplicitResetCancelsIdentity(bool tableMode)
    {
        using var renderer = new TestRenderer(); using var window = new Window(400, 280, "C12 queued identity");
        var root = new Panel(); window.AddChild(root); int old = 0, replacement = 0, legacy = 0, actions = 0;
        var items = new[] { "a", "b", "c" }; Func<string,string> key = s => s;
        var columns = new B.BasicTableColumn<string>[] { new("name", "Name", Comparer: StringComparer.Ordinal) };
        EventCallback<string?> Callback(Action action) => EventCallback.Factory.Create<string?>(this, _ => action());
        RenderFragment View(EventCallback<string?> changed, long reset = 0) => b =>
        {
            b.OpenComponent(0, tableMode ? typeof(B.BasicTable<string>) : typeof(B.ListView<string>));
            b.AddAttribute(1, "Items", items); b.AddAttribute(2, "KeySelector", key); b.AddAttribute(3, "SelectedKey", "a");
            b.AddAttribute(4, "SelectedKeyChanged", changed); b.AddAttribute(5, "OnSelectionChanged", Callback(() => legacy++));
            b.AddAttribute(6, "ResetVersion", reset);
            b.AddAttribute(7, "OnActivate", EventCallback.Factory.Create<ItemActivation<string>>(this, _ => actions++));
            if (tableMode) b.AddAttribute(8, "Columns", columns);
            b.CloseComponent();
        };
        var first = Callback(() => old++); var second = Callback(() => replacement++);
        var host = await renderer.AddComponent<InputBindingTests.Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View(first) });
        var native = root.Children[0]; var list = tableMode ? ((BasicTable<string>)native).Body : (ListView<string>)native;
        void Drain() { while (window.DispatcherQueue.TryDequeue(out var action)) action(); }
        await renderer.Dispatcher.InvokeAsync(() => { list.Controller.Select("b", true); list.Controller.Activate("b"); });
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(second)));
        await renderer.Dispatcher.InvokeAsync(Drain); Assert.Equal(0, old); Assert.Equal(0, replacement); Assert.Equal(0, legacy); Assert.Equal(0, actions);
        await renderer.Dispatcher.InvokeAsync(() => { list.Controller.Select("c", true); Drain(); }); Assert.Equal(1, replacement); Assert.Equal(0, legacy);
        await renderer.Dispatcher.InvokeAsync(() => { list.Controller.Select("b", true); list.Controller.Activate("b"); });
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(second, 1)));
        await renderer.Dispatcher.InvokeAsync(Drain); Assert.Equal(1, replacement); Assert.Equal(0, actions); Assert.Equal("a", list.SelectedKey);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(default, 2)));
        await renderer.Dispatcher.InvokeAsync(() => { list.Controller.Select("b", true); Drain(); }); Assert.Equal(1, legacy);
        await renderer.Dispatcher.InvokeAsync(() => { list.Controller.Select("c", true); list.Controller.Activate("c"); });
        items = ["a", "b", "d"];
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(default, 2)));
        await renderer.Dispatcher.InvokeAsync(Drain); Assert.Equal(1, legacy); Assert.Equal(0, actions); Assert.Equal("a", list.SelectedKey);
        await renderer.Dispatcher.InvokeAsync(() => { list.Controller.Activate("b"); host.Render(_ => { }); Drain(); });
        Assert.Equal(0, actions); Assert.True(list.IsDisposed);
    }
}
