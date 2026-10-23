using System;
using System.Text.Json;
using Xunit;

namespace ThorVG.Tests;

public class tvgLottieSolidColorUpdateTests
{
    [Theory]
    [InlineData("#112233", 17, 34, 51)]
    [InlineData("#11223344", 17, 34, 51)]
    [InlineData("11223344", 17, 34, 51)]
    [InlineData("#123", 0, 1, 35)]
    [InlineData("#xyzxyz", 0, 0, 0)]
    [InlineData("#11Z233", 0, 0, 17)]
    [InlineData("#112233tail", 0, 17, 34)]
    [InlineData("+123456", 18, 52, 86)]
    [InlineData("-1", 255, 255, 255)]
    [InlineData("FFFFFFFFFFFFFFFFF", 255, 255, 255)]
    [InlineData("#112233\0ignored", 17, 34, 51)]
    [InlineData("#112233Ž", 0, 17, 34)]
    public void SolidLayersPreserveNativePartialHexAndIgnoredAlpha(string input, int red, int green, int blue)
    {
        string json = "{\"fr\":30,\"w\":64,\"h\":64,\"ip\":0,\"op\":90,\"layers\":[{\"ty\":1,\"ind\":1,\"ip\":0,\"op\":90,\"sw\":64,\"sh\":64,\"sc\":" +
                      JsonSerializer.Serialize(input) + "}]}";
        var parser = new LottieParser(json, null, false);
        Assert.True(parser.Parse());
        var composition = parser.comp!;
        try
        {
            var layer = Assert.IsType<LottieLayer>(Assert.Single(composition.root!.children));
            var solid = Assert.Single(layer.statical.pooler);
            solid.GetFillColor(out byte r, out byte g, out byte b, out byte a);
            Assert.Equal((byte)red, r);
            Assert.Equal((byte)green, g);
            Assert.Equal((byte)blue, b);
            Assert.Equal((byte)255, a);
        }
        finally { composition.Release(); }
    }
}
