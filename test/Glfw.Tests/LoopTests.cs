using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using G = Glfw.Glfw;
using static Glfw.GLFW;

internal static unsafe partial class Program
{
    private static void ValidateWindowContracts(Window window, int platform)
    {
        object marker = new();
        if (native) N.glfwSetWindowUserPointer(window.Handle, 0x1234); else G.glfwSetWindowUserPointer(window.Managed!, marker);
        GC.Collect(); GC.WaitForPendingFinalizers();
        Check(native ? N.glfwGetWindowUserPointer(window.Handle) == 0x1234 : ReferenceEquals(marker, G.glfwGetWindowUserPointer(window.Managed!)), "window user pointer retention");
        Record("user-pointer-retained", true);
        var changes = new List<string>();
        N.SizeFun? size = null, framebuffer = null; N.ScaleFun? scale = null;
        Glfw.GlfwWindowSizeFun? msize = null; Glfw.GlfwFramebufferSizeFun? mframebuffer = null; Glfw.GlfwWindowContentScaleFun? mscale = null;
        if (native)
        {
            size = (_, w, h) => changes.Add($"window:{w}:{h}"); framebuffer = (_, w, h) => changes.Add($"framebuffer:{w}:{h}"); scale = (_, x, y) => changes.Add($"scale:{x}:{y}");
            N.glfwSetWindowSizeCallback(window.Handle, size); N.glfwSetFramebufferSizeCallback(window.Handle, framebuffer); N.glfwSetWindowContentScaleCallback(window.Handle, scale);
        }
        else
        {
            msize = (_, w, h) => changes.Add($"window:{w}:{h}"); mframebuffer = (_, w, h) => changes.Add($"framebuffer:{w}:{h}"); mscale = (_, x, y) => changes.Add($"scale:{x}:{y}");
            G.glfwSetWindowSizeCallback(window.Managed!, msize); G.glfwSetFramebufferSizeCallback(window.Managed!, mframebuffer); G.glfwSetWindowContentScaleCallback(window.Managed!, mscale);
        }
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        SetSize(window, 101, 79);
        for (int i = 0; i < 30; i++) { Poll(); if (changes.Count >= 2) break; Thread.Sleep(5); }
        Record("independent-size-callbacks", changes);
        Check(changes.Contains("window:101:79") && changes.Contains("framebuffer:101:79"), "independent size and framebuffer callbacks");
        Check(!changes.Exists(c => c.StartsWith("scale:", StringComparison.Ordinal)), "resize does not invent a content scale event");
        float xscale, yscale;
        if (native) N.glfwGetWindowContentScale(window.Handle, out xscale, out yscale); else G.glfwGetWindowContentScale(window.Managed!, out xscale, out yscale);
        Record("content-scale", new[] { xscale, yscale }); Check(xscale > 0 && yscale > 0, "positive content scale");
        if (platform == GLFW_PLATFORM_X11)
        {
            Poll();
            var elapsed = Stopwatch.StartNew();
            bool posted = false;
            var worker = new Thread(() => { Thread.Sleep(30); posted = true; if (native) N.glfwPostEmptyEvent(); else G.glfwPostEmptyEvent(); });
            worker.Start();
            while (!Volatile.Read(ref posted) && elapsed.Elapsed.TotalSeconds < 0.4)
            { if (native) N.glfwWaitEventsTimeout(0.5); else G.glfwWaitEventsTimeout(0.5); }
            worker.Join();
            bool woke = elapsed.Elapsed.TotalSeconds < 0.4;
            Check(woke, "PostEmptyEvent wakes WaitEventsTimeout"); Record("worker-post-wakes-wait", woke);
            if (native) N.glfwPostEmptyEvent(); else G.glfwPostEmptyEvent();
            elapsed.Restart();
            if (native) N.glfwWaitEventsTimeout(0.5); else G.glfwWaitEventsTimeout(0.5);
            Check(elapsed.Elapsed.TotalSeconds < 0.2, "post before wait is retained"); Record("post-before-wait-retained", true);
            Poll(); elapsed.Restart();
            if (native) N.glfwWaitEventsTimeout(0.025); else G.glfwWaitEventsTimeout(0.025);
            Record("timed-wait-completed", elapsed.Elapsed.TotalSeconds < 0.5);
        }
        GC.KeepAlive(size); GC.KeepAlive(framebuffer); GC.KeepAlive(scale); GC.KeepAlive(msize); GC.KeepAlive(mframebuffer); GC.KeepAlive(mscale);
    }
    private static partial class N
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void ScaleFun(nint window, float xscale, float yscale);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwWaitEventsTimeout(double seconds);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwPostEmptyEvent();
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwSetWindowUserPointer(nint window, nint pointer);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwGetWindowUserPointer(nint window);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwSetFramebufferSizeCallback(nint window, SizeFun callback);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwSetWindowContentScaleCallback(nint window, ScaleFun callback);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void glfwGetWindowContentScale(nint window, out float xscale, out float yscale);
    }
}
