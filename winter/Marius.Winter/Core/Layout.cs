using System.Numerics;

namespace Marius.Winter;

public enum Orientation { Horizontal, Vertical }
public enum Alignment { Start, Center, End, Stretch }

public interface ILayout
{
    Vector2 Measure(Element container, float availableWidth, float availableHeight);
    void Arrange(Element container, RectF bounds);
}

public interface ILayoutContainer
{
    ILayout? Layout { get; set; }
}

public class StackLayout : ILayout
{
    public Orientation Orientation { get; set; } = Orientation.Vertical;
    public float Spacing { get; set; } = 4f;
    public Alignment CrossAlignment { get; set; } = Alignment.Stretch;

    public Vector2 Measure(Element container, float availableWidth, float availableHeight)
    {
        float mainSize = 0;
        float crossSize = 0;
        var children = container.Children;
        var padding = container.Style.Padding;

        float innerW = availableWidth - padding.HorizontalTotal;
        float innerH = availableHeight - padding.VerticalTotal;

        for (int i = 0; i < children.Count; i++)
        {
            if (!children[i].Visible) continue;

            var childSize = children[i].Measure(innerW, innerH);

            if (Orientation == Orientation.Vertical)
            {
                mainSize += childSize.Y;
                if (crossSize < childSize.X) crossSize = childSize.X;
            }
            else
            {
                mainSize += childSize.X;
                if (crossSize < childSize.Y) crossSize = childSize.Y;
            }

            if (i > 0) mainSize += Spacing;
        }

        if (Orientation == Orientation.Vertical)
            return new Vector2(crossSize + padding.HorizontalTotal, mainSize + padding.VerticalTotal);
        else
            return new Vector2(mainSize + padding.HorizontalTotal, crossSize + padding.VerticalTotal);
    }

    public void Arrange(Element container, RectF bounds)
    {
        var children = container.Children;
        var padding = container.Style.Padding;

        float x = bounds.X + padding.Left;
        float y = bounds.Y + padding.Top;
        float innerW = bounds.W - padding.HorizontalTotal;
        float innerH = bounds.H - padding.VerticalTotal;

        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (!child.Visible) continue;

            var desired = child.DesiredSize;

            if (Orientation == Orientation.Vertical)
            {
                float childW = CrossAlignment == Alignment.Stretch ? innerW : desired.X;
                float childX = x;

                if (CrossAlignment == Alignment.Center)
                    childX = x + (innerW - childW) / 2f;
                else if (CrossAlignment == Alignment.End)
                    childX = x + innerW - childW;

                child.Arrange(new RectF(childX, y, childW, desired.Y));
                y += desired.Y + Spacing;
            }
            else
            {
                float childH = CrossAlignment == Alignment.Stretch ? innerH : desired.Y;
                float childY = y;

                if (CrossAlignment == Alignment.Center)
                    childY = y + (innerH - childH) / 2f;
                else if (CrossAlignment == Alignment.End)
                    childY = y + innerH - childH;

                child.Arrange(new RectF(x, childY, desired.X, childH));
                x += desired.X + Spacing;
            }
        }
    }
}
