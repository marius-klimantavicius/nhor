# MarkdownView

`Marius.Winter.MarkdownView` renders Markdown using native Winter/ThorVG text,
shapes and pictures. It is a read-only component with its own clipped, wrapping
viewport and vertical/horizontal scrollbars. No browser, HTML renderer, server,
external navigation or implicit image downloads are involved.

## Native

```csharp
var window = new Window(900, 700, "Field notes", Theme.Light);
var status = new Label("Choose a link");
var badge = System.Text.Encoding.UTF8.GetBytes(
    "<svg xmlns='http://www.w3.org/2000/svg' width='80' height='40'><rect width='80' height='40' fill='#245b86'/></svg>");
var view = new MarkdownView("# Field notes\n\nRead the [guide](action:guide).\n\n![Badge](asset:badge)")
{
    Bounds = new RectF(16, 16, 860, 590),
    ImageResolver = url => url == "asset:badge"
        ? new MarkdownImage(badge, "svg")
        : null
};
view.LinkClicked += destination => status.Text = destination;
status.Bounds = new RectF(16, 630, 860, 30);
window.AddChild(view);
window.AddChild(status);
window.Run();
```

`Markdown` replaces the document and resets scroll/selection/link focus. Equal
assignments keep state. `FontSize` follows `Theme.Label.FontSize` until explicitly
set; `ResetFontSize()` restores that behavior. Regular text follows the label
style font and foreground; bold and code use Winter's embedded bold and monospace
fonts. Theme changes and width/font changes invalidate layout without reparsing.
Use `ContentSize`, `ScrollOffset`, `ScrollTo(x, y)` and `Focus()` as needed.

## WinterBlazor

```razor
@using Marius.Winter.Blazor.Elements

<Panel>
    <GridLayout Columns="@(new[] { TrackSize.Fr() })"
                Rows="@(new[] { TrackSize.Fr(), TrackSize.Auto() })" />
    <MarkdownView Markdown="@source" ImageResolver="ResolveImage"
                  LinkClicked="ShowDestination" MinHeight="240" />
    <Label Text="@destination" />
</Panel>

@code {
    private string source = "# Hello Winter\n\n[Guide](action:guide)";
    private string destination = "Choose a link";
    private void ShowDestination(string value) => destination = value;
    private MarkdownImage? ResolveImage(string url) => null;
}
```

Parameters are `Markdown`, nullable `FontSize`, `ImageResolver` and
`EventCallback<string> LinkClicked`, plus standard Winter component sizing,
visibility and layout parameters. Replacing/clearing the callback, font or
resolver takes effect on rerender. `@ref` exposes `Focus()`, `ScrollTo()` and
`ReloadImages()`. Pending native link events expire when their source/callback
changes or their owner is hidden, disabled, detached or disposed.

## Dialect and interaction

The parser is **Markdig 1.4.0**, CommonMark with only strikethrough, strict GFM pipe
tables, task lists and autolinks enabled. Supported layout includes paragraphs,
headings 1–6, nested emphasis/strong/strikethrough, inline code, fenced and indented
code, blockquotes, ordered/unordered/nested lists, rules, links, tables and task
boxes. Escapes, entities and soft/hard breaks follow the parser. Raw HTML tags and
HTML blocks are displayed literally, including scripts; they are never executed.

Code keeps blank lines and spaces; tabs expand to four-column stops. Code does
not wrap, and wide tables use a minimum readable column width and horizontal
scrolling. Tables align cells left, right or center and wrap cell text. Task boxes
are display-only. Syntax highlighting, HTML interpretation, math/diagrams,
footnotes, media playback and other optional Markdig extensions are excluded.
Winter's existing font/shaping/glyph-coverage limits apply.

Click/release a link to invoke the callback. Tab/Shift+Tab focus the viewer through
Winter's usual focus traversal; Left/Right move among link occurrences, Enter or
Space activates one, and Ctrl+Home/End picks the first/last link and reveals it.
Up/Down, PageUp/PageDown, Home/End and wheel scroll vertically; Shift+wheel or the
horizontal scrollbar scroll wide code/tables. Focused links are clipped with the
document. Ctrl+A then Ctrl+C copies the **original Markdown source**; the context
menu offers the same whole-document operation. Arbitrary text-range selection or
copying a formatting-free rendered text range is not implemented.

## Images, ownership and cost

No resolver means alt-text placeholders. An explicit synchronous
`Func<string, MarkdownImage?>` receives the parser's image destination and can
return bytes from application memory, embedded resources, or an application
controlled local file read. Return `null` for unknown destinations. Encoded formats
are those accepted by `Picture.Load` (PNG/JPEG/WebP/SVG); malformed images fall
back to alt text. Treat byte arrays as immutable. SVG loading uses the existing
native ThorVG loader. The viewer itself performs no network/file access.

Resolution/decoding, including unavailable results, is cached per document and
resolver. `ReloadImages()` refreshes mutable resources. Resolution happens on the
UI owner: prepare slow resources beforehand. Images scale down to the available
width while preserving aspect ratio. Large or untrusted image budgets belong to
the application resolver, as with Winter's `Image` control.

The parsed model and full block layout are retained; only visible drawing commands
get native paints. Text measurements are cached per layout, and overlong words
split on graphemes with linear work rather than repeated prefix measurement.
Scrolling does not parse or measure text. Source replacement, detachment and
disposal release image/font/paint caches. There is no arbitrary document-length
truncation. Layout still scales with the whole document; this is not an editor or
a virtualized multi-gigabyte document system.

Both playgrounds have an interactive **Markdown** scenario with the same shared
document, an in-memory image, source/width updates, a 24-section document button
and link destination feedback. Global theme/font controls also apply.

## Dependency

Markdig is pinned in **Winter**, with no package changes to ThorVG. An established
AST parser avoids fragile partial Markdown parsing and supplies CommonMark
escaping, nesting and extension behavior. Release/version, extension APIs and
license were checked against the [official 1.4.0 release](https://github.com/xoofx/markdig/releases/tag/1.4.0),
[tagged source](https://github.com/xoofx/markdig/tree/1.4.0),
[documentation](https://xoofx.github.io/markdig/) and
[license](https://github.com/xoofx/markdig/blob/1.4.0/license.txt).
Its actual license is **BSD-2-Clause**, not MIT. The required redistribution notice
is included in [Markdig.LICENSE.txt](Markdig.LICENSE.txt) and copied to build output.
