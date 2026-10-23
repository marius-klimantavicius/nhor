using System;
using System.IO;
using Xunit;

namespace ThorVG.Tests;

public class tvgLottieFontOwnershipUpdateTests
{
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
