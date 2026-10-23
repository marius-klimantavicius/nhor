using System;
using System.Runtime.InteropServices;
using System.Threading;
using G = Glfw.Glfw;
using static Glfw.GLFW;

internal static unsafe partial class Program
{
    private static void ValidateScaledDisplay()
    {
        InitHint(GLFW_PLATFORM, GLFW_PLATFORM_X11); Check(Init() == 1, "scaled init");
        Defaults(); Hint(GLFW_VISIBLE, 0); Hint(GLFW_CLIENT_API, GLFW_NO_API); Hint(GLFW_SCALE_TO_MONITOR, 1);
        var w = Create(100, 80)!;
        Size(w, out int width, out int height);
        Record("initial-window-size", new[] { width, height });
        int fw, fh; float sx, sy;
        if (native) { N.glfwGetFramebufferSize(w.Handle, out fw, out fh); N.glfwGetWindowContentScale(w.Handle, out sx, out sy); }
        else { G.glfwGetFramebufferSize(w.Managed!, out fw, out fh); G.glfwGetWindowContentScale(w.Managed!, out sx, out sy); }
        Record("initial-framebuffer-size", new[] { fw, fh }); Record("initial-content-scale", new[] { sx, sy });
        Check(width == 150 && height == 120 && fw == 150 && fh == 120 && sx == 1.5f && sy == 1.5f, "X11 initial scale-to-monitor contract");
        double x = -1, y = -1;
        N.CursorFun? callback = null; Glfw.GlfwCursorPosFun? managedCallback = null;
        if (native) { callback = (_, a, b) => { x = a; y = b; }; N.glfwSetCursorPosCallback(w.Handle, callback); }
        else { managedCallback = (_, a, b) => { x = a; y = b; }; G.glfwSetCursorPosCallback(w.Managed!, managedCallback); }
        nint display = native ? N.glfwGetX11Display() : G.glfwGetX11Display();
        nuint window = native ? N.glfwGetX11Window(w.Handle) : G.glfwGetX11Window(w.Managed!);
        Glfw.XEvent ev = default; ev.xmotion = new Glfw.XMotionEvent { type = 6, display = display, window = window, root = XDefaultRootWindow(display), x = 17, y = 23, same_screen = 1 };
        XSendEvent(display, window, 0, 1 << 6, &ev); XSync(display, 0);
        for (int i = 0; i < 10; i++) { Poll(); if (x >= 0) break; Thread.Sleep(5); }
        Check(x == 17 && y == 23, "cursor event remains in native window coordinates");
        Record("cursor-window-coordinates", new[] { x, y });
        if (native) { N.glfwShowWindow(w.Handle); N.glfwFocusWindow(w.Handle); }
        else { G.glfwShowWindow(w.Managed!); G.glfwFocusWindow(w.Managed!); }
        XSync(display, 0); Poll();
        InputMode(w, GLFW_CURSOR, GLFW_CURSOR_CAPTURED);
        XWarpPointer(display, 0, XDefaultRootWindow(display), 0, 0, 0, 0, 700, 500); XSync(display, 0); Poll();
        double px, py;
        if (native) N.glfwGetCursorPos(w.Handle, out px, out py); else G.glfwGetCursorPos(w.Managed!, out px, out py);
        Check(px >= 0 && py >= 0 && px < width && py < height, "captured cursor is confined by X11");
        Record("captured-cursor-confined", true);
        InputMode(w, GLFW_CURSOR, GLFW_CURSOR_NORMAL);
        XWarpPointer(display, 0, XDefaultRootWindow(display), 0, 0, 0, 0, 700, 500); XSync(display, 0); Poll();
        if (native) N.glfwGetCursorPos(w.Handle, out px, out py); else G.glfwGetCursorPos(w.Managed!, out px, out py);
        Check(px >= width || py >= height, "released cursor can leave the window");
        Record("released-cursor-outside", true);
        int scan = native ? N.glfwGetKeyScancode(GLFW_KEY_A) : G.glfwGetKeyScancode(GLFW_KEY_A);
        ev = default; ev.xkey = new Glfw.XKeyEvent { type = 2, display = display, window = window, root = XDefaultRootWindow(display), keycode = (uint)scan, time = 200, same_screen = 1 };
        XSendEvent(display, window, 0, 1, &ev);
        ev = default; ev.xbutton = new Glfw.XButtonEvent { type = 4, display = display, window = window, root = XDefaultRootWindow(display), button = 1, same_screen = 1 };
        XSendEvent(display, window, 0, 4, &ev); XSync(display, 0); Poll();
        Check((native ? N.glfwGetKey(w.Handle, GLFW_KEY_A) : G.glfwGetKey(w.Managed!, GLFW_KEY_A)) == 1, "key is held before focus loss");
        Check((native ? N.glfwGetMouseButton(w.Handle, 0) : G.glfwGetMouseButton(w.Managed!, 0)) == 1, "mouse is held before focus loss");
        var second = Create(40, 40)!;
        if (native) { N.glfwShowWindow(second.Handle); N.glfwFocusWindow(second.Handle); }
        else { G.glfwShowWindow(second.Managed!); G.glfwFocusWindow(second.Managed!); }
        XSync(display, 0); Poll();
        Check((native ? N.glfwGetKey(w.Handle, GLFW_KEY_A) : G.glfwGetKey(w.Managed!, GLFW_KEY_A)) == 0, "focus loss releases key");
        Check((native ? N.glfwGetMouseButton(w.Handle, 0) : G.glfwGetMouseButton(w.Managed!, 0)) == 0, "focus loss releases mouse");
        Record("focus-loss-releases-input", true); Destroy(second);
        Destroy(w); Terminate(); GC.KeepAlive(callback); GC.KeepAlive(managedCallback);
    }
    [DllImport("libX11.so.6")] private static extern int XWarpPointer(nint display, nuint source, nuint destination, int x, int y, uint width, uint height, int dx, int dy);
    private static partial class N
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void CursorFun(nint window, double x, double y);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwSetCursorPosCallback(nint window, CursorFun callback);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwGetFramebufferSize(nint window, out int width, out int height);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwShowWindow(nint window);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwFocusWindow(nint window);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwGetCursorPos(nint window, out double x, out double y);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int glfwGetKey(nint window, int key);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int glfwGetMouseButton(nint window, int button);
    }
}
