using System;
using System.IO;
using ThorVG;
using System.Runtime.InteropServices;
using Xunit;

namespace ThorVG.Tests;
public unsafe class tvgSfntNativeUpdateTests
{
    [Fact]
    public void OrdinarySupportedGlyphsMatchPinnedNative()
    {
        string architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        using var expected = new BinaryReader(File.OpenRead(System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "sfnt-outlines-" + architecture + "-native-golden.bin")));
        int records = 0;
        foreach (var line in File.ReadLines(System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "sfnt-normal-fonts.txt")))
        {
            var parts = line.Split('\t');
            var data = File.ReadAllBytes(System.IO.Path.Combine(AppContext.BaseDirectory, parts[0]));
            TtfReader reader = data[0] == 'O' ? new OtfReader() : new TtfReader();
            reader.data = data;
            reader.size = (uint)data.Length;
            var header = reader.HeaderResult();
            if (header != Result.Success)
                throw new Exception("Valid font header: " + parts[0]);
            uint previous = 0;
            foreach (var code in parts[1].Split(','))
            {
                uint cp = uint.Parse(code);
                using var memory = new MemoryStream();
                using var row = new BinaryWriter(memory);
                void Word(uint v) => row.Write(v);
                void Number(float v) => Word(BitConverter.SingleToUInt32Bits(v));
                Word(cp);
                Word((uint)header);
                Word(reader.metrics.unitsPerEm);
                Word(reader.metrics.numHmtx);
                Word(reader.metrics.locaFormat);
                foreach (var v in new[]
                {
                    reader.metrics.hhea.ascent,
                    reader.metrics.hhea.descent,
                    reader.metrics.hhea.linegap,
                    reader.metrics.hhea.advance
                }

                )
                    Number(v);
                var glyph = new TtfGlyphMetrics();
                var outline = glyph.path;
                try
                {
                    uint offset = reader.Glyph(cp, glyph);
                    bool result = glyph.idx != TtfReader.INVALID_GLYPH && reader.Convert(outline, glyph, offset, new(0, 0), 1);
                    Word(result ? 1u : 0u);
                    Word(glyph.idx);
                    Number(glyph.advance);
                    Number(glyph.lsb);
                    var bounds = glyph.NativeBounds;
                    Number(bounds.min.x);
                    Number(bounds.min.y);
                    Number(bounds.max.x);
                    Number(bounds.max.y);
                    Word(outline.cmds.count);
                    for (uint i = 0; i < outline.cmds.count; ++i)
                        Word((uint)outline.cmds[i]);
                    Word(outline.pts.count);
                    for (uint i = 0; i < outline.pts.count; ++i)
                    {
                        Number(outline.pts[i].x);
                        Number(outline.pts[i].y);
                    }

                    Point position = default;
                    Word(reader.Kerning(previous, glyph.idx, ref position) ? 1u : 0u);
                    Number(position.x);
                    Number(position.y);
                    previous = glyph.idx;
                }
                finally
                {
                    glyph.Dispose();
                }

                row.Flush();
                var actual = memory.ToArray();
                uint expectedLength = expected.ReadUInt32();
                Assert.Equal(expectedLength, (uint)actual.Length);
                var native = expected.ReadBytes((int)expectedLength);
                Assert.True(actual.AsSpan().SequenceEqual(native), $"Native supported font record {records}, {parts[0]},U+{cp:X} ({architecture}) must match every header/metric/outline/kerning word.");
                ++records;
            }
        }

        Assert.Equal(319, records);
        Assert.Equal(expected.BaseStream.Length, expected.BaseStream.Position);
    }
}
