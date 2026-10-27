using System;
using System.Linq;
using ThorVG;
using Xunit;
using static Glfw.GLFW;

namespace Marius.Winter.Tests;

public partial class ScrollbarInputTests
{
    private static uint[] Frame(Window window)
    {
        Contracts.Draw((Canvas)Field(window, "_canvas"), true);
        return WindowRuntimeTests.Buffer(window).ToArray();
    }

    [Fact]
    public void ScrollPanelClipsChildrenFromGuttersAcrossReorderRemovalAndReattachment() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(900, 700, "Scrollbar child clip", Theme.Light, RenderBackend.SW);
        var panel = (ScrollPanel)Attach(window, "ScrollPanel");
        foreach (var child in panel.Children.ToArray()) child.Dispose();
        var red = new Panel { Background = new Color4(1, 0, 0, 1) };
        panel.AddChild(red); WindowRegressionOwner.Flush(window);
        var before = Frame(window); red.Background = new Color4(0, 1, 0, 1); var after = Frame(window);
        var b = Absolute(panel);
        int Pixel(int x, int y) => ((int)b.Y + y) * 900 + (int)b.X + x;
        Assert.NotEqual(before[Pixel(40, 40)], after[Pixel(40, 40)]);
        for (int y = 0; y < panel.Bounds.H; y++) for (int x = 0; x < panel.Bounds.W; x++)
            if (x >= Viewport(panel, true) || y >= Viewport(panel, false)) Assert.Equal(before[Pixel(x, y)], after[Pixel(x, y)]);

        var blue = new Panel { Background = new Color4(0, 0, 1, 1) }; panel.AddChild(blue); Frame(window);
        panel.ReorderChildren([blue, red]); var reordered = Frame(window);
        Assert.Equal(after[Pixel(40, 40)], reordered[Pixel(40, 40)]);
        panel.RemoveChild(red); panel.InsertChild(red, 1); Assert.Equal(reordered, Frame(window));
        panel.ClearChildren(); Assert.NotEqual(reordered[Pixel(40, 40)], Frame(window)[Pixel(40, 40)]);
        red.Dispose(); blue.Dispose();
    });

    [Fact]
    public void TreeRowsCannotPaintThroughTranslucentScrollbarTracks() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(900, 700, "Tree scrollbar clip", Theme.Light, RenderBackend.SW);
        var tree = (TreeView)Attach(window, "TreeView"); var pixels = Frame(window); var b = Absolute(tree);
        // Below the initial minimum thumb, every row must leave the same track/margin pixels.
        for (int x = (int)tree.Bounds.W - 10; x < tree.Bounds.W; x++)
        {
            uint expected = pixels[((int)b.Y + 60) * 900 + (int)b.X + x];
            for (int y = 61; y < 180; y++) Assert.Equal(expected, pixels[((int)b.Y + y) * 900 + (int)b.X + x]);
        }
    });

    [Theory]
    [InlineData("Markdown")] [InlineData("TextArea")] [InlineData("RichTextBox")] [InlineData("LogView")]
    public void OrdinaryScrollbarPressPreservesTextSelection(string kind) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(900, 700, "Scrollbar selection", Theme.Light, RenderBackend.SW);
        var e = Attach(window, kind); window.SetFocus(e); e.OnKeyDown(GLFW_KEY_A, GLFW_MOD_CONTROL, false);
        (int Start, int Length) Selection() => e switch
        {
            MarkdownView md => (md.SelectionStart, md.SelectionLength), RichTextBox rich => (rich.SelectionStart, rich.SelectionLength),
            LogView log => (log.SelectionStart, log.SelectionLength),
            _ => ((int)Field(e, "_selectionStart"), (int)Field(e, "_cursorPos") - (int)Field(e, "_selectionStart"))
        };
        var before = Selection(); Assert.True(before.Length > 0);
        foreach (bool axis in new[] { false, true })
        {
            var track = PaintBounds(e, axis, false);
            At(window, e, track.X + track.W - 1, track.Y + track.H - 1); Button(window, true);
            Move(window, 1500, 1100); Button(window, false);
            Assert.Same(e, window.FocusedElement); Assert.Equal(before, Selection());
        }
    });
}
