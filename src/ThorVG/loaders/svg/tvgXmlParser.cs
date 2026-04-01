// Ported from ThorVG/src/loaders/svg/tvgXmlParser.h and tvgXmlParser.cpp
// XML tokenizer with delegates.

using System;

namespace ThorVG
{
    public enum XMLType
    {
        Open = 0,
        OpenEmpty,
        Close,
        Data,
        CData,
        Error,
        Processing,
        Doctype,
        Comment,
        Ignored,
        DoctypeChild
    }

    public delegate bool XmlCb(SvgParserContext data, XMLType type, string content, int offset, int length);
    public delegate bool XmlAttributeCb(SvgParserContext data, string key, string value);

    public static class XmlParser
    {
        private static readonly string[] XmlEntities = { "&#10;", "&quot;", "&nbsp;", "&apos;", "&amp;", "&lt;", "&gt;", "&#035;", "&#039;" };
        private static readonly int[] XmlEntityLengths = { 5, 6, 6, 6, 5, 4, 4, 6, 6 };

        public static string? NodeTypeToString(SvgNodeType type)
        {
            return type switch
            {
                SvgNodeType.Doc => "Svg",
                SvgNodeType.G => "G",
                SvgNodeType.Defs => "Defs",
                SvgNodeType.Animation => "Animation",
                SvgNodeType.Arc => "Arc",
                SvgNodeType.Circle => "Circle",
                SvgNodeType.Ellipse => "Ellipse",
                SvgNodeType.Image => "Image",
                SvgNodeType.Line => "Line",
                SvgNodeType.Path => "Path",
                SvgNodeType.Polygon => "Polygon",
                SvgNodeType.Polyline => "Polyline",
                SvgNodeType.Rect => "Rect",
                SvgNodeType.Text => "Text",
                SvgNodeType.TextArea => "TextArea",
                SvgNodeType.Tspan => "Tspan",
                SvgNodeType.Use => "Use",
                SvgNodeType.Video => "Video",
                SvgNodeType.ClipPath => "ClipPath",
                SvgNodeType.Mask => "Mask",
                SvgNodeType.Symbol => "Symbol",
                SvgNodeType.Filter => "Filter",
                SvgNodeType.GaussianBlur => "GaussianBlur",
                _ => "Unknown"
            };
        }

        public static bool IsIgnoreUnsupportedLogElements(string tagName)
        {
            // In the C++ code this returns true unless THORVG_LOG_ENABLED is defined.
            // We simply return true (ignore).
            return true;
        }

        private static int FindWhiteSpace(string str, int itr, int itrEnd)
        {
            for (; itr < itrEnd; itr++)
            {
                if (char.IsWhiteSpace(str[itr])) break;
            }
            return itr;
        }

        private static int SkipXmlEntities(string str, int itr, int itrEnd)
        {
            var p = itr;
            while (itr < itrEnd && str[itr] == '&')
            {
                bool found = false;
                for (int i = 0; i < XmlEntities.Length; i++)
                {
                    if (itr + XmlEntityLengths[i] <= itrEnd &&
                        str.AsSpan(itr, XmlEntityLengths[i]).SequenceEqual(XmlEntities[i].AsSpan()))
                    {
                        itr += XmlEntityLengths[i];
                        found = true;
                        break;
                    }
                }
                if (!found) break;
                if (itr == p) break;
                p = itr;
            }
            return itr;
        }

        private static int UnskipXmlEntities(string str, int itr, int itrStart)
        {
            var p = itr;
            while (itr > itrStart && str[itr - 1] == ';')
            {
                bool found = false;
                for (int i = 0; i < XmlEntities.Length; i++)
                {
                    if (itr - XmlEntityLengths[i] > itrStart &&
                        str.AsSpan(itr - XmlEntityLengths[i], XmlEntityLengths[i]).SequenceEqual(XmlEntities[i].AsSpan()))
                    {
                        itr -= XmlEntityLengths[i];
                        found = true;
                        break;
                    }
                }
                if (!found) break;
                if (itr == p) break;
                p = itr;
            }
            return itr;
        }

