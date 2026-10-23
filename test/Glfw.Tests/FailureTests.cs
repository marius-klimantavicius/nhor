using System;
using System.Runtime.CompilerServices;
using G = Glfw.Glfw;
using static Glfw.GLFW;

internal static unsafe partial class Program
{
    private static delegate* unmanaged[Cdecl]<nint, nint, void> originalDestroyGLX;
    private static int destroyedContexts;
    [System.Runtime.InteropServices.UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static nuint FailGLXWindow(nint display, nint config, nuint window, int* attributes) => 0;
    [System.Runtime.InteropServices.UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void CountGLXDestroy(nint display, nint context)
    { destroyedContexts++; originalDestroyGLX(display, context); }
    private static void ValidatePartialCreation()
    {
        G.glfwInitHint(GLFW_PLATFORM, GLFW_PLATFORM_X11); Check(G.glfwInit() == 1, "partial init");
        G.glfwWindowHint(GLFW_VISIBLE, 0);
        var warmup = G.glfwCreateWindow(32,32,"warmup",null,null)!;
        Check(warmup != null, "partial warmup context"); G.glfwDestroyWindow(warmup);
        var glx = Glfw._glfw.glx!;
        var original = glx.CreateWindow;
        originalDestroyGLX = glx.DestroyContext;
        destroyedContexts = 0;
        try
        {
            glx.CreateWindow = &FailGLXWindow; glx.DestroyContext = &CountGLXDestroy;
            var window = G.glfwCreateWindow(32,32,"failed drawable",null,null);
            Check(window == null, "failed drawable propagates failure");
            Check(G.glfwGetError(out _) == GLFW_PLATFORM_ERROR, "failed drawable reports platform error");
            Check(destroyedContexts == 1, "failed drawable releases the real native GLX context");
            Check(Glfw._glfw.windowListHead == null, "failed window unlinked");
            Record("partial-glx-context-destroy-count", destroyedContexts);
        }
        finally { glx.CreateWindow = original; glx.DestroyContext = originalDestroyGLX; G.glfwTerminate(); }
    }
}
