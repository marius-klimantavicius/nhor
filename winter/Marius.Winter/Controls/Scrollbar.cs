using System;
using ThorVG;

namespace Marius.Winter;

internal readonly record struct ScrollbarViewport(float Width, float Height, bool Vertical, bool Horizontal)
{
    internal static ScrollbarViewport Resolve(float width, float height, float contentWidth, float contentHeight,
        bool allowVertical = true, bool allowHorizontal = true, bool alwaysVertical = false, bool alwaysHorizontal = false)
    {
        bool vertical = alwaysVertical, horizontal = alwaysHorizontal;
        float w = 0, h = 0;
        for (int i = 0; i < 3; i++)
        {
            w = Math.Max(0, width - (vertical ? ScrollbarGeometry.Thickness + ScrollbarGeometry.Margin : 0));
            h = Math.Max(0, height - (horizontal ? ScrollbarGeometry.Thickness + ScrollbarGeometry.Margin : 0));
            vertical |= allowVertical && contentHeight > h && h > 0;
            horizontal |= allowHorizontal && contentWidth > w && w > 0;
        }
        return new(w, h, vertical, horizontal);
    }
}

/// <summary>One axis in local logical pixels. Paint, hit testing and range mapping share this geometry.</summary>
internal readonly record struct ScrollbarGeometry(bool Horizontal, RectF Track, RectF HitRegion,
    float Viewport, float Content, float Offset, bool Visible)
{
    internal const float Thickness = 8, Margin = 2, MinimumThumb = 20;
    internal float Maximum => Math.Max(0, Content - Viewport);
    internal float Length => Horizontal ? Track.W : Track.H;
    internal float ThumbLength => Content > 0 ? Math.Min(Length, Math.Max(MinimumThumb, Length * Viewport / Content)) : Length;
    internal float Travel => Math.Max(0, Length - ThumbLength);
    internal float ThumbStart => (Horizontal ? Track.X : Track.Y) + (Maximum > 0 ? Math.Clamp(Offset / Maximum, 0, 1) * Travel : 0);
    internal bool CanDrag => Visible && Maximum > 0 && Travel > 0;
    internal bool Contains(float x, float y) => Visible && Length > 0 && HitRegion.Contains(x, y);
    internal RectF Thumb => Horizontal ? new(ThumbStart, Track.Y, ThumbLength, Track.H) : new(Track.X, ThumbStart, Track.W, ThumbLength);

    internal static ScrollbarGeometry Create(bool horizontal, float cross, float start, float length,
        float viewport, float content, float offset, bool visible)
    {
        length = Math.Max(0, length); viewport = Math.Max(0, viewport); content = Math.Max(0, content);
        var track = horizontal ? new RectF(start, cross, length, Thickness) : new RectF(cross, start, Thickness, length);
        var hit = horizontal ? new RectF(start, cross - Margin, length, Thickness + 2 * Margin)
            : new RectF(cross - Margin, start, Thickness + 2 * Margin, length);
        return new(horizontal, track, hit, viewport, content, offset, visible);
    }

    internal void Paint(Element owner, Shape? track, Shape? thumb, bool hovered, bool dragging)
    {
        track?.ResetShape(); thumb?.ResetShape();
        track?.Visible(Visible && Length > 0); thumb?.Visible(Visible && Maximum > 0 && Length > 0);
        if (!Visible || Length <= 0) return;
        var color = (owner.OwnerWindow?.Theme ?? Theme.Dark).TextColor;
        track?.SetFill(color.R8, color.G8, color.B8, (byte)(owner.IsEffectivelyEnabled ? 24 : 12));
        thumb?.SetFill(color.R8, color.G8, color.B8, (byte)(!owner.IsEffectivelyEnabled ? 45 : dragging ? 210 : hovered ? 170 : 115));
        track?.AppendRect(Track.X, Track.Y, Track.W, Track.H, Thickness / 2, Thickness / 2);
        if (Maximum > 0)
        {
            var rect = Thumb;
            thumb?.AppendRect(rect.X, rect.Y, rect.W, rect.H, Thickness / 2, Thickness / 2);
        }
    }
}

/// <summary>A track press pages once and keeps the same held press draggable, like the native editors.
/// There is no timer repeat: a stationary pointer stops after the initial page.</summary>
internal sealed class ScrollbarInteraction
{
    private ScrollbarGeometry _geometry;
    private float _pointer, _offset;
    private float _originX, _originY, _scaleX, _scaleY;
    private int _hoverAxis;
    internal bool Pressed { get; private set; }
    internal int Axis { get; private set; }
    internal bool Hovered(bool horizontal) => _hoverAxis == (horizontal ? 2 : 1);

    internal bool Hover(float x, float y, ScrollbarGeometry vertical, ScrollbarGeometry horizontal)
    {
        int axis = vertical.Contains(x, y) ? 1 : horizontal.Contains(x, y) ? 2 : 0;
        bool changed = axis != _hoverAxis; _hoverAxis = axis; return changed;
    }
    internal void Leave() => _hoverAxis = 0;
    internal float Begin(Element owner, ScrollbarGeometry geometry, float x, float y)
    {
        owner.WindowToLocal(0, 0, out _originX, out _originY);
        _scaleX = owner.OwnerWindow?.ContentScaleX ?? 1; _scaleY = owner.OwnerWindow?.ContentScaleY ?? 1;
        Pressed = true; Axis = geometry.CanDrag ? geometry.Horizontal ? 2 : 1 : 0;
        _geometry = geometry; _pointer = geometry.Horizontal ? x : y;
        float page = _pointer < geometry.ThumbStart ? -geometry.Viewport
            : _pointer > geometry.ThumbStart + geometry.ThumbLength ? geometry.Viewport : 0;
        _offset = Math.Clamp(geometry.Offset + page, 0, geometry.Maximum);
        return _offset;
    }
    internal bool Move(Element owner, ScrollbarGeometry vertical, ScrollbarGeometry horizontal, float x, float y, out float value)
    {
        value = 0;
        if (Axis == 0) return false;
        var geometry = Axis == 2 ? horizontal : vertical;
        owner.WindowToLocal(0, 0, out var originX, out var originY);
        // Offset changes during a gesture; any other geometry change invalidates its mapping.
        if (!owner.IsActive || originX != _originX || originY != _originY ||
            owner.OwnerWindow?.ContentScaleX != _scaleX || owner.OwnerWindow?.ContentScaleY != _scaleY ||
            !geometry.CanDrag || geometry with { Offset = _geometry.Offset } != _geometry)
        { Axis = 0; return false; }
        value = Math.Clamp(_offset + ((geometry.Horizontal ? x : y) - _pointer) * geometry.Maximum / geometry.Travel, 0, geometry.Maximum);
        return true;
    }
    internal void Cancel() { Pressed = false; Axis = 0; }
}
