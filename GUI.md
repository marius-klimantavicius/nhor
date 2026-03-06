# GUI Framework Plan

A lightweight, efficient GUI framework built directly on the ported ThorVG engine.
Uses ThorVG's retained-mode scene graph with smart shape management — shapes are
created once, mutated in-place, and never cleared/re-added per frame.

## Core Architecture

### Design Principles

1. **Retained shapes** — Each control creates its ThorVG paints (Shape, Text) once
   and adds them to the canvas. Property changes mutate the existing paints via
   `SetFill`, `Translate`, `Opacity`, `SetText`, etc. ThorVG tracks dirty flags
   per-paint (`RenderUpdateFlag`) and only re-renders changed regions.

2. **Scene tree = UI tree** — Each `Element` owns a `ThorVG.Scene`. Child elements'
   scenes are added to their parent's scene. Transforms cascade automatically via
   ThorVG's parent transform accumulation. Moving a panel moves all its children.

3. **Lazy updates** — Elements track their own dirty state. When a property changes
   (text, color, size, position), the element updates only the affected ThorVG
   paints. The framework calls `canvas.Update()` → `canvas.Draw()` → `canvas.Sync()`
   once per frame. ThorVG's spatial-partition dirty region tracker ensures only
   changed pixels are re-rendered.

4. **No allocations in steady state** — After initial setup, the render loop
   allocates nothing. Shape paths are rebuilt only on resize. Colors, transforms,
   opacity, and visibility are mutated in-place (zero-alloc operations in ThorVG).

### Rendering Pipeline (per frame)

```
if (any element dirty OR any animation running) {
    // Elements already mutated their ThorVG paints when properties changed.
    // Animations tick and mutate paints.
    canvas.Update();   // ThorVG diffs old/new bounds, marks dirty regions
    canvas.Draw();     // Only dirty spatial partitions are re-rendered
    canvas.Sync();     // Wait for SW renderer

    // GL blit: upload buffer to texture, draw fullscreen quad, swapBuffers
}
```

When nothing changes, the frame loop is a no-op (no Update/Draw/Sync calls).

---

## Element Base Class

```
Element
├── Scene scene          // ThorVG scene owned by this element
├── Element? parent
├── List<Element> children
├── RectF bounds         // local-space bounds (x, y, w, h)
├── bool visible
├── bool enabled
├── float opacity
├── Cursor cursor
└── ElementState state   // Normal, Hovered, Pressed, Focused, Disabled
```

### Lifecycle

| Phase | What happens |
|-------|-------------|
| **Construct** | `scene = Scene.Gen()`. Subclass creates its ThorVG paints and adds them to `scene`. |
| **Attach** | Parent calls `parent.scene.Add(child.scene)`. Element receives `OnAttached()`. |
| **Layout** | Framework computes `bounds`. Element calls `scene.Translate(bounds.X, bounds.Y)` and updates internal paint geometries if size changed. |
| **Update** | On property change, element mutates its ThorVG paints directly. Sets `dirty = true` on the hosting `Window`. |
| **Detach** | Parent calls `parent.scene.Remove(child.scene)`. Element receives `OnDetached()`. |

### Clipping

Each element can optionally clip its children to its bounds:

```csharp
var clipRect = Shape.Gen();
clipRect.AppendRect(0, 0, bounds.W, bounds.H);
scene.Clip(clipRect);
```

Updated when bounds change. Used by Panel (scrollable content).

### Visibility

`element.Visible = false` → calls `scene.Visible(false)` → ThorVG hides the
entire subtree. No per-child iteration needed. ThorVG marks the old region as
damaged so it's properly erased.

---

## State & Styling

### ElementState

```csharp
[Flags]
enum ElementState { Normal=0, Hovered=1, Pressed=2, Focused=4, Disabled=8 }
```

State is computed from input events. When state changes, the element calls its
`ApplyStyle()` method which updates ThorVG paint properties (fill color, stroke,
opacity) to match the new state.

### Style

```csharp
record Style {
    Color Background;
    Color BackgroundHovered;
    Color BackgroundPressed;
    Color BackgroundDisabled;
    Color Foreground;           // text color
    Color ForegroundDisabled;
    Color Border;
    float BorderWidth;
    float CornerRadius;
    float FontSize;
    string FontName;            // ThorVG font name
    Thickness Padding;
}
```

