using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Marius.Winter.Blazor;
using ThorVG;
using Xunit;
using Asset = Marius.Winter.Scenarios.CatalogAsset;

namespace Marius.Winter.Tests;

public class AssetCatalogFirstFrameTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ActualModuleFirstDrawPaintsRowsThumbnailsAndTableHeadersWithoutScrolling(bool razor) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(800, 650, "C12 first-frame pixels", Theme.Light, RenderBackend.SW);
        using var scenario = razor ? null : new Playground.Scenarios.AssetCatalogScenario(window);
        if (razor) Assert.True(window.UseBlazor().AddComponent<Blazor.Playground.Scenarios.AssetCatalog>().IsCompletedSuccessfully);
        else { window.AddChild(scenario!.Content); window.SetAutoLayout(scenario.Content); }
        var table = AssetCatalogInteractionTests.Walk(window).OfType<BasicTable<Asset>>().Single();
        var list = AssetCatalogInteractionTests.Walk(window).OfType<ListView<Asset>>().Single(l => l != table.Body);
        Draw(window); Assert.Equal(0, list.Viewport.Scroll);
        AssertInkAndThumbnail(window, list.Rows[0]); Save(window, $"catalog-{razor}-first-draw-list");
        AssetCatalogInteractionTests.Walk(window).OfType<Button>().Single(b => b.Text == "Table").OnClick();
        Draw(window); Assert.Equal(0, table.Body.Viewport.Scroll);
        AssertInkAndThumbnail(window, table.Body.Rows[0]);
        var header = AssetCatalogInteractionTests.Walk(table).OfType<Button>().Single(b => b.Text == "Name");
        Assert.True(Count(window, header, c => (c & 255) < 100 && ((c >> 8) & 255) < 100 && ((c >> 16) & 255) < 100) > 30, "First table frame requires readable header glyphs.");
        Save(window, $"catalog-{razor}-first-draw-table");
    });
    private static void Draw(Window window)
    {
        WindowRegressionOwner.Flush(window);
        Contracts.Draw((Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!, true);
    }
    private static void AssertInkAndThumbnail(Window window, Element row)
    {
        Assert.True(Count(window, row, c => (c & 255) < 100 && ((c >> 8) & 255) < 100 && ((c >> 16) & 255) < 100) > 45, "Initial row must contain readable text.");
        var image = AssetCatalogInteractionTests.Walk(row).OfType<SvgImage>().Single();
        Assert.True(Count(window, image, c => (c & 255) > ((c >> 16) & 255) + 30) > 70, "Initial row must paint its actual blue thumbnail.");
    }
    private static int Count(Window window, Element e, Func<uint,bool> predicate)
    {
        e.WindowToLocal(0, 0, out var nx, out var ny); var pixels = WindowRuntimeTests.Buffer(window); int count = 0;
        int w = (int)window.FramebufferWidth, h = (int)window.FramebufferHeight;
        for (int y = Math.Max(0, (int)-ny); y < Math.Min(h, (int)(-ny + e.Bounds.H)); y++)
            for (int x = Math.Max(0, (int)-nx); x < Math.Min(w, (int)(-nx + e.Bounds.W)); x++) if (predicate(pixels[y*w+x])) count++;
        return count;
    }
    private static void Save(Window window, string name)
    {
        var path = Environment.GetEnvironmentVariable("WINTER_C12_EVIDENCE"); if (string.IsNullOrEmpty(path)) return;
        Directory.CreateDirectory(path); using var output = File.Create(Path.Combine(path, name + ".ppm"));
        int w = (int)window.FramebufferWidth, h = (int)window.FramebufferHeight; var data = WindowRuntimeTests.Buffer(window);
        output.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{w} {h}\n255\n"));
        foreach (var p in data) { output.WriteByte((byte)(p >> 16)); output.WriteByte((byte)(p >> 8)); output.WriteByte((byte)p); }
    }
}
