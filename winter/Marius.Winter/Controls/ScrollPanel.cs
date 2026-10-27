using System;
using System.Numerics;
using ThorVG;
using static Glfw.GLFW;

namespace Marius.Winter;

public enum ScrollMode
{
    /// <summary>Show scrollbar only when content overflows (default).</summary>
    Auto,
    /// <summary>Always show scrollbar, even if content fits. Scrollbar is inert when not needed.</summary>
    Always,
    /// <summary>Never show scrollbar. Content is clipped at panel bounds.</summary>
    Never
}

/// <summary>
/// Scrollable container supporting vertical and/or horizontal scrolling.
/// Children are laid out using the optional Layout, and the content is
/// clipped to the visible area.
/// </summary>
public class ScrollPanel : Element, ILayoutContainer
{
    private readonly Scene _contentScene;
    private readonly Shape _contentClip;
    private float _clipWidth = float.NaN, _clipHeight = float.NaN;
    protected override Scene ChildScene => _contentScene;
    private Shape? _backgroundShape;
    private Shape? _clipShape;
    // Vertical scrollbar
    private Shape? _scrollTrackV;
    private Shape? _scrollThumbV;
    // Horizontal scrollbar
    private Shape? _scrollTrackH;
    private Shape? _scrollThumbH;

    private ILayout? _layout;
    private bool _shapesCreated;
    private Color4? _background;
    private Thickness? _padding;

    private ScrollMode _verticalScroll = ScrollMode.Auto;
    private ScrollMode _horizontalScroll = ScrollMode.Auto;

    private float _scrollV; // 0..1 normalized vertical scroll
    private float _scrollH; // 0..1 normalized horizontal scroll
    private float _contentHeight;
    private float _contentWidth;

    private readonly ScrollbarInteraction _scrollbars = new();
    internal override bool IsScrollbarGesture => _scrollbars.Pressed;

    private const float ScrollBarWidth = 8f;
    private const float ScrollBarMargin = 2f;

    internal override bool ManagesOwnChildLayout => true;

    public ScrollPanel()
    {
        _contentScene = Scene.Gen()!; Scene.Add(_contentScene);
        _contentClip = Shape.Gen()!; _contentClip.SetFill(255, 255, 255);
        _contentScene.Clip(_contentClip);
    }

    public Color4? Background
    {
        get => _background;
        set
        {
            _background = value;
            if (_backgroundShape != null && value.HasValue)
            {
                _backgroundShape.SetFill(value.Value.R8, value.Value.G8, value.Value.B8, value.Value.A8);
                _backgroundShape.Visible(true);
            }
            else if (_backgroundShape != null)
            {
                _backgroundShape.Visible(false);
            }
            MarkDirty();
        }
    }

    public ILayout? Layout
    {
        get => _layout;
        set
        {
            _scrollbars.Cancel();
            _layout = value;
            InvalidateMeasure();
        }
    }

    public ScrollMode VerticalScroll
    {
        get => _verticalScroll;
        set
        {
            if (_verticalScroll == value) return;
            _scrollbars.Cancel();
            _verticalScroll = value;
            InvalidateMeasure();
        }
    }

    public ScrollMode HorizontalScroll
    {
        get => _horizontalScroll;
        set
        {
            if (_horizontalScroll == value) return;
            _scrollbars.Cancel();
            _horizontalScroll = value;
            InvalidateMeasure();
        }
    }

    public float Scroll
    {
        get => _scrollV;
        set
        {
            float clamped = Math.Clamp(value, 0f, 1f);
            if (_scrollV == clamped) return;
            _scrollV = clamped;
            UpdateContentOffset();
            UpdateScrollbars();
            MarkDirty();
        }
    }

    public float ScrollHorizontal
    {
        get => _scrollH;
        set
        {
            float clamped = Math.Clamp(value, 0f, 1f);
            if (_scrollH == clamped) return;
            _scrollH = clamped;
            UpdateContentOffset();
            UpdateScrollbars();
            MarkDirty();
        }
    }

    // --- Scrollbar visibility logic ---

