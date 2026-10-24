using System;
using System.Linq;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Xunit;
using Asset = Marius.Winter.Scenarios.CatalogAsset;

namespace Marius.Winter.Tests;

public class AssetCatalogQueuedInputTests
{
    [Theory]
    [InlineData(false, RenderBackend.SW)] [InlineData(true, RenderBackend.SW)]
    [InlineData(false, RenderBackend.GL)] [InlineData(true, RenderBackend.GL)]
    public void TwoHeaderClicksInOneNativePollKeepDescendingOrder(bool razor, RenderBackend backend) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(800, 650, "C12 queued header input", Theme.Light, backend);
        using var scenario = razor ? null : new Playground.Scenarios.AssetCatalogScenario(window);
        if (razor) Assert.True(window.UseBlazor().AddComponent<Blazor.Playground.Scenarios.AssetCatalog>().IsCompletedSuccessfully);
        else { window.AddChild(scenario!.Content); window.SetAutoLayout(scenario.Content); }
        AssetCatalogInteractionTests.Walk(window).OfType<Button>().Single(b => b.Text == "Table").OnClick();
        WindowRegressionOwner.Flush(window);
        var table = AssetCatalogInteractionTests.Walk(window).OfType<BasicTable<Asset>>().Single();
        var header = AssetCatalogInteractionTests.Walk(table).OfType<Button>().Single(b => b.Text == "KB");
        header.WindowToLocal(0, 0, out var nx, out var ny);
        Glfw.Glfw.glfwShowWindow(window.GlfwWindow!);
        using var x = new FormX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
        x.Focus(); x.Move((int)(-nx + header.Bounds.W / 2), (int)(-ny + header.Bounds.H / 2));
        // Both ordinary press/release pairs wait for one native poll, before
        // the owning Window drains the captured wrapper actions.
        x.Click(); x.Click();
        TableSort? observed = null; string? first = null, indicator = null; bool closed = false;
        var input = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(180);
                await window.Dispatcher.InvokeAsync(() =>
                {
                    observed = table.Sort; first = table.Controller.Items[0].Key; indicator = header.Text;
                });
                x.Capture($"queued-header-{razor}-{backend}", 800, 650);
                x.CloseNormally(); closed = true;
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult();
        Assert.True(closed); Assert.True(window.IsDisposed);
        Assert.Equal("KB v", indicator);
        Assert.Equal(new TableSort("size", SortDirection.Descending), observed); Assert.Equal("team", first);
    });
}
