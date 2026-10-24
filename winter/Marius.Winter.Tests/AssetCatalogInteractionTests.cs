using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Xunit;
using Asset = Marius.Winter.Scenarios.CatalogAsset;

namespace Marius.Winter.Tests;

public class AssetCatalogInteractionTests
{
    internal static IEnumerable<Element> Walk(Element e) { yield return e; foreach (var c in e.Children) foreach (var item in Walk(c)) yield return item; }
    [Theory]
    [InlineData(false, false, false, RenderBackend.SW)]
    [InlineData(true, false, false, RenderBackend.SW)]
    [InlineData(false, true, true, RenderBackend.SW)]
    [InlineData(true, true, true, RenderBackend.SW)]
    [InlineData(false, false, false, RenderBackend.GL)]
    [InlineData(true, false, false, RenderBackend.GL)]
    public void ActualCatalogRowsTemplatesKeyboardPointerSortFilterMutationResizeAndNormalClose(bool razor, bool dark, bool large, RenderBackend backend) => WindowRegressionOwner.Run(() =>
    {
        var theme = dark ? Theme.Dark : Theme.Light;
        if (large) { theme.Label.FontSize = theme.Panel.FontSize = theme.Button.FontSize = theme.TextBox.FontSize = 22; }
        using var window = new Window(800, 650, "C12 Asset catalog input", theme, backend);
        using var scenario = razor ? null : new Playground.Scenarios.AssetCatalogScenario(window);
        if (razor) Assert.True(window.UseBlazor().AddComponent<Blazor.Playground.Scenarios.AssetCatalog>().IsCompletedSuccessfully);
        else { window.AddChild(scenario!.Content); window.SetAutoLayout(scenario.Content); }
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new FormX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                await Task.Delay(180); x.Focus();
                string prefix = $"catalog-{(razor ? "razor" : "native")}-{dark}-{large}-{backend}";
                x.Capture(prefix + "-first", 800, 650);
                ListView<Asset> list = null!; BasicTable<Asset> table = null!; TextBox filter = null!;
                await window.Dispatcher.InvokeAsync(() =>
                {
                    table = Walk(window).OfType<BasicTable<Asset>>().Single();
                    list = Walk(window).OfType<ListView<Asset>>().Single(l => !ReferenceEquals(l, table.Body));
                    filter = Walk(window).OfType<TextBox>().Single();
                    Assert.Equal(8, list.Rows.Count); Assert.Equal(0, list.Viewport.Scroll); Assert.True(list.Rows[0].Bounds.H >= 28);
                });
                Button Button(string text) => Walk(window).OfType<Button>().Single(b => b.Text == text && b.IsEffectivelyVisible);
                void Result(string starts) => Assert.Contains(Walk(window).OfType<Label>(), l => l.Text.StartsWith(starts, StringComparison.Ordinal));
                async Task Click(Element e, bool twice = false)
                {
                    float px = 0, py = 0;
                    await window.Dispatcher.InvokeAsync(() => { e.WindowToLocal(0, 0, out var lx, out var ly); px = Math.Min(12, e.Bounds.W / 2) - lx; py = e.Bounds.H / 2 - ly; });
                    x.Move((int)px, (int)py); x.Click(); if (twice) x.Click(); await Task.Delay(65);
                }
                await Click(list.Rows[0]);
                x.Key(0xff54); x.Key(0xff0d); await Task.Delay(70);
                await window.Dispatcher.InvokeAsync(() => { Assert.Equal("forest", list.SelectedKey); Result("Opened: Forest.svg"); Assert.Same(list.FindRow("forest"), window.FocusedElement); });
                x.Capture(prefix + "-selected", 800, 650);
                x.Key(0xff09); x.Key(0xff0d); x.Key(0xff54); await Task.Delay(70);
                await window.Dispatcher.InvokeAsync(() => { Assert.IsAssignableFrom<Button>(window.FocusedElement); Assert.Equal("forest", list.SelectedKey); Result("Preview: Forest.svg"); });
                x.Resize(800, 370); await Task.Delay(80);
                x.Chord(0xffe1, 0xff09); x.Key(0xff57); await Task.Delay(70);
                await window.Dispatcher.InvokeAsync(() => { Assert.Equal("notes", list.SelectedKey); Assert.True(list.Viewport.Scroll > 0); });
                x.Key(0xff50); await Task.Delay(70); x.Resize(800, 650); await Task.Delay(80);
                await Click(list.FindRow("cafe")!, true);
                await window.Dispatcher.InvokeAsync(() => { Assert.Equal("cafe", list.SelectedKey); Result("Opened: Café.png"); });
                x.Capture(prefix + "-activation", 800, 650);
                foreach (string action in new[] { "Insert", "Rename", "Update size", "Reverse" }) await Click(Button(action));
                await window.Dispatcher.InvokeAsync(() => { Assert.Equal(9, list.Rows.Count); Assert.Equal("cafe", list.SelectedKey); Assert.Equal(85, list.SelectedItem!.Size); Assert.StartsWith("Renamed", list.SelectedItem.Name); });
                await Click(Button("Remove"));
                await window.Dispatcher.InvokeAsync(() => { Assert.Null(list.SelectedKey); Assert.Null(list.FindRow("cafe")); });
                await Click(Button("Reset")); await Click(Button("Table"));
                x.Capture(prefix + "-table-first", 800, 650);
                await Click(Button("KB"));
                await window.Dispatcher.InvokeAsync(() => { Assert.Equal("notes", table.Controller.Items[0].Key); Assert.Equal(SortDirection.Ascending, table.Sort!.Direction); });
                await Click(Button("KB ^"));
                await window.Dispatcher.InvokeAsync(() => { Assert.Equal("team", table.Controller.Items[0].Key); Assert.Equal(SortDirection.Descending, table.Sort!.Direction); });
                await Click(table.Body.Rows[0]); x.Key(0xff0d); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => Result("Opened: Team 👩‍💻.png"));
                await Click(Walk(table.Body.Rows[0]).OfType<Button>().Single()); x.Key(0xff0d); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => Result("Preview: Team 👩‍💻.png"));
                float sx = 0, sy = 0;
                await window.Dispatcher.InvokeAsync(() => { table.Body.Viewport.WindowToLocal(0, 0, out var nx, out var ny); sx = -nx + table.Body.Viewport.Bounds.W - 5; sy = -ny + table.Body.Viewport.Bounds.H - 12; });
                x.Move((int)sx, (int)sy); x.Click(); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => { Assert.Equal("team", table.SelectedKey); Result("Preview: Team 👩‍💻.png"); });
                x.Capture(prefix + "-table-sorted", 800, 650);
                await Click(filter); x.TypeAscii("zzzz"); await Task.Delay(80);
                await window.Dispatcher.InvokeAsync(() => { Assert.Empty(table.Body.Rows); Assert.Null(table.SelectedKey); Result("No matching assets."); });
                x.Capture(prefix + "-empty", 800, 650);
                x.Chord(0xffe3, 0x61); x.Key(0xff08); await Task.Delay(70);
                await window.Dispatcher.InvokeAsync(() => Assert.Equal(8, table.Body.Rows.Count));
                await Click(Button("Reset")); await Click(Button("Table")); x.Resize(440, 620); await Task.Delay(120);
                x.Capture(prefix + "-narrow", 440, 620);
                await window.Dispatcher.InvokeAsync(() => { Assert.Equal(8, table.Body.Rows.Count); Assert.Equal(0, table.Body.Viewport.Scroll); Assert.True(table.Body.Bounds.W <= 440); Assert.True(table.Body.Rows[0].Bounds.H > 0); });
                await Click(Button("List")); x.Capture(prefix + "-narrow-list", 440, 620);
                await Click(Button("Reset")); await Click(Button("Table"));
                await Click(Button("Name")); await window.Dispatcher.InvokeAsync(() => Assert.Equal("cafe", table.Controller.Items[0].Key));
                x.CloseNormally();
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult(); Assert.True(window.IsDisposed);
    });

    [Fact]
    public void DescendantEditorOwnsItsArrowHomeAndEnterInputAndTabReturnsToRow() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(420, 260, "C12 composed editor", Theme.Light);
        var controller = new ItemController<string>(s => s); controller.SetItems(["a", "b"]);
        var list = new ListView<string>(controller, new ItemElementFactory<string>(s => new TextBox("A legitimate note é"), (e,s) => { })) { Bounds = new RectF(0, 0, 420, 220) };
        window.AddChild(list); int activations = 0; list.Activated += _ => activations++;
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new FormX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!)); await Task.Delay(160); x.Focus();
                x.Key(0xff09); x.Key(0xff54); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => { Assert.Equal("b", controller.SelectedKey); Assert.Same(list.FindRow("b"), window.FocusedElement); });
                x.Key(0xff09); x.Key(0xff50); x.Key(0xff54); x.Key(0xff0d); await Task.Delay(70);
                await window.Dispatcher.InvokeAsync(() => { Assert.IsType<TextBox>(window.FocusedElement); Assert.Equal("b", controller.SelectedKey); Assert.Equal(0, activations); });
                x.Chord(0xffe1, 0xff09); x.Key(0xff0d); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => { Assert.Same(list.FindRow("b"), window.FocusedElement); Assert.Equal(1, activations); });
                x.CloseNormally();
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult();
    });
}
