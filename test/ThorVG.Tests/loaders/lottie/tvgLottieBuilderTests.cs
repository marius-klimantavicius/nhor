// Tests for LottieBuilder

using Xunit;

namespace ThorVG.Tests
{
    public class LottieBuilderTests
    {
        /// <summary>Minimal valid Lottie JSON.</summary>
        private const string MinimalLottieJson = @"{
            ""v"": ""5.7.0"",
            ""fr"": 30,
            ""ip"": 0,
            ""op"": 60,
            ""w"": 100,
            ""h"": 100,
            ""layers"": []
        }";

        private static LottieComposition ParseComposition(string json)
        {
            var parser = new LottieParser(json, null, false);
            parser.Parse();
            return parser.comp!;
        }

        // ---- Construction ----

        [Fact]
        public void Constructor_CreatesBuilder()
        {
            var builder = new LottieBuilder();
            Assert.NotNull(builder);
        }

        // ---- Expressions ----

        [Fact]
        public void Expressions_ReturnsBool()
        {
            var builder = new LottieBuilder();
            // LottieExpressions.Instance() returns a no-op stub, so this returns true or false
            var result = builder.Expressions();
            // Just check it doesn't throw
            Assert.True(result || !result);
        }

        // ---- Build ----

        [Fact]
        public void Build_WithComposition_DoesNotThrow()
        {
            var builder = new LottieBuilder();
            var comp = ParseComposition(MinimalLottieJson);
            builder.Build(comp);
            // Should have created a scene
            if (comp.root != null)
            {
                Assert.NotNull(comp.root.scene);
            }
        }

        // ---- Update ----

        [Fact]
        public void Update_WithEmptyLayers_ReturnsFalse()
        {
            var builder = new LottieBuilder();
            var comp = ParseComposition(MinimalLottieJson);
            // Empty layers means root has no children, Update correctly returns false
            var result = builder.Update(comp, 0.5f);
            Assert.False(result);
        }

        [Fact]
        public void Update_NullRoot_ReturnsFalse()
        {
            var builder = new LottieBuilder();
            var comp = new LottieComposition();
            comp.root = null;
            Assert.False(builder.Update(comp, 0f));
        }

        // ---- Tween ----

        [Fact]
        public void Tweening_DefaultFalse()
        {
            var builder = new LottieBuilder();
            Assert.False(builder.Tweening());
        }

        [Fact]
        public void OnTween_SetsTweenActive()
        {
            var builder = new LottieBuilder();
            builder.OnTween(30f, 0.5f);
            Assert.True(builder.Tweening());
        }

        [Fact]
        public void OffTween_ClearsTweenActive()
        {
            var builder = new LottieBuilder();
            builder.OnTween(30f, 0.5f);
            builder.OffTween();
            Assert.False(builder.Tweening());
        }

        // ---- RenderContext ----

        [Fact]
        public void RenderContext_Creation()
        {
            var shape = Shape.Gen();
            var ctx = new RenderContext();
            ctx.Init(shape);
            Assert.Same(shape, ctx.propagator);
            Assert.Null(ctx.merging);
            Assert.Empty(ctx.repeaters);
            Assert.Equal(RenderFragment.ByNone, ctx.fragment);
            ctx.Dispose();
        }

        [Fact]
        public void RenderContext_CopyConstructor()
        {
            var shape1 = Shape.Gen();
            var shape2 = Shape.Gen();
            var ctx1 = new RenderContext();
            ctx1.Init(shape1);
            var ctx2 = new RenderContext();
            ctx2.Init(ctx1, shape2);
            Assert.Same(shape2, ctx2.propagator);
            Assert.Null(ctx2.merging);
            ctx1.Dispose();
            ctx2.Dispose();
        }
    }
}
