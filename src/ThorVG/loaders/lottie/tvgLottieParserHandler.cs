// Ported from ThorVG/src/loaders/lottie/tvgLottieParserHandler.h and .cpp.
// System.Text.Json supplies incremental tokens; advancement, conversion and
// recovery follow the native SAX wrapper.
using System;
using System.Text;
using System.Text.Json;

namespace ThorVG
{
    public class LookaheadParserHandler
    {
        public enum LookaheadParsingState
        {
            Init = 0, Error, HasNull, HasBool, HasNumber, HasString, HasKey,
            EnteringObject, ExitingObject, EnteringArray, ExitingArray
        }

        public const int kNullType = 0, kFalseType = 1, kTrueType = 2,
                         kObjectType = 3, kArrayType = 4, kStringType = 5, kNumberType = 6;
        protected LookaheadParsingState _state = LookaheadParsingState.Init;
        private static readonly JsonReaderOptions Options = new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = int.MaxValue
        };
        private readonly string _rawJson;
        private readonly byte[] _source;
        private JsonReaderState _readerState = new JsonReaderState(Options);
        private int _offset, _tokenStart, _tokenEnd;
        private bool _parseError, _rootComplete, _preflightInvalid, _boolean;
        private bool _preflightPending = true;
        private string? _string;
        private int _stringStart, _stringLength;
        private bool _stringPending;
        private double _number;
        private int _integer;
        private bool _isInteger;

        protected ReadOnlySpan<byte> SourceBytes => _source;
        protected string? CurrentString
        {
            get
            {
                if (_stringPending)
                {
                    _string = TvgUtf8.Decode(_source.AsSpan(_stringStart, _stringLength));
                    _stringPending = false;
                }
                return _string;
            }
        }

        public LookaheadParserHandler(string json)
        {
            _rawJson = TvgStr.Duplicate(json)!;
            _source = TvgUtf8.Encode(_rawJson);
        }

        private void ValidateInitialQuery()
        {
            _preflightPending = false;
            // Preserve an early Invalid() query without making ordinary
            // incremental parsing scan the complete document a second time.
            try
            {
                var reader = new Utf8JsonReader(_source, Options);
                bool any = false;
                while (reader.Read()) any = true;
                _preflightInvalid = !any;
            }
            catch (JsonException) { _preflightInvalid = true; }
        }

        public bool Invalid()
        {
            if (_preflightPending) ValidateInitialQuery();
            return _preflightInvalid || _state == LookaheadParsingState.Error;
        }

        public void Error()
        {
            TvgCommon.TVGERR("LOTTIE", "Invalid JSON: unexpected or misaligned data fields.");
            _state = LookaheadParsingState.Error;
            ReadNext(); // Native attempts one token of recovery after a type error.
        }

        public bool ParseNext()
        {
            _preflightPending = false;
            _preflightInvalid = false;
            if (ReadNext()) return true;
            Error();
            return false;
        }

