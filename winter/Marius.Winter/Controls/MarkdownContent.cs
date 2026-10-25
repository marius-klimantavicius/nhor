using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Marius.Winter;

internal enum MarkdownBlockKind { Paragraph, Heading, Code, Quote, List, Item, Rule, Table, Row, Cell }
internal sealed record MarkdownSpan(string Text, TextFormat Format = TextFormat.None, bool Code = false,
    string? Link = null, string? Image = null, bool Break = false, bool? Task = null, object? LinkIdentity = null);
internal sealed record MarkdownBlock(MarkdownBlockKind Kind, IReadOnlyList<MarkdownSpan> Spans,
    IReadOnlyList<MarkdownBlock> Children, int Level = 0, string? Marker = null, int Alignment = 0);
internal sealed class MarkdownContent
{
    // Keep the dialect deliberate: enabling every extension would promise layout
    // for constructs (math, diagrams, footnotes...) this native viewer cannot draw.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .UsePipeTables(new PipeTableOptions { UseGfmRules = true })
        .UseTaskLists().UseAutoLinks().Build();
    public IReadOnlyList<MarkdownBlock> Blocks { get; private init; } = new List<MarkdownBlock>();
    public static MarkdownContent Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new() { Blocks = ConvertBlocks(Markdig.Markdown.Parse(source, Pipeline)) };
    }

    private static IReadOnlyList<MarkdownBlock> ConvertBlocks(ContainerBlock container)
    {
        var result = new List<MarkdownBlock>();
        foreach (var block in container)
        {
            var empty = Array.Empty<MarkdownSpan>();
            var noChildren = Array.Empty<MarkdownBlock>();
            switch (block)
            {
                case HeadingBlock heading:
                    result.Add(new(MarkdownBlockKind.Heading, Inlines(heading.Inline), noChildren, heading.Level)); break;
                case ParagraphBlock paragraph:
                    result.Add(new(MarkdownBlockKind.Paragraph, Inlines(paragraph.Inline), noChildren)); break;
                case CodeBlock code:
                    result.Add(new(MarkdownBlockKind.Code, new[] { new MarkdownSpan(code.Lines.ToString(), Code: true) }, noChildren)); break;
                case HtmlBlock html:
                    // HTML is source text, never an HTML renderer input.
                    result.Add(new(MarkdownBlockKind.Code, new[] { new MarkdownSpan(html.Lines.ToString(), Code: true) }, noChildren)); break;
                case QuoteBlock quote:
                    result.Add(new(MarkdownBlockKind.Quote, empty, ConvertBlocks(quote))); break;
                case ListBlock list:
                    int number = list.IsOrdered && int.TryParse(list.OrderedStart, NumberStyles.None, CultureInfo.InvariantCulture, out var start) ? start : 1;
                    var items = new List<MarkdownBlock>();
                    foreach (var item in list.OfType<ListItemBlock>())
                        items.Add(new(MarkdownBlockKind.Item, empty, ConvertBlocks(item), Marker: list.IsOrdered ? $"{number++}." : "•"));
                    result.Add(new(MarkdownBlockKind.List, empty, items, list.IsLoose ? 1 : 0)); break;
                case ThematicBreakBlock:
                    result.Add(new(MarkdownBlockKind.Rule, empty, noChildren)); break;
                case Table table:
                    var rows = new List<MarkdownBlock>();
                    foreach (var row in table.OfType<TableRow>())
                    {
                        var cells = new List<MarkdownBlock>();
                        foreach (var cell in row.OfType<TableCell>())
                        {
                            var alignment = cell.ColumnIndex < table.ColumnDefinitions.Count ? table.ColumnDefinitions[cell.ColumnIndex].Alignment : null;
                            cells.Add(new(MarkdownBlockKind.Cell, empty, ConvertBlocks(cell), Alignment:
                                alignment == TableColumnAlign.Right ? 1 : alignment == TableColumnAlign.Center ? 0 : -1));
                        }
                        rows.Add(new(MarkdownBlockKind.Row, empty, cells, row.IsHeader ? 1 : 0));
                    }
                    result.Add(new(MarkdownBlockKind.Table, empty, rows)); break;
                case LinkReferenceDefinitionGroup:
                    break; // Definitions are consumed by the parser's links.
            }
        }
        return result;
    }

    private static IReadOnlyList<MarkdownSpan> Inlines(ContainerInline? inline)
    {
        var spans = new List<MarkdownSpan>();
        if (inline != null) Append(inline, spans, TextFormat.None, null);
        return spans;
    }

    private static void Append(ContainerInline container, List<MarkdownSpan> result, TextFormat format, string? link, object? identity = null)
    {
        for (var inline = container.FirstChild; inline != null; inline = inline.NextSibling)
        {
            switch (inline)
            {
                case LiteralInline literal: result.Add(new(literal.Content.ToString(), format, Link: link, LinkIdentity: identity)); break;
                case HtmlEntityInline entity: result.Add(new(entity.Transcoded.ToString(), format, Link: link, LinkIdentity: identity)); break;
                case HtmlInline html: result.Add(new(html.Tag, format, Link: link, LinkIdentity: identity)); break;
                case CodeInline code: result.Add(new(code.Content, format, Code: true, Link: link, LinkIdentity: identity)); break;
                case LineBreakInline line: result.Add(new(line.IsHard ? "" : " ", format, Link: link, Break: line.IsHard, LinkIdentity: identity)); break;
                case EmphasisInline emphasis:
                    var added = emphasis.DelimiterChar == '~' ? TextFormat.Strikethrough : emphasis.DelimiterCount == 2 ? TextFormat.Bold : TextFormat.Italic;
                    Append(emphasis, result, format | added, link, identity); break;
                case LinkInline target when target.IsImage:
                    var alt = new List<MarkdownSpan>(); Append(target, alt, format, null);
                    result.Add(new(string.Concat(alt.Select(s => s.Text)), format, Link: link, Image: target.Url ?? "", LinkIdentity: identity)); break;
                case LinkInline target: Append(target, result, format, target.Url ?? "", new object()); break;
                case AutolinkInline auto: result.Add(new(auto.Url, format, Link: auto.IsEmail ? "mailto:" + auto.Url : auto.Url, LinkIdentity: new object())); break;
                case TaskList task: result.Add(new("", format, Task: task.Checked)); break;
                case ContainerInline nested: Append(nested, result, format, link, identity); break;
            }
        }
    }
}
