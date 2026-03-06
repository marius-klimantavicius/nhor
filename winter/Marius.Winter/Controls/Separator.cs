using System.Numerics;
using ThorVG;

namespace Marius.Winter;

/// <summary>
/// A thin horizontal line used to visually separate groups of controls.
/// </summary>
public class Separator : Element
{
    private Shape? _lineShape;
    private bool _shapesCreated;

    protected override void OnAttached()
    {
        if (!_shapesCreated)
        {
            _shapesCreated = true;
            var theme = OwnerWindow?.Theme ?? Theme.Dark;

            _lineShape = Shape.Gen();
            _lineShape!.StrokeWidth(1f);
            _lineShape.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, theme.BorderDark.A8);
            _lineShape.SetFill(0, 0, 0, 0);
            AddPaint(_lineShape);
        }
    }

    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        return new Vector2(0, 6); // zero intrinsic width; stretches to fill during arrange
    }

    protected override void OnSizeChanged()
    {
        float w = Bounds.W;
        float cy = Bounds.H / 2f;

        _lineShape?.ResetShape();
        _lineShape?.MoveTo(0, cy);
        _lineShape?.LineTo(w, cy);
    }

    protected override void OnThemeChanged()
    {
        if (!_shapesCreated) return;
        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        _lineShape?.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, theme.BorderDark.A8);
        MarkDirty();
    }
}
