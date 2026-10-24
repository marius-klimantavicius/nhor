using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace Marius.Winter.Tests;

public class SplitPaneInputTests
{
    [Theory]
    [InlineData("hide")]
    [InlineData("disable")]
    [InlineData("unmount")]
    [InlineData("dispose")]
    public void ActualDragKeyboardScrollbarResizeAndCaptureCleanup(string transition)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(520, 300, "SplitPane owned X11 input", Theme.Light, RenderBackend.SW);
            var scroll = new ScrollPanel { Layout = new StackLayout(), Padding = new Thickness(0) };
            scroll.AddChild(new Panel { MinHeight = 1000, MaxHeight = 1000 });
            var split = new SplitPane { Position = 180, ResetPosition = 180, FirstMinimum = 80, SecondMinimum = 90, First = scroll, Second = new TextArea("Editable preview") };
            window.AddChild(split); window.SetAutoLayout(split);
            nuint xid = Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!);
            var input = Task.Run(async () =>
            {
                try
                {
                    using var x = new OwnedX11(xid);
                    await Task.Delay(300);
                    x.Focus();
                    x.Key(0xff09); // Tab enters the divider before the editor.
                    await Task.Delay(80);
                    await window.Dispatcher.InvokeAsync(() => Assert.Same(Assert.Single(split.Children, e => e.Focusable), window.FocusedElement));
                    x.Move(184, 80); x.Button(true);
                    await Task.Delay(100);
                    x.Move(234, 80);
                    await Task.Delay(120);
                    x.Button(false);
                    await Task.Delay(80);
                    await window.Dispatcher.InvokeAsync(() => Assert.Equal(230, split.Position));
                    x.Key(0xff53); // Right
                    await Task.Delay(80);
                    await window.Dispatcher.InvokeAsync(() => Assert.Equal(240, split.Position));
                    x.Key(0xff50); // Home
                    await Task.Delay(80);
                    await window.Dispatcher.InvokeAsync(() => Assert.Equal(80, split.Position));
                    x.Key(0xff57); // End
                    await Task.Delay(80);
                    await window.Dispatcher.InvokeAsync(() => Assert.Equal(422, split.Position));
                    x.Key(0xff0d); // Enter reset
                    await Task.Delay(80);
                    await window.Dispatcher.InvokeAsync(() => Assert.Equal(180, split.Position));
                    // Drag the browser scrollbar, not the adjacent divider.
                    x.Move(175, 15); x.Button(true);
                    await Task.Delay(80);
                    x.Move(175, 110);
                    await Task.Delay(100);
                    x.Button(false);
                    await Task.Delay(80);
                    await window.Dispatcher.InvokeAsync(() => { Assert.Equal(180, split.Position); Assert.True(scroll.Scroll > 0); });
                    x.Resize(150, 300);
                    await Task.Delay(120);
                    await window.Dispatcher.InvokeAsync(() => { Assert.Equal(180, split.Position); Assert.InRange(split.VisiblePosition, 66, 68); });
                    x.Resize(520, 300);
                    await Task.Delay(120);
                    await window.Dispatcher.InvokeAsync(() => Assert.Equal(180, split.VisiblePosition));
                    x.Move(184, 80); x.Button(true);
                    await Task.Delay(80);
                    await window.Dispatcher.InvokeAsync(() =>
                    {
                        Assert.NotNull(Capture(window));
                        if (transition == "hide") split.Visible = false;
                        else if (transition == "disable") split.Enabled = false;
                        else if (transition == "unmount") window.RemoveChild(split);
                        else split.Dispose();
                        Assert.Null(Capture(window));
                        if (transition == "hide") split.Visible = true;
                        else if (transition == "disable") split.Enabled = true;
                        else if (transition == "unmount") { window.AddChild(split); window.SetAutoLayout(split); }
                    });
                    x.Move(310, 80);
                    await Task.Delay(100);
                    x.Button(false);
                    await window.Dispatcher.InvokeAsync(() => Assert.Equal(180, split.Position));
                    if (transition == "unmount") await window.Dispatcher.InvokeAsync(split.Dispose);
                    x.CloseNormally();
                }
                finally { window.Close(); }
            });
            window.Run();
            input.GetAwaiter().GetResult();
        });
    }

    private static object? Capture(Window window) => typeof(Window).GetField("_capturedElement", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);

    // No process-global X11 display: this connection uses only the private
    // DISPLAY supplied by the owning test launcher.
    private sealed class OwnedX11 : IDisposable
    {
        private readonly nint _display;
        private readonly nuint _window;
        internal OwnedX11(nuint window)
        {
            Assert.False(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")), "Use an owned Xvfb display.");
            _window = window;
            _display = XOpenDisplay(Environment.GetEnvironmentVariable("DISPLAY")!);
            Assert.NotEqual(0, _display);
        }
        internal void Focus() { XSetInputFocus(_display, _window, 1, 0); XSync(_display, 0); }
        internal void Move(int x, int y) { XWarpPointer(_display, 0, _window, 0, 0, 0, 0, x, y); XSync(_display, 0); }
        internal void Button(bool down) { Assert.NotEqual(0, XTestFakeButtonEvent(_display, 1, down ? 1 : 0, 0)); XSync(_display, 0); }
        internal void Key(nuint symbol)
        {
            uint key = XKeysymToKeycode(_display, symbol);
            Assert.NotEqual(0u, key);
            XTestFakeKeyEvent(_display, key, 1, 0); XTestFakeKeyEvent(_display, key, 0, 30); XSync(_display, 0);
        }
        internal void Resize(uint width, uint height) { XResizeWindow(_display, _window, width, height); XSync(_display, 0); }
        internal void CloseNormally()
        {
            var ev = new ClientMessage
            {
                Type = 33, Display = _display, Window = _window, Format = 32,
                MessageType = XInternAtom(_display, "WM_PROTOCOLS", 0), Data0 = (nint)XInternAtom(_display, "WM_DELETE_WINDOW", 0)
            };
            Assert.NotEqual(0, XSendEvent(_display, _window, 0, 0, ref ev)); XSync(_display, 0);
        }
        public void Dispose() => XCloseDisplay(_display);
    }

    [StructLayout(LayoutKind.Sequential, Size = 192)]
    private struct ClientMessage
    {
        internal int Type;
        internal nuint Serial;
        internal int SendEvent;
        internal nint Display;
        internal nuint Window, MessageType;
        internal int Format;
        internal nint Data0, Data1, Data2, Data3, Data4;
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
