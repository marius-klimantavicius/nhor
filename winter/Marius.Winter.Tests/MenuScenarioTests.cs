using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Xunit;

namespace Marius.Winter.Tests;

public class MenuScenarioTests
{
    [Theory]
    [InlineData(false, false, RenderBackend.SW)] [InlineData(true, false, RenderBackend.SW)]
    [InlineData(false, true, RenderBackend.SW)] [InlineData(true, true, RenderBackend.SW)]
    [InlineData(false, false, RenderBackend.GL)] [InlineData(true, false, RenderBackend.GL)]
    [InlineData(false, true, RenderBackend.GL)] [InlineData(true, true, RenderBackend.GL)]
    public void ActualModulesPaintNavigateConvertLiveCheckContextResetAndCloseWithPopup(bool razor, bool darkLarge, RenderBackend backend) => WindowRegressionOwner.Run(() =>
    {
        var theme = darkLarge ? Theme.Dark : Theme.Light;
        if (darkLarge) foreach (var s in new[] { theme.Panel, theme.Button, theme.Label, theme.MenuBar }) s.FontSize = 22;
        using var window = new Window(820, 720, "C09 Preview and commands", theme, backend);
        using var scenario = razor ? null : new Playground.Scenarios.PreviewAndCommandsScenario(window);
        if (razor) Assert.True(window.UseBlazor().AddComponent<Blazor.Playground.Scenarios.PreviewAndCommands>().IsCompletedSuccessfully);
        else { window.AddChild(scenario!.Content); window.SetAutoLayout(scenario.Content); }
        var input = Task.Run(async () =>
        {
            try
            {
                var tag = $"menus-{razor}-{darkLarge}-{backend}";
                using var x = new FormX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                using var screen = new MenuScreen(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                int width = 820, height = 720;
                await Task.Delay(180); x.Focus();
                Button Button(string text) => Walk(window).OfType<Button>().Single(b => b.IsEffectivelyVisible && b.Text == text);
                MenuBar Bar() => Walk(window).OfType<MenuBar>().Single();
                Menu Menu() => Bar().Menus.Single();
                void Result(string starts) => Assert.Contains(Walk(window).OfType<Label>(), l => l.Text.StartsWith(starts, StringComparison.Ordinal));
                async Task Click(Element e)
                {
                    RectF bounds = default; await window.Dispatcher.InvokeAsync(() => bounds = Absolute(e));
                    x.Move((int)(bounds.X + Math.Min(15, bounds.W / 2)), (int)(bounds.Y + bounds.H / 2)); x.Click(); await Task.Delay(45);
                }
                async Task Row(MenuPopup p, int index)
                { x.Move((int)p.Bounds.X + 40, (int)(p.Bounds.Y + 2 + index * (theme.MenuBar.FontSize + 10) + 10)); x.Click(); await Task.Delay(50); }
                void Capture(string suffix) => x.Capture(tag + suffix, width, height);
                RectF imageBounds = default, titleBounds = default;
                await window.Dispatcher.InvokeAsync(() =>
                {
                    imageBounds = Absolute(Walk(window).OfType<SvgImage>().Single());
                    titleBounds = Absolute(Walk(window).OfType<Label>().Single(l => l.Text == "Preview and commands"));
                    Assert.False(Menu().Entries[0].Enabled); Assert.True(Menu().Entries[1].IsChecked); Result("No saved preview.");
                });
                var first = screen.Read(width, height); Assert.True(first.Ink(titleBounds, darkLarge) > 80);
                Assert.True(first.Count(imageBounds, (r,g,b) => b > r + 20 && g > r + 10) > 1000, "The very first frame must contain the real blue SVG preview."); Capture("-first");
                await Click(Button("Harbor.svg preview"));
                await Click(Bar());
                MenuPopup disabled = null!;
                await window.Dispatcher.InvokeAsync(() => disabled = Assert.Single(MenuInteractionTests.Popups(window)));
                Capture("-disabled-checked"); await Row(disabled, 0);
                await window.Dispatcher.InvokeAsync(() => { Assert.Same(disabled, Assert.Single(MenuInteractionTests.Popups(window))); Result("Source:"); });
                x.Key(0xff1b); await Task.Delay(30);
                await Click(Button("Convert (simulated)")); x.Key(0xffc7); await Task.Delay(70);
                MenuPopup live = null!;
                await window.Dispatcher.InvokeAsync(() => { live = Assert.Single(MenuInteractionTests.Popups(window)); Assert.False(Menu().Entries[0].Enabled); });
                var before = screen.Read(width, height); var rowBounds = new RectF(live.Bounds.X + 25, live.Bounds.Y + 2, 130, theme.MenuBar.FontSize + 10);
                await Task.Delay(460);
                await window.Dispatcher.InvokeAsync(() => { Assert.Same(live, Assert.Single(MenuInteractionTests.Popups(window))); Assert.True(Menu().Entries[0].Enabled); Result("Converted:"); });
                var after = screen.Read(width, height); Assert.True(after.Ink(rowBounds, darkLarge) > before.Ink(rowBounds, darkLarge) + 20, "Conversion must visibly enable Save while the same popup stays open."); Capture("-live-enabled");
                await Row(live, 0); await window.Dispatcher.InvokeAsync(() => Result("Saved:"));
                x.Key(0xffc7); x.Key(0xff54); x.Key(0xff0d); await Task.Delay(80);
                await window.Dispatcher.InvokeAsync(() => { Assert.False(Menu().Entries[1].IsChecked); Result("View: compact."); });
                x.Key(0xffc7); x.Key(0xff57); x.Key(0xff53); await Task.Delay(80);
                await window.Dispatcher.InvokeAsync(() => Assert.Equal(2, MenuInteractionTests.Popups(window).Length)); Capture("-nested");
                x.Key(0xff51); await Task.Delay(35); await window.Dispatcher.InvokeAsync(() => Assert.Single(MenuInteractionTests.Popups(window)));
                x.Key(0xff53); x.Key(0xff1b); await Task.Delay(35); await window.Dispatcher.InvokeAsync(() => Assert.Single(MenuInteractionTests.Popups(window)));
                x.Key(0xff53); x.Resize(460, 650); width = 460; height = 650; await Task.Delay(140);
                await window.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var p in MenuInteractionTests.Popups(window)) { Assert.InRange(p.Bounds.X, 0, 460 - p.Bounds.W); Assert.InRange(p.Bounds.Y, 0, 650 - p.Bounds.H); }
                }); Capture("-nested-narrow");
                x.Key(0xff54); x.Key(0xff0d); await Task.Delay(70); await window.Dispatcher.InvokeAsync(() => Result("Zoom: 125%."));
                await Click(Button("Harbor.svg preview")); x.Key(0xff67); await Task.Delay(70);
                await window.Dispatcher.InvokeAsync(() => Assert.Single(MenuInteractionTests.Popups(window))); Capture("-context-keyboard");
                x.Key(0xff0d); await Task.Delay(60); await window.Dispatcher.InvokeAsync(() => Result("Source:"));
                x.Chord(0xffe1, 0xffc7); x.Key(0xff57); x.Key(0xff0d); await Task.Delay(70); await window.Dispatcher.InvokeAsync(() => Result("Saved:"));
                screen.RightClick(); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => Assert.Single(MenuInteractionTests.Popups(window))); Capture("-context-pointer");
                x.Key(0xff1b);
                MenuItemDef removed = null!; Action stale = null!;
                await window.Dispatcher.InvokeAsync(() => { removed = Menu().Entries[3].SubMenu!.Entries.Single(i => i.Label == "150%"); stale = removed.Action!; });
                await Click(Button("Reverse zoom")); await Click(Button("Toggle 150%"));
                await window.Dispatcher.InvokeAsync(() =>
                {
                    Assert.Equal(new[] { "125%", "100%" }, Menu().Entries[3].SubMenu!.Entries.Select(i => i.Label));
                    Assert.Null(removed.Action); stale(); Assert.DoesNotContain(Walk(window).OfType<Label>(), l => l.Text == "Zoom: 150%.");
                });
                await Click(Button("Toggle preview")); await Click(Button("Toggle preview"));
                await Click(Button("Toggle commands")); await Click(Button("Toggle commands"));
                await Click(Button("Reset"));
                await window.Dispatcher.InvokeAsync(() => { Assert.False(Menu().Entries[0].Enabled); Assert.True(Menu().Entries[1].IsChecked); Result("No saved preview."); }); Capture("-reset-narrow");
                await Click(Button("Convert (simulated)")); x.Key(0xffc7); await Task.Delay(50);
                await window.Dispatcher.InvokeAsync(() => Assert.Single(MenuInteractionTests.Popups(window))); Capture("-normal-close-open");
                x.CloseNormally();
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult(); Assert.True(window.IsDisposed);
    });