        private static int SkipWhiteSpacesAndXmlEntities(string str, int itr, int itrEnd)
        {
            itr = SvgUtil.SkipWhiteSpace(str, itr, itrEnd);
            var p = itr;
            while (true)
            {
                var n = SkipXmlEntities(str, itr, itrEnd);
                if (n != p) { itr = n; p = itr; } else break;
                n = SvgUtil.SkipWhiteSpace(str, itr, itrEnd);
                if (n != p) { itr = n; p = itr; } else break;
            }
            return itr;
        }

        private static int UnskipWhiteSpacesAndXmlEntities(string str, int itr, int itrStart)
        {
            itr = SvgUtil.UnskipWhiteSpace(str, itr, itrStart);
            var p = itr;
            while (true)
            {
                var n = UnskipXmlEntities(str, itr, itrStart);
                if (n != p) { itr = n; p = itr; } else break;
                n = SvgUtil.UnskipWhiteSpace(str, itr, itrStart);
                if (n != p) { itr = n; p = itr; } else break;
            }
            return itr;
        }

        private static int FindStartTag(string str, int itr, int itrEnd)
        {
            var idx = str.IndexOf('<', itr, itrEnd - itr);
            return idx;
        }

        private static int FindEndTag(string str, int itr, int itrEnd)
        {
            bool insideDoubleQuote = false;
            bool insideSingleQuote = false;
            for (; itr < itrEnd; itr++)
            {
                if (str[itr] == '"' && !insideSingleQuote) insideDoubleQuote = !insideDoubleQuote;
                if (str[itr] == '\'' && !insideDoubleQuote) insideSingleQuote = !insideSingleQuote;
                if (!insideDoubleQuote && !insideSingleQuote)
                {
                    if (str[itr] == '>' || str[itr] == '<')
                        return itr;
                }
            }
            return -1;
        }

        private static int FindEndCommentTag(string str, int itr, int itrEnd)
        {
            for (; itr < itrEnd; itr++)
            {
                if (str[itr] == '-' && itr + 1 < itrEnd && str[itr + 1] == '-' && itr + 2 < itrEnd && str[itr + 2] == '>')
                    return itr + 2;
            }
            return -1;
        }

        private static int FindEndCdataTag(string str, int itr, int itrEnd)
        {
            for (; itr < itrEnd; itr++)
            {
                if (str[itr] == ']' && itr + 1 < itrEnd && str[itr + 1] == ']' && itr + 2 < itrEnd && str[itr + 2] == '>')
                    return itr + 2;
            }
            return -1;
        }

        private static int FindDoctypeChildEndTag(string str, int itr, int itrEnd)
        {
            for (; itr < itrEnd; itr++)
            {
                if (str[itr] == '>') return itr;
            }
            return -1;
        }

        private static XMLType GetXMLType(string str, int itr, int itrEnd, out int toff)
        {
            toff = 0;
            if (str[itr + 1] == '/')
            {
                toff = 1;
                return XMLType.Close;
            }
            else if (str[itr + 1] == '?')
            {
                toff = 1;
                return XMLType.Processing;
            }
            else if (str[itr + 1] == '!')
            {
                if (itr + 9 < itrEnd && str.AsSpan(itr + 2, 7).SequenceEqual("DOCTYPE".AsSpan()) &&
                    (itr + 9 >= itrEnd || str[itr + 9] == '>' || char.IsWhiteSpace(str[itr + 9])))
                {
                    toff = 8; // "!DOCTYPE".Length
                    return XMLType.Doctype;
                }
                else if (itr + 11 < itrEnd && str.AsSpan(itr + 2, 7).SequenceEqual("[CDATA[".AsSpan()))
                {
                    toff = 8; // "![CDATA[".Length
                    return XMLType.CData;
                }
                else if (itr + 6 < itrEnd && str[itr + 2] == '-' && str[itr + 3] == '-')
                {
                    toff = 3; // "!--".Length
                    return XMLType.Comment;
                }
                else if (itr + 2 < itrEnd)
                {
                    toff = 1; // "!".Length
                    return XMLType.DoctypeChild;
                }
                return XMLType.Open;
            }
            return XMLType.Open;
        }

