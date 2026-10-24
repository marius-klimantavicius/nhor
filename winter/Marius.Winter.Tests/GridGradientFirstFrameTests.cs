using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using ThorVG;
using Xunit;
using Xunit.Abstractions;

namespace Marius.Winter.Tests;

public class GridGradientFirstFrameTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(RenderBackend.SW)]
    [InlineData(RenderBackend.GL)]
    public void GridActivationPaintsButtonGradientsBeforeScrolling(RenderBackend backend)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(900, 650, "Grid gradient activation", Theme.Light, backend);
            var tabs = new TabWidget();
            window.AddChild(tabs);
            tabs.AddTab("Initial", new Panel());
            var (scroll, buttons) = PlaygroundGrid();
            tabs.AddTab("Grid", scroll);
            var requested = new RectF(380, 28, 500, 350);
            tabs.Measure(500, 350);
            tabs.Arrange(requested);
            Frame(window);

            for (int activation = 0; activation < 3; activation++)
            {
                tabs.ActiveTab = 1;
                Frame(window);
                Assert.Equal(0, scroll.Scroll);
                var initial = Samples(window, backend, buttons);
                var bounds = buttons.ConvertAll(button => button.Bounds);
                // Ordinary scrolling supplies a demonstrated correct reference.
                // Return to the exact same offset and button coordinates; no
                // hovered/focused button or text/border pixels enter the oracle.
                scroll.OnScroll(0, -1);
                Frame(window);
                scroll.OnScroll(0, 1);
                Frame(window);
                Assert.Equal(0, scroll.Scroll);
                Assert.Equal(bounds, buttons.ConvertAll(button => button.Bounds));
                var reference = Samples(window, backend, buttons);
                AssertShading(buttons, reference);
                for (int i = 0; i < buttons.Count; i++)
                    output.WriteLine($"activation {activation}, {i}:{buttons[i].Text}: initial {initial[i]}, after scroll {reference[i]}");
                Assert.Equal(reference, initial);
                Frame(window);
                Assert.Equal(reference, Samples(window, backend, buttons));
                tabs.ActiveTab = 0;
                Frame(window);
                requested = new RectF(380, 28, 520, 350);
                tabs.Arrange(requested);
            }
        });
    }

    [Theory]
    [InlineData(RenderBackend.SW)]
    [InlineData(RenderBackend.GL)]
    public void RevealingViewportPreparesPreviouslyClippedGradientFills(RenderBackend backend)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(360, 250, "Gradient viewport reveal", Theme.Light, backend);
            var scroll = new ScrollPanel { VerticalScroll = ScrollMode.Never, HorizontalScroll = ScrollMode.Never };
            window.AddChild(scroll);
            // The button's own transform/size stays identical when its viewport
            // grows. Its retained fill must still be prepared on first reveal.
            var button = new Button("Retained gradient");
            scroll.AddChild(button);
            button.Arrange(new RectF(100, 15, 140, 24));
            scroll.Arrange(new RectF(20, 20, 50, 80));
            Frame(window);
            var bounds = button.Bounds;
            scroll.Arrange(new RectF(20, 20, 280, 100));
            Frame(window);
            Assert.Equal(bounds, button.Bounds);
            var buttons = new List<Button> { button };
            AssertShading(buttons, Samples(window, backend, buttons));
            scroll.Visible = false;
            Frame(window);
            scroll.Visible = true;
            Frame(window);
            Assert.Equal(bounds, button.Bounds);
            AssertShading(buttons, Samples(window, backend, buttons));
        });
    }

    private static (ScrollPanel, List<Button>) PlaygroundGrid()
    {
        // The shipped native Playground's first three Grid sections, including
        // unequal tracks, automatic placement, and spanning cells.
        var scroll = new ScrollPanel { Layout = new StackLayout { Spacing = 12, CrossAlignment = Alignment.Stretch } };
        var buttons = new List<Button>();
        void Add(Panel panel, string text, GridItem? item = null)
        {
            var button = new Button(text) { LayoutData = item };
            panel.AddChild(button);
            buttons.Add(button);
        }
        Panel Grid(TrackSize[] columns) => new() { Layout = new GridLayout { Columns = columns, ColumnGap = 6, RowGap = 6 } };
        scroll.AddChild(new Label("Grid — 3 columns (1fr, 2fr, 1fr)"));
        var grid1 = Grid(new[] { TrackSize.Fr(1), TrackSize.Fr(2), TrackSize.Fr(1) });
        foreach (string text in new[] { "1:1", "1:2 (wider)", "1:3", "2:1", "2:2", "2:3" }) Add(grid1, text);
        scroll.AddChild(grid1);
        scroll.AddChild(new Label("Grid — Mixed: 100px, auto, 1fr"));
        var grid2 = Grid(new[] { TrackSize.Px(100), TrackSize.Auto(), TrackSize.Fr(1) });
        foreach (string text in new[] { "100px", "Auto", "Fill", "100px", "Auto Wider", "Fill" }) Add(grid2, text);
        scroll.AddChild(grid2);
        scroll.AddChild(new Label("Grid — Spanning cells"));
        var grid3 = Grid(new[] { TrackSize.Fr(1), TrackSize.Fr(1), TrackSize.Fr(1) });
        Add(grid3, "Spans 2 columns", new GridItem { Column = 0, Row = 0, ColumnSpan = 2 });
        Add(grid3, "1x1", new GridItem { Column = 2, Row = 0 });
        Add(grid3, "Spans 3 columns", new GridItem { Column = 0, Row = 1, ColumnSpan = 3 });
        for (int i = 0; i < 3; i++) Add(grid3, "Auto R2");
        scroll.AddChild(grid3);
        scroll.AddChild(new Panel { MinHeight = 100, MaxHeight = 100 });
        return (scroll, buttons);
    }

    private static List<(int Top, int Bottom)> Samples(Window window, RenderBackend backend, List<Button> buttons)
    {
        var samples = new List<(int, int)>();
        foreach (var button in buttons)
        {
            Vector2 position = default;
            for (Element? element = button; element is not null; element = element.Parent)
                position += new Vector2(element.Bounds.X, element.Bounds.Y);
            int x = (int)position.X + 8;
            int top = Pixel(window, backend, x, (int)position.Y + 4);
            int bottom = Pixel(window, backend, x, (int)(position.Y + button.Bounds.H) - 5);
            samples.Add((top, bottom));
        }
        return samples;
    }

    private static void AssertShading(List<Button> buttons, List<(int Top, int Bottom)> samples)
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            // The default light button declares a 234 -> 216 neutral ramp.
            // At these interior rows it must have both fill and downward shade;
            // background-only, flat, border/text-only, or a red proxy fails.
            Assert.True(samples[i].Top is >= 229 and <= 234 && samples[i].Bottom is >= 217 and <= 227 &&
                        samples[i].Top - samples[i].Bottom >= 5,
                $"{i}:{buttons[i].Text} expected light neutral gradient, got {samples[i]}");
        }
    }

    private static void Frame(Window window)
    {
        WindowRegressionOwner.Flush(window);
        var canvas = (Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        Contracts.Draw(canvas, true);
    }

    private static unsafe int Pixel(Window window, RenderBackend backend, int x, int y)
    {
        uint rgb;
        if (backend == RenderBackend.SW)
            rgb = WindowRuntimeTests.Buffer(window)[y * (int)window.FramebufferWidth + x] & 0xffffffu;
        else
        {
            // Read the defined GL target before swapping, as in GlRuntimeTests.
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
            rgb = ((rgba & 255) << 16) | (rgba & 0xff00) | ((rgba >> 16) & 255);
        }
        int red = (int)(rgb >> 16), green = (int)((rgb >> 8) & 255), blue = (int)(rgb & 255);
        Assert.Equal(red, green);
        Assert.Equal(red, blue);
        return red;
    }
}
