using System.Text;
using System.Reflection;
using Xunit;

namespace ThorVG.Tests;

public class tvgLottieCandidateApiTests
{
    [Fact]
    public void StaticContentKeepsAnimationAndLottieResultConditions()
    {
        Assert.Equal(Result.Success, Initializer.Init(0));
        try
        {
            using var animation = LottieAnimation.Gen();
            byte[] svg = Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><rect width='8' height='8'/></svg>");
            Assert.Equal(Result.Success, animation.GetPicture().Load(svg, (uint)svg.Length, "svg", copy: true));
            Assert.Equal(Result.NonSupport, animation.Frame(0));
            Assert.Equal(Result.NonSupport, animation.Frame(50));
            Assert.Equal(Result.NonSupport, animation.Segment(0, 10));
            Assert.Equal(Result.InsufficientCondition, animation.Segment((string?)null));
            Assert.Equal(Result.InsufficientCondition, animation.TweenTo(10));
            Assert.Equal(Result.InsufficientCondition, animation.Tween(0.5f));
            Assert.Equal(Result.InsufficientCondition, animation.Tween(0, 10, 0.5f));
            Assert.Equal(0u, animation.MarkersCnt());
            Assert.Null(animation.Marker(0, out _, out _));
            Assert.Equal(0u, animation.Gen("{}"));
            Assert.Equal(Result.InsufficientCondition, animation.Apply(0));
            Assert.Equal(Result.InsufficientCondition, animation.Del(1));
        }
        finally { Assert.Equal(Result.Success, Initializer.Term()); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TimeRemapSlotOverridesAndResetsStaticAndAnimatedProperties(bool animated)
    {
        Assert.Equal(Result.Success, Initializer.Init(0));
        try
        {
            using var animation = LottieAnimation.Gen();
            byte[] data = Encoding.UTF8.GetBytes("""
                {"fr":30,"w":64,"h":64,"ip":0,"op":90,"layers":[{"ty":3,"ind":1,"ip":0,"op":90,"tm":{"a":0,"k":0,"sid":"time_remap"}}]}
                """);
            Assert.Equal(Result.Success, animation.GetPicture().Load(data, (uint)data.Length, "lot", copy: true));
            var loaderField = typeof(Picture).GetField("loader", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var loader = Assert.IsType<LottieLoader>(loaderField.GetValue(animation.GetPicture()));
            var layer = Assert.IsType<LottieLayer>(Assert.Single(loader.comp!.root!.children));
            Assert.Equal(0, layer.timeRemap.Evaluate(0));
            string slot = animated ? """
                {"time_remap":{"p":{"a":1,"k":[{"t":0,"s":[3],"e":[0],"o":{"x":0.167,"y":0.167},"i":{"x":0.833,"y":0.833}},{"t":180,"s":[0]}]}}}
                """ : """
                {"time_remap":{"p":{"a":0,"k":1.5}}}
                """;
            uint id = animation.Gen(slot);
            Assert.NotEqual(0u, id);
            Assert.Equal(Result.Success, animation.Apply(id));
            if (animated)
            {
                Assert.Equal(2, layer.timeRemap.frames!.Count);
                Assert.Equal(3, layer.timeRemap.Evaluate(0));
                Assert.Equal(0, layer.timeRemap.Evaluate(180));
            }
            else
            {
                Assert.Null(layer.timeRemap.frames);
                Assert.Equal(1.5f, layer.timeRemap.Evaluate(0));
            }
            Assert.Equal(Result.Success, animation.Apply(0));
            Assert.Null(layer.timeRemap.frames);
            Assert.Equal(0, layer.timeRemap.Evaluate(0));
            Assert.Equal(Result.Success, animation.Del(id));
        }
        finally { Assert.Equal(Result.Success, Initializer.Term()); }
    }
}
