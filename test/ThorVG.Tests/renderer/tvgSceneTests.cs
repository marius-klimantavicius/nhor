using Xunit;

namespace ThorVG.Tests
{
    public class tvgSceneTests
    {
        [Fact]
        public void Scene_Gen_ReturnsInstance()
        {
            var scene = Scene.Gen();
            Assert.NotNull(scene);
            Assert.Equal(Type.Scene, scene.PaintType());
        }

        [Fact]
        public void Scene_Add_Success()
        {
            var scene = Scene.Gen();
            var shape = Shape.Gen();
            Assert.Equal(Result.Success, scene.Add(shape));
            Assert.Single(scene.Paints());
        }

        [Fact]
        public void Scene_Add_NullPaint_InvalidArgs()
        {
            var scene = Scene.Gen();
            Assert.Equal(Result.InvalidArguments, scene.Add(null!));
        }

        [Fact]
        public void Scene_Add_Multiple()
        {
            var scene = Scene.Gen();
            scene.Add(Shape.Gen());
            scene.Add(Shape.Gen());
            scene.Add(Shape.Gen());
            Assert.Equal(3, scene.Paints().Count);
        }

        [Fact]
        public void Scene_Add_SetsParent()
        {
            var scene = Scene.Gen();
            var shape = Shape.Gen();
            scene.Add(shape);
            Assert.Same(scene, shape.Parent());
        }

        [Fact]
        public void Scene_Add_AlreadyParented_InsufficientCondition()
        {
            var scene1 = Scene.Gen();
            var scene2 = Scene.Gen();
            var shape = Shape.Gen();
            scene1.Add(shape);
            Assert.Equal(Result.InsufficientCondition, scene2.Add(shape));
        }

        [Fact]
        public void Scene_Remove_Specific()
        {
            var scene = Scene.Gen();
            var shape = Shape.Gen();
            scene.Add(shape);
            Assert.Equal(Result.Success, scene.Remove(shape));
            Assert.Empty(scene.Paints());
        }

        [Fact]
        public void Scene_Remove_All()
        {
            var scene = Scene.Gen();
            scene.Add(Shape.Gen());
            scene.Add(Shape.Gen());
            Assert.Equal(Result.Success, scene.Remove(null));
            Assert.Empty(scene.Paints());
        }

        [Fact]
        public void Scene_Remove_WrongParent_InsufficientCondition()
        {
            var scene1 = Scene.Gen();
            var scene2 = Scene.Gen();
            var shape = Shape.Gen();
            scene1.Add(shape);
            Assert.Equal(Result.InsufficientCondition, scene2.Remove(shape));
        }

        [Fact]
        public void Scene_Add_AtPosition()
        {
            var scene = Scene.Gen();
            var shape1 = Shape.Gen();
            var shape2 = Shape.Gen();
            var shape3 = Shape.Gen();
            scene.Add(shape1);
            scene.Add(shape2);
            scene.Add(shape3, shape2); // Insert before shape2
            Assert.Equal(3, scene.Paints().Count);
            Assert.Same(shape3, scene.Paints()[1]);
        }

        [Fact]
        public void Scene_Duplicate()
        {
            var scene = Scene.Gen();
            scene.Add(Shape.Gen());
            scene.Add(Shape.Gen());

            var dup = (Scene)scene.Duplicate();
            Assert.NotSame(scene, dup);
            Assert.Equal(2, dup.Paints().Count);
        }

        [Fact]
        public void Scene_AddEffect_Clear()
        {
            var scene = Scene.Gen();
            Assert.Equal(Result.Success, scene.AddEffect(SceneEffect.Clear));
        }

        [Fact]
        public void Scene_AddEffect_GaussianBlur()
        {
            var scene = Scene.Gen();
            Assert.Equal(Result.Success, scene.AddEffect(
                SceneEffect.GaussianBlur, 5.0f, 0, 0, 50));
        }

        [Fact]
        public void Scene_AddEffect_DropShadow()
        {
            var scene = Scene.Gen();
            Assert.Equal(Result.Success, scene.AddEffect(
                SceneEffect.DropShadow, 0, 0, 0, 128, 45.0f, 10.0f, 5.0f, 50));
        }

        [Fact]
        public void Scene_AddEffect_Fill()
        {
            var scene = Scene.Gen();
            Assert.Equal(Result.Success, scene.AddEffect(
                SceneEffect.Fill, 255, 0, 0, 255));
        }

        [Fact]
        public void Scene_AddEffect_Tint()
        {
            var scene = Scene.Gen();
            Assert.Equal(Result.Success, scene.AddEffect(
                SceneEffect.Tint, 0, 0, 0, 255, 255, 255, 50.0));
        }

        [Fact]
        public void Scene_AddEffect_Tritone()
        {
            var scene = Scene.Gen();
            Assert.Equal(Result.Success, scene.AddEffect(
                SceneEffect.Tritone, 0, 0, 0, 128, 128, 128, 255, 255, 255, 0));
        }

        [Fact]
        public void SceneIterator_Traverses()
        {
            var scene = Scene.Gen();
            var s1 = Shape.Gen();
            var s2 = Shape.Gen();
            scene.Add(s1);
            scene.Add(s2);

            var it = IteratorAccessor.GetIterator(scene);
            Assert.NotNull(it);
            Assert.Equal(2u, it!.Count());
            Assert.Same(s1, it.Next());
            Assert.Same(s2, it.Next());
            Assert.Null(it.Next());
        }
    }
}
