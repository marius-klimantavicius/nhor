using System;
using System.IO;
using System.Globalization;
using System.Text;
using ThorVG;
using System.Runtime.InteropServices;
using Xunit;

namespace ThorVG.Tests;
public unsafe class tvgSvgPathUtilNativeUpdateTests
{
    [Fact]
    public void ValidPathsUnicodeUrlsAndColorsMatchNative()
    {
        string architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        string folder = System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update");
        using var expected = new BinaryReader(File.OpenRead(System.IO.Path.Combine(folder, "svg-path-util-" + architecture + "-native-golden.bin")));
        int count = 0;
        foreach (var line in File.ReadLines(System.IO.Path.Combine(folder, "svg-path-util-normal-inputs.txt")))
        {
            var tab = line.IndexOf('\t');
            var kind = line[..tab];
            var value = line[(tab + 1)..];
            using var memory = new MemoryStream();
            using var row = new BinaryWriter(memory);
            void Word(uint v) => row.Write(v);
            void Number(float v) => Word(BitConverter.SingleToUInt32Bits(v));
            if (kind == "path")
            {
                var path = new RenderPath();
                try
                {
                    Word(SvgPath.ToShape(value, path) ? 1u : 0u);
                    Word(path.cmds.count);
                    for (uint i = 0; i < path.cmds.count; ++i)
                        Word((uint)path.cmds[i]);
                    Word(path.pts.count);
                    for (uint i = 0; i < path.pts.count; ++i)
                    {
                        Number(path.pts[i].x);
                        Number(path.pts[i].y);
                    }
                }
                finally
                {
                    path.cmds.Dispose();
                    path.pts.Dispose();
                }
            }
            else if (kind == "url")
            {
                var decoded = Encoding.UTF8.GetBytes(SvgUtil.URLDecode(value));
                Word((uint)decoded.Length + 1);
                row.Write(decoded);
            }
            else
            {
                var fields = value.Split(',');
                SvgUtil.HslToRgb(float.Parse(fields[0], CultureInfo.InvariantCulture), float.Parse(fields[1], CultureInfo.InvariantCulture), float.Parse(fields[2], CultureInfo.InvariantCulture), out var r, out var g, out var b);
                Word(r);
                Word(g);
                Word(b);
            }

            row.Flush();
            var actual = memory.ToArray();
            uint length = expected.ReadUInt32();
            Assert.Equal(length, (uint)actual.Length);
            var native = expected.ReadBytes((int)length);
            Assert.True(actual.AsSpan().SequenceEqual(native), $"Native ordinary SVG {kind} record{count} ({architecture}) must match complete defined bytes: {value}");
            ++count;
        }

        Assert.Equal(181, count);
        Assert.Equal(expected.BaseStream.Length, expected.BaseStream.Position);
    }
}
