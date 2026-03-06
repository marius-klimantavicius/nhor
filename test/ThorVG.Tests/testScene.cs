// Ported from ThorVG/test/testScene.cpp

using System;
using System.IO;
using Xunit;

namespace ThorVG.Tests
{
    public class testScene
    {
        private static readonly string TEST_DIR = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ThorVG", "test", "resources"));

        [Fact]
        public void SceneCreation()
        {
            var scene = Scene.Gen();
            Assert.NotNull(scene);

            Assert.Equal(Type.Scene, scene.PaintType());

            Paint.Rel(scene);
        }

        [Fact]
        public void PushingPaintsIntoScene()
        {
            var scene = Scene.Gen();
            Assert.NotNull(scene);
            Assert.Null(scene.Parent());

            var paints = new Paint[3];

            // Pushing Paints
            paints[0] = Shape.Gen();
            paints[0].Ref();
            Assert.Null(paints[0].Parent());
            Assert.Equal(Result.Success, scene.Add(paints[0]));
            Assert.Same(scene, paints[0].Parent());

            paints[1] = Picture.Gen();
            paints[1].Ref();
            Assert.Null(paints[1].Parent());
            Assert.Equal(Result.Success, scene.Add(paints[1]));
            Assert.Same(scene, paints[1].Parent());

            paints[2] = Picture.Gen();
            paints[2].Ref();
            Assert.Null(paints[2].Parent());
            Assert.Equal(Result.Success, scene.Add(paints[2]));
            Assert.Same(scene, paints[2].Parent());

            // Pushing Null Pointer
            Assert.Equal(Result.InvalidArguments, scene.Add(null!));

            // Pushing Invalid Paint
            Assert.Equal(Result.InvalidArguments, scene.Add(null!));

            Assert.Equal(Result.Success, scene.Remove(paints[0]));
            Assert.Equal(Result.InsufficientCondition, scene.Remove(paints[0]));
            Assert.Equal(Result.Success, scene.Add(paints[0], paints[1]));
            Assert.Equal(Result.Success, scene.Remove(paints[1]));
            Assert.Equal(Result.InsufficientCondition, scene.Remove(paints[1]));
            Assert.Equal(Result.Success, scene.Remove(paints[2]));
            Assert.Equal(Result.Success, scene.Remove(paints[0]));

            paints[0].Unref();
            paints[1].Unref();
            paints[2].Unref();

            Paint.Rel(scene);
        }

        [Fact]
        public void SceneClear()
        {
            var scene = Scene.Gen();
            Assert.NotNull(scene);

            Assert.Equal(Result.Success, scene.Add(Shape.Gen()));
            Assert.Equal(Result.Success, scene.Remove());

            Paint.Rel(scene);
        }

        [Fact]
        public void SceneClearAndReuseShape()
        {
            Assert.Equal(Result.Success, Initializer.Init());
            {
                var canvas = SwCanvas.Gen();
                var buffer = new uint[100 * 100];
                canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);

                var scene = Scene.Gen();
                Assert.NotNull(scene);

                var shape = Shape.Gen();
                Assert.NotNull(shape);
                Assert.Equal((ushort)1, shape.Ref());

                Assert.Equal(Result.Success, scene.Add(shape));
                Assert.Equal(Result.Success, canvas.Add(scene));
                Assert.Equal(Result.Success, canvas.Update());

                // No deallocate shape.
                Assert.Equal(Result.Success, scene.Remove());

                // Reuse shape.
                Assert.Equal(Result.Success, scene.Add(shape));
                Assert.Equal((ushort)1, shape.Unref()); // The scene still holds 1.
            }
            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void SceneEffects()
        {
            Assert.Equal(Result.Success, Initializer.Init());
            {
                var canvas = SwCanvas.Gen();
                Assert.NotNull(canvas);

                var buffer = new uint[100 * 100];
                Assert.Equal(Result.Success, canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888));

                var shape = Shape.Gen();
                Assert.NotNull(shape);
                Assert.Equal(Result.Success, shape.AppendCircle(50, 50, 30, 30));
                Assert.Equal(Result.Success, shape.SetFill(0, 255, 0, 255));

                var scene = Scene.Gen();
                Assert.NotNull(scene);
                Assert.Equal(Result.Success, scene.Add(shape));

                var picture = Picture.Gen();
                picture.Load(TEST_DIR + "/tiger.svg");

                scene.Add(picture);
                Assert.Equal(Result.Success, canvas.Add(scene));

                Assert.Equal(Result.Success, scene.AddEffect(SceneEffect.Clear));
                Assert.Equal(Result.Success, scene.AddEffect(SceneEffect.GaussianBlur, 1.5f, 0, 0, 75));
                Assert.Equal(Result.Success, canvas.Update());
                Assert.Equal(Result.Success, canvas.Draw());
                Assert.Equal(Result.Success, canvas.Sync());

                Assert.Equal(Result.Success, scene.AddEffect(SceneEffect.Clear));
                Assert.Equal(Result.Success, scene.AddEffect(SceneEffect.DropShadow, 128, 128, 128, 200, 45.0f, 5.0f, 2.0f, 60));
                Assert.Equal(Result.Success, canvas.Update());
                Assert.Equal(Result.Success, canvas.Draw());
                Assert.Equal(Result.Success, canvas.Sync());

                Assert.Equal(Result.Success, scene.AddEffect(SceneEffect.Clear));
                Assert.Equal(Result.Success, scene.AddEffect(SceneEffect.Fill, 255, 0, 0, 128));
                Assert.Equal(Result.Success, canvas.Update());
                Assert.Equal(Result.Success, canvas.Draw());
                Assert.Equal(Result.Success, canvas.Sync());

                Assert.Equal(Result.Success, scene.AddEffect(SceneEffect.Clear));
                Assert.Equal(Result.Success, scene.AddEffect(SceneEffect.Tint, 0, 0, 0, 255, 255, 255, 50.0));
                Assert.Equal(Result.Success, canvas.Update());
                Assert.Equal(Result.Success, canvas.Draw());
                Assert.Equal(Result.Success, canvas.Sync());

                Assert.Equal(Result.Success, scene.AddEffect(SceneEffect.Clear));
                Assert.Equal(Result.Success, scene.AddEffect(SceneEffect.Tritone, 0, 0, 0, 128, 128, 128, 255, 255, 255, 128));
                Assert.Equal(Result.Success, canvas.Update());
                Assert.Equal(Result.Success, canvas.Draw());
                Assert.Equal(Result.Success, canvas.Sync());

                Assert.Equal(Result.Success, scene.AddEffect(SceneEffect.GaussianBlur, 1.5f, 0, 0, 75));
                Assert.Equal(Result.Success, scene.AddEffect(SceneEffect.DropShadow, 128, 128, 128, 200, 45.0f, 5.0f, 2.0f, 60));

                Assert.Equal(Result.Success, canvas.Add(scene.Duplicate()));
                Assert.Equal(Result.Success, canvas.Update());
                Assert.Equal(Result.Success, canvas.Draw());
                Assert.Equal(Result.Success, canvas.Sync());
            }
            Assert.Equal(Result.Success, Initializer.Term());
        }
    }
}
