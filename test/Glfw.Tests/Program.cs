using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using G = Glfw.Glfw;
using static Glfw.GLFW;

// No test package dependencies. The identical scenarios run against the managed
// port and a caller-selected, independently built pristine upstream library.
internal static unsafe partial class Program
{
    private static bool native;
    private static readonly SortedDictionary<string, object?> Results = new();
    private static readonly List<int> Errors = new();
    private static int assertions;
    private static N.ErrorFun? nativeError;
    private static Glfw.GLFWerrorfun? managedError;
    private sealed class Window
    {
        internal nint Handle;
        internal Glfw.GlfwWindow? Managed;
    }

    private static void Check(bool condition, string name)
    {
        assertions++;
        if (!condition) throw new Exception(name);
    }
    private static void Record(string name, object? value) => Results[name] = value;
    private static int Error() => native ? N.glfwGetError(out _) : G.glfwGetError(out _);
    private static void InitHint(int hint, int value) { if (native) N.glfwInitHint(hint, value); else G.glfwInitHint(hint, value); }
    private static int Init() => native ? N.glfwInit() : G.glfwInit();
    private static void Terminate() { if (native) N.glfwTerminate(); else G.glfwTerminate(); }
    private static void Hint(int hint, int value) { if (native) N.glfwWindowHint(hint, value); else G.glfwWindowHint(hint, value); }
    private static void Defaults() { if (native) N.glfwDefaultWindowHints(); else G.glfwDefaultWindowHints(); }
    private static Window? Create(int width = 64, int height = 48, Window? share = null)
    {
        if (native)
        {
            nint w = N.glfwCreateWindow(width, height, "GLFW isolated parity", 0, share?.Handle ?? 0);
            return w == 0 ? null : new Window { Handle = w };
        }
        var managed = G.glfwCreateWindow(width, height, "GLFW isolated parity", null, share?.Managed);
        return managed == null ? null : new Window { Managed = managed };
    }
    private static void Destroy(Window w) { if (native) N.glfwDestroyWindow(w.Handle); else G.glfwDestroyWindow(w.Managed); }
    private static void Current(Window? w) { if (native) N.glfwMakeContextCurrent(w?.Handle ?? 0); else G.glfwMakeContextCurrent(w?.Managed); }
    private static nint Proc(string name) => native ? N.glfwGetProcAddress(name) : G.glfwGetProcAddress(name);
    private static int Attrib(Window w, int attr) => native ? N.glfwGetWindowAttrib(w.Handle, attr) : G.glfwGetWindowAttrib(w.Managed!, attr);
    private static void Poll() { if (native) N.glfwPollEvents(); else G.glfwPollEvents(); }
    private static void Size(Window w, out int width, out int height)
    { if (native) N.glfwGetWindowSize(w.Handle, out width, out height); else G.glfwGetWindowSize(w.Managed!, out width, out height); }
    private static void SetSize(Window w, int width, int height)
    { if (native) N.glfwSetWindowSize(w.Handle, width, height); else G.glfwSetWindowSize(w.Managed!, width, height); }
    private static void InputMode(Window w, int mode, int value)
    { if (native) N.glfwSetInputMode(w.Handle, mode, value); else G.glfwSetInputMode(w.Managed!, mode, value); }
    private static int GetInputMode(Window w, int mode) => native ? N.glfwGetInputMode(w.Handle, mode) : G.glfwGetInputMode(w.Managed!, mode);

