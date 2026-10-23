using System;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;
using static Glfw.GLFW;
using static Marius.Winter.Blazor.Tests.UnicodeEditingTests;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native editors")]
public sealed class CaretPerformanceTests(EditorWindow host, ITestOutputHelper output)
{
    [Fact]
    public void RepeatedAttachedLongPathCaretAndHitTestingHaveBoundedAllocations()
    {
        var box = (TextBox)host.Attach("TextBox", "/home/user/projects/" + new string('a', 480) + ".txt");
        box.Bounds = new RectF(0, 0, 180, 30);
        for (int i = 0; i < 6; i++) Exercise(box);
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 12; i++) Exercise(box);
        long perIteration = (GC.GetAllocatedBytesForCurrentThread() - start) / 12;
        output.WriteLine($"End + mouse hit/drag: {perIteration:N0} bytes/iteration after warmup");
        Assert.True(perIteration < 16_000, $"Repeated cached caret/hit operations allocated {perIteration:N0} bytes/iteration.");
        Assert.InRange(Caret(box), 0, box.Text.Length);
    }

    private static void Exercise(TextBox box)
    {
        Key(box, GLFW_KEY_END);
        box.OnMouseDown(0, 75, 15);
        box.OnMouseMove(95, 15);
        box.OnMouseUp(0, 95, 15);
    }

    [Fact]
    public void CaretMetricsAndHitPlacementRefreshForTextFontThemeAndLayoutChanges()
    {
        var box = (TextBox)host.Attach("TextBox", "iiiiWWWW");
        AssertWidth(box);
        box.Text = "WWWWiiii😀";
        Key(box, GLFW_KEY_END);
        AssertWidth(box);
        var style = box.Style.Clone();
        style.FontSize = 30;
        box.Style = style;
        AssertWidth(box);
        style.FontName = "monospace";
        AssertWidth(box); // Mutable font fields must also change the cache key.
        box.Bounds = new RectF(0, 0, 340, 44);
        Key(box, GLFW_KEY_HOME);
        box.OnMouseDown(0, 800, 20);
        box.OnMouseUp(0, 800, 20);
        Assert.Equal(box.Text.Length, Caret(box));
        AssertWidth(box);

        var themed = (TextBox)host.Attach("TextBox", "WWWiii");
        var oldTheme = host.Window.Theme;
        var theme = Theme.Dark;
        theme.TextBox.FontSize = 24;
        theme.TextBox.FontName = "monospace";
        try
        {
            host.Window.Theme = theme;
            Key(themed, GLFW_KEY_END);
            AssertWidth(themed);
        }
        finally { host.Window.Theme = oldTheme; }
    }

    private static void AssertWidth(TextBox box)
    {
        Key(box, GLFW_KEY_END);
        var cursorX = (Func<float>)typeof(TextBox).GetMethod("GetCursorX", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate(typeof(Func<float>), box);
        // Independent reference from the renderer's public font/advance APIs.
        var measure = ThorVG.Text.Gen();
        measure.Ref();
        try
        {
            measure.SetFont(box.Style.FontName);
            measure.SetFontSize(box.Style.FontSize);
            measure.SetText("M");
            Assert.Equal(ThorVG.Result.Success, measure.GetMetrics(out var metrics));
            float height = metrics.ascent - metrics.descent;
            measure.SetFontSize(box.Style.FontSize * box.Style.FontSize / height);
            measure.SetText(box.Text);
            Assert.Equal(ThorVG.Result.Success, measure.GetTextSize(out float width, out _));
            Assert.True(width > 0);
            Assert.Equal(width, cursorX(), 2);
            measure.SetText(box.Text[..4]); // These fixtures start with four ASCII letters.
            Assert.Equal(ThorVG.Result.Success, measure.GetTextSize(out float prefixWidth, out _));
            Key(box, GLFW_KEY_HOME);
            float hitX = box.Bounds.H * 0.3f + prefixWidth;
            box.OnMouseDown(0, hitX, box.Bounds.H / 2);
            box.OnMouseUp(0, hitX, box.Bounds.H / 2);
            Assert.Equal(4, Caret(box));
            Key(box, GLFW_KEY_END);
        }
        finally { measure.Unref(); }
    }
}