        public static bool ParseAttributes(string buf, int bufOffset, int bufLength, XmlAttributeCb func, SvgParserContext data)
        {
            int itr = bufOffset;
            int itrEnd = bufOffset + bufLength;

            while (itr < itrEnd)
            {
                int p = SkipWhiteSpacesAndXmlEntities(buf, itr, itrEnd);
                if (p == itrEnd) return true;

                int key = p;
                int keyEnd = key;
                for (; keyEnd < itrEnd; keyEnd++)
                {
                    if (buf[keyEnd] == '=' || char.IsWhiteSpace(buf[keyEnd])) break;
                }
                if (keyEnd == itrEnd) return false;
                if (keyEnd == key) { itr = keyEnd + 1; continue; }

                int valueStart;
                if (buf[keyEnd] == '=') valueStart = keyEnd + 1;
                else
                {
                    var eqIdx = buf.IndexOf('=', keyEnd, itrEnd - keyEnd);
                    if (eqIdx < 0) return false;
                    valueStart = eqIdx + 1;
                }
                keyEnd = UnskipXmlEntities(buf, keyEnd, key);

                valueStart = SkipWhiteSpacesAndXmlEntities(buf, valueStart, itrEnd);
                if (valueStart == itrEnd) return false;

                int valueEnd;
                if (buf[valueStart] == '"' || buf[valueStart] == '\'')
                {
                    var quote = buf[valueStart];
                    valueEnd = buf.IndexOf(quote, valueStart + 1, itrEnd - valueStart - 1);
                    if (valueEnd < 0) return false;
                    valueStart++;
                }
                else
                {
                    valueEnd = FindWhiteSpace(buf, valueStart, itrEnd);
                }

                itr = valueEnd + 1;

                valueStart = SkipWhiteSpacesAndXmlEntities(buf, valueStart, itrEnd);
                valueEnd = UnskipWhiteSpacesAndXmlEntities(buf, valueEnd, valueStart);

                var keyStr = buf.Substring(key, keyEnd - key);

                // Build value, skipping XML entities
                var valChars = new char[valueEnd - valueStart];
                int vi = 0;
                int vpos = valueStart;
                while (vpos < valueEnd)
                {
                    vpos = SkipXmlEntities(buf, vpos, valueEnd);
                    if (vpos < valueEnd)
                        valChars[vi++] = buf[vpos++];
                }
                var valStr = new string(valChars, 0, vi);

                func(data, keyStr, valStr);
            }

            return true;
        }