    private static void RunCore(int platform)
    {
        Terminate();
        _ = Error();
        Record("uninitialized-time", native ? N.glfwGetTime() : G.glfwGetTime());
        Record("uninitialized-error", Error());
        Record("cleared-error", Error());
        InitHint(unchecked((int)0xdeadbeef), 1);
        Record("invalid-hint", Error());
        InitHint(GLFW_PLATFORM, unchecked((int)0xdeadbeef));
        Record("invalid-platform-init", Init());
        Record("invalid-platform-error", Error());
        InitHint(GLFW_PLATFORM, platform);
        Check(Init() == GLFW_TRUE, "initialization: " + Error());
        Check(Init() == GLFW_TRUE, "repeat initialization");
        Record("selected-platform", native ? N.glfwGetPlatform() : G.glfwGetPlatform());
        foreach (int p in new[] { GLFW_PLATFORM_NULL, GLFW_PLATFORM_X11, GLFW_PLATFORM_WIN32, GLFW_PLATFORM_COCOA, GLFW_PLATFORM_WAYLAND, GLFW_ANY_PLATFORM })
        {
            Record("platform-supported-" + p, native ? N.glfwPlatformSupported(p) != 0 : G.glfwPlatformSupported(p));
            Record("platform-supported-error-" + p, Error());
        }
        Defaults();
        Hint(GLFW_VISIBLE, GLFW_FALSE);
        Hint(GLFW_CLIENT_API, GLFW_NO_API);
        Record("zero-width-window", Create(0, 2) == null);
        Record("zero-width-error", Error());
        var w = Create() ?? throw new Exception("no-api window: " + Error());
        Size(w, out int width, out int height);
        Record("window-size", new[] { width, height });
        Record("window-client", Attrib(w, GLFW_CLIENT_API));
        Record("window-visible", Attrib(w, GLFW_VISIBLE));
        Record("window-title", native ? Marshal.PtrToStringUTF8(N.glfwGetWindowTitle(w.Handle)) : G.glfwGetWindowTitle(w.Managed!));
        InputMode(w, GLFW_STICKY_KEYS, 1);
        Record("sticky-mode", GetInputMode(w, GLFW_STICKY_KEYS));
        InputMode(w, GLFW_CURSOR, -1);
        Record("invalid-cursor-mode", Error());
        InputMode(w, GLFW_UNLIMITED_MOUSE_BUTTONS, 1);
        Record("unlimited-mode", GetInputMode(w, GLFW_UNLIMITED_MOUSE_BUTTONS));
        if (native) N.glfwSetCursorPos(w.Handle, double.NaN, 0); else G.glfwSetCursorPos(w.Managed!, double.NaN, 0);
        Record("nan-cursor", Error());
        if (native) N.glfwSetWindowOpacity(w.Handle, float.PositiveInfinity); else G.glfwSetWindowOpacity(w.Managed!, float.PositiveInfinity);
        Record("infinite-opacity", Error());
        foreach (double value in new[] { -1.0, double.NaN, double.PositiveInfinity, 18446744074.0 })
        {
            if (native) N.glfwSetTime(value); else G.glfwSetTime(value);
            Record("invalid-time-" + value.ToString(System.Globalization.CultureInfo.InvariantCulture), Error());
        }
        Current(w);
        Record("no-api-current", Error());
        Record("no-context-proc", Proc("glClear") == 0);
        Record("no-context-proc-error", Error());
        if (native) N.glfwSwapBuffers(w.Handle); else G.glfwSwapBuffers(w.Managed!);
        Record("no-api-swap", Error());
        int resizeCount = 0;
        N.SizeFun? cb = null;
        Glfw.GlfwWindowSizeFun? mcb = null;
        if (native) { cb = (_, _, _) => resizeCount++; N.glfwSetWindowSizeCallback(w.Handle, cb); }
        else { mcb = (_, _, _) => resizeCount++; G.glfwSetWindowSizeCallback(w.Managed!, mcb); }
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        SetSize(w, 97, 73);
        for (int i = 0; i < 25; i++) { Poll(); Size(w, out width, out height); if (resizeCount > 0) break; Thread.Sleep(4); }
        Record("resized-size", new[] { width, height });
        Record("resize-callback-fired", resizeCount > 0);
        GC.KeepAlive(cb); GC.KeepAlive(mcb);
        int threadError = -1;
        var thread = new Thread(() => { _ = Proc("glClear"); threadError = Error(); });
        thread.Start(); thread.Join();
        Record("worker-no-context", threadError);
        Record("main-error-isolated", Error());
        ValidateJoysticks();
        if (platform == GLFW_PLATFORM_X11) ValidateEvents(w);
        ValidateWindowContracts(w, platform);
        Destroy(w);
        for (int i = 0; i < 30; i++) { var repeat = Create() ?? throw new Exception("repeat window " + i); Destroy(repeat); }
        Record("repeated-no-api-windows", 30);
        ValidateLifecycleThreads();
        Terminate();
        for (int i = 0; i < 5; i++) { Check(Init() == 1, "reinit " + i); Terminate(); }
        Record("initialization-cycles", 5);
    }