Styles are value objects. A `Theme` provides default styles per control type.
Controls can override individual style properties. Style resolution:
`element.Style ?? Theme.GetStyle(elementType) ?? Theme.Default`.

### Theme

```csharp
class Theme {
    Style Button, TextBox, Panel, Checkbox, Slider, Label;
    string FontSansName, FontMonoName, FontIconsName;
    // Loaded via Text.LoadFont() at startup
}
```

---

## Animation System

### AnimationManager

A central `AnimationManager` ticks all active animations each frame. Animations
mutate ThorVG paints directly — no intermediate state copies.

```csharp
class AnimationManager {
    List<Animation> active;

    void Tick(float dt) {
        for (int i = active.Count - 1; i >= 0; i--) {
            var a = active[i];
            a.elapsed += dt;
            float t = Clamp01(a.elapsed / a.duration);
            float eased = a.easing(t);
            a.apply(eased);           // mutates ThorVG paint directly
            if (t >= 1.0f) {
                a.onComplete?.Invoke();
                active.RemoveAt(i);
            }
        }
    }
}
```

### Animation

```csharp
class Animation {
    float duration;
    float elapsed;
    Func<float, float> easing;    // EaseInOut, Linear, etc.
    Action<float> apply;          // called with eased t ∈ [0,1]
    Action? onComplete;
}
```

### Usage from controls

```csharp
// Button hover: animate background color
void OnStateChanged(ElementState oldState, ElementState newState) {
    var from = GetBackgroundColor(oldState);
    var to = GetBackgroundColor(newState);
    animator.Start(new Animation {
        duration = 0.15f,
        easing = Easing.EaseOut,
        apply = t => {
            var c = Color.Lerp(from, to, t);
            backgroundShape.SetFill(c.R, c.G, c.B, c.A);
        }
    });
}
```

Animations are cancellable. Starting a new animation on the same property
cancels any in-flight animation for that property (tracked by element + property
key).

### Easing Functions

```
Linear, EaseIn, EaseOut, EaseInOut, EaseInCubic, EaseOutCubic, EaseInOutCubic
```

Implemented as `Func<float, float>`. Standard cubic bezier curves.

---

## Input System

### Event Flow

```
GLFW callback
  → Window.DispatchMouseMove / DispatchMouseButton / DispatchKey / DispatchText
    → Hit test (walk element tree, check bounds, front-to-back)
    → Deliver to target element
    → Bubble up to parent if unhandled
```

### Hit Testing

Walk children in reverse order (front-to-back). For each child, transform the
point into local coordinates and check `bounds.Contains(localPoint)`. Skip
invisible/disabled elements. First hit wins.

For overlays (tooltips, popups), a separate overlay list is tested first.

### Mouse Events

```csharp
// Delivered to elements:
OnMouseEnter()                    // cursor entered bounds
OnMouseLeave()                    // cursor left bounds
OnMouseMove(float x, float y)    // cursor moved within bounds
OnMouseDown(MouseButton, float x, float y)
OnMouseUp(MouseButton, float x, float y)
OnClick()                         // down+up within bounds
OnScroll(float dx, float dy)
OnDrag(float dx, float dy)       // mouse move while button held
```

State transitions:

```
Normal → [mouse enters] → Hovered → [button down] → Pressed → [button up] → Hovered / Click
Hovered → [mouse leaves] → Normal
Pressed → [mouse leaves while held] → still Pressed (capture)
Pressed → [button up outside] → Normal (no click)
```

Mouse capture: when a button is pressed, the element receives all subsequent
mouse events until button release, even if the cursor leaves the element.

### Keyboard Events

```csharp
OnKeyDown(Key key, KeyModifier mods, bool repeat)
OnKeyUp(Key key, KeyModifier mods)
OnTextInput(string text)          // from GLFW char callback
OnFocus()
OnBlur()
```

Focus is managed by the Window. Tab/Shift+Tab cycles focus. Clicking an element
focuses it. Only one element has focus at a time.

### Cursor Management

Each element declares a cursor type. When the hovered element changes, the
window calls `glfwSetCursor()`. Standard cursors are pre-created at startup.

---

