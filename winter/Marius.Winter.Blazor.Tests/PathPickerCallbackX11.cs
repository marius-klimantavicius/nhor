using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;
namespace Marius.Winter.Blazor.Tests;
internal sealed class C07X11 : IDisposable
{
    private readonly C07SelectionX11 _input;
    private readonly nint _display;
    private readonly nuint _window;
    internal C07X11(nuint window) { _window = window; _input = new C07SelectionX11(window); _display = XOpenDisplay(Environment.GetEnvironmentVariable("DISPLAY")!); Assert.NotEqual(0, _display); }
    internal void Focus() => _input.Focus();
    internal void Move(int x, int y) => _input.Move(x, y);
    internal void Click() => _input.Click();
    internal void Key(nuint symbol) => _input.Key(symbol);
    internal void Chord(nuint modifier, nuint key)
    {
        var code = XKeysymToKeycode(_display, modifier); XTestFakeKeyEvent(_display, code, 1, 0); XSync(_display, 0);
        Key(key); XTestFakeKeyEvent(_display, code, 0, 0); XSync(_display, 0);
    }
    internal void TypeAscii(string text) { foreach (char c in text) Key(c); }
    internal void TypeBurst(string text) => _input.TypeBurst(text);
    internal void Resize(uint w, uint h) => _input.Resize(w, h);
    internal void CloseNormally() => _input.CloseNormally();
    internal void Capture(string name, int width, int height)
    {
        var path = Environment.GetEnvironmentVariable("WINTER_C04_EVIDENCE");
        if (string.IsNullOrEmpty(path)) return;
        Directory.CreateDirectory(path);
        var image = XGetImage(_display, _window, 0, 0, (uint)width, (uint)height, nuint.MaxValue, 2); Assert.NotEqual(0, image);
        try
        {
            var info = Marshal.PtrToStructure<XImage>(image); Assert.Equal(32, info.BitsPerPixel);
            var bytes = new byte[info.BytesPerLine * height]; Marshal.Copy(info.Data, bytes, 0, bytes.Length);
            using var stream = File.Create(Path.Combine(path, name + ".ppm"));
            var header = System.Text.Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n"); stream.Write(header);
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) { var i = y * info.BytesPerLine + x * 4; stream.WriteByte(bytes[i + 2]); stream.WriteByte(bytes[i + 1]); stream.WriteByte(bytes[i]); }
        }
        finally { XDestroyImage(image); }
    }
    public void Dispose() { _input.Dispose(); XCloseDisplay(_display); }
    [StructLayout(LayoutKind.Sequential)] private struct XImage
    {
        public int Width, Height, XOffset, Format; public nint Data;
        public int ByteOrder, BitmapUnit, BitmapBitOrder, BitmapPad, Depth, BytesPerLine, BitsPerPixel;
    }
    [DllImport("libX11.so.6")] private static extern nint XOpenDisplay(string name);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(nint display);
    [DllImport("libX11.so.6")] private static extern int XSync(nint display, int discard);
    [DllImport("libX11.so.6")] private static extern byte XKeysymToKeycode(nint display, nuint symbol);
    [DllImport("libXtst.so.6")] private static extern int XTestFakeKeyEvent(nint display, uint key, int press, nuint delay);
    [DllImport("libX11.so.6")] private static extern nint XGetImage(nint display, nuint window, int x, int y, uint width, uint height, nuint planeMask, int format);
    [DllImport("libX11.so.6")] private static extern int XDestroyImage(nint image);
}

internal sealed class C07SelectionX11 : IDisposable
{
    private readonly nint _display;
    private readonly nuint _window;
    internal C07SelectionX11(nuint window)
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
    internal void TypeBurst(string text)
    {
        foreach (var c in text)
        {
            var code = XKeysymToKeycode(_display, c); Assert.NotEqual(0, code);
            XTestFakeKeyEvent(_display, code, 1, 0); XTestFakeKeyEvent(_display, code, 0, 0);
        }
        XSync(_display, 0);
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
