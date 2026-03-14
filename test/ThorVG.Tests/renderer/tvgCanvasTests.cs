using Xunit;

namespace ThorVG.Tests
{
    public class tvgCanvasTests
    {
        [Fact]
        public void SwCanvas_Gen_ReturnsInstance()
        {
            var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);
        }

        [Fact]
        public void GlCanvas_Gen_ReturnsNullWithoutGlContext()
        {
            // Without a real OpenGL context, GlCanvas.Gen() returns null
            var canvas = GlCanvas.Gen();
            Assert.Null(canvas);
        }

        [Fact]
        public void Canvas_Add_Shape()
        {
            var canvas = SwCanvas.Gen();
            var shape = Shape.Gen();
            Assert.Equal(Result.Success, canvas.Add(shape));
            Assert.Single(canvas.GetPaints());
        }

        [Fact]
        public void Canvas_Remove_Shape()
        {
            var canvas = SwCanvas.Gen();
            var shape = Shape.Gen();
            canvas.Add(shape);
            Assert.Equal(Result.Success, canvas.Remove(shape));
            Assert.Empty(canvas.GetPaints());
        }

        [Fact]
        public void Canvas_Remove_All()
        {
            var canvas = SwCanvas.Gen();
            canvas.Add(Shape.Gen());
            canvas.Add(Shape.Gen());
            Assert.Equal(Result.Success, canvas.Remove(null));
            Assert.Empty(canvas.GetPaints());
        }

        [Fact]
        public void Canvas_Update()
        {
            var canvas = SwCanvas.Gen();
            var buffer = new uint[800 * 600];
            canvas.Target(buffer, 800, 800, 600, ColorSpace.ARGB8888);
            canvas.Add(Shape.Gen());
            Assert.Equal(Result.Success, canvas.Update());
        }

        [Fact]
        public void Canvas_Update_WithoutTarget_Fails()
        {
            var canvas = SwCanvas.Gen();
            canvas.Add(Shape.Gen());
            // Without a render target, Update should fail
            Assert.Equal(Result.InsufficientCondition, canvas.Update());
        }

        [Fact]
        public void Canvas_Draw()
        {
            var canvas = SwCanvas.Gen();
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
            var canvas = SwCanvas.Gen();
            canvas.Add(Shape.Gen());
            // Without a render target, Draw should fail
            Assert.Equal(Result.InsufficientCondition, canvas.Draw());
        }

        [Fact]
        public void Canvas_Sync()
        {
            var canvas = SwCanvas.Gen();
            Assert.Equal(Result.Success, canvas.Sync());
        }

        [Fact]
        public void Canvas_Viewport()
        {
            var canvas = SwCanvas.Gen();
            // Initially Synced, so viewport should be settable
            Assert.Equal(Result.Success, canvas.Viewport(0, 0, 800, 600));
        }

        [Fact]
        public void Canvas_Viewport_DuringPainting_Fails()
        {
            var canvas = SwCanvas.Gen();
            canvas.Add(Shape.Gen()); // transitions to Painting
            Assert.Equal(Result.InsufficientCondition, canvas.Viewport(0, 0, 800, 600));
        }

        [Fact]
        public void SwCanvas_Target()
        {
            var canvas = SwCanvas.Gen();
            var buffer = new uint[800 * 600];
            Assert.Equal(Result.Success, canvas.Target(buffer, 800, 800, 600, ColorSpace.ARGB8888));
        }

        [Fact]
        public void GlCanvas_Target_RequiresGlContext()
        {
            // Without a real OpenGL context, GlCanvas.Gen() returns null
            var canvas = GlCanvas.Gen();
            Assert.Null(canvas);
        }
    }
}
