using System;
using System.Runtime.InteropServices;

namespace Marius.Winter;

internal static class OpenGlLibraryLifetime
{
    private static readonly object Gate = new();
    private static nint _module;

    internal static void Retain()
    {
        if (!OperatingSystem.IsLinux()) return;
        lock (Gate)
        {
            if (_module != 0) return;

            // Own one reference to the actual GL entry point library for the
            // process lifetime, as a native application linked to libGL does.
            // Repeated unloading of libGLdispatch and Mesa GLX can exhaust
            // glibc's static TLS area. Contexts, displays, and GLFW's module
            // references still belong to each Window session and are released.
            nint address = Glfw.Glfw.glfwGetProcAddress("glGetString");
            if (dladdr(address, out var info) == 0 || info.FileName == 0)
                throw new InvalidOperationException("Winter could not locate the native OpenGL entry point library.");
            _module = NativeLibrary.Load(Marshal.PtrToStringUTF8(info.FileName)!);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DlInfo
    {
        internal nint FileName, BaseAddress, SymbolName, SymbolAddress;
    }

    [DllImport("libdl.so.2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int dladdr(nint address, out DlInfo info);
}
