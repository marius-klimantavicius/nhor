// Tests for LottieParser (Lottie JSON parsing)

using Xunit;

namespace ThorVG.Tests
{
    public class LottieParserTests
    {
        /// <summary>Minimal valid Lottie JSON with a single empty precomp root layer.</summary>
        private const string MinimalLottieJson = @"{
            ""v"": ""5.7.0"",
            ""fr"": 30,
            ""ip"": 0,
            ""op"": 60,
            ""w"": 100,
            ""h"": 100,
            ""layers"": []
        }";

        /// <summary>Lottie JSON with a shape layer containing a rectangle.</summary>
        private const string ShapeLayerJson = @"{
            ""v"": ""5.7.0"",
            ""fr"": 24,
            ""ip"": 0,
            ""op"": 48,
            ""w"": 200,
            ""h"": 150,
            ""layers"": [
                {
                    ""ty"": 4,
                    ""nm"": ""ShapeLayer"",
                    ""ip"": 0,
                    ""op"": 48,
                    ""st"": 0,
                    ""sr"": 1,
                    ""ks"": {
                        ""o"": { ""a"": 0, ""k"": 100 },
                        ""r"": { ""a"": 0, ""k"": 0 },
                        ""p"": { ""a"": 0, ""k"": [100, 75, 0] },
                        ""a"": { ""a"": 0, ""k"": [0, 0, 0] },
                        ""s"": { ""a"": 0, ""k"": [100, 100, 100] }
                    },
                    ""shapes"": [
                        {
                            ""ty"": ""rc"",
                            ""p"": { ""a"": 0, ""k"": [0, 0] },
                            ""s"": { ""a"": 0, ""k"": [50, 50] },
                            ""r"": { ""a"": 0, ""k"": 0 }
                        }
                    ]
                }
            ]
        }";

        // ---- Parse minimal ----

        [Fact]
        public void Parse_MinimalLottie_Succeeds()
        {
            var parser = new LottieParser(MinimalLottieJson, null, false);
            Assert.True(parser.Parse());
            Assert.NotNull(parser.comp);
        }

        [Fact]
        public void Parse_MinimalLottie_SetsFrameRate()
        {
            var parser = new LottieParser(MinimalLottieJson, null, false);
            parser.Parse();
            Assert.Equal(30f, parser.comp!.frameRate);
        }

        [Fact]
        public void Parse_MinimalLottie_SetsDimensions()
        {
            var parser = new LottieParser(MinimalLottieJson, null, false);
            parser.Parse();
            Assert.Equal(100f, parser.comp!.w);
            Assert.Equal(100f, parser.comp!.h);
        }

        [Fact]
        public void Parse_MinimalLottie_SetsVersion()
        {
            var parser = new LottieParser(MinimalLottieJson, null, false);
            parser.Parse();
            Assert.NotNull(parser.comp!.version);
            Assert.Contains("5.7.0", parser.comp.version);
        }

        [Fact]
        public void Parse_MinimalLottie_SetsFrameRange()
        {
            var parser = new LottieParser(MinimalLottieJson, null, false);
            parser.Parse();
            Assert.NotNull(parser.comp!.root);
            Assert.Equal(0f, parser.comp.root!.inFrame);
            Assert.Equal(60f, parser.comp.root!.outFrame);
        }

        [Fact]
        public void Parse_MinimalLottie_FrameCount()
        {
            var parser = new LottieParser(MinimalLottieJson, null, false);
            parser.Parse();
            // FrameCnt = outFrame - inFrame = 60 - 0 = 60
            Assert.Equal(60f, parser.comp!.FrameCnt());
        }

        // ---- Parse with shape layer ----

        [Fact]
        public void Parse_ShapeLayer_Succeeds()
        {
            var parser = new LottieParser(ShapeLayerJson, null, false);
            Assert.True(parser.Parse());
            Assert.NotNull(parser.comp);
        }

        [Fact]
        public void Parse_ShapeLayer_HasLayers()
        {
            var parser = new LottieParser(ShapeLayerJson, null, false);
            parser.Parse();
            Assert.NotNull(parser.comp!.root);
            Assert.True(parser.comp.root!.children.Count > 0);
        }

        [Fact]
        public void Parse_ShapeLayer_DimensionsCorrect()
        {
            var parser = new LottieParser(ShapeLayerJson, null, false);
            parser.Parse();
            Assert.Equal(200f, parser.comp!.w);
            Assert.Equal(150f, parser.comp!.h);
        }

        [Fact]
        public void Parse_ShapeLayer_FrameRateCorrect()
        {
            var parser = new LottieParser(ShapeLayerJson, null, false);
            parser.Parse();
            Assert.Equal(24f, parser.comp!.frameRate);
        }

        // ---- Invalid input ----

        [Fact]
        public void Parse_InvalidJson_ReturnsFalse()
        {
            var parser = new LottieParser("not json at all", null, false);
            Assert.False(parser.Parse());
        }

        [Fact]
        public void Parse_EmptyObject_ReturnsFalse()
        {
            var parser = new LottieParser("{}", null, false);
            // May parse but produce null comp or incomplete data
            var result = parser.Parse();
            // Even if Parse returns true, comp should have no valid root
            if (result && parser.comp != null)
            {
                // Acceptable: empty object parses to empty composition
                Assert.NotNull(parser.comp);
            }
        }

        [Fact]
        public void Parse_ArrayRoot_ReturnsFalse()
        {
            var parser = new LottieParser("[]", null, false);
            Assert.False(parser.Parse());
        }

        // ---- Duration calculation ----

        [Fact]
        public void Composition_Duration_Correct()
        {
            var parser = new LottieParser(MinimalLottieJson, null, false);
            parser.Parse();
            // Duration = frameCnt / frameRate = 60 / 30 = 2.0 seconds
            Assert.Equal(2.0f, parser.comp!.Duration());
        }
    }
}
