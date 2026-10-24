using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace Marius.Winter.Tests;

public class SelectionInputTests
{
    [Theory]
    [InlineData(RenderBackend.SW, false, false)]
    [InlineData(RenderBackend.SW, true, true)]
    [InlineData(RenderBackend.GL, false, true)]
    public void ActualTabEntryKeyboardPointerScrollbarResizeAndNormalClose(RenderBackend backend, bool dark, bool large) => WindowRegressionOwner.Run(() =>
    {
        var theme = dark ? Theme.Dark : Theme.Light;
        if (large) { theme.Button.FontSize = 24; theme.Panel.FontSize = 24; }
        using var window = new Window(480, 300, "C02 actual owned X11", theme, backend);
        var combo = new ComboBox(Enumerable.Range(0, 32).Select(i => $"Environment {i:00} · Žąsis e\u0301").ToArray()) { Bounds = new RectF(10, 10, 260, 40) };
        var tabs = new TabWidget() { Bounds = new RectF(10, 265, 300, 34) };
        tabs.AddTab("General", new Panel()); tabs.AddTab("Details", new Panel()); tabs.AddTab("Access", new Panel());
        window.AddChild(combo); window.AddChild(tabs);
        var changes = 0; combo.UserSelectionChanged += _ => changes++;
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new SelectionX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                await Task.Delay(180); x.Focus(); x.Key(0xff09); await Task.Delay(70);
                await window.Dispatcher.InvokeAsync(() => Assert.Same(combo, window.FocusedElement));
                x.Key(0xff0d); x.Key(0xff57); await Task.Delay(80);
                await window.Dispatcher.InvokeAsync(() => { Assert.True(combo.IsDropDownOpen); Assert.Equal(31, combo.HighlightedIndex); Assert.Equal(0, combo.SelectedIndex); });
                x.Key(0xff1b); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => { Assert.False(combo.IsDropDownOpen); Assert.Equal(0, changes); });
                x.Key(0x20); x.Key(0xff57); x.Key(0xff0d); await Task.Delay(80);
                await window.Dispatcher.InvokeAsync(() => { Assert.Equal(31, combo.SelectedIndex); Assert.Equal(1, changes); });
                x.Key(0xff09); x.Key(0xff53); await Task.Delay(70);
                await window.Dispatcher.InvokeAsync(() => { Assert.Same(tabs, window.FocusedElement); Assert.Equal(1, tabs.ActiveTab); });
                x.Key(0xff57); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => Assert.Equal(2, tabs.ActiveTab));
                x.Key(0xff50); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => Assert.Equal(0, tabs.ActiveTab));
                x.Move(30, 28); x.Click(); await Task.Delay(70); x.Key(0xff50); await Task.Delay(60);
                // First entry after a teleport has no preceding OnMouseMove within the popup.
                var rowY = 52 + 2 + (large ? 32 : 24) * 1.5f;
                x.Move(450, 230); x.Move(30, (int)rowY); x.Click(); await Task.Delay(80);
                await window.Dispatcher.InvokeAsync(() => { Assert.Equal(1, combo.SelectedIndex); Assert.Equal(2, changes); });
                x.Move(30, 28); x.Click(); await Task.Delay(60);
                RectF popup = default;
                await window.Dispatcher.InvokeAsync(() => popup = Popup(combo).Bounds);
                x.Move((int)popup.Right - 6, (int)popup.Bottom - 12); x.Click(); await Task.Delay(70);
                await window.Dispatcher.InvokeAsync(() => { Assert.True(combo.IsDropDownOpen); Assert.Equal(2, changes); });
                x.Move(450, 230); x.Click(); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => Assert.False(combo.IsDropDownOpen));
                x.Move(30, 28); x.Click(); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => Assert.True(combo.IsDropDownOpen));
                x.Click(); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => Assert.False(combo.IsDropDownOpen));
                // Move the control near the bottom, then open and resize while the popup is live.
                await window.Dispatcher.InvokeAsync(() => { combo.Bounds = new RectF(200, 250, 260, 40); window.SetFocus(combo); });
                x.Key(0xff0d); x.Key(0xff57); await Task.Delay(70);
                x.Resize(260, 160); await Task.Delay(100);
                await window.Dispatcher.InvokeAsync(() =>
                {
                    var bounds = Popup(combo).Bounds;
                    Assert.InRange(bounds.X, 0, window.Bounds.W - bounds.W);
                    Assert.InRange(bounds.Y, 0, window.Bounds.H - bounds.H);
                    Assert.Equal(31, combo.HighlightedIndex); combo.Enabled = false;
                    Assert.False(combo.IsDropDownOpen); Assert.Null(window.FocusedElement); Assert.Equal(2, changes);
                });
                x.CloseNormally();
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult();
        Assert.True(window.IsDisposed);
    });

    private static Element Popup(ComboBox combo) => (Element)typeof(ComboBox).GetField("_popup", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(combo)!;
}

