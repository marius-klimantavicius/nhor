using System;
using System.Text;
using Xunit;

namespace ThorVG.Tests;

public class tvgLottieRootUpdateTests
{
    [Fact]
    public void ParsedCompositionAndAssetRootsKeepCompositionTypeAndLayerParents()
    {
        const string json = """
            {"fr":30,"w":64,"h":64,"ip":0,"op":90,"assets":[{"id":"nested","layers":[{"ty":3,"ind":2,"ip":0,"op":90}]}],"layers":[{"ty":3,"ind":1,"ip":0,"op":90}]}
            """;
        var parser = new LottieParser(json, null, false);
        Assert.True(parser.Parse());
        var comp = parser.comp!;
        try
        {
            var root = Assert.IsType<LottieRootLayer>(comp.root);
            var asset = Assert.IsType<LottieRootLayer>(Assert.Single(comp.assets));
            Assert.Equal(LottieObject.ObjectType.Composition, root.type);
            Assert.Equal(LottieObject.ObjectType.Composition, asset.type);
            Assert.Same(root, Assert.IsType<LottieLayer>(Assert.Single(root.children)).precomp);
            Assert.Same(asset, Assert.IsType<LottieLayer>(Assert.Single(asset.children)).precomp);
            root.Prepare();
            Assert.Equal(LottieObject.ObjectType.Composition, root.type);
        }
        finally { comp.Release(); }
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(0, 5)]
    [InlineData(88.5f, 93.5f)]
    [InlineData(89, 94)]
    [InlineData(89.5f, 94)]
    [InlineData(90, 94)]
    public void ClampStopsFractionalFramesAtTheLastFrame(float relative, float expected)
    {
        var comp = new LottieComposition { root = new LottieRootLayer { inPoint = 5, outPoint = 95 } };
        comp.Clamp(ref relative);
        Assert.Equal(expected, relative);
        comp.Release();
    }

    [Theory]
    [InlineData("{\"ty\":5,\"ind\":1,\"ip\":0,\"op\":90,\"shapes\":[{\"ty\":\"rc\",\"s\":{\"a\":0,\"k\":[8,8]}}]}", "[]")]
    [InlineData("{\"ty\":0,\"ind\":1,\"ip\":0,\"op\":90,\"w\":64,\"h\":64,\"refId\":\"bad\"}", "[{\"id\":\"bad\",\"w\":1,\"h\":1,\"p\":\"missing.png\",\"e\":0}]")]
    public void MalformedTextAndPrecompReferencesPreserveTheNativeEmptyLayer(string layer, string assets)
    {
        Assert.Equal(Result.Success, Initializer.Init());
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes("{\"fr\":30,\"w\":64,\"h\":64,\"ip\":0,\"op\":90,\"assets\":" + assets + ",\"layers\":[" + layer + "]}");
            using var animation = Animation.Gen();
            Assert.Equal(Result.Success, animation.GetPicture().Load(bytes, (uint)bytes.Length, "lottie", copy: true));
            using var canvas = SwCanvas.Gen()!;
            var pixels = new uint[64 * 64];
            Assert.Equal(Result.Success, canvas.Target(pixels, 64, 64, 64, ColorSpace.ABGR8888));
            Assert.Equal(Result.Success, canvas.Add(animation.GetPicture()));
            Assert.Equal(Result.Success, canvas.Draw(true));
            Assert.Equal(Result.Success, canvas.Sync());
            Assert.All(pixels, pixel => Assert.Equal(0u, pixel));
        }
        finally { Assert.Equal(Result.Success, Initializer.Term()); }
    }
}
