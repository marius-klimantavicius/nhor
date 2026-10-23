using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;
using G = Glfw.Glfw;
using static Glfw.GLFW;

internal static unsafe partial class Program
{
    [DllImport("libc", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint setlocale(int category, string? locale);
    private static string CType() => Marshal.PtrToStringUTF8(setlocale(0, null))!;
    private static int[] Mode(Glfw.GlfwVidMode v) => new[] { v.Width, v.Height, v.RedBits, v.GreenBits, v.BlueBits, v.RefreshRate };

    private static void ValidateCurrentMode()
    {
        InitHint(GLFW_PLATFORM, GLFW_PLATFORM_X11);
        Check(Init() == 1, "monitor initialization");
        int[] mode;
        if (native)
        {
            nint monitor = N.glfwGetPrimaryMonitor();
            Check(monitor != 0, "primary monitor");
            var value = N.glfwGetVideoMode(monitor);
            Check(value != null, "current mode");
            mode = new[] { value->Width, value->Height, value->Red, value->Green, value->Blue, value->Refresh };
        }
        else
        {
            var monitor = G.glfwGetPrimaryMonitor();
            Check(monitor != null, "primary monitor");
            var value = G.glfwGetVideoMode(monitor!);
            Check(value.HasValue, "current mode");
            mode = Mode(value!.Value);
        }
        Check(mode[0] > 0 && mode[1] > 0, "real current dimensions");
        Check(Error() == 0, "current mode error");
        Record("current-mode", mode); // Runner compares all six values exactly.
    }

    private static void ValidateContextReinitialization()
    {
        InitHint(GLFW_PLATFORM, OperatingSystem.IsWindows() ? GLFW_PLATFORM_WIN32 : GLFW_PLATFORM_X11);
        byte* pixel = stackalloc byte[4];
        for (int cycle = 0; cycle < 3; cycle++)
        {
            Check(Init() == 1, "context reinitialization");
            Hint(GLFW_VISIBLE, 0); Hint(GLFW_CONTEXT_VERSION_MAJOR, 3); Hint(GLFW_CONTEXT_VERSION_MINOR, 3);
            var window = Create() ?? throw new Exception("context after reinitialization");
            Current(window);
            nint clearAddress = Proc("glClear"), colorAddress = Proc("glClearColor"), readAddress = Proc("glReadPixels");
            Check(clearAddress != 0 && colorAddress != 0 && readAddress != 0, "procedures after module reload");
            ((delegate* unmanaged<float, float, float, float, void>)colorAddress)(1, 0, 1, 1);
            ((delegate* unmanaged<uint, void>)clearAddress)(0x4000);
            ((delegate* unmanaged<int, int, int, int, uint, uint, void*, void>)readAddress)(0, 0, 1, 1, 0x1908, 0x1401, pixel);
            Check(pixel[0] == 255 && pixel[1] == 0 && pixel[2] == 255 && pixel[3] == 255, "render after module reload");
            if (native) N.glfwSwapBuffers(window.Handle); else G.glfwSwapBuffers(window.Managed!);
            Check(Error() == 0, "context cycle error");
            Current(null); Destroy(window); Terminate();
            if (!native && (Glfw._glfw.glx != null || Glfw._glfw.wgl != null)) throw new Exception("stale context extension table after termination");
        }
        Record("context-init-render-cycles", 3);
    }

    // Drive the PUBLIC current-mode API through the real X11 platform dispatch,
    // supplying deterministic RandR responses without changing a screen mode.
    private static G.XRRScreenResources* modeResources;
    private static G.XRRCrtcInfo* modeCrtc;
    private static bool missingCrtc;
    private static int modeResourcesFreed, modeCrtcsFreed;
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static nint ModeResources(nint display, nuint root) => (nint)modeResources;
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static nint ModeCrtc(nint display, nint resources, nuint crtc) => missingCrtc ? 0 : (nint)modeCrtc;
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void FreeModeResources(nint value) => modeResourcesFreed++;
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void FreeModeCrtc(nint value) => modeCrtcsFreed++;
    private static void ValidateModeDispatch()
    {
        Check(!native, "managed deterministic dispatch fixture");
        InitHint(GLFW_PLATFORM, GLFW_PLATFORM_X11); Check(Init() == 1, "dispatch init");
        var monitor = G.glfwGetPrimaryMonitor()!;
        var randr = Glfw._glfw.X11!.randr;
        var getResources = randr.GetScreenResourcesCurrent; var getCrtc = randr.GetCrtcInfo;
        var freeResources = randr.FreeScreenResources; var freeCrtc = randr.FreeCrtcInfo;
        bool available = randr.available, broken = randr.monitorBroken;
        G.XRRModeInfo mi = new() { id = 7, width = 800, height = 600, dotClock = 121, hTotal = 2, vTotal = 1 };
        G.XRRScreenResources resources = new() { nmode = 1, modes = &mi };
        G.XRRCrtcInfo ci = new() { mode = 7 };
        modeResources = &resources; modeCrtc = &ci;
        try
        {
            randr.available = true; randr.monitorBroken = false;
            randr.GetScreenResourcesCurrent = &ModeResources; randr.GetCrtcInfo = &ModeCrtc;
            randr.FreeScreenResources = &FreeModeResources; randr.FreeCrtcInfo = &FreeModeCrtc;
            foreach (ushort rotation in new ushort[] { 1, 2, 4, 8, 17, 18 })
            {
                ci.rotation = rotation;
                var mode = G.glfwGetVideoMode(monitor);
                Check(mode.HasValue, "rotation result");
                bool swap = rotation == 2 || rotation == 8; // Pinned upstream equality, including reflection combinations.
                Check(mode!.Value.Width == (swap ? 600 : 800) && mode.Value.Height == (swap ? 800 : 600), "RandR rotation " + rotation);
                Check(mode.Value.RefreshRate == 61, "C round tie away from zero");
                Record("rotation-" + rotation, Mode(mode.Value));
            }
            ci.mode = 8;
            Check(G.glfwGetVideoMode(monitor) == null, "missing mode returns failure");
            Check(G.glfwGetError(out string? message) == GLFW_PLATFORM_ERROR && message == "X11: Failed to query video mode", "missing mode exact error");
            missingCrtc = true;
            Check(G.glfwGetVideoMode(monitor) == null, "missing CRTC returns failure");
            Check(G.glfwGetError(out _) == GLFW_PLATFORM_ERROR, "missing CRTC error");
            Check(modeResourcesFreed == 8 && modeCrtcsFreed == 7, "RandR response cleanup once, including failures");
            Record("resources-freed", modeResourcesFreed); Record("crtcs-freed", modeCrtcsFreed);
        }
        finally
        {
            randr.available = available; randr.monitorBroken = broken;
            randr.GetScreenResourcesCurrent = getResources; randr.GetCrtcInfo = getCrtc;
            randr.FreeScreenResources = freeResources; randr.FreeCrtcInfo = freeCrtc;
            modeResources = null; modeCrtc = null; missingCrtc = false;
        }
    }

    [DllImport("libX11.so.6")] private static extern int XChangeKeyboardMapping(nint display, int code, int per, nuint* symbols, int count);
    private static void ValidateXim(string scenario)
    {
        // Key remapping is permitted ONLY on the test driver's owned server.
        Check(Environment.GetEnvironmentVariable("GLFW_OWNED_XVFB") == "1", "owned X server required");
        bool preserve = scenario == "xim-preserve";
        Check(setlocale(0, preserve ? "C.utf8" : "C") != 0, "select test locale");
        string before = CType();
        Record("locale-before", before);
        bool compose = scenario != "xim-direct";
        string expected = scenario switch
        {
            "xim-direct" => "😀Ž", "xim-ordinary" => "é", "xim-overflow" => string.Concat(Enumerable.Repeat("Ž日本😀", 16)), _ => "Ž日本😀"
        };
        var expectedScalars = expected.EnumerateRunes().Select(r => (uint)r.Value).ToArray();
        var received = new List<uint>();
        nuint* symbols = stackalloc nuint[] { 0x0101f600, 0x0100017d, 0 };
        if (compose) { symbols[0] = 0xff20; symbols[1] = 0x27; symbols[2] = 0x65; }
        for (int cycle = 0; cycle < 2; cycle++)
        {
            InitHint(GLFW_PLATFORM, GLFW_PLATFORM_X11); Check(Init() == 1, "XIM initialization");
            Check(CType() == (preserve ? before : "C.UTF-8"), "conditional LC_CTYPE initialization");
            nint display = native ? N.glfwGetX11Display() : G.glfwGetX11Display();
            XChangeKeyboardMapping(display, 240, 1, symbols, compose ? 3 : 2); XSync(display, 0);
            for (int instance = 0; instance < 3; instance++)
            {
                Hint(GLFW_CLIENT_API, GLFW_NO_API); Hint(GLFW_VISIBLE, 0);
                var window = Create() ?? throw new Exception("XIM window");
                nuint xid = native ? N.glfwGetX11Window(window.Handle) : G.glfwGetX11Window(window.Managed!);
                if (!native && (Glfw._glfw.X11!.im == 0 || window.Managed!.X11!.ic == 0)) throw new Exception("real XIM/IC required");
                N.CharFun callback = (_, cp) => received.Add(cp);
                Glfw.GlfwCharFun managedCallback = (_, cp) => received.Add(cp);
                if (native) N.glfwSetCharCallback(window.Handle, callback); else G.glfwSetCharCallback(window.Managed!, managedCallback);
                received.Clear();
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                for (uint code = 240; code < (compose ? 243 : 242); code++)
                {
                    for (int type = 2; type <= 3; type++)
                    {
                        Glfw.XEvent ev = default;
                        ev.xkey = new Glfw.XKeyEvent { type = type, display = display, window = xid, root = XDefaultRootWindow(display), time = (nuint)(100 + code * 2 + type), keycode = code, same_screen = 1 };
                        XSendEvent(display, xid, 0, type == 2 ? 1 : 2, &ev);
                    }
                    XSync(display, 0);
                    for (int i = 0; i < 8; i++) { Poll(); Thread.Sleep(3); }
                }
                Check(received.SequenceEqual(expectedScalars), "public XIM callback Unicode scalars, cycle " + cycle + " window " + instance);
                Destroy(window); // Recreate a real IC for the next window.
                GC.KeepAlive(callback); GC.KeepAlive(managedCallback);
            }
            Terminate(); // Reopen the real IM for the next init cycle.
        }
        Record("locale-after", CType()); Record("unicode-characters", received);
        Record("xim-init-cycles", 2); Record("xim-window-cycles", 6);
        Record("utf8-commit-bytes", System.Text.Encoding.UTF8.GetByteCount(expected));
    }

    // These checks execute the real Win32 loader/unwind and public termination
    // dispatch with owned OS module references. On Linux this is cleanup-body
    // evidence, never a claim to have run a Windows process.
    private static void ValidateWin32Modules()
    {
        Check(!native, "managed loader fixture");
        string path = Environment.GetEnvironmentVariable("GLFW_LIFETIME_MODULE") ?? throw new Exception("GLFW_LIFETIME_MODULE required");
        nint external = NativeLibrary.Load(path);
        var sentinel = (delegate* unmanaged[Cdecl]<int>)NativeLibrary.GetExport(external, "glfw_test_module_sentinel");
        var symbols = new List<string>();
        nint Load(string name) => NativeLibrary.Load(path);
        nint Symbol(nint module, string name) { symbols.Add(name); return (nint)sentinel; }
        void Preserved()
        {
            if (OperatingSystem.IsLinux()) Check(System.IO.File.ReadAllText("/proc/self/maps").Contains(path, StringComparison.Ordinal), "external module still mapped");
            Check(sentinel() == 42, "independent owner's function remains callable");
            Check(Glfw.Win32Native.user32 == null && Glfw.Win32Native.kernel32 == null && Glfw.Win32Native.gdi32 == null && Glfw.Win32Native.shell32 == null, "all core owners cleared");
        }
        try
        {
            for (int cycle = 0; cycle < 4; cycle++)
            {
                Check(Glfw.Win32Native.LoadCoreLibraries(Load, Symbol), "core load");
                var state = new Glfw.GlfwLibraryWin32();
                state.user32.instance = Glfw.Win32Native.user32!.handle; // alias, no new load
                state.xinput.instance = Load("xinput"); state.dinput8.instance = Load("dinput8");
                state.dwmapi.instance = Load("dwmapi"); state.shcore.instance = Load("shcore"); state.ntdll.instance = Load("ntdll");
                Glfw._glfw.Win32 = state; Glfw._glfw.platform = new Glfw.Win32Platform(); Glfw._glfw.initialized = true;
                G.glfwTerminate();
                Check(state.user32.instance == 0 && state.xinput.instance == 0 && state.dinput8.instance == 0 && state.dwmapi.instance == 0 && state.shcore.instance == 0 && state.ntdll.instance == 0, "optional owners and alias cleared");
                Check(Glfw._glfw.Win32 == null, "platform state dropped");
                G.terminateWin32(); // Cleanup after public termination is harmless.
                Preserved();
            }
            string[] required = symbols.Distinct().ToArray();
            foreach (string name in required)
            {
                Check(!Glfw.Win32Native.LoadCoreLibraries(Load, (module, symbol) => symbol == name ? 0 : (nint)sentinel), "missing required export fails: " + name);
                Preserved();
            }
            for (int failAt = 0; failAt < 4; failAt++)
            {
                int calls = 0;
                Check(!Glfw.Win32Native.LoadCoreLibraries(name => calls++ == failAt ? 0 : Load(name), Symbol), "partial module load failure " + failAt);
                Preserved();
            }
            string suffix = IntPtr.Size == 8 ? "PtrW" : "W";
            foreach (string name in new[] { "GetClassLong", "GetWindowLong", "SetWindowLong" }) Check(required.Contains(name + suffix), "process-width export selection: " + name);
            Record("required-exports-tested", required.Length); Record("module-shutdown-cycles", 4);
            Record("process-pointer-bytes", IntPtr.Size);
        }
        finally { Glfw.Win32Native.FreeLibraries(null); NativeLibrary.Free(external); }
        if (OperatingSystem.IsLinux()) Check(!System.IO.File.ReadAllText("/proc/self/maps").Contains(path, StringComparison.Ordinal), "no owned reference leaked");
        else if (OperatingSystem.IsWindows()) Check(GetModuleHandleW(path) == 0, "no owned Windows module reference leaked");
        // Full GLX/WGL tables must not carry stale extension pointers across init.
        Glfw._glfw.glx = new Glfw.GlfwLibraryGLX { ARB_create_context = true };
        Glfw._glfw.wgl = new Glfw.GlfwLibraryWGL { ARB_create_context = true };
        G._glfwTerminateGLX(); G._glfwTerminateWGL();
        Check(Glfw._glfw.glx == null && Glfw._glfw.wgl == null, "context module flags reset");
    }

    private static void ValidateWindowsRuntime()
    {
        Check(OperatingSystem.IsWindows(), "actual Windows runtime required; this scenario never skips");
        var external = new List<(string Name, string Export, nint Handle, nint Address)>();
        try
        {
            foreach (var (name, export) in new[] { ("user32.dll", "GetClassLongW"), ("kernel32.dll", "GetProcAddress"), ("gdi32.dll", "SetPixelFormat"), ("shell32.dll", "DragFinish"), ("dinput8.dll", "DirectInput8Create"), ("xinput1_4.dll", "XInputGetState"), ("dwmapi.dll", "DwmFlush"), ("shcore.dll", "GetDpiForMonitor"), ("ntdll.dll", "RtlVerifyVersionInfo") })
                if (NativeLibrary.TryLoad(name, out nint h)) external.Add((name, export, h, NativeLibrary.GetExport(h, export)));
            for (int cycle = 0; cycle < 8; cycle++)
            {
                InitHint(GLFW_PLATFORM, GLFW_PLATFORM_WIN32); Check(Init() == 1, "Windows initialization");
                Hint(GLFW_VISIBLE, 0); Hint(GLFW_CLIENT_API, GLFW_NO_API);
                var window = Create() ?? throw new Exception("Windows icon test window");
                // This public reset invokes GetClassLongPtrW (GetClassLongW on x86).
                if (native) N.glfwSetWindowIcon(window.Handle, 0, 0); else G.glfwSetWindowIcon(window.Managed!, 0, null);
                Check(Error() == 0, "reset class icons");
                if (!native)
                {
                    var u32 = Glfw.Win32Native.user32!; nint hwnd = window.Managed!.Win32!.handle;
                    nint marker = IntPtr.Size == 8 ? unchecked((nint)0x1234567887654321L) : unchecked((nint)0x87654321);
                    nint previous = u32.SetWindowLongPtrW(hwnd, -21, marker); // GWLP_USERDATA
                    Check(u32.GetWindowLongPtrW(hwnd, -21) == marker, "pointer-width round trip");
                    u32.SetWindowLongPtrW(hwnd, -21, previous);
                    Check((nuint)u32.SendMessageW(hwnd, 0x007F, 1, 0) == u32.GetClassLongPtrW(hwnd, -14), "big icon restored");
                    Check((nuint)u32.SendMessageW(hwnd, 0x007F, 0, 0) == u32.GetClassLongPtrW(hwnd, -34), "small icon restored");
                }
                Destroy(window); Terminate();
                foreach (var entry in external)
                {
                    Check(GetModuleHandleW(entry.Name) == entry.Handle, "external module remains loaded: " + entry.Name);
                    Check(NativeLibrary.GetExport(entry.Handle, entry.Export) == entry.Address, "external owner's export remains valid: " + entry.Name);
                }
            }
            if (!native)
            {
                foreach (string symbol in new[] { IntPtr.Size == 8 ? "GetClassLongPtrW" : "GetClassLongW", IntPtr.Size == 8 ? "GetWindowLongPtrW" : "GetWindowLongW", IntPtr.Size == 8 ? "SetWindowLongPtrW" : "SetWindowLongW" })
                {
                    Check(!Glfw.Win32Native.LoadCoreLibraries(G._glfwPlatformLoadModule, (module, name) => name == symbol ? 0 : G._glfwPlatformGetModuleSymbol(module, name)), "Windows required export failure unwinds: " + symbol);
                    Check(Glfw.Win32Native.user32 == null && Glfw.Win32Native.kernel32 == null && Glfw.Win32Native.gdi32 == null && Glfw.Win32Native.shell32 == null, "Windows partial tables cleared");
                    foreach (var entry in external) Check(GetModuleHandleW(entry.Name) == entry.Handle && NativeLibrary.GetExport(entry.Handle, entry.Export) == entry.Address, "Windows external owner survives partial unwind");
                }
            }
            Record("windows-icon-reset-cycles", 8); Record("process-pointer-bytes", IntPtr.Size);
        }
        finally { foreach (var entry in external) NativeLibrary.Free(entry.Handle); }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern nint GetModuleHandleW(string name);

    private static partial class N
    {
        [StructLayout(LayoutKind.Sequential)] internal struct VideoMode { internal int Width, Height, Red, Green, Blue, Refresh; }
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwGetPrimaryMonitor();
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern VideoMode* glfwGetVideoMode(nint monitor);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwSetWindowIcon(nint window, int count, nint images);
    }
}
