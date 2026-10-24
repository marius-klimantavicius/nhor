using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Xunit;
using Asset = Marius.Winter.Scenarios.CatalogAsset;

namespace Marius.Winter.Tests;

public class RegisteredCatalogCommandsInputTests
{
    [Theory]
    [InlineData(RenderBackend.SW)] [InlineData(RenderBackend.GL)]
    public void ActualCompiledThirteenPageHostRunsCatalogTableAndClosesWithCommandsOpen(RenderBackend backend) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(900, 720, "C09 registered Razor pages", Theme.Light, backend);
        Assert.True(window.UseBlazor().AddComponent<Blazor.Playground.App>(new Dictionary<string, object> { ["OwnerWindow"] = window }).IsCompletedSuccessfully);
        var profile = MenuScenarioTests.Walk(window).OfType<TextBox>().First();
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new FormX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!)); await Task.Delay(180); x.Focus();
                using var screen = new MenuScreen(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                async Task Click(Element e)
                {
                    RectF b = default; await window.Dispatcher.InvokeAsync(() => b = MenuScenarioTests.Absolute(e));
                    x.Move((int)b.X + Math.Min(12, (int)b.W / 2), (int)(b.Y + b.H / 2)); x.Click(); await Task.Delay(60);
                }
                Button Button(string name) => MenuScenarioTests.Walk(window).OfType<Button>().Single(b => b.IsEffectivelyVisible && b.Text == name);
                ComboBox selector = null!;
                await window.Dispatcher.InvokeAsync(() => { selector = MenuScenarioTests.Walk(window).OfType<ComboBox>().First(); Assert.Equal(13, selector.Items.Count); });
                await Click(selector); x.Key(0xff50); for (int step = 0; step < 10; step++) x.Key(0xff54); x.Key(0xff0d); await Task.Delay(100);
                BasicTable<Asset> table = null!; ListView<Asset> list = null!;
                await window.Dispatcher.InvokeAsync(() =>
                {
                    Assert.Equal(10, selector.SelectedIndex); table = MenuScenarioTests.Walk(window).OfType<BasicTable<Asset>>().Single();
                    list = MenuScenarioTests.Walk(window).OfType<ListView<Asset>>().Single(l => l != table.Body); Assert.Equal(8, list.Rows.Count); Assert.Equal("harbor", list.SelectedKey);
                    Assert.Same(profile, MenuScenarioTests.Walk(window).OfType<TextBox>().First());
                });
                x.Capture($"registered-{backend}-catalog-first", 900, 720);
                RectF thumbnail = default;
                await window.Dispatcher.InvokeAsync(() => thumbnail = MenuScenarioTests.Absolute(MenuScenarioTests.Walk(list.Rows[0]).OfType<SvgImage>().Single()));
                Assert.True(screen.Read(900, 720).Count(thumbnail, (r,g,b) => b > r + 30 && g > r + 10) > 70, "The registered catalog must paint its first thumbnail without scrolling.");
                await Click(Button("Table")); await Click(Button("KB"));
                await window.Dispatcher.InvokeAsync(() => { Assert.Equal("notes", table.Controller.Items[0].Key); Assert.Equal(SortDirection.Ascending, table.Sort!.Direction); });
                await Click(table.Body.Rows[0]); x.Key(0xff0d); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => Assert.Contains(MenuScenarioTests.Walk(window).OfType<Label>(), l => l.IsEffectivelyVisible && l.Text.StartsWith("Opened: Žąsis é.txt", StringComparison.Ordinal)));
                x.Capture($"registered-{backend}-catalog-table", 900, 720); await Click(Button("Reset"));
                await Click(selector); x.Key(0xff57); x.Key(0xff0d); await Task.Delay(100);
                await window.Dispatcher.InvokeAsync(() => Assert.Equal(12, selector.SelectedIndex)); x.Capture($"registered-{backend}-files-first", 900, 720);
                await Click(Button("Open file"));
                FilePicker picker = null!;
                var deadline = DateTime.UtcNow.AddSeconds(2);
                bool ready = false;
                while (!ready && DateTime.UtcNow < deadline)
                {
                    ready = await window.Dispatcher.InvokeAsync(() =>
                    {
                        picker = MenuScenarioTests.Walk(window).OfType<FilePicker>().Single(p => p.Dialog.IsOpen);
                        return picker.Controller!.State.LoadState == FilePickerLoadState.Ready;
                    });
                    if (!ready) await Task.Delay(20);
                }
                Assert.True(ready); x.Capture($"registered-{backend}-files-open", 900, 720);
                x.Key(0xff50); x.Key(0xff54); x.Key(0xff54); x.Key(0xff0d); await Task.Delay(100);
                await window.Dispatcher.InvokeAsync(() =>
                {
                    Assert.False(picker.Dialog.IsOpen);
                    Assert.Contains(MenuScenarioTests.Walk(window).OfType<Label>(), l => l.IsEffectivelyVisible && l.Text.StartsWith("OpenFile: /Files/cover.svg", StringComparison.Ordinal));
                });
                await Click(Button("Reset"));
                await Click(selector); x.Key(0xff50); for (int step = 0; step < 11; step++) x.Key(0xff54); x.Key(0xff0d); await Task.Delay(100);
                await window.Dispatcher.InvokeAsync(() => { Assert.Equal(11, selector.SelectedIndex); Assert.Same(profile, MenuScenarioTests.Walk(window).OfType<TextBox>().First()); });
                x.Capture($"registered-{backend}-commands-first", 900, 720);
                RectF preview = default;
                await window.Dispatcher.InvokeAsync(() => preview = MenuScenarioTests.Absolute(MenuScenarioTests.Walk(window).OfType<SvgImage>().Single(e => e.IsEffectivelyVisible)));
                Assert.True(screen.Read(900, 720).Count(preview, (r,g,b) => b > r + 20 && g > r + 10) > 1000, "The registered commands page must paint its real preview in its first selected frame.");
                x.Key(0xffc7); await Task.Delay(70);
                await window.Dispatcher.InvokeAsync(() => Assert.Single(MenuInteractionTests.Popups(window)));
                x.Capture($"registered-{backend}-commands-close-open", 900, 720); x.CloseNormally();
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult(); Assert.True(window.IsDisposed);
    });
}
