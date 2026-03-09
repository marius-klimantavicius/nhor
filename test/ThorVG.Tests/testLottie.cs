// Ported from ThorVG/test/testLottie.cpp

using System;
using System.IO;
using Xunit;

namespace ThorVG.Tests
{
    public class testLottie
    {
        private static readonly string TEST_DIR = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ref", "ThorVG", "test", "resources"));

        [Fact]
        public void LottieCoverages()
        {
            Assert.Equal(Result.Success, Initializer.Init());
            {
                string[] names = {
                    "test3.lot",
                    "test4.lot",
                    "test5.lot",
                    "test6.lot",
                    "test7.lot",
                    "test8.lot",
                    "test9.lot",
                    "test10.lot",
                    "test11.lot",
                    "test12.lot"
                };

                var animation = Animation.Gen();
                Assert.NotNull(animation);

                var picture = animation.GetPicture();

                for (int i = 0; i < names.Length; ++i)
                {
                    var path = Path.Combine(TEST_DIR, names[i]);
                    Assert.Equal(Result.Success, picture.Load(path));
                    Assert.Equal(Result.InsufficientCondition, animation.Frame(0.0f));
                    Assert.Equal(Result.Success, animation.Frame(animation.TotalFrame() * 0.5f));
                    Assert.Equal(Result.Success, animation.Frame(animation.TotalFrame()));
                }
            }
            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void LottieSlot()
        {
            Assert.Equal(Result.Success, Initializer.Init());
            {
                var animation = LottieAnimation.Gen();
                Assert.NotNull(animation);

                var picture = animation.GetPicture();

                // Slot Test 1
                string slotJson = @"{""gradient_fill"":{""p"":{""p"":2,""k"":{""a"":0,""k"":[0,0.1,0.1,0.2,1,1,0.1,0.2,0.1,1]}}}}";

                // Negative: slot generation before loaded
                Assert.Equal(0u, animation.Gen(slotJson));

                Assert.Equal(Result.Success, picture.Load(Path.Combine(TEST_DIR, "slot.lot")));

                var id = animation.Gen(slotJson);
                Assert.True(id > 0);

                Assert.Equal(Result.Success, animation.Apply(0));
                Assert.Equal(Result.Success, animation.Apply(id));
                Assert.Equal(Result.Success, animation.Apply(0));
                Assert.Equal(Result.Success, animation.Apply(id));
                Assert.Equal(0u, animation.Gen(""));
                Assert.Equal(Result.Success, animation.Del(id));

                // Slot Test 2
                string slotJson2 = @"{""lottie-icon-outline"":{""p"":{""a"":0,""k"":[1,1,0]}},""lottie-icon-solid"":{""p"":{""a"":0,""k"":[0,0,1]}}}";

                var id2 = animation.Gen(slotJson2);
                Assert.True(id2 > 0);

                Assert.Equal(Result.Success, animation.Apply(id2));
                Assert.Equal(Result.Success, animation.Apply(0));
                Assert.Equal(Result.Success, animation.Apply(id2));
                Assert.Equal(Result.Success, animation.Del(id2));

                // Slot Test 3 (Transform)
                string positionSlot = @"{""transform_id"":{""p"":{""a"":1,""k"":[{""i"":{""x"":0.833,""y"":0.833},""o"":{""x"":0.167,""y"":0.167},""s"":[100,100],""t"":0},{""s"":[200,300],""t"":100}]}}}";
                var id3 = animation.Gen(positionSlot);
                Assert.True(id3 > 0);
                Assert.Equal(Result.Success, animation.Apply(id3));
                Assert.Equal(Result.Success, animation.Apply(0));
                Assert.Equal(Result.Success, animation.Del(id3));

                string scaleSlot = @"{""transform_id"":{""p"":{""a"":1,""k"":[{""i"":{""x"":0.833,""y"":0.833},""o"":{""x"":0.167,""y"":0.167},""s"":[0,0],""t"":0},{""s"":[100,100],""t"":100}]}}}";
                var id4 = animation.Gen(scaleSlot);
                Assert.True(id4 > 0);
                Assert.Equal(Result.Success, animation.Apply(id4));
                Assert.Equal(Result.Success, animation.Apply(0));
                Assert.Equal(Result.Success, animation.Del(id4));

                string rotationSlot = @"{""transform_id"":{""p"":{""a"":1,""k"":[{""i"":{""x"":0.833,""y"":0.833},""o"":{""x"":0.167,""y"":0.167},""s"":[0],""t"":0},{""s"":[180],""t"":100}]}}}";
                var id5 = animation.Gen(rotationSlot);
                Assert.True(id5 > 0);
                Assert.Equal(Result.Success, animation.Apply(id5));
                Assert.Equal(Result.Success, animation.Apply(0));
                Assert.Equal(Result.Success, animation.Del(id5));

                string opacitySlot = @"{""transform_id"":{""p"":{""a"":1,""k"":[{""i"":{""x"":0.833,""y"":0.833},""o"":{""x"":0.167,""y"":0.167},""s"":[0],""t"":0},{""s"":[100],""t"":100}]}}}";
                var id6 = animation.Gen(opacitySlot);
                Assert.True(id6 > 0);
                Assert.Equal(Result.Success, animation.Apply(id6));
                Assert.Equal(Result.Success, animation.Apply(0));
                Assert.Equal(Result.Success, animation.Del(id6));

                // Slot Test 4: Expression
                string expressionSlot = @"{""rect_rotation"":{""p"":{""x"":""var $bm_rt = time * 360;""}},""rect_scale"":{""p"":{""x"":""var $bm_rt = [];$bm_rt[0] = value[0] + Math.cos(2 * Math.PI * time) * 100;$bm_rt[1] = value[1];""}},""rect_position"":{""p"":{""x"":""var $bm_rt = [];$bm_rt[0] = value[0] + Math.cos(2 * Math.PI * time) * 100;$bm_rt[1] = value[1];""}}}";
                var id7 = animation.Gen(expressionSlot);
                Assert.True(id7 > 0);
                Assert.Equal(Result.Success, animation.Apply(id7));
                Assert.Equal(Result.Success, animation.Apply(0));
                Assert.Equal(Result.Success, animation.Del(id7));

                // Slot Test 5: Text
                string textSlot = @"{""text_doc"":{""p"":{""k"":[{""s"":{""f"":""Ubuntu Light Italic"",""t"":""ThorVG!"",""j"":0,""s"":48,""fc"":[1,1,1]},""t"":0}]}}}";
                var id8 = animation.Gen(textSlot);
                Assert.True(id8 > 0);
                Assert.Equal(Result.Success, animation.Apply(id8));
                Assert.Equal(Result.Success, animation.Apply(0));
                Assert.Equal(Result.Success, animation.Del(id8));

                // Slot Test 6: Image
                string imageSlot = @"{""path_img"":{""p"":{""id"":""image_0"",""w"":200,""h"":300,""u"":""images/"",""p"":""logo.png"",""e"":0}}}";
                var id9 = animation.Gen(imageSlot);
                Assert.True(id9 > 0);
                Assert.Equal(Result.Success, animation.Apply(id9));
                Assert.Equal(Result.Success, animation.Apply(0));
                Assert.Equal(Result.Success, animation.Del(id9));
            }
            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void LottieMarker()
        {
            Assert.Equal(Result.Success, Initializer.Init());
            {
                var animation = LottieAnimation.Gen();
                Assert.NotNull(animation);

                var picture = animation.GetPicture();

                // Set marker name before loaded
                Assert.Equal(Result.InsufficientCondition, animation.Segment("sectionC"));

                // Animation load
                Assert.Equal(Result.Success, picture.Load(Path.Combine(TEST_DIR, "segment.lot")));

                // Set marker
                Assert.Equal(Result.Success, animation.Segment("sectionA"));

                // Set marker by invalid name
                Assert.Equal(Result.InvalidArguments, animation.Segment(""));

                // Get marker count
                Assert.Equal(3u, animation.MarkersCnt());

                // Get marker name by index
                Assert.Equal("sectionB", animation.Marker(1));

                // Get marker name by invalid index
                Assert.Null(animation.Marker(uint.MaxValue));

                Assert.Equal(Result.Success, animation.Segment((string?)null));
            }
            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void LottieTween()
        {
            Assert.Equal(Result.Success, Initializer.Init());
            {
                var animation = LottieAnimation.Gen();
                Assert.NotNull(animation);

                var picture = animation.GetPicture();

                Assert.Equal(Result.InsufficientCondition, animation.Tween(0.0f, 10.0f, 0.5f));

                Assert.Equal(Result.Success, picture.Load(Path.Combine(TEST_DIR, "test.lot")));

                // Set initial frame to avoid frame difference being too small
                Assert.Equal(Result.Success, animation.Frame(5.0f));

                // Tween between frames with different progress values
                Assert.Equal(Result.Success, animation.Tween(0.0f, 10.0f, 0.5f));
                Assert.Equal(Result.Success, animation.Tween(10.0f, 20.0f, 0.0f));
                Assert.Equal(Result.Success, animation.Tween(20.0f, 30.0f, 1.0f));

                // Tween with different frame ranges
                Assert.Equal(Result.Success, animation.Tween(10.0f, 50.0f, 0.25f));
                Assert.Equal(Result.Success, animation.Tween(50.0f, 100.0f, 0.75f));

                // Tween between distant frames
                Assert.Equal(Result.Success, animation.Tween(0.0f, 100.0f, 0.5f));
            }
            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void LottieQuality()
        {
            Assert.Equal(Result.Success, Initializer.Init());
            {
                var animation = LottieAnimation.Gen();
                Assert.NotNull(animation);

                var picture = animation.GetPicture();

                Assert.Equal(Result.InsufficientCondition, animation.Quality(50));

                Assert.Equal(Result.Success, picture.Load(Path.Combine(TEST_DIR, "test.lot")));

                // Set quality with minimum value
                Assert.Equal(Result.Success, animation.Quality(0));

                // Set quality with default value
                Assert.Equal(Result.Success, animation.Quality(50));

                // Set quality with maximum value
                Assert.Equal(Result.Success, animation.Quality(100));

                // Set quality with various values
                Assert.Equal(Result.Success, animation.Quality(25));
                Assert.Equal(Result.Success, animation.Quality(75));

                // Set quality with invalid value (> 100)
                Assert.Equal(Result.InvalidArguments, animation.Quality(101));
            }
            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void LottieAssetResolver()
        {
            Assert.Equal(Result.Success, Initializer.Init());
            {
                var animation = LottieAnimation.Gen();
                Assert.NotNull(animation);

                var picture = animation.GetPicture();

                Func<Paint, string, object?, bool> resolver = (Paint p, string src, object? data) =>
                {
                    if (p.PaintType() == Type.Picture)
                    {
                        var resolvedPath = Path.Combine(TEST_DIR, "image", "test.png");
                        var ret = ((Picture)p).Load(resolvedPath);
                        return (ret == Result.Success);
                    }
                    else if (p.PaintType() == Type.Text)
                    {
                        var fontPath = Path.Combine(TEST_DIR, "font", "Arial.ttf");
                        if (Text.LoadFont(fontPath) != Result.Success) return false;
                        var ret = ((Text)p).SetFont("Arial");
                        return (ret == Result.Success);
                    }
                    return false;
                };

                // Test unset resolver
                Assert.Equal(Result.Success, picture.Resolver(resolver, null));
                Assert.Equal(Result.Success, picture.Resolver(null, null));

                // Resolver Test (Image and Font)
                Assert.Equal(Result.Success, picture.Resolver(resolver, null));
                Assert.Equal(Result.Success, picture.Load(Path.Combine(TEST_DIR, "resolver.json")));
                Assert.Equal(Result.Success, animation.Frame(animation.TotalFrame() * 0.5f));

                // Test that setting/unsetting resolver after load
                Assert.Equal(Result.InsufficientCondition, picture.Resolver(resolver, null));
                Assert.Equal(Result.InsufficientCondition, picture.Resolver(null, null));
            }
            Assert.Equal(Result.Success, Initializer.Term());
        }
    }
}
