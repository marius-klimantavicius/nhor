using System;
using System.Runtime.InteropServices;
using G = Glfw.Glfw;
using static Glfw.GLFW;

internal static unsafe partial class Program
{
    private static void ValidateClipboard()
    {
        InitHint(GLFW_PLATFORM, GLFW_PLATFORM_X11); Check(Init() == 1, "clipboard init");
        string? value = native ? Marshal.PtrToStringUTF8(N.glfwGetClipboardString(0)) : G.glfwGetClipboardString(null);
        Record("external-clipboard", value); Record("clipboard-error", Error());
        Check(value != null, "external clipboard transfer");
        Terminate();
    }
    private static partial class N
    {
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern nint glfwGetClipboardString(nint window);
    }
}
