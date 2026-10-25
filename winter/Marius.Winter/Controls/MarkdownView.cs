using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ThorVG;
using static Glfw.GLFW;

namespace Marius.Winter;

/// <summary>Encoded native image bytes returned by an explicit Markdown image resolver.</summary>
public sealed record MarkdownImage(byte[] Bytes, string MimeType = "png");

/// <summary>A read-only native Markdown viewer. Links are callbacks; images require an explicit resolver.</summary>
public class MarkdownView : Element
{
    private const float Padding = 12, Bar = 10;
    private string _markdown = "";
    private float? _fontSize;
    private Func<string, MarkdownImage?>? _resolver;
    private MarkdownContent? _content;
    private MarkdownLayout? _layout;
    private float _layoutWidth = float.NaN;
    private readonly Dictionary<MarkdownFont, NativeFont> _fonts = new();
    private readonly Dictionary<string, Picture?> _images = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector2> _imageSizes = new(StringComparer.Ordinal);
    private readonly List<(string Url, List<RectF> Bounds)> _links = new();
    private Scene? _viewport, _drawings;
    private Shape? _clip, _background, _focus, _outline, _vTrack, _vThumb, _hTrack, _hThumb;
    private bool _ready, _batch, _imagesResolved, _detached;
    private float _scrollX, _scrollY;
    private int _focusedLink = -1, _pressedLink = -1;
    private int _dragAxis;
    private float _dragStart, _dragScroll;
    private bool _selectedAll;

    public MarkdownView(string markdown = "") { Markdown = markdown; Focusable = true; }

    /// <summary>CommonMark with strikethrough, GFM pipe tables, task lists and autolinks. HTML is literal text.</summary>
    public string Markdown
    {
        get => _markdown;
        set
        {
            ArgumentNullException.ThrowIfNull(value); ThrowIfDisposed();
            if (_markdown == value) return;
            _markdown = value; _content = null; ClearImages();
            _focusedLink = _pressedLink = -1; _selectedAll = false; _scrollX = _scrollY = 0;
            InvalidateLayout();
        }
    }

