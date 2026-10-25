using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ThorVG;
using Xunit;
using static Glfw.GLFW;

namespace Marius.Winter.Tests;

public class LogViewReviewRegressionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const int Width = 500, Height = 240;

    [Theory]
    [InlineData(RenderBackend.SW, "default")]
    [InlineData(RenderBackend.SW, "monospace")]
    [InlineData(RenderBackend.GL, "default")]
    [InlineData(RenderBackend.GL, "monospace")]
    public void AccentedLinesMatchIndependentlyMeasuredFullLinePixels(RenderBackend backend, string font) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(Width, Height, "Log accented line placement", Theme.Light, backend);
        using var log = new LogView { Text = "ÉÉÉ\nMg\nŽŴÁ\ne\u0301", FontName = font, FollowTail = false };
        window.AddChild(log);
        for (int stage = 0; stage < 4; stage++)
        {
            if (stage == 1) log.FontName = font == "default" ? "monospace" : "default";
            if (stage == 2) { window.Theme = Theme.Dark; log.FontSize = 22; }
            log.Arrange(new RectF(0, 0, 460, stage == 3 ? 43 : 180));
            log.OnKeyDown(GLFW_KEY_HOME, 0, false);
            var actual = Frame(window, backend);
            var paints = ((Scene)typeof(LogView).GetField("_contentScene", Private)!.GetValue(log)!).Paints().OfType<Text>().ToArray();
            float size = (float)typeof(LogView).GetField("_effectiveFontSize", Private)!.GetValue(log)!;
            string[] lines = log.Text.Split('\n');
            var reference = Text.Gen();
            try
            {
                reference.SetFont(log.FontName); reference.SetFontSize(size);
                for (int i = 0; i < paints.Length && i < lines.Length; i++)
                {
                    // The original contract aligns each complete line's ink top
                    // with its row. Measure the full source independently: never
                    // read the candidate's baseline or cached vertical metrics.
                    reference.SetText(lines[i]);
                    reference.Bounds(out _, out float top, out _, out _);
                    paints[i].SetText(lines[i]);
                    paints[i].Translate(8, 8 + i * size * 1.35f - top);
                }
                var expected = Frame(window, backend);
                string tag = $"accents-{backend}-{font}-{stage}";
                Save(tag + "-actual", actual); Save(tag + "-reference", expected);
                long error = actual.Zip(expected).Sum(pair => RgbError(pair.First, pair.Second));
                int maxDelta = actual.Zip(expected).Max(pair => ChannelDelta(pair.First, pair.Second));
                Console.WriteLine($"{tag}: RGB error={error}, max channel delta={maxDelta}");
                // Only allow a few AA rounding samples, never missing accents
                // or the vertical displacement that moves them above the clip.
                Assert.InRange(maxDelta, 0, 32);
                Assert.InRange(error, 0, 3 * 255);
            }
            finally { reference.Unref(); }
        }
    });

    [Theory]
    [InlineData(RenderBackend.SW, "last", false)]
    [InlineData(RenderBackend.SW, "last", true)]
    [InlineData(RenderBackend.SW, "", false)]
    [InlineData(RenderBackend.SW, "", true)]
    [InlineData(RenderBackend.GL, "last", false)]
    [InlineData(RenderBackend.GL, "last", true)]
    [InlineData(RenderBackend.GL, "", false)]
    [InlineData(RenderBackend.GL, "", true)]
    public void EndAndShiftEndRevealShortOrEmptyFinalLineAfterHorizontalInspection(RenderBackend backend, string last, bool shift) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(Width, Height, "Log mixed-width End", Theme.Light, backend);
        string text = new string('x', 500) + "\n" + last;
        using var log = new LogView { Text = text, FollowTail = false };
        window.AddChild(log); log.Arrange(new RectF(0, 0, 460, 100));
        log.OnKeyDown(GLFW_KEY_HOME, 0, false);
        log.OnScroll(-10000, 0);
        Assert.True(log.HorizontalScrollOffset > 1000);
        log.OnMouseDown(0, 100, 18); log.OnMouseMove(300, 18); log.OnMouseUp(0, 300, 18);
        Assert.NotEmpty(log.SelectedText);
        log.OnKeyDown(GLFW_KEY_C, GLFW_MOD_CONTROL, false);
        Assert.Equal(log.SelectedText, Glfw.Glfw.glfwGetClipboardString(window.GlfwWindow!));
        log.OnKeyDown(GLFW_KEY_HOME, 0, false);
        Assert.Equal(0, log.HorizontalScrollOffset); Assert.False(log.FollowTail);
        log.OnScroll(-10000, 0);
        log.OnKeyDown(GLFW_KEY_END, shift ? GLFW_MOD_SHIFT : 0, false);
        Save($"end-{backend}-{last.Length}-{shift}", Frame(window, backend));
        Assert.Equal(text.Length, log.SelectionStart + log.SelectionLength);
        Assert.Equal(shift ? text : "", log.SelectedText);
        Assert.True(log.FollowTail);
        // A short final row must remain visible, including its beginning. A
        // trailing empty row has its sole caret at the same left clip edge.
        Assert.Equal(0, log.HorizontalScrollOffset);
        Assert.Equal(501, Hit(log, 8, 36));
        Assert.Equal(text.Length, Hit(log, 452, 36));
        if (shift)
        {
            log.OnKeyDown(GLFW_KEY_C, GLFW_MOD_CONTROL, false);
            Assert.Equal(text, Glfw.Glfw.glfwGetClipboardString(window.GlfwWindow!));
        }
        log.OnKeyDown(GLFW_KEY_END, 0, false);
        log.OnKeyDown(GLFW_KEY_LEFT, GLFW_MOD_SHIFT, false);
        Assert.Equal(last.Length == 0 ? "\n" : "t", log.SelectedText);
        log.OnKeyDown(GLFW_KEY_C, GLFW_MOD_CONTROL, false);
        Assert.Equal(log.SelectedText, Glfw.Glfw.glfwGetClipboardString(window.GlfwWindow!));
        log.OnKeyDown(GLFW_KEY_END, 0, false);
        log.Append("\nnext"); Assert.True(log.FollowTail);
        log.OnKeyDown(GLFW_KEY_HOME, GLFW_MOD_SHIFT, false);
        Assert.Equal(0, log.HorizontalScrollOffset); Assert.False(log.FollowTail);
        Assert.Equal(text, log.SelectedText);
        log.OnKeyDown(GLFW_KEY_END, GLFW_MOD_SHIFT, false);
        Assert.Equal("\nnext", log.SelectedText); Assert.True(log.FollowTail);
    });

    private static int Hit(LogView log, float x, float y) => (int)typeof(LogView).GetMethod("GetCharIndexAtPosition", Private)!.Invoke(log, [x, y])!;
    private static int ChannelDelta(uint a, uint b) => new[] { 0, 8, 16 }.Max(shift => Math.Abs((int)((a >> shift) & 255) - (int)((b >> shift) & 255)));
    private static long RgbError(uint a, uint b) => new[] { 0, 8, 16 }.Sum(shift => (long)Math.Abs((int)((a >> shift) & 255) - (int)((b >> shift) & 255)));

    private static unsafe uint[] Frame(Window window, RenderBackend backend)
    {
        Contracts.Draw((Canvas)typeof(Window).GetField("_canvas", Private)!.GetValue(window)!, true);
        if (backend == RenderBackend.SW) return (uint[])WindowRuntimeTests.Buffer(window).Clone();
        var finish = (delegate* unmanaged[Cdecl]<void>)Glfw.Glfw.glfwGetProcAddress("glFinish");
        var bind = (delegate* unmanaged[Cdecl]<uint, uint, void>)Glfw.Glfw.glfwGetProcAddress("glBindFramebuffer");
        var buffer = (delegate* unmanaged[Cdecl]<uint, void>)Glfw.Glfw.glfwGetProcAddress("glReadBuffer");
        var read = (delegate* unmanaged[Cdecl]<int, int, int, int, uint, uint, void*, void>)Glfw.Glfw.glfwGetProcAddress("glReadPixels");
        var error = (delegate* unmanaged[Cdecl]<uint>)Glfw.Glfw.glfwGetProcAddress("glGetError");
        Assert.True(finish != null && bind != null && buffer != null && read != null && error != null);
        finish(); bind(0x8CA8, 0); buffer(0x0405);
        var raw = new uint[Width * Height]; fixed (uint* p = raw) read(0, 0, Width, Height, 0x1908, 0x1401, p);
        Assert.Equal(0u, error());
        var result = new uint[raw.Length];
        for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
        {
            uint c = raw[(Height - 1 - y) * Width + x];
            result[y * Width + x] = (c & 0xff00ff00) | ((c & 255) << 16) | ((c >> 16) & 255);
        }
        return result;
    }

    private static void Save(string name, uint[] pixels)
    {
        if (Environment.GetEnvironmentVariable("WINTER_LOGVIEW_EVIDENCE") is not { } dir) return;
        using var stream = File.Create(Path.Combine(dir, name + ".ppm"));
        stream.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{Width} {Height}\n255\n"));
        foreach (uint c in pixels) { stream.WriteByte((byte)(c >> 16)); stream.WriteByte((byte)(c >> 8)); stream.WriteByte((byte)c); }
    }
}
