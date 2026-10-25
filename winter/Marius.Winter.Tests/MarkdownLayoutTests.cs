using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Xunit;

namespace Marius.Winter.Tests;

public class MarkdownLayoutTests
{
    private static MarkdownTextMetrics Measure(string text, MarkdownFont font) => new(new StringInfo(text).LengthInTextElements * font.Size * .5f, font.Size * .8f, font.Size * .2f);
    private static MarkdownLayout Layout(string source, float width = 220) => MarkdownLayout.Build(MarkdownContent.Parse(source), width, 16, Measure);

    [Fact]
    public void MultilineRawHtmlIsLiteralWithSeparateLinesAndNoMarkdownInterpretation()
    {
        var layout = Layout("<div>\n**literal**\n</div>");
        var text = layout.Drawings.Where(d => d.Kind == MarkdownDrawingKind.Text).ToArray();
        Assert.Equal(new[] { "<div>", "**literal**", "</div>" }, text.Select(d => d.Text));
        Assert.All(text, d => Assert.False(d.Font.Format.HasFlag(TextFormat.Bold)));
        Assert.True(text[1].Bounds.Y > text[0].Bounds.Y);
    }

    [Fact]
    public void WrappedStyledParagraphsKeepBaselineAndHeadingHierarchy()
    {
        var wide = Layout("# Heading\n\nregular **bold** with a long paragraph that wraps over many lines.", 400);
        var narrow = Layout("# Heading\n\nregular **bold** with a long paragraph that wraps over many lines.", 140);
        var heading = wide.Drawings.First(d => d.Text == "Heading");
        var bold = wide.Drawings.Single(d => d.Text == "bold");
        var regular = wide.Drawings.First(d => d.Text.StartsWith("regular"));
        Assert.True(heading.Font.Size > regular.Font.Size);
        Assert.True(bold.Font.Format.HasFlag(TextFormat.Bold));
        Assert.Equal(regular.Baseline, bold.Baseline);
        Assert.True(narrow.Height > wide.Height);
        Assert.All(narrow.Drawings.Where(d => d.Kind == MarkdownDrawingKind.Text), d => Assert.True(d.Bounds.X + d.Bounds.W <= 140.1f));
    }

    [Fact]
    public void QuoteAndNestedListIndentationAndRulesHaveRealGeometry()
    {
        var layout = Layout("> quote\n>\n> - parent\n>   - child\n\n---");
        var parent = layout.Drawings.Single(d => d.Text == "parent");
        var child = layout.Drawings.Single(d => d.Text == "child");
        Assert.True(child.Bounds.X > parent.Bounds.X);
        var bar = Assert.Single(layout.Drawings, d => d.Kind == MarkdownDrawingKind.QuoteBar);
        Assert.True(bar.Bounds.H > child.Bounds.Y);
        Assert.Contains(layout.Drawings, d => d.Kind == MarkdownDrawingKind.Rule && d.Bounds.W > 100);
    }

    [Fact]
    public void CodePreservesBlankLinesAndSpacesAndUsesHorizontalOverflow()
    {
        var layout = Layout("```\n  x\t y\n\n" + new string('z', 100) + "\n```", 120);
        var code = layout.Drawings.Where(d => d.Kind == MarkdownDrawingKind.Text).ToArray();
        Assert.Equal("  x  y", code[0].Text);
        Assert.True(code[1].Bounds.Y - code[0].Bounds.Y > 30);
        Assert.True(layout.Width > 120);
        Assert.Single(layout.Drawings, d => d.Kind == MarkdownDrawingKind.CodeBackground);
        Assert.All(code, d => Assert.True(d.Font.Code));
    }

    [Fact]
    public void TableRowsAlignRightAndCenterAndGrowToWrappedCells()
    {
        var layout = Layout("| Left | Right | Center |\n| :--- | ---: | :---: |\n| many words that must wrap | 4 | x |", 330);
        var cells = layout.Drawings.Where(d => d.Kind == MarkdownDrawingKind.Cell).ToArray();
        Assert.Equal(6, cells.Length);
        Assert.True(cells[3].Bounds.H > 32);
        var right = layout.Drawings.Single(d => d.Text == "4");
        var center = layout.Drawings.Single(d => d.Text == "x");
        Assert.Equal(cells[4].Bounds.X + cells[4].Bounds.W - 8, right.Bounds.X + right.Bounds.W, 2);
        Assert.Equal(cells[5].Bounds.X + cells[5].Bounds.W / 2, center.Bounds.X + center.Bounds.W / 2, 2);
        Assert.All(cells.Take(3), d => Assert.True(d.Header));
    }

    [Fact]
    public void VeryLongWordsWrapOnGraphemesWithLinearMeasurementWork()
    {
        const string grapheme = "👩🏽‍💻";
        var source = string.Concat(Enumerable.Repeat(grapheme, 1000));
        int calls = 0;
        var layout = MarkdownLayout.Build(MarkdownContent.Parse(source), 120, 16, (s, f) => { calls++; return Measure(s, f); });
        Assert.True(layout.Height > 100);
        Assert.All(layout.Drawings.Where(d => d.Kind == MarkdownDrawingKind.Text), d => Assert.Equal(0, d.Text.Length % grapheme.Length));
        Assert.InRange(calls, 1, 2100);
    }

    [Fact]
    public void ResolvedImagesFitWidthAndUnresolvedImagesRetainAltTextAndLink()
    {
        var content = MarkdownContent.Parse("[![badge](asset:badge)](action:badge) ![missing](https://never-fetched.test/img)");
        var layout = MarkdownLayout.Build(content, 120, 16, Measure, new Dictionary<string, Vector2> { ["asset:badge"] = new(400, 200) });
        var image = Assert.Single(layout.Drawings, d => d.Kind == MarkdownDrawingKind.Image);
        Assert.Equal(120, image.Bounds.W); Assert.Equal(60, image.Bounds.H);
        Assert.Equal("action:badge", image.Span!.Link);
        Assert.Contains("missing", string.Concat(layout.Drawings.Where(d => d.Kind == MarkdownDrawingKind.Text).Select(d => d.Text)));
    }
}