    /// <summary>Logical font size. The default follows Theme.Label.FontSize.</summary>
    public float FontSize
    {
        get => _fontSize ?? Style.FontSize;
        set
        {
            ThrowIfDisposed();
            if (!float.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (_fontSize == value) return;
            _fontSize = value; ClearFonts(); InvalidateLayout();
        }
    }
    public void ResetFontSize()
    { ThrowIfDisposed(); if (_fontSize == null) return; _fontSize = null; ClearFonts(); InvalidateLayout(); }

    /// <summary>Explicit synchronous resolver on the UI owner. Null disables images. No networking or file access is implicit.
    /// Return PNG/JPEG/WebP/SVG bytes accepted by Picture.Load, or null for an alt-text fallback.
    /// Results (including null) are cached per source/resolver until ReloadImages.</summary>
    public Func<string, MarkdownImage?>? ImageResolver
    {
        get => _resolver;
        set { ThrowIfDisposed(); if (_resolver == value) return; _resolver = value; ClearImages(); InvalidateLayout(); }
    }
    public event Action<string>? LinkClicked;
    public Vector2 ContentSize { get { EnsureLayout(); return new(_layout!.Width, _layout.Height); } }
    public Vector2 ScrollOffset => new(_scrollX, _scrollY);
    public void Focus() => OwnerWindow?.SetFocus(this);

    /// <summary>Scroll in logical document pixels. Code and wide tables retain horizontal overflow.</summary>
    public void ScrollTo(float x, float y)
    {
        ThrowIfDisposed(); EnsureLayout();
        _scrollX = float.IsFinite(x) ? x : 0; _scrollY = float.IsFinite(y) ? y : 0;
        ClampScroll(); Render();
    }
    public void ReloadImages() { ThrowIfDisposed(); ClearImages(); InvalidateLayout(); }

    internal void Configure(string markdown, float? fontSize, Func<string, MarkdownImage?>? resolver)
    {
        _batch = true;
        try { Markdown = markdown; if (fontSize.HasValue) FontSize = fontSize.Value; else ResetFontSize(); ImageResolver = resolver; }
        finally { _batch = false; }
        if (_ready) { EnsureLayout(); Render(); }
    }

    private void InvalidateLayout()
    {
        _layout = null; _layoutWidth = float.NaN; InvalidateMeasure();
        if (_ready && !_batch) { EnsureLayout(); Render(); }
    }

    private void EnsureLayout(float? requestedWidth = null)
    {
        ThrowIfDisposed();
        float width = requestedWidth ?? Math.Max(1, Bounds.W - Padding * 2 - Bar);
        if (_layout != null && _layoutWidth == width) return;
        _content ??= MarkdownContent.Parse(_markdown);
        if (_ready && !_imagesResolved) ResolveImages();
        _layout = MarkdownLayout.Build(_content, width, FontSize, MeasureText, _imageSizes);
        _layoutWidth = width;
        _links.Clear();
        var occurrences = new Dictionary<object, int>();
        foreach (var drawing in _layout.Drawings)
        {
            if (drawing.Span?.Link is not { } url || drawing.Span.LinkIdentity is not { } identity) continue;
            if (!occurrences.TryGetValue(identity, out var index))
            { occurrences[identity] = index = _links.Count; _links.Add((url, new())); }
            _links[index].Bounds.Add(drawing.Bounds);
        }
        _focusedLink = _links.Count == 0 ? -1 : Math.Clamp(_focusedLink, 0, _links.Count - 1);
        ClampScroll();
    }

    private static IEnumerable<MarkdownSpan> Spans(IEnumerable<MarkdownBlock> blocks)
    {
        foreach (var block in blocks)
        { foreach (var span in block.Spans) yield return span; foreach (var span in Spans(block.Children)) yield return span; }
    }

    private void ResolveImages()
    {
        _imagesResolved = true;
        if (_resolver == null) return;
        foreach (var url in Spans(_content!.Blocks).Where(s => s.Image != null).Select(s => s.Image!).Distinct(StringComparer.Ordinal))
        {
            var source = _resolver(url);
            Picture? picture = null;
            if (source != null && source.Bytes.Length > 0)
            {
                picture = Picture.Gen();
                if (picture.Load(source.Bytes, (uint)source.Bytes.Length, source.MimeType, null, true) != Result.Success ||
                    picture.GetSize(out var w, out var h) != Result.Success || w <= 0 || h <= 0)
                { picture.Unref(); picture = null; }
                else { picture.Ref(); _imageSizes[url] = new(w, h); }
            }
            _images[url] = picture;
        }
    }

    private void ClearImages()
    {
        _drawings?.Remove();
        foreach (var picture in _images.Values) picture?.Unref();
        _images.Clear(); _imageSizes.Clear(); _imagesResolved = false;
    }

    private sealed class NativeFont
    {
        public readonly Text Measure;
        public readonly string Name;
        public readonly float ActualSize, Ascent, Descent;
        public NativeFont(MarkdownFont font, string regular)
        {
            Name = font.Code ? "monospace" : font.Format.HasFlag(TextFormat.Bold) ? "default-bold" : regular;
            Measure = Text.Gen()!;
            Measure.SetFont(Name); Measure.SetFontSize(font.Size); Measure.SetText("M"); Measure.GetMetrics(out var metrics);
            float height = metrics.ascent - metrics.descent;
            ActualSize = height > 0 ? font.Size * font.Size / height : font.Size;
            Measure.SetFontSize(ActualSize); Measure.GetMetrics(out metrics);
            Ascent = metrics.ascent; Descent = -metrics.descent;
        }
    }

    private NativeFont Font(MarkdownFont font)
    {
        if (!_fonts.TryGetValue(font, out var native)) _fonts[font] = native = new(font, Style.FontName);
        return native;
    }
    private MarkdownTextMetrics MeasureText(string text, MarkdownFont font)
    {
        // Detached headless composition can measure before a Window loads fonts.
        // Attachment invalidates this provisional layout before the first frame.
        if (!_ready) return new(text.Length * font.Size * .55f, font.Size * .8f, font.Size * .2f);
        var native = Font(font); native.Measure.SetText(text);
        native.Measure.GetTextSize(out var width, out _);
        return new(width, native.Ascent, native.Descent);
    }
    private void ClearFonts() { foreach (var font in _fonts.Values) font.Measure.Unref(); _fonts.Clear(); }

    protected override void OnAttached()
    {
        _detached = false;
        if (OwnerWindow == null) return;
        if (_viewport == null)
        {
            _background = Shape.Gen()!; AddPaint(_background);
            _viewport = Scene.Gen()!; AddPaint(_viewport);
            _clip = Shape.Gen()!; _clip.SetFill(255, 255, 255); _viewport.Clip(_clip);
            _drawings = Scene.Gen()!; _viewport.Add(_drawings);
            _focus = Shape.Gen()!; _viewport.Add(_focus);
            _outline = Shape.Gen()!; AddPaint(_outline);
            _vTrack = Shape.Gen()!; AddPaint(_vTrack); _vThumb = Shape.Gen()!; AddPaint(_vThumb);
            _hTrack = Shape.Gen()!; AddPaint(_hTrack); _hThumb = Shape.Gen()!; AddPaint(_hThumb);
        }
        _ready = true; InvalidateLayout();
    }

    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        float width = float.IsFinite(availableWidth) ? Math.Max(40, availableWidth) : 480;
        EnsureLayout(Math.Max(1, width - Padding * 2 - Bar));
        return new(width, Math.Min(float.IsFinite(availableHeight) ? availableHeight : 360, _layout!.Height + Padding * 2 + Bar));
    }
    protected override void OnSizeChanged() { if (_ready) { EnsureLayout(); Render(); } }
    protected override void ArrangeCore(RectF finalBounds) { if (_ready) { EnsureLayout(); Render(); } }
    protected override void OnThemeChanged() { ClearFonts(); InvalidateLayout(); }
    protected override void OnDetaching()
    {
        _ready = false; _detached = true; _drawings?.Remove(); ClearImages(); ClearFonts();
        _layout = null; _pressedLink = -1; _dragAxis = 0;
    }
    protected override void OnDisposed()
    {
        ClearImages(); ClearFonts(); _content = null; _layout = null; _links.Clear();
        _markdown = ""; _resolver = null; LinkClicked = null;
    }

