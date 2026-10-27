using Xunit;

namespace ThorVG.Tests
{
    public class tvgGradientUpdateTests
    {
        private static uint[] Render(bool radial, bool update)
        {
            var shape = Shape.Gen(); shape.AppendRect(0, 0, 64, 64);
            Fill fill;
            if (radial) { var r = RadialGradient.Gen(); r.Radial(32, 32, 8, 32, 32, 0); fill = r; }
            else { var l = LinearGradient.Gen(); l.Linear(0, 0, 8, 0); fill = l; }
            fill.SetColorStops(new Fill.ColorStop[] { new(0, 255, 0, 0, 255), new(.35f, 0, 255, 0, 160), new(1, 0, 0, 255, 255) }, 3);
            fill.SetSpread(FillSpread.Repeat); shape.SetFill(fill);
            var pixels = new uint[128 * 128];
            using var canvas = SwCanvas.Gen(EngineOption.None);
            Assert.NotNull(canvas);
            canvas.Target(pixels, 128, 128, 128, ColorSpace.ABGR8888); canvas.Add(shape);
            if (update) { Assert.Equal(Result.Success, canvas.Draw(true)); canvas.Sync(); }
            shape.Scale(2);
            Assert.Equal(Result.Success, canvas.Update());
            Assert.Equal(Result.Success, canvas.Draw(true)); canvas.Sync(); canvas.Remove();
            return pixels;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RepeatedGradient_TransformUpdateMatchesFreshPreparation(bool radial)
        {
            Initializer.Init();
            try { Assert.Equal(Render(radial, false), Render(radial, true)); }
            finally { Initializer.Term(); }
        }
    }
}
