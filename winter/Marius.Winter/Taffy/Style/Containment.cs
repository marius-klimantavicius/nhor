// Ported from src/style/mod.rs at 802e497b.
using System;

namespace Marius.Winter.Taffy;

[Flags]
public enum Contain : byte
{
    None = 0,
    Layout = 1,
    Paint = 2,
    Content = Layout | Paint,
}

public static class ContainExtensions
{
    public static bool Contains(this Contain value, Contain other) => (value & other) == other;
    public static bool Intersects(this Contain value, Contain other) => (value & other) != 0;
    public static bool EstablishesIndependentFormattingContext(this Contain value) => value.Intersects(Contain.Content);
    public static bool SuppressesBaseline(this Contain value) => value.Contains(Contain.Layout);
    public static bool ContainsScrollableOverflow(this Contain value) => value.Intersects(Contain.Content);
}

public readonly record struct ContainingBlockClaims(bool Absolute, bool Fixed)
{
    public static readonly ContainingBlockClaims None = new(false, false);
    public static readonly ContainingBlockClaims All = new(true, true);
    public bool ForPosition(Position position) => position switch
    {
        Position.Absolute => Absolute,
        Position.Fixed => Fixed,
        _ => false,
    };
}

public static class PositionExtensions
{
    public static bool IsOutOfFlow(this Position position) => position is Position.Absolute or Position.Fixed;
    public static bool IsPositioned(this Position position) => position != Position.Static;
}

public interface IOofItemStyle : ICoreStyle
{
    AlignItems? AlignSelf() => null;
    AlignItems? JustifySelf() => null;
    bool IsTable() => false;
    Line<GridPlacement> GridRow() => new(GridPlacement.Auto, GridPlacement.Auto);
    Line<GridPlacement> GridColumn() => new(GridPlacement.Auto, GridPlacement.Auto);
}
