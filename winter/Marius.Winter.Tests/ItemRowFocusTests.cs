using System;
using System.Reflection;
using ThorVG;
using Xunit;

namespace Marius.Winter.Tests;

public class ItemRowFocusTests
{
    [Fact]
    public void SelectedRowPaintSurvivesViewportCollapseAndReturn() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(400, 200, "C12 selected row visibility", Theme.Dark, RenderBackend.SW);
        var controller = new ItemController<string>(s => s); controller.SetItems(["a", "b"]); controller.Select("a");
        var list = new ListView<string>(controller, new ItemElementFactory<string>(s => new Label(s), (e,s) => { })) { Bounds = new RectF(10, 10, 350, 150) };
        window.AddChild(list); Draw(window);
        uint Pixel(Element row) { row.WindowToLocal(0,0,out var nx,out var ny); return WindowRuntimeTests.Buffer(window)[((int)-ny + 2)*400 + (int)-nx + 150]; }
        Assert.NotEqual(Pixel(list.Rows[1]), Pixel(list.Rows[0]));
        list.Visible = false; list.Arrange(new RectF(10,10,0,0)); Draw(window);
        controller.Select(null); controller.Select("a");
        list.Visible = true; list.Arrange(new RectF(10,10,350,150)); Draw(window);
        Assert.NotEqual(Pixel(list.Rows[1]), Pixel(list.Rows[0]));
    });
    [Fact]
    public void RowFocusPaintsADistinctBorderAndChildFocusRemovesIt() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(400, 180, "C12 focus pixels", Theme.Light, RenderBackend.SW);
        var controller = new ItemController<string>(s => s); controller.SetItems(["a"]);
        var list = new ListView<string>(controller, new ItemElementFactory<string>(s => new Button("Preview"), (e,s) => { })) { Bounds = new RectF(10, 10, 350, 150) };
        window.AddChild(list); var row = list.Rows[0]; Draw(window);
        Assert.Equal(0, BorderPixels(window, row));
        window.SetFocus(row); Draw(window);
        Assert.True(BorderPixels(window, row) > 40, "A row with exact navigation focus must paint a visible border, even when the Panel theme has no BorderFocused color.");
        window.SetFocus(row.Content); Draw(window); Assert.Equal(0, BorderPixels(window, row));
        Assert.Null(controller.SelectedKey);
    });
    private static void Draw(Window window)
    {
        WindowRegressionOwner.Flush(window);
        Contracts.Draw((Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!, true);
    }
    private static int BorderPixels(Window window, Element row)
    {
        row.WindowToLocal(0, 0, out var nx, out var ny); var data = WindowRuntimeTests.Buffer(window); int count = 0;
        for (int x = (int)-nx + 6; x < (int)(-nx + row.Bounds.W) - 6; x++)
        {
            uint p = data[((int)-ny) * (int)window.FramebufferWidth + x];
            if ((p & 255) > ((p >> 16) & 255) + 50 && ((p >> 8) & 255) > ((p >> 16) & 255) + 15) count++;
        }
        return count;
    }
}
