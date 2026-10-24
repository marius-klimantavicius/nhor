using System;
using System.Numerics;

namespace Marius.Winter.Scenarios;

// Measure text at the width it will actually receive, after reserving the
// thumbnail, metadata and action. These layouts belong to the catalog example.
public sealed class AssetCatalogNameLayout : ILayout
{
    public Vector2 Measure(Element parent, float width, float height)
    {
        var image = parent.Children[0].Measure(28, height);
        var text = parent.Children[1].Measure(Math.Max(0, width - image.X - 5), float.PositiveInfinity);
        return new(width, Math.Max(image.Y, text.Y));
    }
    public void Arrange(Element parent, RectF bounds)
    {
        var image = parent.Children[0].DesiredSize;
        parent.Children[0].Arrange(new RectF(bounds.X, bounds.Y + Math.Max(0, (bounds.H - image.Y) / 2), image.X, image.Y));
        parent.Children[1].Arrange(new RectF(bounds.X + image.X + 5, bounds.Y, Math.Max(0, bounds.W - image.X - 5), bounds.H));
    }
}

public sealed class AssetCatalogRowLayout : ILayout
{
    public Vector2 Measure(Element parent, float width, float height)
    {
        var metadata = parent.Children[1].Measure(width, height);
        var action = parent.Children[2].Measure(width, height);
        var name = parent.Children[0].Measure(Math.Max(0, width - metadata.X - action.X - 14), float.PositiveInfinity);
        return new(width, Math.Max(name.Y, Math.Max(metadata.Y, action.Y)));
    }
    public void Arrange(Element parent, RectF bounds)
    {
        var metadata = parent.Children[1].DesiredSize; var action = parent.Children[2].DesiredSize;
        float nameWidth = Math.Max(0, bounds.W - metadata.X - action.X - 14);
        parent.Children[0].Arrange(new RectF(bounds.X, bounds.Y, nameWidth, bounds.H));
        parent.Children[1].Arrange(new RectF(bounds.X + nameWidth + 7, bounds.Y + Math.Max(0, (bounds.H - metadata.Y) / 2), metadata.X, metadata.Y));
        parent.Children[2].Arrange(new RectF(bounds.X + nameWidth + metadata.X + 14, bounds.Y + Math.Max(0, (bounds.H - action.Y) / 2), action.X, action.Y));
    }
}
