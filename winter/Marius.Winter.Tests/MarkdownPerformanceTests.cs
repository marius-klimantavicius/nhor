using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Marius.Winter.Scenarios;
using ThorVG;
using Xunit;
using Xunit.Abstractions;
using static Glfw.GLFW;

namespace Marius.Winter.Tests;

public class MarkdownPerformanceTests
{
    private readonly ITestOutputHelper _output;
    public MarkdownPerformanceTests(ITestOutputHelper output) => _output = output;
    private static object? Field(MarkdownView view, string name) => typeof(MarkdownView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view);

    [Fact]
    public void ModerateDocumentKeepsAllSectionsAndReusesParsedLayoutDuringScrolling()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(1200, 900, "Markdown moderate Debug", Theme.Light, RenderBackend.SW);
            string source = MarkdownScenarioData.Moderate();
            var view = new MarkdownView { Bounds = new(10, 10, 1120, 800), ImageResolver = MarkdownScenarioData.ResolveImage };
            window.AddChild(view);
            var watch = Stopwatch.StartNew(); view.Markdown = source; var parseLayout = watch.Elapsed.TotalMilliseconds;
            var layout = (MarkdownLayout)Field(view, "_layout")!;
            var parsed = Field(view, "_content");
            Assert.Equal(24, layout.Drawings.Count(d => d.Text.StartsWith("Section ")));
            Assert.Contains(layout.Drawings, d => d.Text == "Section 24");
            watch.Restart();
            WindowRegressionOwner.Flush(window);
            var canvas = (Canvas)typeof(Window).GetField("_canvas", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            Contracts.Draw(canvas, true); var firstDraw = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            for (int i = 0; i < 20; i++) { view.ScrollTo(0, i * 120); Contracts.Draw(canvas, true); }
            var scroll = watch.Elapsed.TotalMilliseconds;
            Assert.Same(layout, Field(view, "_layout")); Assert.Same(parsed, Field(view, "_content"));
            view.OnKeyDown(GLFW_KEY_END, GLFW_MOD_CONTROL, false);
            Assert.True(view.ScrollOffset.Y > layout.Height - 900);
            watch.Restart(); view.Bounds = new(10, 10, 560, 800); var resize = watch.Elapsed.TotalMilliseconds;
            Assert.Same(parsed, Field(view, "_content"));
            Assert.NotSame(layout, Field(view, "_layout"));
            view.OnKeyDown(GLFW_KEY_A, GLFW_MOD_CONTROL, false); view.OnKeyDown(GLFW_KEY_C, GLFW_MOD_CONTROL, false);
            Assert.Equal(view.Text, Glfw.Glfw.glfwGetClipboardString(null));
            Assert.StartsWith("Section 1\n\nWinter field notes\n\nNative Markdown for readable documents", view.SelectedText);
            Assert.DoesNotContain("**readable documents**", view.SelectedText);
            Assert.Same(view.SelectedText, view.SelectedText);
            var selectedGeometry = Field(view, "_selectionRects"); view.SelectAll();
            Assert.Same(selectedGeometry, Field(view, "_selectionRects"));
            view.ScrollTo(0, 0);
            var narrow = (MarkdownLayout)Field(view, "_layout")!;
            var run = narrow.Drawings.First(d => d.Text.StartsWith("Native Markdown"));
            float x = 10 + 12 + run.Bounds.X, y = 10 + 12 + run.Bounds.Y + run.Bounds.H / 2;
            view.OnMouseDown(GLFW_MOUSE_BUTTON_LEFT, x, y);
            watch.Restart();
            for (int i = 0; i < 50; i++) view.OnMouseMove(x + 10 + i % 30 * 3, y);
            view.OnMouseUp(GLFW_MOUSE_BUTTON_LEFT, x + 97, y);
            double drag = watch.Elapsed.TotalMilliseconds;
            Assert.Same(narrow, Field(view, "_layout")); Assert.Same(parsed, Field(view, "_content"));
            _output.WriteLine($"Debug/SW: UTF16={source.Length}; commands={layout.Drawings.Count}; height={layout.Height:F0}; parse+layout+visible-paints={parseLayout:F2}ms; first-draw={firstDraw:F2}ms; 20 scroll+draw={scroll:F2}ms; half-width-relayout={resize:F2}ms; 50 cached drag updates={drag:F2}ms.");
        });
    }
}
