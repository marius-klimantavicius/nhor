using Xunit;

namespace ThorVG.Tests
{
    public class tvgCanvasTests : System.IDisposable
    {
        public tvgCanvasTests() { Assert.Equal(Result.Success, Initializer.Init()); }
        public void Dispose() { Assert.Equal(Result.Success, Initializer.Term()); }

        [Fact]
        public void SwCanvas_Gen_ReturnsInstance()
        {
            using var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);
        }

        [Fact]
        public void GlCanvas_Gen_ReturnsNullWithoutGlContext()
        {
            // Without a real OpenGL context, GlCanvas.Gen() returns null
            using var canvas = GlCanvas.Gen();
            Assert.Null(canvas);
        }

        [Fact]
        public void Canvas_Add_Shape()
        {
            using var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);
            var shape = Shape.Gen();
            Assert.Equal(Result.Success, canvas.Add(shape));
            Assert.Single(canvas.GetPaints());
        }

        [Fact]
        public void Canvas_Remove_Shape()
        {
            using var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);
            var shape = Shape.Gen();
            canvas.Add(shape);
            Assert.Equal(Result.Success, canvas.Remove(shape));
            Assert.Empty(canvas.GetPaints());
        }

        [Fact]
        public void Canvas_Remove_All()
        {
            using var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);
            canvas.Add(Shape.Gen());
            canvas.Add(Shape.Gen());
            Assert.Equal(Result.Success, canvas.Remove(null));
            Assert.Empty(canvas.GetPaints());
        }

        [Fact]
        public void Canvas_Update()
        {
            using var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);
            var buffer = new uint[800 * 600];
            canvas.Target(buffer, 800, 800, 600, ColorSpace.ARGB8888);
            canvas.Add(Shape.Gen());
            Assert.Equal(Result.Success, canvas.Update());
        }

        [Fact]
        public void Canvas_Update_WithoutTarget_Fails()
        {
            using var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);
            canvas.Add(Shape.Gen());
            // Without a render target, Update should fail
            Assert.Equal(Result.InsufficientCondition, canvas.Update());
        }

        [Fact]
        public void Canvas_Draw()
        {
            using var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);
            var buffer = new uint[800 * 600];
            canvas.Target(buffer, 800, 800, 600, ColorSpace.ARGB8888);
            canvas.Add(Shape.Gen());
            var result = canvas.Draw();
            // Should go through Painting -> Update -> Drawing
            Assert.Equal(Result.Success, result);
        }

        [Fact]
        public void Canvas_Draw_WithoutTarget_Fails()
        {
            using var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);
            canvas.Add(Shape.Gen());
            // Without a render target, Draw should fail
            Assert.Equal(Result.InsufficientCondition, canvas.Draw());
        }

        [Fact]
        public void Canvas_Sync()
        {
            using var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);
            Assert.Equal(Result.Success, canvas.Sync());
        }

        [Fact]
        public void Canvas_Viewport()
        {
            using var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);
            // Initially Synced, so viewport should be settable
            Assert.Equal(Result.Success, canvas.Viewport(0, 0, 800, 600));
        }

        [Fact]
        public void Canvas_Viewport_DuringPainting_Fails()
        {
            using var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);
            canvas.Add(Shape.Gen()); // transitions to Painting
            Assert.Equal(Result.InsufficientCondition, canvas.Viewport(0, 0, 800, 600));
        }

        [Fact]
        public void SwCanvas_Target()
        {
            using var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);
            var buffer = new uint[800 * 600];
            Assert.Equal(Result.Success, canvas.Target(buffer, 800, 800, 600, ColorSpace.ARGB8888));
        }

        [Fact]
        public void GlCanvas_Target_RequiresGlContext()
        {
            // Without a real OpenGL context, GlCanvas.Gen() returns null
            using var canvas = GlCanvas.Gen();
            Assert.Null(canvas);
        }
    }
}
