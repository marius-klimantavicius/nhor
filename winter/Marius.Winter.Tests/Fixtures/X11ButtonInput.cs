using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Marius.Winter.Tests;

// Like xdotool, send real XTest input over a separate X11 connection. The test
// command must own DISPLAY (Xvfb), including for hidden Window construction.
internal static class X11ButtonInput
{
    internal static Task Click(nuint window) => Task.Run(() =>
    {
        Assert.True(OperatingSystem.IsLinux(), "This native-input fixture requires Linux/X11 and an owned Xvfb display.");
        string? displayName = Environment.GetEnvironmentVariable("DISPLAY");
        Assert.False(string.IsNullOrEmpty(displayName), "Run native tests under an owned Xvfb display.");
        nint display = XOpenDisplay(displayName!);
        Assert.NotEqual(0, display);
        nuint root = XDefaultRootWindow(display);
        int originalX = 0, originalY = 0;
        bool restorePointer = false;
        try
        {
            Assert.NotEqual(0, XQueryPointer(display, root, out _, out _, out originalX, out originalY, out _, out _, out _));
            restorePointer = true;
            Assert.NotEqual((nuint)0, window);
            Assert.NotEqual(0, XTestQueryExtension(display, out _, out _, out _, out _));
            Thread.Sleep(600); // Let Run finish its initial frames and become idle.
            XWarpPointer(display, 0, window, 0, 0, 0, 0, 45, 25);
            XSync(display, 0);
            Thread.Sleep(200);
            Assert.NotEqual(0, XTestFakeButtonEvent(display, 1, 1, 0));
            XSync(display, 0);
            Thread.Sleep(300); // Ordinary press/release, not an instantaneous click.
            Assert.NotEqual(0, XTestFakeButtonEvent(display, 1, 0, 0));
            XSync(display, 0);
        }
        finally
        {
            // Do not change pointer/focus behavior for later native tests on a
            // persistent Xvfb server, even when an assertion interrupts input.
            if (restorePointer) XWarpPointer(display, 0, root, 0, 0, 0, 0, originalX, originalY);
            XSync(display, 0);
            XCloseDisplay(display);
        }
    });

    [DllImport("libX11.so.6")] private static extern nint XOpenDisplay(string display);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(nint display);
    [DllImport("libX11.so.6")] private static extern int XSync(nint display, int discard);
    [DllImport("libX11.so.6")] private static extern nuint XDefaultRootWindow(nint display);
    [DllImport("libX11.so.6")] private static extern int XQueryPointer(nint display, nuint window,
        out nuint root, out nuint child, out int rootX, out int rootY, out int windowX, out int windowY, out uint mask);
    [DllImport("libX11.so.6")] private static extern int XWarpPointer(nint display, nuint source, nuint destination,
        int sourceX, int sourceY, uint sourceWidth, uint sourceHeight, int destinationX, int destinationY);
    [DllImport("libXtst.so.6")] private static extern int XTestQueryExtension(nint display,
        out int eventBase, out int errorBase, out int majorVersion, out int minorVersion);
    [DllImport("libXtst.so.6")] private static extern int XTestFakeButtonEvent(nint display, uint button, int press, nuint delay);
}
