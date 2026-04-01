// Ported from ThorVG/src/loaders/svg/tvgSvgLoader.cpp
// Main SvgLoader class: open, read, close, run, header, XML callbacks.

using System;
using System.Collections.Generic;

namespace ThorVG
{
    public static partial class SvgLoader
    {
        // ========== XML Callbacks ==========

        private static void SvgLoaderParserXmlClose(SvgParserContext ctx, string content, int offset, int length)
        {
            int itrEnd = offset + length;
            int pos = SvgUtil.SkipWhiteSpace(content, offset, itrEnd);
            int end = pos;
            while (end < itrEnd && content[end] != '>') end++;
            if (end == pos) return;

            int sz = end - pos;
            while (sz > 0 && char.IsWhiteSpace(content[pos + sz - 1])) sz--;
            if (sz <= 0) return;
            var tagName = content.Substring(pos, sz);

            if (ctx.gradientStack.Count > 0 && ctx.gradientStack[ctx.gradientStack.Count - 1] == null)
            {
                ctx.gradientStack.RemoveAt(ctx.gradientStack.Count - 1);
                return;
            }

            for (int i = 0; i < GroupTags.Length; i++)
            {
                if (SvgHelper.StrAs(tagName, GroupTags[i].tag))
                {
                    if (ctx.stack.Count > 0) ctx.stack.RemoveAt(ctx.stack.Count - 1);
                    break;
                }
            }
            for (int i = 0; i < GradientTags.Length; i++)
            {
                if (SvgHelper.StrAs(tagName, GradientTags[i].tag))
                {
                    if (ctx.gradientStack.Count > 0) ctx.gradientStack.RemoveAt(ctx.gradientStack.Count - 1);
                    break;
                }
            }
            for (int i = 0; i < GraphicsTags.Length; i++)
            {
                if (SvgHelper.StrAs(tagName, GraphicsTags[i].tag))
                {
                    ctx.currentGraphicsNode = null;
                    if (SvgHelper.StrAs(tagName, "text")) ctx.openedTag = OpenedTagType.Other;
                    if (ctx.stack.Count > 0) ctx.stack.RemoveAt(ctx.stack.Count - 1);
                    break;
                }
            }
        }