    private ScrollbarViewport ScrollViewport => ScrollbarViewport.Resolve(Bounds.W, Bounds.H, _contentWidth, _contentHeight,
        _verticalScroll != ScrollMode.Never, _horizontalScroll != ScrollMode.Never,
        _verticalScroll == ScrollMode.Always, _horizontalScroll == ScrollMode.Always);
    private bool ShowScrollbarV() => ScrollViewport.Vertical;
    private bool ShowScrollbarH() => ScrollViewport.Horizontal;
    private bool CanScrollV() => _verticalScroll != ScrollMode.Never && _contentHeight > ViewportH();
    private bool CanScrollH() => _horizontalScroll != ScrollMode.Never && _contentWidth > ViewportW();
    private float ViewportW() => ScrollViewport.Width;
    private float ViewportH() => ScrollViewport.Height;
    private ScrollbarGeometry ScrollGeometry(bool horizontal)
    {
        float viewport = horizontal ? ViewportW() : ViewportH();
        return ScrollbarGeometry.Create(horizontal, horizontal ? Bounds.H - ScrollBarWidth - ScrollBarMargin : Bounds.W - ScrollBarWidth - ScrollBarMargin,
            ScrollBarMargin, viewport - 2 * ScrollBarMargin, viewport, horizontal ? _contentWidth : _contentHeight,
            horizontal ? GetScrollOffsetH() : GetScrollOffsetV(), horizontal ? ShowScrollbarH() : ShowScrollbarV());
    }

    /// <summary>Width available for child layout.</summary>
    private float ContentLayoutWidth()
    {
        float w = Bounds.W;
        if (_verticalScroll != ScrollMode.Never) w -= ScrollBarWidth + ScrollBarMargin;
        return w;
    }

    protected override void OnAttached()
    {
        if (!_shapesCreated)
        {
            _shapesCreated = true;
            var style = Style;

            _backgroundShape = Shape.Gen();
            if (_background.HasValue)
                _backgroundShape!.SetFill(_background.Value.R8, _background.Value.G8, _background.Value.B8, _background.Value.A8);
            else
            {
                var bg = style.Background;
                _backgroundShape!.SetFill(bg.R8, bg.G8, bg.B8, bg.A8);
            }
            AddPaint(_backgroundShape);

            _clipShape = Shape.Gen();
            _clipShape!.AppendRect(Bounds.X, Bounds.Y, Bounds.W, Bounds.H, 0, 0);
            Scene.Clip(_clipShape);

            _scrollTrackV = Shape.Gen();
            _scrollTrackV!.SetFill(128, 128, 128, 40);
            Scene.Add(_scrollTrackV);

            _scrollThumbV = Shape.Gen();
            _scrollThumbV!.SetFill(128, 128, 128, 100);
            Scene.Add(_scrollThumbV);

            _scrollTrackH = Shape.Gen();
            _scrollTrackH!.SetFill(128, 128, 128, 40);
            Scene.Add(_scrollTrackH);

            _scrollThumbH = Shape.Gen();
            _scrollThumbH!.SetFill(128, 128, 128, 100);
            Scene.Add(_scrollThumbH);
        }
    }

    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        // Reserve scrollbar space when vertical scroll is possible
        bool mayShowV = _verticalScroll != ScrollMode.Never;
        float scrollReserve = mayShowV ? ScrollBarWidth + ScrollBarMargin : 0;
        float measureW = availableWidth - scrollReserve;
        float measureH = _verticalScroll == ScrollMode.Never
            ? availableHeight
            : float.PositiveInfinity;

        if (_layout != null)
        {
            var contentSize = _layout.Measure(this, measureW, measureH);
            _contentHeight = contentSize.Y;
            _contentWidth = contentSize.X;
        }
        else
        {
            _contentHeight = 0;
            _contentWidth = 0;
            foreach (var child in Children)
            {
                if (!child.Visible) continue;
                var childSize = child.Measure(measureW, measureH);
                _contentHeight += childSize.Y;
                _contentWidth = MathF.Max(_contentWidth, childSize.X);
            }
        }