    private float ViewWidth => Math.Max(1, Bounds.W - Padding * 2 - Bar);
    private float ViewHeight => Math.Max(1, Bounds.H - Padding * 2 - Bar);
    private void ClampScroll()
    {
        _scrollX = Math.Clamp(_scrollX, 0, Math.Max(0, (_layout?.Width ?? 0) - ViewWidth));
        _scrollY = Math.Clamp(_scrollY, 0, Math.Max(0, (_layout?.Height ?? 0) - ViewHeight));
    }

    private static void Fill(Shape shape, Color4 color) => shape.SetFill(color.R8, color.G8, color.B8, color.A8);
    private static void Rect(Shape shape, RectF bounds, Color4 color)
    { shape.ResetShape(); shape.AppendRect(bounds.X, bounds.Y, Math.Max(0, bounds.W), Math.Max(0, bounds.H), 0, 0); Fill(shape, color); }

    private void Render()
    {
        if (!_ready || _drawings == null || _layout == null || IsDisposed) return;
        var theme = OwnerWindow!.Theme;
        bool dark = theme.WindowBackground.R < .5f;
        var muted = dark ? new Color4(.19f, .21f, .25f) : new Color4(.94f, .95f, .97f);
        var border = dark ? new Color4(.36f, .4f, .46f) : new Color4(.72f, .76f, .82f);
        var link = dark ? new Color4(.42f, .72f, 1) : new Color4(.08f, .35f, .73f);
        Rect(_background!, new(0, 0, Bounds.W, Bounds.H), theme.WindowBackground);
        _clip!.ResetShape(); _clip.AppendRect(Padding, Padding, ViewWidth, ViewHeight, 0, 0);
        _drawings.Remove();
        foreach (var drawing in _layout.Drawings)
        {
            var b = drawing.Bounds;
            if (b.Y + b.H < _scrollY || b.Y > _scrollY + ViewHeight || b.X + b.W < _scrollX || b.X > _scrollX + ViewWidth) continue;
            float x = Padding + b.X - _scrollX, y = Padding + b.Y - _scrollY;
            RectF rect = new(x, y, b.W, b.H);
            Color4 color = drawing.Span?.Link != null ? link : IsEffectivelyEnabled ? Style.Foreground : Style.ForegroundDisabled;
            void Box(RectF box, Color4 fill) { var shape = Shape.Gen()!; Rect(shape, box, fill); _drawings.Add(shape); }
            switch (drawing.Kind)
            {
                case MarkdownDrawingKind.CodeBackground: Box(rect, muted); break;
                case MarkdownDrawingKind.Cell:
                    Box(rect, drawing.Header ? muted : theme.WindowBackground);
                    var cell = Shape.Gen()!; Rect(cell, rect, new Color4(0, 0, 0, 0)); cell.StrokeWidth(1); cell.StrokeFill(border.R8, border.G8, border.B8); _drawings.Add(cell); break;
                case MarkdownDrawingKind.QuoteBar:
                case MarkdownDrawingKind.Rule: Box(rect, border); break;
                case MarkdownDrawingKind.Task:
                    var task = Shape.Gen()!; Rect(task, new(x, y + (b.H - drawing.Font.Size) / 2, drawing.Font.Size, drawing.Font.Size), drawing.Span!.Task == true ? link : muted);
                    task.StrokeWidth(1); task.StrokeFill(border.R8, border.G8, border.B8); _drawings.Add(task);
                    if (drawing.Span.Task == true)
                    {
                        var check = Shape.Gen()!; check.MoveTo(x + 3, y + b.H / 2); check.LineTo(x + 6, y + b.H / 2 + 3); check.LineTo(x + drawing.Font.Size - 3, y + b.H / 2 - 4);
                        check.StrokeWidth(2); check.StrokeFill(255, 255, 255); _drawings.Add(check);
                    }
                    break;
                case MarkdownDrawingKind.Image:
                    if (_images.GetValueOrDefault(drawing.Span!.Image!) is { } source)
                    { var image = (Picture)source.Duplicate(); image.SetSize(b.W, b.H); image.Translate(x, y); _drawings.Add(image); }
                    break;
                case MarkdownDrawingKind.Text:
                    if (_selectedAll) Box(rect, theme.SelectionColor);
                    else if (drawing.Font.Code)
                        Box(rect, muted);
                    var font = Font(drawing.Font);
                    var text = Text.Gen()!; text.SetFont(font.Name); text.SetFontSize(font.ActualSize);
                    text.SetItalic(drawing.Font.Format.HasFlag(TextFormat.Italic) ? .18f : 0);
                    text.SetText(drawing.Text); text.SetFill(color.R8, color.G8, color.B8); text.Opacity(color.A8);
                    text.Translate(x, Padding + drawing.Baseline - _scrollY - font.Ascent); _drawings.Add(text);
                    if (drawing.Span?.Link != null || drawing.Font.Format.HasFlag(TextFormat.Underline)) Box(new(x, Padding + drawing.Baseline - _scrollY + 2, b.W, 1), color);
                    if (drawing.Font.Format.HasFlag(TextFormat.Strikethrough)) Box(new(x, Padding + drawing.Baseline - _scrollY - font.Ascent * .35f, b.W, 1), color);
                    break;
            }
        }
        _focus!.ResetShape(); _outline!.ResetShape();
        if (State.HasFlag(ElementState.Focused))
        {
            _outline.AppendRect(1, 1, Math.Max(0, Bounds.W - 2), Math.Max(0, Bounds.H - 2), 0, 0);
            if (_focusedLink >= 0)
                foreach (var b in _links[_focusedLink].Bounds)
                {
                    float y = Padding + b.Y - _scrollY;
                    _focus.AppendRect(Padding + b.X - _scrollX, y, b.W, b.H, 0, 0);
                }
        }
        _focus.SetFill(0, 0, 0, 0); _focus.StrokeWidth(1); _focus.StrokeFill(link.R8, link.G8, link.B8);
        _outline.SetFill(0, 0, 0, 0); _outline.StrokeWidth(1); _outline.StrokeFill(link.R8, link.G8, link.B8);
        Scrollbar(_vTrack!, _vThumb!, false, border); Scrollbar(_hTrack!, _hThumb!, true, border);
        MarkDirty();
    }

