using System;
using ThorVG;

namespace Marius.Winter;

/// <summary>The classic window paints, shared by modeless and modal frames.</summary>
internal sealed class DialogFrame
{
    private readonly Shape _shadow, _background, _border, _header, _separator;

    internal DialogFrame(Action<Paint> addPaint)
    {
        _shadow = Shape.Gen()!; addPaint(_shadow);
        _background = Shape.Gen()!; addPaint(_background);
        _border = Shape.Gen()!; addPaint(_border);
        _header = Shape.Gen()!; addPaint(_header);
        _separator = Shape.Gen()!; addPaint(_separator);
        foreach (var shape in new[] { _border, _separator })
        {
            shape.StrokeWidth(1);
            shape.SetFill(0, 0, 0, 0);
        }
    }

    internal void ApplyTheme(Theme theme, float headerHeight)
    {
        var shadow = theme.DropShadow;
        _shadow.SetFill(shadow.R8, shadow.G8, shadow.B8, shadow.A8);
        var bg = theme.WindowBackground;
        _background.SetFill(bg.R8, bg.G8, bg.B8, bg.A8);
        var border = theme.BorderDark;
        _border.StrokeFill(border.R8, border.G8, border.B8, border.A8);
        _separator.StrokeFill(border.R8, border.G8, border.B8, border.A8);
        var top = theme.ButtonGradientTopFocused;
        var bottom = theme.ButtonGradientBotFocused;
        var gradient = LinearGradient.Gen();
        gradient.Linear(0, 0, 0, Math.Max(1, headerHeight));
        gradient.SetColorStops(new Fill.ColorStop[]
        {
            new(0, top.R8, top.G8, top.B8, top.A8),
            new(1, bottom.R8, bottom.G8, bottom.B8, bottom.A8)
        }, 2);
        _header.SetFill(gradient);
    }

    internal void Arrange(float width, float height, float headerHeight)
    {
        _shadow.ResetShape(); _shadow.AppendRect(2, 2, width, height, 6, 6);
        _background.ResetShape(); _background.AppendRect(0, 0, width, height, 6, 6);
        _border.ResetShape(); _border.AppendRect(.5f, .5f, Math.Max(0, width - 1), Math.Max(0, height - 1), 6, 6);
        _header.ResetShape(); _header.AppendRect(1, 1, Math.Max(0, width - 2), Math.Max(0, headerHeight - 1), 5, 5);
        _separator.ResetShape(); _separator.MoveTo(0, headerHeight); _separator.LineTo(width, headerHeight);
    }

    internal static void DrawClose(Shape shape, float cx, float cy, float half = 5)
    {
        shape.ResetShape();
        shape.MoveTo(cx - half, cy - half); shape.LineTo(cx + half, cy + half);
        shape.MoveTo(cx + half, cy - half); shape.LineTo(cx - half, cy + half);
    }
}
