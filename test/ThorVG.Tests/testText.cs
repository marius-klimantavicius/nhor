// Ported from ThorVG/test/testText.cpp

using System;
using System.IO;
using System.Text;
using Xunit;

namespace ThorVG.Tests
{
    public class testText
    {
        private static readonly string TEST_DIR = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ThorVG", "test", "resources"));

        [Fact]
        public void TextCreation()
        {
            var text = Text.Gen();
            Assert.NotNull(text);

            Assert.Equal(Type.Text, text.PaintType());

            Paint.Rel(text);
        }

        [Fact]
        public void LoadTTFDataFromFile()
        {
            Initializer.Init();
            {
                var text = Text.Gen();
                Assert.NotNull(text);

                Assert.Equal(Result.InsufficientCondition, Text.UnloadFont(Path.Combine(TEST_DIR, "invalid.ttf")));
                Assert.Equal(Result.Success, Text.LoadFont(Path.Combine(TEST_DIR, "PublicSans-Regular.ttf")));
                Assert.Equal(Result.InvalidArguments, Text.LoadFont(Path.Combine(TEST_DIR, "invalid.ttf")));
                Assert.Equal(Result.Success, Text.UnloadFont(Path.Combine(TEST_DIR, "PublicSans-Regular.ttf")));
                Assert.Equal(Result.InvalidArguments, Text.LoadFont(""));
                Assert.Equal(Result.Success, Text.LoadFont(Path.Combine(TEST_DIR, "PublicSans-Regular.ttf")));

                Paint.Rel(text);
            }
            Initializer.Term();
        }

        [Fact]
        public void LoadTTFDataFromMemory()
        {
            Initializer.Init();
            {
                var data = File.ReadAllBytes(Path.Combine(TEST_DIR, "PublicSans-Regular.ttf"));
                Assert.NotEmpty(data);

                var text = Text.Gen();
                Assert.NotNull(text);

                var svgStr = "<svg height=\"1000\" viewBox=\"0 0 600 600\" ></svg>";
                var svgBytes = Encoding.UTF8.GetBytes(svgStr);

                // load
                Assert.Equal(Result.InvalidArguments, Text.LoadFont(null!, data, (uint)data.Length));
                Assert.Equal(Result.InvalidArguments, Text.LoadFont("PublicSans-Regular", data, 0));
                Assert.Equal(Result.NonSupport, Text.LoadFont("PublicSans-RegularSvg", svgBytes, (uint)svgBytes.Length, "unknown"));
                Assert.Equal(Result.Success, Text.LoadFont("PublicSans-RegularUnknown", data, (uint)data.Length, "unknown"));
                Assert.Equal(Result.Success, Text.LoadFont("PublicSans-RegularTtf", data, (uint)data.Length, "ttf", true));
                Assert.Equal(Result.Success, Text.LoadFont("PublicSans-Regular", data, (uint)data.Length, ""));

                // unload
                Assert.Equal(Result.InsufficientCondition, Text.LoadFont("invalid", null, 0));
                Assert.Equal(Result.InsufficientCondition, Text.LoadFont("PublicSans-RegularSvg", null, 0));
                Assert.Equal(Result.Success, Text.LoadFont("PublicSans-RegularUnknown", null, 0));
                Assert.Equal(Result.Success, Text.LoadFont("PublicSans-RegularTtf", null, 0));
                Assert.Equal(Result.Success, Text.LoadFont("PublicSans-Regular", null, 111));

                Paint.Rel(text);
            }
            Initializer.Term();
        }

        [Fact]
        public void TextFont()
        {
            Initializer.Init();
            {
                var text = Text.Gen();
                Assert.NotNull(text);

                Assert.Equal(Result.Success, Text.LoadFont(Path.Combine(TEST_DIR, "PublicSans-Regular.ttf")));
                Assert.Equal(Result.Success, text.SetFont("PublicSans-Regular"));
                Assert.Equal(Result.Success, text.SetFontSize(80));
                Assert.Equal(Result.Success, text.SetFont("PublicSans-Regular"));
                Assert.Equal(Result.Success, text.SetFontSize(1));
                Assert.Equal(Result.Success, text.SetFontSize(50));
                Assert.Equal(Result.Success, text.SetFont(null));
                Assert.Equal(Result.InsufficientCondition, text.SetFont("InvalidFont"));

                Paint.Rel(text);
            }
            Initializer.Term();
        }

