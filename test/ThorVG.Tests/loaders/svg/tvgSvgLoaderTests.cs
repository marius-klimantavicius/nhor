using Xunit;

namespace ThorVG.Tests
{
    public class tvgSvgLoaderTests
    {
        // ========== Basic parsing ==========

        [Fact]
        public void Parse_EmptySvg_ReturnsDocNode()
        {
            var svg = "<svg></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            Assert.Equal(SvgNodeType.Doc, root!.type);
        }

        [Fact]
        public void Parse_SvgWithWidthHeight()
        {
            var svg = "<svg width=\"100\" height=\"200\"></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            var (vbox, w, h, viewFlag) = SvgLoader.GetViewInfo(root!);
            Assert.Equal(100.0f, w, 1);
            Assert.Equal(200.0f, h, 1);
        }

        [Fact]
        public void Parse_SvgWithViewBox()
        {
            var svg = "<svg viewBox=\"0 0 300 400\"></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            var (vbox, w, h, viewFlag) = SvgLoader.GetViewInfo(root!);
            Assert.Equal(0.0f, vbox.x, 1);
            Assert.Equal(0.0f, vbox.y, 1);
            Assert.Equal(300.0f, vbox.w, 1);
            Assert.Equal(400.0f, vbox.h, 1);
        }

        [Fact]
        public void Parse_NullContent_ReturnsNull()
        {
            // Passing invalid XML should not crash
            var root = SvgLoader.Parse("");
            Assert.Null(root);
        }

        [Fact]
        public void Parse_NonSvgContent_ReturnsNull()
        {
            var root = SvgLoader.Parse("<html><body></body></html>");
            Assert.Null(root);
        }

        // ========== Shape nodes ==========

        [Fact]
        public void Parse_Rect()
        {
            var svg = "<svg><rect x=\"10\" y=\"20\" width=\"100\" height=\"50\"/></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            Assert.True(root!.child.Count >= 1);

            SvgNode? rect = FindChildByType(root, SvgNodeType.Rect);
            Assert.NotNull(rect);
            Assert.Equal(10.0f, rect!.rect.x, 1);
            Assert.Equal(20.0f, rect.rect.y, 1);
            Assert.Equal(100.0f, rect.rect.w, 1);
            Assert.Equal(50.0f, rect.rect.h, 1);
        }

        [Fact]
        public void Parse_RectWithRoundedCorners()
        {
            var svg = "<svg><rect x=\"0\" y=\"0\" width=\"100\" height=\"50\" rx=\"5\" ry=\"10\"/></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? rect = FindChildByType(root!, SvgNodeType.Rect);
            Assert.NotNull(rect);
            Assert.Equal(5.0f, rect!.rect.rx, 1);
            Assert.Equal(10.0f, rect.rect.ry, 1);
        }

        [Fact]
        public void Parse_Circle()
        {
            var svg = "<svg><circle cx=\"50\" cy=\"50\" r=\"25\"/></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? circle = FindChildByType(root!, SvgNodeType.Circle);
            Assert.NotNull(circle);
            Assert.Equal(50.0f, circle!.circle.cx, 1);
            Assert.Equal(50.0f, circle.circle.cy, 1);
            Assert.Equal(25.0f, circle.circle.r, 1);
        }

        [Fact]
        public void Parse_Ellipse()
        {
            var svg = "<svg><ellipse cx=\"100\" cy=\"50\" rx=\"75\" ry=\"30\"/></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? ellipse = FindChildByType(root!, SvgNodeType.Ellipse);
            Assert.NotNull(ellipse);
            Assert.Equal(100.0f, ellipse!.ellipse.cx, 1);
            Assert.Equal(50.0f, ellipse.ellipse.cy, 1);
            Assert.Equal(75.0f, ellipse.ellipse.rx, 1);
            Assert.Equal(30.0f, ellipse.ellipse.ry, 1);
        }

