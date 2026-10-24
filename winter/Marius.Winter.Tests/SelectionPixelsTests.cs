using System;
using System.Linq;
using System.Reflection;
using ThorVG;
using Xunit;

namespace Marius.Winter.Tests;

public class SelectionPixelsTests
{
    [Fact]
    public void ScrolledPopup_StillPaintsReadableLastRowAndRepeatedHomeEndFrames() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(320, 240, "C02 popup text pixels", Theme.Light, RenderBackend.SW);
        var combo = new ComboBox(Enumerable.Range(1, 32).Select(i => $"Environment {i:00}").ToArray()) { Bounds = new RectF(10, 10, 260, 32) };
        window.AddChild(combo); combo.OnClick(); Draw(window);
        var popup = (Element)typeof(ComboBox).GetField("_popup", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(combo)!;
        Assert.True(DarkPixels(window, popup.Bounds.X + 8, popup.Bounds.Y + 2, 180, 24) > 40, "Initial popup row must contain readable text.");
        for (var i = 0; i < 3; i++)
        {
            combo.OnKeyDown(269, 0, false); Draw(window);
            Assert.True(DarkPixels(window, popup.Bounds.X + 8, popup.Bounds.Bottom - 26, 180, 24) > 40, "Scrolled last row must contain readable text, not just a selection highlight.");
            combo.OnKeyDown(268, 0, false); Draw(window);
            Assert.True(DarkPixels(window, popup.Bounds.X + 8, popup.Bounds.Y + 2, 180, 24) > 40, "Returning Home must preserve row text.");
        }
    });

    private static void Draw(Window window)
    {
        WindowRegressionOwner.Flush(window);
        Contracts.Draw((Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!, true);
    }
    private static int DarkPixels(Window window, float x, float y, int width, int height)
    {
        var buffer = WindowRuntimeTests.Buffer(window); int count = 0, stride = (int)window.FramebufferWidth;
        for (var py = (int)y; py < (int)y + height; py++)
            for (var px = (int)x; px < (int)x + width; px++)
            {
                var pixel = buffer[py * stride + px];
                if ((pixel & 255) < 90 && ((pixel >> 8) & 255) < 90 && ((pixel >> 16) & 255) < 90) count++;
            }
        return count;
    }
}
