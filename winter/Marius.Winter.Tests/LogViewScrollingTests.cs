using System;
using System.Linq;
using System.Reflection;
using Task = System.Threading.Tasks.Task;
using ThorVG;
using Xunit;
using static Glfw.GLFW;

namespace Marius.Winter.Tests;

public class LogViewScrollingTests
{
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
