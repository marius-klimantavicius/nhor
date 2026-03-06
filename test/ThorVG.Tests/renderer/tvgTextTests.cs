using Xunit;

namespace ThorVG.Tests
{
    public class tvgTextTests
    {
        [Fact]
        public void Text_Gen_ReturnsInstance()
        {
            var text = Text.Gen();
            Assert.NotNull(text);
            Assert.Equal(Type.Text, text.PaintType());
        }

        [Fact]
        public void Text_SetText()
        {
            var text = Text.Gen();
            Assert.Equal(Result.Success, text.SetText("Hello"));
        }

        [Fact]
        public void Text_SetFont_ReturnsInsufficientCondition()
        {
            // STUB -- font loading not yet ported
            var text = Text.Gen();
            Assert.Equal(Result.InsufficientCondition, text.SetFont("Arial"));
        }

        [Fact]
        public void Text_SetFontSize_Valid()
        {
            var text = Text.Gen();
            Assert.Equal(Result.Success, text.SetFontSize(12.0f));
        }

        [Fact]
        public void Text_SetFontSize_Negative_InvalidArgs()
        {
            var text = Text.Gen();
            Assert.Equal(Result.InvalidArguments, text.SetFontSize(-1.0f));
        }

        [Fact]
        public void Text_SetWrapping()
        {
            var text = Text.Gen();
            Assert.Equal(Result.Success, text.SetWrapping(TextWrap.Word));
        }

        [Fact]
        public void Text_Duplicate()
        {
            var text = Text.Gen();
            text.SetText("World");
            var dup = text.Duplicate();
            Assert.NotSame(text, dup);
            Assert.Equal(Type.Text, dup.PaintType());
        }
    }
}
