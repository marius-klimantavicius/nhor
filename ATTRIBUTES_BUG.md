# Attribute Ordering Bug — Plan

## Problem

Blazor's diff engine does not guarantee the order in which attribute changes are
applied to native element handlers. `AddAttribute` in `RenderAttributes` only
declares the current attribute values — the renderer diffs them against the
previous frame and emits `SetAttribute` edits for changed values, in an order we
cannot control. Attributes whose values haven't changed may not be re-applied at
all.

This breaks any native control property setter that eagerly validates, clamps, or
fires events based on the current state of *other* properties.

### Known Affected Controls

| Control      | Attributes           | Bug                                                      |
|--------------|----------------------|----------------------------------------------------------|
| **Slider**   | Value, Min, Max      | Value clamped to [Min,Max] in setter; wrong if Min/Max not yet applied |
| **ComboBox** | Items, SelectedIndex | SelectedIndex validated against Items.Count; rejected if Items empty |
| **Image**    | Source, MimeType     | Source decoded using MimeType; wrong codec if MimeType not yet applied |
| **Label**    | Text, FontSize, Bold | Measurement in setter uses stale FontSize/Bold           |
| **MenuItem** | Label                | Label baked into native object at SetParent; cannot update after |
| **Tab**      | Label                | Same as MenuItem                                         |

### Why the Current Slider Fails

```
// Native Slider defaults: _min=0, _max=1
ApplyAttribute("Value", 50)  -> Math.Clamp(50, 0, 1) = 1  -> _value = 1
ApplyAttribute("Min", 0)     -> _min = 0; re-sets Value=1  -> clamp(1,0,1) = 1
ApplyAttribute("Max", 100)   -> _max = 100; re-sets Value=1 -> clamp(1,0,100) = 1
// Result: value stuck at 1 instead of 50
// AND: ValueChanged(1) fires during attribute application (before event handler registered)
```

## Design Principles

1. **Native controls keep working exactly as they do today** — property setters
   clamp, validate, and fire events. Programmatic usage (`slider.Value = 50`)
   continues to work with full event semantics. This is correct behavior for
   native (non-Blazor) usage.

2. **Native controls add atomic batch setters** for interdependent property
   groups. These apply all values together in the correct internal order, only
   fire one event at the end with the correct final value, and avoid intermediate
   invalid states.

3. **Blazor handlers use shadow state + batch setters** — they maintain their own
   copies of all interdependent attribute values. On every `ApplyAttribute` call,
   they update the shadow copy and then call the batch setter with all current
   values. This makes attribute application order irrelevant.

4. **No ActualXXX properties needed** — the native `Value` getter always returns
   the correctly clamped value (as it does today). The batch setter ensures the
   internal state is consistent before any clamping or event firing happens.

## Plan

### Phase 1: Slider (template for the pattern)

#### 1a. Native `Slider.cs` — add `SetRange` batch setter

Keep existing `Value`, `Min`, `Max` property setters unchanged (they work correctly
for programmatic use). Add one new method:

```csharp
/// <summary>
/// Atomically set min, max, and value. Applies min/max first, then clamps
/// value, then fires ValueChanged once if the effective value changed.
/// Used by Blazor handler to avoid attribute-ordering bugs.
/// </summary>
public void SetRange(float min, float max, float value)
{
    _min = min;
    _max = max;
    float clamped = Math.Clamp(value, _min, _max);
    if (_step > 0) clamped = MathF.Round(clamped / _step) * _step;
    bool changed = _value != clamped;
    _value = clamped;
    UpdateShapes();
    MarkDirty();
    if (changed) ValueChanged?.Invoke(_value);
}
```

#### 1b. Blazor `Slider` handler — shadow state + batch setter

The handler maintains shadow copies of Min, Max, Value. On any attribute change,
it updates the shadow and calls `SetRange`:

