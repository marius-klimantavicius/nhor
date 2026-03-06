using Xunit;

namespace ThorVG.Tests
{
    public class tvgShapeTests
    {
        [Fact]
        public void Shape_Gen_ReturnsInstance()
        {
            var shape = Shape.Gen();
            Assert.NotNull(shape);
            Assert.Equal(Type.Shape, shape.PaintType());
        }

        [Fact]
        public void Shape_MoveTo_LineTo_Close()
        {
            var shape = Shape.Gen();
            Assert.Equal(Result.Success, shape.MoveTo(0, 0));
            Assert.Equal(Result.Success, shape.LineTo(100, 0));
            Assert.Equal(Result.Success, shape.LineTo(100, 100));
            Assert.Equal(Result.Success, shape.Close());

            unsafe
            {
                shape.GetPath(out var cmds, out uint cmdsCnt, out var pts, out uint ptsCnt);
                Assert.Equal(4u, cmdsCnt);
                Assert.Equal(3u, ptsCnt);
                Assert.Equal(PathCommand.MoveTo, cmds[0]);
                Assert.Equal(PathCommand.LineTo, cmds[1]);
                Assert.Equal(PathCommand.LineTo, cmds[2]);
                Assert.Equal(PathCommand.Close, cmds[3]);
            }
        }

        [Fact]
        public void Shape_CubicTo()
        {
            var shape = Shape.Gen();
            shape.MoveTo(0, 0);
            Assert.Equal(Result.Success, shape.CubicTo(10, 20, 30, 40, 50, 60));
        }

        [Fact]
        public void Shape_AppendRect_Simple()
        {
            var shape = Shape.Gen();
            Assert.Equal(Result.Success, shape.AppendRect(0, 0, 100, 50));

            unsafe
            {
                shape.GetPath(out var cmds, out uint cmdsCnt, out var pts, out uint ptsCnt);
                Assert.Equal(5u, cmdsCnt); // MoveTo + 3 LineTo + Close
                Assert.Equal(4u, ptsCnt);
            }
        }

        [Fact]
        public void Shape_AppendRect_Rounded()
        {
            var shape = Shape.Gen();
            Assert.Equal(Result.Success, shape.AppendRect(0, 0, 100, 50, 10, 10));

            unsafe
            {
                shape.GetPath(out var cmds, out uint cmdsCnt, out var pts, out uint ptsCnt);
                Assert.Equal(10u, cmdsCnt); // 1 MoveTo + 4 LineTo + 4 CubicTo + 1 Close
                Assert.Equal(17u, ptsCnt);
            }
        }

        [Fact]
        public void Shape_AppendCircle()
        {
            var shape = Shape.Gen();
            Assert.Equal(Result.Success, shape.AppendCircle(50, 50, 30, 30));

            unsafe
            {
                shape.GetPath(out var cmds, out uint cmdsCnt, out var pts, out uint ptsCnt);
                Assert.Equal(6u, cmdsCnt); // MoveTo + 4 CubicTo + Close
                Assert.Equal(13u, ptsCnt);
            }
        }

        [Fact]
        public void Shape_FillColor_SetAndGet()
        {
            var shape = Shape.Gen();
            Assert.Equal(Result.Success, shape.SetFill(128, 64, 32, 200));
            shape.GetFillColor(out byte r, out byte g, out byte b, out byte a);
            Assert.Equal(128, r);
            Assert.Equal(64, g);
            Assert.Equal(32, b);
            Assert.Equal(200, a);
        }

        [Fact]
        public void Shape_FillGradient_SetAndGet()
        {
            var shape = Shape.Gen();
            var grad = LinearGradient.Gen();
            Assert.Equal(Result.Success, shape.SetFill(grad));
            Assert.Same(grad, shape.GetFill());
        }

        [Fact]
        public void Shape_FillRule_SetAndGet()
        {
            var shape = Shape.Gen();
            Assert.Equal(FillRule.NonZero, shape.GetFillRule());
            shape.SetFillRule(FillRule.EvenOdd);
            Assert.Equal(FillRule.EvenOdd, shape.GetFillRule());
        }

