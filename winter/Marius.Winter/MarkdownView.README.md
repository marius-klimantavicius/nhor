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

`Text` is the cached rendered plain-text projection. `SelectionStart`,
`SelectionLength` and `SelectedText` describe a range in that text using UTF-16
offsets. `Select(start, length)` expands nonempty ranges to whole graphemes; a
zero-length range floors to a legal caret. `SelectAll()` selects rendered text;
`ClearSelection()` collapses the range at its active end.

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
`ReloadImages()`, plus the same `Text`, selection properties and
`Select()`, `SelectAll()`, `ClearSelection()` methods. Native input, highlights
and clipboard behavior are shared by both consumers.
Pending native link events expire when their source/callback
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

Drag across styled runs, wrapped lines or blocks to select rendered text. Dragging
outside the viewport scrolls vertically and horizontally, including wide code and
tables. Shift+click extends from the anchor. Left/Right move by graphemes;
Ctrl+Left/Right move by words; Up/Down retain the preferred horizontal position.
Home/End use the visual line, Ctrl+Home/End use the document. Shift extends these
movements. PageUp/PageDown and wheel scroll; Shift+PageUp/PageDown extends to the
viewport edge. Shift+wheel and the horizontal scrollbar scroll wide code/tables.
Highlights and carets use cached glyph advances and are clipped with the document.

Ctrl+A selects all **rendered text**. Ctrl+C and the context menu's **Copy** action
copy the selected plain-text range. **Copy Markdown source** is a separate explicit
context action and does not change the selection. Copy without a range leaves the
clipboard alone. The viewer remains read-only.

Tab/Shift+Tab use Winter's usual control focus traversal. On initial viewer focus,
the first link can be activated with Enter/Space. Alt+Left/Right moves among link
occurrences; Alt+Home/End picks the first/last link and reveals it. Text movement
returns to caret navigation; Alt link navigation preserves the range. A click and
release invokes a link; moving at least three logical pixels while pressed
suppresses activation, including when dragged back to the original link.

The projection joins paragraphs/headings/blocks with blank lines, list items with
newlines, table cells with tabs and table rows with newlines. Soft breaks are
spaces and hard breaks are newlines. Visual wrapping never adds characters. Code
preserves spaces, tabs and blank lines even though visual tabs expand to
four-column stops. Generated bullet/number labels, task boxes, quote bars and
rules contribute no text. Images contribute only their alt text, whether resolved
or shown as a placeholder; the generated `[image: …]` decoration is excluded.
Resolved images map their alt range across the image rectangle; placeholders
highlight the alt label itself.
Literal HTML remains literal text. Style delimiters and link URLs are not added
to copied labels.

Equal source assignments preserve state. Source replacement clears selection,
caret, drag, scroll and link focus before rebuilding. Width, scroll, theme, font
size, resolver changes and `ReloadImages()` preserve projection offsets and
rebuild geometry as needed. Detach/readd clears selection and cancels
drag/autoscroll; disposal releases all caches. Blur, hiding or disabling cancels
input/autoscroll while retaining the range.

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
get native paints. Text measurements and range geometry are cached per layout.
Overlong words split on graphemes with linear work rather than repeated prefix
measurement.
Caret advances reuse the log viewer's short scalar-pair font cache, including
kerning, with global grapheme boundaries across inline styles. Dragging and
scrolling do not parse or measure text. Source replacement replaces projection,
layout and image paints; detachment and disposal release image/font/paint caches.
There is no arbitrary document-length
truncation. Layout still scales with the whole document; this is not an editor or
a virtualized multi-gigabyte document system.

Both playgrounds have an interactive **Markdown** scenario with the same shared
document, an in-memory image, source/width updates, a 24-section document button
and link destination feedback. **Select sample** highlights a styled range;
**Show selection** displays its offsets and plain text. Both demos explain drag,
keyboard selection and rendered copy. Global theme/font controls also apply.

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
