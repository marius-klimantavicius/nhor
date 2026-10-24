using System;
using System.Reflection;
using ThorVG;
using Xunit;

namespace Marius.Winter.Tests;

public class NativeSessionLifetimeTests
{
    [Theory]
    [InlineData(RenderBackend.SW)]
    [InlineData(RenderBackend.GL)]
    public void SequentialSessionsKeepRenderingAndReleaseNativeWindows(RenderBackend backend)
    {
        // Both backends use a native GL context. More than 200 complete sessions
        // reproduced loader exhaustion, even with only one Window ever live.
        for (int session = 1; session <= 256; session++)
        {
            // Match the suite's dedicated owner threads, joining each owner
            // before starting the next native session.
            WindowRegressionOwner.Run(() =>
            {
                Window? window = null;
                var failure = Record.Exception(() => window = new Window(64, 48, "Sequential session", backend: backend));
                Assert.True(failure == null, $"Session {session} ({backend}) must create a native context: {failure}");
                using (window!)
                {
                    var panel = new Panel
                    {
                        Background = new Color4(1, 0, 0), CornerRadius = new CornerRadius(0)
                    };
                    window!.AddChild(panel);
                    panel.Arrange(new RectF(0, 0, 64, 48));
                    WindowRegressionOwner.Flush(window);
                    var canvas = (Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
                    Contracts.Draw(canvas, true);
                    Assert.Equal(0xff0000u, Pixel(window, backend));
                    Assert.Same(window.GlfwWindow, Glfw.Glfw.glfwGetCurrentContext());
                }
                Assert.True(window.IsDisposed);
                Assert.Null(window.GlfwWindow);
                Assert.False(Glfw._glfw.initialized);
                Assert.Null(Glfw._glfw.contextSlot);
                Assert.Null(Glfw._glfw.windowListHead);
            });
        }
    }

    private static unsafe uint Pixel(Window window, RenderBackend backend)
    {
        if (backend == RenderBackend.SW)
            return WindowRuntimeTests.Buffer(window)[16 * 64 + 16] & 0xffffffu;
        // Read the defined target before presentation; swapped buffers are undefined.
        var read = (delegate* unmanaged[Cdecl]<int, int, int, int, uint, uint, void*, void>)Glfw.Glfw.glfwGetProcAddress("glReadPixels");
        var error = (delegate* unmanaged[Cdecl]<uint>)Glfw.Glfw.glfwGetProcAddress("glGetError");
        Assert.True(read != null && error != null);
        uint rgba = 0;
        read(16, 16, 1, 1, 0x1908, 0x1401, &rgba);
        Assert.Equal(0u, error());
        return ((rgba & 255) << 16) | (rgba & 0xff00) | ((rgba >> 16) & 255);
    }
}
