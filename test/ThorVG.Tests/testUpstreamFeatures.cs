using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace ThorVG.Tests
{
    public class testUpstreamFeatures
    {
        private static readonly string TEST_DIR = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ref", "ThorVG", "test", "resources"));

        [Fact]
        public void DynamicTweenCapturesAndRebasesValues()
        {
            var property = new LottieFloat();
            var tween = new LottieTween();

            tween.On(20.0f);
            Assert.True(tween.active);
            Assert.False(tween.legacy);
            Assert.False(tween.Inited(property, 5.0f));
            tween.Capture(property, 5.0f, 10.0f);
            tween.progress = 0.5f;
            Assert.Equal(20.0f, tween.Run(property, 5.0f, 30.0f));

            tween.On(40.0f);
            Assert.False(tween.Inited(property, 5.0f));
            tween.Capture(property, 5.0f, 999.0f);
            tween.progress = 0.5f;
            Assert.Equal(30.0f, tween.Run(property, 5.0f, 40.0f));

            tween.Off();
            Assert.False(tween.active);
        }

        [Fact]
        public void DynamicTween_FirstLookupDoesNotInitializeBeforeChainCapture()
        {
            var property = new LottieFloat();
            var tween = new LottieTween();

            tween.On(20.0f);
            Assert.False(tween.Inited(property, 5.0f));
            tween.Capture(property, 5.0f, 10.0f);
            tween.progress = 0.5f;
            Assert.Equal(20.0f, tween.Run(property, 5.0f, 30.0f));

            Assert.False(tween.Inited(property, 5.0f));
            tween.Capture(property, 5.0f, 999.0f);
            Assert.Equal(30.0f, tween.Run(property, 5.0f, 40.0f));
            Assert.True(tween.Inited(property, 5.0f));
        }

        [Fact]
        public unsafe void PuckerBloatModifiesPaths()
        {
            var source = new RenderPath();
            source.AddRect(0, 0, 100, 100, 0, 0, true);
            var puckered = new RenderPath();
            new LottiePuckerBloatModifier(50).Path(source, puckered, null);
            Assert.Equal(PathCommand.MoveTo, puckered.cmds[0]);
            Assert.Equal(PathCommand.CubicTo, puckered.cmds[1]);
            Assert.Equal(75.0f, puckered.pts[0].x);
            Assert.Equal(25.0f, puckered.pts[0].y);
        }

        [Fact]
        public void ZigZagResourceParsesAnimatedParameters()
        {
            var path = Path.Combine(TEST_DIR, "test14.lot");
            Assert.True(File.Exists(path), $"Required test resource is missing: {path}");
            var parser = new LottieParser(File.ReadAllText(path), TEST_DIR, false);
            Assert.True(parser.Parse());

            LottieZigZag? zigzag = null;
            void Visit(LottieObject obj)
            {
                if (obj is LottieZigZag value) zigzag = value;
                if (obj is LottieGroup group)
                    foreach (var child in group.children) Visit(child);
            }
            Visit(parser.comp!.root!);

            Assert.NotNull(zigzag);
            var tween = new LottieTween();
            Assert.Equal(10.0f, zigzag!.amplitude.Evaluate(0, tween, null));
            Assert.Equal(40.0f, zigzag.amplitude.Evaluate(60, tween, null));
            Assert.Equal(4, zigzag.frequency.Evaluate(0, tween, null));
            Assert.Equal(24, zigzag.frequency.Evaluate(60, tween, null));
            Assert.Equal((int)LottieZigZagModifier.PointType.Corner, zigzag.point.Evaluate(0, tween, null));
        }

        [Fact]
        public void NewLottieModifierResourcesParseAndAnimate()
        {
            foreach (var name in new[] { "test13.lot", "test14.lot" })
            {
                var path = Path.Combine(TEST_DIR, name);
                Assert.True(File.Exists(path), $"Required test resource is missing: {path}");
                var loader = new LottieLoader();
                Assert.True(loader.Open(path));
                Assert.True(loader.Read());
                Assert.True(loader.Frame(loader.TotalFrame() * 0.5f));
                Assert.NotNull(loader.GetPaint());
            }
        }

        [Fact]
        public void SvgPatternsRenderDistinctTiles()
        {
            Assert.Equal(Result.Success, Initializer.Init());
            try
            {
                var path = Path.Combine(TEST_DIR, "test4.svg");
                Assert.True(File.Exists(path), $"Required test resource is missing: {path}");

                var picture = Picture.Gen();
                Assert.Equal(Result.Success, picture.Load(path));
                Assert.Equal(Result.Success, picture.SetSize(400, 400));

                var buffer = new uint[400 * 400];
                var canvas = SwCanvas.Gen();
                Assert.Equal(Result.Success, canvas.Target(buffer, 400, 400, 400, ColorSpace.ARGB8888));
                Assert.Equal(Result.Success, canvas.Add(picture));
                Assert.Equal(Result.Success, canvas.Draw());
                Assert.Equal(Result.Success, canvas.Sync());

                var colors = new HashSet<uint>(buffer);
                colors.Remove(0);
                Assert.True(colors.Count >= 4, $"Expected patterned SVG output, found {colors.Count} non-transparent colors.");
            }
            finally
            {
                Assert.Equal(Result.Success, Initializer.Term());
            }
        }

        [Fact]
        public void SoftwareMaskAndBlendAffectRenderedPixels()
        {
            Assert.Equal(Result.Success, Initializer.Init());
            try
            {
                var buffer = new uint[20 * 20];
                var canvas = SwCanvas.Gen();
                Assert.Equal(Result.Success, canvas.Target(buffer, 20, 20, 20, ColorSpace.ARGB8888));

                var background = Shape.Gen();
                background.AppendRect(0, 0, 20, 20);
                background.SetFill(255, 0, 0);
                canvas.Add(background);

                var foreground = Shape.Gen();
                foreground.AppendRect(5, 0, 15, 20);
                foreground.SetFill(0, 255, 0);
                foreground.SetBlend(BlendMethod.Multiply);
                var mask = Shape.Gen();
                mask.AppendRect(5, 0, 8, 20);
                mask.SetFill(255, 255, 255);
                foreground.SetMask(mask, MaskMethod.Alpha);
                canvas.Add(foreground);

                Assert.Equal(Result.Success, canvas.Draw());
                Assert.Equal(Result.Success, canvas.Sync());

                var redOnly = buffer[2 * 20 + 2];
                var blended = buffer[2 * 20 + 7];
                var maskedOut = buffer[2 * 20 + 17];
                Assert.NotEqual(redOnly, blended);
                Assert.Equal(redOnly, maskedOut);
            }
            finally
            {
                Assert.Equal(Result.Success, Initializer.Term());
            }
        }
    }
}
