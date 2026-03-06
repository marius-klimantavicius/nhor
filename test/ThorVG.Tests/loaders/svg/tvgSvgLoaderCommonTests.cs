using Xunit;

namespace ThorVG.Tests
{
    public class tvgSvgLoaderCommonTests
    {
        // ========== SvgNode creation ==========

        [Fact]
        public void SvgNode_DefaultValues()
        {
            var node = new SvgNode();
            Assert.Equal(SvgNodeType.Doc, node.type); // default enum value
            Assert.Null(node.id);
            Assert.Null(node.parent);
            Assert.NotNull(node.child);
            Assert.Empty(node.child);
            Assert.False(node.transform.HasValue);
        }

        [Fact]
        public void SvgNode_AddChild()
        {
            var parent = new SvgNode();
            var child = new SvgNode();
            parent.child.Add(child);
            Assert.Single(parent.child);
            Assert.Same(child, parent.child[0]);
        }

        // ========== SvgStyleProperty defaults ==========

        [Fact]
        public void SvgStyleProperty_DefaultFlags()
        {
            var style = new SvgStyleProperty();
            Assert.Equal((SvgStyleFlags)0, style.flags);
            Assert.Equal((SvgStyleFlags)0, style.flagsImportance);
        }

        [Fact]
        public void SvgStyleProperty_FillDefaults()
        {
            var style = new SvgStyleProperty();
            Assert.Equal((SvgFillFlags)0, style.fill.flags);
            Assert.Equal(0, style.fill.opacity);
            Assert.Equal(FillRule.NonZero, style.fill.fillRule);
        }

        [Fact]
        public void SvgStyleProperty_StrokeDefaults()
        {
            var style = new SvgStyleProperty();
            Assert.Equal((SvgStrokeFlags)0, style.stroke.flags);
            Assert.Equal(0, style.stroke.opacity);
            Assert.Equal(0.0f, style.stroke.width);
        }

        // ========== SvgNodeIdPair ==========

        [Fact]
        public void SvgNodeIdPair_Creation()
        {
            var node = new SvgNode();
            var pair = new SvgNodeIdPair(node, "testId");
            Assert.Same(node, pair.node);
            Assert.Equal("testId", pair.id);
        }

        [Fact]
        public void SvgNodeIdPair_InlistNode()
        {
            var node = new SvgNode();
            var pair = new SvgNodeIdPair(node, "test");
            Assert.Null(pair.Prev);
            Assert.Null(pair.Next);
        }

        [Fact]
        public void SvgNodeIdPair_InlistOperations()
        {
            var list = new Inlist<SvgNodeIdPair>();
            Assert.True(list.Empty());

            var pair1 = new SvgNodeIdPair(new SvgNode(), "a");
            var pair2 = new SvgNodeIdPair(new SvgNode(), "b");
            list.Back(pair1);
            list.Back(pair2);
            Assert.False(list.Empty());

            var popped = list.PopFront();
            Assert.Same(pair1, popped);
            Assert.Equal("a", popped!.id);

            popped = list.PopFront();
            Assert.Same(pair2, popped);
            Assert.Equal("b", popped!.id);

            Assert.True(list.Empty());
        }

        // ========== SvgStyleGradient ==========

        [Fact]
        public void SvgStyleGradient_LinearDefaults()
        {
            var grad = new SvgStyleGradient();
            grad.type = SvgGradientType.Linear;
            grad.linear = new SvgLinearGradient();
            Assert.Equal(0.0f, grad.linear.x1);
            Assert.Equal(0.0f, grad.linear.y1);
            Assert.Equal(0.0f, grad.linear.x2);
            Assert.Equal(0.0f, grad.linear.y2);
        }

        [Fact]
        public void SvgStyleGradient_RadialDefaults()
        {
            var grad = new SvgStyleGradient();
            grad.type = SvgGradientType.Radial;
            grad.radial = new SvgRadialGradient();
            Assert.Equal(0.0f, grad.radial.cx);
            Assert.Equal(0.0f, grad.radial.cy);
            Assert.Equal(0.0f, grad.radial.r);
        }

        [Fact]
        public void SvgStyleGradient_AddStops()
        {
            var grad = new SvgStyleGradient();
            grad.stops.Add(new Fill.ColorStop { offset = 0, r = 255, g = 0, b = 0, a = 255 });
            grad.stops.Add(new Fill.ColorStop { offset = 1, r = 0, g = 0, b = 255, a = 255 });
            Assert.Equal(2, grad.stops.Count);
        }

        // ========== SvgLoaderData ==========

        [Fact]
        public void SvgLoaderData_InitialState()
        {
            var data = new SvgLoaderData();
            Assert.Null(data.doc);
            Assert.Null(data.def);
            Assert.Null(data.cssStyle);
            Assert.NotNull(data.stack);
            Assert.Empty(data.stack);
            Assert.NotNull(data.gradients);
            Assert.Empty(data.gradients);
        }

        // ========== Box struct ==========

        [Fact]
        public void Box_Constructor()
        {
            var box = new Box(1, 2, 3, 4);
            Assert.Equal(1.0f, box.x);
            Assert.Equal(2.0f, box.y);
            Assert.Equal(3.0f, box.w);
            Assert.Equal(4.0f, box.h);
        }

        // ========== Enum values ==========

        [Fact]
        public void SvgNodeType_HasExpectedValues()
        {
            // Verify key enum values exist
            Assert.NotEqual(SvgNodeType.Doc, SvgNodeType.G);
            Assert.NotEqual(SvgNodeType.Rect, SvgNodeType.Circle);
            Assert.NotEqual(SvgNodeType.Path, SvgNodeType.Polygon);
        }

        [Fact]
        public void SvgStyleFlags_AreBitFlags()
        {
            var combined = SvgStyleFlags.Color | SvgStyleFlags.Fill;
            Assert.True((combined & SvgStyleFlags.Color) != 0);
            Assert.True((combined & SvgStyleFlags.Fill) != 0);
            Assert.True((combined & SvgStyleFlags.Stroke) == 0);
        }
    }
}