        [Fact]
        public void Parse_Line()
        {
            var svg = "<svg><line x1=\"0\" y1=\"0\" x2=\"100\" y2=\"100\"/></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? line = FindChildByType(root!, SvgNodeType.Line);
            Assert.NotNull(line);
            Assert.Equal(0.0f, line!.line.x1, 1);
            Assert.Equal(0.0f, line.line.y1, 1);
            Assert.Equal(100.0f, line.line.x2, 1);
            Assert.Equal(100.0f, line.line.y2, 1);
        }

        [Fact]
        public void Parse_Path()
        {
            var svg = "<svg><path d=\"M 10 20 L 30 40\"/></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? path = FindChildByType(root!, SvgNodeType.Path);
            Assert.NotNull(path);
            Assert.NotNull(path!.path.path);
            Assert.Contains("M", path.path.path);
        }

        [Fact]
        public void Parse_Polygon()
        {
            var svg = "<svg><polygon points=\"100,10 40,198 190,78 10,78 160,198\"/></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? polygon = FindChildByType(root!, SvgNodeType.Polygon);
            Assert.NotNull(polygon);
            Assert.Equal(10, polygon!.polygon.pts.Count); // 5 points * 2 coords
        }

        [Fact]
        public void Parse_Polyline()
        {
            var svg = "<svg><polyline points=\"0,0 10,10 20,0\"/></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? polyline = FindChildByType(root!, SvgNodeType.Polyline);
            Assert.NotNull(polyline);
            Assert.Equal(6, polyline!.polyline.pts.Count); // 3 points * 2 coords
        }

        // ========== Group nodes ==========

        [Fact]
        public void Parse_Group()
        {
            var svg = "<svg><g><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\"/></g></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? group = FindChildByType(root!, SvgNodeType.G);
            Assert.NotNull(group);
            Assert.True(group!.child.Count >= 1);
        }

        [Fact]
        public void Parse_NestedGroups()
        {
            var svg = "<svg><g id=\"outer\"><g id=\"inner\"><circle cx=\"5\" cy=\"5\" r=\"2\"/></g></g></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? outer = FindChildByType(root!, SvgNodeType.G);
            Assert.NotNull(outer);
            Assert.Equal("outer", outer!.id);

            SvgNode? inner = FindChildByType(outer, SvgNodeType.G);
            Assert.NotNull(inner);
            Assert.Equal("inner", inner!.id);
        }

        // ========== Style attributes ==========

        [Fact]
        public void Parse_FillColor()
        {
            var svg = "<svg><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"#ff0000\"/></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? rect = FindChildByType(root!, SvgNodeType.Rect);
            Assert.NotNull(rect);
            Assert.Equal(255, rect!.style!.fill.paint.color.r);
            Assert.Equal(0, rect.style.fill.paint.color.g);
            Assert.Equal(0, rect.style.fill.paint.color.b);
        }

        [Fact]
        public void Parse_FillNone()
        {
            var svg = "<svg><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"none\"/></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? rect = FindChildByType(root!, SvgNodeType.Rect);
            Assert.NotNull(rect);
            Assert.True(rect!.style!.fill.paint.none);
        }

        [Fact]
        public void Parse_StrokeColor()
        {
            var svg = "<svg><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" stroke=\"#00ff00\"/></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? rect = FindChildByType(root!, SvgNodeType.Rect);
            Assert.NotNull(rect);
            Assert.Equal(0, rect!.style!.stroke.paint.color.r);
            Assert.Equal(255, rect.style.stroke.paint.color.g);
            Assert.Equal(0, rect.style.stroke.paint.color.b);
        }

        [Fact]
        public void Parse_StrokeWidth()
        {
            var svg = "<svg><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" stroke-width=\"3\"/></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? rect = FindChildByType(root!, SvgNodeType.Rect);
            Assert.NotNull(rect);
            Assert.Equal(3.0f, rect!.style!.stroke.width, 1);
        }

        [Fact]
        public void Parse_Opacity()
        {
            var svg = "<svg><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" opacity=\"0.5\"/></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? rect = FindChildByType(root!, SvgNodeType.Rect);
            Assert.NotNull(rect);
            Assert.InRange(rect!.style!.opacity, 126, 129); // ~128 = 0.5 * 255
        }