        private static void SvgLoaderParserXmlOpen(SvgParserContext ctx, string content, int offset, int length, bool empty)
        {
            // Find the attributes portion
            int attrPos = XmlParser.FindAttributesTag(content, offset, length);
            int attrsOffset;
            int attrsLength;

            if (attrPos < 0)
            {
                attrPos = offset;
                while (attrPos < offset + length && content[attrPos] != '>') attrPos++;
                if (empty && attrPos > offset) attrPos--;
            }

            int sz = attrPos - offset;
            while (sz > 0 && char.IsWhiteSpace(content[offset + sz - 1])) sz--;
            if (sz <= 0 || sz > 19) return;

            var tagName = content.Substring(offset, sz);
            attrsOffset = offset + sz;
            attrsLength = length - sz;

            // If we're inside a gradient with a null top (nested non-gradient/non-stop tag), just push null
            if (ctx.gradientStack.Count > 0 && ctx.gradientStack[ctx.gradientStack.Count - 1] == null)
            {
                if (!empty) ctx.gradientStack.Add(null);
                return;
            }

            // Handle <stop> elements - must be inside a gradient
            if (SvgHelper.StrAs(tagName, "stop"))
            {
                if (ctx.gradientStack.Count == 0)
                {
                    if (!empty) ctx.gradientStack.Add(null);
                    return;
                }
                ctx.svgParse!.gradStop = new Fill.ColorStop { offset = 0, r = 0, g = 0, b = 0, a = 255 };
                ctx.svgParse.flags = SvgStopStyleFlags.StopDefault;
                XmlParser.ParseAttributes(content, attrsOffset, attrsLength, AttrParseStops, ctx);
                ctx.gradientStack[ctx.gradientStack.Count - 1]!.stops.Add(ctx.svgParse.gradStop);
                if (!empty) ctx.gradientStack.Add(null);
                return;
            }

            // If we're inside a gradient, ignore non-stop/non-gradient elements
            if (ctx.gradientStack.Count > 0)
            {
                if (!empty) ctx.gradientStack.Add(null);
                return;
            }

            FactoryMethod? method;
            GradientFactoryMethod? gradientMethod;
            SvgNode? node = null;
            SvgNode? parent = null;

            if ((method = FindGroupFactory(tagName)) != null)
            {
                if (empty) return;
                if (ctx.doc == null)
                {
                    if (!SvgHelper.StrAs(tagName, "svg")) return;
                    node = method(ctx, null, content, attrsOffset, attrsLength, XmlParser.ParseAttributes);
                    ctx.doc = node;
                }
                else
                {
                    if (SvgHelper.StrAs(tagName, "svg")) return;
                    parent = ctx.stack.Count > 0 ? ctx.stack[ctx.stack.Count - 1] : ctx.doc;
                    if (SvgHelper.StrAs(tagName, "style"))
                    {
                        if (ctx.cssStyle == null)
                        {
                            node = method(ctx, null, content, attrsOffset, attrsLength, XmlParser.ParseAttributes);
                            ctx.cssStyle = node;
                            ctx.doc.doc.style = node;
                            ctx.openedTag = OpenedTagType.Style;
                        }
                    }
                    else
                    {
                        node = method(ctx, parent, content, attrsOffset, attrsLength, XmlParser.ParseAttributes);
                    }
                }
                if (node == null) return;
                if (node.type != SvgNodeType.Defs || !empty) ctx.stack.Add(node);
            }
            else if ((method = FindGraphicsFactory(tagName)) != null)
            {
                parent = ctx.stack.Count > 0 ? ctx.stack[ctx.stack.Count - 1] : ctx.doc;
                node = method(ctx, parent, content, attrsOffset, attrsLength, XmlParser.ParseAttributes);
                if (node != null && !empty)
                {
                    if (SvgHelper.StrAs(tagName, "text")) ctx.openedTag = OpenedTagType.Text;
                    var defs = CreateDefsNode(ctx, null, "", 0, 0, null);
                    ctx.stack.Add(defs!);
                    ctx.currentGraphicsNode = node;
                }
            }
            else if ((gradientMethod = FindGradientFactory(tagName)) != null)
            {
                var gradient = gradientMethod(ctx, content, attrsOffset, attrsLength);
                if (ctx.gradientStack.Count == 0 && gradient != null)
                {
                    if (ctx.def != null && ctx.doc!.doc.defs != null)
                        ctx.def.defs.gradients.Add(gradient);
                    else
                        ctx.gradients.Add(gradient);
                }
                if (!empty) ctx.gradientStack.Add(gradient);
            }
            else
            {
                // Unsupported
            }
        }

        private static void SvgLoaderParserText(SvgParserContext ctx, string content, int offset, int length)
        {
            var text = ctx.svgParse!.node!.text;
            text.text = (text.text ?? "") + content.Substring(offset, Math.Min(length, content.Length - offset));
        }

        private static void SvgLoaderParserXmlCssStyle(SvgParserContext ctx, string content, int offset, int length)
        {
            int pos = offset;
            int remaining = length;
            while (remaining > 0)
            {
                var nextPos = XmlParser.ParseCSSAttribute(content, pos, remaining,
                    out string? tag, out string? name, out int attrsOffset, out int attrsLength);
                if (nextPos < 0) break;

                FactoryMethod? method;
                SvgNode? node;

                if (tag != null && (method = FindGroupFactory(tag)) != null)
                {
                    node = method(ctx, ctx.cssStyle, content, attrsOffset, attrsLength, XmlParser.ParseW3CAttribute);
                    if (node != null) node.id = CopyId(name);
                }
                else if (tag != null && (method = FindGraphicsFactory(tag)) != null)
                {
                    node = method(ctx, ctx.cssStyle, content, attrsOffset, attrsLength, XmlParser.ParseW3CAttribute);
                    if (node != null) node.id = CopyId(name);
                }
                else if (tag != null && SvgHelper.StrAs(tag, "all"))
                {
                    if (name != null)
                    {
                        var ids = name.Split(',');
                        foreach (var rawId in ids)
                        {
                            var id = rawId.Trim();
                            if (id.StartsWith(".")) id = id.Substring(1);
                            if (string.IsNullOrEmpty(id)) continue;

                            var cssNode = SvgCssStyle.FindStyleNode(ctx.cssStyle, id);
                            if (cssNode != null)
                            {
                                var oldNode = ctx.svgParse!.node;
                                ctx.svgParse.node = cssNode;
                                XmlParser.ParseW3CAttribute(content, attrsOffset, attrsLength, AttrParseCssStyleNode, ctx);
                                ctx.svgParse.node = oldNode;
                            }
                            else
                            {
                                node = CreateCssStyleNode(ctx, ctx.cssStyle, content, attrsOffset, attrsLength, XmlParser.ParseW3CAttribute);
                                if (node != null) node.id = CopyId(id);
                            }
                        }
                    }
                }
                else if (tag != null && SvgHelper.StrAs(tag, "@font-face"))
                {
                    CreateFontFace(ctx, content, attrsOffset, attrsLength, XmlParser.ParseW3CAttribute);
                }

                remaining -= (nextPos - pos);
                pos = nextPos;
            }
            ctx.openedTag = OpenedTagType.Other;
        }

