// Ported from ThorVG/inc/thorvg.h
// All public enums and value-type structs from the ThorVG public API.
// Replaces the former thorvg.stubs.cs.

namespace ThorVG
{
    /// <summary>Result codes returned by ThorVG APIs.</summary>
    public enum Result
    {
        Success = 0,
        InvalidArguments,
        InsufficientCondition,
        FailedAllocation,
        MemoryCorruption,
        NonSupport,
        Unknown = 255
    }

    /// <summary>Pixel color-space layouts.</summary>
    public enum ColorSpace : byte
    {
        ABGR8888 = 0,
        ARGB8888,
        ABGR8888S,
        ARGB8888S,
        Grayscale8,
        Unknown = 255
    }

    /// <summary>Rendering engine behavior options.</summary>
    public enum EngineOption : byte
    {
        None = 0,
        Default = 1 << 0,
        SmartRender = 1 << 1
    }

    /// <summary>Path drawing commands.</summary>
    public enum PathCommand : byte
    {
        Close = 0,
        MoveTo,
        LineTo,
        CubicTo
    }

    /// <summary>Stroke cap styles for open sub-paths.</summary>
    public enum StrokeCap : byte
    {
        Butt = 0,
        Round,
        Square
    }

    /// <summary>Stroke join styles at corners of joined path segments.</summary>
    public enum StrokeJoin : byte
    {
        Miter = 0,
        Round,
        Bevel
    }

    /// <summary>How to fill outside gradient bounds.</summary>
    public enum FillSpread : byte
    {
        Pad = 0,
        Reflect,
        Repeat
    }

    /// <summary>Algorithm for determining inside of a shape.</summary>
    public enum FillRule : byte
    {
        NonZero = 0,
        EvenOdd
    }

    /// <summary>Image filtering method used during scaling or transformation.</summary>
    public enum FilterMethod : byte
    {
        Bilinear = 0,
        Nearest
    }

    /// <summary>Masking methods.</summary>
    public enum MaskMethod : byte
    {
        None = 0,
        Alpha,
        InvAlpha,
        Luma,
        InvLuma,
        Add,
        Subtract,
        Intersect,
        Difference,
        Lighten,
        Darken
    }

    /// <summary>Blending methods for paint composition.</summary>
    public enum BlendMethod : byte
    {
        Normal = 0,
        Multiply,
        Screen,
        Overlay,
        Darken,
        Lighten,
        ColorDodge,
        ColorBurn,
        HardLight,
        SoftLight,
        Difference,
        Exclusion,
        Hue,
        Saturation,
        Color,
        Luminosity,
        Add,
        Composition = 255
    }

    /// <summary>Scene post-processing effects.</summary>
    public enum SceneEffect : byte
    {
        Clear = 0,
        GaussianBlur,
        DropShadow,
        Fill,
        Tint,
        Tritone
    }

    /// <summary>Text wrapping modes.</summary>
    public enum TextWrap : byte
    {
        None = 0,
        Character,
        Word,
        Smart,
        Ellipsis
    }

    /// <summary>ThorVG class type identifiers.</summary>
    public enum Type : byte
    {
        Undefined = 0,
        Shape,
        Scene,
        Picture,
        Text,
        LinearGradient = 10,
        RadialGradient
    }

    /// <summary>2-D point (x, y).</summary>
    public struct Point
    {
        public float x;
        public float y;

        public Point(float x, float y)
        {
            this.x = x;
            this.y = y;
        }
    }

    /// <summary>3x3 affine transformation matrix.</summary>
    public struct Matrix
    {
        public float e11, e12, e13;
        public float e21, e22, e23;
        public float e31, e32, e33;

        public Matrix(float e11, float e12, float e13,
                      float e21, float e22, float e23,
                      float e31, float e32, float e33)
        {
            this.e11 = e11; this.e12 = e12; this.e13 = e13;
            this.e21 = e21; this.e22 = e22; this.e23 = e23;
            this.e31 = e31; this.e32 = e32; this.e33 = e33;
        }
    }

    /// <summary>Font metrics for text rendering.</summary>
    public struct TextMetrics
    {
        public float ascent;
        public float descent;
        public float linegap;
        public float advance;
    }

    /// <summary>Layout metrics of a glyph.</summary>
    public struct GlyphMetrics
    {
        public float advance;
        public float bearing;
        public Point min;
        public Point max;
    }
}
