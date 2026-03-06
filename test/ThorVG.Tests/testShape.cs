// Ported from ThorVG/test/testShape.cpp

using System;
using System.IO;
using Xunit;

namespace ThorVG.Tests
{
    public class testShape
    {
        private static readonly string TEST_DIR = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ThorVG", "test", "resources"));

        [Fact]
        public void ShapeCreation()
        {
            var shape = Shape.Gen();
            Assert.NotNull(shape);

            Assert.Equal(Type.Shape, shape.PaintType());

            Paint.Rel(shape);
        }

        [Fact]
        public void AppendingCommands()
        {
            var shape = Shape.Gen();
            Assert.NotNull(shape);

            Assert.Equal(Result.Success, shape.Close());

            Assert.Equal(Result.Success, shape.MoveTo(100, 100));
            Assert.Equal(Result.Success, shape.MoveTo(99999999.0f, -99999999.0f));
            Assert.Equal(Result.Success, shape.MoveTo(0, 0));

            Assert.Equal(Result.Success, shape.LineTo(120, 140));
            Assert.Equal(Result.Success, shape.LineTo(99999999.0f, -99999999.0f));
            Assert.Equal(Result.Success, shape.LineTo(0, 0));

            Assert.Equal(Result.Success, shape.CubicTo(0, 0, 0, 0, 0, 0));
            Assert.Equal(Result.Success, shape.CubicTo(0, 0, 99999999.0f, -99999999.0f, 0, 0));
            Assert.Equal(Result.Success, shape.CubicTo(0, 0, 99999999.0f, -99999999.0f, 99999999.0f, -99999999.0f));
            Assert.Equal(Result.Success, shape.CubicTo(99999999.0f, -99999999.0f, 99999999.0f, -99999999.0f, 99999999.0f, -99999999.0f));

            Assert.Equal(Result.Success, shape.Close());

            Assert.Equal(Result.Success, shape.ResetShape());
            Assert.Equal(Result.Success, shape.ResetShape());

            Paint.Rel(shape);
        }

        [Fact]
        public void AppendingShapes()
        {
            var shape = Shape.Gen();
            Assert.NotNull(shape);

            Assert.Equal(Result.Success, shape.MoveTo(100, 100));
            Assert.Equal(Result.Success, shape.LineTo(120, 140));

            Assert.Equal(Result.Success, shape.AppendRect(0, 0, 0, 0, 0, 0));
            Assert.Equal(Result.Success, shape.AppendRect(0, 0, 99999999.0f, -99999999.0f, 0, 0, true));
            Assert.Equal(Result.Success, shape.AppendRect(0, 0, 0, 0, -99999999.0f, 99999999.0f, false));
            Assert.Equal(Result.Success, shape.AppendRect(99999999.0f, -99999999.0f, 99999999.0f, -99999999.0f, 99999999.0f, -99999999.0f, true));
            Assert.Equal(Result.Success, shape.AppendRect(99999999.0f, -99999999.0f, 99999999.0f, -99999999.0f, 99999999.0f, -99999999.0f, false));

            Assert.Equal(Result.Success, shape.AppendCircle(0, 0, 0, 0));
            Assert.Equal(Result.Success, shape.AppendCircle(-99999999.0f, 99999999.0f, 0, 0, true));
            Assert.Equal(Result.Success, shape.AppendCircle(-99999999.0f, 99999999.0f, -99999999.0f, 99999999.0f, false));

            Paint.Rel(shape);
        }

        [Fact]
        public unsafe void AppendingPaths()
        {
            var shape = Shape.Gen();
            Assert.NotNull(shape);

            // Negative cases
            Assert.Equal(Result.InvalidArguments, shape.AppendPath(null, 0, null, 0));
            Assert.Equal(Result.InvalidArguments, shape.AppendPath(null, 100, null, 0));
            Assert.Equal(Result.InvalidArguments, shape.AppendPath(null, 0, null, 100));

            var cmds = new PathCommand[]
            {
                PathCommand.Close,
                PathCommand.MoveTo,
                PathCommand.LineTo,
                PathCommand.CubicTo,
                PathCommand.Close
            };

            var pts = new Point[]
            {
                new(100, 100),
                new(200, 200),
                new(10, 10),
                new(20, 20),
                new(30, 30)
            };

            Assert.Equal(Result.InvalidArguments, shape.AppendPath(cmds, 0, pts, 5));
            Assert.Equal(Result.InvalidArguments, shape.AppendPath(cmds, 5, pts, 0));
            Assert.Equal(Result.Success, shape.AppendPath(cmds, 5, pts, 5));

            shape.GetPath(out var cmds2, out var cmds2Cnt, out var pts2, out var pts2Cnt);
            Assert.Equal(5u, cmds2Cnt);
            Assert.Equal(5u, pts2Cnt);

            for (int i = 0; i < 5; ++i)
            {
                Assert.Equal(cmds[i], cmds2[i]);
                Assert.Equal(pts[i].x, pts2[i].x);
                Assert.Equal(pts[i].y, pts2[i].y);
            }

            shape.ResetShape();
            shape.GetPath(out _, out var cmds3Cnt, out _, out var pts3Cnt);
            Assert.Equal(0u, cmds3Cnt);
            Assert.Equal(0u, pts3Cnt);

            Paint.Rel(shape);
        }

