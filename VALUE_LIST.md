# ValueList\<T\> Implementation Plan

## Motivation

Both the ThorVG port and the Taffy layout engine allocate small `List<T>` instances
on hot paths (every layout pass, every frame). Each `List<T>` is a class — it incurs
a heap allocation for the list object itself plus an internal `T[]` array. For
collections that are typically ≤8 elements and short-lived, a stack-friendly struct
with an inline buffer would eliminate GC pressure.

## Design: `ValueList<T>`

A **mutable value-type** list backed by an 8-element `[InlineArray]` buffer. When
count exceeds 8, it spills to a heap-allocated `T[]`.

```csharp
[InlineArray(8)]
public struct InlineBuffer8<T> { private T _element0; }

public struct ValueList<T>
{
    private InlineBuffer8<T> _inline;
    private T[]? _overflow;
    private int _count;

    public int Count => _count;
    public ref T this[int index] => ref ...;
    public void Add(T item) { ... }
    public void Clear() { _count = 0; _overflow = null; }
    public Span<T> AsSpan() => ...;
    public Enumerator GetEnumerator() => ...;
}
```

### Key design decisions

| Concern | Decision |
|---------|----------|
| **Mutability** | Must pass by `ref` to mutate (it's a struct). Callers that store in fields are fine; local variables need `ref` or re-assignment. |
| **Inline capacity** | 8 elements. Covers ≥90% of Taffy flex items/lines and ThorVG clip stacks without spill. |
| **Spill strategy** | Allocate `T[]` with doubling (16, 32, ...). Copy inline buffer to overflow on first spill. After spill, inline buffer is dead. |
| **Indexer** | Returns `ref T` for zero-copy access. |
| **Enumerator** | Struct enumerator over `Span<T>` — no allocation. |
| **Sort** | Provide `Sort(Comparison<T>)` that delegates to `MemoryExtensions.Sort(Span<T>)`. |
| **Framework** | `[InlineArray]` requires .NET 8+ with `LangVersion latest` (attribute available since .NET 8). Already used in codebase (`Float2`, `Byte4`, `MeasureCacheEntries`). |

### API surface

```csharp
public struct ValueList<T>
{
    // Properties
    int Count { get; }
    bool IsEmpty { get; }
    ref T this[int index] { get; }

    // Mutation (struct — pass by ref or use in-place)
    void Add(T item);
    void Add(ReadOnlySpan<T> items);
    void Insert(int index, T item);
    void RemoveAt(int index);
    void Clear();

    // Access
    Span<T> AsSpan();
    ReadOnlySpan<T> AsReadOnlySpan();
    T[] ToArray();

    // Sorting
    void Sort(Comparison<T> comparison);

    // Enumeration (foreach support)
    Enumerator GetEnumerator();
    public ref struct Enumerator { ... }
}
```

## InlineArray on .NET 8

The `[InlineArray(n)]` attribute **is available on .NET 8** (it was introduced in
.NET 8 / C# 12). The codebase already uses it:

- `src/ThorVG/renderer/tvgRender.types.cs` — `Float2`, `Float4`, `Float12`, `Byte3`, `Byte4`
- `winter/Marius.Winter/Taffy/Tree/Cache.cs` — `MeasureCacheEntries` (InlineArray of 9)

Current `Directory.Build.props`: `net8.0` + `LangVersion latest`. No framework
upgrade needed.

## Candidates for conversion

### Priority 1 — Taffy layout hot paths (per-layout-pass allocations)

| File | Line | Current | Typical size | Notes |
|------|------|---------|-------------|-------|
| `Flexbox.cs` | 542 | `new List<FlexItem>()` | 1–20 | Allocated every flex layout call |
| `Flexbox.cs` | 836,850,859,870 | `new List<FlexLine>(1)` | 1–4 | Flex lines per container |
| `GridAlgorithm.cs` | 155 | `new List<GridItem>(n)` | 1–20 | Allocated every grid layout call |
| `GridAlgorithm.cs` | 187–188 | `new List<GridTrack>()` | 1–12 | Column/row tracks |
| `TrackSizing.cs` | 1135 | `new List<GridTrack>()` | 1–8 | Sub-tracks |
| `GridTypes.cs` | 1079 | `new List<GridTrack>()` | 1–8 | Item axis tracks |

**Impact**: These are the biggest wins. Every window resize triggers layout for all
flex/grid containers. Eliminating these allocations removes the main source of
per-frame GC pressure from the layout engine.

**Migration**: These are all local variables. Change `List<FlexItem>` → `ValueList<FlexItem>`.
Since they're locals, mutations work directly. Methods that accept `List<T>` parameters
need to change to `ref ValueList<T>` or `ReadOnlySpan<T>`.

### Priority 2 — ThorVG renderer clip stacks

| File | Line | Current | Typical size | Notes |
|------|------|---------|-------------|-------|
| `tvgSwRenderer.cs` | 24 | `List<object?> clips` | 2–8 | Per SwTask, passed through pipeline |
| `tvgGlCommon.cs` | 329 | `List<object?> clips` | 2–8 | Per GlShape |
| `tvgCanvas.cs` | 79 | `new List<object?>()` | 2–8 | Canvas clip collection |
| `tvgGlRenderer.cs` | 158–159 | `List<GlRenderPass>` etc. | 2–6 | GL render stacks (push/pop) |

**Impact**: Moderate. Clip lists are small and fit inline. The GL render stacks use
push/pop patterns that map well to ValueList.

**Migration**: These are fields in classes, so mutation is fine (fields are accessed
by reference). The `clips` field is passed to other methods — those signatures need
updating. Since `object?` is a reference type, the inline buffer holds 8 pointers
(64 bytes on x64).

### Priority 3 — ThorVG scene paints

| File | Line | Current | Typical size | Notes |
|------|------|---------|-------------|-------|
| `tvgScene.cs` | 36 | `List<Paint> paints` | 1–30 | Persistent per scene |

**Impact**: Lower. These are long-lived, not allocated per frame. Benefit is mainly
from avoiding the List object header (24 bytes) and array indirection. Scenes with
>8 paints spill to heap anyway.

### Not candidates

- **SVG/Lottie loader lists** — allocated once during parsing, not hot path
- **TaffyTree `_nodes`, `_children`** — large persistent collections, not suitable
- **`List<Fill.ColorStop>`** — typically small but allocated during parsing only

## Implementation steps

### Step 1: Create `ValueList<T>` struct

- File: `src/ThorVG/common/ValueList.cs` (shared by both ThorVG and Taffy via
  project reference)
- Alternatively: `winter/Marius.Winter/Common/ValueList.cs` if Taffy-only first
- Include full API, struct enumerator, bounds checking in debug builds

### Step 2: Convert Taffy Flexbox (highest impact)

1. Change `List<FlexItem>` → `ValueList<FlexItem>` in `GenerateAnonymousFlexItems()`
2. Change `List<FlexLine>` → `ValueList<FlexLine>` in `CollectFlexLines()` variants
3. Update all methods that receive these as parameters: change `List<T>` →
   `ref ValueList<T>` or `Span<T>` depending on whether mutation is needed
4. Verify `FlexItem` and `FlexLine` are structs (they should be — check)

### Step 3: Convert Taffy Grid

1. Same pattern: `List<GridItem>` → `ValueList<GridItem>`, etc.
2. More complex due to `GridTypes.cs` dictionary usage (`List<ushort>` values)

### Step 4: Convert ThorVG clip lists

1. Change `List<object?>` → `ValueList<object?>` for clip collections
2. Update `PrepareCommon()` and related methods in both SW and GL renderers

### Step 5: Benchmark

- Create a benchmark comparing layout performance (flex container with 5 children,
  10 children, 20 children) before and after
- Measure allocation rate with `dotnet-counters` or BenchmarkDotNet `[MemoryDiagnoser]`

## Risks and mitigations

| Risk | Mitigation |
|------|-----------|
| **Struct copy overhead** | ValueList with 8 inline elements = 8×sizeof(T) + pointer + int. For reference types: ~72 bytes. Pass by `ref` to avoid copies. |
| **Forgetting `ref`** | Mutations on a copy are silently lost. Code review + analyzer. Consider a debug-mode "version" field that detects stale copies. |
| **Spill path perf** | After spill, ValueList behaves like a regular list. The inline buffer becomes dead weight (~64 bytes). Acceptable tradeoff for the common small case. |
| **Generic constraints** | No constraints needed — works for any T. |
| **Thread safety** | Same as List\<T\>: none. Not a concern for single-threaded layout. |

## Size estimates

- `ValueList<FlexItem>` where FlexItem is a struct of ~80 bytes: inline buffer = 640 bytes.
  This is large for stack. Consider `InlineArray(4)` for large value types.
- `ValueList<object?>` (clips): inline buffer = 64 bytes on x64. Fine for stack.
- **Decision**: Use `InlineBuffer8<T>` for reference types and small structs. For large
  structs (>32 bytes per element), consider `InlineBuffer4<T>` variant or just use the
  default 8 and accept the stack cost (still better than heap allocation + GC).
