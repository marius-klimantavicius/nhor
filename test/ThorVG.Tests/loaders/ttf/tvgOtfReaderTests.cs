using System;
using System.IO;
using Xunit;

namespace ThorVG.Tests
{
    public class tvgOtfReaderTests
    {
        private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", name);

        [Theory]
        [InlineData("subroutines")]
        [InlineData("curves")]
        [InlineData("hintmask")]
        [InlineData("cid-format0")]
        [InlineData("cid-format3")]
        public unsafe void Cff_ActualOutlinesAndMetrics(string kind)
        {
            var loader = new TtfLoader();
            Assert.True(loader.Open(Fixture("cff-" + kind + ".otf")));
            Assert.IsType<OtfReader>(loader.reader);
            var fm = new FontMetrics { fontSize = 24 };
            var path = new RenderPath();
            try
            {
                Assert.True(loader.Get(fm, "A", path));
                Assert.True(path.cmds.count >= 3);
                Assert.True(path.pts.count >= 3);
                Assert.Equal(PathCommand.MoveTo, path.cmds[0]);
                Assert.Equal(PathCommand.Close, path.cmds[path.cmds.count - 1]);
                if (kind == "curves") Assert.Equal(PathCommand.CubicTo, path.cmds[1]);
                Assert.True(loader.GlyphMetrics(fm, "A", out var glyph));
                Assert.Equal(16, glyph.advance);
                Assert.True(glyph.max.x > glyph.min.x);
                Assert.True(glyph.max.y > glyph.min.y);
            }
            finally { path.cmds.Dispose(); path.pts.Dispose(); }
        }

        [Fact]
        public void Cff_RealDMSansFontOpens()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "legacy-upstream", "resources", "DMSans.otf");
            var loader = new TtfLoader();
            Assert.True(loader.Open(path));
            var fm = new FontMetrics { fontSize = 24 };
            Assert.True(loader.GlyphMetrics(fm, "A", out var metrics));
            Assert.True(metrics.advance > 0);
            Assert.True(metrics.max.y > metrics.min.y);
        }

        [Theory]
        [InlineData("windows-nonbmp", "\uD83D\uDE00", 2)]
        [InlineData("unicode-legacy", "A", 1)]
        public void Sfnt_CharacterMapEncodings(string variant, string character, int consumed)
        {
            var loader = new TtfLoader();
            Assert.True(loader.Open(Fixture("cmap-" + variant + ".otf")));
            Assert.True(loader.GlyphMetrics(new FontMetrics { fontSize = 24 }, character, out var glyph, out int next));
            Assert.Equal(16, glyph.advance);
            Assert.Equal(consumed, next); // C# string offsets are UTF-16 code units.
        }

        [Theory]
        [InlineData(0)]
        [InlineData(4)]
        [InlineData(20)]
        [InlineData(100)]
        public void Cff_TruncatedFontRejected(int length)
        {
            var bytes = File.ReadAllBytes(Fixture("cff-subroutines.otf"));
            var loader = new TtfLoader();
            Assert.Equal(Result.InvalidArguments, loader.OpenResult(bytes, (uint)length, new LoaderOps(ThorVG.Type.Text)));
        }
    }
}