## Layout System

### Approach

Simple and direct. Two layout modes:

1. **Manual** — Element.Position and Element.Size set explicitly. No auto-layout.
2. **Stack** — Children arranged in a row or column with spacing.

Layout is computed top-down: parent computes child positions, then children
lay out their own children recursively.

### StackLayout

```csharp
class StackLayout {
    Orientation orientation;   // Horizontal, Vertical
    float spacing;
    Alignment crossAlign;      // Start, Center, End, Stretch
}
```

### Measure & Arrange

```csharp
// Phase 1: Measure (bottom-up)
Vector2 Measure(float availableWidth, float availableHeight)
    // Leaf elements return their natural size
    // Containers sum children sizes + spacing

// Phase 2: Arrange (top-down)
void Arrange(RectF finalBounds)
    // Sets bounds, calls scene.Translate(), updates clip rect
    // If size changed: rebuild shape paths (AppendRect etc.)
```

Measure is called when content changes (text changed, child added). Arrange is
called when bounds are assigned. Both are lazy — only re-run when marked dirty.

---

## Controls

### Button

```
Scene
├── Shape backgroundShape    // rounded rect, fill changes on hover/press
├── Shape borderShape        // rounded rect stroke
└── Text labelText           // centered text
```

**Properties:** `string Text`, `Action? Clicked`, `Style Style`

**Behavior:**
- Hover: animate background to `BackgroundHovered` (0.15s ease-out)
- Press: animate background to `BackgroundPressed` (0.05s ease-out)
- Release inside: fire `Clicked`, animate back to `BackgroundHovered`
- Release outside: animate back to `Normal`
- Disabled: `BackgroundDisabled` + `ForegroundDisabled`, no hover/press

**Shape updates:**
- `Text` changed → `labelText.SetText(value)`, re-measure, re-center
- Bounds changed → `backgroundShape.ResetShape()` + `AppendRect(...)`,
  same for borderShape
- State changed → `backgroundShape.SetFill(r, g, b, a)`,
  `labelText.SetFill(r, g, b)`

### Label / TextBlock

```
Scene
└── Text textPaint           // single-line or multi-line
```

**Properties:** `string Text`, `float FontSize`, `Color Color`,
`TextWrap Wrap` (None, Word), `TextHAlign HAlign` (Left, Center, Right)

**Formatting** — Inline formatting via a simple markup:
- `**bold**` → switches to bold font
- `*italic*` → sets italic shear on text
- `~~strike~~` → draws strikethrough line (separate Shape)
- `{color=#FF0000}text{/color}` → color span

Implementation: parse markup into `Span[]` where each span has a font name,
size, color, and text. Each span becomes a separate `ThorVG.Text` paint,
positioned by accumulating advance widths.

For single-style text (no markup), a single `ThorVG.Text` is used — zero
parsing overhead.

**Multi-line:** Uses `Text.SetLayout(w, h)` + `Text.SetWrapping(TextWrap.Word)`
for ThorVG-native word wrapping. Avoids manual line-break calculation.

**Shape updates:**
- `Text` changed → `textPaint.SetText(value)` (or rebuild spans)
- Color changed → `textPaint.SetFill(r, g, b)`
- Bounds changed → `textPaint.SetLayout(w, h)`

### TextBox

```
Scene
├── Shape backgroundShape    // rounded rect
├── Shape borderShape        // stroke
├── Shape selectionShape     // selection highlight rect (visible when selecting)
├── Shape cursorShape        // thin rect, blinks via opacity animation
├── Text textPaint           // the text content
└── Text placeholderPaint    // placeholder text (visible when empty)
```

**Properties:** `string Text`, `string Placeholder`, `bool IsReadOnly`,
`Regex? Validation`, `Action<string>? TextChanged`

**Cursor & Selection:**
- Cursor position tracked as character index
- Selection tracked as (anchor, cursor) pair
- `cursorShape`: thin rect at cursor position, opacity animated 0↔1 (blink)
- `selectionShape`: rect covering selected range, semi-transparent fill
- Character positions computed via `Text.Bounds()` on substrings (cached)

**Text measurement cache:**
- Cache glyph positions for the current text (array of x-offsets)
- Invalidated when text changes
- Used for: cursor positioning, click-to-position, selection rendering

