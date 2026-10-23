using Xunit;

namespace ThorVG.Tests
{
    public class tvgConicGradientTests
    {
        [Fact]
        public void ParametersAndDuplicate_PreserveIndependentFillState()
        {
            var fill = ConicGradient.Gen();
            Assert.Equal(Type.ConicGradient, fill.GetFillType());
            fill.Conic(out float x, out float y, out float angle);
            Assert.Equal((0f, 0f, 0f), (x, y, angle));
            Assert.Equal(Result.Success, fill.Conic(12, -8, 463));
            fill.SetSpread(FillSpread.Reflect);
            fill.SetColorStops(new Fill.ColorStop[] { new(0, 255, 0, 0, 255), new(1, 0, 0, 255, 128) }, 2);
            var matrix = TvgMath.Identity(); matrix.e12 = .5f; matrix.e13 = 7;
            fill.SetTransform(matrix);
            var copy = Assert.IsType<ConicGradient>(fill.Duplicate());
            copy.Conic(out x, out y, out angle);
            Assert.Equal((12f, -8f, 463f), (x, y, angle));
            Assert.Equal(FillSpread.Reflect, copy.GetSpread());
            Assert.Equal(matrix, copy.GetTransform());
            Assert.Equal(2u, copy.GetColorStops(out var stops));
            fill.Conic(0, 0, 0);
            fill.SetColorStops(null, 0);
            Assert.Equal(255, stops![0].r);
            copy.Conic(out x, out y, out angle);
            Assert.Equal((12f, -8f, 463f), (x, y, angle));
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(17f)]
        [InlineData(90f)]
        [InlineData(223.5f)]
        public void AngularWrap_IgnoresSpreadAndWholeTurns(float angle)
        {
            Initializer.Init();
            try
            {
                uint[] Render(float start, FillSpread spread)
                {
                    var fill = ConicGradient.Gen(); fill.Conic(32, 32, start); fill.SetSpread(spread);
                    fill.SetColorStops(new Fill.ColorStop[] { new(0, 255, 0, 0, 255), new(.35f, 0, 255, 0, 160), new(1, 0, 0, 255, 255) }, 3);
                    var shape = Shape.Gen(); shape.AppendCircle(32, 32, 28, 24); shape.SetFill(fill);
                    var pixels = new uint[64 * 64]; using var canvas = SwCanvas.Gen(EngineOption.None)!;
                    Assert.Equal(Result.Success, canvas.Target(pixels, 64, 64, 64, ColorSpace.ABGR8888)); canvas.Add(shape);
                    Assert.Equal(Result.Success, canvas.Draw(true)); Assert.Equal(Result.Success, canvas.Sync()); canvas.Remove();
                    return pixels;
                }
                Assert.Equal(Render(angle, FillSpread.Pad), Render(angle, FillSpread.Repeat));
                Assert.Equal(Render(angle, FillSpread.Pad), Render(angle, FillSpread.Reflect));
                // The target stores the unnormalized angle; exact whole-turn equality
                // is reliable at cardinal angles, while noncardinal seam math rounds.
                if (angle == 0 || angle == 90) Assert.Equal(Render(angle, FillSpread.Pad), Render(angle + 360, FillSpread.Pad));
            }
            finally { Initializer.Term(); }
        }
    }
}
