// Ported from ThorVG/src/loaders/svg/tvgSvgLoader.cpp
// Main SvgLoader class: open, read, close, run, header, XML callbacks.

using System;
using System.Collections.Generic;

namespace ThorVG
{
    public static partial class SvgLoader
    {
        // ========== XML Callbacks ==========

        private static void SvgLoaderParserXmlClose(SvgLoaderData loader, string content, int offset, int length)
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

            for (int i = 0; i < GroupTags.Length; i++)
            {
                if (SvgHelper.StrAs(tagName, GroupTags[i].tag))
                {
                    if (loader.stack.Count > 0) loader.stack.RemoveAt(loader.stack.Count - 1);
                    break;
                }
            }
            for (int i = 0; i < GradientTags.Length; i++)
            {
                if (SvgHelper.StrAs(tagName, GradientTags[i].tag))
                {
                    if (loader.gradientStack.Count > 0) loader.gradientStack.RemoveAt(loader.gradientStack.Count - 1);
                    break;
                }
            }
            for (int i = 0; i < GraphicsTags.Length; i++)
            {
                if (SvgHelper.StrAs(tagName, GraphicsTags[i].tag))
                {
                    loader.currentGraphicsNode = null;
                    if (SvgHelper.StrAs(tagName, "text")) loader.openedTag = OpenedTagType.Other;
                    if (loader.stack.Count > 0) loader.stack.RemoveAt(loader.stack.Count - 1);
                    break;
                }
            }
            loader.level--;
        }

        private static void SvgLoaderParserXmlOpen(SvgLoaderData loader, string content, int offset, int length, bool empty)
        {
            loader.level++;

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

            FactoryMethod? method;
            GradientFactoryMethod? gradientMethod;
            SvgNode? node = null;
            SvgNode? parent = null;

            if ((method = FindGroupFactory(tagName)) != null)
            {
                if (empty) return;
                if (loader.doc == null)
                {
                    if (!SvgHelper.StrAs(tagName, "svg")) return;
                    node = method(loader, null, content, attrsOffset, attrsLength, XmlParser.ParseAttributes);
                    loader.doc = node;
                }
                else
                {
                    if (SvgHelper.StrAs(tagName, "svg")) return;
                    parent = loader.stack.Count > 0 ? loader.stack[loader.stack.Count - 1] : loader.doc;
                    if (SvgHelper.StrAs(tagName, "style"))
                    {
                        if (loader.cssStyle == null)
                        {
                            node = method(loader, null, content, attrsOffset, attrsLength, XmlParser.ParseAttributes);
                            loader.cssStyle = node;
                            loader.doc.doc.style = node;
                            loader.openedTag = OpenedTagType.Style;
                        }
                    }
                    else
                    {
                        node = method(loader, parent, content, attrsOffset, attrsLength, XmlParser.ParseAttributes);
                    }
                }
                if (node == null) return;
                if (node.type != SvgNodeType.Defs || !empty) loader.stack.Add(node);
            }
            else if ((method = FindGraphicsFactory(tagName)) != null)
            {
                parent = loader.stack.Count > 0 ? loader.stack[loader.stack.Count - 1] : loader.doc;
                node = method(loader, parent, content, attrsOffset, attrsLength, XmlParser.ParseAttributes);
                if (node != null && !empty)
                {
                    if (SvgHelper.StrAs(tagName, "text")) loader.openedTag = OpenedTagType.Text;
                    var defs = CreateDefsNode(loader, null, "", 0, 0, null);
                    loader.stack.Add(defs!);
                    loader.currentGraphicsNode = node;
                }
            }
            else if ((gradientMethod = FindGradientFactory(tagName)) != null)
            {
                var gradient = gradientMethod(loader, content, attrsOffset, attrsLength);
                if (loader.gradientStack.Count == 0 && gradient != null)
                {
                    if (loader.def != null && loader.doc!.doc.defs != null)
                        loader.def.defs.gradients.Add(gradient);
                    else
                        loader.gradients.Add(gradient);
                }
                if (!empty && gradient != null) loader.gradientStack.Add(gradient);
            }
            else if (SvgHelper.StrAs(tagName, "stop"))
            {
                if (loader.gradientStack.Count == 0) return;
                loader.svgParse!.gradStop = new Fill.ColorStop { offset = 0, r = 0, g = 0, b = 0, a = 255 };
                loader.svgParse.flags = SvgStopStyleFlags.StopDefault;
                XmlParser.ParseAttributes(content, attrsOffset, attrsLength, AttrParseStops, loader);
                loader.gradientStack[loader.gradientStack.Count - 1].stops.Add(loader.svgParse.gradStop);
            }
            else
            {
                // Unsupported
            }
        }

        private static void SvgLoaderParserText(SvgLoaderData loader, string content, int offset, int length)
        {
            var text = loader.svgParse!.node!.text;
            text.text = (text.text ?? "") + content.Substring(offset, Math.Min(length, content.Length - offset));
        }

