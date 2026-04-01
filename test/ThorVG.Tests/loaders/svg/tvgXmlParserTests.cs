using Xunit;
using System.Collections.Generic;

namespace ThorVG.Tests
{
    public class tvgXmlParserTests
    {
        private SvgParserContext MakeLoaderData()
        {
            var data = new SvgParserContext();
            data.svgParse = new SvgParser();
            data.svgParse.flags = SvgStopStyleFlags.StopDefault;
            return data;
        }

        [Fact]
        public void ParseAttributes_SingleAttribute()
        {
            var data = MakeLoaderData();
            data.svgParse!.node = new SvgNode { style = new SvgStyleProperty() };
            var attrs = new List<(string key, string value)>();

            XmlParser.ParseAttributes("x=\"10\"", 0, 6,
                (SvgParserContext d, string key, string val) =>
                {
                    attrs.Add((key, val));
                    return true;
                }, data);

            Assert.Single(attrs);
            Assert.Equal("x", attrs[0].key);
            Assert.Equal("10", attrs[0].value);
        }

        [Fact]
        public void ParseAttributes_MultipleAttributes()
        {
            var data = MakeLoaderData();
            data.svgParse!.node = new SvgNode { style = new SvgStyleProperty() };
            var attrs = new List<(string key, string value)>();

            var buf = "width=\"100\" height=\"200\"";
            XmlParser.ParseAttributes(buf, 0, buf.Length,
                (SvgParserContext d, string key, string val) =>
                {
                    attrs.Add((key, val));
                    return true;
                }, data);

            Assert.Equal(2, attrs.Count);
            Assert.Equal("width", attrs[0].key);
            Assert.Equal("100", attrs[0].value);
            Assert.Equal("height", attrs[1].key);
            Assert.Equal("200", attrs[1].value);
        }

        [Fact]
        public void ParseAttributes_SingleQuotedAttribute()
        {
            var data = MakeLoaderData();
            data.svgParse!.node = new SvgNode { style = new SvgStyleProperty() };
            var attrs = new List<(string key, string value)>();

            var buf = "d='M 0 0'";
            XmlParser.ParseAttributes(buf, 0, buf.Length,
                (SvgParserContext d, string key, string val) =>
                {
                    attrs.Add((key, val));
                    return true;
                }, data);

            Assert.Single(attrs);
            Assert.Equal("d", attrs[0].key);
            Assert.Equal("M 0 0", attrs[0].value);
        }

        [Fact]
        public void ParseW3CAttribute_StyleAttribute()
        {
            var data = MakeLoaderData();
            data.svgParse!.node = new SvgNode { style = new SvgStyleProperty() };
            var attrs = new List<(string key, string value)>();

            var buf = "fill:red;stroke:blue";
            XmlParser.ParseW3CAttribute(buf, 0, buf.Length,
                (SvgParserContext d, string key, string val) =>
                {
                    attrs.Add((key, val));
                    return true;
                }, data);

            Assert.Equal(2, attrs.Count);
            Assert.Equal("fill", attrs[0].key);
            Assert.Equal("red", attrs[0].value);
            Assert.Equal("stroke", attrs[1].key);
            Assert.Equal("blue", attrs[1].value);
        }

        [Fact]
        public void ParseCSSAttribute_SimpleClass()
        {
            var buf = ".myclass { fill: red; }";
            var result = XmlParser.ParseCSSAttribute(buf, 0, buf.Length,
                out string? tag, out string? name, out int attrsOffset, out int attrsLength);

            Assert.True(result > 0);
            Assert.Equal("all", tag);
            Assert.Equal("myclass", name);
            Assert.True(attrsLength > 0);
        }

        [Fact]
        public void ParseCSSAttribute_TagWithClass()
        {
            var buf = "rect.highlight { stroke-width: 2; }";
            var result = XmlParser.ParseCSSAttribute(buf, 0, buf.Length,
                out string? tag, out string? name, out int attrsOffset, out int attrsLength);

            Assert.True(result > 0);
            Assert.Equal("rect", tag);
            Assert.Equal("highlight", name);
        }

        [Fact]
        public void FindAttributesTag_FindsFirstWhiteSpace()
        {
            var buf = "svg width=\"100\"";
            var result = XmlParser.FindAttributesTag(buf, 0, buf.Length);
            Assert.Equal(3, result);
        }

        [Fact]
        public void FindAttributesTag_NoAttributes()
        {
            var buf = "svg";
            var result = XmlParser.FindAttributesTag(buf, 0, buf.Length);
            Assert.Equal(-1, result);
        }

        [Fact]
        public void Parse_SimpleXml()
        {
            var data = MakeLoaderData();
            var calls = new List<(XMLType type, string content)>();

            var xml = "<svg></svg>";
            XmlParser.Parse(xml, xml.Length, true,
                (SvgParserContext d, XMLType type, string content, int offset, int length) =>
                {
                    calls.Add((type, content.Substring(offset, length)));
                    return true;
                }, data);

            Assert.True(calls.Count >= 2);
            Assert.Equal(XMLType.Open, calls[0].type);
            Assert.Contains("svg", calls[0].content);
            Assert.Equal(XMLType.Close, calls[1].type);
        }

        [Fact]
        public void Parse_SelfClosingElement()
        {
            var data = MakeLoaderData();
            var calls = new List<(XMLType type, string content)>();

            var xml = "<circle r=\"5\"/>";
            XmlParser.Parse(xml, xml.Length, true,
                (SvgParserContext d, XMLType type, string content, int offset, int length) =>
                {
                    calls.Add((type, content.Substring(offset, length)));
                    return true;
                }, data);

            Assert.Single(calls);
            Assert.Equal(XMLType.OpenEmpty, calls[0].type);
        }

        [Fact]
        public void NodeTypeToString_ReturnsCorrectNames()
        {
            Assert.Equal("Svg", XmlParser.NodeTypeToString(SvgNodeType.Doc));
            Assert.Equal("G", XmlParser.NodeTypeToString(SvgNodeType.G));
            Assert.Equal("Circle", XmlParser.NodeTypeToString(SvgNodeType.Circle));
            Assert.Equal("Rect", XmlParser.NodeTypeToString(SvgNodeType.Rect));
            Assert.Equal("Path", XmlParser.NodeTypeToString(SvgNodeType.Path));
        }
    }
}
