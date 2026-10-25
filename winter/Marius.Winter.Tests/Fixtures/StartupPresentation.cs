using System;
using System.Runtime.InteropServices;
using Glfw;
using Xunit;

namespace Marius.Winter.Tests;

// Observe the real X11 map and GL swap, forwarding both native operations.
// Unlike a later screenshot, the map sample cannot miss an exposed empty buffer.
internal sealed unsafe class StartupPresentation : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int MapWindow(nint display, nuint window);
    private readonly Window _window;
    private readonly GlfwLibraryX11 _x11;
    private readonly delegate* unmanaged[Cdecl]<nint, nuint, int> _map;
    private readonly Action<GlfwWindow> _swap;
    private readonly MapWindow _observeMap;
    private readonly GlfwWindow _native;
    private readonly int _width, _height;
    internal uint[]? MappedPixels { get; private set; }
    internal uint[]? FirstDrawPixels { get; private set; }
    internal int SwapsAtMap { get; private set; }
    internal int SwapCount { get; private set; }
    internal Action? Mapped;
    internal Action? Swapped;
    internal Exception? Failure { get; private set; }

    internal StartupPresentation(Window window)
    {
        Assert.True(OperatingSystem.IsLinux(), "Run this X11 presentation regression on an owned Xvfb display.");
        _window = window;
        _native = window.GlfwWindow!;
        _width = (int)window.FramebufferWidth;
        _height = (int)window.FramebufferHeight;
        _x11 = Glfw._glfw.X11!;
        Assert.NotNull(_x11);
        _map = _x11.xlib.MapWindow;
        _swap = _native.context.swapBuffers!;
        _observeMap = ObserveMap;
        _x11.xlib.MapWindow = (delegate* unmanaged[Cdecl]<nint, nuint, int>)Marshal.GetFunctionPointerForDelegate(_observeMap);
        _native.context.swapBuffers = ObserveSwap;
    }

    private int ObserveMap(nint display, nuint window)
    {
        int result = _map(display, window);
        // Never unwind a managed assertion through an unmanaged callback.
        try
        {
            XSync(display, 0);
            nint image = XGetImage(display, window, 0, 0, (uint)_width, (uint)_height, nuint.MaxValue, 2);
            Assert.NotEqual(0, image);
            try
            {
                var pixels = new uint[_width * _height];
                for (int y = 0; y < _height; y++)
                    for (int x = 0; x < _width; x++)
                        pixels[y * _width + x] = (uint)XGetPixel(image, x, y) & 0xffffff;
                MappedPixels = pixels;
                SwapsAtMap = SwapCount;
            }
            finally { XDestroyImage(image); }
            Mapped?.Invoke();
        }
        catch (Exception ex) { Failure = ex; _window.Close(); }
        return result;
    }

    private void ObserveSwap(GlfwWindow window)
    {
        FirstDrawPixels ??= ReadBackBuffer();
        _swap(window);
        SwapCount++;
        Swapped?.Invoke();
    }

    private uint[] ReadBackBuffer()
    {
        var finish = (delegate* unmanaged[Cdecl]<void>)Glfw.Glfw.glfwGetProcAddress("glFinish");
        var readBuffer = (delegate* unmanaged[Cdecl]<uint, void>)Glfw.Glfw.glfwGetProcAddress("glReadBuffer");
        var read = (delegate* unmanaged[Cdecl]<int, int, int, int, uint, uint, void*, void>)Glfw.Glfw.glfwGetProcAddress("glReadPixels");
        var error = (delegate* unmanaged[Cdecl]<uint>)Glfw.Glfw.glfwGetProcAddress("glGetError");
        var rgba = new uint[_width * _height];
        finish(); readBuffer(0x0405); // GL_BACK, before the real presentation
        fixed (uint* data = rgba) read(0, 0, _width, _height, 0x1908, 0x1401, data);
        Assert.Equal(0u, error());
        var rgb = new uint[rgba.Length];
        for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
            {
                uint p = rgba[(_height - 1 - y) * _width + x];
                rgb[y * _width + x] = ((p & 255) << 16) | (p & 0xff00) | ((p >> 16) & 255);
            }
        return rgb;
    }

    public void Dispose()
    {
        _x11.xlib.MapWindow = _map;
        _native.context.swapBuffers = _swap;
        GC.KeepAlive(_observeMap);
    }

    [DllImport("libX11.so.6")] private static extern int XSync(nint display, int discard);
    [DllImport("libX11.so.6")] private static extern nint XGetImage(nint display, nuint window, int x, int y, uint width, uint height, nuint planeMask, int format);
    [DllImport("libX11.so.6")] private static extern nuint XGetPixel(nint image, int x, int y);
    [DllImport("libX11.so.6")] private static extern int XDestroyImage(nint image);
}