        public static bool Parse(string buf, int bufLength, bool strip, XmlCb func, SvgParserContext data)
        {
            int itr = 0;
            int itrEnd = bufLength;

            while (itr < itrEnd)
            {
                if (buf[itr] == '<')
                {
                    if (itr + 1 >= itrEnd) return false;

                    var type = GetXMLType(buf, itr, itrEnd, out int toff);

                    int p;
                    if (type == XMLType.CData) p = FindEndCdataTag(buf, itr + 1 + toff, itrEnd);
                    else if (type == XMLType.DoctypeChild) p = FindDoctypeChildEndTag(buf, itr + 1 + toff, itrEnd);
                    else if (type == XMLType.Comment) p = FindEndCommentTag(buf, itr + 1 + toff, itrEnd);
                    else p = FindEndTag(buf, itr + 1 + toff, itrEnd);

                    if (p >= 0)
                    {
                        if (buf[p] == '<' && type != XMLType.Doctype) return false;

                        int start = itr + 1 + toff;
                        int end = p;

                        switch (type)
                        {
                            case XMLType.Open:
                                if (buf[p - 1] == '/') { type = XMLType.OpenEmpty; end--; }
                                break;
                            case XMLType.CData:
                                if (p >= 2 && buf[p - 2] == ']' && buf[p - 1] == ']') end -= 2;
                                break;
                            case XMLType.Processing:
                                if (buf[p - 1] == '?') end--;
                                break;
                            case XMLType.Comment:
                                if (p >= 2 && buf[p - 2] == '-' && buf[p - 1] == '-') end -= 2;
                                break;
                        }

                        if (strip && type != XMLType.CData)
                        {
                            start = SkipWhiteSpacesAndXmlEntities(buf, start, end);
                            end = UnskipWhiteSpacesAndXmlEntities(buf, end, start);
                        }

                        if (!func(data, type, buf, start, end - start)) return false;

                        itr = p + 1;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    int p;
                    int end;

                    if (strip && data.openedTag != OpenedTagType.Text)
                    {
                        p = SkipWhiteSpacesAndXmlEntities(buf, itr, itrEnd);
                        if (p > itr)
                        {
                            if (!func(data, XMLType.Ignored, buf, itr, p - itr)) return false;
                            itr = p;
                        }
                    }

                    var startIdx = FindStartTag(buf, itr, itrEnd);
                    p = startIdx < 0 ? itrEnd : startIdx;

                    end = p;
                    if (strip && data.openedTag != OpenedTagType.Text)
                        end = UnskipWhiteSpacesAndXmlEntities(buf, end, itr);

                    if (itr != end && !func(data, XMLType.Data, buf, itr, end - itr)) return false;

                    if (strip && end < p && !func(data, XMLType.Ignored, buf, end, p - end)) return false;

                    itr = p;
                }
            }
            return true;
        }

        public static bool ParseW3CAttribute(string buf, int bufOffset, int bufLength, XmlAttributeCb func, SvgParserContext data)
        {
            if (buf == null) return false;
            int pos = bufOffset;
            int end = bufOffset + bufLength;
            if (pos == end) return true;

            do
            {
                int sep = buf.IndexOf(':', pos, end - pos);
                int next = buf.IndexOf(';', pos, end - pos);

                // Handle 'src' tag from css font-face containing extra semicolons
                if (sep >= 0)
                {
                    var srcIdx = buf.IndexOf("src", pos, sep - pos, StringComparison.Ordinal);
                    if (srcIdx >= 0 && srcIdx < sep)
                    {
                        if (next >= 0 && next + 1 < end)
                            next = buf.IndexOf(';', next + 1, end - next - 1);
                        else break;
                    }
                }

                if (sep >= end) { next = -1; sep = -1; }
                if (next >= end) next = -1;

                string key = string.Empty;
                string val = string.Empty;

                if (sep >= 0 && next < 0)
                {
                    key = buf.Substring(pos, sep - pos);
                    val = buf.Substring(sep + 1, end - sep - 1);
                }
                else if (sep >= 0 && sep < next)
                {
                    key = buf.Substring(pos, sep - pos);
                    val = buf.Substring(sep + 1, next - sep - 1);
                }
                else if (next >= 0)
                {
                    key = buf.Substring(pos, next - pos);
                }

                if (key.Length > 0)
                {
                    key = key.Trim();
                    val = val.Trim();
                    func(data, key, val);
                }

                if (next < 0) break;
                pos = next + 1;
            } while (true);

            return true;
        }

        /// <summary>
        /// Parses CSS attribute blocks: "tag {}", ".name {}", "tag.name {}"
        /// Returns the position after the closing '}' or -1 if not found.
        /// </summary>
        public static int ParseCSSAttribute(string buf, int bufOffset, int bufLength,
            out string? tag, out string? name, out int attrsOffset, out int attrsLength)
        {
            tag = null;
            name = null;
            attrsOffset = 0;
            attrsLength = 0;

            int itr = SvgUtil.SkipWhiteSpace(buf, bufOffset, bufOffset + bufLength);
            int itrEndIdx = buf.IndexOf('{', bufOffset, bufLength);
            if (itrEndIdx < 0 || itr == itrEndIdx) return -1;

            int nextElement = buf.IndexOf('}', itrEndIdx, bufOffset + bufLength - itrEndIdx);
            if (nextElement < 0) return -1;

            attrsOffset = itrEndIdx + 1;
            attrsLength = nextElement - attrsOffset;

            int itrEnd = SvgUtil.UnskipWhiteSpace(buf, itrEndIdx, itr);
            if (itrEnd > itr && buf[itrEnd - 1] == '.') return -1;

            int dotPos = itr;
            for (; dotPos < itrEnd; dotPos++)
            {
                if (buf[dotPos] == '.') break;
            }

            if (dotPos == itr) tag = "all";
            else tag = buf.Substring(itr, dotPos - itr);

            if (dotPos == itrEnd) name = null;
            else name = buf.Substring(dotPos + 1, itrEnd - dotPos - 1);

            return nextElement + 1;
        }

        public static int FindAttributesTag(string buf, int bufOffset, int bufLength)
        {
            int itr = bufOffset;
            int itrEnd = bufOffset + bufLength;

            for (; itr < itrEnd; itr++)
            {
                if (!char.IsWhiteSpace(buf[itr]))
                {
                    if (buf[itr] == '=') return bufOffset;
                }
                else
                {
                    itr = UnskipXmlEntities(buf, itr, bufOffset);
                    if (itr == itrEnd) return -1;
                    return itr;
                }
            }
            return -1;
        }
    }
}
