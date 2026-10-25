using System;
using System.Linq;
using System.IO;
using System.Reflection;
using Task = System.Threading.Tasks.Task;
using ThorVG;
using Xunit;
using static Glfw.GLFW;

namespace Marius.Winter.Tests;

public class LogViewScrollingTests
{
    [Fact]
    public void WheelWithoutOverflowKeepsFollowingTheBuffer() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(400, 200, "Log no-op wheel");
        var buffer = new LogBuffer(); buffer.Append("ready");
        using var log = new LogView { Buffer = buffer };
        window.AddChild(log); log.Arrange(new RectF(0, 0, 350, 100));
        int callbacks = 0; log.FollowTailChanged += _ => callbacks++;
        log.OnScroll(0, -1);
        Assert.True(log.FollowTail); Assert.Equal(0, callbacks);
        buffer.Append(string.Concat(Enumerable.Repeat("\nnext", 20)));
        Assert.True(log.ScrollOffset > 0);
    });

    [Fact]
    public void ScrolledProportionalGlyphPixelsMatchTheCompleteLine() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(500, 220, "Log viewport renderer", Theme.Light, RenderBackend.SW);
        using var log = new LogView { Text = string.Concat(Enumerable.Repeat("AVATAR To e\u0301 Ž ", 25)), FontName = "default" };
        window.AddChild(log); log.Arrange(new RectF(0, 0, 460, 100));
        log.OnKeyDown(GLFW_KEY_END, 0, false);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var canvas = (Canvas)typeof(Window).GetField("_canvas", flags)!.GetValue(window)!;
        Contracts.Draw(canvas, true);
        var visible = (uint[])WindowRuntimeTests.Buffer(window).Clone();
        var scene = (Scene)typeof(LogView).GetField("_contentScene", flags)!.GetValue(log)!;
        var paint = scene.Paints().OfType<Text>().Single();
        Console.WriteLine($"visible text={paint.GetText()} transform={paint.Transform().e13} scroll={log.HorizontalScrollOffset}");
        float baseline = (float)typeof(LogView).GetField("_baselineY", flags)!.GetValue(log)!;
        // Independent reference: send the exact complete line to ThorVG with
        // the same clip and font, as before viewport rendering was introduced.
        paint.SetText(log.Text); paint.Translate(8 - log.HorizontalScrollOffset, 8 - baseline);
        Contracts.Draw(canvas, true);
        var complete = WindowRuntimeTests.Buffer(window);
        int differing = visible.Zip(complete).Count(pair => pair.First != pair.Second);
        int maxDelta = visible.Zip(complete).Max(pair => Math.Max(
            Math.Abs((int)(pair.First & 255) - (int)(pair.Second & 255)), Math.Max(
            Math.Abs((int)((pair.First >> 8) & 255) - (int)((pair.Second >> 8) & 255)),
            Math.Abs((int)((pair.First >> 16) & 255) - (int)((pair.Second >> 16) & 255)))));
        long error = visible.Zip(complete).Sum(pair => (long)
            Math.Abs((int)(pair.First & 255) - (int)(pair.Second & 255)) +
            Math.Abs((int)((pair.First >> 8) & 255) - (int)((pair.Second >> 8) & 255)) +
            Math.Abs((int)((pair.First >> 16) & 255) - (int)((pair.Second >> 16) & 255)));
        Console.WriteLine($"viewport vs full renderer: differing={differing}, maximum channel delta={maxDelta}, total RGB error={error}");
        if (Environment.GetEnvironmentVariable("WINTER_LOGVIEW_EVIDENCE") is { } folder)
        {
            Save(Path.Combine(folder, "visible.ppm"), visible);
            Save(Path.Combine(folder, "complete.ppm"), complete);
        }
        // Rebasing font-unit paths changes floating-point rounding at a few
        // antialiased edges. Bound both the worst sample and total error (less
        // than one black/white RGB pixel), rather than require bitwise identity.
        Assert.InRange(maxDelta, 0, 32);
        Assert.InRange(error, 0, 3 * 255);
        static void Save(string path, uint[] pixels)
        {
            using var stream = File.Create(path);
            stream.Write(System.Text.Encoding.ASCII.GetBytes("P6\n500 220\n255\n"));
            foreach (uint pixel in pixels) { stream.WriteByte((byte)(pixel >> 16)); stream.WriteByte((byte)(pixel >> 8)); stream.WriteByte((byte)pixel); }
        }
    });

    [Fact]
    public void AppendThatCompletesAGraphemeKeepsSelectionOnLegalBoundaries() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(400, 200, "Log appended grapheme");
        var buffer = new LogBuffer(); buffer.Append("e");
        using var log = new LogView { Buffer = buffer };
        window.AddChild(log); log.Arrange(new RectF(0, 0, 350, 100));
        log.OnKeyDown(GLFW_KEY_A, GLFW_MOD_CONTROL, false);
        buffer.Append("\u0301");
        Assert.Equal("e\u0301", log.SelectedText);
        Assert.Equal(2, log.SelectionLength);
    });

    [Fact]
    public void VerticalWheelPagesScrollbarAndHeldSelectionScrollThroughMultilineText() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(500, 300, "Log two axes");
        using var log = new LogView { Text = string.Join('\n', Enumerable.Range(0, 50).Select(i => $"line {i:00} " + new string('x', 100))), FollowTail = false };
        window.AddChild(log); log.Arrange(new RectF(0, 0, 450, 140));
        log.OnKeyDown(GLFW_KEY_HOME, 0, false);
        log.OnScroll(0, -3); Assert.True(log.ScrollOffset > 0);
        log.OnMouseDown(0, 8, 20); log.OnMouseMove(60, 20); log.OnMouseUp(0, 60, 20);
        Assert.StartsWith("line ", log.SelectedText); Assert.DoesNotContain("line 00", log.SelectedText);
        float old = log.ScrollOffset;
        log.OnKeyDown(GLFW_KEY_PAGE_DOWN, 0, false); Assert.True(log.ScrollOffset > old);
        log.OnKeyDown(GLFW_KEY_PAGE_UP, 0, false); Assert.Equal(old, log.ScrollOffset, 2);
        log.OnKeyDown(GLFW_KEY_HOME, 0, false);
        log.OnMouseDown(0, 438, 20); log.OnMouseMove(438, 115); log.OnMouseUp(0, 438, 115);
        Assert.True(log.ScrollOffset > old);
        log.OnKeyDown(GLFW_KEY_HOME, 0, false);
        log.OnMouseDown(0, 20, 20); log.OnMouseMove(470, 160);
        for (int i = 0; i < 12; i++) window.Animator.Tick(.02f);
        Assert.True(log.ScrollOffset > 0); Assert.True(log.HorizontalScrollOffset > 0);
        log.Visible = false; Assert.False(window.Animator.HasActiveAnimations);
        log.Visible = true;
        var scroll = (log.ScrollOffset, log.HorizontalScrollOffset);
        window.Animator.Tick(.1f); Assert.Equal(scroll, (log.ScrollOffset, log.HorizontalScrollOffset));
    });

    [Fact]
    public async Task WorkerBufferDeliveryAndDisposalCancelSubscriptionsAndAutoscroll()
    {
        var buffer = new LogBuffer(100, 10000);
        LogView log = null!;
        using var pump = new NativePump(window =>
        {
            log = new LogView { Buffer = buffer };
            window.AddChild(log); log.Arrange(new RectF(0, 0, 300, 100));
        });
        await Task.Run(() => buffer.Append("worker " + new string('x', 400)));
        await pump.Call(window =>
        {
            Assert.Equal(buffer.Snapshot.Text, log.Text);
            log.OnMouseDown(0, 20, 20); log.OnMouseMove(400, 20);
            Assert.True(window.Animator.HasActiveAnimations);
            log.Dispose(); Assert.False(window.Animator.HasActiveAnimations);
        });
        await Task.Run(() => buffer.Append(" after dispose"));
        await pump.Call(_ => Assert.DoesNotContain("after dispose", log.Text));
    }

    [Theory]
    [InlineData("default")]
    [InlineData("monospace")]
    public void CacheAdvancesMatchFullRendererForLongKerningAndUnicodeLines(string font) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(600, 250, "Log cached renderer geometry");
        string text = string.Concat(Enumerable.Repeat("AVATAR To Ž e\u0301 👩‍💻  ", 100));
        using var log = new LogView { Text = text, FontName = font };
        window.AddChild(log); log.Arrange(new RectF(0, 0, 500, 100));
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        float size = (float)typeof(LogView).GetField("_effectiveFontSize", flags)!.GetValue(log)!;
        using var measure = new LogFontMeasure(font, size);
        var layout = new LogTextLayout(text, measure);
        var paint = Text.Gen();
        try
        {
            paint.SetFont(font); paint.SetFontSize(size);
            foreach (int offset in new[] { 0, 6, 19, text.Length / 2, text.Length })
            {
                // The chosen repeated-string offsets are scalar-safe; legal
                // carets are independently floored for combining/emoji content.
                var editing = new TextEditingBoundaries(); editing.SetText(text);
                int caret = editing.Floor(offset);
                paint.SetText(text[..caret]); paint.GetTextSize(out float width, out _);
                Assert.InRange(Math.Abs(layout.Advance(caret) - width), 0, .1f);
            }
        }
        finally { paint.Unref(); }
    });
}
