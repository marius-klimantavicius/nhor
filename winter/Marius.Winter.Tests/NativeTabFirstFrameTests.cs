using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using ThorVG;
using Xunit;

namespace Marius.Winter.Tests;

public class NativeTabFirstFrameTests
{
    [Theory]
    [InlineData("Rich", RenderBackend.SW)]
    [InlineData("Flex", RenderBackend.SW)]
    [InlineData("Grid", RenderBackend.SW)]
    [InlineData("Controls", RenderBackend.SW)]
    [InlineData("Rich", RenderBackend.GL)]
    [InlineData("Flex", RenderBackend.GL)]
    [InlineData("Grid", RenderBackend.GL)]
    [InlineData("Controls", RenderBackend.GL)]
    public void ActivatedTabsPaintAllChildrenInTheirFirstFrame(string kind, RenderBackend backend)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(900, 650, "Native tab first frame", Theme.Light, backend);
            var tabs = new TabWidget();
            window.AddChild(tabs);
            tabs.AddTab("Initial", new Panel());
            var markers = new List<Element>();
            Element content = Content(kind, markers);
            tabs.AddTab(kind, content);
            var requested = new RectF(380, 28, 500, 350);
            tabs.Measure(500, 350);
            tabs.Arrange(requested);
            Frame(window);
            Assert.Equal(0, content.Bounds.W);