        private static bool SvgLoaderParser(SvgParserContext ctx, XMLType type, string content, int offset, int length)
        {
            switch (type)
            {
                case XMLType.Open:
                    SvgLoaderParserXmlOpen(ctx, content, offset, length, false);
                    break;
                case XMLType.OpenEmpty:
                    SvgLoaderParserXmlOpen(ctx, content, offset, length, true);
                    break;
                case XMLType.Close:
                    SvgLoaderParserXmlClose(ctx, content, offset, length);
                    break;
                case XMLType.Data:
                case XMLType.CData:
                    if (ctx.openedTag == OpenedTagType.Style) SvgLoaderParserXmlCssStyle(ctx, content, offset, length);
                    else if (ctx.openedTag == OpenedTagType.Text) SvgLoaderParserText(ctx, content, offset, length);
                    break;
            }
            return true;
        }

        // ========== Update passes ==========

        private static void UpdateStyle(SvgNode? node, SvgStyleProperty? parentStyle)
        {
            if (node == null) return;
            StyleInherit(node.style, parentStyle);
            foreach (var child in node.child)
            {
                UpdateStyle(child, node.style);
            }
        }

        private static void UpdateGradient(SvgParserContext ctx, SvgNode? node, List<SvgStyleGradient> gradients)
        {
            if (node == null) return;

            if (node.child.Count > 0)
            {
                foreach (var child in node.child)
                    UpdateGradient(ctx, child, gradients);
            }
            else
            {
                if (node.style!.fill.paint.url != null)
                {
                    SvgStyleGradient? newGrad = null;
                    foreach (var g in gradients)
                    {
                        if (g.id != null && SvgHelper.StrAs(g.id, node.style.fill.paint.url))
                        {
                            newGrad = CloneGradient(g);
                            break;
                        }
                    }
                    if (newGrad != null)
                    {
                        if (newGrad.@ref != null)
                        {
                            foreach (var g in gradients)
                            {
                                if (g.id != null && SvgHelper.StrAs(g.id, newGrad.@ref))
                                {
                                    InheritGradient(ctx, newGrad, g);
                                    break;
                                }
                            }
                        }
                        node.style.fill.paint.gradient = newGrad;
                    }
                }
                if (node.style.stroke.paint.url != null)
                {
                    SvgStyleGradient? newGrad = null;
                    foreach (var g in gradients)
                    {
                        if (g.id != null && SvgHelper.StrAs(g.id, node.style.stroke.paint.url))
                        {
                            newGrad = CloneGradient(g);
                            break;
                        }
                    }
                    if (newGrad != null)
                    {
                        if (newGrad.@ref != null)
                        {
                            foreach (var g in gradients)
                            {
                                if (g.id != null && SvgHelper.StrAs(g.id, newGrad.@ref))
                                {
                                    InheritGradient(ctx, newGrad, g);
                                    break;
                                }
                            }
                        }
                        node.style.stroke.paint.gradient = newGrad;
                    }
                }
            }
        }

        private static void UpdateComposite(SvgNode? node, SvgNode? root)
        {
            if (node == null) return;
            if (node.style!.clipPath.url != null && node.style.clipPath.node == null)
            {
                var findResult = FindNodeById(root, node.style.clipPath.url);
                if (findResult != null) node.style.clipPath.node = findResult;
            }
            if (node.style.mask.url != null && node.style.mask.node == null)
            {
                var findResult = FindNodeById(root, node.style.mask.url);
                if (findResult != null) node.style.mask.node = findResult;
            }
            foreach (var child in node.child)
            {
                UpdateComposite(child, root);
            }
        }

        private static void UpdateFilter(SvgNode? node, SvgNode? root)
        {
            if (node == null) return;
            if (node.style!.filter.url != null && node.style.filter.node == null)
            {
                node.style.filter.node = FindNodeById(root, node.style.filter.url);
            }
            foreach (var child in node.child)
            {
                UpdateFilter(child, root);
            }
        }

