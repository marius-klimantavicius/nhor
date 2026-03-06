using System.Numerics;
using ThorVG;

namespace Marius.Winter;

public class Panel : Element, ILayoutContainer
{
    private Shape? _backgroundShape;
    private Shape? _borderShape;
    private ILayout? _layout;
    private bool _shapesCreated;
    private Color4? _background;
    private Color4? _borderColor;
    private Thickness? _padding;
    private CornerRadius? _cornerRadius;

    public Panel()
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

    public Color4? BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            if (_borderShape != null && value.HasValue)
            {
                _borderShape.StrokeFill(value.Value.R8, value.Value.G8, value.Value.B8, value.Value.A8);
                _borderShape.Visible(true);
            }
            else if (_borderShape != null)
            {
                _borderShape.Visible(false);
            }
            MarkDirty();
        }
    }

    public Thickness? Padding
    {
        get => _padding;
        set
        {
            _padding = value;
            _taffyStyleDirty = true;
            InvalidateMeasure();
        }
    }

    public CornerRadius? CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = value;
            if (Bounds.W > 0) OnSizeChanged();
            MarkDirty();
        }
    }

    public ILayout? Layout
    {
        get => _layout;
        set
        {
            _layout = value;
            _taffyStyleDirty = true;
            InvalidateMeasure();
        }
    }

    protected override void OnAttached()
    {
        if (!_shapesCreated)
        {
            _shapesCreated = true;
            var style = Style;

            _backgroundShape = Shape.Gen();
            if (_background.HasValue)
            {
                _backgroundShape!.SetFill(_background.Value.R8, _background.Value.G8, _background.Value.B8, _background.Value.A8);
            }
            else
            {
                _backgroundShape!.SetFill(0, 0, 0, 0);
                _backgroundShape.Visible(false);
            }
            AddPaint(_backgroundShape);

            _borderShape = Shape.Gen();
            _borderShape!.StrokeWidth(1f);
            _borderShape.SetFill(0, 0, 0, 0);
            if (_borderColor.HasValue)
            {
                _borderShape.StrokeFill(_borderColor.Value.R8, _borderColor.Value.G8, _borderColor.Value.B8, _borderColor.Value.A8);
            }
            else
            {
                _borderShape.Visible(false);
            }
            AddPaint(_borderShape);
        }
    }

    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        if (_layout != null)
            return _layout.Measure(this, availableWidth, availableHeight);

        // No layout — fill available space (clamped to finite values)
        float w = float.IsFinite(availableWidth) ? availableWidth : Bounds.W;
        float h = float.IsFinite(availableHeight) ? availableHeight : Bounds.H;
        return new Vector2(w, h);
    }

    protected override void ArrangeCore(RectF finalBounds)
    {
        if (_layout == null) return;
        var engine = OwnerWindow?._taffyLayout;
        if (engine != null && !engine.IsComputing)
        {
            // Use Taffy to compute layout for this subtree
            engine.ComputeLayout(this, finalBounds.W, finalBounds.H);
        }
        else if (engine == null || !engine.IsComputing)
        {
            // Fallback to manual layout when not attached to a window
            _layout.Arrange(this, new RectF(0, 0, finalBounds.W, finalBounds.H));
        }
        else
        {
            // Taffy is already computing (re-entrant call from ApplyLayout) — skip
        }
    }

    protected override void OnSizeChanged()
    {
        var cr = _cornerRadius ?? new CornerRadius(Style.CornerRadius);

        _backgroundShape?.ResetShape();
        AppendRoundedRect(_backgroundShape, 0, 0, Bounds.W, Bounds.H, cr);

        _borderShape?.ResetShape();
        AppendRoundedRect(_borderShape, 0.5f, 0.5f, Bounds.W - 1, Bounds.H - 1, cr);

        // Layout is handled by ArrangeCore (called after OnSizeChanged by Arrange).
        // No need to compute here — it would cause redundant double-layout.
    }

    internal static void AppendRoundedRect(Shape? shape, float x, float y, float w, float h, CornerRadius cr)
    {
        if (shape == null) return;
        if (cr.IsUniform)
        {
            shape.AppendRect(x, y, w, h, cr.TopLeft, cr.TopLeft);
            return;
        }

        // Per-corner rounded rect using cubic bezier arcs
        const float k = 0.5522847498f; // kappa for quarter-circle
        float tl = cr.TopLeft, tr = cr.TopRight, br = cr.BottomRight, bl = cr.BottomLeft;

        // Start at top-left after the TL arc
        shape.MoveTo(x + tl, y);
        // Top edge → top-right arc
        shape.LineTo(x + w - tr, y);
        if (tr > 0)
            shape.CubicTo(x + w - tr + tr * k, y, x + w, y + tr - tr * k, x + w, y + tr);
        // Right edge → bottom-right arc
        shape.LineTo(x + w, y + h - br);
        if (br > 0)
            shape.CubicTo(x + w, y + h - br + br * k, x + w - br + br * k, y + h, x + w - br, y + h);
        // Bottom edge → bottom-left arc
        shape.LineTo(x + bl, y + h);
        if (bl > 0)
            shape.CubicTo(x + bl - bl * k, y + h, x, y + h - bl + bl * k, x, y + h - bl);
        // Left edge → top-left arc
        shape.LineTo(x, y + tl);
        if (tl > 0)
            shape.CubicTo(x, y + tl - tl * k, x + tl - tl * k, y, x + tl, y);
        shape.Close();
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
        if (!_shapesCreated || !_background.HasValue) return;
        // Only repaint if an explicit background was set
        _backgroundShape?.SetFill(_background.Value.R8, _background.Value.G8, _background.Value.B8, _background.Value.A8);
        MarkDirty();
    }
}
