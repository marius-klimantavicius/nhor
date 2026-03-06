// Ported from ThorVG/src/loaders/lottie/tvgLottieParserHandler.h and .cpp
// Replaces RapidJSON with System.Text.Json JsonDocument-based DOM walking.

using System.Collections.Generic;
using System.Text.Json;

namespace ThorVG
{
    /// <summary>
    /// Lookahead parser handler that walks a pre-parsed JsonDocument.
    /// Replaces the C++ RapidJSON SAX-based streaming approach with a DOM walker.
    /// The public API mirrors the C++ LookaheadParserHandler methods.
    /// </summary>
    public class LookaheadParserHandler
    {
        public enum LookaheadParsingState
        {
            Init = 0,
            Error,
            HasNull,
            HasBool,
            HasNumber,
            HasString,
            HasKey,
            EnteringObject,
            ExitingObject,
            EnteringArray,
            ExitingArray
        }

        // Peek types (matching RapidJSON type constants)
        public const int kNullType = 0;
        public const int kFalseType = 1;
        public const int kTrueType = 2;
        public const int kObjectType = 3;
        public const int kArrayType = 4;
        public const int kStringType = 5;
        public const int kNumberType = 6;

        protected JsonDocument? _doc;
        protected LookaheadParsingState _state = LookaheadParsingState.Init;

        // Stack-based DOM walker
        private readonly struct WalkerFrame
        {
            public readonly JsonElement Element;
            public readonly bool IsObject;
            public readonly JsonElement.ObjectEnumerator ObjEnum;
            public readonly JsonElement.ArrayEnumerator ArrEnum;
            public readonly bool HasCurrent;

            public WalkerFrame(JsonElement element, bool isObject,
                               JsonElement.ObjectEnumerator objEnum,
                               JsonElement.ArrayEnumerator arrEnum,
                               bool hasCurrent)
            {
                Element = element;
                IsObject = isObject;
                ObjEnum = objEnum;
                ArrEnum = arrEnum;
                HasCurrent = hasCurrent;
            }
        }

        // Current value being consumed
        private JsonElement _currentValue;
        private string? _currentKey;
        private bool _hasCurrentValue;

        // Object/array iteration stacks
        private readonly Stack<JsonElement.ObjectEnumerator> _objStack = new();
        private readonly Stack<JsonElement.ArrayEnumerator> _arrStack = new();
        private readonly Stack<bool> _isObjStack = new(); // true = object context, false = array context

        // For tracking where we are in raw JSON (used by captureType/getPos)
        private string? _rawJson;
        #pragma warning disable CS0414 // field assigned but never used
        private int _rawPos;
        #pragma warning restore CS0414

        public LookaheadParserHandler(string json)
        {
            _rawJson = json;
            _rawPos = 0;
            try
            {
                _doc = JsonDocument.Parse(json, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });
                _state = LookaheadParsingState.Init;
            }
            catch (JsonException)
            {
                _state = LookaheadParsingState.Error;
            }
        }

        public bool Invalid()
        {
            return _state == LookaheadParsingState.Error;
        }

        public void Error()
        {
            TvgCommon.TVGERR("LOTTIE", "Invalid JSON: unexpected or misaligned data fields.");
            _state = LookaheadParsingState.Error;
        }

        public bool ParseNext()
        {
            if (_doc == null)
            {
                Error();
                return false;
            }
            // Set up the root element
            _currentValue = _doc.RootElement;
            _hasCurrentValue = true;
            SetStateFromElement(_currentValue);
            return true;
        }

        public bool EnterObject()
        {
            if (!_hasCurrentValue || _currentValue.ValueKind != JsonValueKind.Object)
            {
                Error();
                return false;
            }
            var enumerator = _currentValue.EnumerateObject();
            _objStack.Push(enumerator);
            _isObjStack.Push(true);
            _hasCurrentValue = false;
            return true;
        }

        public bool EnterArray()
        {
            if (!_hasCurrentValue || _currentValue.ValueKind != JsonValueKind.Array)
            {
                Error();
                return false;
            }
            var enumerator = _currentValue.EnumerateArray();
            _arrStack.Push(enumerator);
            _isObjStack.Push(false);
            _hasCurrentValue = false;
            return true;
        }

