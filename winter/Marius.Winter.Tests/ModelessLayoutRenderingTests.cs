using System.Linq;
using System.Reflection;
using ThorVG;
using Xunit;

namespace Marius.Winter.Tests;

public class ModelessLayoutRenderingTests
{
    [Theory]
    [InlineData(RenderBackend.SW)]
    [InlineData(RenderBackend.GL)]
    public void OutOfFlowWindowKeepsOwnedPaintOrderAndInteractiveOverflow(RenderBackend backend) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(500, 400, "Modeless layout paint order", Theme.Light, backend);
        var parent = new Panel { Layout = new FlexLayout { Direction = Orientation.Vertical, Gap = 11 }, Padding = new Thickness(0) };
        window.AddChild(parent);
        var normal = new Panel { MinHeight = 20, MaxHeight = 20 };
        parent.AddChild(normal);
        parent.Arrange(new RectF(20, 30, 420, 60));
        var original = normal.Bounds;
        var dialog = new DialogWindow("Owned modeless") { Bounds = new RectF(60, 100, 280, 170), Layout = new StackLayout() };
        var body = new Checkbox("Body remains interactive"); dialog.AddChild(body);
        parent.AddChild(dialog);
        var canvas = (Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        void Draw() { WindowRegressionOwner.Flush(window); Contracts.Draw(canvas, true); }
        Draw();
        Assert.Equal(original, normal.Bounds);
        Assert.Equal(60, parent.Bounds.H);
        Assert.Same(parent, dialog.Parent); Assert.Same(window, dialog.OwnerWindow); Assert.Same(window, body.OwnerWindow);
        Assert.Same(dialog.Scene, parent.Scene.Paints().Last());
        Assert.Same(dialog, parent.HitTest(100, 145));
        Assert.Same(body, parent.HitTest(20 + 60 + body.Bounds.X + 15, 30 + 100 + body.Bounds.Y + 10));
        int top = Shade(window, backend, 94, 135), bottom = Shade(window, backend, 94, 155);
        Assert.True(top > bottom + 5, $"Owned overflowing header must paint its classic gradient: {top}, {bottom}");
        dialog.Visible = false; Draw();
        Assert.Equal(original, normal.Bounds);
        Assert.NotEqual(bottom, Shade(window, backend, 94, 155));
        dialog.Visible = true; Draw();
        Assert.Equal(bottom, Shade(window, backend, 94, 155));
        Assert.Same(dialog, parent.HitTest(100, 145));
    });

    private static unsafe int Shade(Window window, RenderBackend backend, int x, int y)
    {
        if (backend == RenderBackend.SW)
            return (int)(WindowRuntimeTests.Buffer(window)[y * (int)window.FramebufferWidth + x] & 255);
        var finish = (delegate* unmanaged[Cdecl]<void>)Glfw.Glfw.glfwGetProcAddress("glFinish");
        var bind = (delegate* unmanaged[Cdecl]<uint, uint, void>)Glfw.Glfw.glfwGetProcAddress("glBindFramebuffer");
        var readBuffer = (delegate* unmanaged[Cdecl]<uint, void>)Glfw.Glfw.glfwGetProcAddress("glReadBuffer");
        var read = (delegate* unmanaged[Cdecl]<int, int, int, int, uint, uint, void*, void>)Glfw.Glfw.glfwGetProcAddress("glReadPixels");
        var error = (delegate* unmanaged[Cdecl]<uint>)Glfw.Glfw.glfwGetProcAddress("glGetError");
        Assert.True(finish != null && bind != null && readBuffer != null && read != null && error != null);
        finish(); bind(0x8CA8, 0); readBuffer(0x0405);
        uint rgba = 0;
        read(x, (int)window.FramebufferHeight - 1 - y, 1, 1, 0x1908, 0x1401, &rgba);
        Assert.Equal(0u, error());
        return (int)(rgba & 255);
    }
}
