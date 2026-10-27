using System;
using System.IO;
using System.Runtime.InteropServices;
using Glfw;

namespace Marius.Winter;

/// <summary>A local, opt-in graphics initialization and first-frame probe.</summary>
public static class GraphicsDiagnostics
{
    /// <summary>Writes a pasteable report without showing a window. Returns zero on success.</summary>
    public static int Run(TextWriter output, RenderBackend backend = RenderBackend.SW)
    {
        output.WriteLine($"Backend: {backend}");
        output.WriteLine("Requested: OpenGL 3.3 core (SW also needs a GL upload context)");
        Window? window = null;
        string stage = "construct Window";
        try
        {
            window = new Window(64, 48, "Winter graphics diagnostics", backend: backend);
            output.WriteLine(window.GraphicsInformation);
            stage = "render first frame";
            window.DrawDiagnosticFrame();
            stage = "dispose Window";
            window.Dispose();
            output.WriteLine("Result: success (hidden window; initialization and first frame)");
            return 0;
        }
        catch (Exception failure)
        {
            output.WriteLine("Stage: " + (failure.Data["Winter.Graphics.Stage"] ?? stage));
            if (window == null && failure.Data["Winter.Graphics.Context"] is string context && context.Length != 0)
                output.WriteLine(context);
            output.WriteLine($"Result: failed ({failure.GetType().Name}): {failure.Message}");
            if (failure.Data["Winter.Graphics.Cleanup"] is Exception cleanup)
                output.WriteLine($"Cleanup: {cleanup.GetType().Name}: {cleanup.Message}");
            return 1;
        }
        finally
        {
            // A failing cleanup must not replace the initialization/draw report.
            if (window != null && !window.IsDisposed)
            {
                try { window.Dispose(); }
                catch (Exception cleanup) { output.WriteLine($"Cleanup: {cleanup.GetType().Name}: {cleanup.Message}"); }
            }
        }
    }

    internal static void CleanupFailedInitialization(Exception failure, string stage, string context, Action cleanup)
    {
        failure.Data["Winter.Graphics.Stage"] = stage;
        failure.Data["Winter.Graphics.Context"] = context;
        try { cleanup(); }
        catch (Exception cleanupFailure) { failure.Data["Winter.Graphics.Cleanup"] = cleanupFailure; }
    }

    internal static unsafe string DescribeContext(GlfwWindow window)
    {
        var getString = (delegate* unmanaged[Cdecl]<uint, byte*>)Glfw.Glfw.glfwGetProcAddress("glGetString");
        string version = "unavailable", vendor = "unavailable", renderer = "unavailable", glsl = "unavailable";
        if (getString != null)
        {
            version = Marshal.PtrToStringUTF8((nint)getString(0x1F02)) ?? "unavailable";
            vendor = Marshal.PtrToStringUTF8((nint)getString(0x1F00)) ?? "unavailable";
            renderer = Marshal.PtrToStringUTF8((nint)getString(0x1F01)) ?? "unavailable";
            glsl = Marshal.PtrToStringUTF8((nint)getString(0x8B8C)) ?? "unavailable";
        }
        int profile = Glfw.Glfw.glfwGetWindowAttrib(window, GLFW.GLFW_OPENGL_PROFILE);
        string profileName = profile == GLFW.GLFW_OPENGL_CORE_PROFILE ? "core" :
            profile == GLFW.GLFW_OPENGL_COMPAT_PROFILE ? "compatibility" : "unknown";
        string api = window.context.wgl != null ? "WGL (surface=borrowed HDC, context=HGLRC)" :
            window.context.glx != null ? "GLX (display=X11, surface=drawable)" : "unsupported native target";
        return $"Context API: {api}\nOpenGL: {version}\nProfile: {profileName}\nVendor: {vendor}\nRenderer: {renderer}\nGLSL: {glsl}";
    }
}