```csharp
class Handler : WinterElementHandler
{
    // Shadow state — always holds the latest Blazor-declared values
    float _min, _max = 1f, _value, _step;
    ulong OnValueChangedEventHandlerId;

    // ... constructor ...

    public override void ApplyAttribute(...)
    {
        switch (attributeName)
        {
            case "Value":
                _value = AttributeHelper.GetFloat(attributeValue);
                SliderControl.SetRange(_min, _max, _value);
                break;
            case "Min":
                _min = AttributeHelper.GetFloat(attributeValue);
                SliderControl.SetRange(_min, _max, _value);
                break;
            case "Max":
                _max = AttributeHelper.GetFloat(attributeValue, 1f);
                SliderControl.SetRange(_min, _max, _value);
                break;
            case "Step":
                _step = AttributeHelper.GetFloat(attributeValue);
                SliderControl.Step = _step;
                // Re-apply range in case step quantization changes effective value
                SliderControl.SetRange(_min, _max, _value);
                break;
            // ... ShowValue, event handler unchanged ...
        }
    }
}
```

Now regardless of which attribute Blazor's diff engine sends first (or skips),
every `ApplyAttribute` call applies the full consistent state.

#### 1c. Native setters and user interaction — no changes needed

`Value`, `Min`, `Max` setters keep their current behavior. `SetValueFromX` and
`OnKeyDown` keep their current behavior. Nothing changes for native usage.

### Phase 2: Apply same pattern to other controls

#### ComboBox

**Native `ComboBox.cs`** — add `SetItemsAndIndex`:

```csharp
/// <summary>
/// Atomically set items and selected index. Used by Blazor handler.
/// </summary>
public void SetItemsAndIndex(string[] items, int selectedIndex)
{
    _items.Clear();
    _items.AddRange(items);
    int clamped = Math.Clamp(selectedIndex, -1, Math.Max(0, _items.Count) - 1);
    bool changed = _selectedIndex != clamped;
    _selectedIndex = clamped;
    UpdateLabel();
    InvalidateMeasure();
    MarkDirty();
    if (changed) SelectionChanged?.Invoke(_selectedIndex);
}
```

**Blazor `ComboBox` handler** — shadow `_items` and `_selectedIndex`:

```csharp
case "Items":
    _items = AttributeHelper.GetStringArray(attributeValue) ?? Array.Empty<string>();
    ComboBoxControl.SetItemsAndIndex(_items, _selectedIndex);
    break;
case "SelectedIndex":
    _selectedIndex = AttributeHelper.GetInt(attributeValue, -1);
    ComboBoxControl.SetItemsAndIndex(_items, _selectedIndex);
    break;
```

#### Image

**Native `Image.cs`** — add `SetSource`:

```csharp
/// <summary>
/// Atomically set source bytes and MIME type. Used by Blazor handler.
/// </summary>
public void SetSource(byte[]? source, string mimeType)
{
    _source = source;
    _mimeType = mimeType;
    if (_shapesCreated) ReloadImage();
}
```

**Blazor `Image` handler** — shadow `_source` and `_mimeType`:

```csharp
case "Source":
    _source = AttributeHelper.GetByteArray(attributeValue);
    ImageControl.SetSource(_source, _mimeType);
    break;
case "MimeType":
    _mimeType = AttributeHelper.GetString(attributeValue) ?? "png";
    ImageControl.SetSource(_source, _mimeType);
    break;
```

This avoids double-decoding (currently both Source and MimeType setters call
`ReloadImage()` independently).

#### Label

Labels are already mostly safe — `Text`, `FontSize`, `Bold` setters call
`InvalidateMeasure()` and `MarkDirty()`, and actual measurement/rendering
reads all properties together at layout time. Verify no eager measurement
in setters. No batch setter needed unless we find an issue.

#### MenuItem / Tab

These bake `Label` into the native object at `SetParent` time. The Blazor
adapter already applies all attributes before `AddElementAsChildElement()` ->
`SetParent()`, so ordering is not a problem for initial creation. For label
updates after creation, add `UpdateLabel()` to native tab/menu item APIs.

### Phase 3: Audit remaining controls

Walk every `ApplyAttribute` override and every native control property setter.
Verify:

- [ ] Setter does NOT read sibling properties for validation/clamping
- [ ] No batch setter needed (properties are independent)
- [ ] Measurement is invalidated (not eagerly recomputed) in setters

