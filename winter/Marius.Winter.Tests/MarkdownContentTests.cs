using System.Linq;
using Xunit;

namespace Marius.Winter.Tests;

public class MarkdownContentTests
{
    [Fact]
    public void HeadingsAndNestedInlineStylesSurviveParsingInsteadOfFlattening()
    {
        var blocks = MarkdownContent.Parse("# Title\n\n## Two\n\n### Three\n\n#### Four\n\n##### Five\n\n###### Six\n\nA **bold *both*** ~~gone~~ `a < b`.").Blocks;
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, blocks.Take(6).Select(b => b.Level));
        var paragraph = blocks[6];
        Assert.Equal(MarkdownBlockKind.Paragraph, paragraph.Kind);
        Assert.Contains(paragraph.Spans, s => s.Text == "both" && s.Format == (TextFormat.Bold | TextFormat.Italic));
        Assert.Contains(paragraph.Spans, s => s.Text == "gone" && s.Format == TextFormat.Strikethrough);
        Assert.Contains(paragraph.Spans, s => s.Text == "a < b" && s.Code);
    }

    [Fact]
    public void NestedListsQuotesRulesAndTasksKeepBlockStructureAndStartNumber()
    {
        var blocks = MarkdownContent.Parse("> quote\n>\n> 7. ordered\n>    - [x] done\n>    - [ ] pending\n\n---").Blocks;
        var quote = blocks[0]; Assert.Equal(MarkdownBlockKind.Quote, quote.Kind);
        var list = quote.Children[1]; Assert.Equal(MarkdownBlockKind.List, list.Kind);
        Assert.Equal("7.", list.Children[0].Marker);
        var nested = list.Children[0].Children[1];
        Assert.True(nested.Children[0].Children[0].Spans[0].Task);
        Assert.False(nested.Children[1].Children[0].Spans[0].Task);
        Assert.Equal(MarkdownBlockKind.Rule, blocks[1].Kind);
    }

    [Fact]
    public void CodeKeepsWhitespaceWhileEntitiesEscapesAndBreaksFollowParser()
    {
        var blocks = MarkdownContent.Parse("```cs\n  a\t b\n\n c\n```\n\n    indented  \n\nA &amp; \\*literal\\*  \nnext\nsoft").Blocks;
        Assert.Equal(MarkdownBlockKind.Code, blocks[0].Kind);
        Assert.Equal("  a\t b\n\n c", blocks[0].Spans[0].Text);
        Assert.Equal("indented  ", blocks[1].Spans[0].Text);
        Assert.Contains(blocks[2].Spans, s => s.Break);
        Assert.Equal("A & *literal*next soft", string.Concat(blocks[2].Spans.Select(s => s.Text)));
    }

    [Fact]
    public void LinksAndImagesStayInertAndHtmlIsLiteral()
    {
        var blocks = MarkdownContent.Parse("[docs](https://example.test) <https://example.test/auto> ![local](asset:badge) <b>literal</b>\n\n<script>alert(1)</script>").Blocks;
        Assert.Contains(blocks[0].Spans, s => s.Text == "docs" && s.Link == "https://example.test");
        Assert.Contains(blocks[0].Spans, s => s.Link == "https://example.test/auto");
        Assert.Contains(blocks[0].Spans, s => s.Text == "local" && s.Image == "asset:badge");
        Assert.Contains("<b>literal</b>", string.Concat(blocks[0].Spans.Select(s => s.Text)));
        Assert.Equal("<script>alert(1)</script>", string.Concat(blocks[1].Spans.Select(s => s.Text)));
    }

    [Fact]
    public void PipeTablesKeepHeadersAndColumnAlignment()
    {
        var table = Assert.Single(MarkdownContent.Parse("| Name | Count | Center |\n| :--- | ---: | :---: |\n| **winter** | 42 | yes |").Blocks);
        Assert.Equal(MarkdownBlockKind.Table, table.Kind);
        Assert.Equal(1, table.Children[0].Level);
        Assert.Equal(new[] { -1, 1, 0 }, table.Children[1].Children.Select(c => c.Alignment));
        Assert.Contains(table.Children[1].Children[0].Children[0].Spans, s => s.Format == TextFormat.Bold);
    }
}
