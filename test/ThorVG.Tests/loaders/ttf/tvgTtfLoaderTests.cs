using System;
using System.IO;
using Xunit;

namespace ThorVG.Tests
{
    public class TtfLoaderTests
    {
        private static readonly string FontPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ref", "ThorVG", "test", "resources", "PublicSans-Regular.ttf"));

        private TtfLoader CreateLoader()
        {
            var loader = new TtfLoader();
            Assert.True(loader.Open(FontPath), "Loader should open the font file");
            return loader;
        }

        // ---- Open tests ----

        [Fact]
        public void Open_ValidPath_ReturnsTrue()
        {
            var loader = new TtfLoader();
            Assert.True(loader.Open(FontPath));
        }

        [Fact]
        public void Open_InvalidPath_ReturnsFalse()
        {
            var loader = new TtfLoader();
            Assert.False(loader.Open("/nonexistent/path/font.ttf"));
        }

        [Fact]
        public void Open_SetsName()
        {
            var loader = CreateLoader();
            Assert.NotNull(loader.name);
            Assert.Contains("PublicSans", loader.name!);
        }

        [Fact]
        public void Open_FromData_ReturnsTrue()
        {
            var loader = new TtfLoader();
            var bytes = File.ReadAllBytes(FontPath);
            Assert.True(loader.Open(bytes, (uint)bytes.Length, true));
        }

        [Fact]
        public void Open_FromData_NoCopy_ReturnsTrue()
        {
            var loader = new TtfLoader();
            var bytes = File.ReadAllBytes(FontPath);
            Assert.True(loader.Open(bytes, (uint)bytes.Length, false));
        }

        // ---- Get (text shaping) tests ----

        [Fact]
        public void Get_SimpleText_ProducesPath()
        {
            var loader = CreateLoader();
            var fm = new FontMetrics { fontSize = 24.0f };
            var path = new RenderPath();

            Assert.True(loader.Get(fm, "Hello", path));
            Assert.True(path.cmds.count > 0, "Should produce path commands");
            Assert.True(path.pts.count > 0, "Should produce path points");
        }

        [Fact]
        public void Get_EmptyText_ReturnsFalse()
        {
            var loader = CreateLoader();
            var fm = new FontMetrics { fontSize = 24.0f };
            var path = new RenderPath();

            Assert.False(loader.Get(fm, "", path));
        }

        [Fact]
        public void Get_NullText_ReturnsFalse()
        {
            var loader = CreateLoader();
            var fm = new FontMetrics { fontSize = 24.0f };
            var path = new RenderPath();

            Assert.False(loader.Get(fm, null!, path));
        }

        [Fact]
        public void Get_ZeroFontSize_ReturnsFalse()
        {
            var loader = CreateLoader();
            var fm = new FontMetrics { fontSize = 0.0f };
            var path = new RenderPath();

            Assert.False(loader.Get(fm, "Hello", path));
        }

        [Fact]
        public void Get_SetsTextSize()
        {
            var loader = CreateLoader();
            var fm = new FontMetrics { fontSize = 24.0f };
            var path = new RenderPath();

            loader.Get(fm, "Hello World", path);
            Assert.True(fm.size.x > 0, "Text width should be positive");
            Assert.True(fm.size.y > 0, "Text height should be positive");
        }

        [Fact]
        public void Get_AllocatesEngine()
        {
            var loader = CreateLoader();
            var fm = new FontMetrics { fontSize = 24.0f };
            var path = new RenderPath();

            loader.Get(fm, "A", path);
            Assert.NotNull(fm.engine);
            Assert.IsType<TtfMetrics>(fm.engine);
        }

        [Fact]
        public void Get_SpaceOnly_ProducesNoOutline()
        {
            var loader = CreateLoader();
            var fm = new FontMetrics { fontSize = 24.0f };
            var path = new RenderPath();

            loader.Get(fm, " ", path);
            // Space has no outline path, but the call should succeed
            Assert.True(fm.size.x > 0, "Width should account for space advance");
        }

        // ---- Wrapping modes ----

        [Fact]
        public void Get_WrapCharacter_Works()
        {
            var loader = CreateLoader();
            var fm = new FontMetrics
            {
                fontSize = 24.0f,
                wrap = TextWrap.Character,
                box = new Point(100.0f, 200.0f)
            };
            var path = new RenderPath();

            Assert.True(loader.Get(fm, "This is a longer text string for testing character wrap", path));
            Assert.True(path.cmds.count > 0);
        }

        [Fact]
        public void Get_WrapWord_Works()
        {
            var loader = CreateLoader();
            var fm = new FontMetrics
            {
                fontSize = 24.0f,
                wrap = TextWrap.Word,
                box = new Point(100.0f, 200.0f)
            };
            var path = new RenderPath();

            Assert.True(loader.Get(fm, "This is a longer text string for testing word wrap", path));
            Assert.True(path.cmds.count > 0);
        }

