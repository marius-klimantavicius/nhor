// Ported from ThorVG/test/testPaint.cpp

using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace ThorVG.Tests
{
    public class testPaint
    {
        private static readonly string TEST_DIR = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ref", "ThorVG", "test", "resources"));

        private const float MARGIN = 0.000001f;

        private static void AssertApprox(float expected, float actual, float margin = MARGIN)
        {
            Assert.InRange(actual, expected - margin, expected + margin);
        }

        [Fact]
        public void CustomTransformation()
        {
            var shape = Shape.Gen();
            Assert.NotNull(shape);

            // Verify default transform
            ref var m1 = ref shape.Transform();
            AssertApprox(1.0f, m1.e11);
            AssertApprox(0.0f, m1.e12);
            AssertApprox(0.0f, m1.e13);
            AssertApprox(0.0f, m1.e21);
            AssertApprox(1.0f, m1.e22);
            AssertApprox(0.0f, m1.e23);
            AssertApprox(0.0f, m1.e31);
            AssertApprox(0.0f, m1.e32);
            AssertApprox(1.0f, m1.e33);

            // Custom transform
            var m2 = new Matrix(1.0f, 2.0f, 3.0f, 4.0f, 0.0f, -4.0f, -3.0f, -2.0f, -1.0f);
            Assert.Equal(Result.Success, shape.Transform(m2));

            ref var m3 = ref shape.Transform();
            AssertApprox(m2.e11, m3.e11);
            AssertApprox(m2.e12, m3.e12);
            AssertApprox(m2.e13, m3.e13);
            AssertApprox(m2.e21, m3.e21);
            AssertApprox(m2.e22, m3.e22);
            AssertApprox(m2.e23, m3.e23);
            AssertApprox(m2.e31, m3.e31);
            AssertApprox(m2.e32, m3.e32);
            AssertApprox(m2.e33, m3.e33);

            // It's not allowed if the custom transform is applied.
            Assert.Equal(Result.InsufficientCondition, shape.Translate(155.0f, -155.0f));
            Assert.Equal(Result.InsufficientCondition, shape.Scale(4.7f));
            Assert.Equal(Result.InsufficientCondition, shape.Rotate(45.0f));

            // Verify Transform is not modified
            ref var m4 = ref shape.Transform();
            AssertApprox(m2.e11, m4.e11);
            AssertApprox(m2.e12, m4.e12);
            AssertApprox(m2.e13, m4.e13);
            AssertApprox(m2.e21, m4.e21);
            AssertApprox(m2.e22, m4.e22);
            AssertApprox(m2.e23, m4.e23);
            AssertApprox(m2.e31, m4.e31);
            AssertApprox(m2.e32, m4.e32);
            AssertApprox(m2.e33, m4.e33);

            Paint.Rel(shape);
        }

        [Fact]
        public void BasicTransformation()
        {
            var shape = Shape.Gen();
            Assert.NotNull(shape);

            Assert.Equal(Result.Success, shape.Translate(155.0f, -155.0f));
            Assert.Equal(Result.Success, shape.Rotate(45.0f));
            Assert.Equal(Result.Success, shape.Scale(4.7f));

            ref var m = ref shape.Transform();
            AssertApprox(3.323402f, m.e11);
            AssertApprox(-3.323401f, m.e12);
            AssertApprox(155.0f, m.e13);
            AssertApprox(3.323401f, m.e21);
            AssertApprox(3.323402f, m.e22);
            AssertApprox(-155.0f, m.e23);
            AssertApprox(0.0f, m.e31);
            AssertApprox(0.0f, m.e32);
            AssertApprox(1.0f, m.e33);

            Paint.Rel(shape);
        }

        [Fact]
        public void Opacity()
        {
            var shape = Shape.Gen();
            Assert.NotNull(shape);

            Assert.Equal((byte)255, shape.Opacity());

            Assert.Equal(Result.Success, shape.Opacity(155));
            Assert.Equal((byte)155, shape.Opacity());

            // C++ passes -1 which wraps to 255 as uint8_t. In C# byte is unsigned, use 255.
            Assert.Equal(Result.Success, shape.Opacity(255));
            Assert.Equal((byte)255, shape.Opacity());

            Assert.Equal(Result.Success, shape.Opacity(0));
            Assert.Equal((byte)0, shape.Opacity());

            Paint.Rel(shape);
        }

        [Fact]
        public void Visibility()
        {
            var shape = Shape.Gen();
            Assert.NotNull(shape);

            Assert.True(shape.IsVisible());

            Assert.Equal(Result.Success, shape.Visible(false));
            Assert.False(shape.IsVisible());

            Assert.Equal(Result.Success, shape.Visible(false));
            Assert.False(shape.IsVisible());

            Assert.Equal(Result.Success, shape.Visible(true));
            Assert.True(shape.IsVisible());

            Paint.Rel(shape);
        }

        [Fact]
        public void BoundingBox()
        {
            Assert.Equal(Result.Success, Initializer.Init());
            {
                var buffer = new uint[500 * 500];
                var canvas = SwCanvas.Gen();
                canvas.Target(buffer, 500, 500, 500, ColorSpace.ARGB8888);

                var shape = Shape.Gen();
                canvas.Add(shape);

                // Negative
                Assert.Equal(Result.InsufficientCondition, shape.Bounds(out var x, out var y, out var w, out var h));

                // Case 1
                Assert.Equal(Result.Success, shape.AppendRect(0.0f, 10.0f, 20.0f, 100.0f, 50.0f, 50.0f));
                Assert.Equal(Result.Success, shape.Translate(100.0f, 111.0f));

                canvas.Update();

                // Positive
                Assert.Equal(Result.Success, shape.Bounds(out x, out y, out w, out h));
                Assert.Equal(100.0f, x);
                Assert.Equal(121.0f, y);
                Assert.Equal(20.0f, w);
                Assert.Equal(100.0f, h);

                var pts = new Point[4];
                Assert.Equal(Result.Success, shape.Bounds(pts));
                Assert.Equal(100.0f, pts[0].x);
                Assert.Equal(100.0f, pts[3].x);
                Assert.Equal(121.0f, pts[0].y);
                Assert.Equal(121.0f, pts[1].y);
                Assert.Equal(120.0f, pts[1].x);
                Assert.Equal(120.0f, pts[2].x);
                Assert.Equal(221.0f, pts[2].y);
                Assert.Equal(221.0f, pts[3].y);

                Assert.Equal(Result.Success, canvas.Sync());

                // Case 2
                Assert.Equal(Result.Success, shape.ResetShape());
                Assert.Equal(Result.Success, shape.MoveTo(0.0f, 10.0f));
                Assert.Equal(Result.Success, shape.LineTo(20.0f, 210.0f));
                var identity = new Matrix(1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 1.0f);
                Assert.Equal(Result.Success, shape.Transform(identity));

                Assert.Equal(Result.Success, canvas.Update());

                Assert.Equal(Result.Success, shape.Bounds(out x, out y, out w, out h));
                Assert.Equal(0.0f, x);
                Assert.Equal(10.0f, y);
                Assert.Equal(20.0f, w);
                Assert.Equal(200.0f, h);

                Assert.Equal(Result.Success, shape.Bounds(pts));
                Assert.Equal(0.0f, pts[0].x);
                Assert.Equal(0.0f, pts[3].x);
                Assert.Equal(10.0f, pts[0].y);
                Assert.Equal(10.0f, pts[1].y);
                Assert.Equal(20.0f, pts[1].x);
                Assert.Equal(20.0f, pts[2].x);
                Assert.Equal(210.0f, pts[2].y);
                Assert.Equal(210.0f, pts[3].y);

                Assert.Equal(Result.Success, canvas.Sync());

                // Case 3
                Assert.Equal(Result.Success, shape.ResetShape());
                Assert.Equal(Result.Success, shape.MoveTo(10, 10));
                Assert.Equal(Result.Success, shape.LineTo(190, 10));
                Assert.Equal(Result.Success, shape.StrokeWidth(12.0f));
                Assert.Equal(Result.Success, shape.StrokeFill(255, 0, 0, 255));

                Assert.Equal(Result.Success, canvas.Update());

                Assert.Equal(Result.Success, shape.Bounds(out x, out y, out w, out h));
                Assert.Equal(4.0f, x);
                Assert.Equal(4.0f, y);
                Assert.Equal(12.0f, h);
                Assert.Equal(192.0f, w);

                Assert.Equal(Result.Success, shape.Bounds(pts));
                Assert.Equal(4.0f, pts[0].x);
                Assert.Equal(4.0f, pts[3].x);
                Assert.Equal(4.0f, pts[0].y);
                Assert.Equal(4.0f, pts[1].y);
                Assert.Equal(196.0f, pts[1].x);
                Assert.Equal(196.0f, pts[2].x);
                Assert.Equal(16.0f, pts[2].y);
                Assert.Equal(16.0f, pts[3].y);

                // Text (TTF loader support)
                Assert.Equal(Result.Success, Text.LoadFont(TEST_DIR + "/PublicSans-Regular.ttf"));
                var text = Text.Gen();
                Assert.Equal(Result.Success, canvas.Add(text));
                Assert.Equal(Result.Success, canvas.Sync());

                // Empty Size
                Assert.Equal(Result.Success, text.Bounds(out x, out y, out w, out h));

                // Case 1
                Assert.Equal(Result.Success, text.SetFont("PublicSans-Regular"));
                Assert.Equal(Result.Success, text.SetFontSize(32));
                Assert.Equal(Result.Success, text.SetText("TEST"));
                Assert.Equal(Result.Success, text.Translate(100.0f, 111.0f));
                Assert.Equal(Result.Success, text.Bounds(out x, out y, out w, out h));

                AssertApprox(100.981331f, x);
                AssertApprox(120.301323f, y);
                AssertApprox(103.722679f, w);
                AssertApprox(31.658669f, h, 0.001f);

                Assert.Equal(Result.Success, canvas.Update());

                Assert.Equal(Result.Success, text.Bounds(pts));
                AssertApprox(100.981331f, pts[0].x);
                AssertApprox(204.704010f, pts[1].x);
                AssertApprox(204.704010f, pts[2].x);
                AssertApprox(100.981331f, pts[3].x);
                AssertApprox(120.301323f, pts[0].y);
                AssertApprox(120.301323f, pts[1].y);
                AssertApprox(151.959991f, pts[2].y);
                AssertApprox(151.959991f, pts[3].y);

                // Case 2
                Assert.Equal(Result.Success, text.SetText("BOUNDS"));
                identity = new Matrix(1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 1.0f);
                Assert.Equal(Result.Success, text.Transform(identity));
                Assert.Equal(Result.Success, text.Bounds(out x, out y, out w, out h));

                AssertApprox(4.074667f, x);
                AssertApprox(9.258667f, y);
                AssertApprox(173.503998f, w);
                AssertApprox(31.701332f, h);

                Assert.Equal(Result.Success, canvas.Update());
                Assert.Equal(Result.Success, text.Bounds(pts));
                AssertApprox(4.074667f, pts[0].x);
                AssertApprox(177.578659f, pts[1].x);
                AssertApprox(177.578659f, pts[2].x);
                AssertApprox(4.074667f, pts[3].x);
                AssertApprox(9.258667f, pts[0].y);
                AssertApprox(9.258667f, pts[1].y);
                AssertApprox(40.959999f, pts[2].y);
                AssertApprox(40.959999f, pts[3].y);
            }
            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void Intersection()
        {
            Assert.Equal(Result.Success, Initializer.Init());
            {
                var canvas = SwCanvas.Gen();

                var buffer = new uint[200 * 200];
                canvas.Target(buffer, 200, 200, 200, ColorSpace.ARGB8888);

                var shape = Shape.Gen();
                Assert.NotNull(shape);
                Assert.Equal(Result.Success, shape.AppendRect(50, 50, 100, 100));
                Assert.Equal(Result.Success, shape.SetFill(255, 0, 0, 255));

                Assert.Equal(Result.Success, canvas.Add(shape));
                Assert.Equal(Result.Success, canvas.Draw());

                // Case1. Fully contained
                Assert.True(shape.Intersects(0, 0, 200, 200));

                // Case2. Partially overlapping
                Assert.True(shape.Intersects(25, 25, 50, 50));
                Assert.True(shape.Intersects(125, 125, 50, 50));

                // Case3. Edge-touching
                Assert.True(shape.Intersects(49, 49, 2, 2));
                Assert.True(shape.Intersects(149, 149, 2, 2));

                // Case4. Fully separated
                Assert.False(shape.Intersects(0, 0, 25, 25));
                Assert.False(shape.Intersects(175, 175, 25, 25));
            }
            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void Duplication()
        {
            var paints = new List<Paint>();

            var shape = Shape.Gen();
            Assert.NotNull(shape);
            paints.Add(shape);

            // TTF loader support
            Assert.Equal(Result.Success, Text.LoadFont(TEST_DIR + "/PublicSans-Regular.ttf"));
            var text = Text.Gen();
            Assert.NotNull(text);
            Assert.Equal(Result.Success, text.SetFont("PublicSans-Regular"));
            Assert.Equal(Result.Success, text.SetFontSize(32));
            Assert.Equal(Result.Success, text.SetText("Original Text"));
            Assert.Equal(Result.Success, text.SetFill(255, 0, 0));
            paints.Add(text);

            foreach (var paint in paints)
            {
                // Setup paint properties
                Assert.Equal(Result.Success, paint.Opacity(0));
                Assert.Equal(Result.Success, paint.Translate(200.0f, 100.0f));
                Assert.Equal(Result.Success, paint.Scale(2.2f));
                Assert.Equal(Result.Success, paint.Rotate(90.0f));

                var comp = Shape.Gen();
                Assert.NotNull(comp);
                Assert.Equal(Result.Success, paint.Clip(comp));

                // Duplication
                var dup = paint.Duplicate();
                Assert.NotNull(dup);

                // Compare properties
                Assert.Equal((byte)0, dup.Opacity());

                ref var m = ref paint.Transform();
                AssertApprox(0.0f, m.e11);
                AssertApprox(-2.2f, m.e12);
                AssertApprox(200.0f, m.e13);
                AssertApprox(2.2f, m.e21);
                AssertApprox(0.0f, m.e22);
                AssertApprox(100.0f, m.e23);
                AssertApprox(0.0f, m.e31);
                AssertApprox(0.0f, m.e32);
                AssertApprox(1.0f, m.e33);

                Paint.Rel(dup);
            }

            // release
            foreach (var p in paints)
            {
                Paint.Rel(p);
            }
        }

        [Fact]
        public void ReferenceCount()
        {
            var shape = Shape.Gen();
            Assert.Equal((ushort)0, shape.RefCnt());
            Assert.Equal((ushort)0, shape.Unref(false));
            Assert.Equal((ushort)1, shape.Ref());
            Assert.Equal((ushort)2, shape.Ref());
            Assert.Equal((ushort)3, shape.Ref());
            Assert.Equal((ushort)2, shape.Unref());
            Assert.Equal((ushort)1, shape.Unref());
            Assert.Equal((ushort)0, shape.Unref());

            Initializer.Init();
            {
                var canvas = SwCanvas.Gen();

                shape = Shape.Gen();
                Assert.Equal((ushort)1, shape.Ref());
                canvas.Add(shape);
                Assert.Equal((ushort)2, shape.RefCnt());
                Assert.Equal((ushort)1, shape.Unref());

                shape = Shape.Gen();
                Assert.Equal((ushort)1, shape.Ref());
                var scene = Scene.Gen();
                scene.Add(shape);
                canvas.Add(scene);
                Assert.Equal((ushort)2, shape.RefCnt());
                Assert.Equal((ushort)1, shape.Unref());

                shape = Shape.Gen();
                Assert.Equal((ushort)1, shape.Ref());
                scene = Scene.Gen();
                scene.Add(shape);
                scene.Remove();
                canvas.Add(scene);
                Assert.Equal((ushort)0, shape.Unref());
            }
            Initializer.Term();
        }
    }
}
