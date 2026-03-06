using Xunit;

namespace ThorVG.Tests
{
    public class tvgSvgSceneBuilderTests
    {
        private SvgNode MakeNode(SvgNodeType type)
        {
            return new SvgNode
            {
                type = type,
                style = new SvgStyleProperty
                {
                    display = true,
                    opacity = 255,
                    fill = { opacity = 255 },
                    stroke = { opacity = 255, paint = { none = true } }
                }
            };
        }

        [Fact]
        public void BuildShape_PathNode()
        {
            var node = MakeNode(SvgNodeType.Path);
            node.path.path = "M 0 0 L 100 100";
            var path = new RenderPath();
            var result = SvgSceneBuilder.BuildShape(node, path);
            Assert.True(result);
            Assert.True(path.cmds.count >= 2);
        }

        [Fact]
        public void BuildShape_RectNode()
        {
            var node = MakeNode(SvgNodeType.Rect);
            node.rect.x = 10;
            node.rect.y = 20;
            node.rect.w = 100;
            node.rect.h = 50;
            var path = new RenderPath();
            var result = SvgSceneBuilder.BuildShape(node, path);
            Assert.True(result);
            Assert.True(path.cmds.count >= 4); // MoveTo + 3 LineTo + Close
        }

        [Fact]
        public void BuildShape_RectWithRoundedCorners()
        {
            var node = MakeNode(SvgNodeType.Rect);
            node.rect.x = 0;
            node.rect.y = 0;
            node.rect.w = 100;
            node.rect.h = 50;
            node.rect.rx = 10;
            node.rect.ry = 10;
            var path = new RenderPath();
            var result = SvgSceneBuilder.BuildShape(node, path);
            Assert.True(result);
            // Rounded rect has: MoveTo + LineTo + CubicTo (x4) + Close
            Assert.True(path.cmds.count >= 5);
        }

        [Fact]
        public void BuildShape_CircleNode()
        {
            var node = MakeNode(SvgNodeType.Circle);
            node.circle.cx = 50;
            node.circle.cy = 50;
            node.circle.r = 25;
            var path = new RenderPath();
            var result = SvgSceneBuilder.BuildShape(node, path);
            Assert.True(result);
            // Circle: MoveTo + 4 CubicTo + Close
            Assert.Equal(6u, path.cmds.count);
        }

        [Fact]
        public void BuildShape_EllipseNode()
        {
            var node = MakeNode(SvgNodeType.Ellipse);
            node.ellipse.cx = 100;
            node.ellipse.cy = 50;
            node.ellipse.rx = 80;
            node.ellipse.ry = 30;
            var path = new RenderPath();
            var result = SvgSceneBuilder.BuildShape(node, path);
            Assert.True(result);
            Assert.Equal(6u, path.cmds.count); // same as circle
        }

        [Fact]
        public void BuildShape_LineNode()
        {
            var node = MakeNode(SvgNodeType.Line);
            node.line.x1 = 0;
            node.line.y1 = 0;
            node.line.x2 = 100;
            node.line.y2 = 100;
            var path = new RenderPath();
            var result = SvgSceneBuilder.BuildShape(node, path);
            Assert.True(result);
            Assert.Equal(2u, path.cmds.count); // MoveTo + LineTo
        }

        [Fact]
        public void BuildShape_PolygonNode()
        {
            var node = MakeNode(SvgNodeType.Polygon);
            node.polygon.pts.AddRange(new float[] { 100, 10, 40, 198, 190, 78, 10, 78 });
            var path = new RenderPath();
            var result = SvgSceneBuilder.BuildShape(node, path);
            Assert.True(result);
            // MoveTo + 3 LineTo + Close
            Assert.Equal(5u, path.cmds.count);
        }

        [Fact]
        public void BuildShape_PolygonNodeTooFewPoints()
        {
            var node = MakeNode(SvgNodeType.Polygon);
            node.polygon.pts.AddRange(new float[] { 100, 10 }); // only 1 point
            var path = new RenderPath();
            var result = SvgSceneBuilder.BuildShape(node, path);
            Assert.False(result);
        }

        [Fact]
        public void BuildShape_PolylineNode()
        {
            var node = MakeNode(SvgNodeType.Polyline);
            node.polyline.pts.AddRange(new float[] { 0, 0, 10, 10, 20, 0, 30, 10 });
            var path = new RenderPath();
            var result = SvgSceneBuilder.BuildShape(node, path);
            Assert.True(result);
            // MoveTo + 3 LineTo (no Close for polyline)
            Assert.Equal(4u, path.cmds.count);
        }

        [Fact]
        public void BuildShape_UnknownType()
        {
            var node = MakeNode(SvgNodeType.G); // group, not a shape
            var path = new RenderPath();
            var result = SvgSceneBuilder.BuildShape(node, path);
            Assert.False(result);
        }

        [Fact]
        public void CollectPaths_EmptyTree()
        {
            var paths = SvgSceneBuilder.CollectPaths(null);
            Assert.Empty(paths);
        }

        [Fact]
        public void CollectPaths_SingleShape()
        {
            var root = MakeNode(SvgNodeType.Doc);
            var rect = MakeNode(SvgNodeType.Rect);
            rect.rect.x = 0; rect.rect.y = 0; rect.rect.w = 100; rect.rect.h = 50;
            root.child.Add(rect);

            var paths = SvgSceneBuilder.CollectPaths(root);
            Assert.Single(paths);
        }

        [Fact]
        public void CollectPaths_MultipleShapes()
        {
            var root = MakeNode(SvgNodeType.Doc);
            var rect = MakeNode(SvgNodeType.Rect);
            rect.rect.x = 0; rect.rect.y = 0; rect.rect.w = 100; rect.rect.h = 50;
            root.child.Add(rect);

            var circle = MakeNode(SvgNodeType.Circle);
            circle.circle.cx = 50; circle.circle.cy = 50; circle.circle.r = 25;
            root.child.Add(circle);

            var paths = SvgSceneBuilder.CollectPaths(root);
            Assert.Equal(2, paths.Count);
        }

        [Fact]
        public void CollectPaths_SkipsHiddenNodes()
        {
            var root = MakeNode(SvgNodeType.Doc);
            var rect = MakeNode(SvgNodeType.Rect);
            rect.rect.x = 0; rect.rect.y = 0; rect.rect.w = 100; rect.rect.h = 50;
            rect.style!.display = false; // hidden
            root.child.Add(rect);

            var paths = SvgSceneBuilder.CollectPaths(root);
            Assert.Empty(paths);
        }

        [Fact]
        public void CollectPaths_NestedInGroup()
        {
            var root = MakeNode(SvgNodeType.Doc);
            var group = MakeNode(SvgNodeType.G);
            var rect = MakeNode(SvgNodeType.Rect);
            rect.rect.x = 0; rect.rect.y = 0; rect.rect.w = 100; rect.rect.h = 50;
            group.child.Add(rect);
            root.child.Add(group);

            var paths = SvgSceneBuilder.CollectPaths(root);
            Assert.Single(paths);
        }

        [Fact]
        public void CollectPaths_FromParsedSvg()
        {
            var svg = "<svg><rect x=\"0\" y=\"0\" width=\"100\" height=\"50\"/><circle cx=\"50\" cy=\"50\" r=\"25\"/></svg>";
            var root = SvgLoader.Parse(svg);
            Assert.NotNull(root);

            var paths = SvgSceneBuilder.CollectPaths(root);
            Assert.Equal(2, paths.Count);
        }
    }
}
