using System;
using Xunit;

namespace ThorVG.Tests
{
    public class testAccessor
    {
        private static readonly string TEST_DIR = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ThorVG", "test", "resources"));

        [Fact]
        public void AccessorCreation()
        {
            var accessor = Accessor.Gen();
            Assert.NotNull(accessor);

            var accessor2 = Accessor.Gen();
            Assert.NotNull(accessor2);
        }

        [Fact]
        public void Set()
        {
            Assert.Equal(Result.Success, Initializer.Init());

            var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);

            var buffer = new uint[100 * 100];
            Assert.Equal(Result.Success, canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888));

            var picture = Picture.Gen();
            Assert.NotNull(picture);

            var svgPath = System.IO.Path.Combine(TEST_DIR, "test0.svg");
            if (!System.IO.File.Exists(svgPath))
            {
                Initializer.Term();
                return;
            }

            Assert.Equal(Result.Success, picture.Load(svgPath));

            var accessor = Accessor.Gen();
            Assert.NotNull(accessor);

            // Case 1: null callback
            Assert.Equal(Result.InvalidArguments, accessor.Set(picture, null!));

            // Case 2: find white shapes and change their color
            Shape? ret = null;

            Func<Paint, object?, bool> f = (paint, data) =>
            {
                if (paint.PaintType() == Type.Shape)
                {
                    var shape = (Shape)paint;
                    shape.GetFillColor(out var r, out var g, out var b, out _);
                    if (r == 255 && g == 255 && b == 255)
                    {
                        shape.SetFill(0, 0, 255);
                        shape.id = Accessor.Id("TestAccessor");
                        ret = shape;
                        return false;
                    }
                }
                return true;
            };

            Assert.Equal(Result.Success, accessor.Set(picture, f));
            Assert.NotNull(ret);
            Assert.Equal(Accessor.Id("TestAccessor"), ret!.id);

            Paint.Rel(picture);

            Assert.Equal(Result.Success, Initializer.Term());
        }
    }
}
