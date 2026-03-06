using Xunit;

namespace ThorVG.Tests
{
    public class tvgSvgCssStyleTests
    {
        private SvgNode MakeNode(SvgNodeType type, string? id = null)
        {
            var node = new SvgNode
            {
                type = type,
                id = id,
                style = new SvgStyleProperty
                {
                    opacity = 255,
                    fill = { opacity = 255 },
                    stroke = { opacity = 255, paint = { none = true } }
                }
            };
            return node;
        }

        [Fact]
        public void CopyStyleAttr_CopiesColor()
        {
            var from = MakeNode(SvgNodeType.Rect);
            from.style!.color = new RGB { r = 255, g = 0, b = 0 };
            from.style.curColorSet = true;

            var to = MakeNode(SvgNodeType.Rect);
            SvgCssStyle.CopyStyleAttr(to, from);

            Assert.Equal(255, to.style!.color.r);
            Assert.Equal(0, to.style.color.g);
            Assert.Equal(0, to.style.color.b);
            Assert.True(to.style.curColorSet);
        }

        [Fact]
        public void CopyStyleAttr_CopiesTransform()
        {
            var from = MakeNode(SvgNodeType.G);
            from.transform = new Matrix(2, 0, 0, 0, 2, 0, 0, 0, 1);

            var to = MakeNode(SvgNodeType.G);
            SvgCssStyle.CopyStyleAttr(to, from);

            Assert.True(to.transform.HasValue);
            Assert.Equal(2.0f, to.transform.Value.e11);
        }

        [Fact]
        public void CopyStyleAttr_DoesNotOverwriteExistingFlag()
        {
            var from = MakeNode(SvgNodeType.Rect);
            from.style!.opacity = 128;
            from.style.flags |= SvgStyleFlags.Opacity;

            var to = MakeNode(SvgNodeType.Rect);
            to.style!.opacity = 200;
            to.style.flags |= SvgStyleFlags.Opacity;

            SvgCssStyle.CopyStyleAttr(to, from, overwrite: false);

            Assert.Equal(200, to.style.opacity); // not overwritten
        }

        [Fact]
        public void CopyStyleAttr_OverwriteWhenRequested()
        {
            var from = MakeNode(SvgNodeType.Rect);
            from.style!.opacity = 128;
            from.style.flags |= SvgStyleFlags.Opacity;

            var to = MakeNode(SvgNodeType.Rect);
            to.style!.opacity = 200;
            to.style.flags |= SvgStyleFlags.Opacity;

            SvgCssStyle.CopyStyleAttr(to, from, overwrite: true);

            Assert.Equal(128, to.style.opacity); // overwritten
        }

        [Fact]
        public void FindStyleNode_FindsByName()
        {
            var styleRoot = MakeNode(SvgNodeType.CssStyle);
            var child1 = MakeNode(SvgNodeType.CssStyle, "red");
            var child2 = MakeNode(SvgNodeType.CssStyle, "blue");
            styleRoot.child.Add(child1);
            styleRoot.child.Add(child2);

            var found = SvgCssStyle.FindStyleNode(styleRoot, "blue");
            Assert.NotNull(found);
            Assert.Equal("blue", found!.id);
        }

        [Fact]
        public void FindStyleNode_ReturnsNullIfNotFound()
        {
            var styleRoot = MakeNode(SvgNodeType.CssStyle);
            var found = SvgCssStyle.FindStyleNode(styleRoot, "nonexistent");
            Assert.Null(found);
        }

        [Fact]
        public void FindStyleNode_ByTypeAndTitle()
        {
            var styleRoot = MakeNode(SvgNodeType.CssStyle);
            var child = MakeNode(SvgNodeType.Rect, "myRect");
            styleRoot.child.Add(child);

            var found = SvgCssStyle.FindStyleNode(styleRoot, "myRect", SvgNodeType.Rect);
            Assert.NotNull(found);
            Assert.Equal("myRect", found!.id);
        }

        [Fact]
        public void FindStyleNode_ByTypeAndTitle_WrongType()
        {
            var styleRoot = MakeNode(SvgNodeType.CssStyle);
            var child = MakeNode(SvgNodeType.Circle, "myCircle");
            styleRoot.child.Add(child);

            var found = SvgCssStyle.FindStyleNode(styleRoot, "myCircle", SvgNodeType.Rect);
            Assert.Null(found);
        }

        [Fact]
        public void UpdateStyle_AppliesCssToChildren()
        {
            var doc = MakeNode(SvgNodeType.Doc);
            var rect = MakeNode(SvgNodeType.Rect);
            doc.child.Add(rect);

            var styleRoot = MakeNode(SvgNodeType.CssStyle);
            var cssNode = MakeNode(SvgNodeType.Rect);
            cssNode.style!.opacity = 128;
            cssNode.style.flags |= SvgStyleFlags.Opacity;
            styleRoot.child.Add(cssNode);

            SvgCssStyle.UpdateStyle(doc, styleRoot);

            Assert.Equal(128, rect.style!.opacity);
        }
    }
}
