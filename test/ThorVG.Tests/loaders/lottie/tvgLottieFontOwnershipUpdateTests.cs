using System;
using System.IO;
using Xunit;

namespace ThorVG.Tests;

public class tvgLottieFontOwnershipUpdateTests
{
    [Theory]
    [InlineData(null, "name:missing-resolver-font")]
    [InlineData("https://example.invalid/font.ttf", "https://example.invalid/font.ttf")]
    public void ExternalFontResolverUsesNameWhenNoPathIsAvailable(string? path, string expectedSource)
    {
        Assert.Equal(Result.Success, Initializer.Init(0));
        var font = new LottieFont { name = "missing-resolver-font", external = true, path = path };
        var text = new LottieText();
        text.doc.value = new TextDocument { name = font.name, text = "Text", size = 12, height = 14 };
        var layer = new LottieLayer { layerType = LottieLayer.LayerType.Text, outFrame = 60 };
        layer.children.Add(text);
        var root = new LottieRootLayer { outFrame = 60 };
        root.children.Add(layer);
        var comp = new LottieComposition { root = root, w = 64, h = 64, frameRate = 30 };
        comp.fonts.Add(font);
        var sources = new System.Collections.Generic.List<string>();
        try
        {
            using var builder = new LottieBuilder
            {
                resolver = new AssetResolver
                {
                    func = (paint, source, data) => { sources.Add(source); return false; }
                }
            };
            builder.Build(comp);
            Assert.True(builder.Update(comp, 0));
            Assert.Equal(expectedSource, Assert.Single(sources));
        }
        finally { comp.Release(); Assert.Equal(Result.Success, Initializer.Term()); }
    }

    [Theory]
    [InlineData("standard")]
    [InlineData("unpadded")]
    [InlineData("web-safe")]
    [InlineData("period-padding")]
    [InlineData("cstring-suffix")]
    public void EmbeddedFontsUseNativeDecodedSizeAndTransferredOwnership(string variant)
    {
        byte[] data = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "legacy-upstream", "resources", "DMSans.otf"));
        string encoded = Convert.ToBase64String(data);
        encoded = variant switch
        {
            "unpadded" => encoded.TrimEnd('='),
            "web-safe" => encoded.Replace('+', '-').Replace('/', '_'),
            "period-padding" => encoded.Replace('=', '.'),
            "cstring-suffix" => encoded + "\0ignored",
            _ => encoded
        };
        var font = new LottieFont { name = "embedded-owner-" + Guid.NewGuid().ToString("N"), mime = "otf", b64src = encoded };
        Loader? loader = null;
        Assert.Equal(Result.Success, Initializer.Init(0));
        try
        {
            font.Prepare();
            Assert.Equal((uint)data.Length, font.size);
            loader = LoaderMgr.Font(font.name);
            Assert.NotNull(loader);
            Assert.Equal(Ownership.Transfer, loader!.owner);
            Assert.Equal(1, loader.sharing);
            // Lookup itself acquires a native sharing reference.
            // Native retrieve reports a valid loader even when Close only
            // releases a sharing reference and keeps the cache entry alive.
            Assert.True(LoaderMgr.Retrieve(loader));
            Assert.Equal(0, loader.sharing);
            font.Release();
            Assert.Null(font.b64src);
            Assert.Equal(0, loader.sharing);
        }
        finally
        {
            font.Release();
            if (loader != null) LoaderMgr.Retrieve(loader);
            Assert.Equal(Result.Success, Initializer.Term());
        }
    }
}
