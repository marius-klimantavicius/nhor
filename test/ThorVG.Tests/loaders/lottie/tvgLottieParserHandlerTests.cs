// Tests for LookaheadParserHandler (JSON DOM walking)

using Xunit;

namespace ThorVG.Tests
{
    public class LottieParserHandlerTests
    {
        // ---- Construction ----

        [Fact]
        public void Constructor_ValidJson_NotInvalid()
        {
            var handler = new LookaheadParserHandler("{}");
            Assert.False(handler.Invalid());
        }

        [Fact]
        public void Constructor_InvalidJson_IsInvalid()
        {
            var handler = new LookaheadParserHandler("{bad json");
            Assert.True(handler.Invalid());
        }

        [Fact]
        public void Constructor_EmptyString_IsInvalid()
        {
            var handler = new LookaheadParserHandler("");
            Assert.True(handler.Invalid());
        }

        // ---- ParseNext ----

        [Fact]
        public void ParseNext_OnValidDoc_ReturnsTrue()
        {
            var handler = new LookaheadParserHandler("{\"a\": 1}");
            Assert.True(handler.ParseNext());
        }

        // ---- Object Walking ----

        [Fact]
        public void EnterObject_OnObjectRoot()
        {
            var handler = new LookaheadParserHandler("{\"key\": \"value\"}");
            handler.ParseNext();
            Assert.True(handler.EnterObject());
        }

        [Fact]
        public void NextObjectKey_ReturnsKeys()
        {
            var handler = new LookaheadParserHandler("{\"a\": 1, \"b\": 2}");
            handler.ParseNext();
            handler.EnterObject();

            var key1 = handler.NextObjectKey();
            Assert.Equal("a", key1);
            Assert.Equal(1, handler.GetInt());

            var key2 = handler.NextObjectKey();
            Assert.Equal("b", key2);
            Assert.Equal(2, handler.GetInt());

            var key3 = handler.NextObjectKey();
            Assert.Null(key3); // end of object
        }

        // ---- Array Walking ----

        [Fact]
        public void EnterArray_OnArrayRoot()
        {
            var handler = new LookaheadParserHandler("[1, 2, 3]");
            handler.ParseNext();
            Assert.True(handler.EnterArray());
        }

        [Fact]
        public void NextArrayValue_IteratesElements()
        {
            var handler = new LookaheadParserHandler("[10, 20, 30]");
            handler.ParseNext();
            handler.EnterArray();

            Assert.True(handler.NextArrayValue());
            Assert.Equal(10, handler.GetInt());

            Assert.True(handler.NextArrayValue());
            Assert.Equal(20, handler.GetInt());

            Assert.True(handler.NextArrayValue());
            Assert.Equal(30, handler.GetInt());

            Assert.False(handler.NextArrayValue()); // end
        }

        // ---- GetFloat ----

        [Fact]
        public void GetFloat_ReturnsFloatValue()
        {
            var handler = new LookaheadParserHandler("{\"v\": 3.14}");
            handler.ParseNext();
            handler.EnterObject();
            handler.NextObjectKey();
            Assert.InRange(handler.GetFloat(), 3.13f, 3.15f);
        }

        // ---- GetString ----

        [Fact]
        public void GetString_ReturnsStringValue()
        {
            var handler = new LookaheadParserHandler("{\"name\": \"hello\"}");
            handler.ParseNext();
            handler.EnterObject();
            handler.NextObjectKey();
            Assert.Equal("hello", handler.GetString());
        }

        // ---- GetBool ----

        [Fact]
        public void GetBool_ReturnsTrue()
        {
            var handler = new LookaheadParserHandler("{\"flag\": true}");
            handler.ParseNext();
            handler.EnterObject();
            handler.NextObjectKey();
            Assert.True(handler.GetBool());
        }

        [Fact]
        public void GetBool_ReturnsFalse()
        {
            var handler = new LookaheadParserHandler("{\"flag\": false}");
            handler.ParseNext();
            handler.EnterObject();
            handler.NextObjectKey();
            Assert.False(handler.GetBool());
        }

        [Fact]
        public void GetBool_IntegerOne_ReturnsTrue()
        {
            var handler = new LookaheadParserHandler("{\"flag\": 1}");
            handler.ParseNext();
            handler.EnterObject();
            handler.NextObjectKey();
            Assert.True(handler.GetBool());
        }

        // ---- PeekType ----

        [Fact]
        public void PeekType_Object()
        {
            var handler = new LookaheadParserHandler("{}");
            handler.ParseNext();
            Assert.Equal(LookaheadParserHandler.kObjectType, handler.PeekType());
        }

        [Fact]
        public void PeekType_Array()
        {
            var handler = new LookaheadParserHandler("[]");
            handler.ParseNext();
            Assert.Equal(LookaheadParserHandler.kArrayType, handler.PeekType());
        }

        [Fact]
        public void PeekType_String()
        {
            var handler = new LookaheadParserHandler("{\"k\": \"v\"}");
            handler.ParseNext();
            handler.EnterObject();
            handler.NextObjectKey();
            Assert.Equal(LookaheadParserHandler.kStringType, handler.PeekType());
        }

        [Fact]
        public void PeekType_Number()
        {
            var handler = new LookaheadParserHandler("{\"k\": 42}");
            handler.ParseNext();
            handler.EnterObject();
            handler.NextObjectKey();
            Assert.Equal(LookaheadParserHandler.kNumberType, handler.PeekType());
        }

        // ---- Skip ----

        [Fact]
        public void Skip_ConsumesValue()
        {
            var handler = new LookaheadParserHandler("{\"a\": {\"nested\": true}, \"b\": 2}");
            handler.ParseNext();
            handler.EnterObject();

            var key1 = handler.NextObjectKey();
            Assert.Equal("a", key1);
            handler.Skip(); // skip the nested object

            var key2 = handler.NextObjectKey();
            Assert.Equal("b", key2);
            Assert.Equal(2, handler.GetInt());
        }

        // ---- Nested structures ----

        [Fact]
        public void NestedObjectInArray()
        {
            var handler = new LookaheadParserHandler("[{\"x\": 1}, {\"x\": 2}]");
            handler.ParseNext();
            handler.EnterArray();

            Assert.True(handler.NextArrayValue());
            handler.EnterObject();
            var key = handler.NextObjectKey();
            Assert.Equal("x", key);
            Assert.Equal(1, handler.GetInt());
            handler.NextObjectKey(); // exit object

            Assert.True(handler.NextArrayValue());
            handler.EnterObject();
            key = handler.NextObjectKey();
            Assert.Equal("x", key);
            Assert.Equal(2, handler.GetInt());
            handler.NextObjectKey(); // exit object

            Assert.False(handler.NextArrayValue()); // end
        }

        // ---- GetRawJson ----

        [Fact]
        public void GetRawJson_ReturnsOriginalInput()
        {
            var json = "{\"test\": true}";
            var handler = new LookaheadParserHandler(json);
            Assert.Equal(json, handler.GetRawJson());
        }
    }
}
