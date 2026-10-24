using System;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Marius.Winter.Tests;

public class GlRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GlWindowPumpKeepsParentSpaceClipsAlignedAfterMove(bool tree)
    {
        Element clipped = tree ? new TreeView() : new ScrollPanel { VerticalScroll = ScrollMode.Never, HorizontalScroll = ScrollMode.Never };
        clipped.Style = new Style { Background = new Color4(1, 0, 0), CornerRadius = 0 };
        using var pump = new NativePump(w => { w.AddChild(clipped); clipped.Arrange(new RectF(10, 10, 40, 40)); }, RenderBackend.GL);
        await Task.Delay(60);
        uint before = 0; await pump.Call(w => { DrawTarget(w); before = ReadPixel(w, 20, 20); });
        Assert.True((before & 255) > ((before >> 8) & 255), $"Expected red GL pixel, got 0x{before:X8}");
        await pump.Call(_ => clipped.Arrange(new RectF(100, 10, 40, 40))); await Task.Delay(60);
        await pump.Call(w => { DrawTarget(w); Assert.Equal(before, ReadPixel(w, 110, 20)); Assert.NotEqual(before, ReadPixel(w, 20, 20)); });
    }

    private static void DrawTarget(Window window)
    {
        // Read the defined back target before presentation. Contents of a
        // swapped buffer are undefined by GLX and cannot be a pixel oracle.
        var canvas = (ThorVG.Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        Contracts.Draw(canvas, true);
    }

    private static unsafe uint ReadPixel(Window window, int x, int y)
    {
        var finish = (delegate* unmanaged[Cdecl]<void>)Glfw.Glfw.glfwGetProcAddress("glFinish");
        var bind = (delegate* unmanaged[Cdecl]<uint, uint, void>)Glfw.Glfw.glfwGetProcAddress("glBindFramebuffer");
        var readBuffer = (delegate* unmanaged[Cdecl]<uint, void>)Glfw.Glfw.glfwGetProcAddress("glReadBuffer");
        var read = (delegate* unmanaged[Cdecl]<int, int, int, int, uint, uint, void*, void>)Glfw.Glfw.glfwGetProcAddress("glReadPixels");
        Assert.True(finish != null && bind != null && readBuffer != null && read != null);
        finish(); bind(0x8CA8, 0); readBuffer(0x0405);
        var getError = (delegate* unmanaged[Cdecl]<uint>)Glfw.Glfw.glfwGetProcAddress("glGetError");
        uint pixel = 0; read(x, (int)window.FramebufferHeight - 1 - y, 1, 1, 0x1908, 0x1401, &pixel);
        Assert.Equal(0u, getError());
        return pixel;
    }

    [Fact]
    public async Task SwUploadRetainsBgraPixelsAndPhysicalStrideAcrossGcAndResize()
    {
        using var pump = new NativePump(w => { w.AddChild(new Box()); });
        await Task.Delay(30);
        await pump.Call(w => Glfw.Glfw.glfwSetWindowSize(w.GlfwWindow!, 250, 140));
        GC.Collect(); GC.WaitForPendingFinalizers(); await Task.Delay(50);
        await pump.Call(AssertUpload);
    }

    private static unsafe void AssertUpload(Window window)
    {
        var bind = (delegate* unmanaged[Cdecl]<uint, uint, void>)Glfw.Glfw.glfwGetProcAddress("glBindTexture");
        var get = (delegate* unmanaged[Cdecl]<uint, int, uint, uint, void*, void>)Glfw.Glfw.glfwGetProcAddress("glGetTexImage");
        Assert.True(bind != null && get != null);
        uint texture = (uint)typeof(Window).GetField("_texId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        var actual = new uint[window.FramebufferWidth * window.FramebufferHeight];
        bind(0x0DE1, texture);
        fixed (uint* pixels = actual) get(0x0DE1, 0, 0x80E1, 0x1401, pixels);
        var expected = WindowRuntimeTests.Buffer(window);
        Assert.Equal(expected, actual);
        Assert.NotEqual(actual[0], actual[40 * window.FramebufferWidth + 40]);
    }
}
