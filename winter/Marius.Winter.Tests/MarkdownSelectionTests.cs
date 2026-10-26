using System;
using System.Linq;
using System.Reflection;
using ThorVG;
using Xunit;
using static Glfw.GLFW;

namespace Marius.Winter.Tests;

public class MarkdownSelectionTests
{
    private static MarkdownLayout Layout(MarkdownView view) => (MarkdownLayout)typeof(MarkdownView)
        .GetField("_layout", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view)!;

    [Fact]
    public void ProjectionSeparatorsDecorationsAndAltTextStayStableAcrossWrapping()
    {
        using var view = new MarkdownView("# Title\n\nA **bold** &amp; *style*\\\nhard.\n\n> quote\n\n3. first\n4. second\n\n- [x] done\n\n| A | B |\n| --- | --- |\n| x | y |\n\n![badge](asset:badge)\n\n---\n\nlast");
        const string expected = "Title\n\nA bold & style\nhard.\n\nquote\n\nfirst\nsecond\n\ndone\n\nA\tB\nx\ty\n\nbadge\n\nlast";
        Assert.Equal(expected, view.Text);
        view.SelectAll(); Assert.Equal(expected, view.SelectedText);
        view.Bounds = new(0, 0, 100, 150); _ = view.ContentSize;
        Assert.Equal(expected, view.SelectedText);
        view.ClearSelection(); Assert.Equal("", view.SelectedText); Assert.Equal(0, view.SelectionLength);
    }

    [Fact]
    public void GlobalGraphemesAcrossStyleBoundariesAndSurrogatesAreAtomic()
    {
        using var view = new MarkdownView("a**\u0301** 👩🏽‍💻 end");
        Assert.Equal("a\u0301 👩🏽‍💻 end", view.Text);
        view.Select(1, 1); Assert.Equal("a\u0301", view.SelectedText);
        view.Select(5, 1); Assert.Equal("👩🏽‍💻", view.SelectedText);
        view.Select(1, 0); Assert.Equal(0, view.SelectionStart);
        view.OnKeyDown(GLFW_KEY_RIGHT, GLFW_MOD_SHIFT, false); Assert.Equal("a\u0301", view.SelectedText);
        view.OnKeyDown(GLFW_KEY_RIGHT, 0, false); Assert.Equal(2, view.SelectionStart); Assert.Equal(0, view.SelectionLength);
        view.OnKeyDown(GLFW_KEY_END, GLFW_MOD_CONTROL | GLFW_MOD_SHIFT, false);
        Assert.Equal(view.Text[2..], view.SelectedText);
        view.OnKeyDown(GLFW_KEY_HOME, GLFW_MOD_CONTROL, false);
        Assert.Equal(0, view.SelectionStart); Assert.Equal(0, view.SelectionLength);
    }

    [Fact]
    public void OverlongWordKeepsAGraphemeTogetherAcrossAStyleBoundary()
    {
        var layout = MarkdownLayout.Build(MarkdownContent.Parse("aa**\u0301**aaaa"), 10, 16,
            (s, f) => new(new System.Globalization.StringInfo(s).LengthInTextElements * 8, 12, 4));
        var accent = layout.Drawings.Single(d => d.Text == "\u0301");
        var preceding = layout.Drawings.Last(d => d.Kind == MarkdownDrawingKind.Text && d.Bounds.Y <= accent.Bounds.Y && d.Text.Contains('a'));
        Assert.Equal(preceding.Bounds.Y, accent.Bounds.Y);
    }

