using System;
using System.Runtime.InteropServices;
using Xunit;

namespace Marius.Winter.Blazor.Tests;

internal sealed class MenuBindingX11 : IDisposable
{
    private readonly nint _display;
    private readonly nuint _window;
    internal MenuBindingX11(nuint window)
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
