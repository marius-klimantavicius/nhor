using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Glfw;

namespace Marius.Winter;

// An unmapped GLX drawable need not retain a visible front buffer. Use the
// completed first frame as the X11 map background, then restore GLFW's None
// background immediately after ShowWindow. Later frames use normal GL swaps.
internal sealed unsafe class X11StartupBackground(nint display, nuint window) : IDisposable
{
    internal static X11StartupBackground? Prepare(GlfwWindow window, int width, int height)
    {
        if (Glfw.Glfw.glfwGetPlatform() != GLFW.GLFW_PLATFORM_X11) return null;
        nint display = Glfw.Glfw.glfwGetX11Display();
        nuint handle = Glfw.Glfw.glfwGetX11Window(window);
        if (XGetWindowAttributes(display, handle, out var attributes) == 0)
            throw new InvalidOperationException("Winter could not inspect the X11 startup drawable.");

        var pixels = ReadBackBuffer(width, height);
        XImage* image = XCreateImage(display, attributes.visual, (uint)attributes.depth, 2, 0, null, (uint)width, (uint)height, 32, 0);
        if (image == null) throw new InvalidOperationException("Winter could not allocate the X11 startup image.");
        nuint pixmap = 0;
        nint gc = 0;
        try
        {
            image->Data = NativeMemory.AllocZeroed(checked((nuint)image->BytesPerLine * (nuint)height));
            if (image->Data == null) throw new OutOfMemoryException();
            nuint alphaMask = uint.MaxValue >> (32 - attributes.depth);
            alphaMask &= ~(image->RedMask | image->GreenMask | image->BlueMask);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    uint p = pixels[(height - 1 - y) * width + x];
                    // XPutPixel handles the visual's byte order and pixel size,
                    // including visuals whose RGB masks differ from BGRA8888.
                    nuint pixel = Channel((byte)(p >> 16), image->RedMask) |
                                  Channel((byte)(p >> 8), image->GreenMask) |
                                  Channel((byte)p, image->BlueMask) |
                                  Channel((byte)(p >> 24), alphaMask);
                    XPutPixel(image, x, y, pixel);
                }
            pixmap = XCreatePixmap(display, handle, (uint)width, (uint)height, (uint)attributes.depth);
            if (pixmap == 0) throw new InvalidOperationException("Winter could not allocate the X11 startup pixmap.");
            gc = XCreateGC(display, pixmap, 0, 0);
            if (gc == 0) throw new InvalidOperationException("Winter could not allocate the X11 startup graphics context.");
            XPutImage(display, pixmap, gc, image, 0, 0, 0, 0, (uint)width, (uint)height);
            XSetWindowBackgroundPixmap(display, handle, pixmap);
            // The window now retains the server pixmap; the explicit reference,
            // image and GC can all be freed before the window becomes visible.
            return new X11StartupBackground(display, handle);
        }
        finally
        {
            if (gc != 0) XFreeGC(display, gc);
            if (pixmap != 0) XFreePixmap(display, pixmap);
            XDestroyImage(image); // frees both the native image and its data
        }
    }

    private static nuint Channel(byte value, nuint mask)
    {
        if (mask == 0) return 0;
        int shift = BitOperations.TrailingZeroCount((ulong)mask);
        return ((nuint)value * (mask >> shift) / 255) << shift;
    }

    private static uint[] ReadBackBuffer(int width, int height)
    {
        var get = (delegate* unmanaged[Cdecl]<uint, int*, void>)Glfw.Glfw.glfwGetProcAddress("glGetIntegerv");
        var bind = (delegate* unmanaged[Cdecl]<uint, uint, void>)Glfw.Glfw.glfwGetProcAddress("glBindFramebuffer");
        var readBuffer = (delegate* unmanaged[Cdecl]<uint, void>)Glfw.Glfw.glfwGetProcAddress("glReadBuffer");
        var read = (delegate* unmanaged[Cdecl]<int, int, int, int, uint, uint, void*, void>)Glfw.Glfw.glfwGetProcAddress("glReadPixels");
        int previousFramebuffer, previousBuffer;
        get(0x8CAA, &previousFramebuffer); // GL_READ_FRAMEBUFFER_BINDING
        bind(0x8CA8, 0); // GL_READ_FRAMEBUFFER: read the actual presentation target
        get(0x0C02, &previousBuffer); // GL_READ_BUFFER of the default framebuffer
        try
        {
            var pixels = new uint[checked(width * height)];
            readBuffer(0x0405); // GL_BACK, before swapping makes its contents undefined
            fixed (uint* data = pixels) read(0, 0, width, height, 0x80E1, 0x1401, data); // BGRA, unsigned byte
            return pixels;
        }
        finally
        {
            readBuffer((uint)previousBuffer);
            bind(0x8CA8, (uint)previousFramebuffer);
        }
    }

    public void Dispose() => XSetWindowBackgroundPixmap(display, window, 0);

    // Prefix of Xlib's allocated XImage; XDestroyImage owns the full structure.
    [StructLayout(LayoutKind.Sequential)]
    private struct XImage
    {
        internal int Width, Height, XOffset, Format;
        internal void* Data;
        internal int ByteOrder, BitmapUnit, BitmapBitOrder, BitmapPad, Depth, BytesPerLine, BitsPerPixel;
        internal nuint RedMask, GreenMask, BlueMask;
    }

    [DllImport("libX11.so.6")] private static extern int XGetWindowAttributes(nint display, nuint window, out XWindowAttributes attributes);
    [DllImport("libX11.so.6")] private static extern XImage* XCreateImage(nint display, nint visual, uint depth, int format, int offset, void* data, uint width, uint height, int bitmapPad, int bytesPerLine);
    [DllImport("libX11.so.6")] private static extern int XPutPixel(XImage* image, int x, int y, nuint pixel);
    [DllImport("libX11.so.6")] private static extern int XDestroyImage(XImage* image);
    [DllImport("libX11.so.6")] private static extern nuint XCreatePixmap(nint display, nuint drawable, uint width, uint height, uint depth);
    [DllImport("libX11.so.6")] private static extern int XFreePixmap(nint display, nuint pixmap);
    [DllImport("libX11.so.6")] private static extern nint XCreateGC(nint display, nuint drawable, nuint mask, nint values);
    [DllImport("libX11.so.6")] private static extern int XFreeGC(nint display, nint gc);
    [DllImport("libX11.so.6")] private static extern int XPutImage(nint display, nuint drawable, nint gc, XImage* image, int sourceX, int sourceY, int destinationX, int destinationY, uint width, uint height);
    [DllImport("libX11.so.6")] private static extern int XSetWindowBackgroundPixmap(nint display, nuint window, nuint pixmap);
}