**Input handling:**
- Click: position cursor at nearest character boundary
- Shift+Click: extend selection
- Double-click: select word
- Triple-click: select all
- Arrow keys: move cursor (with shift = extend selection)
- Ctrl+A/C/X/V: select all / copy / cut / paste (via GLFW clipboard)
- Backspace/Delete: delete selection or character
- Text input: insert at cursor, replace selection

**Scrolling:** When text overflows, a horizontal text offset is applied via
`textPaint.Translate(offsetX, 0)`. A clip rect masks the overflow.

**Shape updates:**
- Text changed → `textPaint.SetText()`, invalidate glyph cache, reposition cursor
- Cursor moved → update `cursorShape` translate, restart blink animation
- Selection changed → update `selectionShape` path (ResetShape + AppendRect)
- Focus → show cursor (start blink), highlight border
- Blur → hide cursor/selection, commit value

### Checkbox

```
Scene
├── Shape boxShape           // rounded rect outline
├── Shape checkShape         // checkmark path (✓), visibility toggled
└── Text labelText           // label text next to box
```

**Properties:** `bool IsChecked`, `string Text`, `Action<bool>? Changed`

**Behavior:**
- Click toggles `IsChecked`
- `checkShape.Visible(isChecked)` — ThorVG handles show/hide efficiently
- Hover: animate box border/fill
- The checkmark path is built once (MoveTo/LineTo for ✓ shape)

**Shape updates:**
- `IsChecked` changed → `checkShape.Visible(value)`, fire `Changed`
- `Text` changed → `labelText.SetText(value)`, re-measure

### Slider

```
Scene
├── Shape trackShape         // horizontal rect (track background)
├── Shape fillShape          // filled portion of track (0 to value)
├── Shape thumbShape         // circle or rounded rect (draggable)
└── Text? valueText          // optional value label
```

**Properties:** `float Value`, `float Min`, `float Max`, `float Step`,
`Action<float>? ValueChanged`, `bool ShowValue`

**Behavior:**
- Click on track: jump to value at click position
- Drag thumb: continuous value update
- Value clamped to [Min, Max], snapped to Step if set
- Hover on thumb: animate thumb scale (1.0 → 1.2)

**Shape updates:**
- Value changed → update `fillShape` path width, `thumbShape.Translate(x, cy)`,
  optionally update `valueText.SetText(formatted)`
- Bounds changed → rebuild track/fill/thumb paths

### Panel

```
Scene (clipped to bounds)
├── Shape backgroundShape    // optional background fill
├── [child scenes...]        // child elements
└── Shape? scrollbarShape    // vertical scrollbar (if scrollable)
```

**Properties:** `Color? Background`, `bool Scrollable`,
`Thickness Padding`, `Layout? Layout`

**Scrolling:**
- When content height > panel height, show scrollbar
- Mouse wheel scrolls content
- Content is a child Scene that gets `Translate(0, -scrollOffset)`
- Panel's scene is clipped to panel bounds via `scene.Clip(clipRect)`
- Scrollbar thumb is a Shape, draggable

**Shape updates:**
- Scroll offset changed → `contentScene.Translate(0, -offset)`, update scrollbar
- Bounds changed → update clip rect, re-layout children
- Background changed → `backgroundShape.SetFill(r, g, b, a)`

---

## Window (Root)

```csharp
class Window {
    GlfwWindow handle;
    SwCanvas canvas;
    uint[] buffer;
    Scene rootScene;              // canvas root
    AnimationManager animator;
    Element? hoveredElement;
    Element? focusedElement;
    Element? capturedElement;      // mouse capture
    List<Element> overlays;       // tooltips, popups (rendered last)
    bool dirty;

    // GL blit pipeline (same as ExampleFramework.SwWindow pattern)
    uint texId, vao, vbo, shaderProgram;
}
```

### Frame Loop

```csharp
void Run() {
    while (!glfwWindowShouldClose(handle)) {
        glfwPollEvents();

        float dt = ComputeDeltaTime();
        animator.Tick(dt);

        if (dirty) {
            dirty = false;
            canvas.Update();
            canvas.Draw();
            canvas.Sync();
            BlitToScreen();       // GL texture upload + fullscreen quad
            glfwSwapBuffers(handle);
        }
    }
}
```