            for (int activation = 0; activation < 3; activation++)
            {
                tabs.ActiveTab = 1;
                Frame(window);
                Assert.Equal(requested, tabs.Bounds);
                AssertMarkers(window, backend, markers);
                // A clean subsequent frame must retain the same visible pixels.
                Frame(window);
                AssertMarkers(window, backend, markers);
                tabs.ActiveTab = 0;
                Frame(window);
                Assert.Equal(0, content.Bounds.W);
                Assert.Equal(0, content.Bounds.H);
                requested = new RectF(380, 28, 520, 370);
                tabs.Arrange(requested);
            }
        });
    }

    [Theory]
    [InlineData(false, false, RenderBackend.SW)]
    [InlineData(false, true, RenderBackend.SW)]
    [InlineData(true, false, RenderBackend.SW)]
    [InlineData(true, true, RenderBackend.SW)]
    [InlineData(false, false, RenderBackend.GL)]
    [InlineData(false, true, RenderBackend.GL)]
    [InlineData(true, false, RenderBackend.GL)]
    [InlineData(true, true, RenderBackend.GL)]
    public void GrowingViewportPaintsNewlyExposedPartsOfUnchangedChildren(bool nested, bool taffy, RenderBackend backend)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(300, 240, "Native viewport growth", Theme.Light, backend);
            var outer = Scroll();
            window.AddChild(outer);
            var marker = new SolidMarker(160, 40);
            ScrollPanel parent = outer;
            if (nested)
            {
                var inner = Scroll();
                inner.MinHeight = inner.MaxHeight = 100;
                outer.AddChild(inner);
                parent = inner;
            }
            if (taffy)
            {
                var panel = new Panel { Layout = new StackLayout { CrossAlignment = Alignment.Start }, Padding = new Thickness(0) };
                parent.AddChild(panel);
                panel.AddChild(marker);
            }
            else parent.AddChild(marker);

            outer.Arrange(new RectF(20, 20, 80, 120));
            Frame(window);
            Assert.Equal(0xff0000u, Pixel(window, backend, 40, 40));
            Assert.NotEqual(0xff0000u, Pixel(window, backend, 150, 40));
            var markerBounds = marker.Bounds;
            outer.Arrange(new RectF(20, 20, 210, 120));
            Frame(window);
            Assert.Equal(markerBounds, marker.Bounds);
            // This point lies inside the unchanged red rectangle and inside
            // the enlarged viewport. It was outside the original viewport.
            Assert.Equal(0xff0000u, Pixel(window, backend, 150, 40));
            Assert.NotEqual(0xff0000u, Pixel(window, backend, 235, 40));
            outer.Visible = false;
            Frame(window);
            Assert.Equal(0, outer.Bounds.W);
            outer.Visible = true;
            Frame(window);
            Assert.Equal(new RectF(20, 20, 210, 120), outer.Bounds);
            Assert.Equal(0xff0000u, Pixel(window, backend, 150, 40));
        });
    }

    private static ScrollPanel Scroll() => new()
    {
        Layout = new StackLayout { Spacing = 10, CrossAlignment = Alignment.Start },
        Padding = new Thickness(0),
        VerticalScroll = ScrollMode.Never,
        HorizontalScroll = ScrollMode.Never
    };

    private static Element Content(string kind, List<Element> markers)
    {
        if (kind == "Controls")
        {
            var panel = new Panel { Layout = new StackLayout { Spacing = 10, CrossAlignment = Alignment.Start } };
            panel.AddChild(new Label("Controls in a tab"));
            panel.AddChild(new Button("Tab Button"));
            panel.AddChild(new Checkbox("Tab Checkbox", true));
            var marker = new SolidMarker(60, 24);
            panel.AddChild(marker); markers.Add(marker);
            return panel;
        }
        var scroll = Scroll();
        if (kind == "Rich")
        {
            scroll.AddChild(new Label("SvgImage Control") { FontSize = 18 });
            var svg = new SvgImage("<svg xmlns='http://www.w3.org/2000/svg' width='64' height='40'><rect width='64' height='40' fill='red'/></svg>");
            scroll.AddChild(svg); markers.Add(svg);
            scroll.AddChild(new Label("RichLabel Control") { FontSize = 18 });
            scroll.AddChild(new RichLabel("Visible on activation"));
            var marker = new SolidMarker(60, 24);
            scroll.AddChild(marker); markers.Add(marker);
            scroll.AddChild(new RichTextBox("Hello World!\nSecond line"));
        }
        else
        {
            scroll.AddChild(new Label(kind + " heading"));
            var row = new Panel { Padding = new Thickness(0), Layout = kind == "Flex"
                ? new FlexLayout { Direction = Orientation.Horizontal, Gap = 8 }
                : new GridLayout { Columns = new[] { TrackSize.Fr(1), TrackSize.Fr(1) }, ColumnGap = 8 } };
            for (int i = 0; i < 2; i++)
            {
                var marker = new SolidMarker(60, 24);
                row.AddChild(marker); markers.Add(marker);
            }
            scroll.AddChild(row);
            var lower = new SolidMarker(60, 24);
            scroll.AddChild(lower); markers.Add(lower);
        }
        scroll.AddChild(new Panel { MinHeight = 500, MaxHeight = 500 });
        return scroll;
    }

    private static void AssertMarkers(Window window, RenderBackend backend, List<Element> markers)
    {
        foreach (var marker in markers)
        {
            Assert.True(marker.Bounds.W > 10 && marker.Bounds.H > 10);
            float x = 5, y = 5;
            for (Element? ancestor = marker; ancestor is not null; ancestor = ancestor.Parent)
            {
                x += ancestor.Bounds.X;
                y += ancestor.Bounds.Y;
            }
            uint pixel = Pixel(window, backend, (int)x, (int)y);
            Assert.True(pixel == 0xff0000u, $"{marker.GetType().Name} should paint red at ({x},{y}) on activation; got 0x{pixel:X6}");
        }
    }

    private sealed class SolidMarker(float width, float height) : Element
    {
        private readonly Shape _shape = Shape.Gen();
        protected override void OnAttached()
        {
            if (_shape.Parent() is not null) return;
            _shape.SetFill(255, 0, 0);
            AddPaint(_shape);
        }
        protected override Vector2 MeasureCore(float availableWidth, float availableHeight) => new(width, height);
        protected override void OnSizeChanged()
        {
            _shape.ResetShape();
            _shape.AppendRect(0, 0, Bounds.W, Bounds.H);
        }
    }

    private static void Frame(Window window)
    {
        WindowRegressionOwner.Flush(window);
        var canvas = (Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        Contracts.Draw(canvas, true);
    }

    private static unsafe uint Pixel(Window window, RenderBackend backend, int x, int y)
    {
        if (backend == RenderBackend.SW)
            return WindowRuntimeTests.Buffer(window)[y * (int)window.FramebufferWidth + x] & 0xffffffu;
        // GLX leaves swapped buffers undefined. Read only our defined target,
        // after Draw/Sync and before any swap, as in GlRuntimeTests.
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
        return ((rgba & 255) << 16) | (rgba & 0xff00) | ((rgba >> 16) & 255);
    }
}