        float w = float.IsFinite(availableWidth) ? availableWidth : Bounds.W;
        return new Vector2(w, 0);
    }

    protected override void ArrangeCore(RectF finalBounds)
    {
        if (_layout != null)
        {
            bool mayShowV = _verticalScroll != ScrollMode.Never;
            float scrollReserve = mayShowV ? ScrollBarWidth + ScrollBarMargin : 0;

            float contentW = finalBounds.W - scrollReserve;
            float measureH = _verticalScroll == ScrollMode.Never ? finalBounds.H : float.PositiveInfinity;

            var contentSize = _layout.Measure(this, contentW, measureH);
            _contentHeight = contentSize.Y;
            _contentWidth = contentSize.X;

            float arrangeW = MathF.Max(contentW, _contentWidth);
            float arrangeH = MathF.Max(finalBounds.H, _contentHeight);

            float scrollOffsetY = GetScrollOffsetV();
            float scrollOffsetX = GetScrollOffsetH();
            _layout.Arrange(this, new RectF(-scrollOffsetX, -scrollOffsetY, arrangeW, arrangeH));

            UpdateScrollbars();
        }
    }

    protected override void OnPositionChanged()
    {
        // ThorVG applies a clip in the incoming parent's coordinate space.
        _clipShape?.ResetShape();
        _clipShape?.AppendRect(Bounds.X, Bounds.Y, Bounds.W, Bounds.H, 0, 0);
    }

    protected override void OnSizeChanged()
    {
        _scrollbars.Cancel();
        float w = Bounds.W, h = Bounds.H;

        _backgroundShape?.ResetShape();
        _backgroundShape?.AppendRect(0, 0, w, h, Style.CornerRadius, Style.CornerRadius);

        _clipShape?.ResetShape();
        _clipShape?.AppendRect(Bounds.X, Bounds.Y, w, h, 0, 0);
        // The viewport can reveal children whose geometry stayed identical.
        // Clip refreshes their raster geometry; Transform also prepares fills
        // skipped while clipped out, without changing the retained transforms.
        Scene.pImpl.Mark(RenderUpdateFlag.Clip | RenderUpdateFlag.Transform);

        if (_layout != null)
        {
            float contentW = ContentLayoutWidth();
            float measureH = _verticalScroll == ScrollMode.Never ? h : float.PositiveInfinity;

            var contentSize = _layout.Measure(this, contentW, measureH);
            _contentHeight = contentSize.Y;
            _contentWidth = contentSize.X;

            float arrangeW = MathF.Max(contentW, _contentWidth);
            float arrangeH = MathF.Max(h, _contentHeight);

            float scrollOffsetY = GetScrollOffsetV();
            float scrollOffsetX = GetScrollOffsetH();
            _layout.Arrange(this, new RectF(-scrollOffsetX, -scrollOffsetY, arrangeW, arrangeH));
        }

        // Ensure scrollbars render on top of children
        if (_scrollTrackV != null) BringPaintToFront(_scrollTrackV);
        if (_scrollThumbV != null) BringPaintToFront(_scrollThumbV);
        if (_scrollTrackH != null) BringPaintToFront(_scrollTrackH);
        if (_scrollThumbH != null) BringPaintToFront(_scrollThumbH);

        UpdateScrollbars();
    }

    private float GetScrollOffsetV()
    {
        float visibleH = ViewportH();
        if (_contentHeight <= visibleH) return 0;
        return _scrollV * (_contentHeight - visibleH);
    }

    private float GetScrollOffsetH()
    {
        float visibleW = ViewportW();
        if (_contentWidth <= visibleW) return 0;
        return _scrollH * (_contentWidth - visibleW);
    }

    private void UpdateContentOffset()
    {
        if (_layout == null) return;

        float contentW = ContentLayoutWidth();
        float arrangeW = MathF.Max(contentW, _contentWidth);
        float arrangeH = MathF.Max(Bounds.H, _contentHeight);
        float scrollOffsetY = GetScrollOffsetV();
        float scrollOffsetX = GetScrollOffsetH();
        _layout.Arrange(this, new RectF(-scrollOffsetX, -scrollOffsetY, arrangeW, arrangeH));
    }

    private void UpdateScrollbars()
    {
        float width = ViewportW(), height = ViewportH();
        if (width != _clipWidth || height != _clipHeight)
        {
            _clipWidth = width; _clipHeight = height;
            _contentClip.ResetShape(); _contentClip.AppendRect(0, 0, width, height, 0, 0);
            _contentScene.pImpl.Mark(RenderUpdateFlag.Clip | RenderUpdateFlag.Transform);
        }
        if (!CanScrollV()) _scrollV = 0;
        if (!CanScrollH()) _scrollH = 0;
        ScrollGeometry(false).Paint(this, _scrollTrackV, _scrollThumbV, _scrollbars.Hovered(false), _scrollbars.Axis == 1);
        ScrollGeometry(true).Paint(this, _scrollTrackH, _scrollThumbH, _scrollbars.Hovered(true), _scrollbars.Axis == 2);
    }

    // --- Input handling ---

    private bool IsOverScrollbarV(float lx, float ly) => ScrollGeometry(false).Contains(lx, ly);

    private bool IsOverScrollbarH(float lx, float ly) => ScrollGeometry(true).Contains(lx, ly);

    public override void OnScroll(float dx, float dy)
    {
        // Shift+scroll → horizontal
        var gw = OwnerWindow?.GlfwWindow;
        bool shift = gw != null
            && (Glfw.Glfw.glfwGetKey(gw, GLFW_KEY_LEFT_SHIFT) == GLFW_PRESS
             || Glfw.Glfw.glfwGetKey(gw, GLFW_KEY_RIGHT_SHIFT) == GLFW_PRESS);
        if (shift)
        {
            dx = dy;
            dy = 0;
        }

        bool consumed = false;

        if (dy != 0 && CanScrollV())
        {
            float step = ViewportH() * 0.25f / _contentHeight;
            Scroll = _scrollV - dy * step;
            consumed = true;
        }

        if (dx != 0 && CanScrollH())
        {
            float step = ViewportW() * 0.25f / _contentWidth;
            ScrollHorizontal = _scrollH - dx * step;
            consumed = true;
        }

        if (!consumed)
            base.OnScroll(dx, dy);
    }

    public override bool OnMouseDown(int button, float x, float y)
    {
        if (!IsActive || button != 0) return false;
        WindowToLocal(x, y, out float lx, out float ly);
        _scrollbars.Cancel();
        var vertical = ScrollGeometry(false); var horizontal = ScrollGeometry(true);
        if (vertical.Contains(lx, ly) || horizontal.Contains(lx, ly))
        {
            var geometry = vertical.Contains(lx, ly) ? vertical : horizontal;
            float value = _scrollbars.Begin(this, geometry, lx, ly);
            SetScrollbarOffset(geometry, value); Cursor = CursorType.Arrow; UpdateScrollbars(); MarkDirty();
            return true;
        }
        return (ShowScrollbarV() && lx >= ViewportW()) || (ShowScrollbarH() && ly >= ViewportH());
    }
    private void SetScrollbarOffset(ScrollbarGeometry geometry, float value)
    {
        float normalized = geometry.Maximum > 0 ? value / geometry.Maximum : 0;
        if (geometry.Horizontal) ScrollHorizontal = normalized; else Scroll = normalized;
    }

    public override bool OnMouseUp(int button, float x, float y)
    {
        if (button != 0) return false;
        OnDragEnd(); return true;
    }
    public override void OnDragEnd() { _scrollbars.Cancel(); UpdateScrollbars(); MarkDirty(); }
    protected override void OnActivityChanged(bool active) { if (!active) _scrollbars.Cancel(); UpdateScrollbars(); MarkDirty(); }
    public override void OnMouseLeave() { _scrollbars.Leave(); UpdateScrollbars(); MarkDirty(); }

    public override void OnMouseMove(float x, float y)
    {
        WindowToLocal(x, y, out float lx, out float ly);
        var vertical = ScrollGeometry(false); var horizontal = ScrollGeometry(true);
        Cursor = CursorType.Arrow;
        if (_scrollbars.Hover(lx, ly, vertical, horizontal)) { UpdateScrollbars(); MarkDirty(); }
        int axis = _scrollbars.Axis;
        if (_scrollbars.Move(this, vertical, horizontal, lx, ly, out var value))
            SetScrollbarOffset(axis == 2 ? horizontal : vertical, value);
    }

    public override Element? HitTest(float x, float y)
    {
        if (!Visible || !Enabled) return null;

        float localX = x - Bounds.X;
        float localY = y - Bounds.Y;

        if (!new RectF(0, 0, Bounds.W, Bounds.H).Contains(localX, localY))
            return null;

        // Check scrollbars first
        if (ShowScrollbarV() && localX >= ViewportW() - ScrollBarMargin)
            return this;
        if (ShowScrollbarH() && localY >= ViewportH() - ScrollBarMargin)
            return this;

        // Check children
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            var hit = Children[i].HitTest(localX, localY);
            if (hit != null) return hit;
        }

        return this;
    }

    public Thickness? Padding
    {
        get => _padding;
        set
        {
            _padding = value;
            InvalidateMeasure();
        }
    }

    protected override Style GetDefaultStyle()
    {
        var s = OwnerWindow?.Theme.Panel ?? new Style();
        if (_padding.HasValue)
        {
            s = s.Clone();
            s.Padding = _padding.Value;
        }
        return s;
    }

    protected override void OnThemeChanged()
    {
        _scrollbars.Cancel();
        UpdateScrollbars();
        if (!_shapesCreated || _background.HasValue) return;
        var style = Style;
        _backgroundShape?.SetFill(style.Background.R8, style.Background.G8, style.Background.B8, style.Background.A8);

        MarkDirty();
    }
}
