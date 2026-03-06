// Ported from ThorVG/src/loaders/svg/tvgSvgLoaderCommon.h
// SVG DOM types: enums, structs, and classes.

using System;
using System.Collections.Generic;

namespace ThorVG
{
    // Alias: using SvgColor = tvg::RGB;
    // We use RGB directly.

    public struct Box
    {
        public float x, y, w, h;

        public Box(float x, float y, float w, float h)
        {
            this.x = x; this.y = y; this.w = w; this.h = h;
        }

        public void Intersect(in Box box)
        {
            var x1 = x + w;
            var y1 = y + h;
            var x2 = box.x + box.w;
            var y2 = box.y + box.h;

            x = x > box.x ? x : box.x;
            y = y > box.y ? y : box.y;
            w = (x1 < x2 ? x1 : x2) - x;
            h = (y1 < y2 ? y1 : y2) - y;

            if (w < 0.0f) w = 0.0f;
            if (h < 0.0f) h = 0.0f;
        }
    }

    public enum SvgNodeType
    {
        Doc,
        G,
        Defs,
        Animation,
        Arc,
        Circle,
        Ellipse,
        Image,
        Line,
        Path,
        Polygon,
        Polyline,
        Rect,
        Text,
        TextArea,
        Tspan,
        Use,
        Video,
        ClipPath,
        Mask,
        CssStyle,
        Symbol,
        Filter,
        GaussianBlur,
        Unknown
    }

    [Flags]
    public enum SvgFillFlags
    {
        Paint = 0x01,
        Opacity = 0x02,
        Gradient = 0x04,
        FillRule = 0x08,
        ClipPath = 0x16
    }

    [Flags]
    public enum SvgStrokeFlags
    {
        Paint = 0x1,
        Opacity = 0x2,
        Gradient = 0x4,
        Scale = 0x8,
        Width = 0x10,
        Cap = 0x20,
        Join = 0x40,
        Dash = 0x80,
        Miterlimit = 0x100,
        DashOffset = 0x200
    }

    public enum SvgGradientType
    {
        Linear,
        Radial
    }

    [Flags]
    public enum SvgStyleFlags
    {
        Color = 0x01,
        Fill = 0x02,
        FillRule = 0x04,
        FillOpacity = 0x08,
        Opacity = 0x010,
        Stroke = 0x20,
        StrokeWidth = 0x40,
        StrokeLineJoin = 0x80,
        StrokeLineCap = 0x100,
        StrokeOpacity = 0x200,
        StrokeDashArray = 0x400,
        Transform = 0x800,
        ClipPath = 0x1000,
        Mask = 0x2000,
        MaskType = 0x4000,
        Display = 0x8000,
        PaintOrder = 0x10000,
        StrokeMiterlimit = 0x20000,
        StrokeDashOffset = 0x40000,
        Filter = 0x80000
    }

    [Flags]
    public enum SvgStopStyleFlags
    {
        StopDefault = 0x0,
        StopOpacity = 0x01,
        StopColor = 0x02
    }

    [Flags]
    public enum SvgGradientFlags
    {
        None = 0x0,
        GradientUnits = 0x1,
        SpreadMethod = 0x2,
        X1 = 0x4,
        X2 = 0x8,
        Y1 = 0x10,
        Y2 = 0x20,
        Cx = 0x40,
        Cy = 0x80,
        R = 0x100,
        Fx = 0x200,
        Fy = 0x400,
        Fr = 0x800
    }

    public enum SvgMaskType
    {
        Luminance = 0,
        Alpha
    }

    public enum SvgXmlSpace
    {
        None,
        Default,
        Preserve
    }

    public enum SvgParserLengthType
    {
        Vertical,
        Horizontal,
        Diagonal,
        Other
    }

    [Flags]
    public enum SvgViewFlag
    {
        None = 0x0,
        Width = 0x01,
        Height = 0x02,
        Viewbox = 0x04,
        WidthInPercent = 0x08,
        HeightInPercent = 0x10
    }

    public enum AspectRatioAlign
    {
        None,
        XMinYMin,
        XMidYMin,
        XMaxYMin,
        XMinYMid,
        XMidYMid,
        XMaxYMid,
        XMinYMax,
        XMidYMax,
        XMaxYMax
    }