// Uses only the private display inherited from the Python-owned Xvfb launcher.
internal sealed class SelectionX11 : IDisposable
{
    private readonly nint _display;
    private readonly nuint _window;
    internal SelectionX11(nuint window)
    {
        _window = window; _display = XOpenDisplay(Environment.GetEnvironmentVariable("DISPLAY")!);
        Assert.NotEqual(0, _display);
    }
    internal void Focus() { XSetInputFocus(_display, _window, 1, 0); XSync(_display, 0); }
    internal void Move(int x, int y) { XWarpPointer(_display, 0, _window, 0, 0, 0, 0, x, y); XSync(_display, 0); }
    internal void Click() { XTestFakeButtonEvent(_display, 1, 1, 0); XTestFakeButtonEvent(_display, 1, 0, 20); XSync(_display, 0); }
    internal void Key(nuint symbol)
    {
        var code = XKeysymToKeycode(_display, symbol); Assert.NotEqual(0, code);
        XTestFakeKeyEvent(_display, code, 1, 0); XTestFakeKeyEvent(_display, code, 0, 20); XSync(_display, 0);
    }
    internal void Resize(uint width, uint height) { XResizeWindow(_display, _window, width, height); XSync(_display, 0); }
    internal void CloseNormally()
    {
        var message = new ClientMessage { Type = 33, Display = _display, Window = _window, Format = 32,
            MessageType = XInternAtom(_display, "WM_PROTOCOLS", 0), Data0 = (nint)XInternAtom(_display, "WM_DELETE_WINDOW", 0) };
        Assert.NotEqual(0, XSendEvent(_display, _window, 0, 0, ref message)); XSync(_display, 0);
    }
    public void Dispose() => XCloseDisplay(_display);
    [StructLayout(LayoutKind.Sequential, Size = 192)]
    private struct ClientMessage
    {
        internal int Type; internal nuint Serial; internal int SendEvent; internal nint Display;
        internal nuint Window, MessageType; internal int Format; internal nint Data0, Data1, Data2, Data3, Data4;
    }
    [DllImport("libX11.so.6")] private static extern nint XOpenDisplay(string name);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(nint display);
    [DllImport("libX11.so.6")] private static extern int XSync(nint display, int discard);
    [DllImport("libX11.so.6")] private static extern int XSetInputFocus(nint display, nuint window, int revert, nuint time);
    [DllImport("libX11.so.6")] private static extern int XWarpPointer(nint display, nuint source, nuint dest, int sx, int sy, uint sw, uint sh, int x, int y);
    [DllImport("libX11.so.6")] private static extern byte XKeysymToKeycode(nint display, nuint symbol);
    [DllImport("libX11.so.6")] private static extern int XResizeWindow(nint display, nuint window, uint width, uint height);
    [DllImport("libX11.so.6")] private static extern nuint XInternAtom(nint display, string name, int onlyExisting);
    [DllImport("libX11.so.6")] private static extern int XSendEvent(nint display, nuint window, int propagate, nint mask, ref ClientMessage message);
    [DllImport("libXtst.so.6")] private static extern int XTestFakeButtonEvent(nint display, uint button, int press, nuint delay);
    [DllImport("libXtst.so.6")] private static extern int XTestFakeKeyEvent(nint display, uint key, int press, nuint delay);
}