        private bool ReadNext()
        {
            if (_parseError) return false;
            if (_rootComplete) return true;
            try
            {
                var reader = new Utf8JsonReader(_source.AsSpan(_offset), true, _readerState);
                if (!reader.Read()) { _parseError = true; return false; }
                _tokenStart = _offset + checked((int)reader.TokenStartIndex);
                _tokenEnd = _offset + checked((int)reader.BytesConsumed);
                _offset = _tokenEnd;
                _readerState = reader.CurrentState;
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject: _state = LookaheadParsingState.EnteringObject; break;
                    case JsonTokenType.EndObject: _state = LookaheadParsingState.ExitingObject; break;
                    case JsonTokenType.StartArray: _state = LookaheadParsingState.EnteringArray; break;
                    case JsonTokenType.EndArray: _state = LookaheadParsingState.ExitingArray; break;
                    case JsonTokenType.PropertyName:
                        if (!ReadTokenString(ref reader)) return false;
                        _state = LookaheadParsingState.HasKey; break;
                    case JsonTokenType.String:
                        if (!ReadTokenString(ref reader)) return false;
                        _state = LookaheadParsingState.HasString; break;
                    case JsonTokenType.True:
                    case JsonTokenType.False:
                        _boolean = reader.TokenType == JsonTokenType.True; _state = LookaheadParsingState.HasBool; break;
                    case JsonTokenType.Null: _state = LookaheadParsingState.HasNull; break;
                    case JsonTokenType.Number:
                        _isInteger = reader.TryGetInt32(out _integer);
                        if (!LottieJsonNumber.TryParse(reader.ValueSpan, out _number)) { _parseError = true; return false; }
                        _state = LookaheadParsingState.HasNumber;
                        break;
                    default: _parseError = true; return false;
                }
                if (reader.CurrentDepth == 0 && reader.TokenType != JsonTokenType.StartObject && reader.TokenType != JsonTokenType.StartArray)
                {
                    for (int i = _offset; i < _source.Length; ++i)
                    {
                        byte c = _source[i];
                        if (c != 32 && c != 9 && c != 10 && c != 13) { _parseError = true; return false; }
                    }
                    _rootComplete = true;
                }
                return true;
            }
            catch (JsonException) { _parseError = true; return false; }
        }

        private bool ReadTokenString(ref Utf8JsonReader reader)
        {
            try
            {
                if (!reader.ValueIsEscaped)
                {
                    // Native SAX strings borrow the insitu input. Defer the
                    // immutable managed string until a caller consumes it so
                    // SkipOut does not allocate every ignored key/value.
                    _stringStart = _tokenStart + 1;
                    _stringLength = reader.ValueSpan.Length;
                    _string = null;
                    _stringPending = true;
                }
                else
                {
                    Span<byte> value = reader.ValueSpan.Length <= 512
                        ? stackalloc byte[reader.ValueSpan.Length] : new byte[reader.ValueSpan.Length];
                    int length = reader.CopyString(value);
                    _string = TvgUtf8.Decode(value.Slice(0, length));
                    _stringPending = false;
                }
                return true;
            }
            catch (InvalidOperationException)
            {
                // Utf8JsonReader defers escaped-surrogate validation until
                // string conversion. Native reports a parse error here.
                _parseError = true;
                return false;
            }
        }

        public bool EnterObject()
        {
            if (_state == LookaheadParsingState.EnteringObject) { ParseNext(); return true; }
            Error(); return false;
        }
        public bool EnterArray()
        {
            if (_state == LookaheadParsingState.EnteringArray) { ParseNext(); return true; }
            Error(); return false;
        }
        public bool NextArrayValue()
        {
            if (_state == LookaheadParsingState.ExitingArray) { ParseNext(); return false; }
            if (_state == LookaheadParsingState.ExitingObject) return false;
            if (_state == LookaheadParsingState.Error || _state == LookaheadParsingState.HasKey) { Error(); return false; }
            return true;
        }
        public string? NextObjectKey()
        {
            if (_state == LookaheadParsingState.HasKey)
            {
                string? result = TvgStr.Duplicate(CurrentString);
                ParseNext(); return result;
            }
            if (_state == LookaheadParsingState.ExitingArray || _state == LookaheadParsingState.EnteringObject) return null;
            if (_state != LookaheadParsingState.ExitingObject) { Error(); return null; }
            ParseNext(); return null;
        }
        public int GetInt()
        {
            if (_state == LookaheadParsingState.HasNumber)
            {
                // Non-Int32 numeric tags violate native Value.GetInt's
                // assertion contract. Retain the managed numeric fallback;
                // there is no defined native return value for those calls.
                int result = _isInteger ? _integer : (int)_number;
                ParseNext(); return result;
            }
            Error(); return 0;
        }
        public float GetFloat()
        {
            if (_state == LookaheadParsingState.HasNumber)
            {
                // Native converts its stored double/integer. Direct Single
                // parsing changes values immediately beside float midpoints.
                float result = (float)_number;
                ParseNext(); return result;
            }
            Error(); return 0;
        }
        public string? GetString()
        {
            if (_state == LookaheadParsingState.HasString)
            {
                string? result = TvgStr.Duplicate(CurrentString);
                ParseNext(); return result;
            }
            Error(); return null;
        }
        public string? GetStringCopy() => TvgStr.Duplicate(GetString());
        public bool GetBool()
        {
            if (_state == LookaheadParsingState.HasBool)
            {
                bool result = _boolean;
                ParseNext(); return result;
            }
            Error(); return false;
        }
        public void GetNull()
        {
            if (_state == LookaheadParsingState.HasNull) { ParseNext(); return; }
            Error();
        }
        public int PeekType() => _state switch
        {
            LookaheadParsingState.HasNull => kNullType,
            LookaheadParsingState.HasBool => _boolean ? kTrueType : kFalseType,
            LookaheadParsingState.HasNumber => kNumberType,
            LookaheadParsingState.HasString or LookaheadParsingState.HasKey => kStringType,
            LookaheadParsingState.EnteringArray => kArrayType,
            LookaheadParsingState.EnteringObject => kObjectType,
            _ => -1
        };
        public bool IsPrimitive() => _state >= LookaheadParsingState.HasNull && _state <= LookaheadParsingState.HasKey;
        public void Skip()
        {
            if (PeekType() == kArrayType) { EnterArray(); SkipOut(1); }
            else if (PeekType() == kObjectType) { EnterObject(); SkipOut(1); }
            else SkipOut(0);
        }
        public void SkipOut(int depth)
        {
            do
            {
                if (_state == LookaheadParsingState.EnteringArray || _state == LookaheadParsingState.EnteringObject) ++depth;
                else if (_state == LookaheadParsingState.ExitingArray || _state == LookaheadParsingState.ExitingObject) --depth;
                else if (_state == LookaheadParsingState.Error) return;
                ParseNext();
            }
            while (depth > 0);
        }
        public string GetRawJson() => _rawJson;
        public int GetPos() => _offset; // Native src_ pointer as a UTF-8 byte offset.
        public string? GetCurrentValueRawText()
        {
            if (PeekType() < 0 || _state == LookaheadParsingState.HasKey) return null;
            if (IsPrimitive()) return TvgUtf8.Decode(_source.AsSpan(_tokenStart, _tokenEnd - _tokenStart));
            try
            {
                var reader = new Utf8JsonReader(_source.AsSpan(_tokenStart), Options);
                using var value = JsonDocument.ParseValue(ref reader);
                return TvgUtf8.Decode(_source.AsSpan(_tokenStart, checked((int)reader.BytesConsumed)));
            }
            catch (JsonException) { Error(); return null; }
        }
        public string? PeekStringProperty(string keyName)
        {
            if (_state != LookaheadParsingState.EnteringObject) return null;
            try
            {
                var reader = new Utf8JsonReader(_source.AsSpan(_tokenStart), Options);
                if (!reader.Read()) return null;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) return null;
                    bool found = TvgStr.Equal(reader.GetString(), keyName);
                    if (!reader.Read()) return null;
                    if (found) return reader.TokenType == JsonTokenType.String ? TvgStr.Duplicate(reader.GetString()) : null;
                    reader.Skip();
                }
            }
            catch (JsonException) { Error(); }
            return null;
        }
    }
}
