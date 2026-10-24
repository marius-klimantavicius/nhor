using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ThorVG;
using Xunit;
using static Glfw.GLFW;

namespace Marius.Winter.Tests;

public class RichSelectionGeometryTests
{
    private const int Width = 520, Height = 280;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    // The oracle is rasterized glyph ink versus rasterized selection/caret,
    // independent of the control's private line-height or baseline calculation.
    [Theory]
    [InlineData(12, "default", false)]
    [InlineData(16, "default", false)]
    [InlineData(24, "default", false)]
    [InlineData(32, "default", false)]
    [InlineData(16, "default", true)]
    [InlineData(24, "monospace", true)]
    public void SelectionSurroundsInkWithBalancedVerticalRoom(int size, string font, bool rich)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = CreateWindow();
            var editor = Attach(window, "Hgy Hgy", size, font);
            if (rich) editor.SetFormat(4, 3, TextFormat.Bold | TextFormat.Italic);
            var ink = Extent(Render(window), IsInk);
            editor.OnFocus(); Key(editor, GLFW_KEY_A, GLFW_MOD_CONTROL);
            var selection = Extent(Render(window), IsSelection);
            AssertBalanced(ink, selection, size);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MultilineSelectionEnclosesAccentsAndDescendersWithoutSelectingNextLine(bool gl)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = CreateWindow(gl);
            var editor = Attach(window, "ÁÉgy e\u0301\nHgy second line", 24);
            editor.SetFormat(1, 3, TextFormat.Bold | TextFormat.Italic);
            var before = Render(window, gl);
            // Separate the two lines using rows that actually contain glyph ink.
            var rows = InkBands(before);
            Assert.Equal(2, rows.Count);
            var firstInk = Extent(before, IsInk, rows[0].Top, rows[0].Bottom + 1);
            editor.OnFocus(); Key(editor, GLFW_KEY_HOME, GLFW_MOD_CONTROL);
            Key(editor, GLFW_KEY_END, GLFW_MOD_SHIFT);
            var after = Render(window, gl);
            var selection = Extent(after, IsSelection);
            AssertBalanced(firstInk, selection, 24);
            Assert.True(selection.Bottom < rows[1].Top, "Selecting one line must leave the next line's ink outside its highlight.");
            for (int y = rows[1].Top; y <= rows[1].Bottom; y++)
                for (int x = 28; x < 412; x++)
                    Assert.Equal(before[y * Width + x], after[y * Width + x]);
        });
    }

    [Theory]
    [InlineData(12)]
    [InlineData(24)]
    public void CaretSpansAccentsAndDescenders(int size)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = CreateWindow();
            var editor = Attach(window, "ÁHgy", size);
            var ink = Extent(Render(window), IsInk);
            editor.OnFocus(); Key(editor, GLFW_KEY_END);
            var caret = Extent(Render(window), IsCaret);
            Assert.True(caret.Top <= ink.Top && caret.Bottom >= ink.Bottom,
                $"Caret {caret} must cover the full glyph ink {ink}, including accents and descenders.");
        });
    }

    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    public void RegularAndBoldRunsShareTheRenderedBaseline(int size)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = CreateWindow();
            var editor = Attach(window, "HH HH", size, "monospace");
            editor.SetColor(0, 2, new Color4(1, 0, 0));
            editor.SetColor(3, 2, new Color4(0, 0, 1));
            editor.SetFormat(3, 2, TextFormat.Bold);
            var pixels = Render(window);
            var regular = Extent(pixels, c => Red(c) > 150 && Green(c) < 100 && Blue(c) < 100);
            var bold = Extent(pixels, c => Blue(c) > 150 && Green(c) < 100 && Red(c) < 100);
            // H has no descender. Its lowest ink row independently exposes the
            // baseline; different hinting can round the edge by one raster row.
            Assert.InRange(Math.Abs(regular.Bottom - bold.Bottom), 0, 1);
        });
    }

    [Fact]
    public void ScrolledAndNarrowEditorKeepsInkSelectionHitsAndGraphemeIndicesTogether()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = CreateWindow();
            const string line = "Ágy e\u0301 😀 long horizontal text";
            var editor = Attach(window, string.Join("\n", Enumerable.Repeat(line, 8)), 16);
            editor.Arrange(new RectF(20, 20, 150, 100));
            editor.OnScroll(0, -1); // ordinary vertical scrolling, no internal offsets
            var before = Render(window);
            var bands = InkBands(before);
            Assert.True(bands.Count >= 2);
            // Click the left margin alongside a fully visible line's ink.
            int y = bands[1].Top + (bands[1].Bottom - bands[1].Top) / 2;
            editor.OnMouseDown(0, 28, y); editor.OnMouseUp(0, 28, y);
            int start = Caret(editor);
            Assert.Equal(0, start % (line.Length + 1));
            editor.OnFocus(); Key(editor, GLFW_KEY_END, GLFW_MOD_SHIFT);
            var selected = Render(window);
            var highlight = Extent(selected, IsSelection);
            Assert.True(highlight.Top <= bands[1].Top && highlight.Bottom >= bands[1].Bottom,
                "Hit-testing at rendered glyph ink must select that same visual line after scrolling.");
            AssertNoSelectionOutsideClip(selected, 28, 27, 162, 112);
            Key(editor, GLFW_KEY_HOME);
            for (int i = 0; i < 5; i++) Key(editor, GLFW_KEY_RIGHT);
            Assert.Equal(start + "Ágy e\u0301".Length, Caret(editor));
            Key(editor, GLFW_KEY_RIGHT, GLFW_MOD_SHIFT); // space
            Key(editor, GLFW_KEY_RIGHT, GLFW_MOD_SHIFT); // whole supplementary scalar
            editor.OnTextInput("Ω");
            Assert.Contains("Ágy e\u0301Ω long", editor.Text);
            // RichTextBox has no soft wrapping API: narrowing uses horizontal
            // scrolling. It must not invent extra logical rows or split UTF-16.
            Assert.Equal(8, editor.Text.Split('\n').Length);
            AssertNoSelectionOutsideClip(Render(window), 28, 27, 162, 112);
        });
    }

    [Fact]
    public void FontStyleChangesRetainBalancedSelectionAndFullHeightCaret()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = CreateWindow();
            var editor = Attach(window, "Hgy Hgy", 12);
            editor.SetFormat(4, 3, TextFormat.Bold);
            foreach (var (size, font) in new[] { (24, "monospace"), (16, "default") })
            {
                editor.OnBlur();
                editor.Style.FontSize = size; editor.Style.FontName = font;
                var ink = Extent(Render(window), IsInk);
                editor.OnFocus(); Key(editor, GLFW_KEY_A, GLFW_MOD_CONTROL);
                AssertBalanced(ink, Extent(Render(window), IsSelection), size);
                Key(editor, GLFW_KEY_END);
                var caret = Extent(Render(window), IsCaret);
                Assert.True(caret.Top <= ink.Top && caret.Bottom >= ink.Bottom);
            }
        });
    }

    [Fact]
    public void DecorationsFollowTheRenderedBaselineAndCapBody()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = CreateWindow();
            var editor = Attach(window, "HH HH\nHH HH", 24);
            var before = Render(window);
            var bands = InkBands(before);
            Assert.Equal(2, bands.Count);
            editor.SetFormat(0, 5, TextFormat.Underline);
            var underline = ChangedInk(before, Render(window));
            Assert.True(underline.Top > bands[0].Bottom && underline.Bottom < bands[1].Top);
            Assert.InRange(underline.Top - bands[0].Bottom, 1, 7);
            editor.SetFormat(0, 5, TextFormat.Strikethrough);
            var strike = ChangedInk(before, Render(window));
            Assert.InRange((strike.Top + strike.Bottom) / 2f, bands[0].Top + 3, bands[0].Bottom - 3);
        });
    }

    private static Box ChangedInk(uint[] before, uint[] after)
    {
        var mask = after.Select((c, i) => c != before[i] && IsInk(c) ? 0xff000000u : 0xffffffffu).ToArray();
        return Extent(mask, IsInk);
    }

    private static Window CreateWindow(bool gl = false)
    {
        var theme = Theme.Light;
        theme.TextColor = new Color4(0, 0, 0);
        theme.SelectionColor = new Color4(0, .7f, 1);
        theme.CursorColor = new Color4(1, 0, 1);
        return new Window(Width, Height, "Rich selection pixel contract", theme, gl ? RenderBackend.GL : RenderBackend.SW);
    }

    private static RichTextBox Attach(Window window, string text, int size, string font = "default")
    {
        var editor = new RichTextBox(text) { Style = new Style { FontSize = size, FontName = font,
            Background = new Color4(1, 1, 1), Border = Color4.Transparent, BorderFocused = Color4.Transparent } };
        window.AddChild(editor); editor.Arrange(new RectF(20, 20, 400, 220));
        return editor;
    }

    private static void Key(RichTextBox editor, int key, int mods = 0) => editor.OnKeyDown(key, mods, false);
    private static int Caret(RichTextBox editor) => (int)typeof(RichTextBox).GetField("_cursorPos", Private)!.GetValue(editor)!;
    private static int Red(uint c) => (int)(c >> 16) & 255;
    private static int Green(uint c) => (int)(c >> 8) & 255;
    private static int Blue(uint c) => (int)c & 255;
    private static bool IsInk(uint c) => Red(c) < 100 && Green(c) < 100 && Blue(c) < 100;
    private static bool IsSelection(uint c) => Red(c) < 80 && Green(c) > 100 && Blue(c) > 200;
    private static bool IsCaret(uint c) => Red(c) > 180 && Green(c) < 80 && Blue(c) > 180;

    private readonly record struct Box(int Top, int Bottom);
    private static Box Extent(uint[] pixels, Func<uint, bool> predicate, int top = 28, int bottom = 232)
    {
        var rows = Enumerable.Range(top, bottom - top)
            .Where(y => Enumerable.Range(28, 384).Any(x => predicate(pixels[y * Width + x]))).ToArray();
        Assert.NotEmpty(rows);
        return new Box(rows[0], rows[^1]);
    }

    private static List<Box> InkBands(uint[] pixels)
    {
        var result = new List<Box>();
        for (int y = 28; y < 112; y++)
        {
            if (!Enumerable.Range(28, 130).Any(x => IsInk(pixels[y * Width + x]))) continue;
            // An accent can have a blank raster row between its mark and body.
            // Keep that ink in one band; actual line leading is substantially wider.
            if (result.Count == 0 || result[^1].Bottom < y - 3) result.Add(new Box(y, y));
            else result[^1] = result[^1] with { Bottom = y };
        }
        return result;
    }

    private static void AssertBalanced(Box ink, Box selection, int size)
    {
        int above = ink.Top - selection.Top, below = selection.Bottom - ink.Bottom;
        Assert.True(above >= Math.Max(1, size / 12) && below >= 1,
            $"Selection {selection} must surround glyph ink {ink} above and below; room={above}/{below}.");
        Assert.True(Math.Abs(above - below) <= size * .3f + 1,
            $"Selection must not place almost all vertical room below the ink; room={above}/{below}, font={size}.");
    }

    private static void AssertNoSelectionOutsideClip(uint[] pixels, int left, int top, int right, int bottom)
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (x < left || x >= right || y < top || y >= bottom) Assert.False(IsSelection(pixels[y * Width + x]));
    }

    private static unsafe uint[] Render(Window window, bool gl = false)
    {
        Contracts.Draw((Canvas)typeof(Window).GetField("_canvas", Private)!.GetValue(window)!, true);
        if (!gl) return (uint[])WindowRuntimeTests.Buffer(window).Clone();
        // Read the defined back target before any presentation, as in GlRuntimeTests.
        var finish = (delegate* unmanaged[Cdecl]<void>)Glfw.Glfw.glfwGetProcAddress("glFinish");
        var bind = (delegate* unmanaged[Cdecl]<uint, uint, void>)Glfw.Glfw.glfwGetProcAddress("glBindFramebuffer");
        var buffer = (delegate* unmanaged[Cdecl]<uint, void>)Glfw.Glfw.glfwGetProcAddress("glReadBuffer");
        var read = (delegate* unmanaged[Cdecl]<int, int, int, int, uint, uint, void*, void>)Glfw.Glfw.glfwGetProcAddress("glReadPixels");
        var error = (delegate* unmanaged[Cdecl]<uint>)Glfw.Glfw.glfwGetProcAddress("glGetError");
        Assert.True(finish != null && bind != null && buffer != null && read != null && error != null);
        finish(); bind(0x8CA8, 0); buffer(0x0405);
        var raw = new uint[Width * Height];
        fixed (uint* p = raw) read(0, 0, Width, Height, 0x1908, 0x1401, p);
        Assert.Equal(0u, error());
        var result = new uint[raw.Length];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                uint c = raw[(Height - 1 - y) * Width + x];
                result[y * Width + x] = (c & 0xff00ff00) | ((c & 255) << 16) | ((c >> 16) & 255);
            }
        return result;
    }
}