Controls to audit: Button, Checkbox, TextBox, ProgressBar, ScrollPanel,
DialogWindow, TabWidget, TreeView, LogView, SvgImage, Separator.

ProgressBar is a borderline case — `Value` is clamped to [0,1] but this is a
fixed range (not dependent on sibling properties), so it's safe.

### Phase 4: Add regression test

Create a test that applies Slider attributes in every permutation of
(Value, Min, Max) and verifies that the final value is always correct:

```csharp
// Test via batch setter (Blazor path)
foreach (var perm in Permutations(("Value", 50f), ("Min", 0f), ("Max", 100f)))
{
    var slider = new Slider();
    float min = 0, max = 1, value = 0;
    foreach (var (name, val) in perm)
    {
        if (name == "Min") min = val;
        else if (name == "Max") max = val;
        else value = val;
        slider.SetRange(min, max, value);
    }
    Assert.Equal(50f, slider.Value);
}

// Test via individual setters (native path) — order matters, but correct
// order (Min, Max, Value) should work
var s = new Slider();
s.Min = 0; s.Max = 100; s.Value = 50;
Assert.Equal(50f, s.Value);
```

Also test ComboBox:
```csharp
// Items before SelectedIndex
var cb = new ComboBox();
cb.SetItemsAndIndex(new[] { "A", "B", "C" }, 2);
Assert.Equal(2, cb.SelectedIndex);

// SelectedIndex before Items (would fail without batch setter)
var cb2 = new ComboBox();
string[] items = Array.Empty<string>();
int idx = 2;
// Simulate "SelectedIndex arrives first"
cb2.SetItemsAndIndex(items, idx); // clamped to -1 (no items)
items = new[] { "A", "B", "C" };
cb2.SetItemsAndIndex(items, idx); // now 2 is valid
Assert.Equal(2, cb2.SelectedIndex);
```

## Why This Approach

### Alternative considered: "dumb setters + ActualXXX"

The original plan proposed making all native setters "dumb" (no clamping, no
events) and adding `ActualValue`/`ActualSelectedIndex` computed properties.
Events would only fire from user interaction (mouse/keyboard).

**Rejected because:**
- Breaks programmatic usage: `slider.Value = 50` would no longer fire
  `ValueChanged`, breaking native playground code and any non-Blazor consumer.
- Requires all code reading `Value` to switch to `ActualValue`.
- The "how do we know it's a user interaction" question has no clean answer —
  it would require threading an `isUserInteraction` flag through setters.

### Why shadow state + batch setters is better

- **Zero breaking changes** to native API — all existing code works identically.
- **Blazor handler is the only thing that changes** — it's a self-contained fix.
- **Batch setters are a clean addition** — they're useful beyond Blazor (any code
  that needs to set multiple interdependent properties atomically).
- **Simple to understand** — each Blazor handler maintains "what Blazor currently
  wants" and re-applies the full state on every attribute change.

## File Checklist

- [ ] `winter/Marius.Winter/Controls/Slider.cs` — add `SetRange(min, max, value)`
- [ ] `winter/Marius.Winter/Controls/ComboBox.cs` — add `SetItemsAndIndex(items, index)`
- [ ] `winter/Marius.Winter/Controls/Image.cs` — add `SetSource(bytes, mimeType)`
- [ ] `winter/Marius.Winter/Controls/Label.cs` — verify no eager measurement
- [ ] `winter/Marius.Winter.Blazor/Elements/Slider.cs` — shadow state + SetRange
- [ ] `winter/Marius.Winter.Blazor/Elements/ComboBox.cs` — shadow state + SetItemsAndIndex
- [ ] `winter/Marius.Winter.Blazor/Elements/Image.cs` — shadow state + SetSource
- [ ] `winter/Marius.Winter/Controls/TabWidget.cs` — add UpdateTabLabel API
- [ ] `winter/Marius.Winter/Controls/MenuBar.cs` — add UpdateItemLabel API
- [ ] Phase 3 audit of all remaining controls
- [ ] Phase 4 regression tests
