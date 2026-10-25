using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ThorVG;
using Xunit;

namespace Marius.Winter.Tests;

public class MarkdownInlineAndListTests
{
    private static MarkdownLayout Layout(MarkdownView view) => (MarkdownLayout)typeof(MarkdownView)
        .GetField("_layout", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view)!;

    [Fact]
    public void NativeFontsKeepStyledWordsAndOrderedTaskMarkersOnTheirIntendedLines()
    {
        WindowRegressionOwner.Run(() =>
        {
            const int width = 1100, height = 700;
            using var window = new Window(width, height, "Markdown inline and list regressions", Theme.Light, RenderBackend.SW);
            void Caption(string text, RectF bounds) => window.AddChild(new Label(text) { Bounds = bounds, FontSize = 18 });
            MarkdownView View(string source, RectF bounds)
            {
                var view = new MarkdownView(source) { Bounds = bounds, FontSize = 16 };
                window.AddChild(view); return view;
            }
            Caption("Styled word, 91px content width", new(24, 24, 330, 30));
            Caption("Ordered list starting at 100", new(370, 24, 320, 30));
            Caption("Ordered list starting at 1000", new(720, 24, 350, 30));
            var word = View("A inter**national**.", new(24, 70, 125, 190));
            var threeDigits = View("100. First item\n101. Second item", new(370, 70, 320, 190));
            var fourDigits = View("1000. First item\n1001. Second item", new(720, 70, 350, 190));
            Caption("Ordered tasks retain numbers and checkboxes", new(24, 300, 530, 30));
            Caption("Unordered tasks retain checkboxes", new(600, 300, 460, 30));
            var tasks = View("3. [x] Complete first\n4. [ ] Complete second", new(24, 345, 530, 170));
            View("- [x] Complete first\n- [ ] Complete second", new(600, 345, 460, 170));
            Caption("All examples are actual native MarkdownView paints at FontSize 16.", new(24, 570, 1050, 30));
            WindowRegressionOwner.Flush(window);
            var canvas = (Canvas)typeof(Window).GetField("_canvas", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            Contracts.Draw(canvas, true);
            // Optional focused presentation evidence; no artifact is produced in
            // ordinary test runs. The caller supplies its ignored tmp destination.
            if (Environment.GetEnvironmentVariable("WINTER_MARKDOWN_REGRESSION_CAPTURE") is { Length: > 0 } path)
            {
                using var output = new BinaryWriter(File.Create(path));
                output.Write(Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n"));
                foreach (uint pixel in WindowRuntimeTests.Buffer(window))
                { output.Write((byte)(pixel >> 16)); output.Write((byte)(pixel >> 8)); output.Write((byte)pixel); }
            }
            var lines = Layout(word).Drawings.Where(d => d.Kind == MarkdownDrawingKind.Text).GroupBy(d => d.Bounds.Y)
                .Select(line => string.Concat(line.Select(d => d.Text)).Trim()).ToArray();
            Assert.Equal(new[] { "A", "international." }, lines);
            foreach (var (view, start) in new[] { (threeDigits, 100), (fourDigits, 1000) })
            {
                var marker = Assert.Single(Layout(view).Drawings, d => d.Text == $"{start}.");
                var body = Layout(view).Drawings.Single(d => d.Text == "First item");
                Assert.Equal(body.Baseline, marker.Baseline);
                Assert.True(marker.Bounds.Right < body.Bounds.X);
            }
            Assert.Contains(Layout(tasks).Drawings, d => d.Text == "3.");
            Assert.Contains(Layout(tasks).Drawings, d => d.Text == "4.");
            Assert.Equal(2, Layout(tasks).Drawings.Count(d => d.Kind == MarkdownDrawingKind.Task));
        });
    }
}