        /// <summary>
        /// Recursively checks whether cloneNode is a descendant of node, to prevent circular references.
        /// </summary>
        internal static bool CheckPostponed(SvgNode node, SvgNode cloneNode, int depth)
        {
            if (node == cloneNode) return true;
            if (depth >= 512) return false;
            foreach (var child in node.child)
            {
                if (CheckPostponed(child, cloneNode, depth + 1)) return true;
            }
            return false;
        }

        private static void ClonePostponedNodes(Inlist<SvgNodeIdPair> cloneNodes, SvgNode? doc)
        {
            uint cloneNodesCount = (uint)cloneNodes.Count;
            uint postponeCount = 0;
            var nodeIdPair = cloneNodes.PopFront();
            while (nodeIdPair != null)
            {
                if (postponeCount >= cloneNodesCount)
                {
                    // Circular use reference detected - discard all remaining
                    do
                    {
                        // nodeIdPair discarded
                    } while ((nodeIdPair = cloneNodes.PopFront()) != null);
                    break;
                }

                if (FindParentById(nodeIdPair.node, nodeIdPair.id, doc) == null)
                {
                    // Check if none of nodeFrom's children are in the cloneNodes list
                    var postpone = false;
                    var defs = GetDefsNode(nodeIdPair.node);
                    var nodeFrom = FindNodeById(defs, nodeIdPair.id);
                    if (nodeFrom == null) nodeFrom = FindNodeById(doc, nodeIdPair.id);

                    if (nodeFrom != null)
                    {
                        var pair = cloneNodes.Head;
                        while (pair != null)
                        {
                            if (CheckPostponed(nodeFrom, pair.node, 1))
                            {
                                postpone = true;
                                cloneNodes.Back(nodeIdPair);
                                break;
                            }
                            pair = pair.Next;
                        }
                    }

                    // Since none of the child nodes of nodeFrom are in the cloneNodes list, clone immediately
                    if (!postpone)
                    {
                        CloneNode(nodeFrom, nodeIdPair.node, 0);
                        if (nodeFrom != null && nodeFrom.type == SvgNodeType.Symbol && nodeIdPair.node.type == SvgNodeType.Use)
                        {
                            nodeIdPair.node.use.symbol = nodeFrom;
                        }
                        postponeCount = 0;
                        --cloneNodesCount;
                    }
                    else
                    {
                        ++postponeCount;
                    }
                }
                else
                {
                    postponeCount = 0;
                    --cloneNodesCount;
                }
                nodeIdPair = cloneNodes.PopFront();
            }
        }

        // ========== Valid check parser ==========

        private static bool SvgLoaderParserForValidCheckXmlOpen(SvgParserContext ctx, string content, int offset, int length)
        {
            int attrPos = XmlParser.FindAttributesTag(content, offset, length);
            int attrsOffset;
            int attrsLength;

            if (attrPos < 0)
            {
                attrPos = offset;
                while (attrPos < offset + length && content[attrPos] != '>') attrPos++;
            }

            int sz = attrPos - offset;
            while (sz > 0 && char.IsWhiteSpace(content[offset + sz - 1])) sz--;
            if (sz <= 0 || sz > 19) return false;

            var tagName = content.Substring(offset, sz);
            attrsOffset = offset + sz;
            attrsLength = length - sz;

            var method = FindGroupFactory(tagName);
            if (method != null)
            {
                if (ctx.doc == null)
                {
                    if (!SvgHelper.StrAs(tagName, "svg")) return true;
                    var node = method(ctx, null, content, attrsOffset, attrsLength, XmlParser.ParseAttributes);
                    ctx.doc = node;
                    if (node != null) ctx.stack.Add(node);
                    return false; // Found svg tag
                }
            }
            return true;
        }

        private static bool SvgLoaderParserForValidCheck(SvgParserContext ctx, XMLType type, string content, int offset, int length)
        {
            switch (type)
            {
                case XMLType.Open:
                case XMLType.OpenEmpty:
                    return SvgLoaderParserForValidCheckXmlOpen(ctx, content, offset, length);
                default:
                    return true;
            }
        }

        // ========== Public API ==========

        /// <summary>
        /// Parse SVG data from a string. Returns the root scene if successful.
        /// This is a simplified synchronous entry point that combines open/header/run.
        /// </summary>
        public static SvgNode? Parse(string svgContent)
        {
            return Parse(svgContent, out _);
        }

