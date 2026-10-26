using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Marius.Winter.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using ThorVG;
using Xunit;
using static Glfw.GLFW;

namespace Marius.Winter.Tests;

public class MarkdownViewTests
{
    private static MarkdownLayout Layout(MarkdownView view) => (MarkdownLayout)typeof(MarkdownView).GetField("_layout", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;

    public sealed class CallbackProbe : ComponentBase
    {
        [Parameter] public Action<string>? Clicked { get; set; }
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<Marius.Winter.Blazor.Elements.MarkdownView>(0);
            builder.AddAttribute(1, "Markdown", "[link](action:queued)");
            builder.AddAttribute(2, "LinkClicked", EventCallback.Factory.Create<string>(this, url => Clicked?.Invoke(url)));
            builder.CloseComponent();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void QueuedBlazorLinksExpireAcrossDisabledOrDetachedOwner(bool disable)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(640, 480, "Markdown queued lifetime", Theme.Light, RenderBackend.SW);
            var clicks = new List<string>(); var host = window.UseBlazor();
            var mounted = host.AddComponent<CallbackProbe>(new() { ["Clicked"] = (Action<string>)clicks.Add });
            Assert.True(mounted.IsCompletedSuccessfully);
            var view = Walk(window).OfType<MarkdownView>().Single();
            WindowRegressionOwner.Flush(window); view.OnFocus(); view.OnKeyDown(GLFW_KEY_ENTER, 0, false);
            Assert.Empty(clicks);
            if (disable) { view.Enabled = false; view.Enabled = true; }
            else { var parent = view.Parent!; parent.RemoveChild(view); parent.AddChild(view); }
            while (window.DispatcherQueue.TryDequeue(out var action)) action();
            Assert.Empty(clicks);
            view.OnFocus(); view.OnKeyDown(GLFW_KEY_ENTER, 0, false);
            while (window.DispatcherQueue.TryDequeue(out var action)) action();
            Assert.Equal(new[] { "action:queued" }, clicks);
        });
    }

    private static IEnumerable<Element> Walk(Element element)
    { yield return element; foreach (var child in element.Children) foreach (var descendant in Walk(child)) yield return descendant; }

    [Fact]
    public void FocusedLinkPaintCannotLeakIntoHorizontalViewportGutter()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(640, 480, "Markdown clipped link", Theme.Light, RenderBackend.SW);
            var view = new MarkdownView("[a long visible link](action:link)\n\n```\n" + new string('z', 200) + "\n```") { Bounds = new(20, 20, 260, 150) };
            window.AddChild(view); WindowRegressionOwner.Flush(window);
            view.OnBlur(); view.ScrollTo(30, 0);
            var canvas = (Canvas)typeof(Window).GetField("_canvas", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            Contracts.Draw(canvas, true); var before = WindowRuntimeTests.Buffer(window).ToArray();
            view.OnFocus(); Contracts.Draw(canvas, true); var after = WindowRuntimeTests.Buffer(window);
            // The link's horizontal focus edges cross this gutter without the
            // viewport clip. The separate control outline is farther left.
            foreach (int y in Enumerable.Range(31, 28)) foreach (int x in Enumerable.Range(24, 6))
                Assert.Equal(before[y * 640 + x], after[y * 640 + x]);
        });
    }

    [Fact]
    public void KeyboardStopsFollowLinkOccurrencesAcrossStylesAndAdjacentEqualDestinations()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(640, 480, "Markdown semantic links", Theme.Light, RenderBackend.SW);
            var view = new MarkdownView("[**bold** plain](action:styled) [a](action:same)[b](action:same) [last](action:last)") { Bounds = new(0, 0, 400, 200) };
            window.AddChild(view); WindowRegressionOwner.Flush(window);
            var clicks = new List<string>(); view.LinkClicked += clicks.Add;
            view.OnFocus(); view.OnKeyDown(GLFW_KEY_ENTER, 0, false);
            for (int i = 0; i < 3; i++) { view.OnKeyDown(GLFW_KEY_RIGHT, GLFW_MOD_ALT, false); view.OnKeyDown(GLFW_KEY_ENTER, 0, false); }
            Assert.Equal(new[] { "action:styled", "action:same", "action:same", "action:last" }, clicks);
            Assert.Contains(Layout(view).Drawings, d => d.Text == "a");
            Assert.Contains(Layout(view).Drawings, d => d.Text == "b");
        });
    }

    [Fact]
    public void FirstFrameRendersHierarchyAndCachesLayoutUntilWidthFontOrSourceChanges()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(640, 480, "Markdown first frame", Theme.Light, RenderBackend.SW);
            var view = new MarkdownView("# Heading\n\nA **bold** paragraph with enough words to wrap when narrow.") { Bounds = new(10, 10, 500, 350) };
            window.AddChild(view); WindowRegressionOwner.Flush(window);
            Assert.True(view.ContentSize.Y > 70);
            var layout = Layout(view);
            view.Measure(500, 350); view.Arrange(view.Bounds);
            Assert.Same(layout, Layout(view));
            Contracts.Draw((Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!, true);
            var pixels = WindowRuntimeTests.Buffer(window);
            bool Ink(uint c) => ((c >> 16) & 255) < 130 && ((c >> 8) & 255) < 130 && (c & 255) < 130;
            Assert.Contains(Enumerable.Range(25, 40).SelectMany(y => Enumerable.Range(25, 160).Select(x => pixels[y * 640 + x])), Ink);
            view.Bounds = new(10, 10, 180, 350);
            Assert.NotSame(layout, Layout(view)); var narrow = Layout(view);
            view.FontSize = 24; Assert.True(view.ContentSize.Y > narrow.Height);
            view.Markdown = "updated"; Assert.Single(Layout(view).Drawings, d => d.Kind == MarkdownDrawingKind.Text);
            Assert.Equal("updated", Layout(view).Drawings.Single(d => d.Kind == MarkdownDrawingKind.Text).Text);
        });
    }

    [Fact]
    public void LinksUsePointerReleaseAndKeyboardAndScrollRejectsClippedHits()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(640, 480, "Markdown links", Theme.Light, RenderBackend.SW);
            var view = new MarkdownView("[first](action:first)\n\n" + string.Concat(Enumerable.Repeat("paragraph\n\n", 20)) + "[last](action:last)") { Bounds = new(20, 20, 300, 160) };
            window.AddChild(view); WindowRegressionOwner.Flush(window);
            var clicks = new List<string>(); view.LinkClicked += clicks.Add;
            var first = Layout(view).Drawings.First(d => d.Span?.Link != null);
            float x = 20 + 12 + first.Bounds.X + 3, y = 20 + 12 + first.Bounds.Y + 3;
            view.OnMouseDown(GLFW_MOUSE_BUTTON_LEFT, x, y); Assert.Empty(clicks);
            view.OnMouseUp(GLFW_MOUSE_BUTTON_LEFT, x, y); Assert.Equal(new[] { "action:first" }, clicks);
            view.OnFocus(); view.OnKeyDown(GLFW_KEY_END, GLFW_MOD_ALT, false);
            view.OnKeyDown(GLFW_KEY_ENTER, 0, false); Assert.Equal("action:last", clicks.Last());
            Assert.True(view.ScrollOffset.Y > 0);
            view.OnMouseDown(GLFW_MOUSE_BUTTON_LEFT, x, 0); view.OnMouseUp(GLFW_MOUSE_BUTTON_LEFT, x, 0); Assert.Equal(2, clicks.Count);
            view.Markdown = "no links"; view.OnKeyDown(GLFW_KEY_ENTER, 0, false); Assert.Equal(2, clicks.Count);
            window.RemoveChild(view); view.Dispose(); view.OnKeyDown(GLFW_KEY_ENTER, 0, false); Assert.Equal(2, clicks.Count);
        });
    }

    [Fact]
    public void ResolverCachesImagesAcrossResizeAndClearsPaintsOnReloadReplacementAndDisposal()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(640, 480, "Markdown resources", Theme.Light, RenderBackend.SW);
            int calls = 0;
            // Same byte-oriented SVG support as the existing native Picture API.
            var bytes = System.Text.Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg' width='80' height='40'><rect width='80' height='40' fill='#2288cc'/></svg>");
            var view = new MarkdownView("![badge](asset:badge) ![badge](asset:badge)") { Bounds = new(0, 0, 300, 150), ImageResolver = _ => { calls++; return new(bytes, "svg"); } };
            window.AddChild(view); WindowRegressionOwner.Flush(window);
            Assert.Equal(1, calls); Assert.Equal(2, Layout(view).Drawings.Count(d => d.Kind == MarkdownDrawingKind.Image));
            view.Bounds = new(0, 0, 240, 150); Assert.Equal(1, calls);
            view.ReloadImages(); Assert.Equal(2, calls);
            view.ImageResolver = _ => null; Assert.DoesNotContain(Layout(view).Drawings, d => d.Kind == MarkdownDrawingKind.Image);
            view.Markdown = "replacement"; Assert.Equal(0, view.ScrollOffset.Y);
            window.RemoveChild(view); window.AddChild(view); WindowRegressionOwner.Flush(window);
            view.Dispose(); Assert.True(view.IsDisposed); Assert.Empty(view.Scene.Paints());
        });
    }
}