        [Fact]
        public void Shape_StrokeWidth_SetAndGet()
        {
            var shape = Shape.Gen();
            Assert.Equal(0.0f, shape.GetStrokeWidth());
            shape.StrokeWidth(3.5f);
            Assert.Equal(3.5f, shape.GetStrokeWidth());
        }

        [Fact]
        public void Shape_StrokeFillColor_SetAndGet()
        {
            var shape = Shape.Gen();
            shape.StrokeFill(100, 200, 50, 150);
            shape.GetStrokeFill(out byte r, out byte g, out byte b, out byte a);
            Assert.Equal(100, r);
            Assert.Equal(200, g);
            Assert.Equal(50, b);
            Assert.Equal(150, a);
        }

        [Fact]
        public void Shape_StrokeFillGradient()
        {
            var shape = Shape.Gen();
            var grad = RadialGradient.Gen();
            Assert.Equal(Result.Success, shape.StrokeFill(grad));
            Assert.Same(grad, shape.GetStrokeFillGradient());
        }

        [Fact]
        public void Shape_StrokeJoin_SetAndGet()
        {
            var shape = Shape.Gen();
            shape.StrokeJoin(StrokeJoin.Round);
            Assert.Equal(StrokeJoin.Round, shape.GetStrokeJoin());
        }

        [Fact]
        public void Shape_StrokeCap_SetAndGet()
        {
            var shape = Shape.Gen();
            shape.StrokeCap(StrokeCap.Round);
            Assert.Equal(StrokeCap.Round, shape.GetStrokeCap());
        }

        [Fact]
        public void Shape_StrokeMiterlimit_SetAndGet()
        {
            var shape = Shape.Gen();
            Assert.Equal(Result.Success, shape.StrokeMiterlimit(8.0f));
            Assert.Equal(8.0f, shape.GetStrokeMiterlimit());
        }

        [Fact]
        public void Shape_StrokeMiterlimit_Negative_InvalidArgs()
        {
            var shape = Shape.Gen();
            Assert.Equal(Result.InvalidArguments, shape.StrokeMiterlimit(-1.0f));
        }

        [Fact]
        public void Shape_StrokeDash()
        {
            var shape = Shape.Gen();
            var pattern = new float[] { 10.0f, 5.0f };
            Assert.Equal(Result.Success, shape.StrokeDash(pattern, 2, 1.0f));
        }

        [Fact]
        public void Shape_Trimpath()
        {
            var shape = Shape.Gen();
            Assert.Equal(Result.Success, shape.Trimpath(0.2f, 0.8f));
        }

        [Fact]
        public void Shape_Order()
        {
            var shape = Shape.Gen();
            Assert.Equal(Result.Success, shape.Order(true));
        }

        [Fact]
        public void Shape_ResetShape()
        {
            var shape = Shape.Gen();
            shape.MoveTo(0, 0);
            shape.LineTo(100, 100);
            shape.SetFill(255, 0, 0, 255);
            Assert.Equal(Result.Success, shape.ResetShape());
        }

        [Fact]
        public void Shape_Duplicate_CopiesPath()
        {
            var shape = Shape.Gen();
            shape.AppendRect(0, 0, 50, 50);
            shape.SetFill(100, 200, 50, 255);

            var dup = (Shape)shape.Duplicate();
            Assert.NotSame(shape, dup);
            Assert.Equal(Type.Shape, dup.PaintType());

            dup.GetFillColor(out byte r, out byte g, out byte b, out byte a);
            Assert.Equal(100, r);
            Assert.Equal(200, g);
            Assert.Equal(50, b);
            Assert.Equal(255, a);

            unsafe
            {
                dup.GetPath(out var cmds, out uint cmdsCnt, out var pts, out uint ptsCnt);
                Assert.Equal(5u, cmdsCnt);
                Assert.Equal(4u, ptsCnt);
            }
        }
    }
}
