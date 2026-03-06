# Styling System Plan

## Goals

Replace the current flat theme-per-control-type system with a layered styling
model that supports descendant/bulk styling, shorthand token definitions, and
per-element overrides — without becoming a full CSS engine.

---

## Priority Layers (lowest → highest)

```
1. Theme          — base defaults for each control type (what exists today)
2. StyleContext    — ancestor-scoped overrides, attached to any container
3. Utility tokens  — tailwind-style compact string on individual elements
4. Direct props    — typed C# properties (Background, Padding, CornerRadius, …)
                     including LayoutData set by parent layouts
```

Each layer only overrides properties it explicitly sets. Unset properties fall
through to the next lower layer.

---

## Layer 1: Theme (exists today, minor changes)

`Theme` stays as-is: a bag of `Style` objects keyed by control type
(`Theme.Button`, `Theme.Panel`, etc.) plus global colors.

**Change:** `Style` fields become nullable so we can distinguish "not set" from
"set to zero/transparent." This is needed for the merge logic. See
"Property Resolution" below.

```csharp
public class Style
{
    public Color4? Background;
    public Color4? BackgroundHovered;
    // ... all fields become nullable
    public float? CornerRadius;
    public float? FontSize;
    public Thickness? Padding;
}
```

Theme styles are the terminal fallback. If a property is null even in the theme,
the control uses a hardcoded built-in default (e.g. FontSize=16, Padding=0).

---

## Layer 2: StyleContext (new)

A `StyleContext` is an object attached to any element. It provides style
overrides that cascade to all descendants (closest ancestor wins).

```csharp
public class StyleContext
{
    // Override by control type
    public Dictionary<Type, Style>? TypeOverrides;

    // Override by named class (element opts in via StyleClass property)
    public Dictionary<string, Style>? ClassOverrides;

    // Applies to ALL descendant elements regardless of type
    public Style? Universal;
}
```

**On Element:**
```csharp
public StyleContext? StyleContext { get; set; }
public string? StyleClass { get; set; }
```

**Usage (Blazor):**
```xml
<Panel StyleContext="@_sidebarCtx">
    <!-- All Buttons inside get dark styling -->
    <Button>Dark button</Button>
    <Panel>
        <Button>Also dark</Button>
    </Panel>
</Panel>
```

**Usage (native):**
```csharp
sidebar.StyleContext = new StyleContext
{
    TypeOverrides = { [typeof(Button)] = darkButtonStyle }
};
```

### Resolution walk

When an element needs a style property:

1. Walk up from self to root, collecting StyleContext hits:
   - Check `ClassOverrides[element.StyleClass]` if StyleClass is set
   - Check `TypeOverrides[element.GetType()]`
   - Check `Universal`
   - First non-null value for the requested property wins
2. If no ancestor StyleContext provides the property → fall through to Theme