    internal static IEnumerable<Element> Walk(Element root) { yield return root; foreach (var c in root.Children) foreach (var e in Walk(c)) yield return e; }
    internal static RectF Absolute(Element e) { e.WindowToLocal(0, 0, out var x, out var y); return new RectF(-x, -y, e.Bounds.W, e.Bounds.H); }
}

internal sealed class MenuScreen : IDisposable
{
    private readonly nint _display;
    private readonly nuint _window;
    internal MenuScreen(nuint window) { _window = window; _display = XOpenDisplay(Environment.GetEnvironmentVariable("DISPLAY")!); Assert.NotEqual(0, _display); }
    internal void RightClick() { XTestFakeButtonEvent(_display, 3, 1, 0); XTestFakeButtonEvent(_display, 3, 0, 20); XSync(_display, 0); }
    internal Frame Read(int width, int height)
    {
        var handle = XGetImage(_display, _window, 0, 0, (uint)width, (uint)height, nuint.MaxValue, 2); Assert.NotEqual(0, handle);
        try { var image = Marshal.PtrToStructure<XImage>(handle); Assert.Equal(32, image.BitsPerPixel); var bytes = new byte[image.BytesPerLine * height]; Marshal.Copy(image.Data, bytes, 0, bytes.Length); return new Frame(width, height, image.BytesPerLine, bytes); }
        finally { XDestroyImage(handle); }
    }
    internal sealed record Frame(int Width, int Height, int Stride, byte[] Pixels)
    {
        internal int Ink(RectF rect, bool dark) => Count(rect, (r,g,b) => dark ? r > 170 && g > 170 && b > 170 : r < 100 && g < 100 && b < 100);
        internal int Count(RectF rect, Func<int,int,int,bool> predicate)
        {
            int count = 0;
            for (int y = Math.Max(0, (int)rect.Y); y < Math.Min(Height, (int)rect.Bottom); y++)
                for (int x = Math.Max(0, (int)rect.X); x < Math.Min(Width, (int)rect.Right); x++) { int i = y * Stride + x * 4; if (predicate(Pixels[i+2], Pixels[i+1], Pixels[i])) count++; }
            return count;
        }
    }
    public void Dispose() => XCloseDisplay(_display);
    [StructLayout(LayoutKind.Sequential)] private struct XImage { internal int Width, Height, XOffset, Format; internal nint Data; internal int ByteOrder, BitmapUnit, BitmapBitOrder, BitmapPad, Depth, BytesPerLine, BitsPerPixel; }
    [DllImport("libX11.so.6")] private static extern nint XOpenDisplay(string name);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(nint display);
    [DllImport("libX11.so.6")] private static extern int XSync(nint display, int discard);
    [DllImport("libXtst.so.6")] private static extern int XTestFakeButtonEvent(nint display, uint button, int press, nuint delay);
    [DllImport("libX11.so.6")] private static extern nint XGetImage(nint display, nuint window, int x, int y, uint width, uint height, nuint mask, int format);
    [DllImport("libX11.so.6")] private static extern int XDestroyImage(nint image);
}
