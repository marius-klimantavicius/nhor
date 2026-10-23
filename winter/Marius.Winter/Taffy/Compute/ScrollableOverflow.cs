// Ported from src/compute/common/scrollable_overflow.rs at 802e497b.
using static Marius.Winter.Taffy.TaffySys;

namespace Marius.Winter.Taffy;

public static class ScrollableOverflow
{
    public static Rect<float> ComputeContribution(Point<float> location, Size<float> size,
        Rect<float> rect, Point<Overflow> overflow, Contain contain, bool parentIsScrollContainer)
    {
        bool clipped = overflow.X.IsScrollContainer() || overflow.Y.IsScrollContainer() || contain.ContainsScrollableOverflow();
        bool x = !clipped && overflow.X == Overflow.Visible;
        bool y = !clipped && overflow.Y == Overflow.Visible;
        float endX = x ? F32Max(size.Width, rect.Right) : size.Width;
        float endY = y ? F32Max(size.Height, rect.Bottom) : size.Height;
        if (endX <= 0 || endY <= 0) return default;
        var contribution = new Rect<float>(location.X + (x ? F32Min(0, rect.Left) : 0), location.X + endX,
            location.Y + (y ? F32Min(0, rect.Top) : 0), location.Y + endY);
        return parentIsScrollContainer && (contribution.Right <= 0 || contribution.Bottom <= 0) ? default : contribution;
    }
}
