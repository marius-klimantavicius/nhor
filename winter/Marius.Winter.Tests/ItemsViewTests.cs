using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Marius.Winter.Tests;

public class ItemsViewTests
{
    private sealed class MutableAsset(string key, string name) { public string Key = key; public string Name = name; }
    [Fact]
    public void ExplicitUpdateRecomposesOneMutableItemWithoutDestroyingItsRow()
    {
        var item = new MutableAsset("a", "Before"); var controller = new ItemController<MutableAsset>(a => a.Key); controller.SetItems([item]);
        int updates = 0;
        using var list = new ListView<MutableAsset>(controller, new ItemElementFactory<MutableAsset>(a => new Label(a.Name), (e,a) => { ((Label)e).Text = a.Name; updates++; }));
        var row = list.Rows[0]; item.Name = "Updated"; controller.Update("a", item);
        Assert.Equal("Updated", ((Label)row.Content!).Text); Assert.Equal(1, updates); Assert.Same(row, list.Rows[0]);
    }
    private sealed class TrackedLabel(string text) : Label(text)
    {
        public int Disposals;
        public override void Dispose() { if (!IsDisposed) Disposals++; base.Dispose(); }
    }
    [Fact]
    public void NativeFactoriesKeepRowsUpdateOnlyChangedValuesAndReleaseOwnedAndBorrowedContent() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(480, 300, "C12 native factories");
        var controller = new ItemController<ItemControllerTests.Asset>(a => a.Key);
        controller.SetItems([new("a", "Same", 2), new("b", "Same", 1)]);
        var outputs = new List<TrackedLabel>(); var updates = new List<string>();
        var factory = new ItemElementFactory<ItemControllerTests.Asset>(a => { var label = new TrackedLabel(a.Name); outputs.Add(label); return label; },
            (e, a) => { ((Label)e).Text = a.Name; updates.Add(a.Key); });
        var list = new ListView<ItemControllerTests.Asset>(controller, factory) { Bounds = new RectF(0, 0, 480, 260) };
        window.AddChild(list); WindowRegressionOwner.Flush(window);
        var b = list.FindRow("b")!; window.SetFocus(b); controller.Select("b");
        controller.Reorder(["b", "a"]); controller.Update("a", new("a", "Café 日本語 👩‍💻", 4));
        WindowRegressionOwner.Flush(window);
        Assert.Same(b, list.Rows[0]); Assert.Same(b, window.FocusedElement); Assert.Equal("b", list.SelectedKey);
        Assert.Equal(new[] { "a" }, updates); Assert.Equal(2, outputs.Count);
        controller.Remove("a"); Assert.Equal(1, outputs[0].Disposals); Assert.Equal(0, outputs[1].Disposals);
        var borrowed = new TrackedLabel("Borrowed"); list.SetRowContent("b", borrowed);
        Assert.Equal(1, outputs[1].Disposals);
        var empty = new TrackedLabel("Empty"); list.SetEmptyContent(empty);
        controller.SetItems([]);
        Assert.Null(borrowed.Parent); Assert.False(borrowed.IsDisposed); Assert.Null(controller.SelectedKey);
        Assert.True(empty.Visible); Assert.True(b.IsDisposed);
        list.Dispose(); Assert.Null(empty.Parent); Assert.False(empty.IsDisposed);
        controller.SetItems([new("c", "After detach", 1)]); Assert.Empty(list.Children);
        borrowed.Dispose(); empty.Dispose();
    });

    [Fact]
    public void SharedControllerStableSortSelectionSnapshotsAndAtomicColumnValidation() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(500, 320, "C12 table sort");
        var controller = new ItemController<ItemControllerTests.Asset>(a => a.Key);
        controller.SetItems([new("a", "Same", 2), new("b", "Žąsis", 1), new("c", "Same", 2)]);
        using var list = new ListView<ItemControllerTests.Asset>(controller);
        var table = new BasicTable<ItemControllerTests.Asset>(controller, [new("name", "Name", 200, 50, 1, a => a.Name),
            new("size", "Size", 100, 45, 0, a => a.Size.ToString(), Comparer: Comparer<ItemControllerTests.Asset>.Create((a,b) => a.Size.CompareTo(b.Size)))]) { Bounds = new RectF(0, 0, 500, 300) };
        window.AddChild(table); WindowRegressionOwner.Flush(window);
        var a = table.Body.FindRow("a")!; controller.Select("a"); window.SetFocus(a);
        var sort = new List<TableSort>(); table.SortChanged += sort.Add;
        table.ToggleSort("size");
        Assert.Equal(new[] { "b", "a", "c" }, controller.Items.Select(i => i.Key)); Assert.Same(a, table.Body.FindRow("a"));
        Assert.Same(a, window.FocusedElement); Assert.Equal("a", list.SelectedKey);
        table.ToggleSort("size"); Assert.Equal(new[] { "a", "c", "b" }, controller.Items.Select(i => i.Key)); Assert.Equal(2, sort.Count);
        var activations = new List<ItemActivation<ItemControllerTests.Asset>>(); controller.Activated += activations.Add;
        controller.Activate("a"); controller.Update("a", new("a", "Renamed", 3)); controller.Activate("c");
        Assert.Equal(new[] { "a", "c" }, activations.Select(i => i.Key)); Assert.Equal("Same", activations[0].Item.Name);
        Assert.Throws<ArgumentException>(() => table.SetColumns([new("same", "One"), new("same", "Two")]));
        Assert.Equal(2, table.Columns.Count);
        var layout = new TableCellsLayout<ItemControllerTests.Asset>(() => table.Columns);
        Assert.Equal(new[] { 50f, 45f }, layout.Widths(20)); Assert.Equal(600, layout.Widths(600).Sum());
        controller.Configure([], "missing"); Assert.Null(controller.SelectedKey);
    });
}