    private void Scrollbar(Shape track, Shape thumb, bool horizontal, Color4 color)
    {
        float viewport = horizontal ? ViewWidth : ViewHeight, total = horizontal ? _layout!.Width : _layout!.Height;
        bool visible = total > viewport; track.Visible(visible); thumb.Visible(visible); if (!visible) return;
        float length = Math.Max(20, viewport * viewport / total), scroll = horizontal ? _scrollX : _scrollY;
        float offset = scroll / (total - viewport) * (viewport - length);
        Rect(track, horizontal ? new(Padding, Bounds.H - Bar, viewport, 6) : new(Bounds.W - Bar, Padding, 6, viewport), new Color4(color.R, color.G, color.B, .2f));
        Rect(thumb, horizontal ? new(Padding + offset, Bounds.H - Bar, length, 6) : new(Bounds.W - Bar, Padding + offset, 6, length), color);
    }

    private int LinkAt(float x, float y)
    {
        WindowToLocal(x, y, out var lx, out var ly);
        if (lx < Padding || ly < Padding || lx > Padding + ViewWidth || ly > Padding + ViewHeight) return -1;
        for (int i = 0; i < _links.Count; i++)
            if (_links[i].Bounds.Any(b => b.Contains(lx - Padding + _scrollX, ly - Padding + _scrollY))) return i;
        return -1;
    }
    public override bool OnMouseDown(int button, float x, float y)
    {
        if (button != GLFW_MOUSE_BUTTON_LEFT || IsDisposed || _detached || !IsEffectivelyEnabled || !IsEffectivelyVisible) return false;
        EnsureLayout(); WindowToLocal(x, y, out var lx, out var ly);
        if (lx < 0 || ly < 0 || lx > Bounds.W || ly > Bounds.H) return false;
        Focus(); _selectedAll = false;
        if (lx >= Bounds.W - Bar && _layout!.Height > ViewHeight) _dragAxis = 1;
        else if (ly >= Bounds.H - Bar && _layout!.Width > ViewWidth) _dragAxis = 2;
        if (_dragAxis != 0)
        { _dragStart = _dragAxis == 1 ? y : x; _dragScroll = _dragAxis == 1 ? _scrollY : _scrollX; return true; }
        _pressedLink = LinkAt(x, y); if (_pressedLink >= 0) _focusedLink = _pressedLink;
        Render(); return true;
    }
    public override bool OnMouseUp(int button, float x, float y)
    {
        if (button != GLFW_MOUSE_BUTTON_LEFT || IsDisposed || _detached || !IsEffectivelyEnabled || !IsEffectivelyVisible) return false;
        _dragAxis = 0; int pressed = _pressedLink; _pressedLink = -1;
        if (pressed >= 0 && pressed < _links.Count && pressed == LinkAt(x, y)) LinkClicked?.Invoke(_links[pressed].Url);
        return true;
    }
    public override void OnMouseMove(float x, float y)
    {
        if (IsDisposed) return;
        if (_dragAxis != 0)
        {
            float viewport = _dragAxis == 1 ? ViewHeight : ViewWidth, total = _dragAxis == 1 ? _layout!.Height : _layout!.Width;
            float length = Math.Max(20, viewport * viewport / total);
            float value = _dragScroll + ((_dragAxis == 1 ? y : x) - _dragStart) * (total - viewport) / Math.Max(1, viewport - length);
            ScrollTo(_dragAxis == 2 ? value : _scrollX, _dragAxis == 1 ? value : _scrollY);
        }
        Cursor = LinkAt(x, y) >= 0 ? CursorType.Hand : CursorType.Arrow;
    }
    public override void OnScroll(float dx, float dy)
    {
        if (IsDisposed || _detached || !IsEffectivelyEnabled || !IsEffectivelyVisible) return;
        var window = OwnerWindow?.GlfwWindow;
        if (window != null && (Glfw.Glfw.glfwGetKey(window, GLFW_KEY_LEFT_SHIFT) == GLFW_PRESS || Glfw.Glfw.glfwGetKey(window, GLFW_KEY_RIGHT_SHIFT) == GLFW_PRESS))
        { ScrollTo(_scrollX - dy * FontSize * 3, _scrollY); return; }
        ScrollTo(_scrollX - dx * FontSize * 3, _scrollY - dy * FontSize * 3);
    }
    public override void OnFocus() { if (IsDisposed) return; EnsureLayout(); State |= ElementState.Focused; if (_links.Count > 0 && _focusedLink < 0) _focusedLink = 0; Render(); }
    public override void OnBlur() { State &= ~ElementState.Focused; _pressedLink = -1; _dragAxis = 0; Render(); }
    public override void OnKeyDown(int key, int mods, bool repeat)
    {
        if (IsDisposed || _detached || !IsEffectivelyEnabled || !IsEffectivelyVisible) return;
        EnsureLayout(); bool control = (mods & GLFW_MOD_CONTROL) != 0;
        if (control && key == GLFW_KEY_A) { _selectedAll = true; Render(); return; }
        if (control && key == GLFW_KEY_C) { CopySelection(); return; }
        if ((key == GLFW_KEY_ENTER || key == GLFW_KEY_KP_ENTER || key == GLFW_KEY_SPACE) && _focusedLink >= 0)
        { if (!repeat) LinkClicked?.Invoke(_links[_focusedLink].Url); return; }
        int next = _focusedLink;
        if (key == GLFW_KEY_RIGHT) next++;
        else if (key == GLFW_KEY_LEFT) next--;
        else if (control && key == GLFW_KEY_HOME) next = 0;
        else if (control && key == GLFW_KEY_END) next = _links.Count - 1;
        else if (key == GLFW_KEY_DOWN) { ScrollTo(_scrollX, _scrollY + FontSize * 3); return; }
        else if (key == GLFW_KEY_UP) { ScrollTo(_scrollX, _scrollY - FontSize * 3); return; }
        else if (key == GLFW_KEY_PAGE_DOWN) { ScrollTo(_scrollX, _scrollY + ViewHeight * .9f); return; }
        else if (key == GLFW_KEY_PAGE_UP) { ScrollTo(_scrollX, _scrollY - ViewHeight * .9f); return; }
        else if (key == GLFW_KEY_HOME) { ScrollTo(0, 0); return; }
        else if (key == GLFW_KEY_END) { ScrollTo(_scrollX, _layout!.Height); return; }
        else { base.OnKeyDown(key, mods, repeat); return; }
        if (_links.Count > 0)
        {
            _focusedLink = Math.Clamp(next, 0, _links.Count - 1);
            var bounds = _links[_focusedLink].Bounds[0];
            ScrollTo(bounds.X < _scrollX ? bounds.X : bounds.Right > _scrollX + ViewWidth ? bounds.Right - ViewWidth : _scrollX,
                bounds.Y < _scrollY ? bounds.Y : bounds.Bottom > _scrollY + ViewHeight ? bounds.Bottom - ViewHeight : _scrollY);
        }
    }
    protected override void OnActivityChanged(bool active) { if (!active) { _pressedLink = -1; _dragAxis = 0; } }
    public override void OnDragEnd() { _pressedLink = -1; _dragAxis = 0; }
    private void CopySelection() { if (_selectedAll && OwnerWindow != null) Glfw.Glfw.glfwSetClipboardString(null, _markdown); }
    public override void OnBuildContextMenu(Menu menu)
    {
        menu.AddItem("Select all Markdown", () => { _selectedAll = true; Focus(); Render(); });
        menu.AddItem("Copy Markdown source", () => { _selectedAll = true; CopySelection(); });
    }
}
