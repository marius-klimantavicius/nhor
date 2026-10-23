using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using G = Glfw.Glfw;
using static Glfw.GLFW;

internal static unsafe partial class Program
{
    private static void ValidateJoysticks()
    {
        foreach (int jid in new[] { -1, 16 })
        {
            Record("joystick-invalid-present-" + jid, native ? N.glfwJoystickPresent(jid) != 0 : G.glfwJoystickPresent(jid));
            Record("joystick-invalid-error-" + jid, Error());
        }
        int connected = 0;
        for (int jid = 0; jid <= GLFW_JOYSTICK_LAST; jid++)
        {
            bool present = native ? N.glfwJoystickPresent(jid) != 0 : G.glfwJoystickPresent(jid);
            if (present) connected++;
            int count;
            bool axes = native ? N.glfwGetJoystickAxes(jid, out count) != 0 : G.glfwGetJoystickAxes(jid, out count) != null;
            Record("joystick-axes-" + jid, new[] { axes ? 1 : 0, count });
            bool buttons = native ? N.glfwGetJoystickButtons(jid, out count) != 0 : G.glfwGetJoystickButtons(jid, out count) != null;
            Record("joystick-buttons-" + jid, new[] { buttons ? 1 : 0, count });
            bool hats = native ? N.glfwGetJoystickHats(jid, out count) != 0 : G.glfwGetJoystickHats(jid, out count) != null;
            Record("joystick-hats-" + jid, new[] { hats ? 1 : 0, count });
            Record("joystick-name-" + jid, native ? Marshal.PtrToStringUTF8(N.glfwGetJoystickName(jid)) : G.glfwGetJoystickName(jid));
            Record("joystick-guid-" + jid, native ? Marshal.PtrToStringUTF8(N.glfwGetJoystickGUID(jid)) : G.glfwGetJoystickGUID(jid));
            Record("joystick-gamepad-" + jid, native ? N.glfwJoystickIsGamepad(jid) != 0 : G.glfwJoystickIsGamepad(jid));
        }
        Record("joysticks-present", connected);
        foreach (string mapping in new[] { "# ignored\n", "abc,name,a:b0,", "00000000000000000000000000000000,Example,a:b0,platform:Linux,", "00000000000000000000000000000000,Example,+a:b0,", "00000000000000000000000000000000,Example,a:+a0,leftx:-a0~,dpup:h0.1," })
        {
            Record("mapping-update-" + mapping, native ? N.glfwUpdateGamepadMappings(mapping) != 0 : G.glfwUpdateGamepadMappings(mapping));
            Record("mapping-update-error-" + mapping, Error());
        }
    }

    private static void ValidateLifecycleThreads()
    {
        // A live worker's error slot must be recreated across termination, as
        // deleting/recreating the native TLS key does. No context is owned here.
        using var first = new ManualResetEventSlim();
        using var next = new ManualResetEventSlim();
        int after = -1;
        var worker = new Thread(() => { _ = Proc("glClear"); first.Set(); next.Wait(); after = Error(); });
        worker.Start(); first.Wait();
        Terminate();
        Check(Init() == 1, "reinitialize with live worker");
        next.Set(); worker.Join();
        Record("worker-error-after-reinit", after);
        // The window passed by RunCore was deliberately destroyed by terminate;
        // call sites invoke this scenario after they have finished using it.
    }

