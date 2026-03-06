// Ported from ThorVG/src/loaders/svg/tvgSvgCssStyle.h and tvgSvgCssStyle.cpp
// CSS style handling.

namespace ThorVG
{
    public static class SvgCssStyle
    {
        private static bool IsImportanceApplicable(SvgStyleFlags toFlagsImportance, SvgStyleFlags fromFlagsImportance, SvgStyleFlags flag)
        {
            return (toFlagsImportance & flag) == 0 && (fromFlagsImportance & flag) != 0;
        }

        private static void CopyStyle(SvgStyleProperty to, SvgStyleProperty? from, bool overwrite)
        {
            if (from == null) return;

            // Color
            if ((from.curColorSet && (overwrite || (to.flags & SvgStyleFlags.Color) == 0)) ||
                IsImportanceApplicable(to.flagsImportance, from.flagsImportance, SvgStyleFlags.Color))
            {
                to.color = from.color;
                to.curColorSet = true;
                to.flags |= SvgStyleFlags.Color;
                if ((from.flagsImportance & SvgStyleFlags.Color) != 0) to.flagsImportance |= SvgStyleFlags.Color;
            }
            // PaintOrder
            if (((from.flags & SvgStyleFlags.PaintOrder) != 0 && (overwrite || (to.flags & SvgStyleFlags.PaintOrder) == 0)) ||
                IsImportanceApplicable(to.flagsImportance, from.flagsImportance, SvgStyleFlags.PaintOrder))
            {
                to.paintOrder = from.paintOrder;
                to.flags |= SvgStyleFlags.PaintOrder;
                if ((from.flagsImportance & SvgStyleFlags.PaintOrder) != 0) to.flagsImportance |= SvgStyleFlags.PaintOrder;
            }
            // Display
            if (((from.flags & SvgStyleFlags.Display) != 0 && (overwrite || (to.flags & SvgStyleFlags.Display) == 0)) ||
                IsImportanceApplicable(to.flagsImportance, from.flagsImportance, SvgStyleFlags.Display))
            {
                to.display = from.display;
                to.flags |= SvgStyleFlags.Display;
                if ((from.flagsImportance & SvgStyleFlags.Display) != 0) to.flagsImportance |= SvgStyleFlags.Display;
            }
            // Fill Paint
            if (((from.fill.flags & SvgFillFlags.Paint) != 0 && (overwrite || (to.flags & SvgStyleFlags.Fill) == 0)) ||
                IsImportanceApplicable(to.flagsImportance, from.flagsImportance, SvgStyleFlags.Fill))
            {
                to.fill.paint.color = from.fill.paint.color;
                to.fill.paint.none = from.fill.paint.none;
                to.fill.paint.curColor = from.fill.paint.curColor;
                if (from.fill.paint.url != null) to.fill.paint.url = from.fill.paint.url;
                to.fill.flags |= SvgFillFlags.Paint;
                to.flags |= SvgStyleFlags.Fill;
                if ((from.flagsImportance & SvgStyleFlags.Fill) != 0) to.flagsImportance |= SvgStyleFlags.Fill;
            }
            // Fill Opacity
            if (((from.fill.flags & SvgFillFlags.Opacity) != 0 && (overwrite || (to.flags & SvgStyleFlags.FillOpacity) == 0)) ||
                IsImportanceApplicable(to.flagsImportance, from.flagsImportance, SvgStyleFlags.FillOpacity))
            {
                to.fill.opacity = from.fill.opacity;
                to.fill.flags |= SvgFillFlags.Opacity;
                to.flags |= SvgStyleFlags.FillOpacity;
                if ((from.flagsImportance & SvgStyleFlags.FillOpacity) != 0) to.flagsImportance |= SvgStyleFlags.FillOpacity;
            }
            // Fill Rule
            if (((from.fill.flags & SvgFillFlags.FillRule) != 0 && (overwrite || (to.flags & SvgStyleFlags.FillRule) == 0)) ||
                IsImportanceApplicable(to.flagsImportance, from.flagsImportance, SvgStyleFlags.FillRule))
            {
                to.fill.fillRule = from.fill.fillRule;
                to.fill.flags |= SvgFillFlags.FillRule;
                to.flags |= SvgStyleFlags.FillRule;
                if ((from.flagsImportance & SvgStyleFlags.FillRule) != 0) to.flagsImportance |= SvgStyleFlags.FillRule;
            }
            // Stroke Paint
            if (((from.stroke.flags & SvgStrokeFlags.Paint) != 0 && (overwrite || (to.flags & SvgStyleFlags.Stroke) == 0)) ||
                IsImportanceApplicable(to.flagsImportance, from.flagsImportance, SvgStyleFlags.Stroke))
            {
                to.stroke.paint.color = from.stroke.paint.color;
                to.stroke.paint.none = from.stroke.paint.none;
                to.stroke.paint.curColor = from.stroke.paint.curColor;
                if (from.stroke.paint.url != null) to.stroke.paint.url = from.stroke.paint.url;
                to.stroke.flags |= SvgStrokeFlags.Paint;
                to.flags |= SvgStyleFlags.Stroke;
                if ((from.flagsImportance & SvgStyleFlags.Stroke) != 0) to.flagsImportance |= SvgStyleFlags.Stroke;
            }
            // Stroke Opacity
            if (((from.stroke.flags & SvgStrokeFlags.Opacity) != 0 && (overwrite || (to.flags & SvgStyleFlags.StrokeOpacity) == 0)) ||
                IsImportanceApplicable(to.flagsImportance, from.flagsImportance, SvgStyleFlags.StrokeOpacity))
            {
                to.stroke.opacity = from.stroke.opacity;
                to.stroke.flags |= SvgStrokeFlags.Opacity;
                to.flags |= SvgStyleFlags.StrokeOpacity;
                if ((from.flagsImportance & SvgStyleFlags.StrokeOpacity) != 0) to.flagsImportance |= SvgStyleFlags.StrokeOpacity;
            }
            // Stroke Width
            if (((from.stroke.flags & SvgStrokeFlags.Width) != 0 && (overwrite || (to.flags & SvgStyleFlags.StrokeWidth) == 0)) ||
                IsImportanceApplicable(to.flagsImportance, from.flagsImportance, SvgStyleFlags.StrokeWidth))
            {
                to.stroke.width = from.stroke.width;
                to.stroke.flags |= SvgStrokeFlags.Width;
                to.flags |= SvgStyleFlags.StrokeWidth;
                if ((from.flagsImportance & SvgStyleFlags.StrokeWidth) != 0) to.flagsImportance |= SvgStyleFlags.StrokeWidth;
            }
            // Stroke Dash
            if (((from.stroke.flags & SvgStrokeFlags.Dash) != 0 && (overwrite || (to.flags & SvgStyleFlags.StrokeDashArray) == 0)) ||
                IsImportanceApplicable(to.flagsImportance, from.flagsImportance, SvgStyleFlags.StrokeDashArray))
            {
                if (from.stroke.dash.array.Count > 0)
                {
                    to.stroke.dash.array.Clear();
                    to.stroke.dash.array.AddRange(from.stroke.dash.array);
                    to.stroke.flags |= SvgStrokeFlags.Dash;
                    to.flags |= SvgStyleFlags.StrokeDashArray;
                    if ((from.flagsImportance & SvgStyleFlags.StrokeDashArray) != 0) to.flagsImportance |= SvgStyleFlags.StrokeDashArray;
                }
            }
            // Stroke Cap
            if (((from.stroke.flags & SvgStrokeFlags.Cap) != 0 && (overwrite || (to.flags & SvgStyleFlags.StrokeLineCap) == 0)) ||
                IsImportanceApplicable(to.flagsImportance, from.flagsImportance, SvgStyleFlags.StrokeLineCap))
            {
                to.stroke.cap = from.stroke.cap;
                to.stroke.flags |= SvgStrokeFlags.Cap;
                to.flags |= SvgStyleFlags.StrokeLineCap;
                if ((from.flagsImportance & SvgStyleFlags.StrokeLineCap) != 0) to.flagsImportance |= SvgStyleFlags.StrokeLineCap;
            }
            // Stroke Join
            if (((from.stroke.flags & SvgStrokeFlags.Join) != 0 && (overwrite || (to.flags & SvgStyleFlags.StrokeLineJoin) == 0)) ||
                IsImportanceApplicable(to.flagsImportance, from.flagsImportance, SvgStyleFlags.StrokeLineJoin))
            {
                to.stroke.join = from.stroke.join;
                to.stroke.flags |= SvgStrokeFlags.Join;
                to.flags |= SvgStyleFlags.StrokeLineJoin;
                if ((from.flagsImportance & SvgStyleFlags.StrokeLineJoin) != 0) to.flagsImportance |= SvgStyleFlags.StrokeLineJoin;
            }
            // Opacity
            if (((from.flags & SvgStyleFlags.Opacity) != 0 && (overwrite || (to.flags & SvgStyleFlags.Opacity) == 0)) ||
                IsImportanceApplicable(to.flagsImportance, from.flagsImportance, SvgStyleFlags.Opacity))
            {
                to.opacity = from.opacity;
                to.flags |= SvgStyleFlags.Opacity;
                if ((from.flagsImportance & SvgStyleFlags.Opacity) != 0) to.flagsImportance |= SvgStyleFlags.Opacity;
            }
        }

