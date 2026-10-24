using System;
using System.Linq;
using System.Reflection;
using ThorVG;
using Xunit;

namespace Marius.Winter.Tests;

public class MenuStateTests
{
    [Fact]
    public void OpenPopupReflectsLiveCheckDisabledShortcutAndDefinitionOrderWithoutReplacement() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(460, 280, "C09 live menu pixels", Theme.Light, RenderBackend.SW);
        var menu = new Menu(); int calls = 0;
        var save = menu.InsertItem(0, "Save preview", () => calls++); save.Shortcut = "Ctrl+S";
        var detail = menu.InsertItem(1, "Detailed view"); menu.AddSeparator(); var zoom = menu.AddSubMenu("Zoom"); zoom.AddItem("125%");
        var popup = new MenuPopup(window, menu); popup.PositionAt(420, 240); window.ShowOverlay(popup, window);
        Draw(window); var before = Marker(window, popup, 1); var enabled = Marker(window, popup, 0, 28, 110);
        var captured = save.Action!; save.Enabled = false; detail.IsChecked = true; save.Shortcut = "Ctrl+Shift+S";
        Draw(window);
        Assert.Same(popup, Assert.Single(MenuInteractionTests.Popups(window)));
        Assert.False(before.SequenceEqual(Marker(window, popup, 1)), "A live checked item must paint its check mark in the same open popup.");
        Assert.False(enabled.SequenceEqual(Marker(window, popup, 0, 28, 110)), "Live disabled state must visibly dim the actual row.");
        captured(); Assert.Equal(0, calls);
        popup.MoveFocus(1); Assert.Same(detail, popup.HighlightedItem);
        popup.OnKeyDown(257, 0, false); Assert.Equal(0, calls);
        menu.MoveItem(detail, 0); Draw(window); Assert.Same(detail, popup.HighlightedItem);
        Assert.InRange(popup.Bounds.X, 0, window.Bounds.W - popup.Bounds.W); Assert.InRange(popup.Bounds.Y, 0, window.Bounds.H - popup.Bounds.H);
        save.Enabled = true; captured(); Assert.Equal(1, calls);
        menu.RemoveItem(save); captured(); Assert.Equal(1, calls);
        const string longLabel = "Preview · Café · Žąsis · 東京 · Family 👩🏽‍💻 · a long legitimate document title for a narrow window";
        detail.Label = longLabel; Draw(window); Assert.Equal(longLabel, detail.Label);
        Assert.True(popup.Bounds.W <= window.Bounds.W - 8);
        var dark = Theme.Dark; dark.MenuBar.FontSize = 22; window.Theme = dark; Draw(window);
        Assert.True(popup.Bounds.H >= 22 * 3);
        window.RemoveOverlay(popup); popup.Dispose();
    });

    private static void Draw(Window window)
    {
        WindowRegressionOwner.Flush(window);
        Contracts.Draw((Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!, true);
    }
    private static uint[] Marker(Window window, MenuPopup popup, int row, int start = 5, int width = 18)
    {
        var pixels = WindowRuntimeTests.Buffer(window); int w = (int)window.FramebufferWidth;
        int x = (int)popup.Bounds.X + start, y = (int)popup.Bounds.Y + 2 + row * 24;
        return Enumerable.Range(0, 20).SelectMany(dy => pixels.Skip((y + dy) * w + x).Take(width)).ToArray();
    }
}