    [Fact]
    public void SelectAllCopiesRenderedTextAcrossStylesBlocksAndCodeWhitespace()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(800, 600, "Rendered selection copy", Theme.Light, RenderBackend.SW);
            var view = new MarkdownView("# Heading\n\nA **bold** [link](action:one) &amp; `code`.\n\n- [x] done\n- next\n\n```\n\tvalue\n\n  end\n```") { Bounds = new(20, 20, 650, 450) };
            window.AddChild(view); WindowRegressionOwner.Flush(window);
            view.OnKeyDown(GLFW_KEY_A, GLFW_MOD_CONTROL, false);
            view.OnKeyDown(GLFW_KEY_C, GLFW_MOD_CONTROL, false);
            Assert.Equal("Heading\n\nA bold link & code.\n\ndone\nnext\n\n\tvalue\n\n  end", Glfw.Glfw.glfwGetClipboardString(null));
        });
    }

    [Fact]
    public void DraggingStyledLinkCopiesOnlyRangeAndDoesNotActivateLink()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(800, 600, "Rendered range drag", Theme.Light, RenderBackend.SW);
            var view = new MarkdownView("before [**bold** link](action:one) after") { Bounds = new(20, 20, 650, 450) };
            window.AddChild(view); WindowRegressionOwner.Flush(window);
            int clicks = 0; view.LinkClicked += _ => clicks++;
            var bold = Layout(view).Drawings.Single(d => d.Text == "bold");
            float x = 32 + bold.Bounds.X, y = 32 + bold.Bounds.Y + bold.Bounds.H / 2;
            Glfw.Glfw.glfwSetClipboardString(null, "sentinel");
            view.OnMouseDown(GLFW_MOUSE_BUTTON_LEFT, x, y);
            view.OnMouseMove(x + bold.Bounds.W, y);
            view.OnMouseUp(GLFW_MOUSE_BUTTON_LEFT, x + bold.Bounds.W, y);
            view.OnKeyDown(GLFW_KEY_C, GLFW_MOD_CONTROL, false);
            Assert.Equal("bold", Glfw.Glfw.glfwGetClipboardString(null));
            Assert.Equal(0, clicks);
        });
    }

    [Fact]
    public void NativeHighlightMatchesMeasuredPartialRunAndClipsAtBothViewportEdges()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(800, 600, "Range highlight pixels", Theme.Light, RenderBackend.SW);
            var view = new MarkdownView("AVATAR words\n\n```\n" + new string('W', 100) + "\n```") { Bounds = new(20, 20, 290, 150), FontSize = 20 };
            window.AddChild(view); WindowRegressionOwner.Flush(window);
            var run = Layout(view).Drawings.First(d => d.Text == "AVATAR words");
            var paint = ThorVG.Text.Gen();
            try
            {
                paint.SetFont("default"); paint.SetFontSize(20); paint.SetText("M"); paint.GetMetrics(out var metrics);
                paint.SetFontSize(400 / (metrics.ascent - metrics.descent));
                float Width(string value) { paint.SetText(value); paint.GetTextSize(out float w, out _); return w; }
                float left = Width("AV"), right = Width("AVATAR");
                Assert.Equal(Width("AVATAR words"), run.Bounds.W, 3);
                view.OnBlur();
                var canvas = (Canvas)typeof(Window).GetField("_canvas", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
                Contracts.Draw(canvas, true); var before = WindowRuntimeTests.Buffer(window).ToArray();
                view.Select(2, 4); Contracts.Draw(canvas, true); var after = WindowRuntimeTests.Buffer(window);
                int y = (int)(32 + run.Bounds.Bottom - 2);
                int[] changed = Enumerable.Range(33, 250).Where(x => before[y * 800 + x] != after[y * 800 + x]).ToArray();
                Assert.NotEmpty(changed);
                Assert.InRange(changed.First(), (int)(32 + left) - 1, (int)Math.Ceiling(32 + left) + 1);
                Assert.InRange(changed.Last(), (int)(32 + right) - 2, (int)Math.Ceiling(32 + right));
                view.SelectAll(); view.ScrollTo(40, 10); Contracts.Draw(canvas, true);
                var selected = WindowRuntimeTests.Buffer(window).ToArray();
                view.ClearSelection(); view.OnBlur(); Contracts.Draw(canvas, true); var cleared = WindowRuntimeTests.Buffer(window);
                foreach (int row in Enumerable.Range(33, 120))
                    foreach (int x in new[] { 25, 29, 292, 297 })
                        Assert.Equal(selected[row * 800 + x], cleared[row * 800 + x]);
            }
            finally { paint.Unref(); }
        });
    }

    [Fact]
    public void PointerRangeSpansBlocksAndKeyboardLineSelectionUsesVisualLines()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(800, 600, "Block range", Theme.Light, RenderBackend.SW);
            var view = new MarkdownView("first **bold**\n\nsecond paragraph wraps onto several lines when narrow.") { Bounds = new(20, 20, 200, 350) };
            window.AddChild(view); WindowRegressionOwner.Flush(window);
            var bold = Layout(view).Drawings.Single(d => d.Text == "bold");
            var second = Layout(view).Drawings.First(d => d.Text.StartsWith("second"));
            float x = 32 + bold.Bounds.X, y = 32 + bold.Bounds.Y + bold.Bounds.H / 2;
            view.OnMouseDown(GLFW_MOUSE_BUTTON_LEFT, x, y);
            view.OnMouseMove(32 + second.Bounds.X, 32 + second.Bounds.Y + second.Bounds.H / 2);
            view.OnMouseUp(GLFW_MOUSE_BUTTON_LEFT, 32 + second.Bounds.X, 32 + second.Bounds.Y + second.Bounds.H / 2);
            Assert.Equal("bold\n\n", view.SelectedText);
            view.OnKeyDown(GLFW_KEY_END, GLFW_MOD_SHIFT, false);
            Assert.StartsWith("bold\n\nsecond", view.SelectedText);
            int end = view.SelectionStart + view.SelectionLength;
            view.OnKeyDown(GLFW_KEY_HOME, 0, false);
            Assert.Equal(view.Text.IndexOf("second", StringComparison.Ordinal), view.SelectionStart);
            view.OnKeyDown(GLFW_KEY_END, GLFW_MOD_SHIFT, false);
            Assert.Equal(end - view.SelectionStart, view.SelectionLength);
            view.OnKeyDown(GLFW_KEY_DOWN, GLFW_MOD_SHIFT, false); Assert.True(view.SelectionLength > end - view.SelectionStart);
            view.Select(0, 0); view.OnKeyDown(GLFW_KEY_END, 0, false);
            view.OnKeyDown(GLFW_KEY_DOWN, 0, false); view.OnKeyDown(GLFW_KEY_HOME, 0, false);
            // The first line is shorter than the second: moving down retains
            // the right edge, and Home stays on that second visual line.
            Assert.Equal(view.Text.IndexOf("second", StringComparison.Ordinal), view.SelectionStart);
        });
    }

    [Fact]
    public void CodeTabsCopyAsTabsAndScrolledTableRangeCopiesCellSeparators()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(800, 600, "Code and table ranges", Theme.Light, RenderBackend.SW);
            var view = new MarkdownView("```\n\tx\n```\n\n| A | B | C |\n| --- | --- | --- |\n| a | b | c |") { Bounds = new(20, 20, 180, 400) };
            window.AddChild(view); WindowRegressionOwner.Flush(window);
            var code = Layout(view).Drawings.Single(d => d.Text == "    x");
            float x = 32 + code.Bounds.X, y = 32 + code.Bounds.Y + code.Bounds.H / 2;
            view.OnMouseDown(GLFW_MOUSE_BUTTON_LEFT, x, y); view.OnMouseMove(x + code.Bounds.W * .8f, y);
            view.OnMouseUp(GLFW_MOUSE_BUTTON_LEFT, x + code.Bounds.W * .8f, y);
            Assert.Equal("\t", view.SelectedText);
            view.ScrollTo(90, 0);
            var b = Layout(view).Drawings.Single(d => d.Text == "B");
            var c = Layout(view).Drawings.Single(d => d.Text == "C");
            x = 32 + b.Bounds.X - view.ScrollOffset.X; y = 32 + b.Bounds.Y + b.Bounds.H / 2;
            float end = 32 + c.Bounds.Right - view.ScrollOffset.X;
            view.OnMouseDown(GLFW_MOUSE_BUTTON_LEFT, x, y); view.OnMouseMove(end, y); view.OnMouseUp(GLFW_MOUSE_BUTTON_LEFT, end, y);
            Assert.Equal("B\tC", view.SelectedText);
        });
    }

    [Fact]
    public void RelayoutImagesAndThemeKeepSelectionButReplacementAndDetachClearIt()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(800, 600, "Selection lifetime", Theme.Light, RenderBackend.SW);
            var view = new MarkdownView("a **bold** paragraph ![badge](asset:badge)\n\n```\n\twide code\n```") { Bounds = new(20, 20, 500, 350) };
            window.AddChild(view); WindowRegressionOwner.Flush(window);
            view.Select(2, 4); string text = view.Text;
            var parsed = typeof(MarkdownView).GetField("_content", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view);
            var first = Layout(view); view.ScrollTo(20, 10); Assert.Same(first, Layout(view));
            view.Bounds = new(20, 20, 180, 250); view.FontSize = 22; window.Theme = Theme.Dark;
            view.ImageResolver = _ => Marius.Winter.Scenarios.MarkdownScenarioData.ResolveImage("asset:winter-badge"); view.ReloadImages();
            Assert.Contains(Layout(view).Drawings, d => d.Kind == MarkdownDrawingKind.Image);
            Assert.Same(parsed, typeof(MarkdownView).GetField("_content", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view));
            Assert.Equal(text, view.Text); Assert.Equal("bold", view.SelectedText); Assert.Equal(2, view.SelectionStart);
            view.Markdown = view.Markdown; Assert.Equal("bold", view.SelectedText);
            view.Markdown = "replacement"; Assert.Equal("", view.SelectedText); Assert.Equal(0, view.SelectionLength);
            Glfw.Glfw.glfwSetClipboardString(null, "sentinel"); view.OnKeyDown(GLFW_KEY_C, GLFW_MOD_CONTROL, false);
            Assert.Equal("sentinel", Glfw.Glfw.glfwGetClipboardString(null));
            view.SelectAll(); window.RemoveChild(view); Assert.Equal("", view.SelectedText);
            window.AddChild(view); Assert.Equal(0, view.SelectionLength);
            view.SelectAll(); view.Dispose(); Assert.Equal("", view.SelectedText);
        });
    }

    [Fact]
    public void DragAutoScrollsBothAxesWithoutRelayoutAndStopsOnBlurAndReplacement()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(800, 600, "Range autoscroll", Theme.Light, RenderBackend.SW);
            var view = new MarkdownView("```\n\t" + new string('W', 150) + "\n```\n\n" + string.Join("\n\n", Enumerable.Repeat("paragraph", 40))) { Bounds = new(20, 20, 220, 170) };
            window.AddChild(view); WindowRegressionOwner.Flush(window); var layout = Layout(view);
            view.OnMouseDown(GLFW_MOUSE_BUTTON_LEFT, 43, 45);
            view.OnMouseMove(300, 250);
            for (int i = 0; i < 5; i++) window.Animator.Tick(.02f);
            Assert.True(view.ScrollOffset.X > 0); Assert.True(view.ScrollOffset.Y > 0); Assert.True(view.SelectionLength > 0);
            Assert.Same(layout, Layout(view));
            view.OnBlur(); Assert.False(window.Animator.HasActiveAnimations);
            var scroll = view.ScrollOffset; window.Animator.Tick(.1f); Assert.Equal(scroll, view.ScrollOffset);
            view.ScrollTo(0, 0); view.OnMouseDown(GLFW_MOUSE_BUTTON_LEFT, 43, 45); view.OnMouseMove(300, 250);
            view.Markdown = "new"; Assert.False(window.Animator.HasActiveAnimations); Assert.Equal(0, view.SelectionLength);
            view.OnMouseUp(GLFW_MOUSE_BUTTON_LEFT, 300, 250); Assert.Equal("", view.SelectedText);
        });
    }

    [Fact]
    public void ContextMenuCopiesRangeAndSourceAsSeparateActionsAndAltKeysNavigateLinks()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(800, 600, "Selection and link commands", Theme.Light, RenderBackend.SW);
            const string source = "[first](action:first) **bold** [last](action:last)";
            var view = new MarkdownView(source) { Bounds = new(20, 20, 500, 300) };
            window.AddChild(view); WindowRegressionOwner.Flush(window);
            string? clicked = null; view.LinkClicked += url => clicked = url;
            view.Select(6, 4); var menu = new Menu(); view.OnBuildContextMenu(menu);
            menu.Entries.Single(e => e.Label == "Copy").Action!(); Assert.Equal("bold", Glfw.Glfw.glfwGetClipboardString(null));
            menu.Entries.Single(e => e.Label == "Copy Markdown source").Action!(); Assert.Equal(source, Glfw.Glfw.glfwGetClipboardString(null));
            Assert.Equal("bold", view.SelectedText);
            view.OnKeyDown(GLFW_KEY_END, GLFW_MOD_ALT, false); view.OnKeyDown(GLFW_KEY_ENTER, 0, false); Assert.Equal("action:last", clicked);
            view.OnKeyDown(GLFW_KEY_HOME, GLFW_MOD_ALT, false); view.OnKeyDown(GLFW_KEY_SPACE, 0, false); Assert.Equal("action:first", clicked);
            view.OnKeyDown(GLFW_KEY_RIGHT, GLFW_MOD_SHIFT, false); clicked = null;
            view.OnKeyDown(GLFW_KEY_ENTER, 0, false); Assert.Null(clicked);
        });
    }

    [Fact]
    public void ShiftClickExtendsSelectionWithoutActivatingTheLink()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(800, 600, "Shift click selection", Theme.Light, RenderBackend.SW);
            var view = new MarkdownView("before [link](action:link) after") { Bounds = new(20, 20, 500, 250) };
            window.AddChild(view); WindowRegressionOwner.Flush(window);
            int clicks = 0; view.LinkClicked += _ => clicks++;
            view.Select(0, 0);
            var link = Layout(view).Drawings.Single(d => d.Span?.Link != null);
            float x = 32 + link.Bounds.Right - 1, y = 32 + link.Bounds.Y + link.Bounds.H / 2;
            // The same GLFW key state queried by native pointer interaction.
            window.GlfwWindow!.Keys[GLFW_KEY_LEFT_SHIFT] = GLFW_PRESS;
            try { view.OnMouseDown(GLFW_MOUSE_BUTTON_LEFT, x, y); view.OnMouseUp(GLFW_MOUSE_BUTTON_LEFT, x, y); }
            finally { window.GlfwWindow.Keys[GLFW_KEY_LEFT_SHIFT] = GLFW_RELEASE; }
            Assert.Equal("before link", view.SelectedText); Assert.Equal(0, clicks);
            view.OnMouseDown(GLFW_MOUSE_BUTTON_LEFT, x, y); view.OnMouseUp(GLFW_MOUSE_BUTTON_LEFT, x, y);
            Assert.Equal(1, clicks);
            view.OnKeyDown(GLFW_KEY_HOME, GLFW_MOD_ALT, false); view.OnKeyDown(GLFW_KEY_ENTER, 0, false);
            Assert.Equal(2, clicks);
        });
    }
}
