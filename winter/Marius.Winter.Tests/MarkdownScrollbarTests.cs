using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using ThorVG;
using Xunit;
using static Glfw.GLFW;

namespace Marius.Winter.Tests;

public class MarkdownScrollbarTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string Document = "[selected link](action:test)\n\n```\n" +
        string.Join('\n', Enumerable.Range(0, 40).Select(i => $"line {i}: " + new string('x', 120))) + "\n```";

    private static MarkdownView Attach(Window window)
    {
        var parent = new Panel { Bounds = new(51, 37, 600, 420) };
        var view = new MarkdownView(Document) { Bounds = new(23, 19, 320, 210) };
        parent.AddChild(view); window.AddChild(parent); WindowRegressionOwner.Flush(window);
        return view;
    }
    private static RectF Absolute(Element element)
    { element.WindowToLocal(0, 0, out float x, out float y); return new(-x, -y, element.Bounds.W, element.Bounds.H); }
    private static RectF Thumb(MarkdownView view, bool horizontal)
    {
        var shape = (Shape)typeof(MarkdownView).GetField(horizontal ? "_hThumb" : "_vThumb", Private)!.GetValue(view)!;
        shape.Bounds(out float x, out float y, out float w, out float h);
        var absolute = Absolute(view);
        return new(x - absolute.X, y - absolute.Y, w, h);
    }
    private static float Offset(MarkdownView view, bool horizontal) => horizontal ? view.ScrollOffset.X : view.ScrollOffset.Y;
    private static float Page(MarkdownView view, bool horizontal) => (horizontal ? view.Bounds.W : view.Bounds.H) - 34;
    private static float Maximum(MarkdownView view, bool horizontal) => (horizontal ? view.ContentSize.X : view.ContentSize.Y) - Page(view, horizontal);
    private static void Move(Window window, float x, float y)
    { var native = window.GlfwWindow!; native.Callbacks.CursorPos!(native, x, y); }
    private static void Press(Window window)
    { var native = window.GlfwWindow!; native.Callbacks.MouseButton!(native, GLFW_MOUSE_BUTTON_LEFT, GLFW_PRESS, 0); }
    private static void Release(Window window)
    { var native = window.GlfwWindow!; native.Callbacks.MouseButton!(native, GLFW_MOUSE_BUTTON_LEFT, GLFW_RELEASE, 0); }
    private static void AtTrack(Window window, MarkdownView view, bool horizontal, float position)
    {
        var bounds = Absolute(view);
        Move(window, bounds.X + (horizontal ? position : view.Bounds.W - 7), bounds.Y + (horizontal ? view.Bounds.H - 7 : position));
    }

    [Theory]
    [InlineData(false, -1)]
    [InlineData(false, 1)]
    [InlineData(true, -1)]
    [InlineData(true, 1)]
    public void TrackPressPagesOnceThenSameHeldPressDragsWithoutChangingSelection(bool horizontal, int direction) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(800, 600, "Markdown track input", Theme.Light, RenderBackend.SW);
        var view = Attach(window); int links = 0; view.LinkClicked += _ => links++;
        view.Select(0, 8); string selection = view.SelectedText;
        view.ScrollTo(Maximum(view, true) / 2, Maximum(view, false) / 2);
        var layout = typeof(MarkdownView).GetField("_layout", Private)!.GetValue(view);
        float before = Offset(view, horizontal), other = Offset(view, !horizontal), page = Page(view, horizontal);
        var thumb = Thumb(view, horizontal);
        float position = direction < 0 ? 13 : 12 + page - 1;
        Assert.True(direction < 0 ? position < (horizontal ? thumb.X : thumb.Y) : position > (horizontal ? thumb.Right : thumb.Bottom));
        AtTrack(window, view, horizontal, position); Assert.Same(view, window.HitTest(Absolute(view).X + (horizontal ? position : view.Bounds.W - 7), Absolute(view).Y + (horizontal ? view.Bounds.H - 7 : position)));
        Press(window);
        float expected = Math.Clamp(before + direction * page, 0, Maximum(view, horizontal));
        Assert.Equal(expected, Offset(view, horizontal), 3);
        // A stationary held track press pages once; that same press remains draggable.
        window.Animator.Tick(.6f); Assert.Equal(expected, Offset(view, horizontal), 3);
        Move(window, 1200, 900);
        Assert.Equal(Maximum(view, horizontal), Offset(view, horizontal), 3); Assert.Equal(other, Offset(view, !horizontal), 3);
        Release(window); Move(window, -100, -100);
        Assert.Equal(Maximum(view, horizontal), Offset(view, horizontal), 3);
        Assert.Equal(selection, view.SelectedText); Assert.Equal(0, view.SelectionStart); Assert.Equal(8, view.SelectionLength);
        Assert.Equal(0, links); Assert.False(window.Animator.HasActiveAnimations);
        Assert.Same(layout, typeof(MarkdownView).GetField("_layout", Private)!.GetValue(view));

        // Repeated independent clicks page toward, then clamp at, the endpoint.
        view.ScrollTo(horizontal ? expected : other, horizontal ? other : expected);
        for (int i = 0; i < 30 && Offset(view, horizontal) != (direction < 0 ? 0 : Maximum(view, horizontal)); i++)
        {
            float old = Offset(view, horizontal);
            AtTrack(window, view, horizontal, position); Press(window); Release(window);
            Assert.Equal(Math.Clamp(old + direction * page, 0, Maximum(view, horizontal)), Offset(view, horizontal), 3);
        }
        Assert.Equal(direction < 0 ? 0 : Maximum(view, horizontal), Offset(view, horizontal), 3);
        thumb = Thumb(view, horizontal);
        Assert.Equal(direction < 0 ? 12 : 12 + page, direction < 0 ? (horizontal ? thumb.X : thumb.Y) : (horizontal ? thumb.Right : thumb.Bottom), 3);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThumbKeepsGripReachesBothEndsAndReleasesOutsideWhileScrollInputsStayInSync(bool horizontal) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(800, 600, "Markdown thumb capture", Theme.Light, RenderBackend.SW);
        var view = Attach(window); view.Select(0, 8); view.ScrollTo(100, 100);
        var layout = typeof(MarkdownView).GetField("_layout", Private)!.GetValue(view);
        var thumb = Thumb(view, horizontal);
        float start = horizontal ? thumb.X : thumb.Y, length = horizontal ? thumb.W : thumb.H;
        float grip = start + length * .73f, before = Offset(view, horizontal);
        AtTrack(window, view, horizontal, grip); Press(window);
        Assert.Equal(before, Offset(view, horizontal));
        AtTrack(window, view, horizontal, grip + 8);
        Assert.Equal(start + 8, horizontal ? Thumb(view, true).X : Thumb(view, false).Y, 3);
        Assert.True(Offset(view, horizontal) > before);
        Move(window, 1200, 900); // Captured input beyond this control and its parent.
        Assert.Equal(Maximum(view, horizontal), Offset(view, horizontal), 3);
        Release(window); Move(window, 730, 510);
        Assert.Equal(Maximum(view, horizontal), Offset(view, horizontal), 3);
        thumb = Thumb(view, horizontal);
        Assert.Equal(12 + Page(view, horizontal), horizontal ? thumb.Right : thumb.Bottom, 3);

        AtTrack(window, view, horizontal, (horizontal ? thumb.Right : thumb.Bottom) - 1); Press(window);
        Move(window, -100, -100); Assert.Equal(0, Offset(view, horizontal)); Release(window);
        thumb = Thumb(view, horizontal); Assert.Equal(12, horizontal ? thumb.X : thumb.Y, 3);

        view.ScrollTo(100, 100); thumb = Thumb(view, horizontal);
        AtTrack(window, view, horizontal, 40);
        var native = window.GlfwWindow!;
        native.Callbacks.Scroll!(native, horizontal ? -1 : 0, horizontal ? 0 : -1);
        Assert.Equal(100 + view.FontSize * 3, Offset(view, horizontal), 3);
        Assert.True((horizontal ? Thumb(view, true).X : Thumb(view, false).Y) > (horizontal ? thumb.X : thumb.Y));
        view.ScrollTo(Maximum(view, true), Maximum(view, false));
        thumb = Thumb(view, horizontal); Assert.Equal(12 + Page(view, horizontal), horizontal ? thumb.Right : thumb.Bottom, 3);
        view.OnKeyDown(GLFW_KEY_HOME, GLFW_MOD_CONTROL, false);
        Assert.Equal(Vector2.Zero, view.ScrollOffset);
        view.OnKeyDown(GLFW_KEY_PAGE_DOWN, 0, false); Assert.Equal(Page(view, false) * .9f, view.ScrollOffset.Y, 3);
        Assert.Same(layout, typeof(MarkdownView).GetField("_layout", Private)!.GetValue(view));
        Assert.False(window.Animator.HasActiveAnimations);
    });

    [Fact]
    public void HeightOnlyGrowthClampsBeforePaintingAndNextWheelKeepsItsNormalStep() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(800, 600, "Markdown height-only resize", Theme.Light, RenderBackend.SW);
        var view = Attach(window); view.Select(0, 8); view.ScrollTo(80, float.MaxValue);
        string selection = view.SelectedText;
        var layout = typeof(MarkdownView).GetField("_layout", Private)!.GetValue(view);
        float oldMaximum = view.ScrollOffset.Y;
        var thumb = Thumb(view, false);
        AtTrack(window, view, false, thumb.Y + thumb.H / 2); Press(window);

        // Use the actual size-change path, retaining width so layout stays cached.
        view.Bounds = new(view.Bounds.X, view.Bounds.Y, view.Bounds.W, view.Bounds.H + 140);
        float maximum = Math.Max(0, view.ContentSize.Y - Page(view, false));
        Assert.Equal(oldMaximum - 140, maximum, 3);
        Assert.Equal(maximum, view.ScrollOffset.Y, 3);
        Assert.Equal(80, view.ScrollOffset.X);
        Assert.Same(layout, typeof(MarkdownView).GetField("_layout", Private)!.GetValue(view));
        var trackPaint = (Shape)typeof(MarkdownView).GetField("_vTrack", Private)!.GetValue(view)!;
        trackPaint.Bounds(out float x, out float y, out float w, out float h);
        view.WindowToLocal(x, y, out x, out y);
        var track = new RectF(x, y, w, h);
        thumb = Thumb(view, false);
        Assert.InRange(thumb.Y, track.Y, track.Bottom);
        Assert.InRange(thumb.Bottom, track.Y, track.Bottom);
        Assert.Equal(track.Bottom, thumb.Bottom, 3);

        // Height-only resize also cancels the captured thumb's old geometry.
        Move(window, 730, 510); Release(window); window.Animator.Tick(.1f);
        Assert.Equal(maximum, view.ScrollOffset.Y, 3);
        Assert.Equal(selection, view.SelectedText); Assert.Equal(8, view.SelectionLength);
        Assert.False(window.Animator.HasActiveAnimations);
        AtTrack(window, view, false, track.Y + 20);
        var native = window.GlfwWindow!;
        native.Callbacks.Scroll!(native, 0, 1);
        Assert.Equal(maximum - view.FontSize * 3, view.ScrollOffset.Y, 3);
        var afterWheel = Thumb(view, false);
        Assert.InRange(afterWheel.Y, track.Y, track.Bottom);
        Assert.True(afterWheel.Y < thumb.Y); Assert.True(afterWheel.Bottom < track.Bottom);
        Assert.Equal(80, view.ScrollOffset.X); Assert.Equal(selection, view.SelectedText);
        Assert.Same(layout, typeof(MarkdownView).GetField("_layout", Private)!.GetValue(view));
    });

    [Fact]
    public void ChangedScrollbarGeometryCancelsHeldThumbAndTinyTracksContainTheirThumbs() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(800, 600, "Markdown changing geometry", Theme.Light, RenderBackend.SW);
        var view = Attach(window);
        Action[] changes = [() => view.Bounds = new(23, 19, 260, 180), () => view.FontSize = 22,
            () => window.Theme = Theme.Dark, () => view.Markdown = Document + "\n\nreplacement"];
        foreach (var change in changes)
        {
            view.ScrollTo(80, 80); view.Select(0, 8);
            var thumb = Thumb(view, false);
            AtTrack(window, view, false, thumb.Y + thumb.H / 2); Press(window);
            change(); var scroll = view.ScrollOffset; string selection = view.SelectedText;
            Move(window, 700, 500); Release(window); window.Animator.Tick(.1f);
            Assert.Equal(scroll, view.ScrollOffset); Assert.Equal(selection, view.SelectedText);
            Assert.False(window.Animator.HasActiveAnimations);
        }
        view.Bounds = new(23, 19, 42, 42); view.ScrollTo(float.MaxValue, float.MaxValue);
        foreach (bool horizontal in new[] { false, true })
        {
            var thumb = Thumb(view, horizontal);
            Assert.InRange(horizontal ? thumb.W : thumb.H, 0, Page(view, horizontal));
            Assert.Equal(12, horizontal ? thumb.X : thumb.Y, 3);
            Assert.Equal(12 + Page(view, horizontal), horizontal ? thumb.Right : thumb.Bottom, 3);
            AtTrack(window, view, horizontal, 15); Press(window); var scroll = view.ScrollOffset;
            Move(window, 700, 500); Release(window); Assert.Equal(scroll, view.ScrollOffset);
        }
    });
}
