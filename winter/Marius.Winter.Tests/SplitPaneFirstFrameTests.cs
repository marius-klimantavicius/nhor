using System;
using System.Reflection;
using ThorVG;
using Xunit;

namespace Marius.Winter.Tests;

public class SplitPaneFirstFrameTests
{
    [Theory]
    [InlineData(RenderBackend.SW)]
    [InlineData(RenderBackend.GL)]
    public void BothClippedPanesPaintOnFirstFrameAndAfterResizeHideAndOrientationChange(RenderBackend backend)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(600, 400, "SplitPane first-frame pixels", Theme.Light, backend);
            var first = new Panel { Background = new Color4(1, 0, 0, 1), MinWidth = 500 };
            var second = new Panel { Background = new Color4(0, 1, 0, 1) };
            first.AddChild(new Label("Review é — 日本語 — 👩‍💻") { FontSize = 24 });
            var split = new SplitPane { Position = 140, FirstMinimum = 100, SecondMinimum = 120, First = first, Second = second };
            window.AddChild(split);
            split.Arrange(new RectF(40, 30, 400, 200));
            Frame(window);
            Assert.Equal(0xff0000u, Pixel(window, backend, 50, 150));
            Assert.Equal(0x00ff00u, Pixel(window, backend, 200, 150));
            // The first child explicitly asks for more width than its slot. Its
            // clipping container must protect both the divider and second pane.
            Assert.NotEqual(0xff0000u, Pixel(window, backend, 183, 150));
            for (int i = 0; i < 3; i++)
            {
                split.Position = 280;
                Frame(window);
                Assert.Equal(0xff0000u, Pixel(window, backend, 300, 150));
                Assert.Equal(0x00ff00u, Pixel(window, backend, 340, 150));
                split.Arrange(new RectF(40, 30, 180, 200));
                Frame(window);
                Assert.Equal(0xff0000u, Pixel(window, backend, 50, 150));
                Assert.Equal(0x00ff00u, Pixel(window, backend, 160, 150));
                split.Visible = false;
                Frame(window);
                Assert.NotEqual(0xff0000u, Pixel(window, backend, 50, 150));
                window.Theme = i % 2 == 0 ? Theme.Dark : Theme.Light;
                window.Theme.DefaultFontSize = 24;
                split.Visible = true;
                split.Orientation = Orientation.Vertical;
                split.Position = 90;
                split.Arrange(new RectF(40, 30, 400, 260));
                Frame(window);
                Assert.Equal(0xff0000u, Pixel(window, backend, 50, 100));
                Assert.Equal(0x00ff00u, Pixel(window, backend, 50, 200));
                split.Orientation = Orientation.Horizontal;
                split.Arrange(new RectF(40, 30, 400, 200));
            }
        });
    }

    private static void Frame(Window window)
    {
        WindowRegressionOwner.Flush(window);
        Contracts.Draw((Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!, true);
    }
    private static unsafe uint Pixel(Window window, RenderBackend backend, int x, int y)
    {
        if (backend == RenderBackend.SW) return WindowRuntimeTests.Buffer(window)[y * (int)window.FramebufferWidth + x] & 0xffffffu;
        var finish = (delegate* unmanaged[Cdecl]<void>)Glfw.Glfw.glfwGetProcAddress("glFinish");
        var bind = (delegate* unmanaged[Cdecl]<uint, uint, void>)Glfw.Glfw.glfwGetProcAddress("glBindFramebuffer");
        var readBuffer = (delegate* unmanaged[Cdecl]<uint, void>)Glfw.Glfw.glfwGetProcAddress("glReadBuffer");
        var read = (delegate* unmanaged[Cdecl]<int, int, int, int, uint, uint, void*, void>)Glfw.Glfw.glfwGetProcAddress("glReadPixels");
        var error = (delegate* unmanaged[Cdecl]<uint>)Glfw.Glfw.glfwGetProcAddress("glGetError");
        Assert.True(finish != null && bind != null && readBuffer != null && read != null && error != null);
        finish(); bind(0x8CA8, 0); readBuffer(0x0405);
        uint rgba = 0;
        read(x, (int)window.FramebufferHeight - y - 1, 1, 1, 0x1908, 0x1401, &rgba);
        Assert.Equal(0u, error());
        return ((rgba & 255) << 16) | (rgba & 0xff00) | ((rgba >> 16) & 255);
    }
}