        /// <summary>
        /// Parse SVG data from a string. Returns the root doc node and the loader data
        /// needed for scene building.
        /// </summary>
        public static SvgNode? Parse(string svgContent, out SvgParserContext ctxOut)
        {
            var ctx = new SvgParserContext();
            ctx.svgParse = new SvgParser();
            ctx.svgParse.flags = SvgStopStyleFlags.StopDefault;

            // Header pass: find <svg> tag
            XmlParser.Parse(svgContent, svgContent.Length, true, SvgLoaderParserForValidCheck, ctx);

            if (ctx.doc == null || ctx.doc.type != SvgNodeType.Doc)
            {
                ctxOut = ctx;
                return null;
            }

            // Full parse
            ctx.svgParse = new SvgParser();
            ctx.svgParse.flags = SvgStopStyleFlags.StopDefault;
            ctx.doc = null;
            ctx.stack.Clear();
            ctx.gradients.Clear();
            ctx.gradientStack.Clear();

            XmlParser.Parse(svgContent, svgContent.Length, true, SvgLoaderParser, ctx);

            if (ctx.doc == null)
            {
                ctxOut = ctx;
                return null;
            }

            var defs = ctx.doc.doc.defs;

            // Apply CSS styling
            if (ctx.nodesToStyle.Count > 0)
                CssApplyStyleToPostponeds(ctx.nodesToStyle, ctx.cssStyle);
            if (ctx.cssStyle != null)
                SvgCssStyle.UpdateStyle(ctx.doc, ctx.cssStyle);

            // Clone postponed nodes
            if (!ctx.cloneNodes.Empty())
                ClonePostponedNodes(ctx.cloneNodes, ctx.doc);

            // Update passes
            UpdateComposite(ctx.doc, ctx.doc);
            if (defs != null) UpdateComposite(ctx.doc, defs);

            UpdateFilter(ctx.doc, ctx.doc);
            if (defs != null) UpdateFilter(ctx.doc, defs);

            UpdateStyle(ctx.doc, null);
            if (defs != null) UpdateStyle(defs, null);

            if (ctx.gradients.Count > 0)
                UpdateGradient(ctx, ctx.doc, ctx.gradients);
            if (defs != null)
                UpdateGradient(ctx, ctx.doc, defs.defs.gradients);

            ctxOut = ctx;
            return ctx.doc;
        }

        /// <summary>
        /// Get the viewBox, width, height, and viewFlag from a parsed SVG doc node.
        /// </summary>
        public static (Box vbox, float w, float h, SvgViewFlag viewFlag) GetViewInfo(SvgNode doc)
        {
            var viewFlag = doc.doc.viewFlag;
            float w, h;
            Box vbox;

            if ((viewFlag & SvgViewFlag.Viewbox) != 0)
            {
                vbox = doc.doc.vbox;
                if ((viewFlag & SvgViewFlag.Width) != 0)
                {
                    w = doc.doc.w;
                }
                else
                {
                    w = doc.doc.vbox.w;
                    if ((viewFlag & SvgViewFlag.WidthInPercent) != 0)
                    {
                        w *= doc.doc.w;
                        viewFlag ^= SvgViewFlag.WidthInPercent;
                    }
                    viewFlag |= SvgViewFlag.Width;
                }
                if ((viewFlag & SvgViewFlag.Height) != 0)
                {
                    h = doc.doc.h;
                }
                else
                {
                    h = doc.doc.vbox.h;
                    if ((viewFlag & SvgViewFlag.HeightInPercent) != 0)
                    {
                        h *= doc.doc.h;
                        viewFlag ^= SvgViewFlag.HeightInPercent;
                    }
                    viewFlag |= SvgViewFlag.Height;
                }
            }
            else
            {
                vbox.x = 0; vbox.y = 0;
                if ((viewFlag & SvgViewFlag.Width) != 0)
                {
                    vbox.w = w = doc.doc.w;
                }
                else
                {
                    vbox.w = 1.0f;
                    w = (viewFlag & SvgViewFlag.WidthInPercent) != 0 ? doc.doc.w : 1.0f;
                }
                if ((viewFlag & SvgViewFlag.Height) != 0)
                {
                    vbox.h = h = doc.doc.h;
                }
                else
                {
                    vbox.h = 1.0f;
                    h = (viewFlag & SvgViewFlag.HeightInPercent) != 0 ? doc.doc.h : 1.0f;
                }
            }
            return (vbox, w, h, viewFlag);
        }
    }
}
