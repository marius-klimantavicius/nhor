// Ported from taffy/src/style/mod.rs (CoreStyle trait),
//           taffy/src/style/flex.rs (FlexboxContainerStyle, FlexboxItemStyle traits),
//           taffy/src/style/block.rs (BlockContainerStyle, BlockItemStyle traits),
//           taffy/src/style/grid.rs (GridContainerStyle, GridItemStyle traits)
//
// Style traits as C# interfaces. ICoreStyle is defined in CoreStyle.cs.

using System;
using System.Collections.Immutable;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// Controls whether grid items are placed row-wise or column-wise.
    /// And whether the sparse or dense packing algorithm is used.
    /// Port of Rust's GridAutoFlow enum.
    /// </summary>
    public enum GridAutoFlow
    {
        /// <summary>Items are placed by filling each row in turn, adding new rows as necessary</summary>
        Row,
        /// <summary>Items are placed by filling each column in turn, adding new columns as necessary</summary>
        Column,
        /// <summary>Combines Row with the dense packing algorithm</summary>
        RowDense,
        /// <summary>Combines Column with the dense packing algorithm</summary>
        ColumnDense,
    }

    public static class GridAutoFlowExtensions
    {
        /// <summary>Whether grid auto placement uses the dense placement algorithm</summary>
        public static bool IsDense(this GridAutoFlow self)
        {
            return self == GridAutoFlow.RowDense || self == GridAutoFlow.ColumnDense;
        }

        /// <summary>Whether grid auto placement fills areas row-wise or column-wise</summary>
        public static AbsoluteAxis PrimaryAxis(this GridAutoFlow self)
        {
            return self switch
            {
                GridAutoFlow.Row or GridAutoFlow.RowDense => AbsoluteAxis.Horizontal,
                GridAutoFlow.Column or GridAutoFlow.ColumnDense => AbsoluteAxis.Vertical,
                _ => AbsoluteAxis.Horizontal,
            };
        }
    }

    /// <summary>
    /// The set of styles required for a Flexbox container.
    /// Port of Rust's FlexboxContainerStyle trait.
    /// </summary>
    public interface IFlexboxContainerStyle : ICoreStyle
    {
        /// <summary>Which direction does the main axis flow in?</summary>
        FlexDirection FlexDirection() => Taffy.FlexDirection.Row;

        /// <summary>Should elements wrap, or stay in a single line?</summary>
        FlexWrap FlexWrap() => Taffy.FlexWrap.NoWrap;
        ushort FlexLineCount() => 1;

        /// <summary>How large should the gaps between items be?</summary>
        Size<LengthPercentage> Gap() => new Size<LengthPercentage>(LengthPercentage.ZERO, LengthPercentage.ZERO);

        /// <summary>How should content contained within this item be aligned in the cross/block axis</summary>
        AlignContent? AlignContent() => Taffy.AlignContent.Normal;

        /// <summary>How this node's children aligned in the cross/block axis?</summary>
        AlignItems? AlignItems() => Taffy.AlignItems.Normal;

        /// <summary>How this node's children should be aligned in the inline axis</summary>
        AlignContent? JustifyContent() => Taffy.AlignContent.Normal;
    }

    /// <summary>
    /// The set of styles required for a Flexbox item (child of a Flexbox container).
    /// Port of Rust's FlexboxItemStyle trait.
    /// </summary>
    public interface IFlexboxItemStyle : ICoreStyle
    {
        /// <summary>Sets the initial main axis size of the item</summary>
        Dimension FlexBasis() => Dimension.AUTO;

        /// <summary>The relative rate at which this item grows when it is expanding to fill space</summary>
        float FlexGrow() => 0f;

        /// <summary>The relative rate at which this item shrinks when it is contracting to fit into space</summary>
        float FlexShrink() => 1f;

        /// <summary>How this node should be aligned in the cross/block axis.
        /// Falls back to the parent's AlignItems if not set.</summary>
        AlignItems? AlignSelf() => null;
    }

    /// <summary>
    /// The set of styles required for a Block layout container.
    /// Port of Rust's BlockContainerStyle trait.
    /// </summary>
    public interface IBlockContainerStyle : ICoreStyle
    {
        AlignItems? JustifyItems() => Taffy.AlignItems.Normal;
        /// <summary>How items elements should aligned in the inline axis</summary>
        TextAlign TextAlign() => Taffy.TextAlign.Auto;

        /// <summary>How children are aligned in the block (cross) axis</summary>
        AlignContent? AlignContent() => Taffy.AlignContent.Normal;
    }

    /// <summary>
    /// The set of styles required for a Block layout item (child of a Block container).
    /// Port of Rust's BlockItemStyle trait.
    /// </summary>
    public interface IBlockItemStyle : ICoreStyle
    {
        AlignItems? AlignSelf() => null;
        AlignItems? JustifySelf() => null;
        AlignContent? AlignContent() => Taffy.AlignContent.Normal;
        /// <summary>Whether the item is a table. Table children are handled specially in block layout.</summary>
        bool IsTable() => false;

        /// <summary>Whether the item is floated</summary>
        Float Float() => Taffy.Float.None;

        /// <summary>Whether the item clears floats</summary>
        Clear Clear() => Taffy.Clear.None;
    }

    /// <summary>
    /// The set of styles required for a CSS Grid container.
    /// Port of Rust's GridContainerStyle trait.
    ///
    /// Immutable lists and managed strings provide the repeatable, cheaply cloned views
    /// represented by the native associated iterator and string types.
    /// </summary>
    public interface IGridContainerStyle : ICoreStyle
    {
        ImmutableList<GridTemplateComponent> GetGridTemplateRows() => ImmutableList<GridTemplateComponent>.Empty;
        ImmutableList<GridTemplateComponent> GetGridTemplateColumns() => ImmutableList<GridTemplateComponent>.Empty;
        ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>> GetGridAutoRows() => ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>>.Empty;
        ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>> GetGridAutoColumns() => ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>>.Empty;
        ImmutableList<GridTemplateArea> GetGridTemplateAreas() => ImmutableList<GridTemplateArea>.Empty;
        ImmutableList<ImmutableList<string>> GetGridTemplateRowNames() => ImmutableList<ImmutableList<string>>.Empty;
        ImmutableList<ImmutableList<string>> GetGridTemplateColumnNames() => ImmutableList<ImmutableList<string>>.Empty;
        ushort GetGridTemplateAreaRowCount()
        {
            ushort count = 0;
            foreach (var area in GetGridTemplateAreas()) count = Math.Max(count, (ushort)(Math.Max(area.RowEnd, (ushort)1) - 1));
            return count;
        }
        ushort GetGridTemplateAreaColumnCount()
        {
            ushort count = 0;
            foreach (var area in GetGridTemplateAreas()) count = Math.Max(count, (ushort)(Math.Max(area.ColumnEnd, (ushort)1) - 1));
            return count;
        }
        /// <summary>Controls how items get placed into the grid for auto-placed items</summary>
        GridAutoFlow GridAutoFlow() => Taffy.GridAutoFlow.Row;

        /// <summary>How large should the gaps between items be?</summary>
        Size<LengthPercentage> Gap() => new Size<LengthPercentage>(LengthPercentage.ZERO, LengthPercentage.ZERO);

        /// <summary>How should content contained within this item be aligned in the cross/block axis</summary>
        AlignContent? AlignContent() => Taffy.AlignContent.Normal;

        /// <summary>How should contained within this item be aligned in the main/inline axis</summary>
        AlignContent? JustifyContent() => Taffy.AlignContent.Normal;

        /// <summary>How this node's children aligned in the cross/block axis?</summary>
        AlignItems? AlignItems() => Taffy.AlignItems.Normal;

        /// <summary>How this node's children should be aligned in the inline axis</summary>
        AlignItems? JustifyItems() => Taffy.AlignItems.Normal;
    }

    /// <summary>
    /// The set of styles required for a CSS Grid item (child of a CSS Grid container).
    /// Port of Rust's GridItemStyle trait.
    /// </summary>
    public interface IGridItemStyle : ICoreStyle
    {
        Line<GridPlacement> GetGridRow() => new(GridPlacement.Auto, GridPlacement.Auto);
        Line<GridPlacement> GetGridColumn() => new(GridPlacement.Auto, GridPlacement.Auto);
        Line<GridPlacement> GetGridPlacement(AbsoluteAxis axis) => axis == AbsoluteAxis.Horizontal ? GetGridColumn() : GetGridRow();
        /// <summary>How this node should be aligned in the cross/block axis.
        /// Falls back to the parent's AlignItems if not set.</summary>
        AlignItems? AlignSelf() => null;

        /// <summary>How this node should be aligned in the inline axis.
        /// Falls back to the parent's JustifyItems if not set.</summary>
        AlignItems? JustifySelf() => null;
    }
}
