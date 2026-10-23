using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace ThorVG.Tests
{
    public class tvgLottieTweenUpdateTests
    {
        [Fact]
        public void TweenPreservesUnpairedTargetGradientStops()
        {
            using var tween = new LottieTween(); tween.On(10); tween.progress = 0.5f;
            var property = new LottieColorStop();
            var fill = LinearGradient.Gen();
            fill.SetColorStops(new[] { new Fill.ColorStop(0, 0, 0, 0, 255) }, 1);
            tween.Capture(property, 0, fill);
            fill.SetColorStops(new[] { new Fill.ColorStop(0, 200, 100, 50, 255), new Fill.ColorStop(1, 1, 2, 3, 128) }, 2);
            tween.Run(property, 0, fill);
            Assert.Equal(2u, fill.GetColorStops(out var stops));
            Assert.Equal((byte)100, stops![0].r);
            Assert.Equal((byte)1, stops[1].r);
            Assert.Equal((byte)128, stops[1].a);
        }

        [Fact]
        public void HeldGradientStopsFollowNativeInterpolationOrder()
        {
            var stops = new LottieColorStop { count = 1, populated = true };
            var start = stops.NewFrame(); start.hold = true;
            start.value.data = new[] { new Fill.ColorStop(0, 255, 0, 0, 255) };
            var end = stops.NewFrame(); end.no = 20;
            end.value.data = new[] { new Fill.ColorStop(0, 0, 255, 0, 255) };
            var fill = LinearGradient.Gen(); stops.Evaluate(10, fill);
            fill.GetColorStops(out var colors);
            Assert.Equal((byte)127, colors![0].r); Assert.Equal((byte)127, colors[0].g);
            stops.Release();
        }

        [Theory]
        [InlineData(0u)]
        [InlineData(2u)]
        public void AudioResolverUsesMasterVolumeAndRemappedFramePosition(uint threads)
        {
            Initializer.Init(threads);
            using var animation = LottieAnimation.Gen();
            using var canvas = SwCanvas.Gen()!;
            try
            {
                Assert.True(LottieAnimation.Expressions());
                Assert.Equal(0, animation.Volume());
                Assert.Equal(Result.InsufficientCondition, animation.Volume(1.5f));
                Assert.Equal(Result.Success, animation.GetPicture().Load(Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "lottie-audio-controls.json")));
                canvas.Target(new uint[256], 16, 16, 16, ColorSpace.ABGR8888);
                canvas.Add(animation.GetPicture());
                Assert.Equal(Result.Success, animation.Volume(1.5f));
                Assert.Equal(1.5f, animation.Volume());
                var events = new List<LottieAudioResolver>();
                animation.Resolver((info, data) => events.Add(info));
                Advance(12); Advance(13);
                animation.Volume(.5f); Advance(14); Advance(4); Advance(10);
                animation.Resolver(null); Advance(35);
                Assert.Equal(4, events.Count);
                Assert.Equal(120, events[0].volume); Assert.Equal(.35f, events[0].offset); Assert.True(events[0].active);
                Assert.Equal(40, events[1].volume); Assert.Equal(.45f, events[1].offset); Assert.True(events[1].active);
                Assert.Equal(100, events[2].volume); Assert.Equal(0, events[2].offset); Assert.False(events[2].active);
                Assert.Equal(40, events[3].volume); Assert.Equal(.25f, events[3].offset); Assert.True(events[3].active);

                void Advance(float frame)
                {
                    Assert.Equal(Result.Success, animation.Frame(frame));
                    Assert.Equal(Result.Success, canvas.Update());
                    Assert.Equal(Result.Success, canvas.Draw(true));
                    Assert.Equal(Result.Success, canvas.Sync());
                }
            }
            finally { canvas.Dispose(); animation.Dispose(); Initializer.Term(); }
        }
    }
}