    private static void RunGlx(string scenario)
    {
        InitHint(GLFW_PLATFORM, scenario.EndsWith("null", StringComparison.Ordinal) ? GLFW_PLATFORM_NULL : GLFW_PLATFORM_X11);
        Check(Init() == 1, "GLX init " + Error());
        Defaults(); Hint(GLFW_VISIBLE, 0); Hint(GLFW_CONTEXT_VERSION_MAJOR, 3); Hint(GLFW_CONTEXT_VERSION_MINOR, 3); Hint(GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE);
        int source = scenario.StartsWith("egl", StringComparison.Ordinal) ? GLFW_EGL_CONTEXT_API : scenario.StartsWith("osmesa", StringComparison.Ordinal) ? GLFW_OSMESA_CONTEXT_API : GLFW_NATIVE_CONTEXT_API;
        Hint(GLFW_CONTEXT_CREATION_API, source);
        byte* pixel = stackalloc byte[4];
        for (int cycle = 0; cycle < 12; cycle++)
        {
            var w = Create() ?? throw new Exception("GLX create " + Error());
            Current(w);
            Check(Proc("glClear") != 0, "GL proc");
            if (cycle == 0)
            {
                // A second hidden context must share objects and restore the
                // previously current context after internal attribute discovery.
                var generate = (delegate* unmanaged<int, uint*, void>)Proc("glGenTextures");
                var bind = (delegate* unmanaged<uint, uint, void>)Proc("glBindTexture");
                var isTexture = (delegate* unmanaged<uint, byte>)Proc("glIsTexture");
                var delete = (delegate* unmanaged<int, uint*, void>)Proc("glDeleteTextures");
                uint texture; generate(1, &texture); bind(0x0de1, texture);
                var shared = Create(share: w) ?? throw new Exception("shared context " + Error());
                Check(native ? N.glfwGetCurrentContext() == w.Handle : ReferenceEquals(G.glfwGetCurrentContext(), w.Managed), "creation restores previous context");
                Current(shared); Check(isTexture(texture) != 0, "GL object sharing");
                Current(w); Destroy(shared); delete(1, &texture);
                Record("shared-context-texture", true);
            }
            var clearColor = (delegate* unmanaged<float, float, float, float, void>)Proc("glClearColor");
            var clear = (delegate* unmanaged<uint, void>)Proc("glClear");
            var read = (delegate* unmanaged<int, int, int, int, uint, uint, byte*, void>)Proc("glReadPixels");
            var finish = (delegate* unmanaged<void>)Proc("glFinish");
            var getString = (delegate* unmanaged<uint, nint>)Proc("glGetString");
            if (cycle == 0)
            {
                Record("gl-vendor", Marshal.PtrToStringAnsi(getString(0x1F00)));
                Record("gl-renderer", Marshal.PtrToStringAnsi(getString(0x1F01)));
                Record("gl-version", Marshal.PtrToStringAnsi(getString(0x1F02)));
                Record("gl-context-version", new[] { Attrib(w, GLFW_CONTEXT_VERSION_MAJOR), Attrib(w, GLFW_CONTEXT_VERSION_MINOR) });
            }
            // Endpoint colors avoid driver-specific default-framebuffer dithering.
            clearColor(1f, 0f, 1f, 1f); clear(0x4000); finish();
            read(0, 0, 1, 1, 0x1908, 0x1401, pixel);
            if (cycle == 0) Record("gl-pixel", new int[] { pixel[0], pixel[1], pixel[2], pixel[3] });
            Check(pixel[0] == 255 && pixel[1] == 0 && pixel[2] == 255 && pixel[3] == 255, "GL framebuffer bytes: " + string.Join(",", new int[] { pixel[0], pixel[1], pixel[2], pixel[3] }));
            if (native) { N.glfwSwapInterval(0); N.glfwSwapBuffers(w.Handle); }
            else { G.glfwSwapInterval(0); G.glfwSwapBuffers(w.Managed!); }
            Check(Error() == 0, "GL rendering/swap errors");
            if (source == GLFW_OSMESA_CONTEXT_API)
            {
                nint context = native ? N.glfwGetOSMesaContext(w.Handle) : G.glfwGetOSMesaContext(w.Managed!);
                Check(context != 0, "OSMesa native context");
                int width, height, format; nint buffer;
                int ok;
                if (native) ok = N.glfwGetOSMesaColorBuffer(w.Handle, out width, out height, out format, out buffer);
                else ok = G.glfwGetOSMesaColorBuffer(w.Managed!, out width, out height, out format, out buffer) ? 1 : 0;
                Check(ok == 1 && width == 64 && height == 48 && buffer != 0, "OSMesa buffer");
                if (cycle == 0) Record("osmesa-color-buffer", new int[] { width, height, format, Marshal.ReadByte(buffer), Marshal.ReadByte(buffer, 1), Marshal.ReadByte(buffer, 2) });
                if (native) ok = N.glfwGetOSMesaDepthBuffer(w.Handle, out width, out height, out format, out buffer);
                else ok = G.glfwGetOSMesaDepthBuffer(w.Managed!, out width, out height, out format, out buffer) ? 1 : 0;
                Check(ok == 1 && width == 64 && height == 48 && buffer != 0, "OSMesa depth buffer");
                if (cycle == 0) Record("osmesa-depth-layout", new int[] { width, height, format });
            }
            if (source == GLFW_EGL_CONTEXT_API)
            {
                Check((native ? N.glfwGetEGLContext(w.Handle) : G.glfwGetEGLContext(w.Managed!)) != 0, "EGL native context");
                Check((native ? N.glfwGetEGLSurface(w.Handle) : G.glfwGetEGLSurface(w.Managed!)) != 0, "EGL native surface");
                nint config; bool ok = native ? N.glfwGetEGLConfig(w.Handle, out config) != 0 : G.glfwGetEGLConfig(w.Managed!, out config);
                Check(ok && config != 0, "EGL native config");
            }
            Current(null);
            int workerContext = 0;
            var worker = new Thread(() => { Current(w); workerContext = Proc("glClear") != 0 ? 1 : 0; Current(null); });
            worker.Start(); worker.Join();
            Check(workerContext == 1, "context transfer to worker");
            Destroy(w);
        }
        Record("glx-render-cycles", 12);
        Terminate();
    }

