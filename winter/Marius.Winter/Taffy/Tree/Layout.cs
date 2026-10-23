// Ported from taffy/src/tree/layout.rs
// Final data structures that represent the high-level UI layout

using System;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// Whether we are performing a full layout, or we merely need to size the node
    /// </summary>
    public enum RunMode
    {
        /// A full layout for this node and all children should be computed
        PerformLayout,
        /// The layout algorithm should be executed such that an accurate container size for the node can be determined.
        ComputeSize,
        /// This node should have a null layout set as it has been hidden
        PerformHiddenLayout,
    }

    /// <summary>
    /// Whether styles should be taken into account when computing size
    /// </summary>
    public enum SizingMode
    {
        /// Only content contributions should be taken into account
        ContentSize,
        /// Inherent size styles should be taken into account in addition to content contributions
        InherentSize,
    }

    /// <summary>
    /// A set of margins that are available for collapsing with for block layout's margin collapsing
    /// </summary>
    public struct CollapsibleMarginSet : IEquatable<CollapsibleMarginSet>
    {
        public readonly bool Equals(CollapsibleMarginSet other) => Positive == other.Positive && Negative == other.Negative;
        public override readonly bool Equals(object? value) => value is CollapsibleMarginSet other && Equals(other);
        public override readonly int GetHashCode() => HashCode.Combine(Positive, Negative);
        public static bool operator ==(CollapsibleMarginSet left, CollapsibleMarginSet right) => left.Equals(right);
        public static bool operator !=(CollapsibleMarginSet left, CollapsibleMarginSet right) => !left.Equals(right);

        /// <summary>The largest positive margin</summary>
        public float Positive;
        /// <summary>The smallest negative margin (with largest absolute value)</summary>
        public float Negative;

        /// <summary>A default margin set with no collapsible margins</summary>
        public static readonly CollapsibleMarginSet ZERO = new CollapsibleMarginSet { Positive = 0f, Negative = 0f };

        /// <summary>Create a set from a single margin</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CollapsibleMarginSet FromMargin(float margin)
        {
            if (margin >= 0f)
                return new CollapsibleMarginSet { Positive = margin, Negative = 0f };
            else
                return new CollapsibleMarginSet { Positive = 0f, Negative = margin };
        }

        /// <summary>Collapse a single margin with this set</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly CollapsibleMarginSet CollapseWithMargin(float margin)
        {
            return margin >= 0f
                ? new() { Positive = TaffySys.F32Max(Positive, margin), Negative = Negative }
                : new() { Positive = Positive, Negative = TaffySys.F32Min(Negative, margin) };
        }

        /// <summary>Collapse another margin set with this set</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly CollapsibleMarginSet CollapseWithSet(CollapsibleMarginSet other)
        {
            return new() { Positive = TaffySys.F32Max(Positive, other.Positive), Negative = TaffySys.F32Min(Negative, other.Negative) };
        }

        /// <summary>Resolve the resultant margin from this set</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Resolve()
        {
            return Positive + Negative;
        }
    }

    /// <summary>
    /// An axis that layout algorithms can be requested to compute a size for
    /// </summary>
    public enum RequestedAxis
    {
        /// The horizontal axis
        Horizontal,
        /// The vertical axis
        Vertical,
        /// Both axes
        Both,
    }

    /// <summary>
    /// A struct containing the inputs constraints/hints for laying out a node
    /// </summary>
    public struct LayoutInput : IEquatable<LayoutInput>
    {
        public readonly bool Equals(LayoutInput other) => RunMode == other.RunMode && SizingMode == other.SizingMode && Axis == other.Axis && KnownDimensions == other.KnownDimensions && (Size<bool>)KnownDimensionsAreDefinite == (Size<bool>)other.KnownDimensionsAreDefinite && ParentSize == other.ParentSize && AvailableSpace == other.AvailableSpace && VerticalMarginsAreCollapsible == other.VerticalMarginsAreCollapsible;
        public override readonly bool Equals(object? value) => value is LayoutInput other && Equals(other);
        public override readonly int GetHashCode() => HashCode.Combine(RunMode, SizingMode, Axis, KnownDimensions, (Size<bool>)KnownDimensionsAreDefinite, ParentSize, AvailableSpace, VerticalMarginsAreCollapsible);
        public static bool operator ==(LayoutInput left, LayoutInput right) => left.Equals(right);
        public static bool operator !=(LayoutInput left, LayoutInput right) => !left.Equals(right);

        public LayoutInput()
        {
            this = default;
            KnownDimensionsAreDefinite = new Size<bool>(true, true);
        }

        /// <summary>Swap axes; margins cannot collapse across a change of block axis.</summary>
        public LayoutInput Transpose() => new LayoutInput
        {
            RunMode = RunMode,
            SizingMode = SizingMode,
            Axis = Axis.Transpose(),
            KnownDimensions = KnownDimensions.Transpose(),
            KnownDimensionsAreDefinite = KnownDimensionsAreDefinite.Transpose(),
            ParentSize = ParentSize.Transpose(),
            AvailableSpace = AvailableSpace.Transpose(),
            VerticalMarginsAreCollapsible = new Line<bool>(false, false),
        };

        /// <summary>Whether we only need to know the Node's size, or whether we need to perform a full layout</summary>
        public RunMode RunMode;
        /// <summary>Whether a Node's style sizes should be taken into account or ignored</summary>
        public SizingMode SizingMode;
        /// <summary>Which axis we need the size of</summary>
        public RequestedAxis Axis;
        /// <summary>Known dimensions represent dimensions which should be taken as fixed when performing layout</summary>
        public Size<float?> KnownDimensions;
        /// <summary>Whether each imposed known dimension is definite for descendant percentage resolution.</summary>
        public DefiniteDimensions KnownDimensionsAreDefinite;
        /// <summary>Parent size dimensions are intended to be used for percentage resolution</summary>
        public Size<float?> ParentSize;
        /// <summary>Available space represents an amount of space to layout into</summary>
        public Size<AvailableSpace> AvailableSpace;
        /// <summary>Specific to CSS Block layout. Used for correctly computing margin collapsing.</summary>
        public Line<bool> VerticalMarginsAreCollapsible;

        /// <summary>A LayoutInput that can be used to request hidden layout</summary>
        public static readonly LayoutInput HIDDEN = new LayoutInput
        {
            RunMode = RunMode.PerformHiddenLayout,
            KnownDimensions = SizeExtensions.NoneF32,
            ParentSize = SizeExtensions.NoneF32,
            AvailableSpace = new Size<AvailableSpace>(Taffy.AvailableSpace.MAX_CONTENT, Taffy.AvailableSpace.MAX_CONTENT),
            SizingMode = SizingMode.InherentSize,
            Axis = RequestedAxis.Both,
            VerticalMarginsAreCollapsible = new Line<bool>(false, false),
        };
    }

    /// <summary>
    /// A struct containing the result of laying a single node
    /// </summary>
    public struct LayoutOutput : IEquatable<LayoutOutput>
    {
        public readonly bool Equals(LayoutOutput other) => Size == other.Size && ScrollableOverflowRect == other.ScrollableOverflowRect && FirstBaselines == other.FirstBaselines && LastBaseline == other.LastBaseline && TopMargin.Equals(other.TopMargin) && BottomMargin.Equals(other.BottomMargin) && MarginsCanCollapseThrough == other.MarginsCanCollapseThrough && OofCandidates.Equals(other.OofCandidates) && OofPositioningArea == other.OofPositioningArea;
        public override readonly bool Equals(object? value) => value is LayoutOutput other && Equals(other);
        public override readonly int GetHashCode() => HashCode.Combine(Size, ScrollableOverflowRect, FirstBaselines, LastBaseline, TopMargin, BottomMargin, MarginsCanCollapseThrough, HashCode.Combine(OofCandidates, OofPositioningArea));
        public static bool operator ==(LayoutOutput left, LayoutOutput right) => left.Equals(right);
        public static bool operator !=(LayoutOutput left, LayoutOutput right) => !left.Equals(right);

        /// <summary>The size of the node</summary>
        public Size<float> Size;
        /// <summary>The size of the content within the node</summary>
        public Rect<float> ScrollableOverflowRect;
        public Size<float> ContentSize
        {
            readonly get => new(ScrollableOverflowRect.Right, ScrollableOverflowRect.Bottom);
            set => ScrollableOverflowRect = new(0, value.Width, 0, value.Height);
        }
        /// <summary>The first baseline of the node in each dimension, if any</summary>
        public Point<float?> FirstBaselines;
        public Baselines Baselines
        {
            readonly get => new(FirstBaselines.Y, LastBaseline);
            set { FirstBaselines = new(null, value.First); LastBaseline = value.Last; }
        }
        public float? LastBaseline;
        public OofCandidates OofCandidates;
        public OofPositioningArea? OofPositioningArea;
        public readonly LayoutOutput Clone()
        {
            var copy = this;
            copy.OofCandidates = OofCandidates.Clone();
            return copy;
        }
        public readonly LayoutOutput Transpose()
        {
            var copy = Clone();
            copy.Size = Size.Transpose();
            copy.ScrollableOverflowRect = ScrollableOverflowRect.Transpose();
            copy.Baselines = Baselines.None;
            copy.TopMargin = copy.BottomMargin = CollapsibleMarginSet.ZERO;
            copy.MarginsCanCollapseThrough = false;
            if (OofPositioningArea is { } area)
                copy.OofPositioningArea = new(area.Size.Transpose(), new(area.Offset.Y, area.Offset.X));
            return copy;
        }
        /// <summary>Top margin that can be collapsed with</summary>
        public CollapsibleMarginSet TopMargin;
        /// <summary>Bottom margin that can be collapsed with</summary>
        public CollapsibleMarginSet BottomMargin;
        /// <summary>Whether margins can be collapsed through this node</summary>
        public bool MarginsCanCollapseThrough;

        /// <summary>An all-zero LayoutOutput for hidden nodes</summary>
        public static readonly LayoutOutput HIDDEN = new LayoutOutput
        {
            Size = SizeExtensions.ZeroF32,
            ContentSize = SizeExtensions.ZeroF32,
            FirstBaselines = PointExtensions.NoneF32,
            TopMargin = CollapsibleMarginSet.ZERO,
            BottomMargin = CollapsibleMarginSet.ZERO,
            MarginsCanCollapseThrough = false,
        };

        /// <summary>A blank layout output</summary>
        public static readonly LayoutOutput DEFAULT = HIDDEN;

        /// <summary>Constructor to create a LayoutOutput from sizes and baselines</summary>
        public static LayoutOutput FromSizesAndBaselines(Size<float> size, Size<float> contentSize, Point<float?> firstBaselines)
        {
            return new LayoutOutput
            {
                Size = size,
                ContentSize = contentSize,
                FirstBaselines = firstBaselines,
                TopMargin = CollapsibleMarginSet.ZERO,
                BottomMargin = CollapsibleMarginSet.ZERO,
                MarginsCanCollapseThrough = false,
            };
        }

        public static LayoutOutput FromSizesAndBaselines(Size<float> size, Rect<float> overflow, Baselines baselines)
            => new() { Size = size, ScrollableOverflowRect = overflow, Baselines = baselines };
        public static LayoutOutput FromSizes(Size<float> size, Rect<float> overflow)
            => FromSizesAndBaselines(size, overflow, Baselines.None);

        /// <summary>Construct a LayoutOutput from just the container and content sizes</summary>
        public static LayoutOutput FromSizes(Size<float> size, Size<float> contentSize)
        {
            return FromSizesAndBaselines(size, contentSize, PointExtensions.NoneF32);
        }

        /// <summary>Construct a LayoutOutput from just the container's size</summary>
        public static LayoutOutput FromOuterSize(Size<float> size)
        {
            return FromSizes(size, SizeExtensions.ZeroF32);
        }
    }

    /// <summary>
    /// The final result of a layout algorithm for a single node.
    /// </summary>
    public struct Layout : IEquatable<Layout>
    {
        public readonly bool Equals(Layout other) => Order == other.Order && Location == other.Location && Size == other.Size && ScrollableOverflowRect == other.ScrollableOverflowRect && ScrollbarSize == other.ScrollbarSize && Border == other.Border && Padding == other.Padding && Margin == other.Margin;
        public override readonly bool Equals(object? value) => value is Layout other && Equals(other);
        public override readonly int GetHashCode() => HashCode.Combine(Order, Location, Size, ScrollableOverflowRect, ScrollbarSize, Border, Padding, Margin);
        public static bool operator ==(Layout left, Layout right) => left.Equals(right);
        public static bool operator !=(Layout left, Layout right) => !left.Equals(right);

        /// <summary>The relative ordering of the node</summary>
        public uint Order;
        /// <summary>The top-left corner of the node</summary>
        public Point<float> Location;
        /// <summary>The width and height of the node</summary>
        public Size<float> Size;
        /// <summary>The width and height of the content inside the node</summary>
        public Rect<float> ScrollableOverflowRect;
        public Size<float> ContentSize
        {
            readonly get => new(ScrollableOverflowRect.Right, ScrollableOverflowRect.Bottom);
            set => ScrollableOverflowRect = new(0, value.Width, 0, value.Height);
        }
        /// <summary>The size of the scrollbars in each dimension</summary>
        public Size<float> ScrollbarSize;
        /// <summary>The size of the borders of the node</summary>
        public Rect<float> Border;
        /// <summary>The size of the padding of the node</summary>
        public Rect<float> Padding;
        /// <summary>The size of the margin of the node</summary>
        public Rect<float> Margin;

        /// <summary>Creates a new zero-Layout</summary>
        public static Layout New() => new Layout();

        /// <summary>Creates a new zero-Layout with the supplied order value</summary>
        public static Layout WithOrder(uint order) => new Layout { Order = order };

        /// <summary>Get the width of the node's content box</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ContentBoxWidth()
        {
            return Size.Width - Padding.Left - Padding.Right - Border.Left - Border.Right;
        }

        /// <summary>Get the height of the node's content box</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ContentBoxHeight()
        {
            return Size.Height - Padding.Top - Padding.Bottom - Border.Top - Border.Bottom;
        }

        /// <summary>Get the size of the node's content box</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Size<float> ContentBoxSize()
        {
            return new Size<float>(ContentBoxWidth(), ContentBoxHeight());
        }

        /// <summary>Get x offset of the node's content box relative to its parent's border box</summary>
        public float ContentBoxX()
        {
            return Location.X + Border.Left + Padding.Left;
        }

        /// <summary>Get y offset of the node's content box relative to its parent's border box</summary>
        public float ContentBoxY()
        {
            return Location.Y + Border.Top + Padding.Top;
        }

        /// <summary>Return the scroll width of the node</summary>
        public float ScrollWidth()
        {
            return TaffySys.F32Max(0f,
                ScrollableOverflowRect.Right + TaffySys.F32Min(ScrollbarSize.Width, Size.Width) - Size.Width + Border.Left + Border.Right);
        }

        /// <summary>Return the scroll height of the node</summary>
        public float ScrollHeight()
        {
            return TaffySys.F32Max(0f,
                ScrollableOverflowRect.Bottom + TaffySys.F32Min(ScrollbarSize.Height, Size.Height) - Size.Height + Border.Top + Border.Bottom);
        }
    }
}
