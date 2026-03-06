// Port of taffy/src/style/flex.rs

namespace Marius.Winter.Taffy;

/// <summary>
/// The direction of the flexbox layout main axis.
///
/// There are always two perpendicular layout axes: main (or primary) and cross (or secondary).
/// Adding items will cause them to be positioned adjacent to each other along the main axis.
/// By varying this value throughout your tree, you can create complex axis-aligned layouts.
///
/// Items are always aligned relative to the cross axis, and justified relative to the main axis.
///
/// The default behavior is <see cref="Row"/>.
/// </summary>
public enum FlexDirection
{
    /// <summary>
    /// Defines +x as the main axis.
    /// Items will be added from left to right in a row.
    /// </summary>
    Row = 0,

    /// <summary>
    /// Defines +y as the main axis.
    /// Items will be added from top to bottom in a column.
    /// </summary>
    Column,

    /// <summary>
    /// Defines -x as the main axis.
    /// Items will be added from right to left in a row.
    /// </summary>
    RowReverse,

    /// <summary>
    /// Defines -y as the main axis.
    /// Items will be added from bottom to top in a column.
    /// </summary>
    ColumnReverse,
}

/// <summary>
/// Extension methods for <see cref="FlexDirection"/>.
/// </summary>
public static class FlexDirectionExtensions
{
    /// <summary>Is the direction Row or RowReverse?</summary>
    public static bool IsRow(this FlexDirection self)
    {
        return self == FlexDirection.Row || self == FlexDirection.RowReverse;
    }

    /// <summary>Is the direction Column or ColumnReverse?</summary>
    public static bool IsColumn(this FlexDirection self)
    {
        return self == FlexDirection.Column || self == FlexDirection.ColumnReverse;
    }

    /// <summary>Is the direction RowReverse or ColumnReverse?</summary>
    public static bool IsReverse(this FlexDirection self)
    {
        return self == FlexDirection.RowReverse || self == FlexDirection.ColumnReverse;
    }

    /// <summary>The AbsoluteAxis that corresponds to the main axis.</summary>
    public static AbsoluteAxis MainAxis(this FlexDirection self)
    {
        return self switch
        {
            FlexDirection.Row or FlexDirection.RowReverse => AbsoluteAxis.Horizontal,
            FlexDirection.Column or FlexDirection.ColumnReverse => AbsoluteAxis.Vertical,
            _ => AbsoluteAxis.Horizontal,
        };
    }

    /// <summary>The AbsoluteAxis that corresponds to the cross axis.</summary>
    public static AbsoluteAxis CrossAxis(this FlexDirection self)
    {
        return self switch
        {
            FlexDirection.Row or FlexDirection.RowReverse => AbsoluteAxis.Vertical,
            FlexDirection.Column or FlexDirection.ColumnReverse => AbsoluteAxis.Horizontal,
            _ => AbsoluteAxis.Vertical,
        };
    }
}

/// <summary>
/// Controls whether flex items are forced onto one line or can wrap onto multiple lines.
///
/// Defaults to <see cref="NoWrap"/>.
/// </summary>
public enum FlexWrap
{
    /// <summary>Items will not wrap and stay on a single line</summary>
    NoWrap = 0,

    /// <summary>Items will wrap according to this item's FlexDirection</summary>
    Wrap,

    /// <summary>Items will wrap in the opposite direction to this item's FlexDirection</summary>
    WrapReverse,
}