    private static int Main(string[] args)
    {
        native = args.Length > 0 && args[0] == "native";
        if (native)
        {
            string path = Environment.GetEnvironmentVariable("GLFW_NATIVE_LIBRARY") ?? throw new Exception("GLFW_NATIVE_LIBRARY required");
            NativeLibrary.SetDllImportResolver(typeof(Program).Assembly, (name, _, _) => name == "glfw-parity" ? NativeLibrary.Load(path) : 0);
            nativeError = (code, _) => Errors.Add(code);
            N.glfwSetErrorCallback(nativeError);
        }
        else { managedError = (code, _) => Errors.Add(code); G.glfwSetErrorCallback(managedError); }
        try
        {
            string scenario = args.Length > 1 ? args[1] : "null";
            if (scenario == "device") { ValidateVirtualDevice(args[2]); return 0; }
            if (scenario == "x11-abi") ValidateX11Abi();
            else if (scenario == "x11-key-names") ValidateX11KeyNames();
            else if (scenario == "abi" || scenario == "windows-abi") ValidateAbi();
            else if (scenario == "windows-abi32") ValidateAbi(true);
            else if (scenario == "mappings") ValidateMappings();
            else if (scenario == "resources") ValidateResources();
            else if (scenario == "partial") ValidatePartialCreation();
            else if (scenario == "constants") ValidateConstants();
            else if (scenario == "roots") ValidateCallbackRoots();
            else if (scenario == "clipboard") ValidateClipboard();
            else if (scenario == "errors") ValidateErrors();
            else if (scenario == "scaled") ValidateScaledDisplay();
            else if (scenario == "current-mode") ValidateCurrentMode();
            else if (scenario == "context-reinit") ValidateContextReinitialization();
            else if (scenario == "mode-dispatch") ValidateModeDispatch();
            else if (scenario.StartsWith("xim-", StringComparison.Ordinal)) ValidateXim(scenario);
            else if (scenario == "win32-modules") ValidateWin32Modules();
            else if (scenario == "windows-runtime") ValidateWindowsRuntime();
            else if (scenario == "glx" || scenario.StartsWith("egl", StringComparison.Ordinal) || scenario.StartsWith("osmesa", StringComparison.Ordinal)) RunGlx(scenario);
            else RunCore(scenario == "x11" ? GLFW_PLATFORM_X11 : GLFW_PLATFORM_NULL);
            Record("assertions", assertions);
            Record("error-callbacks", Errors);
            Console.WriteLine(JsonSerializer.Serialize(Results, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Console.WriteLine(JsonSerializer.Serialize(Results, new JsonSerializerOptions { WriteIndented = true })); return 1; }
        finally { Terminate(); GC.KeepAlive(nativeError); GC.KeepAlive(managedError); }
    }

    private static partial class N
    {
        private const string Lib = "glfw-parity";
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void ErrorFun(int code, nint text);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void SizeFun(nint window, int width, int height);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwSetErrorCallback(ErrorFun cb);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int glfwGetError(out nint description);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwInitHint(int hint, int value);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int glfwInit();
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwTerminate();
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwDefaultWindowHints();
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwWindowHint(int hint, int value);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwCreateWindow(int width, int height, [MarshalAs(UnmanagedType.LPUTF8Str)] string title, nint monitor, nint share);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwDestroyWindow(nint window);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwMakeContextCurrent(nint window);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwGetProcAddress([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int glfwGetWindowAttrib(nint window, int attr);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwGetWindowTitle(nint window);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwPollEvents();
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwGetWindowSize(nint window, out int width, out int height);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwSetWindowSize(nint window, int width, int height);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwSetWindowSizeCallback(nint window, SizeFun callback);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwSetInputMode(nint window, int mode, int value);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int glfwGetInputMode(nint window, int mode);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwSetCursorPos(nint window, double x, double y);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwSetWindowOpacity(nint window, float opacity);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern double glfwGetTime();
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwSetTime(double time);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwSwapBuffers(nint window);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwSwapInterval(int interval);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwGetOSMesaContext(nint window);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int glfwGetOSMesaColorBuffer(nint window, out int width, out int height, out int format, out nint buffer);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int glfwGetOSMesaDepthBuffer(nint window, out int width, out int height, out int bytesPerValue, out nint buffer);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwGetEGLContext(nint window);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwGetEGLSurface(nint window);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int glfwGetEGLConfig(nint window, out nint config);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwGetCurrentContext();
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int glfwGetPlatform();
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int glfwPlatformSupported(int platform);
    }
}
