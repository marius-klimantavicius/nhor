# Blazor Converter App — Implementation Plan

Desktop app that converts **SVG → PNG** and **Lottie JSON → GIF** using ThorVG,
with a Winter Blazor UI.

**Project**: `winter/Marius.Winter.Blazor.Converter/`

---

## Layout

```
┌──────────────────────────────────────────────────────────────────┐
│  [TextBox: /path/to/file.svg                              ] [⋯] │  ← path bar
├──────────────┬───────────────────────────────────────────────────┤
│              │                                                   │
│  TreeView    │   ┌─────────────────┐  ┌─────────────────┐       │
│  (file       │   │  Source Preview  │  │ Converted Preview│      │
│   browser)   │   │  (SvgImage or   │  │  (Image showing  │      │
│              │   │   first frame)  │  │   PNG / GIF)     │      │
│              │   └─────────────────┘  └─────────────────┘       │
│              │                                                   │
│              │   [ Convert ]                   [ Save As… ]      │
├──────────────┴───────────────────────────────────────────────────┤
│  Status: Ready / Converting… / Saved to /path/output.png        │
└──────────────────────────────────────────────────────────────────┘
```

- **Left pane** (~200 px): `TreeView` file browser, dynamically populated.
- **Right pane**: two preview panels side-by-side + action buttons + status bar.
- Top path bar: `TextBox` (editable) + browse button (⋯) that focuses the tree.

---

## Steps

### 1. Project scaffolding

Create the project skeleton:

```
winter/Marius.Winter.Blazor.Converter/
├── Program.cs
├── App.razor
├── Components/
│   ├── FileBrowser.razor          ← TreeView file browser
│   ├── SaveDialog.razor           ← modal save-as dialog with tree + new-folder
│   └── PreviewPanel.razor         ← single preview (source or converted)
├── Services/
│   └── ConverterService.cs        ← SVG→PNG and Lottie→GIF logic
├── PngEncoder.cs                  ← minimal PNG writer (zlib via DeflateStream)
├── _Imports.razor
└── Marius.Winter.Blazor.Converter.csproj
```

**`.csproj`**: `Sdk="Microsoft.NET.Sdk.Razor"`, `OutputType=Exe`,
`AllowUnsafeBlocks=true`, references `Marius.Winter.Blazor.csproj`.

**`Program.cs`**: Create `Window(1100, 700, "ThorVG Converter", Theme.Light, RenderBackend.SW)`,
call `UseBlazor()`, add `App` component, call `Run()`.

**`_Imports.razor`**: `@using Marius.Winter`, `@using Marius.Winter.Blazor`,
`@using Marius.Winter.Blazor.Elements`,
`@using Marius.Winter.Blazor.Converter.Components`,
`@using Marius.Winter.Blazor.Converter.Services`.

### 2. FileBrowser component (`FileBrowser.razor`)

A reusable `TreeView`-based file browser that:

- Accepts a `RootPath` parameter (default `/` on Linux).
- On first render, populates root-level directories.
- On node expand (selection), lazily loads child directories and matching files
  (`.svg`, `.json`, `.lottie`).
- Fires `EventCallback<string> OnFileSelected` with the full path when a file
  node is clicked.
- Uses folder/file icons (inline SVG strings).

**Implementation approach**: The tree is built in C# code-behind (`@code {}`)
by enumerating `Directory.GetDirectories` / `Directory.GetFiles` filtered to
supported extensions. Each `TreeNode` gets `Tag=fullPath`. Lazy loading is
triggered by `OnSelectionChanged` — when a directory node is selected, its
children are populated if not already loaded.

### 3. PreviewPanel component (`PreviewPanel.razor`)

A simple wrapper panel that:

- Accepts `byte[]? Data`, `string? MimeType`, `string? SvgContent`, and
  `float Width / Height`.
- If `SvgContent` is set → renders `<SvgImage Svg="@SvgContent" />`.
- If `Data` is set → renders `<Image Source="@Data" MimeType="@MimeType" />`.
- Shows a centered placeholder label ("No file loaded" / "Not yet converted")
  when both are null.

### 4. ConverterService (`Services/ConverterService.cs`)

Static methods, no DI needed:

```csharp
public static class ConverterService
{
    // Detect file type from extension
    public static FileKind Detect(string path);  // Svg, Lottie, Unknown

    // SVG → PNG: returns PNG byte array
    public static byte[] ConvertSvgToPng(string svgPath, uint maxDimension = 1024);

    // Lottie → GIF: returns GIF byte array
    public static byte[] ConvertLottieToGif(string lottiePath, uint quality = 100, uint fps = 0);
}
```