        [Fact]
        public void Parse_StyleAttribute()
        {
            var svg = "<svg><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" style=\"fill:#0000ff;stroke-width:2\"/></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? rect = FindChildByType(root!, SvgNodeType.Rect);
            Assert.NotNull(rect);
            Assert.Equal(0, rect!.style!.fill.paint.color.r);
            Assert.Equal(0, rect.style.fill.paint.color.g);
            Assert.Equal(255, rect.style.fill.paint.color.b);
            Assert.Equal(2.0f, rect.style.stroke.width, 1);
        }

        [Fact]
        public void Parse_NamedColor()
        {
            var svg = "<svg><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"red\"/></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? rect = FindChildByType(root!, SvgNodeType.Rect);
            Assert.NotNull(rect);
            Assert.Equal(255, rect!.style!.fill.paint.color.r);
            Assert.Equal(0, rect.style.fill.paint.color.g);
            Assert.Equal(0, rect.style.fill.paint.color.b);
        }

        [Fact]
        public void Parse_Display()
        {
            var svg = "<svg><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" display=\"none\"/></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? rect = FindChildByType(root!, SvgNodeType.Rect);
            Assert.NotNull(rect);
            Assert.False(rect!.style!.display);
        }

        // ========== Transform ==========

        [Fact]
        public void Parse_TranslateTransform()
        {
            var svg = "<svg><g transform=\"translate(10,20)\"><rect x=\"0\" y=\"0\" width=\"5\" height=\"5\"/></g></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? group = FindChildByType(root!, SvgNodeType.G);
            Assert.NotNull(group);
            Assert.True(group!.transform.HasValue);
            Assert.Equal(10.0f, group.transform.Value.e13, 1);
            Assert.Equal(20.0f, group.transform.Value.e23, 1);
        }

        [Fact]
        public void Parse_ScaleTransform()
        {
            var svg = "<svg><g transform=\"scale(2)\"><rect x=\"0\" y=\"0\" width=\"5\" height=\"5\"/></g></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? group = FindChildByType(root!, SvgNodeType.G);
            Assert.NotNull(group);
            Assert.True(group!.transform.HasValue);
            Assert.Equal(2.0f, group.transform.Value.e11, 1);
            Assert.Equal(2.0f, group.transform.Value.e22, 1);
        }

        // ========== Defs and gradients ==========

        [Fact]
        public void Parse_DefsNode()
        {
            var svg = "<svg><defs><linearGradient id=\"g1\"><stop offset=\"0\" stop-color=\"red\"/><stop offset=\"1\" stop-color=\"blue\"/></linearGradient></defs></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            Assert.NotNull(root!.doc.defs);
        }

        [Fact]
        public void Parse_LinearGradient()
        {
            var svg = "<svg><defs><linearGradient id=\"g1\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"0\"><stop offset=\"0\" stop-color=\"red\"/><stop offset=\"1\" stop-color=\"blue\"/></linearGradient></defs><rect x=\"0\" y=\"0\" width=\"100\" height=\"100\" fill=\"url(#g1)\"/></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? rect = FindChildByType(root!, SvgNodeType.Rect);
            Assert.NotNull(rect);
            Assert.NotNull(rect!.style!.fill.paint.gradient);
            Assert.Equal(SvgGradientType.Linear, rect.style.fill.paint.gradient!.type);
        }

        [Fact]
        public void Parse_RadialGradient()
        {
            var svg = "<svg><defs><radialGradient id=\"g2\" cx=\"0.5\" cy=\"0.5\" r=\"0.5\"><stop offset=\"0\" stop-color=\"white\"/><stop offset=\"1\" stop-color=\"black\"/></radialGradient></defs><circle cx=\"50\" cy=\"50\" r=\"40\" fill=\"url(#g2)\"/></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? circle = FindChildByType(root!, SvgNodeType.Circle);
            Assert.NotNull(circle);
            Assert.NotNull(circle!.style!.fill.paint.gradient);
            Assert.Equal(SvgGradientType.Radial, circle.style.fill.paint.gradient!.type);
        }

