// Ported from taffy/src/geometry.rs
// Geometric primitives useful for layout

using System;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// The simple absolute horizontal and vertical axis
    /// </summary>
    public enum AbsoluteAxis
    {
        /// The horizontal axis
        Horizontal,
        /// The vertical axis
        Vertical,
    }

    public static class AbsoluteAxisExtensions
    {
        /// <summary>
        /// Returns the other variant of the enum
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AbsoluteAxis OtherAxis(this AbsoluteAxis self)
        {
            return self switch
            {
                AbsoluteAxis.Horizontal => AbsoluteAxis.Vertical,
                AbsoluteAxis.Vertical => AbsoluteAxis.Horizontal,
                _ => throw new ArgumentOutOfRangeException(nameof(self)),
            };
        }
    }

    /// <summary>
    /// The CSS abstract axis.
    /// https://www.w3.org/TR/css-writing-modes-3/#abstract-axes
    /// </summary>
    public enum AbstractAxis
    {
        /// The axis in the inline dimension
        Inline,
        /// The axis in the block dimension
        Block,
    }

    public static class AbstractAxisExtensions
    {
        /// <summary>
        /// Returns the other variant of the enum
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AbstractAxis Other(this AbstractAxis self)
        {
            return self switch
            {
                AbstractAxis.Inline => AbstractAxis.Block,
                AbstractAxis.Block => AbstractAxis.Inline,
                _ => throw new ArgumentOutOfRangeException(nameof(self)),
            };
        }

        /// <summary>
        /// Convert an AbstractAxis into an AbsoluteAxis naively assuming that the Inline axis is Horizontal.
        /// This is currently always true, but will change if Taffy ever implements the writing_mode property.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AbsoluteAxis AsAbsNaive(this AbstractAxis self)
        {
            return self switch
            {
                AbstractAxis.Inline => AbsoluteAxis.Horizontal,
                AbstractAxis.Block => AbsoluteAxis.Vertical,
                _ => throw new ArgumentOutOfRangeException(nameof(self)),
            };
        }
    }

    /// <summary>
    /// Container that holds an item in each absolute axis without specifying what kind of item it is.
    /// </summary>
    public struct InBothAbsAxis<T>
    {
        /// The item in the horizontal axis
        public T Horizontal;
        /// The item in the vertical axis
        public T Vertical;

        public InBothAbsAxis(T horizontal, T vertical)
        {
            Horizontal = horizontal;
            Vertical = vertical;
        }

        /// <summary>
        /// Get the contained item based on the AbsoluteAxis passed
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get(AbsoluteAxis axis)
        {
            return axis switch
            {
                AbsoluteAxis.Horizontal => Horizontal,
                AbsoluteAxis.Vertical => Vertical,
                _ => throw new ArgumentOutOfRangeException(nameof(axis)),
            };
        }
    }

    /// <summary>
    /// An axis-aligned UI rectangle
    /// </summary>
    public struct Rect<T>
    {
        /// The left (or start) edge
        public T Left;
        /// The right (or end) edge
        public T Right;
        /// The top edge
        public T Top;
        /// The bottom edge
        public T Bottom;

        public Rect(T left, T right, T top, T bottom)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }

        /// <summary>
        /// Applies the function f to the left, right, top, and bottom properties
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rect<R> Map<R>(Func<T, R> f)
        {
            return new Rect<R>(f(Left), f(Right), f(Top), f(Bottom));
        }

        /// <summary>
        /// Applies the function f to all four sides of the rect.
        /// When applied to the left and right sides, the width is used as the second parameter.
        /// When applied to the top or bottom sides, the height is used instead.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rect<R> ZipSize<R, U>(Size<U> size, Func<T, U, R> f)
        {
            return new Rect<R>(
                f(Left, size.Width),
                f(Right, size.Width),
                f(Top, size.Height),
                f(Bottom, size.Height)
            );
        }

        /// <summary>
        /// Returns a Line representing the left and right properties of the Rect
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Line<T> HorizontalComponents()
        {
            return new Line<T>(Left, Right);
        }

        /// <summary>
        /// Returns a Line containing the top and bottom properties of the Rect
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Line<T> VerticalComponents()
        {
            return new Line<T>(Top, Bottom);
        }

        public override string ToString() => $"Rect {{ Left = {Left}, Right = {Right}, Top = {Top}, Bottom = {Bottom} }}";
    }

    /// <summary>
    /// Extension methods for Rect with numeric types
    /// </summary>
    public static class RectExtensions
    {
        /// <summary>
        /// Creates a new Rect with 0.0 as all parameters
        /// </summary>
        public static readonly Rect<float> ZeroF32 = new Rect<float>(0.0f, 0.0f, 0.0f, 0.0f);

        /// <summary>
        /// Creates a new Rect
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect<float> NewF32(float start, float end, float top, float bottom)
        {
            return new Rect<float>(start, end, top, bottom);
        }

        /// <summary>
        /// The sum of left and right
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float HorizontalAxisSum(this Rect<float> self)
        {
            return self.Left + self.Right;
        }

        /// <summary>
        /// The sum of top and bottom
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float VerticalAxisSum(this Rect<float> self)
        {
            return self.Top + self.Bottom;
        }

        /// <summary>
        /// Both horizontal_axis_sum and vertical_axis_sum as a Size
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float> SumAxes(this Rect<float> self)
        {
            return new Size<float>(self.HorizontalAxisSum(), self.VerticalAxisSum());
        }

        /// <summary>
        /// The sum of the two fields of the Rect representing the main axis.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MainAxisSum(this Rect<float> self, FlexDirection direction)
        {
            return direction.IsRow() ? self.HorizontalAxisSum() : self.VerticalAxisSum();
        }

        /// <summary>
        /// The sum of the two fields of the Rect representing the cross axis.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CrossAxisSum(this Rect<float> self, FlexDirection direction)
        {
            return direction.IsRow() ? self.VerticalAxisSum() : self.HorizontalAxisSum();
        }

        /// <summary>
        /// The start or top value of the Rect, from the perspective of the main layout axis
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MainStart(this Rect<float> self, FlexDirection direction)
        {
            return direction.IsRow() ? self.Left : self.Top;
        }

        /// <summary>
        /// The end or bottom value of the Rect, from the perspective of the main layout axis
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MainEnd(this Rect<float> self, FlexDirection direction)
        {
            return direction.IsRow() ? self.Right : self.Bottom;
        }

        /// <summary>
        /// The start or top value of the Rect, from the perspective of the cross layout axis
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CrossStart(this Rect<float> self, FlexDirection direction)
        {
            return direction.IsRow() ? self.Top : self.Left;
        }

        /// <summary>
        /// The end or bottom value of the Rect, from the perspective of the cross layout axis
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CrossEnd(this Rect<float> self, FlexDirection direction)
        {
            return direction.IsRow() ? self.Bottom : self.Right;
        }

        /// <summary>
        /// Get either the horizontal or vertical sum depending on the AbsoluteAxis passed in
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GridAxisSum(this Rect<float> self, AbsoluteAxis axis)
        {
            return axis switch
            {
                AbsoluteAxis.Horizontal => self.Left + self.Right,
                AbsoluteAxis.Vertical => self.Top + self.Bottom,
                _ => throw new ArgumentOutOfRangeException(nameof(axis)),
            };
        }

        /// <summary>
        /// Add two Rect of float together component-wise
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect<float> Add(this Rect<float> self, Rect<float> rhs)
        {
            return new Rect<float>(
                self.Left + rhs.Left,
                self.Right + rhs.Right,
                self.Top + rhs.Top,
                self.Bottom + rhs.Bottom
            );
        }

        // --- Rect<float?> extensions ---

        /// <summary>
        /// The sum of left and right (treating null as 0)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float HorizontalAxisSum(this Rect<float?> self)
        {
            return (self.Left ?? 0f) + (self.Right ?? 0f);
        }

        /// <summary>
        /// The sum of top and bottom (treating null as 0)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float VerticalAxisSum(this Rect<float?> self)
        {
            return (self.Top ?? 0f) + (self.Bottom ?? 0f);
        }

        /// <summary>
        /// Both horizontal_axis_sum and vertical_axis_sum as a Size
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float> SumAxes(this Rect<float?> self)
        {
            return new Size<float>(self.HorizontalAxisSum(), self.VerticalAxisSum());
        }

        /// <summary>
        /// The sum of the two fields of the Rect representing the main axis.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MainAxisSum(this Rect<float?> self, FlexDirection direction)
        {
            return direction.IsRow() ? self.HorizontalAxisSum() : self.VerticalAxisSum();
        }

        /// <summary>
        /// The sum of the two fields of the Rect representing the cross axis.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CrossAxisSum(this Rect<float?> self, FlexDirection direction)
        {
            return direction.IsRow() ? self.VerticalAxisSum() : self.HorizontalAxisSum();
        }

        /// <summary>
        /// The start or top value of the Rect, from the perspective of the main layout axis
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? MainStart(this Rect<float?> self, FlexDirection direction)
        {
            return direction.IsRow() ? self.Left : self.Top;
        }

        /// <summary>
        /// The end or bottom value of the Rect, from the perspective of the main layout axis
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? MainEnd(this Rect<float?> self, FlexDirection direction)
        {
            return direction.IsRow() ? self.Right : self.Bottom;
        }

        /// <summary>
        /// The start or top value of the Rect, from the perspective of the cross layout axis
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? CrossStart(this Rect<float?> self, FlexDirection direction)
        {
            return direction.IsRow() ? self.Top : self.Left;
        }

        /// <summary>
        /// The end or bottom value of the Rect, from the perspective of the cross layout axis
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? CrossEnd(this Rect<float?> self, FlexDirection direction)
        {
            return direction.IsRow() ? self.Bottom : self.Right;
        }

        /// <summary>
        /// Get either the horizontal or vertical sum depending on the AbsoluteAxis passed in
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GridAxisSum(this Rect<float?> self, AbsoluteAxis axis)
        {
            return axis switch
            {
                AbsoluteAxis.Horizontal => (self.Left ?? 0f) + (self.Right ?? 0f),
                AbsoluteAxis.Vertical => (self.Top ?? 0f) + (self.Bottom ?? 0f),
                _ => throw new ArgumentOutOfRangeException(nameof(axis)),
            };
        }
    }

    /// <summary>
    /// An abstract "line". Represents any type that has a start and an end
    /// </summary>
    public struct Line<T>
    {
        /// The start position of a line
        public T Start;
        /// The end position of a line
        public T End;

        public Line(T start, T end)
        {
            Start = start;
            End = end;
        }

        /// <summary>
        /// Applies the function f to both the start and end
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Line<R> Map<R>(Func<T, R> f)
        {
            return new Line<R>(f(Start), f(End));
        }

        public override string ToString() => $"Line {{ Start = {Start}, End = {End} }}";
    }

    public static class LineExtensions
    {
        /// <summary>
        /// A Line of bool with both start and end set to true
        /// </summary>
        public static readonly Line<bool> TrueLine = new Line<bool>(true, true);

        /// <summary>
        /// A Line of bool with both start and end set to false
        /// </summary>
        public static readonly Line<bool> FalseLine = new Line<bool>(false, false);

        /// <summary>
        /// Adds the start and end values together and returns the result
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sum(this Line<float> self)
        {
            return self.Start + self.End;
        }
    }

    /// <summary>
    /// The width and height of a Rect
    /// </summary>
    public struct Size<T>
    {
        /// The x extent of the rectangle
        public T Width;
        /// The y extent of the rectangle
        public T Height;

        public Size(T width, T height)
        {
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Applies the function f to both the width and height
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Size<R> Map<R>(Func<T, R> f)
        {
            return new Size<R>(f(Width), f(Height));
        }

        /// <summary>
        /// Applies the function f to the width
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Size<T> MapWidth(Func<T, T> f)
        {
            return new Size<T>(f(Width), Height);
        }

        /// <summary>
        /// Applies the function f to the height
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Size<T> MapHeight(Func<T, T> f)
        {
            return new Size<T>(Width, f(Height));
        }

        /// <summary>
        /// Applies the function f to both the width and height of this value and another passed value
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Size<R> ZipMap<TOther, R>(Size<TOther> other, Func<T, TOther, R> f)
        {
            return new Size<R>(f(Width, other.Width), f(Height, other.Height));
        }

        /// <summary>
        /// Get either the width or height depending on the AbsoluteAxis passed in
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetAbs(AbsoluteAxis axis)
        {
            return axis switch
            {
                AbsoluteAxis.Horizontal => Width,
                AbsoluteAxis.Vertical => Height,
                _ => throw new ArgumentOutOfRangeException(nameof(axis)),
            };
        }

        /// <summary>
        /// Gets the extent of the specified layout axis
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get(AbstractAxis axis)
        {
            return axis switch
            {
                AbstractAxis.Inline => Width,
                AbstractAxis.Block => Height,
                _ => throw new ArgumentOutOfRangeException(nameof(axis)),
            };
        }

        /// <summary>
        /// Sets the extent of the specified layout axis
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(AbstractAxis axis, T value)
        {
            switch (axis)
            {
                case AbstractAxis.Inline: Width = value; break;
                case AbstractAxis.Block: Height = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(axis));
            }
        }

        // --- FlexDirection-based accessors ---

        /// <summary>
        /// Gets the extent of the main layout axis
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Main(FlexDirection direction)
        {
            return direction.IsRow() ? Width : Height;
        }

        /// <summary>
        /// Gets the extent of the cross layout axis
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Cross(FlexDirection direction)
        {
            return direction.IsRow() ? Height : Width;
        }

        /// <summary>
        /// Sets the extent of the main layout axis
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetMain(FlexDirection direction, T value)
        {
            if (direction.IsRow())
                Width = value;
            else
                Height = value;
        }

        /// <summary>
        /// Sets the extent of the cross layout axis
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetCross(FlexDirection direction, T value)
        {
            if (direction.IsRow())
                Height = value;
            else
                Width = value;
        }

        /// <summary>
        /// Creates a new value with the main axis set to the provided value
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Size<T> WithMain(FlexDirection direction, T value)
        {
            var result = this;
            if (direction.IsRow())
                result.Width = value;
            else
                result.Height = value;
            return result;
        }

        /// <summary>
        /// Creates a new value with the cross axis set to the provided value
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Size<T> WithCross(FlexDirection direction, T value)
        {
            var result = this;
            if (direction.IsRow())
                result.Height = value;
            else
                result.Width = value;
            return result;
        }

        /// <summary>
        /// Creates a new value with the main axis modified by the callback provided
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Size<T> MapMain(FlexDirection direction, Func<T, T> mapper)
        {
            var result = this;
            if (direction.IsRow())
                result.Width = mapper(result.Width);
            else
                result.Height = mapper(result.Height);
            return result;
        }

        /// <summary>
        /// Creates a new value with the cross axis modified by the callback provided
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Size<T> MapCross(FlexDirection direction, Func<T, T> mapper)
        {
            var result = this;
            if (direction.IsRow())
                result.Height = mapper(result.Height);
            else
                result.Width = mapper(result.Width);
            return result;
        }

        public override string ToString() => $"Size {{ Width = {Width}, Height = {Height} }}";
    }

    public static class SizeExtensions
    {
        /// <summary>
        /// A Size with zero width and height
        /// </summary>
        public static readonly Size<float> ZeroF32 = new Size<float>(0.0f, 0.0f);

        /// <summary>
        /// A Size with None width and height
        /// </summary>
        public static readonly Size<float?> NoneF32 = new Size<float?>(null, null);

        /// <summary>
        /// Applies f32_max to each component separately
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float> F32Max(this Size<float> self, Size<float> rhs)
        {
            return new Size<float>(
                MathF.Max(self.Width, rhs.Width),
                MathF.Max(self.Height, rhs.Height)
            );
        }

        /// <summary>
        /// Applies f32_min to each component separately
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float> F32Min(this Size<float> self, Size<float> rhs)
        {
            return new Size<float>(
                MathF.Min(self.Width, rhs.Width),
                MathF.Min(self.Height, rhs.Height)
            );
        }

        /// <summary>
        /// Return true if both width and height are greater than 0 else false
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasNonZeroArea(this Size<float> self)
        {
            return self.Width > 0.0f && self.Height > 0.0f;
        }

        /// <summary>
        /// Add two sizes together component-wise
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float> Add(this Size<float> self, Size<float> rhs)
        {
            return new Size<float>(self.Width + rhs.Width, self.Height + rhs.Height);
        }

        /// <summary>
        /// Subtract two sizes component-wise
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float> Sub(this Size<float> self, Size<float> rhs)
        {
            return new Size<float>(self.Width - rhs.Width, self.Height - rhs.Height);
        }

        // --- Size<float?> extensions ---

        /// <summary>
        /// Creates a Size with Some(width) and Some(height)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float?> NewOptionF32(float width, float height)
        {
            return new Size<float?>(width, height);
        }

        /// <summary>
        /// Creates a new Size with either the width or height set based on the provided direction
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float?> FromCross(FlexDirection direction, float? value)
        {
            var result = NoneF32;
            if (direction.IsRow())
                result.Height = value;
            else
                result.Width = value;
            return result;
        }

        /// <summary>
        /// Applies aspect_ratio (if one is supplied) to the Size:
        /// - If width is Some but height is None, then height is computed from width and aspect_ratio
        /// - If height is Some but width is None, then width is computed from height and aspect_ratio
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float?> MaybeApplyAspectRatio(this Size<float?> self, float? aspectRatio)
        {
            if (!aspectRatio.HasValue)
                return self;

            float ratio = aspectRatio.Value;
            if (self.Width.HasValue && !self.Height.HasValue)
                return new Size<float?>(self.Width, self.Width.Value / ratio);
            if (!self.Width.HasValue && self.Height.HasValue)
                return new Size<float?>(self.Height.Value * ratio, self.Height);
            return self;
        }

        /// <summary>
        /// Performs unwrap_or on each component separately
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float> UnwrapOr(this Size<float?> self, Size<float> alt)
        {
            return new Size<float>(
                self.Width ?? alt.Width,
                self.Height ?? alt.Height
            );
        }

        /// <summary>
        /// Performs or on each component separately
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float?> Or(this Size<float?> self, Size<float?> alt)
        {
            return new Size<float?>(
                self.Width ?? alt.Width,
                self.Height ?? alt.Height
            );
        }

        /// <summary>
        /// Return true if both components are Some, else false.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool BothAxisDefined(this Size<float?> self)
        {
            return self.Width.HasValue && self.Height.HasValue;
        }
    }

    /// <summary>
    /// A 2-dimensional coordinate.
    /// When used in association with a Rect, represents the top-left corner.
    /// </summary>
    public struct Point<T>
    {
        /// The x-coordinate
        public T X;
        /// The y-coordinate
        public T Y;

        public Point(T x, T y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Applies the function f to both the x and y
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Point<R> Map<R>(Func<T, R> f)
        {
            return new Point<R>(f(X), f(Y));
        }

        /// <summary>
        /// Gets the extent of the specified layout axis
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get(AbstractAxis axis)
        {
            return axis switch
            {
                AbstractAxis.Inline => X,
                AbstractAxis.Block => Y,
                _ => throw new ArgumentOutOfRangeException(nameof(axis)),
            };
        }

        /// <summary>
        /// Swap x and y components
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Point<T> Transpose()
        {
            return new Point<T>(Y, X);
        }

        /// <summary>
        /// Sets the extent of the specified layout axis
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(AbstractAxis axis, T value)
        {
            switch (axis)
            {
                case AbstractAxis.Inline: X = value; break;
                case AbstractAxis.Block: Y = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(axis));
            }
        }

        /// <summary>
        /// Gets the component in the main layout axis
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Main(FlexDirection direction)
        {
            return direction.IsRow() ? X : Y;
        }

        /// <summary>
        /// Gets the component in the cross layout axis
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Cross(FlexDirection direction)
        {
            return direction.IsRow() ? Y : X;
        }

        /// <summary>
        /// Converts a Point to a Size
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Size<T> ToSize()
        {
            return new Size<T>(X, Y);
        }

        public override string ToString() => $"Point {{ X = {X}, Y = {Y} }}";
    }

    public static class PointExtensions
    {
        /// <summary>
        /// A Point with values (0,0), representing the origin
        /// </summary>
        public static readonly Point<float> ZeroF32 = new Point<float>(0.0f, 0.0f);

        /// <summary>
        /// A Point with values (None, None)
        /// </summary>
        public static readonly Point<float?> NoneF32 = new Point<float?>(null, null);

        /// <summary>
        /// Add two points together component-wise
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point<float> Add(this Point<float> self, Point<float> rhs)
        {
            return new Point<float>(self.X + rhs.X, self.Y + rhs.Y);
        }
    }

    /// <summary>
    /// Generic struct which holds a "min" value and a "max" value
    /// </summary>
    public struct MinMax<TMin, TMax>
    {
        /// The value representing the minimum
        public TMin Min;
        /// The value representing the maximum
        public TMax Max;

        public MinMax(TMin min, TMax max)
        {
            Min = min;
            Max = max;
        }

        public override string ToString() => $"MinMax {{ Min = {Min}, Max = {Max} }}";
    }
}
