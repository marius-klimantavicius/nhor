using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Marius.Winter.Taffy;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

public class FloatNativeParityTests
{
    private sealed record Case(string[] Operations, string[] Expected);
    private static readonly Case[] s_cases = LoadCases();
    private static readonly Clear[] s_clear = { Clear.None, Clear.Left, Clear.Right, Clear.Both };
    private static string Bits(float value) => BitConverter.SingleToUInt32Bits(value).ToString(CultureInfo.InvariantCulture);
    private static string Optional(float? value) => value.HasValue ? Bits(value.Value) : "-";
    private static string Index(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "-";

    private static Case[] LoadCases()
    {
        using var stream = typeof(FloatNativeParityTests).Assembly.GetManifestResourceStream(
            "Marius.Winter.Taffy.Tests.Native.float-corpus.json")!;
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.EnumerateArray().Select(element => new Case(
            element.GetProperty("operations").EnumerateArray().Select(value => value.GetString()!).ToArray(),
            element.GetProperty("expected").EnumerateArray().Select(value => value.GetString()!).ToArray())).ToArray();
    }

    public static IEnumerable<object[]> CaseIds()
    {
        for (int i = 0; i < s_cases.Length; i++) yield return new object[] { i };
    }

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void FloatOperationsMatchPinnedNativeResultsBitForBit(int caseId)
    {
        var test = s_cases[caseId];
        var context = new FloatContext();
        for (int operation = 0; operation < test.Operations.Length; operation++)
        {
            string[] fields = test.Operations[operation].Split(' ');
            float F(int index) => float.Parse(fields[index], CultureInfo.InvariantCulture);
            int N(int index) => int.Parse(fields[index], CultureInfo.InvariantCulture);
            int? After(int index) => fields[index] == "-" ? null : N(index);
            string actual;
            switch (fields[0])
            {
                case "reset":
                    context = new FloatContext();
                    context.SetWidth(F(1));
                    actual = "";
                    break;
                case "place":
                {
                    var direction = (FloatDirection)N(7);
                    var point = context.PlaceFloatedBox((ulong)N(1), new Size<float>(F(2), F(3)),
                        F(4), new[] { F(5), F(6) }, direction, s_clear[N(8)]);
                    actual = $"{Bits(point.X)} {Bits(point.Y)}";
                    var floats = direction == FloatDirection.Left ? context.LeftFloats : context.RightFloats;
                    Assert.Equal((ulong)N(1), floats[^1].Id);
                    break;
                }
                case "content":
                {
                    var slot = context.FindContentSlot(F(1), new[] { F(2), F(3) }, s_clear[N(4)], After(5));
                    actual = $"{Index(slot.SegmentId)} {Bits(slot.X)} {Bits(slot.Y)} {Bits(slot.Width)} {Bits(slot.Height)}";
                    break;
                }
                case "bfc":
                {
                    var slot = context.FindBfcSlot(F(1), new[] { F(2), F(3) }, new[] { F(4), F(5) },
                        N(6) == 0 ? Direction.Ltr : Direction.Rtl, s_clear[N(7)], After(8));
                    actual = $"{Index(slot.SegmentId)} {Bits(slot.X)} {Bits(slot.Y)} {Bits(slot.BorderWidth)} {Bits(slot.StretchWidth)}";
                    break;
                }
                case "clear":
                    actual = Optional(context.ClearedThreshold(s_clear[N(1)]));
                    break;
                case "intrinsic":
                {
                    var available = fields[1] switch
                    {
                        "min" => AvailableSpace.MinContent,
                        "max" => AvailableSpace.MaxContent,
                        _ => AvailableSpace.Definite(F(1)),
                    };
                    var calculator = new FloatIntrinsicWidthCalculator(available);
                    for (int index = 2; index < fields.Length; index += 3)
                        calculator.AddFloat(F(index), (FloatDirection)N(index + 1), s_clear[N(index + 2)]);
                    actual = Bits(calculator.Result());
                    break;
                }
                case "active":
                    actual = $"{context.HasFloats().ToString().ToLowerInvariant()} {context.HasActiveFloats(F(1)).ToString().ToLowerInvariant()}";
                    break;
                default:
                    throw new InvalidOperationException(test.Operations[operation]);
            }
            Assert.True(test.Expected[operation] == actual,
                $"Native mismatch in case {caseId}, step {operation}: {test.Operations[operation]}\nExpected {test.Expected[operation]}\nActual {actual}");
        }
    }

    [Fact]
    public void FloatingPointMinAndMaxUseRustNumberSemantics()
    {
        Assert.Equal(1f, TaffySys.F32Max(float.NaN, 1));
        Assert.Equal(1f, TaffySys.F32Max(1, float.NaN));
        Assert.Equal(1f, TaffySys.F32Min(float.NaN, 1));
        Assert.Equal(1f, TaffySys.F32Min(1, float.NaN));
        Assert.True(float.IsNaN(TaffySys.F32Min(float.NaN, float.NaN)));
        Assert.True(float.IsNaN(TaffySys.F32Max(float.NaN, float.NaN)));
    }
}
