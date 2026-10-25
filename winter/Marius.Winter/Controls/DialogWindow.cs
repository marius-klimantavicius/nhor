using System;
using System.Numerics;
using ThorVG;

namespace Marius.Winter;

/// <summary>
/// Draggable window panel with a title bar. Can contain child elements.
/// Nanogui-style rendering with header gradient and drop shadow.
/// </summary>
public class DialogWindow : Element, ILayoutContainer
{
    private DialogFrame? _frame;
    private Scene? _titleScene;
    private Text? _titleText;
    private Shape? _closeShape;
    private ILayout? _layout;
    private bool _shapesCreated;
    private string _title;
    private bool _dragging;
    private float _dragOffsetX, _dragOffsetY;
    private bool _resizing;
    private int _resizeEdge; // bitmask: 1=right, 2=bottom, 4=left, 8=top
    private const float ResizeGrip = 8f;
    private const float CloseButtonSize = 14f;
    private const float CloseButtonMargin = 8f;
    private const float MinDialogW = 200f;
    private const float MinDialogH = 100f;

    internal override bool ManagesOwnChildLayout => true;

    public const float HeaderHeight = 30f;

    public Action? CloseClicked;

    public DialogWindow(string title = "Window")
    {
        _title = title;
        Bounds = new RectF(100, 60, 500, 400);
    }