        public static void CopyStyleAttr(SvgNode to, SvgNode from, bool overwrite = false)
        {
            // Copy matrix attribute
            if (from.transform.HasValue && (overwrite || (to.style!.flags & SvgStyleFlags.Transform) == 0))
            {
                to.transform = from.transform;
                to.style!.flags |= SvgStyleFlags.Transform;
            }
            // Copy style attribute
            CopyStyle(to.style!, from.style, overwrite);

            if (from.style!.clipPath.url != null)
            {
                to.style!.clipPath.url = from.style.clipPath.url;
            }
            if (from.style.mask.url != null)
            {
                to.style!.mask.url = from.style.mask.url;
            }
        }

        public static SvgNode? FindStyleNode(SvgNode? style, string? title, SvgNodeType type)
        {
            if (style == null) return null;
            foreach (var child in style.child)
            {
                if (child.type == type)
                {
                    if ((title == null && child.id == null) ||
                        (title != null && child.id != null && SvgHelper.StrAs(child.id, title)))
                        return child;
                }
            }
            return null;
        }

        public static SvgNode? FindStyleNode(SvgNode? style, string? title)
        {
            if (style == null || title == null) return null;
            foreach (var child in style.child)
            {
                if (child.type == SvgNodeType.CssStyle)
                {
                    if (child.id != null && SvgHelper.StrAs(child.id, title))
                        return child;
                }
            }
            return null;
        }

        public static void UpdateStyle(SvgNode doc, SvgNode? style)
        {
            if (style == null) return;
            foreach (var child in doc.child)
            {
                var cssNode = FindStyleNode(style, null, child.type);
                if (cssNode != null) CopyStyleAttr(child, cssNode);
                UpdateStyle(child, style);
            }
        }
    }
}