    public enum AspectRatioMeetOrSlice
    {
        Meet,
        Slice
    }

    public class SvgDocNode
    {
        public float w, h;
        public Box vbox;
        public SvgViewFlag viewFlag;
        public SvgNode? defs;
        public SvgNode? style;
        public AspectRatioAlign align;
        public AspectRatioMeetOrSlice meetOrSlice;
    }

    public class SvgDefsNode
    {
        public List<SvgStyleGradient> gradients = new List<SvgStyleGradient>();
    }

    public class SvgSymbolNode
    {
        public float w, h;
        public float vx, vy, vw, vh;
        public AspectRatioAlign align;
        public AspectRatioMeetOrSlice meetOrSlice;
        public bool overflowVisible;
        public bool hasViewBox;
        public bool hasWidth;
        public bool hasHeight;
    }

    public class SvgUseNode
    {
        public float x, y, w, h;
        public bool isWidthSet;
        public bool isHeightSet;
        public SvgNode? symbol;
    }

    public class SvgEllipseNode
    {
        public float cx, cy, rx, ry;
    }

    public class SvgCircleNode
    {
        public float cx, cy, r;
    }

    public class SvgRectNode
    {
        public float x, y, w, h, rx, ry;
        public bool hasRx, hasRy;
    }

    public class SvgLineNode
    {
        public float x1, y1, x2, y2;
    }

    public class SvgImageNode
    {
        public float x, y, w, h;
        public string? href;
    }

    public class SvgPathNode
    {
        public string? path;
    }

    public class SvgPolygonNode
    {
        public List<float> pts = new List<float>();
    }

    public class SvgClipNode
    {
        public bool userSpace;
    }

    public class SvgMaskNode
    {
        public SvgMaskType type;
        public bool userSpace;
    }

    public class SvgTextNode
    {
        public string? text;
        public string? fontFamily;
        public float x, y;
        public float fontSize;
    }

    public class SvgGaussianBlurNode
    {
        public float stdDevX, stdDevY;
        public Box box;
        public bool[] isPercentage = new bool[4];
        public bool hasBox;
        public bool edgeModeWrap;
    }

    public class SvgFilterNode
    {
        public Box box;
        public bool[] isPercentage = new bool[4];
        public bool filterUserSpace;
        public bool primitiveUserSpace;
    }

    public class SvgLinearGradient
    {
        public float x1, y1, x2, y2;
        public bool isX1Percentage;
        public bool isY1Percentage;
        public bool isX2Percentage;
        public bool isY2Percentage;
    }

    public class SvgRadialGradient
    {
        public float cx, cy, fx, fy, r, fr;
        public bool isCxPercentage;
        public bool isCyPercentage;
        public bool isFxPercentage;
        public bool isFyPercentage;
        public bool isRPercentage;
        public bool isFrPercentage;
    }

    public class SvgComposite
    {
        public string? url;
        public SvgNode? node;
        public bool applying;
    }

    public class SvgPaint
    {
        public SvgStyleGradient? gradient;
        public string? url;
        public RGB color;
        public bool none;
        public bool curColor;
    }

    public class SvgDash
    {
        public List<float> array = new List<float>();
        public float offset;
    }

    public class SvgStyleGradient
    {
        public SvgGradientType type;
        public string? id;
        public string? @ref;
        public FillSpread spread;
        public SvgRadialGradient? radial;
        public SvgLinearGradient? linear;
        public Matrix? transform;
        public List<Fill.ColorStop> stops = new List<Fill.ColorStop>();
        public SvgGradientFlags flags;
        public bool userSpace;

        public void Clear()
        {
            stops.Clear();
            transform = null;
            radial = null;
            linear = null;
            @ref = null;
            id = null;
        }
    }

    public class SvgStyleFill
    {
        public SvgFillFlags flags;
        public SvgPaint paint = new SvgPaint();
        public int opacity;
        public FillRule fillRule;
    }

    public class SvgStyleStroke
    {
        public SvgStrokeFlags flags;
        public SvgPaint paint = new SvgPaint();
        public int opacity;
        public float scale;
        public float width;
        public float centered;
        public StrokeCap cap;
        public StrokeJoin join;
        public float miterlimit;
        public SvgDash dash = new SvgDash();
    }

