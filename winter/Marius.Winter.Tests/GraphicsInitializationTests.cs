using System;
using Glfw;
using Xunit;

namespace Marius.Winter.Tests;

public class GraphicsInitializationTests
{
    [Fact]
    public void WglTargetPassesBorrowedDeviceContextAsSurface()
    {
        var native = new GlfwContextWGL { dc = 0x1234, handle = 0x5678 };
        var window = new GlfwWindow();
        window.context.wgl = native;

        var target = Window.GetGlCanvasTarget(window);

        Assert.Equal((nint)0, target.Display);
        Assert.Equal(native.dc, target.Surface);
        Assert.Equal(native.handle, target.Context);
        Assert.Same(native, window.context.wgl);
    }

    [Theory]
    [InlineData(RenderBackend.SW)]
    [InlineData(RenderBackend.GL)]
    public void NativeWindowInitializesAndKeepsItsOwnedTarget(RenderBackend backend)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(64, 48, "Graphics initialization", backend: backend);
            var native = window.GlfwWindow!;
            Assert.Same(native, Glfw.Glfw.glfwGetCurrentContext());
            var target = Window.GetGlCanvasTarget(native);
            if (OperatingSystem.IsWindows())
            {
                Assert.NotNull(native.context.wgl);
                Assert.NotEqual((nint)0, target.Surface);
                Assert.Equal(native.context.wgl!.dc, target.Surface);
                Assert.Equal(native.context.wgl.handle, target.Context);
            }
            else
            {
                Assert.NotNull(native.context.glx);
                Assert.Equal((nint)native.context.glx!.window, target.Surface);
                Assert.Equal(native.context.glx.handle, target.Context);
            }
        });
    }
}
