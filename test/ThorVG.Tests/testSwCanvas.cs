using System;
using Xunit;

namespace ThorVG.Tests
{
    public class testSwCanvas
    {
        private static readonly string TEST_DIR = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ThorVG", "test", "resources"));

        [Fact(Skip = "C# SwCanvas.Gen() always succeeds without init check")]
        public void MissingInitialization()
        {
            var canvas = SwCanvas.Gen();
            Assert.Null(canvas);
        }

        [Fact]
        public void BasicCreation()
        {
            Assert.Equal(Result.Success, Initializer.Init());

            var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);

            var canvas2 = SwCanvas.Gen();
            Assert.NotNull(canvas2);

            var canvas3 = SwCanvas.Gen();
            Assert.NotNull(canvas3);

            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void TargetBuffer()
        {
            Assert.Equal(Result.Success, Initializer.Init());

            var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);

            var buffer = new uint[100 * 100];
            Assert.Equal(Result.Success, canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888));
            Assert.Equal(Result.Success, canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888));

            Assert.Equal(Result.InvalidArguments, canvas.Target((uint[])null!, 100, 100, 100, ColorSpace.ARGB8888));
            Assert.Equal(Result.InvalidArguments, canvas.Target(buffer, 0, 100, 100, ColorSpace.ARGB8888));
            Assert.Equal(Result.InvalidArguments, canvas.Target(buffer, 100, 0, 100, ColorSpace.ARGB8888));
            Assert.Equal(Result.InvalidArguments, canvas.Target(buffer, 100, 200, 100, ColorSpace.ARGB8888));
            Assert.Equal(Result.InvalidArguments, canvas.Target(buffer, 100, 100, 0, ColorSpace.ARGB8888));

            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void PushingPaints()
        {
            Assert.Equal(Result.Success, Initializer.Init());

            var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);

            var buffer = new uint[100 * 100];
            Assert.Equal(Result.Success, canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888));

            // Try all types of paints.
            Assert.Equal(Result.Success, canvas.Add(Shape.Gen()));
            Assert.Equal(Result.Success, canvas.Add(Picture.Gen()));
            Assert.Equal(Result.Success, canvas.Add(Scene.Gen()));

            // Cases by contexts.
            Assert.Equal(Result.Success, canvas.Update());

            Assert.Equal(Result.Success, canvas.Add(Shape.Gen()));
            Assert.Equal(Result.Success, canvas.Add(Shape.Gen()));

            Assert.Equal(Result.Success, canvas.Remove());

            var paints = new Paint[2];

            paints[0] = Shape.Gen();
            Assert.Equal(Result.Success, canvas.Add(paints[0]));

            // Negative case 1
            Assert.Equal(Result.InvalidArguments, canvas.Add(null!));

            paints[1] = Shape.Gen();
            Assert.Equal(Result.Success, canvas.Add(paints[1]));
            Assert.Equal(Result.Success, canvas.Draw());

            // Check list of paints
            var list = canvas.GetPaints();
            Assert.Equal(2, list.Count);
            Assert.Same(paints[0], list[0]);
            Assert.Same(paints[1], list[1]);

            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void Update()
        {
            Assert.Equal(Result.Success, Initializer.Init());

            var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);

            var buffer = new uint[100 * 100];
            Assert.Equal(Result.Success, canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888));

            Assert.Equal(Result.Success, canvas.Update());

            Assert.Equal(Result.Success, canvas.Add(Shape.Gen()));

            // No added shape
            var shape = Shape.Gen();

            // Normal case
            Assert.Equal(Result.Success, canvas.Add(shape));
            Assert.Equal(Result.Success, canvas.Update());
            Assert.Equal(Result.Success, canvas.Draw());
            Assert.Equal(Result.InsufficientCondition, canvas.Update());
            Assert.Equal(Result.Success, canvas.Sync());

            Assert.Equal(Result.Success, canvas.Update());

            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void SynchronizedDrawing()
        {
            Assert.Equal(Result.Success, Initializer.Init());

            var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);

            Assert.Equal(Result.Success, canvas.Sync());
            Assert.Equal(Result.InsufficientCondition, canvas.Draw());

            var buffer = new uint[100 * 100];
            Assert.Equal(Result.Success, canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888));

            Assert.Equal(Result.Success, canvas.Draw());
            Assert.Equal(Result.Success, canvas.Sync());

            // Invalid Shape
            var shape = Shape.Gen();
            Assert.NotNull(shape);
            Assert.Equal(Result.Success, canvas.Add(shape));

            Assert.Equal(Result.Success, canvas.Draw());
            Assert.Equal(Result.Success, canvas.Sync());

            var shape2 = Shape.Gen();
            Assert.NotNull(shape2);
            Assert.Equal(Result.Success, shape2.AppendRect(0, 0, 100, 100));
            Assert.Equal(Result.Success, shape2.SetFill(255, 255, 255, 255));

            Assert.Equal(Result.Success, canvas.Add(shape2));
            Assert.Equal(Result.Success, canvas.Draw());
            Assert.Equal(Result.Success, canvas.Sync());

            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void AsynchronousDrawing()
        {
            // Use multi-threading
            Assert.Equal(Result.Success, Initializer.Init(2));

            var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);

            var buffer = new uint[100 * 100];
            Assert.Equal(Result.Success, canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888));

            for (int i = 0; i < 3; ++i)
            {
                var shape = Shape.Gen();
                Assert.NotNull(shape);
                Assert.Equal(Result.Success, shape.AppendRect(0, 0, 100, 100));
                Assert.Equal(Result.Success, shape.SetFill(255, 255, 255, 255));
                Assert.Equal(Result.Success, canvas.Add(shape));
            }

            Assert.Equal(Result.Success, canvas.Draw());
            Assert.Equal(Result.Success, canvas.Sync());

            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void Viewport()
        {
            Assert.Equal(Result.Success, Initializer.Init());

            var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);

            Assert.Equal(Result.Success, canvas.Viewport(25, 25, 100, 100));

            var buffer = new uint[100 * 100];
            Assert.Equal(Result.Success, canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888));

            Assert.Equal(Result.Success, canvas.Viewport(25, 25, 50, 50));

            var shape = Shape.Gen();
            Assert.NotNull(shape);
            Assert.Equal(Result.Success, shape.AppendRect(0, 0, 100, 100));
            Assert.Equal(Result.Success, shape.SetFill(255, 255, 255, 255));

            Assert.Equal(Result.Success, canvas.Add(shape));

            // Negative, not allowed
            Assert.Equal(Result.InsufficientCondition, canvas.Viewport(15, 25, 5, 5));

            Assert.Equal(Result.Success, canvas.Draw());

            // Negative, not allowed
            Assert.Equal(Result.InsufficientCondition, canvas.Viewport(25, 25, 10, 10));

            Assert.Equal(Result.Success, canvas.Sync());

            Assert.Equal(Result.Success, Initializer.Term());
        }
    }
}
