// Ported from taffy/src/style/mod.rs (Style struct and trait implementations)
// A typed representation of CSS style properties used as input to layout computation.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// A typed representation of the CSS style information for a single node.
    ///
    /// The most important idea in flexbox is the notion of a "main" and "cross" axis, which are always perpendicular
    /// to each other. The orientation of these axes are controlled via the FlexDirection field.
    ///
    /// This struct follows the CSS equivalent directly; information about the behavior on the web should transfer directly.
    ///
    /// Detailed information about the exact behavior of each of these fields can be found on MDN by searching for the field name.
    /// The distinction between margin, padding and border is explained well in the
    /// introduction to the box model (https://developer.mozilla.org/en-US/docs/Web/CSS/CSS_Box_Model/Introduction_to_the_CSS_box_model).
    /// </summary>
    public class Style : ICoreStyle, IFlexboxContainerStyle, IFlexboxItemStyle,
        IBlockContainerStyle, IBlockItemStyle, IGridContainerStyle, IGridItemStyle
    {
        // Static shared empty lists to avoid allocations for non-grid styles
        private static readonly ImmutableList<GridTemplateComponent> s_emptyGridTemplateComponents = ImmutableList<GridTemplateComponent>.Empty;
        private static readonly ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>> s_emptyMinMaxTracks = ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>>.Empty;
        private static readonly ImmutableList<GridTemplateArea> s_emptyGridTemplateAreas = ImmutableList<GridTemplateArea>.Empty;
        private static readonly ImmutableList<ImmutableList<string>> s_emptyNameLists = ImmutableList<ImmutableList<string>>.Empty;

        // --- Display ---

        /// <summary>What layout strategy should be used?</summary>
        public Display Display;

        /// <summary>Whether a child is display:table or not. Affects children of block layouts.</summary>
        public bool ItemIsTable;

        /// <summary>Is it a replaced element like an image or form field?</summary>
        public bool ItemIsReplaced;

        /// <summary>Should size styles apply to the content box or the border box of the node</summary>
        public BoxSizing BoxSizingValue;

        // --- Overflow properties ---

        /// <summary>How children overflowing their container should affect layout</summary>
        public Point<Overflow> OverflowValue;

        /// <summary>How much space (in points) should be reserved for the scrollbars of Overflow.Scroll and Overflow.Auto nodes</summary>
        public float ScrollbarWidthValue;

        // --- Float properties ---

        /// <summary>Should the box be floated</summary>
        public Float FloatValue;

        /// <summary>Should the box clear floats</summary>
        public Clear ClearValue;

        // --- Direction ---

        /// <summary>The text/layout direction</summary>
        public Direction DirectionValue;

        // --- Position properties ---

        /// <summary>What should the position value of this struct use as a base offset?</summary>
        public Position PositionValue;

        /// <summary>How should the position of this element be tweaked relative to the layout defined?</summary>
        public Rect<LengthPercentageAuto> Inset;

        // --- Size properties ---

        /// <summary>Sets the initial size of the item</summary>
        public Size<Dimension> SizeValue;

        /// <summary>Controls the minimum size of the item</summary>
        public Size<Dimension> MinSizeValue;

        /// <summary>Controls the maximum size of the item</summary>
        public Size<Dimension> MaxSizeValue;

        /// <summary>Sets the preferred aspect ratio for the item. The ratio is calculated as width divided by height.</summary>
        public float? AspectRatioValue;

        // --- Spacing properties ---

        /// <summary>How large should the margin be on each side?</summary>
        public Rect<LengthPercentageAuto> MarginValue;

        /// <summary>How large should the padding be on each side?</summary>
        public Rect<LengthPercentage> PaddingValue;

        /// <summary>How large should the border be on each side?</summary>
        public Rect<LengthPercentage> BorderValue;

        // --- Alignment properties ---

        /// <summary>How this node's children aligned in the cross/block axis?</summary>
        public AlignItems? AlignItemsValue;

        /// <summary>How this node should be aligned in the cross/block axis. Falls back to the parent's AlignItems if not set.</summary>
        public AlignItems? AlignSelfValue;

        /// <summary>How this node's children should be aligned in the inline axis</summary>
        public AlignItems? JustifyItemsValue;

        /// <summary>How this node should be aligned in the inline axis. Falls back to the parent's JustifyItems if not set.</summary>
        public AlignItems? JustifySelfValue;

        /// <summary>How should content contained within this item be aligned in the cross/block axis</summary>
        public AlignContent? AlignContentValue;

        /// <summary>How should content contained within this item be aligned in the main/inline axis</summary>
        public AlignContent? JustifyContentValue;

        /// <summary>How large should the gaps between items in a grid or flex container be?</summary>
        public Size<LengthPercentage> GapValue;

        // --- Block container properties ---

        /// <summary>How items elements should be aligned in the inline axis</summary>
        public TextAlign TextAlignValue;

        // --- Flexbox container properties ---

        /// <summary>Which direction does the main axis flow in?</summary>
        public FlexDirection FlexDirectionValue;

        /// <summary>Should elements wrap, or stay in a single line?</summary>
        public FlexWrap FlexWrapValue;

        // --- Flexbox item properties ---

        /// <summary>Sets the initial main axis size of the item</summary>
        public Dimension FlexBasisValue;

        /// <summary>The relative rate at which this item grows when it is expanding to fill space. 0.0 is the default value, and this value must be positive.</summary>
        public float FlexGrowValue;

        /// <summary>The relative rate at which this item shrinks when it is contracting to fit into space. 1.0 is the default value, and this value must be positive.</summary>
        public float FlexShrinkValue;

        // --- Grid container properties ---

        /// <summary>Defines the track sizing functions (heights) of the grid rows</summary>
        public ImmutableList<GridTemplateComponent> GridTemplateRows;

        /// <summary>Defines the track sizing functions (widths) of the grid columns</summary>
        public ImmutableList<GridTemplateComponent> GridTemplateColumns;

        /// <summary>Defines the size of implicitly created rows</summary>
        public ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>> GridAutoRows;

        /// <summary>Defines the size of implicitly created columns</summary>
        public ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>> GridAutoColumns;

        /// <summary>Controls how items get placed into the grid for auto-placed items</summary>
        public GridAutoFlow GridAutoFlowValue;

        // --- Grid container named properties ---

        /// <summary>Defines the rectangular grid areas</summary>
        public ImmutableList<GridTemplateArea> GridTemplateAreas;

        /// <summary>The named lines between the columns</summary>
        public ImmutableList<ImmutableList<string>> GridTemplateColumnNames;

        /// <summary>The named lines between the rows</summary>
        public ImmutableList<ImmutableList<string>> GridTemplateRowNames;

        // --- Grid child properties ---

        /// <summary>Defines which row in the grid the item should start and end at</summary>
        public Line<GridPlacement> GridRow;

        /// <summary>Defines which column in the grid the item should start and end at</summary>
        public Line<GridPlacement> GridColumn;

        // =========================================================================
        // Default constructor
        // =========================================================================

        /// <summary>
        /// Creates a new Style with default values matching the CSS specification defaults.
        /// </summary>
        public Style()
        {
            Display = Display.Flex;
            ItemIsTable = false;
            ItemIsReplaced = false;
            BoxSizingValue = BoxSizing.BorderBox;
            OverflowValue = new Point<Overflow>(Overflow.Visible, Overflow.Visible);
            ScrollbarWidthValue = 0f;
            FloatValue = Float.None;
            ClearValue = Clear.None;
            PositionValue = Position.Relative;
            Inset = new Rect<LengthPercentageAuto>(
                LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO,
                LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO);
            SizeValue = new Size<Dimension>(Dimension.AUTO, Dimension.AUTO);
            MinSizeValue = new Size<Dimension>(Dimension.AUTO, Dimension.AUTO);
            MaxSizeValue = new Size<Dimension>(Dimension.AUTO, Dimension.AUTO);
            AspectRatioValue = null;
            MarginValue = new Rect<LengthPercentageAuto>(
                LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO,
                LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO);
            PaddingValue = new Rect<LengthPercentage>(
                LengthPercentage.ZERO, LengthPercentage.ZERO,
                LengthPercentage.ZERO, LengthPercentage.ZERO);
            BorderValue = new Rect<LengthPercentage>(
                LengthPercentage.ZERO, LengthPercentage.ZERO,
                LengthPercentage.ZERO, LengthPercentage.ZERO);
            AlignItemsValue = null;
            AlignSelfValue = null;
            JustifyItemsValue = null;
            JustifySelfValue = null;
            AlignContentValue = null;
            JustifyContentValue = null;
            GapValue = new Size<LengthPercentage>(LengthPercentage.ZERO, LengthPercentage.ZERO);
            TextAlignValue = TextAlign.Auto;
            FlexDirectionValue = FlexDirection.Row;
            FlexWrapValue = FlexWrap.NoWrap;
            FlexBasisValue = Dimension.AUTO;
            FlexGrowValue = 0f;
            FlexShrinkValue = 1f;
            GridTemplateRows = s_emptyGridTemplateComponents;
            GridTemplateColumns = s_emptyGridTemplateComponents;
            GridAutoRows = s_emptyMinMaxTracks;
            GridAutoColumns = s_emptyMinMaxTracks;
            GridAutoFlowValue = GridAutoFlow.Row;
            GridTemplateAreas = s_emptyGridTemplateAreas;
            GridTemplateColumnNames = s_emptyNameLists;
            GridTemplateRowNames = s_emptyNameLists;
            GridRow = new Line<GridPlacement>(GridPlacement.Auto, GridPlacement.Auto);
            GridColumn = new Line<GridPlacement>(GridPlacement.Auto, GridPlacement.Auto);
        }

        /// <summary>
        /// Creates a new Style with default values. Equivalent to new Style().
        /// </summary>
        public static Style Default => new Style();

        // =========================================================================
        // ICoreStyle implementation
        // =========================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        BoxGenerationMode ICoreStyle.BoxGenerationMode() => Display switch
        {
            Display.None => Taffy.BoxGenerationMode.None,
            _ => Taffy.BoxGenerationMode.Normal,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ICoreStyle.IsBlock() => Display == Display.Block;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ICoreStyle.IsCompressibleReplaced() => ItemIsReplaced;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        BoxSizing ICoreStyle.BoxSizing() => BoxSizingValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Point<Overflow> ICoreStyle.Overflow() => OverflowValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float ICoreStyle.ScrollbarWidth() => ScrollbarWidthValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Direction ICoreStyle.Direction() => DirectionValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Position ICoreStyle.Position() => PositionValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Rect<LengthPercentageAuto> ICoreStyle.Inset() => Inset;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Size<Dimension> ICoreStyle.Size() => SizeValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Size<Dimension> ICoreStyle.MinSize() => MinSizeValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Size<Dimension> ICoreStyle.MaxSize() => MaxSizeValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float? ICoreStyle.AspectRatio() => AspectRatioValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Rect<LengthPercentageAuto> ICoreStyle.Margin() => MarginValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Rect<LengthPercentage> ICoreStyle.Padding() => PaddingValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Rect<LengthPercentage> ICoreStyle.Border() => BorderValue;

        // =========================================================================
        // IBlockContainerStyle implementation
        // =========================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        TextAlign IBlockContainerStyle.TextAlign() => TextAlignValue;

        // =========================================================================
        // IBlockItemStyle implementation
        // =========================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IBlockItemStyle.IsTable() => ItemIsTable;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Float IBlockItemStyle.Float() => FloatValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Clear IBlockItemStyle.Clear() => ClearValue;

        // =========================================================================
        // IFlexboxContainerStyle implementation
        // =========================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        FlexDirection IFlexboxContainerStyle.FlexDirection() => FlexDirectionValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        FlexWrap IFlexboxContainerStyle.FlexWrap() => FlexWrapValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Size<LengthPercentage> IFlexboxContainerStyle.Gap() => GapValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        AlignContent? IFlexboxContainerStyle.AlignContent() => AlignContentValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        AlignItems? IFlexboxContainerStyle.AlignItems() => AlignItemsValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        AlignContent? IFlexboxContainerStyle.JustifyContent() => JustifyContentValue;

        // =========================================================================
        // IFlexboxItemStyle implementation
        // =========================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Dimension IFlexboxItemStyle.FlexBasis() => FlexBasisValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float IFlexboxItemStyle.FlexGrow() => FlexGrowValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float IFlexboxItemStyle.FlexShrink() => FlexShrinkValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        AlignItems? IFlexboxItemStyle.AlignSelf() => AlignSelfValue;

        // =========================================================================
        // IGridContainerStyle implementation
        // =========================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        GridAutoFlow IGridContainerStyle.GridAutoFlow() => GridAutoFlowValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Size<LengthPercentage> IGridContainerStyle.Gap() => GapValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        AlignContent? IGridContainerStyle.AlignContent() => AlignContentValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        AlignContent? IGridContainerStyle.JustifyContent() => JustifyContentValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        AlignItems? IGridContainerStyle.AlignItems() => AlignItemsValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        AlignItems? IGridContainerStyle.JustifyItems() => JustifyItemsValue;

        // =========================================================================
        // IGridItemStyle implementation
        // =========================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        AlignItems? IGridItemStyle.AlignSelf() => AlignSelfValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        AlignItems? IGridItemStyle.JustifySelf() => JustifySelfValue;

        // =========================================================================
        // Helper methods (grid accessors matching Rust's GridContainerStyle/GridItemStyle)
        // =========================================================================

        /// <summary>Gets the grid template rows definition</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ImmutableList<GridTemplateComponent> GetGridTemplateRows() => GridTemplateRows;

        /// <summary>Gets the grid template columns definition</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ImmutableList<GridTemplateComponent> GetGridTemplateColumns() => GridTemplateColumns;

        /// <summary>Gets the grid auto rows definition</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>> GetGridAutoRows() => GridAutoRows;

        /// <summary>Gets the grid auto columns definition</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>> GetGridAutoColumns() => GridAutoColumns;

        /// <summary>Gets the grid row placement for this item</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Line<GridPlacement> GetGridRow() => GridRow;

        /// <summary>Gets the grid column placement for this item</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Line<GridPlacement> GetGridColumn() => GridColumn;

        /// <summary>Gets the grid template areas</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ImmutableList<GridTemplateArea> GetGridTemplateAreas() => GridTemplateAreas;

        /// <summary>Gets the grid template column names</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ImmutableList<ImmutableList<string>> GetGridTemplateColumnNames() => GridTemplateColumnNames;

        /// <summary>Gets the grid template row names</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ImmutableList<ImmutableList<string>> GetGridTemplateRowNames() => GridTemplateRowNames;

        // =========================================================================
        // Public convenience accessors (non-interface, for direct use)
        // =========================================================================

        /// <summary>Which box generation mode should be used</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BoxGenerationMode GetBoxGenerationMode() => Display switch
        {
            Display.None => Taffy.BoxGenerationMode.None,
            _ => Taffy.BoxGenerationMode.Normal,
        };

        /// <summary>Is block layout?</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsBlock() => Display == Display.Block;

        /// <summary>Is it a compressible replaced element?</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCompressibleReplaced() => ItemIsReplaced;
    }
}