        // ========== Node IDs ==========

        [Fact]
        public void Parse_NodeId()
        {
            var svg = "<svg><rect id=\"myRect\" x=\"0\" y=\"0\" width=\"10\" height=\"10\"/></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? rect = FindChildByType(root!, SvgNodeType.Rect);
            Assert.NotNull(rect);
            Assert.Equal("myRect", rect!.id);
        }

        // ========== Style inheritance ==========

        [Fact]
        public void Parse_StyleInheritedFromParent()
        {
            var svg = "<svg><g fill=\"#ff0000\"><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\"/></g></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? group = FindChildByType(root!, SvgNodeType.G);
            Assert.NotNull(group);
            SvgNode? rect = FindChildByType(group!, SvgNodeType.Rect);
            Assert.NotNull(rect);
            // After style inheritance pass, fill should be inherited from group
            Assert.Equal(255, rect!.style!.fill.paint.color.r);
        }

        // ========== ClipPath and Mask ==========

        [Fact]
        public void Parse_ClipPath()
        {
            var svg = "<svg><defs><clipPath id=\"clip1\"><rect x=\"0\" y=\"0\" width=\"50\" height=\"50\"/></clipPath></defs><rect x=\"0\" y=\"0\" width=\"100\" height=\"100\" clip-path=\"url(#clip1)\"/></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? rect = FindChildByType(root!, SvgNodeType.Rect);
            Assert.NotNull(rect);
            // After composite update, clipPath node should be resolved
            Assert.NotNull(rect!.style!.clipPath.node);
        }

        // ========== CSS style block ==========

        [Fact]
        public void Parse_CssStyleBlock()
        {
            var svg = "<svg><style>.red { fill: #ff0000; }</style><rect class=\"red\" x=\"0\" y=\"0\" width=\"10\" height=\"10\"/></svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            SvgNode? rect = FindChildByType(root!, SvgNodeType.Rect);
            Assert.NotNull(rect);
            Assert.Equal(255, rect!.style!.fill.paint.color.r);
        }

        // ========== Complex SVG document ==========

        [Fact]
        public void Parse_ComplexDocument()
        {
            var svg = @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""200"" height=""200"" viewBox=""0 0 200 200"">
  <defs>
    <linearGradient id=""bg"" x1=""0"" y1=""0"" x2=""1"" y2=""1"">
      <stop offset=""0%"" stop-color=""white""/>
      <stop offset=""100%"" stop-color=""gray""/>
    </linearGradient>
  </defs>
  <g transform=""translate(10,10)"">
    <rect x=""0"" y=""0"" width=""180"" height=""180"" rx=""5"" ry=""5"" fill=""url(#bg)"" stroke=""black"" stroke-width=""2""/>
    <circle cx=""90"" cy=""90"" r=""40"" fill=""red"" opacity=""0.8""/>
    <path d=""M 50 150 Q 90 100 130 150"" fill=""none"" stroke=""blue"" stroke-width=""3""/>
  </g>
</svg>";
            var root = SvgLoader.Parse(svg);

            Assert.NotNull(root);
            Assert.Equal(SvgNodeType.Doc, root!.type);

            var (vbox, w, h, viewFlag) = SvgLoader.GetViewInfo(root);
            Assert.Equal(200.0f, w, 1);
            Assert.Equal(200.0f, h, 1);
            Assert.Equal(0.0f, vbox.x, 1);
            Assert.Equal(0.0f, vbox.y, 1);
            Assert.Equal(200.0f, vbox.w, 1);
            Assert.Equal(200.0f, vbox.h, 1);
        }

        // ========== Helper ==========

        private static SvgNode? FindChildByType(SvgNode parent, SvgNodeType type)
        {
            foreach (var child in parent.child)
            {
                if (child.type == type) return child;
                var found = FindChildByType(child, type);
                if (found != null) return found;
            }
            return null;
        }
    }
}