        public string? NextObjectKey()
        {
            if (_isObjStack.Count == 0 || !_isObjStack.Peek())
            {
                // Not in an object context - return null gracefully if we just
                // exited an object/array or are entering a new container.
                // This handles the case where a sub-parser (e.g. ParseGroup)
                // already fully consumed the object, and the caller tries to
                // iterate remaining keys.
                if (_state == LookaheadParsingState.ExitingArray ||
                    _state == LookaheadParsingState.ExitingObject ||
                    _state == LookaheadParsingState.EnteringObject)
                    return null;
                Error();
                return null;
            }

            var enumerator = _objStack.Pop();
            if (enumerator.MoveNext())
            {
                var prop = enumerator.Current;
                _currentKey = prop.Name;
                _currentValue = prop.Value;
                _hasCurrentValue = true;
                SetStateFromElement(_currentValue);
                _objStack.Push(enumerator);
                return _currentKey;
            }
            else
            {
                // Exiting object - pop the context flag but do NOT push
                // the exhausted enumerator back onto _objStack
                _isObjStack.Pop();
                _state = LookaheadParsingState.ExitingObject;
                _hasCurrentValue = false;
                return null;
            }
        }

        public bool NextArrayValue()
        {
            // Not in an array context — handle gracefully if we just
            // exited an object/array from a nested sub-parser.
            if (_isObjStack.Count == 0 || _isObjStack.Peek())
            {
                if (_state == LookaheadParsingState.ExitingObject ||
                    _state == LookaheadParsingState.ExitingArray)
                    return false;
                Error();
                return false;
            }

            // If AutoAdvance already provided the next value, just return true.
            if (_hasCurrentValue) return true;

            // Advance the array enumerator (first call after EnterArray,
            // or after AutoAdvance found the array exhausted).
            var enumerator = _arrStack.Pop();
            if (enumerator.MoveNext())
            {
                _currentValue = enumerator.Current;
                _hasCurrentValue = true;
                SetStateFromElement(_currentValue);
                _arrStack.Push(enumerator);
                return true;
            }
            else
            {
                // Exiting array — pop context and set state for callers that
                // check it (e.g., graceful returns in NextObjectKey).
                _isObjStack.Pop();
                _state = LookaheadParsingState.ExitingArray;
                _hasCurrentValue = false;
                return false;
            }
        }

        public int GetInt()
        {
            if (!_hasCurrentValue)
            {
                Error();
                return 0;
            }
            _hasCurrentValue = false;
            try
            {
                if (_currentValue.ValueKind == JsonValueKind.Number)
                {
                    int result;
                    if (_currentValue.TryGetInt32(out int i)) result = i;
                    // Lottie JSON sometimes stores integers as floats (e.g. 1.0)
                    else result = (int)_currentValue.GetDouble();
                    AutoAdvance();
                    return result;
                }
                if (_currentValue.ValueKind == JsonValueKind.True) { AutoAdvance(); return 1; }
                if (_currentValue.ValueKind == JsonValueKind.False) { AutoAdvance(); return 0; }
            }
            catch { }
            Error();
            return 0;
        }

        public float GetFloat()
        {
            if (!_hasCurrentValue)
            {
                Error();
                return 0;
            }
            _hasCurrentValue = false;
            try
            {
                if (_currentValue.ValueKind == JsonValueKind.Number)
                {
                    float result;
                    if (_currentValue.TryGetSingle(out float f)) result = f;
                    else result = (float)_currentValue.GetDouble();
                    AutoAdvance();
                    return result;
                }
            }
            catch { }
            Error();
            return 0;
        }

        public string? GetString()
        {
            if (!_hasCurrentValue)
            {
                Error();
                return null;
            }
            _hasCurrentValue = false;
            if (_currentValue.ValueKind == JsonValueKind.String)
            {
                var result = _currentValue.GetString();
                AutoAdvance();
                return result;
            }
            Error();
            return null;
        }

        public string? GetStringCopy()
        {
            var str = GetString();
            return str != null ? new string(str) : null;
        }

        public bool GetBool()
        {
            if (!_hasCurrentValue)
            {
                Error();
                return false;
            }
            _hasCurrentValue = false;
            if (_currentValue.ValueKind == JsonValueKind.True) { AutoAdvance(); return true; }
            if (_currentValue.ValueKind == JsonValueKind.False) { AutoAdvance(); return false; }
            // Some Lottie files use 0/1 for booleans
            if (_currentValue.ValueKind == JsonValueKind.Number)
            {
                var result = _currentValue.GetInt32() != 0;
                AutoAdvance();
                return result;
            }
            Error();
            return false;
        }

