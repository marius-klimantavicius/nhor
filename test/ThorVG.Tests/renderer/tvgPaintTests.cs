using Xunit;

namespace ThorVG.Tests
{
    public class tvgPaintTests
    {
        // Use Shape as the concrete Paint for testing Paint API.

        [Fact]
        public void Paint_DefaultOpacity_Is255()
        {
            var shape = Shape.Gen();
            Assert.Equal(255, shape.Opacity());
        }

        [Fact]
        public void Paint_SetOpacity()
        {
            var shape = Shape.Gen();
            Assert.Equal(Result.Success, shape.Opacity(128));
            Assert.Equal(128, shape.Opacity());
        }

        [Fact]
        public void Paint_Translate()
        {
            var shape = Shape.Gen();
            Assert.Equal(Result.Success, shape.Translate(10.0f, 20.0f));
        }

        [Fact]
        public void Paint_Rotate()
        {
            var shape = Shape.Gen();
            Assert.Equal(Result.Success, shape.Rotate(45.0f));
        }

        [Fact]
        public void Paint_Scale()
        {
            var shape = Shape.Gen();
            Assert.Equal(Result.Success, shape.Scale(2.0f));
        }

        [Fact]
        public void Paint_Transform_SetAndGet()
        {
            var shape = Shape.Gen();
            var m = new Matrix(2, 0, 0, 0, 2, 0, 0, 0, 1);
            Assert.Equal(Result.Success, shape.Transform(m));
            ref var got = ref shape.Transform();
            Assert.Equal(2.0f, got.e11);
            Assert.Equal(2.0f, got.e22);
        }

        [Fact]
        public void Paint_Visibility()
        {
            var shape = Shape.Gen();
            Assert.True(shape.IsVisible()); // default: hidden=false => IsVisible=true

            // Visible(false) => SetVisible(!false) => SetVisible(true) => hidden=true
            shape.Visible(false);
            Assert.False(shape.IsVisible());

            // Visible(true) => SetVisible(!true) => SetVisible(false) => hidden=false
            shape.Visible(true);
            Assert.True(shape.IsVisible());
        }

        [Fact]
        public void Paint_RefCounting()
        {
            var shape = Shape.Gen();
            Assert.Equal(0, shape.RefCnt());
            shape.Ref();
            Assert.Equal(1, shape.RefCnt());
            shape.Ref();
            Assert.Equal(2, shape.RefCnt());
            shape.Unref(false);
            Assert.Equal(1, shape.RefCnt());
        }

        [Fact]
        public void Paint_Blend_SetValid()
        {
            var shape = Shape.Gen();
            Assert.Equal(Result.Success, shape.SetBlend(BlendMethod.Normal));
            Assert.Equal(Result.Success, shape.SetBlend(BlendMethod.Add));
        }

        [Fact]
        public void Paint_Blend_SetInvalid()
        {
            var shape = Shape.Gen();
            // Values between Add and Composition are invalid
            Assert.Equal(Result.InvalidArguments, shape.SetBlend((BlendMethod)100));
        }

        [Fact]
        public void Paint_Mask_SetAndGet()
        {
            var shape = Shape.Gen();
            var target = Shape.Gen();
            Assert.Equal(Result.Success, shape.SetMask(target, MaskMethod.Alpha));
            var method = shape.GetMask(out var got);
            Assert.Equal(MaskMethod.Alpha, method);
            Assert.Same(target, got);
        }

        [Fact]
        public void Paint_Mask_ClearWithNone()
        {
            var shape = Shape.Gen();
            var target = Shape.Gen();
            shape.SetMask(target, MaskMethod.Alpha);
            Assert.Equal(Result.Success, shape.SetMask(null, MaskMethod.None));
            var method = shape.GetMask(out var got);
            Assert.Equal(MaskMethod.None, method);
            Assert.Null(got);
        }

        [Fact]
        public void Paint_Clip_SetAndGet()
        {
            var shape = Shape.Gen();
            var clipper = Shape.Gen();
            Assert.Equal(Result.Success, shape.Clip(clipper));
            Assert.Same(clipper, shape.GetClipper());
        }

        [Fact]
        public void Paint_Clip_SetNull_ClearsClipper()
        {
            var shape = Shape.Gen();
            var clipper = Shape.Gen();
            shape.Clip(clipper);
            shape.Clip(null);
            Assert.Null(shape.GetClipper());
        }

        [Fact]
        public void Paint_Duplicate_CopiesProperties()
        {
            var shape = Shape.Gen();
            shape.Opacity(100);
            shape.SetBlend(BlendMethod.Multiply);

            var dup = shape.Duplicate();
            Assert.NotSame(shape, dup);
            Assert.Equal(100, dup.Opacity());
        }

        [Fact]
        public void Paint_Parent_InitiallyNull()
        {
            var shape = Shape.Gen();
            Assert.Null(shape.Parent());
        }
    }
}