This is O(depth × contexts), but:
- Depths are typically <20
- StyleContexts are sparse (most elements won't have one)
- Results are cached per element, invalidated on reparent or context change

---

## Layer 3: Utility Tokens (new)

A string property on Element that accepts space-separated tailwind-style tokens.

```csharp
public string? Tw { get; set; }  // short name, like className in React
```

```xml
<Panel Tw="bg-white rounded-none p-4 border-b border-gray-200">
<Label Tw="text-lg font-bold text-gray-400">muted subtitle</Label>
```

### Token vocabulary (initial subset)

Only the tokens we actually need. Not the full Tailwind spec.

| Category | Tokens | Maps to |
|----------|--------|---------|
| Background | `bg-white`, `bg-black`, `bg-transparent`, `bg-{color}` | Background |
| Text color | `text-{color}` | Foreground |
| Font size | `text-xs`(11) `text-sm`(13) `text-base`(16) `text-lg`(18) `text-xl`(20) `text-2xl`(24) | FontSize |
| Font weight | `font-bold`, `font-normal` | Bold |
| Padding | `p-{n}`, `px-{n}`, `py-{n}`, `pt-{n}` etc. | Padding |
| Corner radius | `rounded`, `rounded-none`, `rounded-sm`, `rounded-lg`, `rounded-full` | CornerRadius |
| Border | `border`, `border-0`, `border-{color}` | Border, BorderWidth |
| Opacity | `opacity-{0..100}` | Opacity |

Spacing scale: `0`=0, `0.5`=2, `1`=4, `2`=8, `3`=12, `4`=16, `5`=20, `6`=24,
`8`=32, `10`=40, `12`=48 (matches Tailwind's default 4px base).

Color palette: defined in Theme or a shared `Colors` dictionary. Tokens
reference named colors (`gray-400`, `blue-500`, `brand`, etc.), not hex values.

### Parsing

Tokens are parsed once when the `Tw` string changes (not every frame). The
parser produces a `Style` object that slots into the priority stack between
StyleContext and direct props.

The existing AI-ported Tailwind C# parser can be used if good enough, or we
write a minimal hand-rolled parser — the subset above is ~50 rules, not complex.

### State variants

**Not in initial scope.** `hover:bg-blue-600` style variants would require
hooking into OnStateChanged, which is control-specific. If needed later, the
parser can produce a `Dictionary<ElementState, Style>` instead of a single
Style, and controls would check it in their state handlers.

---

## Layer 4: Direct Props (exists today, no changes)

`Panel.Background`, `Panel.CornerRadius`, `Label.FontSize`, `Button.Color`,
`FlexItem.Grow`, etc. These always win over everything else.

No changes needed. They already override the style system.

---

## Property Resolution

For any style property P on any element, resolved value is:

```
directProp(P)
  ?? twStyle(P)                        // parsed from Tw tokens
  ?? walk ancestors for StyleContext(P)  // closest ancestor, type/class/universal
  ?? theme[controlType](P)             // Theme.Button, Theme.Panel, etc.
  ?? builtInDefault(P)                 // hardcoded fallback
```

### Nullable Style fields

Today `Style` uses value types (`float`, `Color4`) which can't distinguish
"not set" from "zero." Changing to nullable types (`float?`, `Color4?`) lets
the merge logic skip unset properties and fall through to lower layers.

**Migration:** Existing Theme definitions set all fields explicitly, so they
continue to work. Controls that read `Style.FontSize` would read
`Style.FontSize ?? 16f` (or a helper method `Style.GetFontSize(fallback)`).

---

## Inheritance vs Explicit-Only

### Decision needed

Two models for how unset properties flow:

**Option A: Explicit inheritance only**
- Properties only cascade through StyleContext if the StyleContext explicitly
  sets them. An element with no StyleContext and no direct prop gets the theme
  default — never its parent's value.
- Simpler mental model. Easy to reason about.
- Downside: can't set `text-gray-900` on a container and have all descendants
  pick it up unless each descendant type is listed in the StyleContext.

**Option B: Implicit inheritance for selected properties**
- Some properties (FontSize, Foreground/text color, FontName) are "inherited"
  by default — if not set on the element or any StyleContext, check the parent
  element's resolved value before falling back to theme.
- Matches CSS behavior for font/color properties.
- More powerful but harder to trace. "Where did this font size come from?"

**Option C (hybrid): StyleContext.Universal + no implicit inheritance**
- StyleContext already has a `Universal` style that applies to all descendants.
- Setting `Universal = new Style { Foreground = gray900 }` achieves the same
  effect as implicit color inheritance, but explicitly.
- No magic — the author chose to set Universal, so it's traceable.

**Current leaning: Option C.** It gives the power of inheritance without the
implicit behavior. Everything is explicit and traceable.

---

## `Initial` / Reset Value

### Decision needed

An `Initial` sentinel value that means "ignore this layer and fall through to
theme" — useful if a StyleContext sets `Background = red` and a child wants to
opt out back to the theme default.

**Arguments for:**
- Necessary if StyleContext.Universal is used heavily (some children need to
  escape it).

**Arguments against:**
- Adds complexity. A child can set its own direct prop to the theme value
  explicitly. Or use a different StyleClass.
- "Initial" would need to be distinguishable from null (null = not set,
  initial = explicitly reset). Requires a sentinel value or wrapper type.

**Suggestion:** Defer. Don't implement until a real use case demands it. If
needed, use a static sentinel: `Color4.Initial` / `StyleValue.Initial`.

---

## Caching and Invalidation

Each element caches its fully-resolved style (merged from all layers). The
cache is invalidated when:

- `Tw` string changes
- `StyleClass` changes
- `StyleContext` is set/changed on self or any ancestor
- Element is reparented (new ancestor chain)
- Window theme changes (already triggers `NotifyThemeChanged()` cascade)

`NotifyThemeChanged()` already walks all descendants — extend it to also
clear style caches. StyleContext changes trigger a similar descendant walk on
the container that changed.

---

## Implementation Order

1. **Make Style fields nullable.** Update all controls to handle nullable
   reads. This is the foundation — everything else depends on it.

2. **Add StyleContext.** Element gets `StyleContext` and `StyleClass` props.
   Modify `GetDefaultStyle()` / add `ResolveStyleProperty()` to walk
   ancestors. Minimal — no tokens, no Blazor serialization yet.

3. **Add Tw (utility tokens).** Parser + Element.Tw property. Slot parsed
   Style into the resolution chain between StyleContext and direct props.

4. **Blazor serialization.** StyleContext and Tw through AttributesBuilder.
   StyleContext goes through WeakObjectStore (reference type). Tw is a plain
   string (trivial).

5. **Migrate playground app.** Replace verbose per-element styling in
   App.razor/pages with StyleContext + Tw where it simplifies things.

---

## Files Affected

| File | Changes |
|------|---------|
| `Style.cs` | All fields → nullable, add merge helpers |
| `Element.cs` | Add `StyleContext`, `StyleClass`, `Tw`, resolution logic, cache |
| `Theme.cs` (same file) | No structural changes, but field types change |
| Every control | Update style reads from `Style.X` → `Style.X ?? fallback` |
| `AttributesBuilder.cs` | Serialize StyleContext (WeakObjectStore) |
| `AttributeHelper.cs` | Deserialize StyleContext |
| Blazor wrappers | Add StyleContext, StyleClass, Tw parameters + handlers |
| New: `TwParser.cs` | Tailwind token parser → Style |

---

## Open Questions

1. **Implicit inheritance (Option A/B/C)?** Current leaning is C (Universal
   in StyleContext). Need to validate with real UI code.

2. **Initial/reset value?** Defer until a concrete use case appears.

3. **State variants in Tw?** (`hover:bg-blue-500`) Defer to later iteration.

4. **Color palette source?** Define named colors in Theme? Separate static
   palette? Both are viable; Theme integration means dark/light modes get
   different palettes automatically.

5. **Use existing AI-ported Tailwind parser or write minimal hand-rolled?**
   Depends on quality of the port and how much of the spec we actually need.
