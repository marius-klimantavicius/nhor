// Ported from ThorVG/src/loaders/svg/tvgSvgLoader.cpp
// Attribute handlers, node factories, gradient parsing, style inheritance.

using System;
using System.Collections.Generic;

namespace ThorVG
{
    public static partial class SvgLoader
    {
        // ========== Paint handling ==========
        private static void HandlePaintAttr(SvgPaint paint, string value)
        {
            if (SvgHelper.StrAs(value, "none"))
            {
                paint.none = true;
                return;
            }
            if (SvgHelper.StrAs(value, "currentColor"))
            {
                paint.curColor = true;
                paint.none = false;
                return;
            }
            byte r = 0, g = 0, b = 0;
            string? url = paint.url;
            if (ToColor(value, ref r, ref g, ref b, ref url))
            {
                paint.color = new RGB { r = r, g = g, b = b };
                paint.none = false;
            }
            paint.url = url;
        }

        // ========== Style attribute handlers ==========
        private static void HandleColorAttr(SvgLoaderData loader, SvgNode node, string value)
        {
            byte r = 0, g = 0, b = 0;
            if (ToColorNoPaint(value, ref r, ref g, ref b))
            {
                node.style!.color = new RGB { r = r, g = g, b = b };
                node.style.curColorSet = true;
            }
        }

        private static void HandleFillAttr(SvgLoaderData loader, SvgNode node, string value)
        {
            node.style!.fill.flags |= SvgFillFlags.Paint;
            HandlePaintAttr(node.style.fill.paint, value);
        }

        private static void HandleStrokeAttr(SvgLoaderData loader, SvgNode node, string value)
        {
            node.style!.stroke.flags |= SvgStrokeFlags.Paint;
            HandlePaintAttr(node.style.stroke.paint, value);
        }

        private static void HandleStrokeOpacityAttr(SvgLoaderData loader, SvgNode node, string value)
        {
            node.style!.stroke.flags |= SvgStrokeFlags.Opacity;
            node.style.stroke.opacity = ToOpacity(value);
        }

        private static void HandleStrokeDashArrayAttr(SvgLoaderData loader, SvgNode node, string value)
        {
            node.style!.stroke.flags |= SvgStrokeFlags.Dash;
            ParseDashArray(loader, value, node.style.stroke.dash);
        }

        private static void HandleStrokeDashOffsetAttr(SvgLoaderData loader, SvgNode node, string value)
        {
            node.style!.stroke.flags |= SvgStrokeFlags.DashOffset;
            node.style!.stroke.dash.offset = ToFloat(loader.svgParse, value, SvgParserLengthType.Horizontal);
        }

        private static void HandleStrokeWidthAttr(SvgLoaderData loader, SvgNode node, string value)
        {
            node.style!.stroke.flags |= SvgStrokeFlags.Width;
            node.style.stroke.width = ToFloat(loader.svgParse, value, SvgParserLengthType.Diagonal);
        }

        private static void HandleStrokeLineCapAttr(SvgLoaderData loader, SvgNode node, string value)
        {
            node.style!.stroke.flags |= SvgStrokeFlags.Cap;
            node.style.stroke.cap = ToLineCap(value);
        }

        private static void HandleStrokeLineJoinAttr(SvgLoaderData loader, SvgNode node, string value)
        {
            node.style!.stroke.flags |= SvgStrokeFlags.Join;
            node.style.stroke.join = ToLineJoin(value);
        }

        private static void HandleStrokeMiterlimitAttr(SvgLoaderData loader, SvgNode node, string value)
        {
            int pos = 0;
            float miterlimit = TvgStr.ToFloat(value, ref pos);
            if (miterlimit < 0.0f) return;
            node.style!.stroke.flags |= SvgStrokeFlags.Miterlimit;
            node.style.stroke.miterlimit = miterlimit;
        }

        private static void HandleFillRuleAttr(SvgLoaderData loader, SvgNode node, string value)
        {
            node.style!.fill.flags |= SvgFillFlags.FillRule;
            node.style.fill.fillRule = ToFillRule(value);
        }

        private static void HandleOpacityAttr(SvgLoaderData loader, SvgNode node, string value)
        {
            node.style!.flags |= SvgStyleFlags.Opacity;
            node.style.opacity = ToOpacity(value);
        }

        private static void HandleFillOpacityAttr(SvgLoaderData loader, SvgNode node, string value)
        {
            node.style!.fill.flags |= SvgFillFlags.Opacity;
            node.style.fill.opacity = ToOpacity(value);
        }

        private static void HandleTransformAttr(SvgLoaderData loader, SvgNode node, string value)
        {
            node.transform = ParseTransformationMatrix(value);
        }

        private static void HandleClipPathAttr(SvgLoaderData loader, SvgNode node, string value)
        {
            if (value.Length >= 3 && value.StartsWith("url", StringComparison.Ordinal))
            {
                node.style!.clipPath.url = IdFromUrl(value.Substring(3));
            }
        }

        private static void HandleMaskAttr(SvgLoaderData loader, SvgNode node, string value)
        {
            if (value.Length >= 3 && value.StartsWith("url", StringComparison.Ordinal))
            {
                node.style!.mask.url = IdFromUrl(value.Substring(3));
            }
        }

        private static void HandleFilterAttr(SvgLoaderData loader, SvgNode node, string value)
        {
            if (value.Length >= 3 && value.StartsWith("url", StringComparison.Ordinal))
            {
                node.style!.filter.url = IdFromUrl(value.Substring(3));
            }
        }

        private static void HandleMaskTypeAttr(SvgLoaderData loader, SvgNode node, string value)
        {
            node.maskNode.type = ToMaskType(value);
        }

        private static void HandleDisplayAttr(SvgLoaderData loader, SvgNode node, string value)
        {
            node.style!.flags |= SvgStyleFlags.Display;
            node.style.display = !SvgHelper.StrAs(value, "none");
        }

        private static void HandlePaintOrderAttr(SvgLoaderData loader, SvgNode node, string value)
        {
            node.style!.flags |= SvgStyleFlags.PaintOrder;
            node.style.paintOrder = ToPaintOrder(value);
        }

        private static void HandleCssClassAttr(SvgLoaderData loader, SvgNode node, string value)
        {
            node.style!.cssClass = CopyId(value);

            if (!CssApplyClass(node, node.style.cssClass, loader.cssStyle))
            {
                loader.nodesToStyle.Add(new SvgNodeIdPair(node, node.style.cssClass ?? ""));
            }
        }

        // ========== _parseStyleAttr ==========
        private static bool ParseStyleAttr(SvgLoaderData loader, string key, string value, bool style)
        {
            var node = loader.svgParse!.node!;
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) return false;

            key = key.Trim();
            value = value.Trim();

            if (!style && SvgHelper.StrAs(key, "xml:space"))
            {
                node.xmlSpace = ToXmlSpace(value);
                return true;
            }

            for (int i = 0; i < StyleTags.Length; i++)
            {
                if (SvgHelper.StrAs(StyleTags[i].tag, key))
                {
                    bool importance = false;
                    var valueToUse = value;
                    int impIdx = value.IndexOf("!important", StringComparison.Ordinal);
                    if (impIdx >= 0)
                    {
                        valueToUse = value.Substring(0, impIdx).TrimEnd();
                        importance = true;
                    }
                    if (style)
                    {
                        if (importance || (node.style!.flagsImportance & StyleTags[i].flag) == 0)
                        {
                            StyleTags[i].handler(loader, node, valueToUse);
                            node.style!.flags |= StyleTags[i].flag;
                        }
                    }
                    else if ((node.style!.flags & StyleTags[i].flag) == 0)
                    {
                        StyleTags[i].handler(loader, node, valueToUse);
                    }
                    if (importance)
                    {
                        node.style!.flagsImportance = node.style.flags | StyleTags[i].flag;
                    }
                    return true;
                }
            }

            return false;
        }

        private static bool ParseStyleAttrCb(SvgLoaderData data, string key, string value)
        {
            return ParseStyleAttr(data, key, value, true);
        }

        // ========== Attribute parse callbacks ==========

        private static bool AttrParseSvgNode(SvgLoaderData loader, string key, string value)
        {
            var node = loader.svgParse!.node!;
            var doc = node.doc;

            if (SvgHelper.StrAs(key, "width"))
            {
                doc.w = ToFloat(loader.svgParse, value, SvgParserLengthType.Horizontal);
                if (value.Contains("%") && (doc.viewFlag & SvgViewFlag.Viewbox) == 0)
                    doc.viewFlag |= SvgViewFlag.WidthInPercent;
                else
                    doc.viewFlag |= SvgViewFlag.Width;
            }
            else if (SvgHelper.StrAs(key, "height"))
            {
                doc.h = ToFloat(loader.svgParse, value, SvgParserLengthType.Vertical);
                if (value.Contains("%") && (doc.viewFlag & SvgViewFlag.Viewbox) == 0)
                    doc.viewFlag |= SvgViewFlag.HeightInPercent;
                else
                    doc.viewFlag |= SvgViewFlag.Height;
            }
            else if (SvgHelper.StrAs(key, "viewBox"))
            {
                int pos = 0;
                float x = 0, y = 0, w = 0, h = 0;
                if (ParseNumber(value, ref pos, out x))
                {
                    if (ParseNumber(value, ref pos, out y))
                    {
                        if (ParseNumber(value, ref pos, out w))
                        {
                            if (ParseNumber(value, ref pos, out h))
                            {
                                doc.vbox = new Box(x, y, w, h);
                                doc.viewFlag |= SvgViewFlag.Viewbox;
                                loader.svgParse.global.h = h;
                            }
                            loader.svgParse.global.w = w;
                        }
                        loader.svgParse.global.y = y;
                    }
                    loader.svgParse.global.x = x;
                }
                if ((doc.viewFlag & SvgViewFlag.Viewbox) != 0 && (doc.vbox.w < 0.0f || doc.vbox.h < 0.0f))
                {
                    doc.viewFlag &= ~SvgViewFlag.Viewbox;
                }
                if ((doc.viewFlag & SvgViewFlag.Viewbox) == 0)
                {
                    loader.svgParse.global.x = 0;
                    loader.svgParse.global.y = 0;
                    loader.svgParse.global.w = 1;
                    loader.svgParse.global.h = 1;
                }
            }
            else if (SvgHelper.StrAs(key, "preserveAspectRatio"))
            {
                int pos = 0;
                ParseAspectRatio(value, ref pos, ref doc.align, ref doc.meetOrSlice);
            }
            else if (SvgHelper.StrAs(key, "style"))
            {
                return XmlParser.ParseW3CAttribute(value, 0, value.Length, ParseStyleAttrCb, loader);
            }
            else
            {
                return ParseStyleAttr(loader, key, value, false);
            }
            return true;
        }

