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

    private bool _draggingThumbV;
    private bool _draggingThumbH;
    private float _dragStart;
    private float _dragStartScroll;

    private const float ScrollBarWidth = 8f;
    private const float ScrollBarMargin = 2f;

    internal override bool ManagesOwnChildLayout => true;

    public ScrollPanel()
    {
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

    private bool ShowScrollbarV()
    {
        if (_verticalScroll == ScrollMode.Never) return false;
        if (_verticalScroll == ScrollMode.Always) return true;
        return _contentHeight > Bounds.H;
    }

    private bool ShowScrollbarH()
    {
        if (_horizontalScroll == ScrollMode.Never) return false;
        if (_horizontalScroll == ScrollMode.Always) return true;
        float availW = Bounds.W;
        if (ShowScrollbarV()) availW -= ScrollBarWidth + ScrollBarMargin;
        return _contentWidth > availW;
    }

    private bool CanScrollV() => _verticalScroll != ScrollMode.Never && _contentHeight > ViewportH();
    private bool CanScrollH() => _horizontalScroll != ScrollMode.Never && _contentWidth > ViewportW();

    /// <summary>Content area width (excluding scrollbar space if vertical scrollbar is shown).</summary>
    private float ViewportW()
    {
        float w = Bounds.W;
        if (ShowScrollbarV()) w -= ScrollBarWidth + ScrollBarMargin;
        return w;
    }

    /// <summary>Content area height (excluding scrollbar space if horizontal scrollbar is shown).</summary>
    private float ViewportH()
    {
        float h = Bounds.H;
        if (ShowScrollbarH()) h -= ScrollBarWidth + ScrollBarMargin;
        return h;
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

    protected override void OnSizeChanged()
    {
        float w = Bounds.W, h = Bounds.H;

        _backgroundShape?.ResetShape();
        _backgroundShape?.AppendRect(0, 0, w, h, Style.CornerRadius, Style.CornerRadius);

        _clipShape?.ResetShape();
        _clipShape?.AppendRect(Bounds.X, Bounds.Y, w, h, 0, 0);

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
        if (_scrollTrackV != null) { Scene.Remove(_scrollTrackV); Scene.Add(_scrollTrackV); }
        if (_scrollThumbV != null) { Scene.Remove(_scrollThumbV); Scene.Add(_scrollThumbV); }
        if (_scrollTrackH != null) { Scene.Remove(_scrollTrackH); Scene.Add(_scrollTrackH); }
        if (_scrollThumbH != null) { Scene.Remove(_scrollThumbH); Scene.Add(_scrollThumbH); }

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
        float w = Bounds.W, h = Bounds.H;
        bool showV = ShowScrollbarV();
        bool showH = ShowScrollbarH();
        float halfW = ScrollBarWidth / 2;

        // --- Vertical scrollbar ---
        _scrollTrackV?.ResetShape();
        _scrollThumbV?.ResetShape();

        if (!showV)
        {
            _scrollTrackV?.Visible(false);
            _scrollThumbV?.Visible(false);
        }
        else
        {
            _scrollTrackV?.Visible(true);

            float trackX = w - ScrollBarWidth - ScrollBarMargin;
            float trackY = ScrollBarMargin;
            float trackH = ViewportH() - 2 * ScrollBarMargin;
            if (trackH < 0) trackH = 0;

            _scrollTrackV?.AppendRect(trackX, trackY, ScrollBarWidth, trackH, halfW, halfW);

            if (CanScrollV())
            {
                _scrollThumbV?.Visible(true);
                float ratio = Math.Min(1f, ViewportH() / _contentHeight);
                float thumbH = Math.Max(20, trackH * ratio);
                float thumbY = trackY + _scrollV * (trackH - thumbH);
                _scrollThumbV?.AppendRect(trackX, thumbY, ScrollBarWidth, thumbH, halfW, halfW);
            }
            else
            {
                // Always mode but no overflow: show track only, no thumb
                _scrollThumbV?.Visible(false);
            }
        }

        // --- Horizontal scrollbar ---
        _scrollTrackH?.ResetShape();
        _scrollThumbH?.ResetShape();

        if (!showH)
        {
            _scrollTrackH?.Visible(false);
            _scrollThumbH?.Visible(false);
        }
        else
        {
            _scrollTrackH?.Visible(true);

            float trackY = h - ScrollBarWidth - ScrollBarMargin;
            float trackX = ScrollBarMargin;
            float trackW = ViewportW() - 2 * ScrollBarMargin;
            if (trackW < 0) trackW = 0;

            _scrollTrackH?.AppendRect(trackX, trackY, trackW, ScrollBarWidth, halfW, halfW);

            if (CanScrollH())
            {
                _scrollThumbH?.Visible(true);
                float ratio = Math.Min(1f, ViewportW() / _contentWidth);
                float thumbW = Math.Max(20, trackW * ratio);
                float thumbX = trackX + _scrollH * (trackW - thumbW);
                _scrollThumbH?.AppendRect(thumbX, trackY, thumbW, ScrollBarWidth, halfW, halfW);
            }
            else
            {
                _scrollThumbH?.Visible(false);
            }
        }
    }

    // --- Input handling ---

    private bool IsOverScrollbarV(float lx, float ly)
    {
        if (!ShowScrollbarV()) return false;
        return lx >= Bounds.W - ScrollBarWidth - ScrollBarMargin;
    }

    private bool IsOverScrollbarH(float lx, float ly)
    {
        if (!ShowScrollbarH()) return false;
        return ly >= Bounds.H - ScrollBarWidth - ScrollBarMargin;
    }

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
        if (!Enabled || button != 0) return false;

        WindowToLocal(x, y, out float lx, out float ly);

        // Vertical scrollbar drag
        if (CanScrollV() && IsOverScrollbarV(lx, ly))
        {
            _draggingThumbV = true;
            _dragStart = ly;
            _dragStartScroll = _scrollV;
            return true;
        }

        // Horizontal scrollbar drag
        if (CanScrollH() && IsOverScrollbarH(lx, ly))
        {
            _draggingThumbH = true;
            _dragStart = lx;
            _dragStartScroll = _scrollH;
            return true;
        }

        return false;
    }

    public override bool OnMouseUp(int button, float x, float y)
    {
        if (button != 0) return false;
        _draggingThumbV = false;
        _draggingThumbH = false;
        return true;
    }

    public override void OnMouseMove(float x, float y)
    {
        WindowToLocal(x, y, out float lx, out float ly);

        // Cursor: arrow over scrollbars, default otherwise
        if (IsOverScrollbarV(lx, ly) || IsOverScrollbarH(lx, ly))
            Cursor = CursorType.Arrow;
        else
            Cursor = CursorType.Arrow; // ScrollPanel default is arrow

        if (_draggingThumbV)
        {
            float trackH = ViewportH() - 2 * ScrollBarMargin;
            float ratio = Math.Min(1f, ViewportH() / _contentHeight);
            float thumbH = Math.Max(20, trackH * ratio);
            float scrollableTrack = trackH - thumbH;

            if (scrollableTrack > 0)
            {
                float delta = (ly - _dragStart) / scrollableTrack;
                Scroll = _dragStartScroll + delta;
            }
        }

        if (_draggingThumbH)
        {
            float trackW = ViewportW() - 2 * ScrollBarMargin;
            float ratio = Math.Min(1f, ViewportW() / _contentWidth);
            float thumbW = Math.Max(20, trackW * ratio);
            float scrollableTrack = trackW - thumbW;

            if (scrollableTrack > 0)
            {
                float delta = (lx - _dragStart) / scrollableTrack;
                ScrollHorizontal = _dragStartScroll + delta;
            }
        }
    }

    public override Element? HitTest(float x, float y)
    {
        if (!Visible || !Enabled) return null;

        float localX = x - Bounds.X;
        float localY = y - Bounds.Y;

        if (!new RectF(0, 0, Bounds.W, Bounds.H).Contains(localX, localY))
            return null;

        // Check scrollbars first
        if (ShowScrollbarV() && localX >= Bounds.W - ScrollBarWidth - ScrollBarMargin)
            return this;
        if (ShowScrollbarH() && localY >= Bounds.H - ScrollBarWidth - ScrollBarMargin)
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
        if (!_shapesCreated || _background.HasValue) return;
        var style = Style;
        _backgroundShape?.SetFill(style.Background.R8, style.Background.G8, style.Background.B8, style.Background.A8);
        _scrollThumbV?.SetFill(128, 128, 128, 100);
        _scrollThumbH?.SetFill(128, 128, 128, 100);
        MarkDirty();
    }
}
