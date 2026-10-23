using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using ThorVG;
using System.Runtime.InteropServices;
using Xunit;

namespace ThorVG.Tests;
public unsafe class tvgSvgLoaderNativeUpdateTests
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicMethods, typeof(SvgLoader))]
    static object Call(string name, params object[] args) => typeof(SvgLoader).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args)!;
    [Fact]
    public void OrdinaryLoaderRecordsMatchNative()
    {
        string architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        string folder = System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update");
        using var expected = new BinaryReader(File.OpenRead(System.IO.Path.Combine(folder, "svg-loader-" + architecture + "-native-golden.bin")));
        int count = 0;
        foreach (var line in File.ReadLines(System.IO.Path.Combine(folder, "svg-loader-normal-inputs.txt")))
        {
            var tab = line.IndexOf('\t');
            var kind = line[..tab];
            var value = line[(tab + 1)..];
            using var memory = new MemoryStream();
            using var row = new BinaryWriter(memory);
            void Word(uint v) => row.Write(v);
            void Number(float v) => Word(BitConverter.SingleToUInt32Bits(v));
            var parser = new SvgParser
            {
                global = new(0, 0, 123.7f, 81.3f)
            };
            var ctx = new SvgParserContext
            {
                svgParse = parser
            };
            if (kind == "transform")
            {
                var m = (Matrix? )Call("ParseTransformationMatrix", value);
                Word(m.HasValue ? 1u : 0u);
                if (m is Matrix v)
                    foreach (var f in new[]
                    {
                        v.e11,
                        v.e12,
                        v.e13,
                        v.e21,
                        v.e22,
                        v.e23,
                        v.e31,
                        v.e32,
                        v.e33
                    }

                    )
                        Number(f);
            }
            else if (kind == "length")
            {
                var comma = value.IndexOf(',');
                Number((float)Call("ToFloat", parser, value[(comma + 1)..], (SvgParserLengthType)int.Parse(value[..comma])));
            }
            else if (kind == "dash")
            {
                var dash = new SvgDash();
                Word((bool)Call("ParseDashArray", ctx, value, dash) ? 1u : 0u);
                Word((uint)dash.array.Count);
                foreach (var f in dash.array)
                    Number(f);
            }
            else if (kind == "color")
            {
                object[] parameters =
                {
                    value,
                    (byte)0,
                    (byte)0,
                    (byte)0,
                    (byte)255,
                    null !,
                    true
                };
                Word((bool)Call("ToColor", parameters) ? 1u : 0u);
                for (int i = 1; i < 5; ++i)
                    Word((byte)parameters[i]);
                if (parameters[5] is string url)
                {
                    var bytes = Encoding.UTF8.GetBytes(url);
                    Word((uint)bytes.Length);
                    row.Write(bytes);
                }
                else
                    Word(uint.MaxValue);
            }
            else if (kind == "gradient")
            {
                var fields = value.Split('|');
                var from = (SvgStyleGradient)Call(fields[0] == "0" ? "CreateLinearGradient" : "CreateRadialGradient", ctx, fields[2], 0, fields[2].Length);
                var to = (SvgStyleGradient)Call(fields[1] == "0" ? "CreateLinearGradient" : "CreateRadialGradient", ctx, fields[3], 0, fields[3].Length);
                from.stops.Add(new() { offset = 0, r = 1, g = 2, b = 3, a = 255 });
                from.stops.Add(new() { offset = 1, r = 210, g = 111, b = 14, a = 128 });
                Call("InheritGradient", ctx, to, from);
                Word((uint)to.type);
                Word((uint)to.flags);
                Word(to.userSpace ? 1u : 0u);
                if (to.type == SvgGradientType.Linear)
                {
                    var g = to.linear!;
                    foreach (var v in new[]
                    {
                        g.x1,
                        g.y1,
                        g.x2,
                        g.y2
                    }

                    )
                        Number(v);
                    foreach (var v in new[]
                    {
                        g.isX1Percentage,
                        g.isY1Percentage,
                        g.isX2Percentage,
                        g.isY2Percentage
                    }

                    )
                        Word(v ? 1u : 0u);
                }
                else
                {
                    var g = to.radial!;
                    foreach (var v in new[]
                    {
                        g.cx,
                        g.cy,
                        g.fx,
                        g.fy,
                        g.r,
                        g.fr
                    }

                    )
                        Number(v);
                    foreach (var v in new[]
                    {
                        g.isCxPercentage,
                        g.isCyPercentage,
                        g.isFxPercentage,
                        g.isFyPercentage,
                        g.isRPercentage,
                        g.isFrPercentage
                    }

                    )
                        Word(v ? 1u : 0u);
                }

                Word((uint)to.stops.Count);
                foreach (var stop in to.stops)
                {
                    Number(stop.offset);
                    Word(stop.r);
                    Word(stop.g);
                    Word(stop.b);
                    Word(stop.a);
                }
            }
            else
            {
                var xml = value[2..];
                bool success = XmlParser.Parse(xml, xml.Length, value[0] == '1', (c, t, s, o, n) =>
                {
                    Word((uint)t);
                    var bytes = Encoding.UTF8.GetBytes(s.AsSpan(o, n).ToString());
                    Word((uint)bytes.Length);
                    row.Write(bytes);
                    if (t == XMLType.Open && s.AsSpan(o, n).StartsWith("text"))
                        c.openedTag = OpenedTagType.Text;
                    else if (t == XMLType.Close && s.AsSpan(o, n).SequenceEqual("text"))
                        c.openedTag = OpenedTagType.Other;
                    return true;
                }, ctx);
                Word(uint.MaxValue);
                Word(success ? 1u : 0u);
            }

            row.Flush();
            var actual = memory.ToArray();
            uint length = expected.ReadUInt32();
            Assert.Equal(length, (uint)actual.Length);
            var native = expected.ReadBytes((int)length);
            Assert.True(actual.AsSpan().SequenceEqual(native), $"Native ordinary SVG loader {kind} record{count} ({architecture}) must match complete defined bytes: {value}");
            ++count;
        }

        Assert.Equal(291, count);
        Assert.Equal(expected.BaseStream.Length, expected.BaseStream.Position);
    }
}
