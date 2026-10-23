using Xunit;

namespace ThorVG.Tests
{
    public class tvgUpdateOwnershipTests
    {
        [Fact]
        public void SceneAttach_DetachPropagatesThroughNestedClipsAndMasks()
        {
            var shape = Shape.Gen();
            var clip = Shape.Gen();
            var innerClip = Shape.Gen();
            var mask = Shape.Gen();
            var maskClip = Shape.Gen();
            shape.Ref(); // keep the paint tree alive when removed from the scene
            Assert.Equal(Result.Success, clip.Clip(innerClip));
            Assert.Equal(Result.Success, mask.Clip(maskClip));
            Assert.Equal(Result.Success, shape.Clip(clip));
            Assert.Equal(Result.Success, shape.SetMask(mask, MaskMethod.Alpha));
            var scene = Scene.Gen();
            Assert.Equal(Result.Success, scene.Add(shape));
            foreach (var paint in new Paint[] { shape, clip, innerClip, mask, maskClip }) Assert.Same(scene, paint.Parent());
            Assert.Equal(Result.Success, scene.Remove(shape));
            foreach (var paint in new Paint[] { shape, clip, innerClip, mask, maskClip }) Assert.Null(paint.Parent());
            shape.Unref();
            Paint.Rel(scene);
        }
    }
}