        [Fact]
        public void Stroking()
        {
            var shape = Shape.Gen();
            Assert.NotNull(shape);

            // Stroke Order Before Stroke Setting
            Assert.Equal(Result.Success, shape.Order(true));
            Assert.Equal(Result.Success, shape.Order(false));

            // Stroke Width
            Assert.Equal(Result.Success, shape.StrokeWidth(0));
            Assert.Equal(0.0f, shape.GetStrokeWidth());
            Assert.Equal(Result.Success, shape.StrokeWidth(300));
            Assert.Equal(300.0f, shape.GetStrokeWidth());

            // Stroke Color
            Assert.Equal(Result.Success, shape.StrokeFill(0, 50, 100, 200));
            shape.GetStrokeFill(out var r, out var g, out var b, out var a);
            Assert.Equal((byte)100, b);
            shape.GetStrokeFill(out r, out g, out b, out a);
            Assert.Equal((byte)0, r);
            Assert.Equal((byte)50, g);
            Assert.Equal((byte)100, b);
            Assert.Equal((byte)200, a);
            shape.GetStrokeFill(out _, out _, out _, out _);

            // Stroke Dash
            Assert.Equal(Result.InvalidArguments, shape.StrokeDash(null, 3));

            var dashPattern0 = new float[] { -10.0f, 1.5f, 2.22f };
            Assert.Equal(Result.InvalidArguments, shape.StrokeDash(dashPattern0, 0));
            Assert.Equal(Result.Success, shape.StrokeDash(dashPattern0, 3));

            var dashPattern1 = new float[] { 0.0f, 0.0f };
            Assert.Equal(Result.Success, shape.StrokeDash(dashPattern1, 2));

            var dashPattern2 = new float[] { 10.0f };
            Assert.Equal(Result.Success, shape.StrokeDash(dashPattern2, 1));

            var dashPattern3 = new float[] { 1.0f, 1.5f, 2.22f };
            Assert.Equal(Result.Success, shape.StrokeDash(dashPattern3, 3));
            Assert.Equal(Result.Success, shape.StrokeDash(dashPattern3, 3, 4.5f));

            var cnt = shape.GetStrokeDash(out var dashPattern4, out var offset);
            Assert.Equal(3u, cnt);
            Assert.Equal(1.0f, dashPattern4![0]);
            Assert.Equal(1.5f, dashPattern4[1]);
            Assert.Equal(2.22f, dashPattern4[2]);
            Assert.Equal(4.5f, offset);

            Assert.Equal(Result.Success, shape.StrokeDash(null, 0));

            // Stroke Cap
            Assert.Equal(StrokeCap.Square, shape.GetStrokeCap());
            Assert.Equal(Result.Success, shape.StrokeCap(StrokeCap.Round));
            Assert.Equal(Result.Success, shape.StrokeCap(StrokeCap.Butt));
            Assert.Equal(StrokeCap.Butt, shape.GetStrokeCap());

            // Stroke Join
            Assert.Equal(StrokeJoin.Bevel, shape.GetStrokeJoin());
            Assert.Equal(Result.Success, shape.StrokeJoin(StrokeJoin.Miter));
            Assert.Equal(Result.Success, shape.StrokeJoin(StrokeJoin.Round));
            Assert.Equal(StrokeJoin.Round, shape.GetStrokeJoin());

            // Stroke Miterlimit
            Assert.Equal(4.0f, shape.GetStrokeMiterlimit());
            Assert.Equal(Result.Success, shape.StrokeMiterlimit(0.00001f));
            Assert.Equal(0.00001f, shape.GetStrokeMiterlimit());
            Assert.Equal(Result.Success, shape.StrokeMiterlimit(1000.0f));
            Assert.Equal(1000.0f, shape.GetStrokeMiterlimit());
            Assert.Equal(Result.InvalidArguments, shape.StrokeMiterlimit(-0.001f));

            Assert.Equal(Result.Success, shape.Trimpath(0.3f, 0.88f, false));

            // Stroke Order After Stroke Setting
            Assert.Equal(Result.Success, shape.Order(true));
            Assert.Equal(Result.Success, shape.Order(false));

            Paint.Rel(shape);
        }

        [Fact]
        public void ShapeFilling()
        {
            var shape = Shape.Gen();
            Assert.NotNull(shape);

            // Fill Color
            Assert.Equal(Result.Success, shape.SetFill(255, 100, 50, 5));
            shape.GetFillColor(out var r, out _, out var b, out _);
            Assert.Equal((byte)255, r);
            Assert.Equal((byte)50, b);
            shape.GetFillColor(out r, out var g, out b, out var a);
            Assert.Equal((byte)100, g);
            Assert.Equal((byte)5, a);

            // Fill Rule
            Assert.Equal(FillRule.NonZero, shape.GetFillRule());
            Assert.Equal(Result.Success, shape.SetFillRule(FillRule.EvenOdd));
            Assert.Equal(FillRule.EvenOdd, shape.GetFillRule());

            Paint.Rel(shape);
        }
    }
}
