// Tests for Lottie model types

using Xunit;

namespace ThorVG.Tests
{
    public class LottieModelTests
    {
        // ---- LottieComposition ----

        [Fact]
        public void LottieComposition_DefaultFields()
        {
            var comp = new LottieComposition();
            Assert.Equal(0f, comp.w);
            Assert.Equal(0f, comp.h);
            Assert.Equal(0f, comp.frameRate);
            Assert.Null(comp.version);
            Assert.Null(comp.root);
        }

        [Fact]
        public void LottieComposition_FrameCnt()
        {
            var comp = new LottieComposition();
            comp.root = new LottieLayer();
            comp.root.inFrame = 10;
            comp.root.outFrame = 70;
            Assert.Equal(60f, comp.FrameCnt());
        }

        [Fact]
        public void LottieComposition_Duration()
        {
            var comp = new LottieComposition();
            comp.root = new LottieLayer();
            comp.root.inFrame = 0;
            comp.root.outFrame = 90;
            comp.frameRate = 30;
            // Duration = 90 / 30 = 3.0
            Assert.Equal(3.0f, comp.Duration());
        }

        [Fact]
        public void LottieComposition_Clamp_WithinRange()
        {
            var comp = new LottieComposition();
            comp.root = new LottieLayer();
            comp.root.inFrame = 0;
            comp.root.outFrame = 60;

            float no = 30f;
            comp.Clamp(ref no);
            Assert.Equal(30f, no);
        }

        [Fact]
        public void LottieComposition_Clamp_BelowRange()
        {
            var comp = new LottieComposition();
            comp.root = new LottieLayer();
            comp.root.inFrame = 10;
            comp.root.outFrame = 60;

            // Clamp adds inFrame to the input: 5 + 10 = 15, then clamps to [inFrame, outFrame)
            float no = 5f;
            comp.Clamp(ref no);
            Assert.Equal(15f, no);
        }

        [Fact]
        public void LottieComposition_Clamp_AboveRange()
        {
            var comp = new LottieComposition();
            comp.root = new LottieLayer();
            comp.root.inFrame = 0;
            comp.root.outFrame = 60;

            float no = 100f;
            comp.Clamp(ref no);
            // Should be clamped to outFrame - 1 frame epsilon or outFrame
            Assert.InRange(no, 0f, 60f);
        }

        // ---- LottieLayer ----

        [Fact]
        public void LottieLayer_DefaultType()
        {
            var layer = new LottieLayer();
            Assert.Equal(LottieObject.ObjectType.Layer, layer.type);
        }

        [Fact]
        public void LottieLayer_Children()
        {
            var layer = new LottieLayer();
            Assert.NotNull(layer.children);
            Assert.Empty(layer.children);
        }

        [Fact]
        public void LottieLayer_Hidden_Default()
        {
            var layer = new LottieLayer();
            Assert.False(layer.hidden);
        }

        // ---- LottieGroup ----

        [Fact]
        public void LottieGroup_Children_IsModifiable()
        {
            var group = new LottieGroup();
            var child = new LottieObject();
            group.children.Add(child);
            Assert.Single(group.children);
        }

        // ---- LottieObject ----

        [Fact]
        public void LottieObject_DefaultType()
        {
            var obj = new LottieObject();
            // Default type is Composition (0)
            Assert.Equal(LottieObject.ObjectType.Composition, obj.type);
        }

        // ---- LottieMarker ----

        [Fact]
        public void LottieMarker_Fields()
        {
            var marker = new LottieMarker();
            marker.name = "testMarker";
            marker.time = 10f;
            marker.duration = 20f;
            Assert.Equal("testMarker", marker.name);
            Assert.Equal(10f, marker.time);
            Assert.Equal(20f, marker.duration);
        }

        // ---- LottieBitmap ----

        [Fact]
        public void LottieBitmap_DefaultFields()
        {
            var bmp = new LottieBitmap();
            Assert.Null(bmp.path);
            Assert.Null(bmp.mimeType);
            Assert.Equal(0f, bmp.width);
            Assert.Equal(0f, bmp.height);
        }
    }
}
