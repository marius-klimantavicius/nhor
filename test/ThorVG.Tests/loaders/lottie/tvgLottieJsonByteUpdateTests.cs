using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ThorVG.Tests;

public class tvgLottieJsonByteUpdateTests
{
    [Fact]
    public void StringsAndEscapesMatchEveryNativeByteProfile()
    {
        using var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "fixtures", "thorvg-update", "json-byte-native-golden.json")));
        foreach (var row in golden.RootElement.GetProperty("cases").EnumerateArray())
        {
            var handler = new LookaheadParserHandler(TvgUtf8.Decode(Convert.FromHexString(row.GetProperty("inputHex").GetString()!)));
            var actual = new StringBuilder();
            bool parsed = handler.ParseNext();
            actual.Append("parse=").Append(parsed ? 1 : 0);
            if (parsed && handler.EnterObject())
            {
                string? key;
                int count = 0;
                while ((key = handler.NextObjectKey()) != null)
                {
                    Assert.True(++count < 16);
                    actual.Append(",key=").Append(Hex(key)).Append(",value=");
                    if (handler.PeekType() == LookaheadParserHandler.kStringType) actual.Append(Hex(handler.GetStringCopy()));
                    else { actual.Append("not-string"); handler.Skip(); }
                }
            }
            actual.Append(",invalid=").Append(handler.Invalid() ? 1 : 0);
            string name = row.GetProperty("name").GetString()!;
            Assert.Equal(name + ":" + row.GetProperty("expectedNative").GetString(), name + ":" + actual);
        }
    }

    [Fact]
    public void NativeBytesSurviveCStringAndTextBoundariesWithoutAliasingReplacementCharacters()
    {
        string a = TvgUtf8.Decode(new byte[] { 0x80 });
        string b = TvgUtf8.Decode(new byte[] { 0xff });
        Assert.False(TvgStr.Equal(a, b));
        Assert.False(TvgStr.Equal(a, "\ufffd"));
        Assert.True(TvgStr.Equal(a, TvgUtf8.Decode(new byte[] { 0x80 })));
        Assert.Equal("80", Hex(TvgStr.Duplicate(a)));
        Assert.Equal("80ff", Hex(TvgStr.Concat(a, b)));
        Assert.Equal("8041", Hex(TvgStr.Append(a, "AB", 1)));
        string path = TvgStr.Concat("dir/", TvgStr.Concat(a, ".otf"));
        Assert.Equal("80", Hex(TvgStr.Filename(path)));
        Assert.Equal("otf", TvgStr.Fileext(path));
        Assert.Equal("dir/", TvgStr.Dirname(path));
        string text = TvgStr.Concat(TvgStr.Concat("é", a), "\0ignored");
        Assert.Equal("c3a980", Convert.ToHexString(LottieDataHelper.Utf8CString(text)).ToLowerInvariant());
        var document = new TextDocument { text = text };
        Assert.Equal(LottieDataHelper.Utf8CString(text), document.Utf8);
        var paint = Text.Gen();
        try
        {
            Assert.Equal(Result.Success, paint.SetText(text));
            Assert.Equal(3u, paint.utf8len);
            Assert.Equal("c3a980", Hex(paint.GetText()));
        }
        finally { Paint.Rel(paint); }
        // Native hashes char bytes with the platform's signedness.
        ulong expected = unchecked(5381UL * 33 + (TvgCompressor.NativeUnsignedChar ? 128UL : unchecked((ulong)(sbyte)0x80)));
        if (OperatingSystem.IsWindows() || IntPtr.Size == 4) expected = (uint)expected;
        Assert.Equal(expected, TvgCompressor.Djb2EncodeNative(a));
    }

    private static string Hex(string? value) => value == null ? "null" : Convert.ToHexString(TvgUtf8.Encode(value)).ToLowerInvariant();
}
