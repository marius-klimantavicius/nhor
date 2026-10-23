using System;
using System.IO;
using System.Runtime.CompilerServices;
using G = Glfw.Glfw;
using static Glfw.GLFW;

internal static unsafe partial class Program
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAndDestroy()
    {
        var window = G.glfwCreateWindow(32, 32, "GLFW resource test", null, null)!;
        Check(window != null, "resource window creation");
        G.glfwSetWindowSizeCallback(window!, (_, _, _) => { });
        var reference = new WeakReference(window);
        G.glfwDestroyWindow(window);
        return reference;
    }
    private static void ValidateResources()
    {
        G.glfwInitHint(GLFW_PLATFORM, GLFW_PLATFORM_X11);
        void Cycle()
        {
            Check(G.glfwInit() == 1, "resource initialization");
            G.glfwWindowHint(GLFW_VISIBLE, 0); G.glfwWindowHint(GLFW_CLIENT_API, GLFW_NO_API);
            for (int i = 0; i < 10; i++) _ = CreateAndDestroy();
            G.glfwTerminate();
        }
        for (int i = 0; i < 3; i++) Cycle();
        int before = Directory.GetFiles("/proc/self/fd").Length;
        for (int i = 0; i < 20; i++) Cycle();
        int after = Directory.GetFiles("/proc/self/fd").Length;
        Record("file-descriptors-before", before); Record("file-descriptors-after", after);
        Check(before == after, "initialization cycles leak file descriptors");
        Check(G.glfwInit() == 1, "GC retention init"); G.glfwWindowHint(GLFW_VISIBLE, 0); G.glfwWindowHint(GLFW_CLIENT_API, GLFW_NO_API);
        var references = new WeakReference[100];
        for (int i = 0; i < references.Length; i++) references[i] = CreateAndDestroy();
        G.glfwTerminate();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        int alive = Array.FindAll(references, r => r.IsAlive).Length;
        Record("destroyed-window-references-alive", alive); Record("destroyed-window-reference-count", 100);
        Check(alive == 0, "destroyed windows retained by native callbacks or global state");
        Record("resource-initialization-cycles", 24); Record("resource-window-cycles", 330);
    }
}
