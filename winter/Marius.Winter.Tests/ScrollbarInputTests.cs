using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using ThorVG;
using Xunit;
using static Glfw.GLFW;

namespace Marius.Winter.Tests;

public partial class ScrollbarInputTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string Text = string.Join('\n', Enumerable.Range(0, 45).Select(i => $"line {i} " + new string('x', 150)));
    private static object Field(object target, string name)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
            if (type.GetField(name, Private) is { } field) return field.GetValue(target)!;
        throw new MissingFieldException(name);
    }
    private static float Number(object target, string name) => (float)Field(target, name);
    private static float Call(object target, string name) => (float)target.GetType().GetMethod(name, Private)!.Invoke(target, null)!;
    private static RectF Absolute(Element element)
    { element.WindowToLocal(0, 0, out float x, out float y); return new(-x, -y, element.Bounds.W, element.Bounds.H); }
    private static void Move(Window window, float x, float y) => window.GlfwWindow!.Callbacks.CursorPos!(window.GlfwWindow, x, y);
    private static void Button(Window window, bool down) => window.GlfwWindow!.Callbacks.MouseButton!(window.GlfwWindow, 0, down ? GLFW_PRESS : GLFW_RELEASE, 0);
    private static void At(Window window, Element element, float x, float y)
    { var b = Absolute(element); Move(window, b.X + x, b.Y + y); }
    private static void CursorIs(Window window, CursorType cursor)
    { var cursors = (Array)Field(window, "_cursors"); Assert.Same(cursors.GetValue((int)cursor), window.GlfwWindow!.Cursor); }

    private sealed class LargeLayout : ILayout
    {
        public Vector2 Measure(Element parent, float width, float height) => new(1500, 1600);
        public void Arrange(Element parent, RectF bounds) { foreach (var child in parent.Children) child.Arrange(new(bounds.X, bounds.Y, 1500, 1600)); }
    }
    private static Element Attach(Window window, string kind, bool blazor = false)
    {
        Element element = blazor ? Mount(window, kind) : kind switch
        {
            "Markdown" => new MarkdownView("[link](action:test)\n\n```\n" + Text + "\n```"),
            "TextArea" => new TextArea(Text), "RichTextBox" => new RichTextBox(Text),
            "LogView" => new LogView { FollowTail = false, Text = Text },
            "ScrollPanel" => new ScrollPanel { Layout = new LargeLayout() },
            _ => new TreeView()
        };
        if (element is TreeView tree) { tree.BeginUpdate(); for (int i = 0; i < 45; i++) tree.AddNode("row " + i + new string('x', 150)); tree.EndUpdate(); }
        if (element is ScrollPanel panel) panel.AddChild(new TextBox("interactive child"));
        var parent = blazor ? (Panel)element.Parent! : new Panel();
        parent.Bounds = new(43, 31, 650, 480); parent.Cursor = CursorType.Hand;
        element.Bounds = new(27, 21, 320, 210);
        if (!blazor) { parent.AddChild(element); window.AddChild(parent); }
        WindowRegressionOwner.Flush(window);
        return element;
    }
    private static string PaintName(Element e, bool horizontal, bool thumb) => e switch
    {
        MarkdownView => horizontal ? thumb ? "_hThumb" : "_hTrack" : thumb ? "_vThumb" : "_vTrack",
        TreeView => horizontal ? thumb ? "_scrollThumbH" : "_scrollTrackH" : thumb ? "_scrollThumb" : "_scrollTrack",
        LogView => horizontal ? thumb ? "_horizontalThumb" : "_horizontalTrack" : thumb ? "_scrollThumb" : "_scrollTrack",
        _ => horizontal ? thumb ? "_scrollThumbH" : "_scrollTrackH" : thumb ? "_scrollThumbV" : "_scrollTrackV"
    };
    private static RectF PaintBounds(Element e, bool horizontal, bool thumb)
    {
        ((Shape)Field(e, PaintName(e, horizontal, thumb))).Bounds(out float x, out float y, out float w, out float h);
        var a = Absolute(e); return new(x - a.X, y - a.Y, w, h);
    }
    private static float Viewport(Element e, bool horizontal) => e switch
    {
        MarkdownView => (horizontal ? e.Bounds.W : e.Bounds.H) - 34,
        LogView => Number(e, horizontal ? "_viewportW" : "_viewportH"),
        TreeView => (horizontal ? e.Bounds.W : e.Bounds.H) - 10,
        _ => Call(e, horizontal ? "ViewportW" : "ViewportH")
    };
    private static float Offset(Element e, bool horizontal) => e switch
    {
        MarkdownView m => horizontal ? m.ScrollOffset.X : m.ScrollOffset.Y,
        ScrollPanel => Call(e, horizontal ? "GetScrollOffsetH" : "GetScrollOffsetV"),
        TreeView => Call(e, horizontal ? "GetScrollOffsetH" : "GetScrollOffset"),
        _ => Number(e, horizontal ? "_scrollX" : "_scrollY")
    };
    private static float Total(Element e, bool horizontal) => e switch
    {
        MarkdownView m => horizontal ? m.ContentSize.X : m.ContentSize.Y,
        ScrollPanel or TreeView => Number(e, horizontal ? "_contentWidth" : "_contentHeight"),
        LogView => Number(e, horizontal ? "_totalContentWidth" : "_totalContentHeight"),
        _ => Number(e, horizontal ? "_maxLineWidth" : "_totalContentHeight")
    };

    [Theory]
    [MemberData(nameof(ScrollCases))]
    public void NativeTrackPagesOnceAndSameHeldPressDragsWithArrowAndOutsideRelease(string kind, bool horizontal, bool blazor) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(900, 700, "Shared scrollbar input", Theme.Light, RenderBackend.SW);
        var e = Attach(window, kind, blazor); var track = PaintBounds(e, horizontal, false);
        float page = Viewport(e, horizontal), max = Total(e, horizontal) - page;
        Assert.True(max > page * 2); Assert.True(track.W > 0 && track.H > 0);
        float px = horizontal ? track.Right - 1 : track.X + track.W / 2;
        float py = horizontal ? track.Y + track.H / 2 : track.Bottom - 1;
        // First entry must resolve a position-specific Arrow even under a Hand ancestor.
        At(window, e, px, py);
        var b = Absolute(e); Assert.Same(e, window.HitTest(b.X + px, b.Y + py));
        Button(window, true);
        Assert.Equal(page, Offset(e, horizontal), 2);
        window.Animator.Tick(.6f); Assert.Equal(page, Offset(e, horizontal), 2);
        At(window, e, px - (horizontal ? 7 : 0), py - (horizontal ? 0 : 7));
        Assert.True(Offset(e, horizontal) < page, "Same held track press must drag without a second click.");
        CursorIs(window, CursorType.Arrow);
        Move(window, 1500, 1100); Assert.Equal(max, Offset(e, horizontal), 2); CursorIs(window, CursorType.Arrow);
        Button(window, false); Move(window, -100, -100); Assert.Equal(max, Offset(e, horizontal), 2);
        Assert.Equal(0, Offset(e, !horizontal));
        track = PaintBounds(e, horizontal, false); var thumb = PaintBounds(e, horizontal, true);
        Assert.InRange(horizontal ? thumb.X : thumb.Y, horizontal ? track.X : track.Y, horizontal ? track.Right : track.Bottom);
        Assert.Equal(horizontal ? track.Right : track.Bottom, horizontal ? thumb.Right : thumb.Bottom, 2);
    });

    [Theory]
    [InlineData("Markdown", CursorType.Hand)] [InlineData("TextArea", CursorType.Hand)]
    [InlineData("RichTextBox", CursorType.Hand)] [InlineData("LogView", CursorType.Hand)]
    [InlineData("Markdown", CursorType.IBeam)] [InlineData("TextArea", CursorType.IBeam)]
    [InlineData("RichTextBox", CursorType.IBeam)] [InlineData("LogView", CursorType.IBeam)]
    public void CursorTransitionsOnFirstEntryTextToGutterAndCapturedOutside(string kind, CursorType ancestor) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(900, 700, "Scrollbar cursor routing", Theme.Light, RenderBackend.SW);
        var e = Attach(window, kind); var track = PaintBounds(e, false, false);
        e.Parent!.Cursor = ancestor;
        At(window, e, track.X + track.W / 2, track.Y + 2); CursorIs(window, CursorType.Arrow);
        At(window, e, 30, 80); CursorIs(window, CursorType.IBeam);
        if (e is MarkdownView) { At(window, e, 20, 20); CursorIs(window, CursorType.Hand); }
        At(window, e, track.X + track.W / 2, track.Y + 2); CursorIs(window, CursorType.Arrow);
        Button(window, true); Move(window, 10, 10); CursorIs(window, CursorType.Arrow); Button(window, false);
        At(window, e, 30, 80); CursorIs(window, CursorType.IBeam);
    });
}