        public void GetNull()
        {
            if (!_hasCurrentValue || _currentValue.ValueKind != JsonValueKind.Null)
            {
                Error();
                return;
            }
            _hasCurrentValue = false;
            AutoAdvance();
        }

        public int PeekType()
        {
            if (!_hasCurrentValue) return -1;
            return _currentValue.ValueKind switch
            {
                JsonValueKind.Null => kNullType,
                JsonValueKind.False => kFalseType,
                JsonValueKind.True => kTrueType,
                JsonValueKind.Object => kObjectType,
                JsonValueKind.Array => kArrayType,
                JsonValueKind.String => kStringType,
                JsonValueKind.Number => kNumberType,
                _ => -1
            };
        }

        public bool IsPrimitive()
        {
            return _hasCurrentValue &&
                   _currentValue.ValueKind != JsonValueKind.Object &&
                   _currentValue.ValueKind != JsonValueKind.Array;
        }

        public void Skip()
        {
            if (!_hasCurrentValue)
            {
                // try to advance past current object/array
                return;
            }
            // Simply consume the current value
            _hasCurrentValue = false;
            AutoAdvance();
        }

        public void SkipOut(int depth)
        {
            // For DOM-based approach: skip remaining keys/values until we exit 'depth' levels
            while (depth > 0)
            {
                if (_isObjStack.Count == 0) break;
                if (_isObjStack.Peek()) // object context
                {
                    if (NextObjectKey() == null)
                        depth--;
                    else
                        Skip();
                }
                else // array context
                {
                    if (NextArrayValue())
                        Skip();
                    else
                        depth--;
                }
            }
        }

        /// <summary>
        /// Returns the raw JSON string (used for captureSlots).
        /// </summary>
        public string? GetRawJson() => _rawJson;

        /// <summary>
        /// Returns the raw text of the current value (for captureType lookahead).
        /// </summary>
        public string? GetCurrentValueRawText()
        {
            if (_hasCurrentValue) return _currentValue.GetRawText();
            return null;
        }

        /// <summary>
        /// Peek at a string property in the current value (before EnterObject).
        /// This is used as the DOM equivalent of C++ captureType(): it finds
        /// the "ty" key value in a JSON object without consuming any tokens.
        /// Must be called BEFORE EnterObject() while _currentValue still
        /// points to the object element.
        /// Returns null if not found or if current value is not an object.
        /// </summary>
        public string? PeekStringProperty(string keyName)
        {
            if (!_hasCurrentValue || _currentValue.ValueKind != JsonValueKind.Object) return null;
            if (_currentValue.TryGetProperty(keyName, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
            return null;
        }

        /// <summary>
        /// Mirrors C++ parseNext(): after consuming a primitive value, advance
        /// the current array enumerator (if in an array context) so the next
        /// value is immediately available for consecutive GetFloat()/GetInt()/etc. calls.
        /// In object context, NextObjectKey() handles advancement, so we skip.
        /// Note: Does NOT pop context when array is exhausted — NextArrayValue() handles that.
        /// </summary>
        private void AutoAdvance()
        {
            if (_isObjStack.Count == 0) return;
            if (_isObjStack.Peek()) return; // in object context — NextObjectKey handles it

            // In array context: advance the enumerator
            if (_arrStack.Count == 0) return;
            var enumerator = _arrStack.Pop();
            if (enumerator.MoveNext())
            {
                _currentValue = enumerator.Current;
                _hasCurrentValue = true;
                SetStateFromElement(_currentValue);
                _arrStack.Push(enumerator);
            }
            else
            {
                // Array exhausted — leave context in place for NextArrayValue() to clean up.
                // Push the exhausted enumerator back so NextArrayValue() can pop it properly.
                _arrStack.Push(enumerator);
                _hasCurrentValue = false;
            }
        }

        private void SetStateFromElement(JsonElement element)
        {
            _state = element.ValueKind switch
            {
                JsonValueKind.Null => LookaheadParsingState.HasNull,
                JsonValueKind.True => LookaheadParsingState.HasBool,
                JsonValueKind.False => LookaheadParsingState.HasBool,
                JsonValueKind.Number => LookaheadParsingState.HasNumber,
                JsonValueKind.String => LookaheadParsingState.HasString,
                JsonValueKind.Object => LookaheadParsingState.EnteringObject,
                JsonValueKind.Array => LookaheadParsingState.EnteringArray,
                _ => LookaheadParsingState.Error
            };
        }
    }
}
