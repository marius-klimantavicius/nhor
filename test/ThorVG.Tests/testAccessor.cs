using System;
using System.IO;
using Xunit;

namespace ThorVG.Tests
{
    public class testAccessor
    {
        private static readonly string TEST_DIR = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ref", "ThorVG", "test", "resources"));

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
            picture.accessible = true;

            var svgPath = Path.Combine(TEST_DIR, "test0.svg");
            Assert.True(File.Exists(svgPath), $"Required test resource is missing: {svgPath}");

            Assert.Equal(Result.Success, picture.Load(svgPath));

            var accessor = Accessor.Gen();
            Assert.NotNull(accessor);

            // Case 1: null callback
            Assert.Equal(Result.InvalidArguments, accessor.Set(picture, null!));

            Shape? found = null;
            Func<Paint, object?, bool> f = (paint, data) =>
            {
                var currentAccessor = Assert.IsType<Accessor>(data);
                if (currentAccessor.Name(paint.id) == "path42")
                {
                    var shape = Assert.IsType<Shape>(paint);
                    Assert.Equal(Result.Success, shape.SetFill(0, 0, 255));
                    found = shape;
                    return false;
                }
                return true;
            };

            Assert.Equal(Result.Success, accessor.Set(picture, f, accessor));
            Assert.NotNull(found);
            Assert.Same(found, picture.FindPaint(Accessor.Id("path42")));

            Paint.Rel(picture);

            Assert.Equal(Result.Success, Initializer.Term());
        }
    }
}
