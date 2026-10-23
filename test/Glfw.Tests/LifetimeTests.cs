using System;
using System.Runtime.CompilerServices;
using G = Glfw.Glfw;
using static Glfw.GLFW;

internal static unsafe partial class Program
{
    private static int callbackHits;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference InstallSizeCallback(Glfw.GlfwWindow window)
    {
        var captured = new object();
        Glfw.GlfwWindowSizeFun callback = (_, _, _) => { callbackHits++; GC.KeepAlive(captured); };
        G.glfwSetWindowSizeCallback(window, callback);
        return new WeakReference(callback);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ClearSizeCallback(Glfw.GlfwWindow window) => G.glfwSetWindowSizeCallback(window, null);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference InstallUserPointer(Glfw.GlfwWindow window)
    { var marker = new object(); G.glfwSetWindowUserPointer(window, marker); return new WeakReference(marker); }
    private static void Collect() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
    private static void ValidateCallbackRoots()
    {
        G.glfwInitHint(GLFW_PLATFORM, GLFW_PLATFORM_NULL); Check(G.glfwInit() == 1, "callback roots init");
        G.glfwWindowHint(GLFW_CLIENT_API, GLFW_NO_API); G.glfwWindowHint(GLFW_VISIBLE, 0);
        var window = G.glfwCreateWindow(32,32,"callback roots",null,null)!;
        callbackHits = 0;
        var callback = InstallSizeCallback(window);
        Collect(); Check(callback.IsAlive, "window retains callback with no caller reference");
        G.glfwSetWindowSize(window, 50, 40); Check(callbackHits == 1, "retained callback fires after GC");
        ClearSizeCallback(window); Collect(); Check(!callback.IsAlive, "clearing callback releases delegate");
        var pointer = InstallUserPointer(window); Collect(); Check(pointer.IsAlive, "window retains user pointer with no caller reference");
        G.glfwSetWindowUserPointer(window, null); Collect(); Check(!pointer.IsAlive, "clearing user pointer releases object");
        callback = InstallSizeCallback(window); G.glfwDestroyWindow(window); Collect();
        Check(!callback.IsAlive, "destroying window clears callbacks even with retained window wrapper");
        Record("callback-lifetime-assertions", 7); Record("post-GC-callback-invocations", callbackHits);
        G.glfwTerminate(); GC.KeepAlive(window);
    }
}
