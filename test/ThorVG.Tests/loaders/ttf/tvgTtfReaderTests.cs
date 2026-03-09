using System;
using System.IO;
using Xunit;

namespace ThorVG.Tests
{
    public class TtfReaderTests
    {
        private static readonly string FontPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ref", "ThorVG", "test", "resources", "PublicSans-Regular.ttf"));

        private TtfReader LoadReader()
        {
            var reader = new TtfReader();
            reader.data = File.ReadAllBytes(FontPath);
            reader.size = (uint)reader.data.Length;
            return reader;
        }

        [Fact]
        public void Header_ValidFont_ReturnsTrue()
        {
            var reader = LoadReader();
            Assert.True(reader.Header());
        }

        [Fact]
        public void Header_SetsUnitsPerEm()
        {
            var reader = LoadReader();
            reader.Header();
            Assert.True(reader.metrics.unitsPerEm > 0);
        }

        [Fact]
        public void Header_SetsHheaMetrics()
        {
            var reader = LoadReader();
            reader.Header();
            // A valid font must have non-zero ascent
            Assert.True(reader.metrics.hhea.ascent > 0);
            // Descent is typically negative
            Assert.True(reader.metrics.hhea.descent < 0 || reader.metrics.hhea.descent == 0);
            // Advance = ascent - descent + linegap
            Assert.Equal(
                reader.metrics.hhea.ascent - reader.metrics.hhea.descent + reader.metrics.hhea.linegap,
                reader.metrics.hhea.advance);
        }

        [Fact]
        public void Header_NullData_ReturnsFalse()
        {
            var reader = new TtfReader();
            Assert.False(reader.Header());
        }

        [Fact]
        public void Header_EmptyData_ReturnsFalse()
        {
            var reader = new TtfReader();
            reader.data = new byte[4];
            reader.size = 4;
            Assert.False(reader.Header());
        }

        [Fact]
        public void Header_GarbageData_ReturnsFalse()
        {
            var reader = new TtfReader();
            reader.data = new byte[256];
            reader.size = 256;
            // Fill with random non-TTF data
            for (int i = 0; i < 256; i++) reader.data[i] = (byte)(i & 0xFF);
            Assert.False(reader.Header());
        }

        [Fact]
        public void Glyph_LetterA_ReturnsValidIndex()
        {
            var reader = LoadReader();
            reader.Header();
            var glyphIdx = reader.Glyph('A');
            Assert.NotEqual(TtfReader.INVALID_GLYPH, glyphIdx);
        }

        [Fact]
        public void Glyph_Space_ReturnsValidIndex()
        {
            var reader = LoadReader();
            reader.Header();
            var glyphIdx = reader.Glyph(' ');
            Assert.NotEqual(TtfReader.INVALID_GLYPH, glyphIdx);
        }

        [Fact]
        public void GlyphMetrics_LetterA_HasNonZeroDimensions()
        {
            var reader = LoadReader();
            reader.Header();
            var tgm = new TtfGlyphMetrics();
            var offset = reader.Glyph('A', tgm);
            Assert.NotEqual(TtfReader.INVALID_GLYPH, tgm.idx);
            Assert.True(tgm.advance > 0, "Advance should be positive for letter A");
            Assert.True(tgm.w > 0, "Width should be positive for letter A");
            Assert.True(tgm.h > 0, "Height should be positive for letter A");
        }

        [Fact]
        public void GlyphMetrics_Space_HasNoOutline()
        {
            var reader = LoadReader();
            reader.Header();
            var tgm = new TtfGlyphMetrics();
            var offset = reader.Glyph(' ', tgm);
            // Space glyph typically has no outline (offset 0)
            Assert.Equal(0u, offset);
            Assert.True(tgm.advance > 0, "Space should have non-zero advance");
        }

        [Fact]
        public void Convert_LetterA_ProducesPath()
        {
            var reader = LoadReader();
            reader.Header();
            var tgm = new TtfGlyphMetrics();
            var glyphOffset = reader.Glyph('A', tgm);
            Assert.True(glyphOffset > 0, "Letter A should have an outline");

            var result = reader.Convert(tgm.path, tgm, glyphOffset, new Point(0, 0), 1);
            Assert.True(result, "Convert should succeed for letter A");
            Assert.True(tgm.path.cmds.count > 0, "Path should have commands");
            Assert.True(tgm.path.pts.count > 0, "Path should have points");
        }

        [Fact]
        public void Convert_MultipleGlyphs_ProducePaths()
        {
            var reader = LoadReader();
            reader.Header();

            foreach (char c in "Hello")
            {
                var tgm = new TtfGlyphMetrics();
                var glyphOffset = reader.Glyph(c, tgm);
                if (glyphOffset > 0)
                {
                    var result = reader.Convert(tgm.path, tgm, glyphOffset, new Point(0, 0), 1);
                    Assert.True(result, $"Convert should succeed for '{c}'");
                    Assert.True(tgm.path.cmds.count > 0, $"Path for '{c}' should have commands");
                }
            }
        }

        [Fact]
        public void Convert_ZeroOffset_ReturnsTrue()
        {
            var reader = LoadReader();
            reader.Header();
            var tgm = new TtfGlyphMetrics();
            // A glyph offset of 0 means no outline, convert should return true
            var result = reader.Convert(tgm.path, tgm, 0, new Point(0, 0), 1);
            Assert.True(result);
        }

        [Fact]
        public void Kerning_DoesNotCrash()
        {
            var reader = LoadReader();
            reader.Header();

            var glyphA = reader.Glyph('A');
            var glyphV = reader.Glyph('V');
            var kerning = new Point(0, 0);

            // Kerning may or may not be present; just verify it does not crash
            reader.Kerning(glyphA, glyphV, ref kerning);
            // No assertion on the result value; the font may lack kerning tables
        }

        [Fact]
        public void Glyph_Digit0_ReturnsValidIndex()
        {
            var reader = LoadReader();
            reader.Header();
            var idx = reader.Glyph('0');
            Assert.NotEqual(TtfReader.INVALID_GLYPH, idx);
        }

        [Fact]
        public void GlyphMetrics_VariousCharacters_HavePositiveAdvance()
        {
            var reader = LoadReader();
            reader.Header();
            foreach (char c in "abcXYZ123!@#")
            {
                var tgm = new TtfGlyphMetrics();
                reader.Glyph(c, tgm);
                Assert.True(tgm.advance > 0, $"Character '{c}' should have positive advance");
            }
        }
    }
}