        private static bool AttrParseGNode(SvgLoaderData loader, string key, string value)
        {
            var node = loader.svgParse!.node!;

            if (SvgHelper.StrAs(key, "style"))
                return XmlParser.ParseW3CAttribute(value, 0, value.Length, ParseStyleAttrCb, loader);
            else if (SvgHelper.StrAs(key, "transform"))
                node.transform = ParseTransformationMatrix(value);
            else if (SvgHelper.StrAs(key, "id"))
                node.id = CopyId(value);
            else if (SvgHelper.StrAs(key, "class"))
                HandleCssClassAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "clip-path"))
                HandleClipPathAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "mask"))
                HandleMaskAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "filter"))
                HandleFilterAttr(loader, node, value);
            else
                return ParseStyleAttr(loader, key, value, false);
            return true;
        }

        private static bool AttrParseClipPathNode(SvgLoaderData loader, string key, string value)
        {
            var node = loader.svgParse!.node!;

            if (SvgHelper.StrAs(key, "style"))
                return XmlParser.ParseW3CAttribute(value, 0, value.Length, ParseStyleAttrCb, loader);
            else if (SvgHelper.StrAs(key, "transform"))
                node.transform = ParseTransformationMatrix(value);
            else if (SvgHelper.StrAs(key, "id"))
                node.id = CopyId(value);
            else if (SvgHelper.StrAs(key, "class"))
                HandleCssClassAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "clipPathUnits"))
            {
                if (SvgHelper.StrAs(value, "objectBoundingBox")) node.clip.userSpace = false;
            }
            else
                return ParseStyleAttr(loader, key, value, false);
            return true;
        }

        private static bool AttrParseMaskNode(SvgLoaderData loader, string key, string value)
        {
            var node = loader.svgParse!.node!;

            if (SvgHelper.StrAs(key, "style"))
                return XmlParser.ParseW3CAttribute(value, 0, value.Length, ParseStyleAttrCb, loader);
            else if (SvgHelper.StrAs(key, "transform"))
                node.transform = ParseTransformationMatrix(value);
            else if (SvgHelper.StrAs(key, "id"))
                node.id = CopyId(value);
            else if (SvgHelper.StrAs(key, "class"))
                HandleCssClassAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "maskContentUnits"))
            {
                if (SvgHelper.StrAs(value, "objectBoundingBox")) node.maskNode.userSpace = false;
            }
            else if (SvgHelper.StrAs(key, "mask-type"))
                node.maskNode.type = ToMaskType(value);
            else
                return ParseStyleAttr(loader, key, value, false);
            return true;
        }

        private static bool AttrParseCssStyleNode(SvgLoaderData loader, string key, string value)
        {
            var node = loader.svgParse!.node!;
            if (SvgHelper.StrAs(key, "id"))
                node.id = CopyId(value);
            else
                return ParseStyleAttr(loader, key, value, false);
            return true;
        }

        private static bool AttrParseSymbolNode(SvgLoaderData loader, string key, string value)
        {
            var node = loader.svgParse!.node!;
            var symbol = node.symbol;

            if (SvgHelper.StrAs(key, "viewBox"))
            {
                int pos = 0;
                if (!ParseNumber(value, ref pos, out symbol.vx)) return false;
                if (!ParseNumber(value, ref pos, out symbol.vy)) return false;
                if (!ParseNumber(value, ref pos, out symbol.vw)) return false;
                if (!ParseNumber(value, ref pos, out symbol.vh)) return false;
                symbol.hasViewBox = true;
            }
            else if (SvgHelper.StrAs(key, "width"))
            {
                symbol.w = ToFloat(loader.svgParse, value, SvgParserLengthType.Horizontal);
                symbol.hasWidth = true;
            }
            else if (SvgHelper.StrAs(key, "height"))
            {
                symbol.h = ToFloat(loader.svgParse, value, SvgParserLengthType.Vertical);
                symbol.hasHeight = true;
            }
            else if (SvgHelper.StrAs(key, "preserveAspectRatio"))
            {
                int pos = 0;
                ParseAspectRatio(value, ref pos, ref symbol.align, ref symbol.meetOrSlice);
            }
            else if (SvgHelper.StrAs(key, "overflow"))
            {
                if (SvgHelper.StrAs(value, "visible")) symbol.overflowVisible = true;
            }
            else
                return AttrParseGNode(loader, key, value);
            return true;
        }

        // ========== Graphics node attribute parsers ==========

        private static bool AttrParsePathNode(SvgLoaderData loader, string key, string value)
        {
            var node = loader.svgParse!.node!;
            if (SvgHelper.StrAs(key, "d"))
                node.path.path = CopyId(value);
            else if (SvgHelper.StrAs(key, "style"))
                return XmlParser.ParseW3CAttribute(value, 0, value.Length, ParseStyleAttrCb, loader);
            else if (SvgHelper.StrAs(key, "clip-path"))
                HandleClipPathAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "mask"))
                HandleMaskAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "filter"))
                HandleFilterAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "id"))
                node.id = CopyId(value);
            else if (SvgHelper.StrAs(key, "class"))
                HandleCssClassAttr(loader, node, value);
            else
                return ParseStyleAttr(loader, key, value, false);
            return true;
        }

        private static bool AttrParseCircleNode(SvgLoaderData loader, string key, string value)
        {
            var node = loader.svgParse!.node!;
            var circle = node.circle;

            if (SvgHelper.StrAs(key, "cx")) { circle.cx = ToFloat(loader.svgParse, value, SvgParserLengthType.Horizontal); return true; }
            if (SvgHelper.StrAs(key, "cy")) { circle.cy = ToFloat(loader.svgParse, value, SvgParserLengthType.Vertical); return true; }
            if (SvgHelper.StrAs(key, "r")) { circle.r = ToFloat(loader.svgParse, value, SvgParserLengthType.Diagonal); return true; }

            if (SvgHelper.StrAs(key, "style"))
                return XmlParser.ParseW3CAttribute(value, 0, value.Length, ParseStyleAttrCb, loader);
            else if (SvgHelper.StrAs(key, "clip-path"))
                HandleClipPathAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "mask"))
                HandleMaskAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "filter"))
                HandleFilterAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "id"))
                node.id = CopyId(value);
            else if (SvgHelper.StrAs(key, "class"))
                HandleCssClassAttr(loader, node, value);
            else
                return ParseStyleAttr(loader, key, value, false);
            return true;
        }

        private static bool AttrParseEllipseNode(SvgLoaderData loader, string key, string value)
        {
            var node = loader.svgParse!.node!;
            var ellipse = node.ellipse;

            if (SvgHelper.StrAs(key, "cx")) { ellipse.cx = ToFloat(loader.svgParse, value, SvgParserLengthType.Horizontal); return true; }
            if (SvgHelper.StrAs(key, "cy")) { ellipse.cy = ToFloat(loader.svgParse, value, SvgParserLengthType.Vertical); return true; }
            if (SvgHelper.StrAs(key, "rx")) { ellipse.rx = ToFloat(loader.svgParse, value, SvgParserLengthType.Horizontal); return true; }
            if (SvgHelper.StrAs(key, "ry")) { ellipse.ry = ToFloat(loader.svgParse, value, SvgParserLengthType.Vertical); return true; }

            if (SvgHelper.StrAs(key, "id"))
                node.id = CopyId(value);
            else if (SvgHelper.StrAs(key, "class"))
                HandleCssClassAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "style"))
                return XmlParser.ParseW3CAttribute(value, 0, value.Length, ParseStyleAttrCb, loader);
            else if (SvgHelper.StrAs(key, "clip-path"))
                HandleClipPathAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "mask"))
                HandleMaskAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "filter"))
                HandleFilterAttr(loader, node, value);
            else
                return ParseStyleAttr(loader, key, value, false);
            return true;
        }

        private static bool AttrParsePolygonPoints(string str, SvgPolygonNode polygon)
        {
            int pos = 0;
            while (true)
            {
                if (!ParseNumber(str, ref pos, out float x)) break;
                if (!ParseNumber(str, ref pos, out float y)) break;
                polygon.pts.Add(x);
                polygon.pts.Add(y);
            }
            return true;
        }

        private static bool AttrParsePolygonNode(SvgLoaderData loader, string key, string value)
        {
            var node = loader.svgParse!.node!;
            SvgPolygonNode polygon;
            if (node.type == SvgNodeType.Polygon) polygon = node.polygon;
            else polygon = node.polyline;

            if (SvgHelper.StrAs(key, "points"))
                return AttrParsePolygonPoints(value, polygon);
            else if (SvgHelper.StrAs(key, "style"))
                return XmlParser.ParseW3CAttribute(value, 0, value.Length, ParseStyleAttrCb, loader);
            else if (SvgHelper.StrAs(key, "clip-path"))
                HandleClipPathAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "mask"))
                HandleMaskAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "filter"))
                HandleFilterAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "id"))
                node.id = CopyId(value);
            else if (SvgHelper.StrAs(key, "class"))
                HandleCssClassAttr(loader, node, value);
            else
                return ParseStyleAttr(loader, key, value, false);
            return true;
        }

        private static bool AttrParseRectNode(SvgLoaderData loader, string key, string value)
        {
            var node = loader.svgParse!.node!;
            var rect = node.rect;

            if (SvgHelper.StrAs(key, "x")) { rect.x = ToFloat(loader.svgParse, value, SvgParserLengthType.Horizontal); return true; }
            if (SvgHelper.StrAs(key, "y")) { rect.y = ToFloat(loader.svgParse, value, SvgParserLengthType.Vertical); return true; }
            if (SvgHelper.StrAs(key, "width")) { rect.w = ToFloat(loader.svgParse, value, SvgParserLengthType.Horizontal); return true; }
            if (SvgHelper.StrAs(key, "height")) { rect.h = ToFloat(loader.svgParse, value, SvgParserLengthType.Vertical); return true; }
            if (SvgHelper.StrAs(key, "rx"))
            {
                rect.rx = ToFloat(loader.svgParse, value, SvgParserLengthType.Horizontal);
                rect.hasRx = true;
                if (rect.rx >= MathConstants.FLOAT_EPSILON && rect.ry < MathConstants.FLOAT_EPSILON && !rect.hasRy) rect.ry = rect.rx;
                return true;
            }
            if (SvgHelper.StrAs(key, "ry"))
            {
                rect.ry = ToFloat(loader.svgParse, value, SvgParserLengthType.Vertical);
                rect.hasRy = true;
                if (rect.ry >= MathConstants.FLOAT_EPSILON && rect.rx < MathConstants.FLOAT_EPSILON && !rect.hasRx) rect.rx = rect.ry;
                return true;
            }

            if (SvgHelper.StrAs(key, "id"))
                node.id = CopyId(value);
            else if (SvgHelper.StrAs(key, "class"))
                HandleCssClassAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "style"))
                return XmlParser.ParseW3CAttribute(value, 0, value.Length, ParseStyleAttrCb, loader);
            else if (SvgHelper.StrAs(key, "clip-path"))
                HandleClipPathAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "mask"))
                HandleMaskAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "filter"))
                HandleFilterAttr(loader, node, value);
            else
                return ParseStyleAttr(loader, key, value, false);
            return true;
        }

        private static bool AttrParseLineNode(SvgLoaderData loader, string key, string value)
        {
            var node = loader.svgParse!.node!;
            var line = node.line;

            if (SvgHelper.StrAs(key, "x1")) { line.x1 = ToFloat(loader.svgParse, value, SvgParserLengthType.Horizontal); return true; }
            if (SvgHelper.StrAs(key, "y1")) { line.y1 = ToFloat(loader.svgParse, value, SvgParserLengthType.Vertical); return true; }
            if (SvgHelper.StrAs(key, "x2")) { line.x2 = ToFloat(loader.svgParse, value, SvgParserLengthType.Horizontal); return true; }
            if (SvgHelper.StrAs(key, "y2")) { line.y2 = ToFloat(loader.svgParse, value, SvgParserLengthType.Vertical); return true; }

            if (SvgHelper.StrAs(key, "id"))
                node.id = CopyId(value);
            else if (SvgHelper.StrAs(key, "class"))
                HandleCssClassAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "style"))
                return XmlParser.ParseW3CAttribute(value, 0, value.Length, ParseStyleAttrCb, loader);
            else if (SvgHelper.StrAs(key, "clip-path"))
                HandleClipPathAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "mask"))
                HandleMaskAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "filter"))
                HandleFilterAttr(loader, node, value);
            else
                return ParseStyleAttr(loader, key, value, false);
            return true;
        }

        private static bool AttrParseImageNode(SvgLoaderData loader, string key, string value)
        {
            var node = loader.svgParse!.node!;
            var image = node.image;

            if (SvgHelper.StrAs(key, "x")) { image.x = ToFloat(loader.svgParse, value, SvgParserLengthType.Horizontal); return true; }
            if (SvgHelper.StrAs(key, "y")) { image.y = ToFloat(loader.svgParse, value, SvgParserLengthType.Vertical); return true; }
            if (SvgHelper.StrAs(key, "width")) { image.w = ToFloat(loader.svgParse, value, SvgParserLengthType.Horizontal); return true; }
            if (SvgHelper.StrAs(key, "height")) { image.h = ToFloat(loader.svgParse, value, SvgParserLengthType.Vertical); return true; }

            if (SvgHelper.StrAs(key, "href") || SvgHelper.StrAs(key, "xlink:href"))
                image.href = IdFromHref(value);
            else if (SvgHelper.StrAs(key, "id"))
                node.id = CopyId(value);
            else if (SvgHelper.StrAs(key, "class"))
                HandleCssClassAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "style"))
                return XmlParser.ParseW3CAttribute(value, 0, value.Length, ParseStyleAttrCb, loader);
            else if (SvgHelper.StrAs(key, "clip-path"))
                HandleClipPathAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "mask"))
                HandleMaskAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "filter"))
                HandleFilterAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "transform"))
                node.transform = ParseTransformationMatrix(value);
            else
                return ParseStyleAttr(loader, key, value, true);
            return true;
        }

        private static bool AttrParseTextNode(SvgLoaderData loader, string key, string value)
        {
            var node = loader.svgParse!.node!;
            var text = node.text;

            if (SvgHelper.StrAs(key, "x")) { text.x = ToFloat(loader.svgParse, value, SvgParserLengthType.Horizontal); return true; }
            if (SvgHelper.StrAs(key, "y")) { text.y = ToFloat(loader.svgParse, value, SvgParserLengthType.Vertical); return true; }
            if (SvgHelper.StrAs(key, "font-size")) { text.fontSize = ToFloat(loader.svgParse, value, SvgParserLengthType.Vertical); return true; }

            if (SvgHelper.StrAs(key, "font-family"))
                text.fontFamily = value;
            else if (SvgHelper.StrAs(key, "style"))
                return XmlParser.ParseW3CAttribute(value, 0, value.Length, ParseStyleAttrCb, loader);
            else if (SvgHelper.StrAs(key, "clip-path"))
                HandleClipPathAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "mask"))
                HandleMaskAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "filter"))
                HandleFilterAttr(loader, node, value);
            else if (SvgHelper.StrAs(key, "id"))
                node.id = CopyId(value);
            else if (SvgHelper.StrAs(key, "class"))
                HandleCssClassAttr(loader, node, value);
            else
                return ParseStyleAttr(loader, key, value, false);
            return true;
        }

        private static bool AttrParseUseNode(SvgLoaderData loader, string key, string value)
        {
            var node = loader.svgParse!.node!;
            var use = node.use;

            if (SvgHelper.StrAs(key, "x")) { use.x = ToFloat(loader.svgParse, value, SvgParserLengthType.Horizontal); return true; }
            if (SvgHelper.StrAs(key, "y")) { use.y = ToFloat(loader.svgParse, value, SvgParserLengthType.Vertical); return true; }
            if (SvgHelper.StrAs(key, "width")) { use.w = ToFloat(loader.svgParse, value, SvgParserLengthType.Horizontal); use.isWidthSet = true; return true; }
            if (SvgHelper.StrAs(key, "height")) { use.h = ToFloat(loader.svgParse, value, SvgParserLengthType.Vertical); use.isHeightSet = true; return true; }

            if (SvgHelper.StrAs(key, "href") || SvgHelper.StrAs(key, "xlink:href"))
            {
                var id = IdFromHref(value);
                var defs = GetDefsNode(node);
                var nodeFrom = FindNodeById(defs, id);
                if (nodeFrom != null)
                {
                    if (FindParentById(node, id, loader.doc) == null)
                    {
                        // Check if none of nodeFrom's children are in the cloneNodes list
                        var postpone = false;
                        var pair = loader.cloneNodes.Head;
                        while (pair != null)
                        {
                            if (SvgLoader.CheckPostponed(nodeFrom, pair.node, 1))
                            {
                                postpone = true;
                                loader.cloneNodes.Back(new SvgNodeIdPair(node, id ?? ""));
                                break;
                            }
                            pair = pair.Next;
                        }
                        if (!postpone)
                        {
                            CloneNode(nodeFrom, node, 0);
                            if (nodeFrom.type == SvgNodeType.Symbol) use.symbol = nodeFrom;
                        }
                    }
                }
                else
                {
                    // Postpone cloning - some svg export software include <defs> at end of file
                    loader.cloneNodes.Back(new SvgNodeIdPair(node, id ?? ""));
                }
            }
            else
                return AttrParseGNode(loader, key, value);
            return true;
        }

        private static bool AttrParseFilterNode(SvgLoaderData loader, string key, string value)
        {
            var node = loader.svgParse!.node!;
            var filter = node.filter;

            if (SvgHelper.StrAs(key, "id"))
                node.id = CopyId(value);
            else if (SvgHelper.StrAs(key, "primitiveUnits"))
            {
                if (SvgHelper.StrAs(value, "objectBoundingBox")) filter.primitiveUserSpace = false;
            }
            else if (SvgHelper.StrAs(key, "filterUnits"))
            {
                if (SvgHelper.StrAs(value, "userSpaceOnUse")) filter.filterUserSpace = true;
            }
            else if (SvgHelper.StrAs(key, "x"))
            {
                bool isP = false;
                filter.box.x = GradientToFloat(null, value, ref isP);
                filter.isPercentage[0] = isP;
            }
            else if (SvgHelper.StrAs(key, "y"))
            {
                bool isP = false;
                filter.box.y = GradientToFloat(null, value, ref isP);
                filter.isPercentage[1] = isP;
            }
            else if (SvgHelper.StrAs(key, "width"))
            {
                bool isP = false;
                filter.box.w = GradientToFloat(null, value, ref isP);
                filter.isPercentage[2] = isP;
            }
            else if (SvgHelper.StrAs(key, "height"))
            {
                bool isP = false;
                filter.box.h = GradientToFloat(null, value, ref isP);
                filter.isPercentage[3] = isP;
            }
            return true;
        }

        private static bool AttrParseGaussianBlurNode(SvgLoaderData loader, string key, string value)
        {
            var node = loader.svgParse!.node!;
            var gb = node.gaussianBlur;

            if (SvgHelper.StrAs(key, "id"))
                node.id = CopyId(value);
            else if (SvgHelper.StrAs(key, "stdDeviation"))
                ParseGaussianBlurStdDeviation(value, ref gb.stdDevX, ref gb.stdDevY);
            else if (SvgHelper.StrAs(key, "edgeMode"))
            {
                if (SvgHelper.StrAs(value, "wrap")) gb.edgeModeWrap = true;
            }
            else if (SvgHelper.StrAs(key, "x") || SvgHelper.StrAs(key, "y") || SvgHelper.StrAs(key, "width") || SvgHelper.StrAs(key, "height"))
            {
                // box parsing
                int idx = SvgHelper.StrAs(key, "x") ? 0 : SvgHelper.StrAs(key, "y") ? 1 : SvgHelper.StrAs(key, "width") ? 2 : 3;
                bool isP = false;
                float val = GradientToFloat(null, value, ref isP);
                gb.isPercentage[idx] = isP;
                switch (idx)
                {
                    case 0: gb.box.x = val; break;
                    case 1: gb.box.y = val; break;
                    case 2: gb.box.w = val; break;
                    case 3: gb.box.h = val; break;
                }
                gb.hasBox = true;
            }
            else
                return ParseStyleAttr(loader, key, value, false);
            return true;
        }

        private static bool AttrParseFontFace(SvgLoaderData loader, string key, string value)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) return false;
            key = key.Trim();
            value = value.Trim();
            var font = loader.fonts[loader.fonts.Count - 1];

            if (SvgHelper.StrAs(key, "font-family"))
                font.name = Unquote(value);
            else if (SvgHelper.StrAs(key, "src"))
            {
                var (src, len) = SrcFromUrl(value);
                font.src = src;
                font.srcLen = len;
            }
            return true;
        }

        // ========== Stop attribute parsers ==========

        private static bool AttrParseStopsStyle(SvgLoaderData loader, string key, string value)
        {
            var stop = loader.svgParse!.gradStop;

            if (SvgHelper.StrAs(key, "stop-opacity"))
            {
                stop.a = (byte)ToOpacity(value);
                loader.svgParse.flags |= SvgStopStyleFlags.StopOpacity;
            }
            else if (SvgHelper.StrAs(key, "stop-color"))
            {
                if (SvgHelper.StrAs(value, "currentColor"))
                {
                    var latestColor = FindLatestColor(loader);
                    if (latestColor.HasValue)
                    {
                        stop.r = latestColor.Value.r;
                        stop.g = latestColor.Value.g;
                        stop.b = latestColor.Value.b;
                    }
                }
                else
                {
                    byte r = 0, g = 0, b = 0;
                    if (ToColorNoPaint(value, ref r, ref g, ref b))
                    {
                        stop.r = r;
                        stop.g = g;
                        stop.b = b;
                        loader.svgParse.flags |= SvgStopStyleFlags.StopColor;
                    }
                }
            }
            else return false;

            loader.svgParse.gradStop = stop;
            return true;
        }

        private static bool AttrParseStops(SvgLoaderData loader, string key, string value)
        {
            var stop = loader.svgParse!.gradStop;

            if (SvgHelper.StrAs(key, "offset"))
            {
                stop.offset = ToOffset(value);
            }
            else if (SvgHelper.StrAs(key, "stop-opacity"))
            {
                if ((loader.svgParse.flags & SvgStopStyleFlags.StopOpacity) == 0)
                    stop.a = (byte)ToOpacity(value);
            }
            else if (SvgHelper.StrAs(key, "stop-color"))
            {
                if (SvgHelper.StrAs(value, "currentColor"))
                {
                    var latestColor = FindLatestColor(loader);
                    if (latestColor.HasValue)
                    {
                        stop.r = latestColor.Value.r;
                        stop.g = latestColor.Value.g;
                        stop.b = latestColor.Value.b;
                    }
                }
                else if ((loader.svgParse.flags & SvgStopStyleFlags.StopColor) == 0)
                {
                    byte r = 0, g = 0, b = 0;
                    ToColorNoPaint(value, ref r, ref g, ref b);
                    stop.r = r;
                    stop.g = g;
                    stop.b = b;
                }
            }
            else if (SvgHelper.StrAs(key, "style"))
            {
                loader.svgParse.gradStop = stop;
                XmlParser.ParseW3CAttribute(value, 0, value.Length, AttrParseStopsStyle, loader);
                return true;
            }
            else return false;

            loader.svgParse.gradStop = stop;
            return true;
        }

        private static RGB? FindLatestColor(SvgLoaderData loader)
        {
            var parent = loader.stack.Count > 0 ? loader.stack[loader.stack.Count - 1] : loader.doc;
            while (parent != null)
            {
                if (parent.style!.curColorSet) return parent.style.color;
                parent = parent.parent;
            }
            return null;
        }

        // ========== Node factories ==========

        private static SvgNode CreateNode(SvgNode? parent, SvgNodeType type)
        {
            var node = new SvgNode();
            node.style = new SvgStyleProperty();

            // Default values
            node.style.opacity = 255;
            node.style.fill.opacity = 255;
            node.style.fill.fillRule = FillRule.NonZero;
            node.style.stroke.paint.none = true;
            node.style.stroke.opacity = 255;
            node.style.stroke.width = 1;
            node.style.stroke.cap = StrokeCap.Butt;
            node.style.stroke.join = StrokeJoin.Miter;
            node.style.stroke.miterlimit = 4.0f;
            node.style.stroke.scale = 1.0f;
            node.style.paintOrder = ToPaintOrder("fill stroke");
            node.style.display = true;
            node.parent = parent;
            node.type = type;
            node.xmlSpace = SvgXmlSpace.None;

            if (parent != null) parent.child.Add(node);
            return node;
        }

        private static SvgNode? CreateDefsNode(SvgLoaderData loader, SvgNode? parent, string buf, int bufOffset, int bufLength, ParseAttributesFunc? func)
        {
            if (loader.def != null && loader.doc!.doc.defs != null) return loader.def;
            loader.def = CreateNode(null, SvgNodeType.Defs);
            loader.doc!.doc.defs = loader.def;
            return loader.def;
        }

        private static SvgNode? CreateGNode(SvgLoaderData loader, SvgNode? parent, string buf, int bufOffset, int bufLength, ParseAttributesFunc? func)
        {
            loader.svgParse!.node = CreateNode(parent, SvgNodeType.G);
            func?.Invoke(buf, bufOffset, bufLength, AttrParseGNode, loader);
            return loader.svgParse.node;
        }

        private static SvgNode? CreateSvgNode(SvgLoaderData loader, SvgNode? parent, string buf, int bufOffset, int bufLength, ParseAttributesFunc? func)
        {
            loader.svgParse!.node = CreateNode(parent, SvgNodeType.Doc);
            var doc = loader.svgParse.node.doc;

            loader.svgParse.global.w = 1.0f;
            loader.svgParse.global.h = 1.0f;

            doc.align = AspectRatioAlign.XMidYMid;
            doc.meetOrSlice = AspectRatioMeetOrSlice.Meet;
            doc.viewFlag = SvgViewFlag.None;
            func?.Invoke(buf, bufOffset, bufLength, AttrParseSvgNode, loader);

            if ((doc.viewFlag & SvgViewFlag.Viewbox) == 0)
            {
                if ((doc.viewFlag & SvgViewFlag.Width) != 0) loader.svgParse.global.w = doc.w;
                if ((doc.viewFlag & SvgViewFlag.Height) != 0) loader.svgParse.global.h = doc.h;
            }
            return loader.svgParse.node;
        }

        private static SvgNode? CreateMaskNode(SvgLoaderData loader, SvgNode? parent, string buf, int bufOffset, int bufLength, ParseAttributesFunc? func)
        {
            loader.svgParse!.node = CreateNode(parent, SvgNodeType.Mask);
            loader.svgParse.node.maskNode.userSpace = true;
            loader.svgParse.node.maskNode.type = SvgMaskType.Luminance;
            func?.Invoke(buf, bufOffset, bufLength, AttrParseMaskNode, loader);
            return loader.svgParse.node;
        }

        private static SvgNode? CreateClipPathNode(SvgLoaderData loader, SvgNode? parent, string buf, int bufOffset, int bufLength, ParseAttributesFunc? func)
        {
            loader.svgParse!.node = CreateNode(parent, SvgNodeType.ClipPath);
            loader.svgParse.node.style!.display = false;
            loader.svgParse.node.clip.userSpace = true;
            func?.Invoke(buf, bufOffset, bufLength, AttrParseClipPathNode, loader);
            return loader.svgParse.node;
        }

        private static SvgNode? CreateCssStyleNode(SvgLoaderData loader, SvgNode? parent, string buf, int bufOffset, int bufLength, ParseAttributesFunc? func)
        {
            loader.svgParse!.node = CreateNode(parent, SvgNodeType.CssStyle);
            func?.Invoke(buf, bufOffset, bufLength, AttrParseCssStyleNode, loader);
            return loader.svgParse.node;
        }

        private static SvgNode? CreateSymbolNode(SvgLoaderData loader, SvgNode? parent, string buf, int bufOffset, int bufLength, ParseAttributesFunc? func)
        {
            loader.svgParse!.node = CreateNode(parent, SvgNodeType.Symbol);
            loader.svgParse.node.symbol.align = AspectRatioAlign.XMidYMid;
            loader.svgParse.node.symbol.meetOrSlice = AspectRatioMeetOrSlice.Meet;
            func?.Invoke(buf, bufOffset, bufLength, AttrParseSymbolNode, loader);
            return loader.svgParse.node;
        }

        private static SvgNode? CreatePathNode(SvgLoaderData loader, SvgNode? parent, string buf, int bufOffset, int bufLength, ParseAttributesFunc? func)
        {
            loader.svgParse!.node = CreateNode(parent, SvgNodeType.Path);
            func?.Invoke(buf, bufOffset, bufLength, AttrParsePathNode, loader);
            return loader.svgParse.node;
        }

        private static SvgNode? CreateCircleNode(SvgLoaderData loader, SvgNode? parent, string buf, int bufOffset, int bufLength, ParseAttributesFunc? func)
        {
            loader.svgParse!.node = CreateNode(parent, SvgNodeType.Circle);
            func?.Invoke(buf, bufOffset, bufLength, AttrParseCircleNode, loader);
            return loader.svgParse.node;
        }

        private static SvgNode? CreateEllipseNode(SvgLoaderData loader, SvgNode? parent, string buf, int bufOffset, int bufLength, ParseAttributesFunc? func)
        {
            loader.svgParse!.node = CreateNode(parent, SvgNodeType.Ellipse);
            func?.Invoke(buf, bufOffset, bufLength, AttrParseEllipseNode, loader);
            return loader.svgParse.node;
        }

        private static SvgNode? CreatePolygonNode(SvgLoaderData loader, SvgNode? parent, string buf, int bufOffset, int bufLength, ParseAttributesFunc? func)
        {
            loader.svgParse!.node = CreateNode(parent, SvgNodeType.Polygon);
            func?.Invoke(buf, bufOffset, bufLength, AttrParsePolygonNode, loader);
            return loader.svgParse.node;
        }

        private static SvgNode? CreatePolylineNode(SvgLoaderData loader, SvgNode? parent, string buf, int bufOffset, int bufLength, ParseAttributesFunc? func)
        {
            loader.svgParse!.node = CreateNode(parent, SvgNodeType.Polyline);
            func?.Invoke(buf, bufOffset, bufLength, AttrParsePolygonNode, loader);
            return loader.svgParse.node;
        }

        private static SvgNode? CreateRectNode(SvgLoaderData loader, SvgNode? parent, string buf, int bufOffset, int bufLength, ParseAttributesFunc? func)
        {
            loader.svgParse!.node = CreateNode(parent, SvgNodeType.Rect);
            func?.Invoke(buf, bufOffset, bufLength, AttrParseRectNode, loader);
            return loader.svgParse.node;
        }

        private static SvgNode? CreateLineNode(SvgLoaderData loader, SvgNode? parent, string buf, int bufOffset, int bufLength, ParseAttributesFunc? func)
        {
            loader.svgParse!.node = CreateNode(parent, SvgNodeType.Line);
            func?.Invoke(buf, bufOffset, bufLength, AttrParseLineNode, loader);
            return loader.svgParse.node;
        }

        private static SvgNode? CreateImageNode(SvgLoaderData loader, SvgNode? parent, string buf, int bufOffset, int bufLength, ParseAttributesFunc? func)
        {
            loader.svgParse!.node = CreateNode(parent, SvgNodeType.Image);
            func?.Invoke(buf, bufOffset, bufLength, AttrParseImageNode, loader);
            return loader.svgParse.node;
        }

        private static SvgNode? CreateTextNode(SvgLoaderData loader, SvgNode? parent, string buf, int bufOffset, int bufLength, ParseAttributesFunc? func)
        {
            loader.svgParse!.node = CreateNode(parent, SvgNodeType.Text);
            loader.svgParse.node.text.fontSize = 10.0f;
            func?.Invoke(buf, bufOffset, bufLength, AttrParseTextNode, loader);
            return loader.svgParse.node;
        }

        private static SvgNode? CreateUseNode(SvgLoaderData loader, SvgNode? parent, string buf, int bufOffset, int bufLength, ParseAttributesFunc? func)
        {
            loader.svgParse!.node = CreateNode(parent, SvgNodeType.Use);
            func?.Invoke(buf, bufOffset, bufLength, AttrParseUseNode, loader);
            return loader.svgParse.node;
        }

        private static SvgNode? CreateGaussianBlurNode(SvgLoaderData loader, SvgNode? parent, string buf, int bufOffset, int bufLength, ParseAttributesFunc? func)
        {
            loader.svgParse!.node = CreateNode(parent, SvgNodeType.GaussianBlur);
            loader.svgParse.node.style!.display = false;
            loader.svgParse.node.gaussianBlur.box = new Box(0, 0, 1, 1);
            func?.Invoke(buf, bufOffset, bufLength, AttrParseGaussianBlurNode, loader);
            return loader.svgParse.node;
        }

        private static SvgNode? CreateFilterNode(SvgLoaderData loader, SvgNode? parent, string buf, int bufOffset, int bufLength, ParseAttributesFunc? func)
        {
            loader.svgParse!.node = CreateNode(parent, SvgNodeType.Filter);
            var filter = loader.svgParse.node.filter;
            loader.svgParse.node.style!.display = false;
            filter.box = new Box(-0.1f, -0.1f, 1.2f, 1.2f);
            filter.primitiveUserSpace = true;
            func?.Invoke(buf, bufOffset, bufLength, AttrParseFilterNode, loader);

            if (filter.filterUserSpace) RecalcBox(loader, ref filter.box, filter.isPercentage);
            return loader.svgParse.node;
        }

        private static void CreateFontFace(SvgLoaderData loader, string buf, int bufOffset, int bufLength, ParseAttributesFunc func)
        {
            loader.fonts.Add(new FontFace());
            func(buf, bufOffset, bufLength, AttrParseFontFace, loader);
        }

        private static void RecalcBox(SvgLoaderData loader, ref Box box, bool[] isPercentage)
        {
            if (isPercentage[0]) box.x *= loader.svgParse!.global.w;
            if (isPercentage[1]) box.y *= loader.svgParse!.global.h;
            if (isPercentage[2]) box.w *= loader.svgParse!.global.w;
            if (isPercentage[3]) box.h *= loader.svgParse!.global.h;
        }

        // ========== Tag lookup tables ==========
        private static readonly (string tag, FactoryMethod handler)[] GraphicsTags = {
            ("use", CreateUseNode!),
            ("circle", CreateCircleNode!),
            ("ellipse", CreateEllipseNode!),
            ("path", CreatePathNode!),
            ("polygon", CreatePolygonNode!),
            ("rect", CreateRectNode!),
            ("polyline", CreatePolylineNode!),
            ("line", CreateLineNode!),
            ("image", CreateImageNode!),
            ("text", CreateTextNode!),
            ("feGaussianBlur", CreateGaussianBlurNode!)
        };

        private static readonly (string tag, FactoryMethod handler)[] GroupTags = {
            ("defs", CreateDefsNode!),
            ("g", CreateGNode!),
            ("svg", CreateSvgNode!),
            ("mask", CreateMaskNode!),
            ("clipPath", CreateClipPathNode!),
            ("style", CreateCssStyleNode!),
            ("symbol", CreateSymbolNode!),
            ("filter", CreateFilterNode!)
        };

        private static readonly (string tag, GradientFactoryMethod handler)[] GradientTags = {
            ("linearGradient", CreateLinearGradient),
            ("radialGradient", CreateRadialGradient)
        };

        private static FactoryMethod? FindGroupFactory(string name)
        {
            for (int i = 0; i < GroupTags.Length; i++)
                if (SvgHelper.StrAs(GroupTags[i].tag, name)) return GroupTags[i].handler;
            return null;
        }

        private static FactoryMethod? FindGraphicsFactory(string name)
        {
            for (int i = 0; i < GraphicsTags.Length; i++)
                if (SvgHelper.StrAs(GraphicsTags[i].tag, name)) return GraphicsTags[i].handler;
            return null;
        }

        private static GradientFactoryMethod? FindGradientFactory(string name)
        {
            for (int i = 0; i < GradientTags.Length; i++)
                if (SvgHelper.StrAs(GradientTags[i].tag, name)) return GradientTags[i].handler;
            return null;
        }

        // ========== Gradient factories ==========

        private static bool AttrParseRadialGradientNode(SvgLoaderData loader, string key, string value)
        {
            var grad = loader.svgParse!.styleGrad!;
            var radial = grad.radial!;

            if (SvgHelper.StrAs(key, "cx")) { bool p = false; radial.cx = GradientToFloat(loader.svgParse, value, ref p); radial.isCxPercentage = p; if (!loader.svgParse.parsedFx) { radial.fx = radial.cx; radial.isFxPercentage = p; } grad.flags |= SvgGradientFlags.Cx; return true; }
            if (SvgHelper.StrAs(key, "cy")) { bool p = false; radial.cy = GradientToFloat(loader.svgParse, value, ref p); radial.isCyPercentage = p; if (!loader.svgParse.parsedFy) { radial.fy = radial.cy; radial.isFyPercentage = p; } grad.flags |= SvgGradientFlags.Cy; return true; }
            if (SvgHelper.StrAs(key, "fx")) { bool p = false; radial.fx = GradientToFloat(loader.svgParse, value, ref p); radial.isFxPercentage = p; loader.svgParse.parsedFx = true; grad.flags |= SvgGradientFlags.Fx; return true; }
            if (SvgHelper.StrAs(key, "fy")) { bool p = false; radial.fy = GradientToFloat(loader.svgParse, value, ref p); radial.isFyPercentage = p; loader.svgParse.parsedFy = true; grad.flags |= SvgGradientFlags.Fy; return true; }
            if (SvgHelper.StrAs(key, "r")) { bool p = false; radial.r = GradientToFloat(loader.svgParse, value, ref p); radial.isRPercentage = p; grad.flags |= SvgGradientFlags.R; return true; }
            if (SvgHelper.StrAs(key, "fr")) { bool p = false; radial.fr = GradientToFloat(loader.svgParse, value, ref p); radial.isFrPercentage = p; grad.flags |= SvgGradientFlags.Fr; return true; }

            if (SvgHelper.StrAs(key, "id")) grad.id = CopyId(value);
            else if (SvgHelper.StrAs(key, "spreadMethod")) { grad.spread = ParseSpreadValue(value); grad.flags |= SvgGradientFlags.SpreadMethod; }
            else if (SvgHelper.StrAs(key, "href") || SvgHelper.StrAs(key, "xlink:href")) grad.@ref = IdFromHref(value);
            else if (SvgHelper.StrAs(key, "gradientUnits")) { if (SvgHelper.StrAs(value, "userSpaceOnUse")) grad.userSpace = true; grad.flags |= SvgGradientFlags.GradientUnits; }
            else if (SvgHelper.StrAs(key, "gradientTransform")) grad.transform = ParseTransformationMatrix(value);
            else return false;
            return true;
        }

        private static SvgStyleGradient? CreateRadialGradient(SvgLoaderData loader, string buf, int bufOffset, int bufLength)
        {
            var grad = new SvgStyleGradient();
            loader.svgParse!.styleGrad = grad;

            grad.flags = SvgGradientFlags.None;
            grad.type = SvgGradientType.Radial;
            grad.radial = new SvgRadialGradient();
            grad.radial.cx = 0.5f; grad.radial.cy = 0.5f;
            grad.radial.fx = 0.5f; grad.radial.fy = 0.5f;
            grad.radial.r = 0.5f;
            grad.radial.isCxPercentage = true; grad.radial.isCyPercentage = true;
            grad.radial.isFxPercentage = true; grad.radial.isFyPercentage = true;
            grad.radial.isRPercentage = true; grad.radial.isFrPercentage = true;

            loader.svgParse.parsedFx = false;
            loader.svgParse.parsedFy = false;

            XmlParser.ParseAttributes(buf, bufOffset, bufLength, AttrParseRadialGradientNode, loader);

            // Recalc for userSpace
            RecalcRadialGrad(loader, grad);

            return grad;
        }

        private static void RecalcRadialGrad(SvgLoaderData loader, SvgStyleGradient grad)
        {
            var r = grad.radial!;
            var g = loader.svgParse!.global;
            var userSpace = grad.userSpace;
            if (userSpace && !r.isCxPercentage) r.cx /= g.w;
            if (userSpace && !r.isCyPercentage) r.cy /= g.h;
            if (userSpace && !r.isFxPercentage) r.fx /= g.w;
            if (userSpace && !r.isFyPercentage) r.fy /= g.h;
            var diag = MathF.Sqrt(g.w * g.w + g.h * g.h) / MathF.Sqrt(2.0f);
            if (userSpace && !r.isRPercentage && diag > 0) r.r /= diag;
            if (userSpace && !r.isFrPercentage && diag > 0) r.fr /= diag;
        }

        private static bool AttrParseLinearGradientNode(SvgLoaderData loader, string key, string value)
        {
            var grad = loader.svgParse!.styleGrad!;
            var linear = grad.linear!;

            if (SvgHelper.StrAs(key, "x1")) { bool p = false; linear.x1 = GradientToFloat(loader.svgParse, value, ref p); linear.isX1Percentage = p; grad.flags |= SvgGradientFlags.X1; return true; }
            if (SvgHelper.StrAs(key, "y1")) { bool p = false; linear.y1 = GradientToFloat(loader.svgParse, value, ref p); linear.isY1Percentage = p; grad.flags |= SvgGradientFlags.Y1; return true; }
            if (SvgHelper.StrAs(key, "x2")) { bool p = false; linear.x2 = GradientToFloat(loader.svgParse, value, ref p); linear.isX2Percentage = p; grad.flags |= SvgGradientFlags.X2; return true; }
            if (SvgHelper.StrAs(key, "y2")) { bool p = false; linear.y2 = GradientToFloat(loader.svgParse, value, ref p); linear.isY2Percentage = p; grad.flags |= SvgGradientFlags.Y2; return true; }

            if (SvgHelper.StrAs(key, "id")) grad.id = CopyId(value);
            else if (SvgHelper.StrAs(key, "spreadMethod")) { grad.spread = ParseSpreadValue(value); grad.flags |= SvgGradientFlags.SpreadMethod; }
            else if (SvgHelper.StrAs(key, "href") || SvgHelper.StrAs(key, "xlink:href")) grad.@ref = IdFromHref(value);
            else if (SvgHelper.StrAs(key, "gradientUnits")) { if (SvgHelper.StrAs(value, "userSpaceOnUse")) grad.userSpace = true; grad.flags |= SvgGradientFlags.GradientUnits; }
            else if (SvgHelper.StrAs(key, "gradientTransform")) grad.transform = ParseTransformationMatrix(value);
            else return false;
            return true;
        }

        private static SvgStyleGradient? CreateLinearGradient(SvgLoaderData loader, string buf, int bufOffset, int bufLength)
        {
            var grad = new SvgStyleGradient();
            loader.svgParse!.styleGrad = grad;

            grad.flags = SvgGradientFlags.None;
            grad.type = SvgGradientType.Linear;
            grad.linear = new SvgLinearGradient();
            grad.linear.x2 = 1.0f;
            grad.linear.isX2Percentage = true;

            XmlParser.ParseAttributes(buf, bufOffset, bufLength, AttrParseLinearGradientNode, loader);

            // Recalc for userSpace
            RecalcLinearGrad(loader, grad);

            return grad;
        }

        private static void RecalcLinearGrad(SvgLoaderData loader, SvgStyleGradient grad)
        {
            var l = grad.linear!;
            var g = loader.svgParse!.global;
            var userSpace = grad.userSpace;
            if (userSpace && !l.isX1Percentage) l.x1 /= g.w;
            if (userSpace && !l.isY1Percentage) l.y1 /= g.h;
            if (userSpace && !l.isX2Percentage) l.x2 /= g.w;
            if (userSpace && !l.isY2Percentage) l.y2 /= g.h;
        }

        // ========== Style inheritance / copy ==========

        private static void StyleInherit(SvgStyleProperty? child, SvgStyleProperty? parent)
        {
            if (parent == null || child == null) return;

            if (!child.curColorSet)
            {
                child.color = parent.color;
                child.curColorSet = parent.curColorSet;
            }
            if ((child.flags & SvgStyleFlags.PaintOrder) == 0)
                child.paintOrder = parent.paintOrder;
            // Fill
            if ((child.fill.flags & SvgFillFlags.Paint) == 0)
            {
                child.fill.paint.color = parent.fill.paint.color;
                child.fill.paint.none = parent.fill.paint.none;
                child.fill.paint.curColor = parent.fill.paint.curColor;
                if (parent.fill.paint.url != null) child.fill.paint.url = parent.fill.paint.url;
            }
            if ((child.fill.flags & SvgFillFlags.Opacity) == 0) child.fill.opacity = parent.fill.opacity;
            if ((child.fill.flags & SvgFillFlags.FillRule) == 0) child.fill.fillRule = parent.fill.fillRule;
            // Stroke
            if ((child.stroke.flags & SvgStrokeFlags.Paint) == 0)
            {
                child.stroke.paint.color = parent.stroke.paint.color;
                child.stroke.paint.none = parent.stroke.paint.none;
                child.stroke.paint.curColor = parent.stroke.paint.curColor;
                if (parent.stroke.paint.url != null) child.stroke.paint.url = parent.stroke.paint.url;
            }
            if ((child.stroke.flags & SvgStrokeFlags.Opacity) == 0) child.stroke.opacity = parent.stroke.opacity;
            if ((child.stroke.flags & SvgStrokeFlags.Width) == 0) child.stroke.width = parent.stroke.width;
            if ((child.stroke.flags & SvgStrokeFlags.Dash) == 0)
            {
                if (parent.stroke.dash.array.Count > 0)
                {
                    child.stroke.dash.array.Clear();
                    child.stroke.dash.array.AddRange(parent.stroke.dash.array);
                }
            }
            if ((child.stroke.flags & SvgStrokeFlags.DashOffset) == 0) child.stroke.dash.offset = parent.stroke.dash.offset;
            if ((child.stroke.flags & SvgStrokeFlags.Cap) == 0) child.stroke.cap = parent.stroke.cap;
            if ((child.stroke.flags & SvgStrokeFlags.Join) == 0) child.stroke.join = parent.stroke.join;
            if ((child.stroke.flags & SvgStrokeFlags.Miterlimit) == 0) child.stroke.miterlimit = parent.stroke.miterlimit;
        }

        private static void StyleCopy(SvgStyleProperty? to, SvgStyleProperty? from)
        {
            if (from == null || to == null) return;

            if (from.curColorSet)
            {
                to.color = from.color;
                to.curColorSet = true;
            }
            if ((from.flags & SvgStyleFlags.Opacity) != 0) to.opacity = from.opacity;
            if ((from.flags & SvgStyleFlags.PaintOrder) != 0) to.paintOrder = from.paintOrder;
            if ((from.flags & SvgStyleFlags.Display) != 0) to.display = from.display;
            // Fill
            to.fill.flags |= from.fill.flags;
            if ((from.fill.flags & SvgFillFlags.Paint) != 0)
            {
                to.fill.paint.color = from.fill.paint.color;
                to.fill.paint.none = from.fill.paint.none;
                to.fill.paint.curColor = from.fill.paint.curColor;
                if (from.fill.paint.url != null) to.fill.paint.url = from.fill.paint.url;
            }
            if ((from.fill.flags & SvgFillFlags.Opacity) != 0) to.fill.opacity = from.fill.opacity;
            if ((from.fill.flags & SvgFillFlags.FillRule) != 0) to.fill.fillRule = from.fill.fillRule;
            // Stroke
            to.stroke.flags |= from.stroke.flags;
            if ((from.stroke.flags & SvgStrokeFlags.Paint) != 0)
            {
                to.stroke.paint.color = from.stroke.paint.color;
                to.stroke.paint.none = from.stroke.paint.none;
                to.stroke.paint.curColor = from.stroke.paint.curColor;
                if (from.stroke.paint.url != null) to.stroke.paint.url = from.stroke.paint.url;
            }
            if ((from.stroke.flags & SvgStrokeFlags.Opacity) != 0) to.stroke.opacity = from.stroke.opacity;
            if ((from.stroke.flags & SvgStrokeFlags.Width) != 0) to.stroke.width = from.stroke.width;
            if ((from.stroke.flags & SvgStrokeFlags.Dash) != 0)
            {
                if (from.stroke.dash.array.Count > 0)
                {
                    to.stroke.dash.array.Clear();
                    to.stroke.dash.array.AddRange(from.stroke.dash.array);
                }
            }
            if ((from.stroke.flags & SvgStrokeFlags.DashOffset) != 0) to.stroke.dash.offset = from.stroke.dash.offset;
            if ((from.stroke.flags & SvgStrokeFlags.Cap) != 0) to.stroke.cap = from.stroke.cap;
            if ((from.stroke.flags & SvgStrokeFlags.Join) != 0) to.stroke.join = from.stroke.join;
            if ((from.stroke.flags & SvgStrokeFlags.Miterlimit) != 0) to.stroke.miterlimit = from.stroke.miterlimit;
        }

        // ========== Node helpers ==========

        private static SvgNode? GetDefsNode(SvgNode? node)
        {
            if (node == null) return null;
            while (node.parent != null) node = node.parent;
            if (node.type == SvgNodeType.Doc) return node.doc.defs;
            if (node.type == SvgNodeType.Defs) return node;
            return null;
        }

        private static SvgNode? FindNodeById(SvgNode? node, string? id)
        {
            if (node == null || id == null) return null;
            if (node.id != null && SvgHelper.StrAs(node.id, id)) return node;
            foreach (var child in node.child)
            {
                var result = FindNodeById(child, id);
                if (result != null) return result;
            }
            return null;
        }

        private static SvgNode? FindParentById(SvgNode node, string? id, SvgNode? doc)
        {
            var parent = node.parent;
            while (parent != null && parent != doc)
            {
                if (parent.id != null && id != null && SvgHelper.StrAs(parent.id, id)) return parent;
                parent = parent.parent;
            }
            return null;
        }

        private static void CopyAttr(SvgNode to, SvgNode from)
        {
            if (from.transform.HasValue) to.transform = from.transform;
            StyleCopy(to.style, from.style);
            to.style!.flags |= from.style!.flags;
            if (from.style.clipPath.url != null) to.style.clipPath.url = from.style.clipPath.url;
            if (from.style.mask.url != null) to.style.mask.url = from.style.mask.url;
            if (from.style.filter.url != null) to.style.filter.url = from.style.filter.url;

            switch (from.type)
            {
                case SvgNodeType.Circle:
                    to.circle.cx = from.circle.cx; to.circle.cy = from.circle.cy; to.circle.r = from.circle.r;
                    break;
                case SvgNodeType.Ellipse:
                    to.ellipse.cx = from.ellipse.cx; to.ellipse.cy = from.ellipse.cy; to.ellipse.rx = from.ellipse.rx; to.ellipse.ry = from.ellipse.ry;
                    break;
                case SvgNodeType.Rect:
                    to.rect.x = from.rect.x; to.rect.y = from.rect.y; to.rect.w = from.rect.w; to.rect.h = from.rect.h;
                    to.rect.rx = from.rect.rx; to.rect.ry = from.rect.ry; to.rect.hasRx = from.rect.hasRx; to.rect.hasRy = from.rect.hasRy;
                    break;
                case SvgNodeType.Line:
                    to.line.x1 = from.line.x1; to.line.y1 = from.line.y1; to.line.x2 = from.line.x2; to.line.y2 = from.line.y2;
                    break;
                case SvgNodeType.Path:
                    to.path.path = from.path.path;
                    break;
                case SvgNodeType.Polygon:
                    to.polygon.pts.Clear(); to.polygon.pts.AddRange(from.polygon.pts);
                    break;
                case SvgNodeType.Polyline:
                    to.polyline.pts.Clear(); to.polyline.pts.AddRange(from.polyline.pts);
                    break;
                case SvgNodeType.Image:
                    to.image.x = from.image.x; to.image.y = from.image.y; to.image.w = from.image.w; to.image.h = from.image.h;
                    to.image.href = from.image.href;
                    break;
                case SvgNodeType.Use:
                    to.use.x = from.use.x; to.use.y = from.use.y; to.use.w = from.use.w; to.use.h = from.use.h;
                    to.use.isWidthSet = from.use.isWidthSet; to.use.isHeightSet = from.use.isHeightSet;
                    to.use.symbol = from.use.symbol;
                    break;
                case SvgNodeType.Text:
                    to.text.x = from.text.x; to.text.y = from.text.y; to.text.fontSize = from.text.fontSize;
                    to.text.text = from.text.text; to.text.fontFamily = from.text.fontFamily;
                    break;
            }
        }

        private static void CloneNode(SvgNode? from, SvgNode? parent, int depth)
        {
            if (depth == 8192) return;
            if (from == null || parent == null || from == parent) return;

            var newNode = CreateNode(parent, from.type);
            StyleInherit(newNode.style, parent.style);
            CopyAttr(newNode, from);

            foreach (var child in from.child)
            {
                CloneNode(child, newNode, depth + 1);
            }
        }

        // ========== CSS application ==========

        private static bool CssApplyClass(SvgNode node, string? classString, SvgNode? styleRoot)
        {
            if (classString == null || styleRoot == null) return false;

            bool allFound = true;
            var tempNode = new SvgNode();
            tempNode.style = new SvgStyleProperty();
            tempNode.type = node.type;
            tempNode.style.opacity = 255;
            tempNode.style.fill.opacity = 255;
            tempNode.style.stroke.opacity = 255;

            var classes = classString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var appliedClasses = new HashSet<string>();

            foreach (var cls in classes)
            {
                var name = cls.Trim();
                if (string.IsNullOrEmpty(name)) continue;
                if (!appliedClasses.Add(name)) continue;

                bool found = false;
                var cssNode = SvgCssStyle.FindStyleNode(styleRoot, name);
                if (cssNode != null)
                {
                    SvgCssStyle.CopyStyleAttr(tempNode, cssNode, true);
                    found = true;
                }
                var cssNodeTyped = SvgCssStyle.FindStyleNode(styleRoot, name, node.type);
                if (cssNodeTyped != null)
                {
                    SvgCssStyle.CopyStyleAttr(tempNode, cssNodeTyped, true);
                    found = true;
                }
                if (!found) allFound = false;
            }

            SvgCssStyle.CopyStyleAttr(node, tempNode);
            return allFound;
        }

        private static void CssApplyStyleToPostponeds(List<SvgNodeIdPair> postponeds, SvgNode? style)
        {
            foreach (var pair in postponeds)
            {
                CssApplyClass(pair.node, pair.id, style);
            }
        }

        // ========== Gradient inheritance helpers ==========

        private static SvgStyleGradient? CloneGradient(SvgStyleGradient? from)
        {
            if (from == null) return null;
            var grad = new SvgStyleGradient();
            grad.type = from.type;
            grad.id = from.id;
            grad.@ref = from.@ref;
            grad.spread = from.spread;
            grad.userSpace = from.userSpace;
            grad.flags = from.flags;
            grad.transform = from.transform;

            if (from.linear != null)
            {
                grad.linear = new SvgLinearGradient
                {
                    x1 = from.linear.x1, y1 = from.linear.y1, x2 = from.linear.x2, y2 = from.linear.y2,
                    isX1Percentage = from.linear.isX1Percentage, isY1Percentage = from.linear.isY1Percentage,
                    isX2Percentage = from.linear.isX2Percentage, isY2Percentage = from.linear.isY2Percentage
                };
            }
            if (from.radial != null)
            {
                grad.radial = new SvgRadialGradient
                {
                    cx = from.radial.cx, cy = from.radial.cy, fx = from.radial.fx, fy = from.radial.fy,
                    r = from.radial.r, fr = from.radial.fr,
                    isCxPercentage = from.radial.isCxPercentage, isCyPercentage = from.radial.isCyPercentage,
                    isFxPercentage = from.radial.isFxPercentage, isFyPercentage = from.radial.isFyPercentage,
                    isRPercentage = from.radial.isRPercentage, isFrPercentage = from.radial.isFrPercentage
                };
            }
            grad.stops.AddRange(from.stops);
            return grad;
        }

        private static void InheritGradient(SvgLoaderData loader, SvgStyleGradient to, SvgStyleGradient from)
        {
            if ((to.flags & SvgGradientFlags.SpreadMethod) == 0 && (from.flags & SvgGradientFlags.SpreadMethod) != 0)
            {
                to.spread = from.spread;
                to.flags |= SvgGradientFlags.SpreadMethod;
            }
            bool gradUnitSet = (to.flags & SvgGradientFlags.GradientUnits) != 0;
            if (!gradUnitSet && (from.flags & SvgGradientFlags.GradientUnits) != 0)
            {
                to.userSpace = from.userSpace;
                to.flags |= SvgGradientFlags.GradientUnits;
            }
            if (to.transform == null && from.transform != null) to.transform = from.transform;

            var g = loader.svgParse!.global;

            if (to.type == SvgGradientType.Linear && to.linear != null && from.linear != null)
            {
                InheritLinearCoord(loader, to, from, SvgGradientFlags.X1, gradUnitSet, ref to.linear.x1, from.linear.x1, ref to.linear.isX1Percentage, from.linear.isX1Percentage, true);
                InheritLinearCoord(loader, to, from, SvgGradientFlags.Y1, gradUnitSet, ref to.linear.y1, from.linear.y1, ref to.linear.isY1Percentage, from.linear.isY1Percentage, false);
                InheritLinearCoord(loader, to, from, SvgGradientFlags.X2, gradUnitSet, ref to.linear.x2, from.linear.x2, ref to.linear.isX2Percentage, from.linear.isX2Percentage, true);
                InheritLinearCoord(loader, to, from, SvgGradientFlags.Y2, gradUnitSet, ref to.linear.y2, from.linear.y2, ref to.linear.isY2Percentage, from.linear.isY2Percentage, false);
            }
            else if (to.type == SvgGradientType.Radial && to.radial != null && from.radial != null)
            {
                var diag = MathF.Sqrt(g.w * g.w + g.h * g.h) / MathF.Sqrt(2.0f);

                // cx
                bool cxSet = (to.flags & SvgGradientFlags.Cx) != 0;
                if (!cxSet && (from.flags & SvgGradientFlags.Cx) != 0) { to.radial.cx = from.radial.cx; to.radial.isCxPercentage = from.radial.isCxPercentage; to.flags |= SvgGradientFlags.Cx; }
                if (!gradUnitSet && cxSet) { if (to.userSpace && !to.radial.isCxPercentage) to.radial.cx /= g.w; if ((to.flags & SvgGradientFlags.Fx) == 0) to.radial.fx = to.radial.cx; }
                if (to.userSpace != from.userSpace && gradUnitSet && !cxSet) { if (!to.radial.isCxPercentage) { if (to.userSpace) to.radial.cx /= g.w; else to.radial.cx *= g.w; } }

                // cy
                bool cySet = (to.flags & SvgGradientFlags.Cy) != 0;
                if (!cySet && (from.flags & SvgGradientFlags.Cy) != 0) { to.radial.cy = from.radial.cy; to.radial.isCyPercentage = from.radial.isCyPercentage; to.flags |= SvgGradientFlags.Cy; }
                if (!gradUnitSet && cySet) { if (to.userSpace && !to.radial.isCyPercentage) to.radial.cy /= g.h; if ((to.flags & SvgGradientFlags.Fy) == 0) to.radial.fy = to.radial.cy; }
                if (to.userSpace != from.userSpace && gradUnitSet && !cySet) { if (!to.radial.isCyPercentage) { if (to.userSpace) to.radial.cy /= g.h; else to.radial.cy *= g.h; } }

                // fx
                bool fxSet = (to.flags & SvgGradientFlags.Fx) != 0;
                if (!fxSet && (from.flags & SvgGradientFlags.Fx) != 0) { to.radial.fx = from.radial.fx; to.radial.isFxPercentage = from.radial.isFxPercentage; to.flags |= SvgGradientFlags.Fx; }
                if (!gradUnitSet && fxSet) { if (to.userSpace && !to.radial.isFxPercentage) to.radial.fx /= g.w; }
                if (to.userSpace != from.userSpace && gradUnitSet && !fxSet) { if ((to.flags & SvgGradientFlags.Fx) == 0) { /* skip recalc for unset fx */ } else if (!to.radial.isFxPercentage) { if (to.userSpace) to.radial.fx /= g.w; else to.radial.fx *= g.w; } }

                // fy
                bool fySet = (to.flags & SvgGradientFlags.Fy) != 0;
                if (!fySet && (from.flags & SvgGradientFlags.Fy) != 0) { to.radial.fy = from.radial.fy; to.radial.isFyPercentage = from.radial.isFyPercentage; to.flags |= SvgGradientFlags.Fy; }
                if (!gradUnitSet && fySet) { if (to.userSpace && !to.radial.isFyPercentage) to.radial.fy /= g.h; }
                if (to.userSpace != from.userSpace && gradUnitSet && !fySet) { if ((to.flags & SvgGradientFlags.Fy) == 0) { /* skip recalc for unset fy */ } else if (!to.radial.isFyPercentage) { if (to.userSpace) to.radial.fy /= g.h; else to.radial.fy *= g.h; } }

                // r
                bool rSet = (to.flags & SvgGradientFlags.R) != 0;
                if (!rSet && (from.flags & SvgGradientFlags.R) != 0) { to.radial.r = from.radial.r; to.radial.isRPercentage = from.radial.isRPercentage; to.flags |= SvgGradientFlags.R; }
                if (!gradUnitSet && rSet && diag > 0) { if (to.userSpace && !to.radial.isRPercentage) to.radial.r /= diag; }
                if (to.userSpace != from.userSpace && gradUnitSet && !rSet && diag > 0) { if (!to.radial.isRPercentage) { if (to.userSpace) to.radial.r /= diag; else to.radial.r *= diag; } }

                // fr
                bool frSet = (to.flags & SvgGradientFlags.Fr) != 0;
                if (!frSet && (from.flags & SvgGradientFlags.Fr) != 0) { to.radial.fr = from.radial.fr; to.radial.isFrPercentage = from.radial.isFrPercentage; to.flags |= SvgGradientFlags.Fr; }
                if (!gradUnitSet && frSet && diag > 0) { if (to.userSpace && !to.radial.isFrPercentage) to.radial.fr /= diag; }
                if (to.userSpace != from.userSpace && gradUnitSet && !frSet && diag > 0) { if (!to.radial.isFrPercentage) { if (to.userSpace) to.radial.fr /= diag; else to.radial.fr *= diag; } }
            }
            if (to.stops.Count == 0) to.stops.AddRange(from.stops);
        }

        private static void InheritLinearCoord(SvgLoaderData loader, SvgStyleGradient to, SvgStyleGradient from,
            SvgGradientFlags flag, bool gradUnitSet, ref float coord, float fromCoord, ref bool isPercentage, bool fromIsPercentage, bool isHorizontal)
        {
            var g = loader.svgParse!.global;
            var dim = isHorizontal ? g.w : g.h;
            bool coordSet = (to.flags & flag) != 0;

            // Step 1: Inherit missing coordinate
            if (!coordSet && (from.flags & flag) != 0) { coord = fromCoord; isPercentage = fromIsPercentage; to.flags |= flag; }

            // Step 2: GradUnits not set directly, coord set -> recalc
            if (!gradUnitSet && coordSet) { if (to.userSpace && !isPercentage && dim != 0) coord /= dim; }

            // Step 3: GradUnits set, coord not set directly -> recalc inherited coord
            if (to.userSpace == from.userSpace) return;
            if (gradUnitSet && !coordSet) { if (!isPercentage && dim != 0) { if (to.userSpace) coord /= dim; else coord *= dim; } }
        }
    }
}
