using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

public class StyleParsingTests
{
    private static readonly JsonDocument Corpus = Load("parse-corpus");
    private static JsonDocument Load(string name)
    {
        using var raw = typeof(StyleParsingTests).Assembly.GetManifestResourceStream($"Marius.Winter.Taffy.Tests.Native.{name}.json.gz")!;
        using var gzip = new GZipStream(raw, CompressionMode.Decompress); return JsonDocument.Parse(gzip);
    }
    public static IEnumerable<object[]> Types() => Corpus.RootElement.EnumerateArray().Select(c => c.GetProperty("type").GetString()!).Distinct().Select(t => new object[] { t });
    private static Type ManagedType(string name) => name switch
    {
        "GridPlacement<String>" => typeof(GridPlacement), "GridTemplateComponent<String>" => typeof(GridTemplateComponent),
        "GridTemplateTracks<String,GridTemplateComponent<String>>" => typeof(GridTemplateTracks<GridTemplateComponent>),
        "GridTemplateTracks<String,TrackSizingFunction>" => typeof(GridTemplateTracks<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>>),
        "TrackSizingFunction" => typeof(MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>),
        _ => typeof(Style).Assembly.GetType("Marius.Winter.Taffy." + name, true)!,
    };
    [Theory, MemberData(nameof(Types))]
    public void NativeValueGrammarAndSerializedValues(string name)
    {
        var type = ManagedType(name);
        var parse = typeof(CssStyleParser).GetMethod(nameof(CssStyleParser.Parse))!.MakeGenericMethod(type);
        var serialize = typeof(TaffyStyleJson).GetMethod(nameof(TaffyStyleJson.Serialize))!.MakeGenericMethod(type);
        var deserialize = typeof(TaffyStyleJson).GetMethods().Single(m => m.Name == nameof(TaffyStyleJson.Deserialize) && m.GetParameters()[0].ParameterType == typeof(string)).MakeGenericMethod(type);
        foreach (var test in Corpus.RootElement.EnumerateArray().Where(c => c.GetProperty("type").GetString() == name))
        {
            string input = test.GetProperty("input").GetString()!; var expected = test.GetProperty("expected");
            bool success = expected.GetProperty("ok").GetBoolean(); object? actual = null;
            try { actual = parse.Invoke(null, new object[] { input }); Assert.True(success, $"{name} accepted {input}"); }
            catch (TargetInvocationException error) when (error.InnerException is CssParseException) { Assert.False(success, $"{name} rejected {input}: {error.InnerException}"); continue; }
            var expectedValue = expected.GetProperty("value");
            string json = (string)serialize.Invoke(null, new[] { actual })!;
            using var result = JsonDocument.Parse(json);
            if (name == "AvailableSpace" && expectedValue.ValueKind == JsonValueKind.Object && expectedValue.GetProperty("Definite").ValueKind == JsonValueKind.Number)
            {
                // Native serde_json::Value promotes f32 to f64; both wire spellings must decode to exactly the same f32.
                Assert.Equal(BitConverter.SingleToUInt32Bits(expectedValue.GetProperty("Definite").GetSingle()), BitConverter.SingleToUInt32Bits(((AvailableSpace)actual!).Unwrap()));
            }
            else Assert.True(JsonElement.DeepEquals(expectedValue, result.RootElement), $"{name} {input}: expected {expectedValue}, actual {json}");
            // serde_json serializes non-finite Definite values as null, which cannot deserialize to f32.
            if (name == "AvailableSpace" && expectedValue.ValueKind == JsonValueKind.Object && expectedValue.GetProperty("Definite").ValueKind == JsonValueKind.Null) continue;
            var roundtrip = deserialize.Invoke(null, new object[] { expectedValue.GetRawText() });
            using var written = JsonDocument.Parse((string)serialize.Invoke(null, new[] { roundtrip })!);
            if (name == "AvailableSpace") Assert.Equal(actual, roundtrip);
            else Assert.True(JsonElement.DeepEquals(expectedValue, written.RootElement), $"{name} JSON round trip: {expectedValue} -> {written.RootElement}");
        }
    }
    [Fact]
    public void HandwrittenStylesRoundTripNativeWireFormat()
    {
        using var corpus = Load("handwritten-corpus");
        foreach (var test in corpus.RootElement.EnumerateArray())
        foreach (var phase in test.GetProperty("phases").EnumerateArray())
        foreach (var node in phase.GetProperty("nodes").EnumerateArray())
        {
            var expected = node.GetProperty("style"); var style = TaffyStyleJson.Deserialize<Style>(expected);
            using var written = JsonDocument.Parse(TaffyStyleJson.Serialize(style));
            Assert.True(JsonElement.DeepEquals(expected, written.RootElement), $"{test.GetProperty("name")}: {expected} -> {written.RootElement}");
        }
    }
    [Fact]
    public void NativeStyleDefaultsAndDeserializationErrors()
    {
        using var corpus = Load("serde-corpus");
        foreach (var test in corpus.RootElement.EnumerateArray())
        {
            var expected = test.GetProperty("expected"); string input = test.GetProperty("input").GetString()!;
            if (!expected.GetProperty("ok").GetBoolean()) { Assert.Throws<JsonException>(() => TaffyStyleJson.Deserialize<Style>(input)); continue; }
            var style = TaffyStyleJson.Deserialize<Style>(input);
            using var written = JsonDocument.Parse(TaffyStyleJson.Serialize(style));
            Assert.True(JsonElement.DeepEquals(expected.GetProperty("value"), written.RootElement), $"{input}: expected {expected.GetProperty("value")}, actual {written.RootElement}");
            if (input == "{}")
            {
                using var defaults = JsonDocument.Parse(TaffyStyleJson.Serialize(new Style()));
                Assert.True(JsonElement.DeepEquals(expected.GetProperty("value"), defaults.RootElement));
            }
        }
    }
    [Fact]
    public void RawCompactTagsAndTypedWireValidationMatchNative()
    {
        using var corpus = Load("wire-corpus");
        foreach (var test in corpus.RootElement.EnumerateArray())
        {
            var name = test.GetProperty("type").GetString()!;
            var type = ManagedType(name);
            var deserialize = typeof(TaffyStyleJson).GetMethods().Single(m => m.Name == nameof(TaffyStyleJson.Deserialize) && m.GetParameters()[0].ParameterType == typeof(string)).MakeGenericMethod(type);
            var serialize = typeof(TaffyStyleJson).GetMethod(nameof(TaffyStyleJson.Serialize))!.MakeGenericMethod(type);
            var expected = test.GetProperty("expected"); var input = test.GetProperty("input").GetString()!;
            object? actual;
            try { actual = deserialize.Invoke(null, new object[] { input }); Assert.True(expected.GetProperty("ok").GetBoolean(), $"{name} accepted {input}"); }
            catch (TargetInvocationException error) when (error.InnerException is JsonException) { Assert.False(expected.GetProperty("ok").GetBoolean(), $"{name} rejected {input}: {error.InnerException}"); continue; }
            using var written = JsonDocument.Parse((string)serialize.Invoke(null, new[] { actual })!);
            Assert.True(JsonElement.DeepEquals(expected.GetProperty("value"), written.RootElement), $"{name} {input}: expected {expected.GetProperty("value")}, actual {written.RootElement}");
        }
    }
}
