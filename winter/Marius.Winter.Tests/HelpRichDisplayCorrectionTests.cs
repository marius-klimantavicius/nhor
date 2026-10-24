using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Microsoft.AspNetCore.Components;
using ThorVG;
using Xunit;
using B = Marius.Winter.Blazor.Elements;
using Task = System.Threading.Tasks.Task;

namespace Marius.Winter.Tests;

public class HelpRichDisplayCorrectionTests
{
    private const string Square = "<svg xmlns='http://www.w3.org/2000/svg' width='24' height='24'><rect width='24' height='24' fill='#f5b400'/></svg>";
    private static object? Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner);
    private static TooltipOverlay? Tooltip(Window window) => (TooltipOverlay?)Field(window, "_tooltipOverlay");
    private static uint[] Frame(Window window)
    {
        Contracts.Draw((Canvas)Field(window, "_canvas")!, true);
        return WindowRuntimeTests.Buffer(window).ToArray();
    }
    private static Task Parameters<T>(NativePump pump, T component, params (string name, object? value)[] values) where T : IComponent =>
        pump.Window.Dispatcher.InvokeAsync(() => component.SetParametersAsync(ParameterView.FromDictionary(values.ToDictionary(v => v.name, v => v.value))));

    [Fact]
    public async Task StationaryPointer_AsyncWrapperReplacementFallbackClearAndPendingRemovalRefreshOnlyOwner()
    {
        Task<B.Button> mounted = null!;
        var rich = new TooltipContent("Original", Square) { TrailingSvg = Square };
        using var pump = new NativePump(w => mounted = w.UseBlazor().AddComponent<B.Button>(new()
        {
            ["Text"] = "Favorite", ["Tooltip"] = "Plain fallback", ["TooltipContent"] = rich
        }));
        var component = await mounted;
        using var pointer = new OwnedPointer(Glfw.Glfw.glfwGetX11Window(pump.Window.GlfwWindow!));
        Button owner = null!;
        await pump.Call(w => { owner = Assert.IsType<Button>(Assert.Single(w.Children)); owner.Arrange(new RectF(5, 5, 90, 30)); });
        pointer.Move(40, 20);
        await Task.Delay(800);
        await pump.Call(w => Assert.Equal("Original", Field(Assert.IsType<TooltipOverlay>(Tooltip(w)), "_text")));
        var position = pointer.Position();
        using var unrelated = new Panel();
        await pump.Call(w =>
        {
            w.ShowOverlay(unrelated);
            unrelated.Arrange(new RectF(150, 90, 20, 20));
            var current = Tooltip(w);
            unrelated.Tooltip = "Other owner's help";
            Assert.Same(current, Tooltip(w));
        });
        for (int cycle = 0; cycle < 2; cycle++)
        {
            TooltipOverlay old = null!;
            Paint[] oldPictures = null!;
            await pump.Call(w => { old = Assert.IsType<TooltipOverlay>(Tooltip(w)); oldPictures = Paints(old.Scene).OfType<Picture>().Cast<Paint>().ToArray(); });
            await Parameters(pump, component, ("TooltipContent", new TooltipContent("Replacement", Square) { TrailingSvg = Square }));
            await pump.Call(w =>
            {
                var current = Assert.IsType<TooltipOverlay>(Tooltip(w));
                Assert.Equal("Replacement", Field(current, "_text"));
                Assert.Equal(2, Paints(current.Scene).OfType<Picture>().Count());
                Assert.NotSame(old, current);
                Assert.True(old.IsDisposed);
                Assert.All(oldPictures, paint => Assert.Equal(0, paint.RefCnt()));
                Assert.Same(w, unrelated.OwnerWindow);
                Assert.InRange(current.Bounds.X, 4, w.Bounds.W - current.Bounds.W - 4);
            });
            await Parameters(pump, component, ("TooltipContent", null));
            await pump.Call(w =>
            {
                var current = Assert.IsType<TooltipOverlay>(Tooltip(w));
                Assert.Equal("Plain fallback", Field(current, "_text"));
                Assert.Empty(Paints(current.Scene).OfType<Picture>());
            });
            await Parameters(pump, component, ("Tooltip", null));
            await pump.Call(w => { Assert.Null(Tooltip(w)); Assert.Null(Field(w, "_tooltipElement")); });
            await Task.Delay(650);
            await pump.Call(w => Assert.Null(Tooltip(w)));
            // Restoring content under the same pointer starts a fresh delayed show.
            await Parameters(pump, component, ("Tooltip", "Plain fallback"), ("TooltipContent", rich));
            await Task.Delay(750);
            await pump.Call(w => Assert.Equal("Original", Field(Assert.IsType<TooltipOverlay>(Tooltip(w)), "_text")));
        }
        Assert.Equal(position, pointer.Position());
        // A replacement while the delay is pending must show only the latest
        // content; clearing that pending presentation must leave no late help.
        await Parameters(pump, component, ("Tooltip", null), ("TooltipContent", null));
        await Parameters(pump, component, ("TooltipContent", rich));
        await Parameters(pump, component, ("TooltipContent", new TooltipContent("Pending replacement")));
        await Task.Delay(700);
        await pump.Call(w => Assert.Equal("Pending replacement", Field(Assert.IsType<TooltipOverlay>(Tooltip(w)), "_text")));
        await Parameters(pump, component, ("TooltipContent", null));
        await Parameters(pump, component, ("TooltipContent", rich));
        await Parameters(pump, component, ("TooltipContent", null));
        await Task.Delay(650);
        await pump.Call(w => Assert.Null(Tooltip(w)));
        await Parameters(pump, component, ("TooltipContent", rich));
        await pump.Call(w =>
        {
            Assert.Null(Tooltip(w)); // still pending
            w.RemoveChild(owner);
            Assert.False(owner.IsDisposed); // detached resources remain caller-owned
            Assert.Null(Field(w, "_tooltipElement"));
        });
        await Task.Delay(650);
        await pump.Call(w => { Assert.Null(Tooltip(w)); w.RemoveOverlay(unrelated); owner.Dispose(); });
    }

    [Fact]
    public async Task SvgWrapper_DimensionOnlyUpdatesAndIndependentClearsChangePixelsAtFixedPosition()
    {
        Task<B.SvgImage> mounted = null!;
        using var pump = new NativePump(w => mounted = w.UseBlazor().AddComponent<B.SvgImage>(new()
        { ["Svg"] = Square, ["RequestedWidth"] = 64f, ["RequestedHeight"] = 48f }));
        var component = await mounted;
        SvgImage image = null!;
        Picture picture = null!;
        await pump.Call(w => { image = Assert.IsType<SvgImage>(Assert.Single(w.Children)); picture = (Picture)Field(image, "_picture")!; });
        async Task Check(float? width, float? height, int expected)
        {
            await Parameters(pump, component, ("RequestedWidth", width), ("RequestedHeight", height));
            await pump.Call(w =>
            {
                var size = image.Measure(180, 110);
                image.Arrange(new RectF(5, 5, size.X, size.Y));
                var pixels = Frame(w);
                var ink = pixels.Select((p, i) => (p, x: i % (int)w.FramebufferWidth, y: i / (int)w.FramebufferWidth))
                    .Where(p => (p.p & 0xffffff) == 0xf5b400).ToArray();
                Assert.NotEmpty(ink);
                Assert.Equal(5, ink.Min(p => p.x)); Assert.Equal(5, ink.Min(p => p.y));
                Assert.Equal(expected, ink.Max(p => p.x) - ink.Min(p => p.x) + 1);
                Assert.Equal(expected, ink.Max(p => p.y) - ink.Min(p => p.y) + 1);
                Assert.Same(picture, Field(image, "_picture"));
                Assert.Equal(1, picture.RefCnt());
            });
        }
        await Check(64, 48, 48);
        await Check(96, 96, 96);
        await Check(null, 48, 24);
        await Check(64, null, 24);
        await Check(null, null, 24);
        await Check(64, 48, 48);
    }

    [Fact]
    public async Task RichLabelWrapper_InitialAndUpdatedAlphaComposeWithElementOpacityAndClearToTheme()
    {
        Task<B.RichLabel> mounted = null!;
        using var pump = new NativePump(w =>
        {
            w.Theme = Theme.Light;
            mounted = w.UseBlazor().AddComponent<B.RichLabel>(new()
            { ["Text"] = "Read", ["FontSize"] = 32f, ["Color"] = new Color4(1, 0, 0, 0) });
        });
        var component = await mounted;
        RichLabel label = null!;
        uint[] transparent = null!, opaque = null!;
        await pump.Call(w =>
        {
            label = Assert.IsType<RichLabel>(Assert.Single(w.Children));
            label.Arrange(new RectF(5, 5, 180, 90));
            transparent = Frame(w);
            Assert.DoesNotContain(transparent, p => ((p >> 16) & 255) > 180 && ((p >> 8) & 255) < 50);
        });
        await Parameters(pump, component, ("Color", new Color4(1, 0, 0, 1)));
        int sample = 0;
        await pump.Call(w =>
        {
            opaque = Frame(w);
            sample = Array.FindIndex(opaque, p => (p & 0xffffff) == 0xff0000);
            Assert.True(sample >= 0, "Opaque text must contain fully covered red ink.");
        });
        async Task CheckAlpha(float alpha, float elementOpacity)
        {
            await Parameters(pump, component, ("Color", new Color4(1, 0, 0, alpha)), ("Opacity", elementOpacity));
            await pump.Call(w =>
            {
                var frame = Frame(w);
                float factor = alpha * elementOpacity;
                foreach (int shift in new[] { 0, 8, 16 })
                {
                    float expected = ((transparent[sample] >> shift) & 255) * (1 - factor) + ((opaque[sample] >> shift) & 255) * factor;
                    Assert.InRange((int)((frame[sample] >> shift) & 255), (int)expected - 3, (int)expected + 3);
                }
                if (alpha == 0) Assert.Equal(transparent, frame);
            });
        }
        await CheckAlpha(.5f, 1);
        await CheckAlpha(.5f, .5f);
        await CheckAlpha(0, 1);
        await Parameters(pump, component, ("Color", null));
        await pump.Call(w =>
        {
            Assert.Equal(w.Theme.TextColor, label.Color);
            Assert.Equal(w.Theme.TextColor.A8, ((Text)Field(label, "_textPaint")!).Opacity());
            Assert.False(transparent.SequenceEqual(Frame(w)));
            w.Theme = Theme.Dark;
            Assert.Equal(w.Theme.TextColor.A8, ((Text)Field(label, "_textPaint")!).Opacity());
            Assert.False(transparent.SequenceEqual(Frame(w)));
        });
        // Text alpha must not leak into SVG mode.
        await Parameters(pump, component, ("Text", null), ("Svg", Square), ("Color", new Color4(1, 0, 0, 0)));
        await pump.Call(w => Assert.Contains(Frame(w), p => (p & 0xffffff) == 0xf5b400));
    }

    private static IEnumerable<Paint> Paints(Scene scene)
    {
        foreach (var paint in scene.Paints())
        {
            yield return paint;
            if (paint is Scene child) foreach (var nested in Paints(child)) yield return nested;
        }
    }

    // The test launcher supplies only its private Xvfb DISPLAY. This connection
    // injects a real hover and verifies that parameter updates never move it.
    private sealed class OwnedPointer : IDisposable
    {
        private readonly nint _display;
        private readonly nuint _window;
        internal OwnedPointer(nuint window)
        {
            _window = window;
            _display = XOpenDisplay(Environment.GetEnvironmentVariable("DISPLAY")!);
            Assert.NotEqual(0, _display);
        }
        internal void Move(int x, int y) { XWarpPointer(_display, 0, _window, 0, 0, 0, 0, x, y); XSync(_display, 0); }
        internal (int, int) Position()
        {
            Assert.NotEqual(0, XQueryPointer(_display, _window, out _, out _, out _, out _, out int x, out int y, out _));
            return (x, y);
        }
        public void Dispose() => XCloseDisplay(_display);
        [DllImport("libX11.so.6")] private static extern nint XOpenDisplay(string name);
        [DllImport("libX11.so.6")] private static extern int XCloseDisplay(nint display);
        [DllImport("libX11.so.6")] private static extern int XSync(nint display, int discard);
        [DllImport("libX11.so.6")] private static extern int XWarpPointer(nint display, nuint source, nuint destination, int sx, int sy, uint sw, uint sh, int x, int y);
        [DllImport("libX11.so.6")] private static extern int XQueryPointer(nint display, nuint window, out nuint root, out nuint child, out int rx, out int ry, out int x, out int y, out uint mask);
    }
}
