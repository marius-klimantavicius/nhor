// Ported from taffy/src/style/mod.rs (CoreStyle trait and related enums)
// Contains the ICoreStyle interface and enums: Position, BoxSizing, Overflow, Display, BoxGenerationMode

using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// The positioning strategy for this item.
    /// </summary>
    public enum Position
    {
        /// The offset is computed relative to the final position given by the layout algorithm.
        Relative,
        /// The offset is computed relative to this item's closest positioned ancestor, if any.
        Absolute,
    }

    /// <summary>
    /// Specifies whether size styles for this node are assigned to the node's "content box" or "border box"
    /// </summary>
    public enum BoxSizing
    {
        /// Size styles specify the box's "border box" (the size excluding margin but including padding/border)
        BorderBox,
        /// Size styles specify the box's "content box" (the size excluding padding/border/margin)
        ContentBox,
    }

    /// <summary>
    /// How children overflowing their container should affect layout
    /// </summary>
    public enum Overflow
    {
        /// Content that overflows should contribute to the scroll region of its parent.
        Visible,
        /// Content that overflows should not contribute to the scroll region of its parent.
        Clip,
        /// The automatic minimum size of this node as a flexbox/grid item should be 0.
        Hidden,
        /// Like Hidden but space should be reserved for a scrollbar.
        Scroll,
    }

    public static class OverflowExtensions
    {
        /// <summary>
        /// Returns true for overflow modes that contain their contents (Hidden, Scroll)
        /// or else false (Visible, Clip).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsScrollContainer(this Overflow self)
        {
            return self == Overflow.Hidden || self == Overflow.Scroll;
        }

        /// <summary>
        /// Returns Some(0.0) if the overflow mode would cause the automatic minimum size of a Flexbox
        /// or CSS Grid item to be 0. Else returns None.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? MaybeIntoAutomaticMinSize(this Overflow self)
        {
            return self.IsScrollContainer() ? 0f : null;
        }
    }

    /// <summary>
    /// Sets the layout used for the children of this node
    /// </summary>
    public enum Display
    {
        /// The children will follow the block layout algorithm
        Block,
        /// The children will follow the flexbox layout algorithm
        Flex,
        /// The children will follow the CSS Grid layout algorithm
        Grid,
        /// The node is hidden, and its children will also be hidden
        None,
    }

    /// <summary>
    /// An abstracted version of the CSS display property
    /// </summary>
    public enum BoxGenerationMode
    {
        /// The node generates a box in the regular way
        Normal,
        /// The node and its descendants generate no boxes (they are hidden)
        None,
    }

    /// <summary>
    /// The core set of styles that are shared between all CSS layout nodes.
    /// Port of Rust's CoreStyle trait.
    /// </summary>
    /// <summary>
    /// The text/layout direction of a container
    /// </summary>
    public enum Direction
    {
        /// Left-to-right
        Ltr,
        /// Right-to-left
        Rtl,
    }

    public static class DirectionExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsRtl(this Direction d) => d == Direction.Rtl;
    }

    public interface ICoreStyle
    {
        /// <summary>Which box generation mode should be used</summary>
        BoxGenerationMode BoxGenerationMode() => Taffy.BoxGenerationMode.Normal;

        /// <summary>Is block layout?</summary>
        bool IsBlock() => false;

        /// <summary>Is it a compressible replaced element?</summary>
        bool IsCompressibleReplaced() => false;

        /// <summary>Which box do size styles apply to</summary>
        BoxSizing BoxSizing() => Taffy.BoxSizing.BorderBox;

        /// <summary>How children overflowing their container should affect layout</summary>
        Point<Overflow> Overflow() => new Point<Overflow>(Taffy.Overflow.Visible, Taffy.Overflow.Visible);

        /// <summary>How much space should be reserved for scrollbars</summary>
        float ScrollbarWidth() => 0f;

        /// <summary>The text/layout direction</summary>
        Direction Direction() => Taffy.Direction.Ltr;

        /// <summary>What should the position value of this struct use as a base offset?</summary>
        Position Position() => Taffy.Position.Relative;

        /// <summary>How should the position of this element be tweaked relative to the layout defined?</summary>
        Rect<LengthPercentageAuto> Inset() => new Rect<LengthPercentageAuto>(
            LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO,
            LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO);

        /// <summary>Sets the initial size of the item</summary>
        Size<Dimension> Size() => new Size<Dimension>(Dimension.AUTO, Dimension.AUTO);

        /// <summary>Controls the minimum size of the item</summary>
        Size<Dimension> MinSize() => new Size<Dimension>(Dimension.AUTO, Dimension.AUTO);

        /// <summary>Controls the maximum size of the item</summary>
        Size<Dimension> MaxSize() => new Size<Dimension>(Dimension.AUTO, Dimension.AUTO);

        /// <summary>Sets the preferred aspect ratio for the item (width / height)</summary>
        float? AspectRatio() => null;

        /// <summary>How large should the margin be on each side?</summary>
        Rect<LengthPercentageAuto> Margin() => new Rect<LengthPercentageAuto>(
            LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO,
            LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO);

        /// <summary>How large should the padding be on each side?</summary>
        Rect<LengthPercentage> Padding() => new Rect<LengthPercentage>(
            LengthPercentage.ZERO, LengthPercentage.ZERO,
            LengthPercentage.ZERO, LengthPercentage.ZERO);

        /// <summary>How large should the border be on each side?</summary>
        Rect<LengthPercentage> Border() => new Rect<LengthPercentage>(
            LengthPercentage.ZERO, LengthPercentage.ZERO,
            LengthPercentage.ZERO, LengthPercentage.ZERO);
    }
}