    public string Title
    {
        get => _title;
        set
        {
            if (_title == value) return;
            _title = value;
            _titleText?.SetText(value);
            CenterTitle();
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

    protected override void OnAttached()
    {
        if (!_shapesCreated)
        {
            _shapesCreated = true;
            var style = Style;
            var theme = OwnerWindow?.Theme ?? Theme.Dark;

            _frame = new DialogFrame(AddPaint);
            _frame.ApplyTheme(theme, HeaderHeight);

            // Title
            _titleText = ThorVG.Text.Gen();
            _titleText!.SetFont(style.FontName);
            _titleText.SetFontSize(EffectiveFontSize(style.FontSize));
            _titleText.SetText(_title);
            _titleText.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);

            _titleScene = Scene.Gen()!;
            _titleScene.Add(_titleText);
            AddPaint(_titleScene);

            // Close button "x"
            _closeShape = Shape.Gen();
            _closeShape!.StrokeWidth(1.5f);
            _closeShape.StrokeFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8, theme.TextColor.A8);
            _closeShape.StrokeCap(StrokeCap.Round);
            _closeShape.SetFill(0, 0, 0, 0);
            AddPaint(_closeShape);
        }
    }

    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        return new Vector2(Bounds.W, Bounds.H);
    }

    /// <summary>
    /// DialogWindow manages its own position and size. Ignore the parent layout's
    /// final bounds — only re-arrange internal content using our current Bounds.
    /// </summary>
    internal override bool IgnoresParentArrange => true;

    private const float ContentPadding = 8f;

    protected override void ArrangeCore(RectF finalBounds)
    {
        if (_layout != null)
        {
            float contentW = finalBounds.W - ContentPadding * 2;
            float contentH = finalBounds.H - HeaderHeight - ContentPadding * 2;
            _layout.Measure(this, contentW, contentH);
            _layout.Arrange(this, new RectF(ContentPadding, HeaderHeight + ContentPadding, contentW, contentH));
        }
    }

    protected override void OnSizeChanged()
    {
        float w = Bounds.W, h = Bounds.H;
        _frame?.Arrange(w, h, HeaderHeight);

        CenterTitle();
        UpdateCloseButton();

        // Arrange content
        if (_layout != null)
        {
            float contentW = w - ContentPadding * 2;
            float contentH = h - HeaderHeight - ContentPadding * 2;
            _layout.Measure(this, contentW, contentH);
            _layout.Arrange(this, new RectF(ContentPadding, HeaderHeight + ContentPadding, contentW, contentH));
        }
    }

    private void CenterTitle()
    {
        if (_titleScene == null || string.IsNullOrEmpty(_title)) return;
        var style = Style;
        var m = ThorVG.Text.Gen();
        if (m == null) return;
        try
        {
            m.SetFont(style.FontName);
            m.SetFontSize(EffectiveFontSize(style.FontSize));
            m.SetText(_title);
            m.Bounds(out float bx, out float by, out float bw, out float bh);
            if (bw <= 0 || bh <= 0) return;

            float x = (Bounds.W - bw) / 2f - bx;
            float y = (HeaderHeight - bh) / 2f - by;
            _titleScene.Translate(x, y);
        }
        finally { m.Unref(); }
    }

    private void UpdateCloseButton()
    {
        if (_closeShape == null) return;
        float cx = Bounds.W - CloseButtonMargin - CloseButtonSize / 2f;
        float cy = HeaderHeight / 2f;
        DialogFrame.DrawClose(_closeShape, cx, cy);
    }

    private bool HitCloseButton(float lx, float ly)
    {
        float cx = Bounds.W - CloseButtonMargin - CloseButtonSize / 2f;
        float cy = HeaderHeight / 2f;
        return MathF.Abs(lx - cx) <= CloseButtonSize / 2f + 2f
            && MathF.Abs(ly - cy) <= CloseButtonSize / 2f + 2f;
    }

    // --- Dragging ---

    private int DetectResizeEdge(float lx, float ly)
    {
        int edge = 0;
        if (lx >= Bounds.W - ResizeGrip) edge |= 1; // right
        if (ly >= Bounds.H - ResizeGrip) edge |= 2; // bottom
        if (lx <= ResizeGrip) edge |= 4;             // left
        if (ly <= ResizeGrip && ly >= 0) edge |= 8;  // top (but not in header)
        return edge;
    }

    public override Element? HitTest(float x, float y)
    {
        if (!Visible || !Enabled) return null;

        float localX = x - Bounds.X;
        float localY = y - Bounds.Y;

        // Expand hit area slightly for resize grips on edges
        var hitRect = new RectF(-ResizeGrip, -ResizeGrip, Bounds.W + ResizeGrip * 2, Bounds.H + ResizeGrip * 2);
        if (!hitRect.Contains(localX, localY))
            return null;

        // Resize edges take priority
        var clampedX = MathF.Max(0, MathF.Min(localX, Bounds.W));
        var clampedY = MathF.Max(0, MathF.Min(localY, Bounds.H));
        if (DetectResizeEdge(clampedX, clampedY) != 0)
            return this;

        // If in header area, we handle it (for dragging)
        if (localY < HeaderHeight)
            return this;

        // Otherwise check children
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            var hit = Children[i].HitTest(localX, localY);
            if (hit != null) return hit;
        }

        return this;
    }

    public override bool OnMouseDown(int button, float x, float y)
    {
        if (!Enabled || button != 0) return false;

        WindowToLocal(x, y, out float lx, out float ly);

        // Check resize edges first
        _resizeEdge = DetectResizeEdge(lx, ly);
        if (_resizeEdge != 0)
        {
            _resizing = true;
            _dragOffsetX = x;
            _dragOffsetY = y;
            return true;
        }

        if (ly < HeaderHeight)
        {
            if (HitCloseButton(lx, ly))
            {
                CloseClicked?.Invoke();
                return true;
            }
            _dragging = true;
            _dragOffsetX = lx;
            _dragOffsetY = ly;
            return true;
        }

        return false;
    }

    public override bool OnMouseUp(int button, float x, float y)
    {
        if (button != 0) return false;
        _dragging = false;
        _resizing = false;
        _resizeEdge = 0;
        return true;
    }

    private static CursorType CursorForEdge(int edge)
    {
        return edge switch
        {
            1 or 4 => CursorType.HResize,       // right or left
            2 or 8 => CursorType.VResize,        // bottom or top
            3 or 12 => CursorType.ResizeNWSE,    // bottom-right (3) or top-left (12)
            9 or 6 => CursorType.ResizeNESW,     // top-right (9) or bottom-left (6)
            5 => CursorType.ResizeNWSE,           // left+right (unusual but handle)
            10 => CursorType.VResize,             // top+bottom (unusual)
            _ => CursorType.Arrow,
        };
    }

    public override void OnMouseMove(float x, float y)
    {
        if (_resizing)
        {
            float dx = x - _dragOffsetX;
            float dy = y - _dragOffsetY;
            _dragOffsetX = x;
            _dragOffsetY = y;

            float bx = Bounds.X, by = Bounds.Y, bw = Bounds.W, bh = Bounds.H;

            if ((_resizeEdge & 1) != 0) bw = MathF.Max(MinDialogW, bw + dx);    // right
            if ((_resizeEdge & 2) != 0) bh = MathF.Max(MinDialogH, bh + dy);    // bottom
            if ((_resizeEdge & 4) != 0) { var nw = MathF.Max(MinDialogW, bw - dx); bx += bw - nw; bw = nw; } // left
            if ((_resizeEdge & 8) != 0) { var nh = MathF.Max(MinDialogH, bh - dy); by += bh - nh; bh = nh; } // top

            Bounds = new RectF(bx, by, bw, bh);
            return;
        }

        if (_dragging)
        {
            float newX = x - _dragOffsetX;
            float newY = y - _dragOffsetY;

            if (Parent != null && Parent is not Window)
            {
                Parent.WindowToLocal(x, y, out float px, out float py);
                newX = px - _dragOffsetX;
                newY = py - _dragOffsetY;
            }

            // Clamp so the title bar stays inside the client area
            var parentW = Parent?.Bounds.W ?? 10000f;
            var parentH = Parent?.Bounds.H ?? 10000f;
            newX = MathF.Max(0, MathF.Min(newX, parentW - Bounds.W));
            newY = MathF.Max(0, MathF.Min(newY, parentH - HeaderHeight));

            Bounds = new RectF(newX, newY, Bounds.W, Bounds.H);
            return;
        }

        // Update cursor based on hover position
        WindowToLocal(x, y, out float lx, out float ly);
        var edge = DetectResizeEdge(lx, ly);
        if (edge != 0)
            Cursor = CursorForEdge(edge);
        else if (ly < HeaderHeight && HitCloseButton(lx, ly))
            Cursor = CursorType.Hand;
        else
            Cursor = CursorType.Arrow;
    }

    protected override Style GetDefaultStyle()
    {
        return OwnerWindow?.Theme.Panel ?? new Style();
    }

    protected override void OnThemeChanged()
    {
        if (!_shapesCreated) return;
        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        _frame?.ApplyTheme(theme, HeaderHeight);
        _titleText?.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);
        _closeShape?.StrokeFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8, theme.TextColor.A8);
        MarkDirty();
    }
}