**SVG → PNG flow**:
1. `Initializer.Init()` (guard: only once).
2. `var picture = Picture.Gen(); picture.Load(path);`
3. `picture.GetSize(out w, out h);` — scale to fit `maxDimension` if needed.
4. Allocate `uint[] buffer = new uint[w * h]`.
5. `var canvas = SwCanvas.Gen(); canvas.Target(buffer, w, w, h, ColorSpace.ARGB8888);`
6. `canvas.Add(picture); canvas.Update(); canvas.Draw(true); canvas.Sync();`
7. Encode `buffer` to PNG bytes via `PngEncoder.Encode(buffer, w, h)`.

**Lottie → GIF flow**:
1. `Initializer.Init()`.
2. `var animation = Animation.Gen(); animation.GetPicture().Load(path);`
3. `animation.GetPicture().GetSize(out w, out h);`
4. Save to a temp file via `Saver`: `var saver = Saver.Gen(); saver.Save(animation, tempPath, quality, fps); saver.Sync();`
5. Read the temp GIF file bytes back, delete temp file, return bytes.

### 5. PngEncoder (`PngEncoder.cs`)

Minimal PNG encoder using `System.IO.Compression.DeflateStream` (built-in):

```csharp
public static class PngEncoder
{
    public static byte[] Encode(uint[] argbBuffer, uint width, uint height);
}
```

Writes: PNG signature → IHDR → IDAT (zlib-compressed RGBA scanlines with
filter byte 0 per row) → IEND. CRC32 computed per chunk. Converts ARGB8888
to RGBA byte order during encoding.

### 6. App.razor — Main layout

Uses `GridLayout` for the overall structure:

```
Columns: 200px | 1fr
Rows:    Auto (path bar) | 1fr (main content) | Auto (status bar)
```

- **Row 0, ColSpan 2**: Path bar — `FlexLayout` horizontal with `TextBox` (grow=1)
  + `Button` (⋯).
- **Row 1, Col 0**: `FileBrowser` component.
- **Row 1, Col 1**: Inner `GridLayout` or `FlexLayout`:
  - Top: two `PreviewPanel` side-by-side (source + converted).
  - Bottom: `FlexLayout` with `Convert` and `Save As…` buttons.
- **Row 2, ColSpan 2**: `Label` status bar.

**State**:
- `string? selectedPath` — currently selected file.
- `FileKind fileKind` — detected type.
- `string? svgContent` — loaded SVG string (for source preview).
- `byte[]? sourceImageData` — raw first-frame PNG for Lottie source preview.
- `byte[]? convertedData` — output PNG or GIF bytes.
- `string? convertedMimeType` — "png" or "gif".
- `string statusText` — status bar message.

**Interactions**:
- TextBox `OnTextChanged` → update `selectedPath`, auto-load if file exists.
- FileBrowser `OnFileSelected` → update `selectedPath` + TextBox, auto-load.
- **Load logic**: on path change, detect kind; if SVG, read file content into
  `svgContent`; if Lottie, render first frame to `sourceImageData`.
- **Convert button**: calls `ConverterService`, stores result in `convertedData`.
- **Save As… button**: opens `SaveDialog`.

### 7. SaveDialog component (`SaveDialog.razor`)

A `DialogWindow` containing:

- `TreeView` showing directories only (similar to FileBrowser but dirs only).
- `TextBox` for filename entry (pre-filled with suggested name like
  `input.png` or `input.gif`).
- `Button` "New Folder" — creates a directory under the currently selected
  tree node, refreshes tree.
- `Button` "Save" — combines selected directory + filename, fires
  `EventCallback<string> OnSave` with the full output path.
- `Button` "Cancel" — closes dialog.

Layout inside dialog:
```
┌─────────────────────────────────┐
│  Save As                        │
├─────────────────────────────────┤
│  [TreeView: directory browser]  │
│                                 │
│  Name: [output.png         ]    │
│  [New Folder] [Cancel] [Save]   │
└─────────────────────────────────┘
```

The parent `App.razor` controls dialog visibility with a `bool showSaveDialog`
flag. When `OnSave` fires, it writes `convertedData` to the chosen path via
`File.WriteAllBytes()` and updates the status bar.

### 8. Wiring it all together

1. User selects a file via TreeView or types a path → source preview loads.
2. User clicks "Convert" → `ConverterService` runs, converted preview shows.
3. User clicks "Save As…" → `SaveDialog` opens, user picks location + name.
4. File is written, status bar confirms.

---

## File type support

| Input          | Output | Detect by              |
|----------------|--------|------------------------|
| `.svg`         | PNG    | extension              |
| `.json`        | GIF    | extension + sniff `{}` |
| `.lottie`      | GIF    | extension              |

---

## Dependencies

- `Marius.Winter.Blazor` (project reference) — UI framework.
- `ThorVG` (transitive via Winter) — rendering engine.
- `System.IO.Compression` (BCL) — zlib for PNG encoder.
- No third-party NuGet packages.