    public class SvgFilter
    {
        public string? url;
        public SvgNode? node;
    }

    public class SvgStyleProperty
    {
        public SvgStyleFill fill = new SvgStyleFill();
        public SvgStyleStroke stroke = new SvgStyleStroke();
        public SvgComposite clipPath = new SvgComposite();
        public SvgComposite mask = new SvgComposite();
        public SvgFilter filter = new SvgFilter();
        public int opacity;
        public RGB color;
        public string? cssClass;
        public SvgStyleFlags flags;
        public SvgStyleFlags flagsImportance;
        public bool curColorSet;
        public bool paintOrder;
        public bool display;
    }

    /// <summary>
    /// SVG DOM node. In C++ this uses a union of node-type-specific data;
    /// in C# we use individual nullable fields (only one is populated at a time).
    /// </summary>
    public class SvgNode
    {
        public SvgNodeType type;
        public SvgNode? parent;
        public List<SvgNode> child = new List<SvgNode>();
        public string? id;
        public SvgStyleProperty? style;
        public Matrix? transform;
        public SvgXmlSpace xmlSpace = SvgXmlSpace.None;

        // Union-like members (only one is populated based on type)
        public SvgDocNode doc = new SvgDocNode();
        public SvgDefsNode defs = new SvgDefsNode();
        public SvgUseNode use = new SvgUseNode();
        public SvgCircleNode circle = new SvgCircleNode();
        public SvgEllipseNode ellipse = new SvgEllipseNode();
        public SvgPolygonNode polygon = new SvgPolygonNode();
        public SvgPolygonNode polyline = new SvgPolygonNode();
        public SvgRectNode rect = new SvgRectNode();
        public SvgPathNode path = new SvgPathNode();
        public SvgLineNode line = new SvgLineNode();
        public SvgImageNode image = new SvgImageNode();
        public SvgMaskNode maskNode = new SvgMaskNode();
        public SvgClipNode clip = new SvgClipNode();
        public SvgSymbolNode symbol = new SvgSymbolNode();
        public SvgTextNode text = new SvgTextNode();
        public SvgFilterNode filter = new SvgFilterNode();
        public SvgGaussianBlurNode gaussianBlur = new SvgGaussianBlurNode();
    }

    public class SvgParser
    {
        public SvgNode? node;
        public SvgStyleGradient? styleGrad;
        public Fill.ColorStop gradStop;
        public SvgStopStyleFlags flags;
        public Box global;

        public bool parsedFx;
        public bool parsedFy;
    }

    public class SvgNodeIdPair : IInlistNode<SvgNodeIdPair>
    {
        public SvgNode node;
        public string id;

        public SvgNodeIdPair(SvgNode n, string i) { node = n; id = i; }

        public SvgNodeIdPair? Prev { get; set; }
        public SvgNodeIdPair? Next { get; set; }
    }

    public class FontFace
    {
        public string? name;
        public string? src;
        public int srcLen;
        public byte[]? decoded;
    }

    public enum OpenedTagType : byte
    {
        Other = 0,
        Style,
        Text
    }

    public class SvgLoaderData
    {
        public List<SvgNode> stack = new List<SvgNode>();
        public SvgNode? doc;
        public SvgNode? def;
        public SvgNode? cssStyle;
        public List<SvgStyleGradient> gradients = new List<SvgStyleGradient>();
        public List<SvgStyleGradient> gradientStack = new List<SvgStyleGradient>();
        public SvgParser? svgParse;
        public Inlist<SvgNodeIdPair> cloneNodes = new Inlist<SvgNodeIdPair>();
        public List<SvgNodeIdPair> nodesToStyle = new List<SvgNodeIdPair>();
        public List<string> images = new List<string>();
        public List<FontFace> fonts = new List<FontFace>();
        public int level;
        public bool result;
        public OpenedTagType openedTag = OpenedTagType.Other;
        public SvgNode? currentGraphicsNode;
    }

    /// <summary>Helper to check string equality (mirrors C++ STR_AS macro).</summary>
    internal static class SvgHelper
    {
        public static bool StrAs(string? a, string b) => string.Equals(a, b, StringComparison.Ordinal);
    }
}