        [Fact]
        public void Get_WrapSmart_Works()
        {
            var loader = CreateLoader();
            var fm = new FontMetrics
            {
                fontSize = 24.0f,
                wrap = TextWrap.Smart,
                box = new Point(100.0f, 200.0f)
            };
            var path = new RenderPath();

            Assert.True(loader.Get(fm, "This is a longer text string for testing smart wrap", path));
            Assert.True(path.cmds.count > 0);
        }

        [Fact]
        public void Get_WrapEllipsis_Works()
        {
            var loader = CreateLoader();
            var fm = new FontMetrics
            {
                fontSize = 24.0f,
                wrap = TextWrap.Ellipsis,
                box = new Point(100.0f, 200.0f)
            };
            var path = new RenderPath();

            Assert.True(loader.Get(fm, "This is a very long text that should be truncated with ellipsis", path));
            Assert.True(path.cmds.count > 0);
        }

        [Fact]
        public void Get_WrapNone_WithBox_Works()
        {
            var loader = CreateLoader();
            var fm = new FontMetrics
            {
                fontSize = 24.0f,
                wrap = TextWrap.None,
                box = new Point(100.0f, 200.0f)
            };
            var path = new RenderPath();

            Assert.True(loader.Get(fm, "Hello World", path));
            Assert.True(path.cmds.count > 0);
        }

        // ---- Multiline ----

        [Fact]
        public void Get_LineFeed_ProducesMultiline()
        {
            var loader = CreateLoader();
            var fm = new FontMetrics { fontSize = 24.0f };
            var path = new RenderPath();

            Assert.True(loader.Get(fm, "Line1\nLine2", path));
            Assert.True(fm.size.y > 0);
        }

        // ---- Metrics ----

        [Fact]
        public void Metrics_ReturnsScaledValues()
        {
            var loader = CreateLoader();
            var fm = new FontMetrics { fontSize = 24.0f };

            loader.Metrics(fm, out TextMetrics tm);
            Assert.True(tm.ascent > 0, "Ascent should be positive");
            Assert.True(tm.advance > 0, "Advance should be positive");
        }

        [Fact]
        public void Metrics_ScalesWithFontSize()
        {
            var loader = CreateLoader();

            var fm12 = new FontMetrics { fontSize = 12.0f };
            var fm24 = new FontMetrics { fontSize = 24.0f };

            loader.Metrics(fm12, out TextMetrics tm12);
            loader.Metrics(fm24, out TextMetrics tm24);

            // At double the font size, metrics should be roughly double
            Assert.True(tm24.ascent > tm12.ascent);
            Assert.InRange(tm24.ascent / tm12.ascent, 1.9f, 2.1f);
        }

        // ---- Release / Copy ----

        [Fact]
        public void Release_ClearsEngine()
        {
            var loader = CreateLoader();
            var fm = new FontMetrics { fontSize = 24.0f };
            var path = new RenderPath();

            loader.Get(fm, "A", path);
            Assert.NotNull(fm.engine);

            loader.Release(fm);
            Assert.Null(fm.engine);
        }

        [Fact]
        public void Copy_DuplicatesMetrics()
        {
            var loader = CreateLoader();
            var input = new FontMetrics
            {
                fontSize = 24.0f,
                wrap = TextWrap.Word,
                box = new Point(100, 200),
                spacing = new Point(1.5f, 1.5f),
                align = new Point(0.5f, 0.5f)
            };
            var path = new RenderPath();
            loader.Get(input, "A", path);

            var output = new FontMetrics();
            loader.Copy(input, output);

            Assert.Equal(input.fontSize, output.fontSize);
            Assert.Equal(input.wrap, output.wrap);
            Assert.Equal(input.box.x, output.box.x);
            Assert.Equal(input.box.y, output.box.y);
            Assert.Equal(input.spacing.x, output.spacing.x);
            Assert.Equal(input.spacing.y, output.spacing.y);
            Assert.NotNull(output.engine);
            Assert.NotSame(input.engine, output.engine);
        }

        // ---- Transform ----

        [Fact]
        public void Transform_DoesNotThrow()
        {
            var loader = CreateLoader();
            var fm = new FontMetrics { fontSize = 24.0f };
            var path = new RenderPath();
            loader.Get(fm, "A", path);

            var shape = Shape.Gen();
            // Should not throw
            loader.Transform(shape, fm, 0.0f);
        }

        [Fact]
        public void Transform_WithItalicShear_DoesNotThrow()
        {
            var loader = CreateLoader();
            var fm = new FontMetrics { fontSize = 24.0f };
            var path = new RenderPath();
            loader.Get(fm, "A", path);

            var shape = Shape.Gen();
            loader.Transform(shape, fm, 0.2f);
        }

        // ---- Caching ----

        [Fact]
        public void GlyphCaching_SameCharReturnsSameMetrics()
        {
            var loader = CreateLoader();
            var fm = new FontMetrics { fontSize = 24.0f };
            var path1 = new RenderPath();
            var path2 = new RenderPath();

            // First call caches glyphs, second uses cache
            loader.Get(fm, "AA", path1);
            var count1 = path1.pts.count;

            loader.Get(fm, "AA", path2);
            var count2 = path2.pts.count;

            Assert.Equal(count1, count2);
        }
    }
}