        [Fact]
        public void TextBasic()
        {
            Initializer.Init();
            {
                var canvas = SwCanvas.Gen();
                var buffer = new uint[100 * 100];
                canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);

                var text = Text.Gen();
                Assert.NotNull(text);

                Assert.Equal(Result.Success, Text.LoadFont(Path.Combine(TEST_DIR, "PublicSans-Regular.ttf")));
                Assert.Equal(Result.Success, text.SetFont("PublicSans-Regular"));
                Assert.Equal(Result.Success, text.SetFontSize(80));
                Assert.Equal(Result.Success, text.SetText(null));
                Assert.Equal(Result.Success, text.SetText(""));
                Assert.Equal(Result.Success, text.SetText("ABCDEFGHIJIKLMOPQRSTUVWXYZ"));
                Assert.Equal(Result.Success, text.SetText("THORVG Text"));
                Assert.Equal(Result.Success, text.SetFill(255, 255, 255));
                Assert.Equal(Result.Success, canvas.Add(text));
                Assert.Equal(Result.Success, canvas.Update());
                Assert.Equal(Result.Success, canvas.Sync());
            }
            Initializer.Term();
        }

        [Fact]
        public void TextWithCompositeGlyphs()
        {
            Initializer.Init();
            {
                var canvas = SwCanvas.Gen();
                var buffer = new uint[100 * 100];
                canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);

                var text = Text.Gen();
                Assert.NotNull(text);

                Assert.Equal(Result.Success, Text.LoadFont(Path.Combine(TEST_DIR, "PublicSans-Regular.ttf")));
                Assert.Equal(Result.Success, text.SetFont("PublicSans-Regular"));
                Assert.Equal(Result.Success, text.SetFontSize(80));
                // UTF-8 bytes \xc5\xbb\x6f\xc5\x82\xc4\x85\x64\xc5\xba \xc8\xab = "Żołądź ȫ"
                Assert.Equal(Result.Success, text.SetText("\u017Bo\u0142\u0105d\u017A \u022B"));
                Assert.Equal(Result.Success, text.SetFill(255, 255, 255));
                Assert.Equal(Result.Success, canvas.Add(text));
                Assert.Equal(Result.Success, canvas.Update());
                Assert.Equal(Result.Success, canvas.Sync());
            }
            Initializer.Term();
        }

        [Fact]
        public void TextStyles()
        {
            Initializer.Init();
            {
                var canvas = SwCanvas.Gen();
                var buffer = new uint[100 * 100];
                canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);

                var text = Text.Gen();
                Assert.NotNull(text);

                Assert.Equal(Result.Success, Text.LoadFont(Path.Combine(TEST_DIR, "PublicSans-Regular.ttf")));
                Assert.Equal(Result.Success, text.SetFont("PublicSans-Regular"));
                Assert.Equal(Result.Success, text.SetFontSize(80));
                Assert.Equal(Result.Success, text.SetText("ThorVG Test\n Text!"));
                Assert.Equal(Result.Success, text.SetFill(255, 255, 255));

                Assert.Equal(Result.Success, text.SetOutline(0, 0, 0, 0));
                Assert.Equal(Result.Success, text.SetOutline(3, 255, 255, 255));
                Assert.Equal(Result.Success, text.SetOutline(0, 0, 0, 0));

                Assert.Equal(Result.Success, text.SetItalic(-10.0f));
                Assert.Equal(Result.Success, text.SetItalic(10000.0f));
                Assert.Equal(Result.Success, text.SetItalic(0.0f));
                Assert.Equal(Result.Success, text.SetItalic(0.18f));

                Assert.Equal(Result.Success, canvas.Add(text));
                Assert.Equal(Result.Success, canvas.Update());
                Assert.Equal(Result.Success, canvas.Sync());
            }
            Initializer.Term();
        }

        [Fact]
        public void TextLayout()
        {
            Initializer.Init();
            {
                var canvas = SwCanvas.Gen();
                var buffer = new uint[100 * 100];
                canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);

                var text = Text.Gen();
                Assert.NotNull(text);

                Assert.Equal(Result.Success, Text.LoadFont(Path.Combine(TEST_DIR, "PublicSans-Regular.ttf")));
                Assert.Equal(Result.Success, text.SetFont("PublicSans-Regular"));
                Assert.Equal(Result.Success, text.SetFontSize(80));
                Assert.Equal(Result.Success, text.SetFill(255, 255, 255));
                Assert.Equal(Result.Success, text.SetText("ThorVG Test\n Text!"));

                Assert.Equal(Result.Success, text.SetAlign(0.0f, 0.0f));
                Assert.Equal(Result.Success, text.SetAlign(0.5f, 0.5f));
                Assert.Equal(Result.Success, text.SetAlign(1.0f, 1.0f));
                Assert.Equal(Result.Success, text.SetAlign(2.0f, 2.0f));
                Assert.Equal(Result.Success, text.SetAlign(-1.0f, -1.0f));

                Assert.Equal(Result.Success, text.SetLayout(0, 0));
                Assert.Equal(Result.Success, text.SetLayout(-100, -100));
                Assert.Equal(Result.Success, text.SetLayout(100, 100));

                Assert.Equal(Result.Success, canvas.Add(text));
                Assert.Equal(Result.Success, canvas.Update());
                Assert.Equal(Result.Success, canvas.Sync());
            }
            Initializer.Term();
        }

        [Fact]
        public void TextWrapMode()
        {
            Initializer.Init();
            {
                var canvas = SwCanvas.Gen();
                var buffer = new uint[100 * 100];
                canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);

                var text = Text.Gen();
                Assert.NotNull(text);

                Assert.Equal(Result.Success, Text.LoadFont(Path.Combine(TEST_DIR, "PublicSans-Regular.ttf")));
                Assert.Equal(Result.Success, text.SetFont("PublicSans-Regular"));
                Assert.Equal(Result.Success, text.SetFontSize(80));
                Assert.Equal(Result.Success, text.SetFill(255, 255, 255));
                Assert.Equal(Result.Success, text.SetAlign(0.5f, 0.5f));
                Assert.Equal(Result.Success, text.SetText("Very Long Long Text ThorVG Test\n ABCDEFGHIJKLMNOPRSTU!"));
                Assert.Equal(Result.Success, text.SetLayout(100, 100));
                Assert.Equal(Result.Success, canvas.Add(text));

                Assert.Equal(Result.Success, text.SetWrapping(TextWrap.Character));
                Assert.Equal(Result.Success, canvas.Update());
                Assert.Equal(Result.Success, canvas.Sync());

                Assert.Equal(Result.Success, text.SetWrapping(TextWrap.Word));
                Assert.Equal(Result.Success, canvas.Update());
                Assert.Equal(Result.Success, canvas.Sync());

                Assert.Equal(Result.Success, text.SetWrapping(TextWrap.Smart));
                Assert.Equal(Result.Success, canvas.Update());
                Assert.Equal(Result.Success, canvas.Sync());

                Assert.Equal(Result.Success, text.SetWrapping(TextWrap.Ellipsis));
                Assert.Equal(Result.Success, canvas.Update());
                Assert.Equal(Result.Success, canvas.Sync());
            }
            Initializer.Term();
        }

        [Fact]
        public void TextSpacing()
        {
            Initializer.Init();
            {
                var canvas = SwCanvas.Gen();
                var buffer = new uint[100 * 100];
                canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);

                var text = Text.Gen();
                Assert.NotNull(text);

                Assert.Equal(Result.Success, Text.LoadFont(Path.Combine(TEST_DIR, "PublicSans-Regular.ttf")));
                Assert.Equal(Result.Success, text.SetFont("PublicSans-Regular"));
                Assert.Equal(Result.Success, text.SetFontSize(80));
                // UTF-8 bytes \xc5\xbb\x6f\xc5\x82\xc4\x85\x64\xc5\xba \xc8\xab = "Żołądź ȫ"
                Assert.Equal(Result.Success, text.SetText("\u017Bo\u0142\u0105d\u017A \u022B"));
                Assert.Equal(Result.InvalidArguments, text.SetSpacing(-1.0f, -1.0f));
                Assert.Equal(Result.Success, text.SetSpacing(0.0f, 0.0f));
                Assert.Equal(Result.Success, text.SetSpacing(1.5f, 1.5f));
                Assert.Equal(Result.Success, text.SetSpacing(2.0f, 2.0f));

                Assert.Equal(Result.Success, canvas.Add(text));
            }
            Initializer.Term();
        }
    }
}