    private static void ValidateEvents(Window window)
    {
        nint display = native ? N.glfwGetX11Display() : G.glfwGetX11Display();
        nuint xid = native ? N.glfwGetX11Window(window.Handle) : G.glfwGetX11Window(window.Managed!);
        Check(display != 0 && xid != 0, "X11 native access");
        var events = new List<string>();
        N.KeyFun? key = null; N.MouseFun? mouse = null; N.ScrollFun? scroll = null; N.CharFun? chars = null;
        Glfw.GlfwKeyFun? mkey = null; Glfw.GlfwMouseButtonFun? mmouse = null; Glfw.GlfwScrollFun? mscroll = null; Glfw.GlfwCharFun? mchars = null;
        if (native)
        {
            key = (_, k, _, a, m) => events.Add($"key:{k}:{a}:{m}");
            mouse = (_, b, a, m) => events.Add($"mouse:{b}:{a}:{m}");
            scroll = (_, x, y) => events.Add($"scroll:{x}:{y}");
            chars = (_, c) => events.Add($"char:{c}");
            N.glfwSetKeyCallback(window.Handle, key); N.glfwSetMouseButtonCallback(window.Handle, mouse); N.glfwSetScrollCallback(window.Handle, scroll); N.glfwSetCharCallback(window.Handle, chars);
        }
        else
        {
            mkey = (_, k, _, a, m) => events.Add($"key:{k}:{a}:{m}");
            mmouse = (_, b, a, m) => events.Add($"mouse:{b}:{a}:{m}");
            mscroll = (_, x, y) => events.Add($"scroll:{x}:{y}");
            mchars = (_, c) => events.Add($"char:{c}");
            G.glfwSetKeyCallback(window.Managed!, mkey); G.glfwSetMouseButtonCallback(window.Managed!, mmouse); G.glfwSetScrollCallback(window.Managed!, mscroll); G.glfwSetCharCallback(window.Managed!, mchars);
        }
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        int scancode = native ? N.glfwGetKeyScancode(GLFW_KEY_A) : G.glfwGetKeyScancode(GLFW_KEY_A);
        void SendKey(int type, nuint time)
        {
            Glfw.XEvent ev = default;
            ev.xkey = new Glfw.XKeyEvent { type = type, display = display, window = xid, root = XDefaultRootWindow(display), keycode = (uint)scancode, time = time, same_screen = 1 };
            XSendEvent(display, xid, 0, type == 2 ? 1 : 2, &ev); XSync(display, 0); Poll();
            // Input methods can deliver committed text asynchronously.
            for (int i = 0; i < 20; i++) { Thread.Sleep(5); Poll(); }
        }
        SendKey(2, 100); SendKey(2, 110); SendKey(3, 130);
        foreach (uint button in new uint[] { 1, 4, 6, 13 })
        {
            Glfw.XEvent ev = default;
            ev.xbutton = new Glfw.XButtonEvent { type = 4, display = display, window = xid, root = XDefaultRootWindow(display), button = button, same_screen = 1 };
            XSendEvent(display, xid, 0, 4, &ev); XSync(display, 0); Poll();
        }
        Record("x11-input-events", events);
        Record("x11-event-error", Error());
        Check(events.Contains("key:65:1:0") && events.Contains("key:65:2:0") && events.Contains("key:65:0:0"), "key callback ordering and repeat");
        Check(events.Contains("mouse:8:1:0"), "unlimited mouse callback");
        Check(native || window.Managed!.X11!.ic != 0, "real XIM input context");
        GC.KeepAlive(key); GC.KeepAlive(mouse); GC.KeepAlive(scroll); GC.KeepAlive(chars); GC.KeepAlive(mkey); GC.KeepAlive(mmouse); GC.KeepAlive(mscroll); GC.KeepAlive(mchars);
    }

    [DllImport("libX11.so.6")] private static extern int XSendEvent(nint display, nuint window, int propagate, nint mask, Glfw.XEvent* ev);
    [DllImport("libX11.so.6")] private static extern int XSync(nint display, int discard);
    [DllImport("libX11.so.6")] private static extern nuint XDefaultRootWindow(nint display);
    private static partial class N
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void KeyFun(nint window, int key, int scan, int action, int mods);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void MouseFun(nint window, int button, int action, int mods);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void ScrollFun(nint window, double x, double y);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void CharFun(nint window, uint codepoint);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int glfwJoystickPresent(int jid);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwGetJoystickAxes(int jid, out int count);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwGetJoystickButtons(int jid, out int count);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwGetJoystickHats(int jid, out int count);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwGetJoystickName(int jid);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwGetJoystickGUID(int jid);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int glfwJoystickIsGamepad(int jid);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int glfwUpdateGamepadMappings([MarshalAs(UnmanagedType.LPUTF8Str)] string text);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwGetX11Display();
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nuint glfwGetX11Window(nint window);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int glfwGetKeyScancode(int key);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwSetKeyCallback(nint window, KeyFun callback);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwSetMouseButtonCallback(nint window, MouseFun callback);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwSetScrollCallback(nint window, ScrollFun callback);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwSetCharCallback(nint window, CharFun callback);
    }
}
