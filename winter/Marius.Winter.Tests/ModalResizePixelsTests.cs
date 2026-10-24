using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Xunit;

namespace Marius.Winter.Tests;

public class ModalResizePixelsTests
{
    [Theory]
    [InlineData(false, RenderBackend.SW, false)]
    [InlineData(true, RenderBackend.SW, false)]
    [InlineData(false, RenderBackend.GL, false)]
    [InlineData(true, RenderBackend.GL, false)]
    [InlineData(false, RenderBackend.SW, true)]
    [InlineData(true, RenderBackend.SW, true)]
    [InlineData(false, RenderBackend.GL, true)]
    [InlineData(true, RenderBackend.GL, true)]
    public void ActualResizePaintsCenteredTitleAndCompleteSaveCancelGlyphsAfterSettling(bool razor, RenderBackend backend, bool dark) => WindowRegressionOwner.Run(() =>
    {
        var theme = dark ? Theme.Dark : Theme.Light;
        foreach (var style in new[] { theme.TextBox, theme.Panel, theme.Button, theme.Label, theme.Checkbox }) style.FontSize = 22;
        using var window = new Window(800, 620, "C06 settled resize pixels", theme, backend);
        if (razor) Assert.True(window.UseBlazor().AddComponent<Blazor.Playground.Scenarios.WindowsAndDialogs>(new Dictionary<string, object> { ["OwnerWindow"] = window }).IsCompletedSuccessfully);
        else
        {
            var scenario = new Playground.Scenarios.WindowsAndDialogsScenario(window);
            window.AddChild(scenario.Content); window.SetAutoLayout(scenario.Content);
        }
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new SelectionX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                using var screen = new ScreenPixels(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                var tag = $"resize-{razor}-{backend}-{dark}";
                await Task.Delay(170); x.Focus(); x.Key(0xff09); x.Key(0xff0d); await Task.Delay(80);
                ModalDialog modal = null!; Label title = null!; Button save = null!, cancel = null!;
                RectF titleBig = default, saveBig = default, cancelBig = default;
                await window.Dispatcher.InvokeAsync(() =>
                {
                    modal = ModalScenarioInputTests.Walk(window).OfType<ModalDialog>().Single();
                    var elements = ModalScenarioInputTests.Walk(modal.ModalRoot).ToArray();
                    title = elements.OfType<Label>().Single(l => l.Text == "Edit profile");
                    save = elements.OfType<Button>().Single(b => b.Text == "Save"); cancel = elements.OfType<Button>().Single(b => b.Text == "Cancel");
                    titleBig = ModalScenarioInputTests.Absolute(title); saveBig = ModalScenarioInputTests.Absolute(save); cancelBig = ModalScenarioInputTests.Absolute(cancel);
                });
                var baseline = screen.Read(800, 620);
                int titleInk = baseline.Ink(titleBig, theme.WindowBackground), saveInk = baseline.Ink(saveBig, theme.WindowBackground), cancelInk = baseline.Ink(cancelBig, theme.WindowBackground);
                Assert.True(titleInk > 80 && saveInk > 30 && cancelInk > 30, "The large-frame reference must contain actual title and action glyphs.");
                x.Resize(340, 240);
                var pending = screen.Read(340, 240); pending.Save(tag + "-resize-pending");
                var ready = pending;
                var until = DateTime.UtcNow.AddSeconds(3);
                // Observe the actual front buffer without dispatcher posts, extra
                // drawing, input or dirty mutations that could manufacture a redraw.
                while ((!ready.HasCardAtLeftMargin(theme.WindowBackground) || ready.Ink(new RectF(20, 18, 220, 42), theme.WindowBackground) < titleInk * .80) && DateTime.UtcNow < until)
                { await Task.Delay(25); ready = screen.Read(340, 240); }
                ready.Save(tag + "-settled");
                Assert.True(ready.HasCardAtLeftMargin(theme.WindowBackground), "Settled pixels must place the card at the new eight-unit margin, not the old 800-wide center.");
                Assert.True(ready.Ink(new RectF(20, 18, 220, 42), theme.WindowBackground) >= titleInk * .80, "The complete title must be visibly painted near the new top-left card header.");
                RectF saveSmall = default, cancelSmall = default;
                await window.Dispatcher.InvokeAsync(() =>
                {
                    Assert.Equal(340, window.Bounds.W); Assert.Equal(240, window.Bounds.H);
                    Assert.Equal(8, modal.DialogBounds.X); Assert.Equal(8, modal.DialogBounds.Y);
                    saveSmall = ModalScenarioInputTests.Absolute(save); cancelSmall = ModalScenarioInputTests.Absolute(cancel);
                    Assert.InRange(saveSmall.Bottom, 0, 240); Assert.InRange(cancelSmall.Right, 0, 340);
                });
                Assert.True(ready.Ink(saveSmall, theme.WindowBackground) >= saveInk * .80, "Save glyphs must paint completely in the resized footer.");
                Assert.True(ready.Ink(cancelSmall, theme.WindowBackground) >= cancelInk * .80, "Cancel glyphs must paint completely in the resized footer.");
                await Task.Delay(350);
                var later = screen.Read(340, 240); later.Save(tag + "-later");
                Assert.True(later.HasCardAtLeftMargin(theme.WindowBackground));
                Assert.True(later.Ink(new RectF(20, 18, 220, 42), theme.WindowBackground) >= titleInk * .80);
                Assert.True(later.Ink(saveSmall, theme.WindowBackground) >= saveInk * .80);
                Assert.True(later.Ink(cancelSmall, theme.WindowBackground) >= cancelInk * .80);
                x.CloseNormally();
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult();
    });

    private sealed class ScreenPixels : IDisposable
    {
        private readonly nint _display;
        private readonly nuint _window;
        internal ScreenPixels(nuint window) { _window = window; _display = XOpenDisplay(Environment.GetEnvironmentVariable("DISPLAY")!); Assert.NotEqual(0, _display); }
        internal Frame Read(int width, int height)
        {
            var handle = XGetImage(_display, _window, 0, 0, (uint)width, (uint)height, nuint.MaxValue, 2); Assert.NotEqual(0, handle);
            try
            {
                var image = Marshal.PtrToStructure<XImage>(handle); Assert.Equal(32, image.BitsPerPixel);
                var bytes = new byte[image.BytesPerLine * height]; Marshal.Copy(image.Data, bytes, 0, bytes.Length);
                return new Frame(width, height, image.BytesPerLine, bytes);
            }
            finally { XDestroyImage(handle); }
        }
        public void Dispose() => XCloseDisplay(_display);
        [StructLayout(LayoutKind.Sequential)] private struct XImage
        {
            internal int Width, Height, XOffset, Format; internal nint Data;
            internal int ByteOrder, BitmapUnit, BitmapBitOrder, BitmapPad, Depth, BytesPerLine, BitsPerPixel;
        }
        [DllImport("libX11.so.6")] private static extern nint XOpenDisplay(string display);
        [DllImport("libX11.so.6")] private static extern int XCloseDisplay(nint display);
        [DllImport("libX11.so.6")] private static extern nint XGetImage(nint display, nuint window, int x, int y, uint width, uint height, nuint mask, int format);
        [DllImport("libX11.so.6")] private static extern int XDestroyImage(nint image);
    }
    private sealed record Frame(int Width, int Height, int Stride, byte[] Bytes)
    {
        internal bool HasCardAtLeftMargin(Color4 color)
        {
            // Sample the flat inner padding, avoiding the antialiased rounded
            // border. Dark's stock window color is translucent (230/255 alpha).
            // Bound its actual composite over the .48-black backdrop; do not
            // mistake that valid composite for the uncomposited source RGB.
            bool Channel(byte pixel, byte source) => pixel >= source * color.A - 3 && pixel <= source * color.A + 255 * .52f * (1 - color.A) + 3;
            foreach (int y in new[] { 16, 45, 80, 140, 200 })
            {
                int at = y * Stride + 16 * 4;
                if (!Channel(Bytes[at + 2], color.R8) || !Channel(Bytes[at + 1], color.G8) || !Channel(Bytes[at], color.B8)) return false;
            }
            return true;
        }
        internal int Ink(RectF bounds, Color4 background)
        {
            int count = 0;
            for (int y = Math.Max(0, (int)bounds.Y + 4); y < Math.Min(Height, bounds.Bottom - 4); y++)
                for (int x = Math.Max(0, (int)bounds.X + 5); x < Math.Min(Width, bounds.Right - 5); x++)
                {
                    int at = y * Stride + x * 4;
                    int difference = Math.Abs(Bytes[at + 2] - background.R8) + Math.Abs(Bytes[at + 1] - background.G8) + Math.Abs(Bytes[at] - background.B8);
                    if (difference > 280) count++;
                }
            return count;
        }
        internal void Save(string name)
        {
            var folder = Environment.GetEnvironmentVariable("WINTER_C06_RESIZE_EVIDENCE");
            if (string.IsNullOrEmpty(folder)) return;
            Directory.CreateDirectory(folder);
            using var stream = File.Create(Path.Combine(folder, name + ".ppm"));
            stream.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{Width} {Height}\n255\n"));
            for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
            { int at = y * Stride + x * 4; stream.WriteByte(Bytes[at + 2]); stream.WriteByte(Bytes[at + 1]); stream.WriteByte(Bytes[at]); }
        }
    }
}