        private static void SvgLoaderParserXmlCssStyle(SvgLoaderData loader, string content, int offset, int length)
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
                    node = method(loader, loader.cssStyle, content, attrsOffset, attrsLength, XmlParser.ParseW3CAttribute);
                    if (node != null) node.id = CopyId(name);
                }
                else if (tag != null && (method = FindGraphicsFactory(tag)) != null)
                {
                    node = method(loader, loader.cssStyle, content, attrsOffset, attrsLength, XmlParser.ParseW3CAttribute);
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

                            var cssNode = SvgCssStyle.FindStyleNode(loader.cssStyle, id);
                            if (cssNode != null)
                            {
                                var oldNode = loader.svgParse!.node;
                                loader.svgParse.node = cssNode;
                                XmlParser.ParseW3CAttribute(content, attrsOffset, attrsLength, AttrParseCssStyleNode, loader);
                                loader.svgParse.node = oldNode;
                            }
                            else
                            {
                                node = CreateCssStyleNode(loader, loader.cssStyle, content, attrsOffset, attrsLength, XmlParser.ParseW3CAttribute);
                                if (node != null) node.id = CopyId(id);
                            }
                        }
                    }
                }
                else if (tag != null && SvgHelper.StrAs(tag, "@font-face"))
                {
                    CreateFontFace(loader, content, attrsOffset, attrsLength, XmlParser.ParseW3CAttribute);
                }

                remaining -= (nextPos - pos);
                pos = nextPos;
            }
            loader.openedTag = OpenedTagType.Other;
        }

        private static bool SvgLoaderParser(SvgLoaderData loader, XMLType type, string content, int offset, int length)
        {
            switch (type)
            {
                case XMLType.Open:
                    SvgLoaderParserXmlOpen(loader, content, offset, length, false);
                    break;
                case XMLType.OpenEmpty:
                    SvgLoaderParserXmlOpen(loader, content, offset, length, true);
                    break;
                case XMLType.Close:
                    SvgLoaderParserXmlClose(loader, content, offset, length);
                    break;
                case XMLType.Data:
                case XMLType.CData:
                    if (loader.openedTag == OpenedTagType.Style) SvgLoaderParserXmlCssStyle(loader, content, offset, length);
                    else if (loader.openedTag == OpenedTagType.Text) SvgLoaderParserText(loader, content, offset, length);
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

        private static void UpdateGradient(SvgLoaderData loader, SvgNode? node, List<SvgStyleGradient> gradients)
        {
            if (node == null) return;

            if (node.child.Count > 0)
            {
                foreach (var child in node.child)
                    UpdateGradient(loader, child, gradients);
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
                                    InheritGradient(loader, newGrad, g);
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
                                    InheritGradient(loader, newGrad, g);
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
            var nodeIdPair = cloneNodes.PopFront();
            while (nodeIdPair != null)
            {
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
                    }
                }
                nodeIdPair = cloneNodes.PopFront();
            }
        }

        // ========== Valid check parser ==========

        private static bool SvgLoaderParserForValidCheckXmlOpen(SvgLoaderData loader, string content, int offset, int length)
        {
            loader.level++;
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
                if (loader.doc == null)
                {
                    if (!SvgHelper.StrAs(tagName, "svg")) return true;
                    var node = method(loader, null, content, attrsOffset, attrsLength, XmlParser.ParseAttributes);
                    loader.doc = node;
                    if (node != null) loader.stack.Add(node);
                    return false; // Found svg tag
                }
            }
            return true;
        }

        private static bool SvgLoaderParserForValidCheck(SvgLoaderData loader, XMLType type, string content, int offset, int length)
        {
            switch (type)
            {
                case XMLType.Open:
                case XMLType.OpenEmpty:
                    return SvgLoaderParserForValidCheckXmlOpen(loader, content, offset, length);
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
        public static SvgNode? Parse(string svgContent, out SvgLoaderData loaderDataOut)
        {
            var loaderData = new SvgLoaderData();
            loaderData.svgParse = new SvgParser();
            loaderData.svgParse.flags = SvgStopStyleFlags.StopDefault;

            // Header pass: find <svg> tag
            XmlParser.Parse(svgContent, svgContent.Length, true, SvgLoaderParserForValidCheck, loaderData);

            if (loaderData.doc == null || loaderData.doc.type != SvgNodeType.Doc)
            {
                loaderDataOut = loaderData;
                return null;
            }

            // Full parse
            loaderData.svgParse = new SvgParser();
            loaderData.svgParse.flags = SvgStopStyleFlags.StopDefault;
            loaderData.doc = null;
            loaderData.stack.Clear();
            loaderData.gradients.Clear();
            loaderData.gradientStack.Clear();

            XmlParser.Parse(svgContent, svgContent.Length, true, SvgLoaderParser, loaderData);

            if (loaderData.doc == null)
            {
                loaderDataOut = loaderData;
                return null;
            }

            var defs = loaderData.doc.doc.defs;

            // Apply CSS styling
            if (loaderData.nodesToStyle.Count > 0)
                CssApplyStyleToPostponeds(loaderData.nodesToStyle, loaderData.cssStyle);
            if (loaderData.cssStyle != null)
                SvgCssStyle.UpdateStyle(loaderData.doc, loaderData.cssStyle);

            // Clone postponed nodes
            if (!loaderData.cloneNodes.Empty())
                ClonePostponedNodes(loaderData.cloneNodes, loaderData.doc);

            // Update passes
            UpdateComposite(loaderData.doc, loaderData.doc);
            if (defs != null) UpdateComposite(loaderData.doc, defs);

            UpdateFilter(loaderData.doc, loaderData.doc);
            if (defs != null) UpdateFilter(loaderData.doc, defs);

            UpdateStyle(loaderData.doc, null);
            if (defs != null) UpdateStyle(defs, null);

            if (loaderData.gradients.Count > 0)
                UpdateGradient(loaderData, loaderData.doc, loaderData.gradients);
            if (defs != null)
                UpdateGradient(loaderData, loaderData.doc, defs.defs.gradients);

            loaderDataOut = loaderData;
            return loaderData.doc;
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
