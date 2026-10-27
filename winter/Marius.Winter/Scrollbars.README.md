# Native scrollbars

Winter's native and Blazor controls use the same scrollbar geometry, painting and held-pointer interaction in `Controls/Scrollbar.cs`. Blazor mounts native controls; it does not use browser scrollbars.

## Inventory

| Control | Native scrolling and composition | Content-specific behavior retained |
| --- | --- | --- |
| MarkdownView | Both axes; reserves 10 logical pixels per axis alongside its 12-pixel document padding | Cached Markdown layout, horizontal code/table overflow, links, selection, keyboard navigation and selection autoscroll |
| TextArea | Both axes; 8-pixel content padding | Multiline editing, caret, selection, read-only behavior and selection autoscroll |
| RichTextBox | Both axes; 8-pixel content padding | Rich document/run editing, formatting, retained selection, history and selection autoscroll |
| LogView | Both axes; 8-pixel content padding | Cached line layouts, selectable logs, follow-tail, buffer subscriptions and selection autoscroll |
| ScrollPanel | Both axes; Auto/Always/Never independently | Child layout, nested scrolling, interactive child focus and a separate clipped child scene |
| TreeView | Both axes; 4-pixel track-end padding | Row selection, expansion, keyboard navigation and separately clipped row paints |
| ComboBox popup | Vertical | Item highlight, chooser focus, keyboard selection and popup placement |
| ListView / BasicTable | ListView's viewport is a ScrollPanel; BasicTable composes ListView | Keyed rows, selection and focused interactive row/cell children |
| Menu / context menu / submenus | MenuPopup has wheel and keyboard overflow scrolling; no painted scrollbar | Menu commands, submenu placement and keyboard focus |
| TextBox | Single-line caret-driven horizontal text offset; no painted scrollbar | Single-line editing and selection |
| Other controls / Taffy overflow | No additional native scrollbar painters | Taffy overflow sizing metadata is not a scrollbar widget |

The corresponding Blazor elements use these native implementations. List/table/menu wrappers preserve the same compositions.

## Shared behavior

Tracks and thumbs are 8 logical pixels thick, with rounded ends, a 20-pixel minimum thumb bounded by the actual track length, and a 2-pixel cross-axis hit allowance. Theme text color supplies the scrollbar color, with separate idle, hover, drag and disabled opacity. Both axes reserve space until their overflow geometry stabilizes. Track endpoints, thumb travel, hit testing and content range mapping use the same geometry.

A thumb press retains its grip point. A press elsewhere on the track first scrolls one viewport toward the pointer, clamps at the content end, and immediately retains that **same held press** for relative dragging from the new offset. Releasing outside the control ends capture. A stationary held press does not repeat. The native TextArea/RichTextBox reference allowed held dragging anywhere in the gutter and had no repeat timer; the shared behavior retains that drag behavior while preserving Markdown/LogView's initial one-viewport paging. Pointer motion after paging drives dragging, without another press or a timer moving the thumb toward the pointer.

The cursor becomes Arrow on first gutter entry, after text/link hover, and throughout captured scrollbar dragging outside the control. Explicit Arrow overrides ancestor IBeam/Hand cursors. A scrollbar press preserves selection and existing editor focus, and does not dispatch a content click, double-click or link activation.

Resize, source replacement, font/theme changes, scale changes, ancestor movement, hide, disable and detach invalidate the old gesture mapping. Scroll offsets and thumb positions clamp before painting. Markdown height-only resize retains the cached document layout. Text and selection keep their control-specific keyboard behavior.

## Regression coverage

`MarkdownScrollbarTests`, `ScrollbarInputTests`, `ScrollbarLifetimeTests` and `ScrollbarPaintTests` exercise real Window native pointer callbacks, nested hit testing and the actual GLFW cursor. They cover native controls and mounted Blazor components, both axes, initial paging plus same-press dragging, grip points, end clamps, outside release, small tracks, source shrink, Auto/Always/Never, changing geometry/lifetime, selection/focus preservation, popup item safety, and pixel-level content clipping through reordering and reattachment.

Native UI checks must use an owned display. On Linux, clear desktop display/authentication variables, supply an owned Xvfb display of at least 1600×1200, and set `LIBGL_ALWAYS_SOFTWARE=1` and `LP_NUM_THREADS=1`. Run native input sessions sequentially. Tests that save converter evidence require `WINTER_C07_EVIDENCE` to point to a writable evidence directory. Such software-rendered checks do not establish hardware rendering or Windows runtime behavior.
