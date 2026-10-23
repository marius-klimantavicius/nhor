using System;
using G = Glfw.Glfw;
using static Glfw.GLFW;

internal static unsafe partial class Program
{
    private static void ValidateErrors()
    {
        InitHint(GLFW_PLATFORM, GLFW_PLATFORM_X11); Check(Init() == 1, "invalid context init");
        (string Name, int Hint, int Value, int[]? More)[] cases = {
            ("client", GLFW_CLIENT_API, 123, null), ("source", GLFW_CONTEXT_CREATION_API, 123, null),
            ("major-zero", GLFW_CONTEXT_VERSION_MAJOR, 0, null), ("minor-negative", GLFW_CONTEXT_VERSION_MINOR, -1, null),
            ("gl16", GLFW_CONTEXT_VERSION_MINOR, 6, null),
            ("gl22", GLFW_CONTEXT_VERSION_MAJOR, 2, new[] { GLFW_CONTEXT_VERSION_MINOR, 2 }),
            ("gl34", GLFW_CONTEXT_VERSION_MAJOR, 3, new[] { GLFW_CONTEXT_VERSION_MINOR, 4 }),
            ("profile-before32", GLFW_CONTEXT_VERSION_MAJOR, 3, new[] { GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE }),
            ("invalid-profile", GLFW_OPENGL_PROFILE, 123, null),
            ("robustness", GLFW_CONTEXT_ROBUSTNESS, 123, null), ("release", GLFW_CONTEXT_RELEASE_BEHAVIOR, 123, null),
            ("es12", GLFW_CLIENT_API, GLFW_OPENGL_ES_API, new[] { GLFW_CONTEXT_VERSION_MINOR, 2 }),
            ("forward10", GLFW_OPENGL_FORWARD_COMPAT, 1, null),
            ("unavailable-version", GLFW_CONTEXT_VERSION_MAJOR, 99, null),
            ("stereo", GLFW_STEREO, 1, null),
        };
        foreach (var c in cases)
        {
            Defaults(); Hint(GLFW_VISIBLE, 0); Hint(c.Hint, c.Value);
            if (c.More != null) Hint(c.More[0], c.More[1]);
            var window = Create();
            Record("invalid-context-" + c.Name, window != null);
            Record("invalid-context-error-" + c.Name, Error());
            if (window != null) Destroy(window);
        }
        Defaults(); Hint(GLFW_VISIBLE, 0); Hint(GLFW_CLIENT_API, GLFW_NO_API);
        var w = Create()!;
        if (native) N.glfwGetGLXContext(w.Handle); else G.glfwGetGLXContext(w.Managed!);
        Record("native-glx-with-no-api", Error());
        if (native) N.glfwGetEGLContext(w.Handle); else G.glfwGetEGLContext(w.Managed!);
        Record("native-egl-with-no-api", Error());
        if (native) N.glfwGetOSMesaContext(w.Handle); else G.glfwGetOSMesaContext(w.Managed!);
        Record("native-osmesa-with-no-api", Error());
        Destroy(w); Terminate();
    }
    private static partial class N
    {
        [System.Runtime.InteropServices.DllImport(Lib, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)] internal static extern nint glfwGetGLXContext(nint window);
    }
}
