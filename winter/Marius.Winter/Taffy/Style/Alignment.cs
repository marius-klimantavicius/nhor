// Port of taffy/src/style/alignment.rs

namespace Marius.Winter.Taffy;

/// <summary>
/// Used to control how child nodes are aligned.
/// For Flexbox it controls alignment in the cross axis.
/// For Grid it controls alignment in the block axis.
///
/// Also used as: AlignSelf, JustifyItems, JustifySelf (which are type aliases in Rust).
/// </summary>
public enum AlignItems
{
    /// <summary>Items are packed toward the start of the axis</summary>
    Start,
    /// <summary>Items are packed toward the end of the axis</summary>
    End,
    /// <summary>
    /// Items are packed towards the flex-relative start of the axis.
    /// For flex containers with flex_direction RowReverse or ColumnReverse this is equivalent
    /// to End. In all other cases it is equivalent to Start.
    /// </summary>
    FlexStart,
    /// <summary>
    /// Items are packed towards the flex-relative end of the axis.
    /// For flex containers with flex_direction RowReverse or ColumnReverse this is equivalent
    /// to Start. In all other cases it is equivalent to End.
    /// </summary>
    FlexEnd,
    /// <summary>Items are packed along the center of the cross axis</summary>
    Center,
    /// <summary>Items are aligned such as their baselines align</summary>
    Baseline,
    /// <summary>Stretch to fill the container</summary>
    Stretch,
}

// In Rust: pub type AlignSelf = AlignItems;
// In Rust: pub type JustifyItems = AlignItems;
// In Rust: pub type JustifySelf = AlignItems;
// In C#, just use AlignItems directly wherever AlignSelf/JustifyItems/JustifySelf would be used.

/// <summary>
/// Sets the distribution of space between and around content items.
/// For Flexbox it controls alignment in the cross axis.
/// For Grid it controls alignment in the block axis.
///
/// Also used as: JustifyContent (which is a type alias in Rust).
/// </summary>
public enum AlignContent
{
    /// <summary>Items are packed toward the start of the axis</summary>
    Start,
    /// <summary>Items are packed toward the end of the axis</summary>
    End,
    /// <summary>
    /// Items are packed towards the flex-relative start of the axis.
    /// For flex containers with flex_direction RowReverse or ColumnReverse this is equivalent
    /// to End. In all other cases it is equivalent to Start.
    /// </summary>
    FlexStart,
    /// <summary>
    /// Items are packed towards the flex-relative end of the axis.
    /// For flex containers with flex_direction RowReverse or ColumnReverse this is equivalent
    /// to Start. In all other cases it is equivalent to End.
    /// </summary>
    FlexEnd,
    /// <summary>Items are centered around the middle of the axis</summary>
    Center,
    /// <summary>Items are stretched to fill the container</summary>
    Stretch,
    /// <summary>
    /// The first and last items are aligned flush with the edges of the container (no gap).
    /// The gap between items is distributed evenly.
    /// </summary>
    SpaceBetween,
    /// <summary>
    /// The gap between the first and last items is exactly THE SAME as the gap between items.
    /// The gaps are distributed evenly.
    /// </summary>
    SpaceEvenly,
    /// <summary>
    /// The gap between the first and last items is exactly HALF the gap between items.
    /// The gaps are distributed evenly in proportion to these ratios.
    /// </summary>
    SpaceAround,
}

// In Rust: pub type JustifyContent = AlignContent;
// In C#, just use AlignContent directly wherever JustifyContent would be used.