### Idle optimization

When nothing changes (`dirty == false` and no animations running), the loop
only calls `glfwPollEvents()` — no rendering work. Could switch to
`glfwWaitEventsTimeout()` to reduce CPU usage when idle.

---

## File Structure

Solution: `winter/Marius.Winter.sln`

```
winter/
├── Marius.Winter.sln
├── Marius.Winter/
│   ├── Marius.Winter.csproj
│   ├── Core/
│   │   ├── Element.cs            // base class
│   │   ├── Window.cs             // root window, GLFW integration, GL blit
│   │   ├── Style.cs              // Style record, Theme
│   │   ├── Layout.cs             // StackLayout, Measure/Arrange
│   │   ├── InputManager.cs       // hit testing, focus, mouse capture
│   │   └── AnimationManager.cs   // animation tick, easing functions
│   ├── Controls/
│   │   ├── Button.cs
│   │   ├── Label.cs
│   │   ├── TextBox.cs
│   │   ├── Checkbox.cs
│   │   ├── Slider.cs
│   │   └── Panel.cs
│   └── Resources/
│       ├── Roboto-Regular.ttf
│       ├── Roboto-Bold.ttf
│       └── Roboto-Mono.ttf
└── Marius.Winter.Playground/
    ├── Marius.Winter.Playground.csproj
    └── Program.cs                // demo/test app
```

Project references: `../../src/ThorVG/ThorVG.csproj`, `../../src/Glfw/Glfw.csproj`.

Also add to `ThorVG.slnx`:
```xml
<Folder Name="/winter/">
    <Project Path="winter/Marius.Winter/Marius.Winter.csproj" />
    <Project Path="winter/Marius.Winter.Playground/Marius.Winter.Playground.csproj" />
</Folder>
```

---

## Implementation Order

| Phase | Scope | Deliverable |
|-------|-------|-------------|
| **1** | Core: Element, Window, GL blit, Theme | Empty window renders with background color |
| **2** | Core: InputManager, hit testing, focus | Mouse/keyboard events delivered to elements |
| **3** | Core: AnimationManager, easing | Property animations work |
| **4** | Core: Layout (StackLayout, Measure/Arrange) | Children auto-positioned |
| **5** | Control: Label | Static text with formatting |
| **6** | Control: Button | Clickable with hover/press animations |
| **7** | Control: Panel | Container with optional scroll + clip |
| **8** | Control: Checkbox | Toggle with checkmark animation |
| **9** | Control: Slider | Draggable thumb, value display |
| **10** | Control: TextBox | Full text editing with cursor/selection |

---

## Key Efficiency Details

### What happens when nothing changes
- `glfwPollEvents()` processes OS events
- `animator.Tick()` returns immediately (no active animations)
- `dirty` is false → skip `canvas.Update/Draw/Sync` and GL blit entirely
- CPU cost: effectively zero (just the poll syscall)

### What happens on a hover color change
1. `backgroundShape.SetFill(r, g, b, a)` — ThorVG sets `RenderUpdateFlag.Color`
2. `canvas.Update()` — ThorVG computes the shape's bounding region, marks it
   dirty in the spatial partition grid (16 cells). Skips path rasterization
   (only color changed). Cost: O(1).
3. `canvas.Draw()` — SW renderer redraws only the dirty cells overlapping the
   shape's bounds. Cost: O(shape_area), not O(canvas_area).
4. `canvas.Sync()` — waits for renderer.
5. GL blit uploads the full buffer (could be optimized to partial upload later).

### What happens on text change
1. `textPaint.SetText("new")` — ThorVG sets `RenderUpdateFlag.Path` (glyph
   outlines change).
2. `canvas.Update()` — ThorVG marks old and new text bounds as dirty. Rebuilds
   glyph outlines for the new text. Cost: O(text_length).
3. `canvas.Draw()` — redraws dirty region. Cost: O(text_area + old_text_area).

### Shape count estimate
A typical screen with 50 controls: ~200 ThorVG paints total (4 per control on
average). ThorVG's scene tree walk is O(N) but with early skip for clean paints
(`PaintSkip` returns true if `renderFlag == None`). In steady state, almost all
paints are clean → near-zero update cost.
