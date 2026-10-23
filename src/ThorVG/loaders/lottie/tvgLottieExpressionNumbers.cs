// Numeric adaptation for the pinned JerryScript float32 configuration. Jint
// still parses and executes JavaScript; the AST identifies numeric operations
// without changing strings, comments, evaluation order, or member receivers.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Acornima;
using Acornima.Ast;
using Jint;
using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace ThorVG;

internal static class LottieExpressionNumbers
{
    private const string RoundName = "$__tvg_float";
    private const string NumberName = "$__tvg_number";
    private const string UpdateName = "$__tvg_update";
    private const string UpdateBindingName = "$__tvg_update_binding";
    private const string AssignName = "$__tvg_assign";
    private const string BinaryName = "$__tvg_binary";
    private static readonly ConditionalWeakTable<LottieExpression, AdaptedCode> Codes = new();
    private sealed class AdaptedCode
    {
        public string? Source;
        public Prepared<Script> Script;
    }

    public static Prepared<Script> Prepare(LottieExpression expression)
    {
        var code = Codes.GetValue(expression, _ => new AdaptedCode());
        if (code.Source != expression.code)
        {
            code.Script = LottieExpressionRuntime.Prepare(expression.code!);
            code.Source = expression.code;
        }
        return code.Script;
    }

    public static void Install(Engine engine)
    {
        LottieExpressionRuntime.SetHelper(engine, RoundName, new ClrFunction(engine, RoundName, (_, args) =>
            args[0].IsNumber() ? new JsNumber((float)args[0].AsNumber()) : args[0]));
        LottieExpressionRuntime.SetHelper(engine, NumberName, new ClrFunction(engine, NumberName, (_, args) =>
        {
            return new JsNumber(LottieExpressionMath.ToNumber(args[0]));
        }));
        engine.SetValue("parseFloat", new ClrFunction(engine, "parseFloat", (_, args) =>
            new JsNumber(ParseFloat(Jint.Runtime.TypeConverter.ToString(args.Length == 0 ? JsValue.Undefined : args[0]))), 1, PropertyFlag.Configurable));
        engine.SetValue("parseInt", new ClrFunction(engine, "parseInt", (_, args) =>
            new JsNumber(ParseInt(Jint.Runtime.TypeConverter.ToString(args.Length == 0 ? JsValue.Undefined : args[0]),
                args.Length > 1 ? Jint.Runtime.TypeConverter.ToInt32(new JsNumber(LottieExpressionMath.ToNumber(args[1]))) : 0)), 2, PropertyFlag.Configurable));
        engine.SetValue("isFinite", new ClrFunction(engine, "isFinite", (_, args) =>
            Bool(float.IsFinite(LottieExpressionMath.ToNumber(args.Length == 0 ? JsValue.Undefined : args[0]))), 1, PropertyFlag.Configurable));
        engine.SetValue("isNaN", new ClrFunction(engine, "isNaN", (_, args) =>
            Bool(float.IsNaN(LottieExpressionMath.ToNumber(args.Length == 0 ? JsValue.Undefined : args[0]))), 1, PropertyFlag.Configurable));
        LottieExpressionRuntime.EvaluateOwned(engine, "(function(old){function Number(value){\"use strict\";var n=arguments.length?" + NumberName + "(value):0;return new.target?new old(n):n;}" +
            "var p=Object.getOwnPropertyDescriptors(old);p.MAX_VALUE.value=3.4028234663852886e38;p.MIN_VALUE.value=1.1754943508222875e-38;" +
            "p.EPSILON.value=1.1920928955078125e-7;p.MAX_SAFE_INTEGER.value=16777215;p.MIN_SAFE_INTEGER.value=-16777215;Object.defineProperties(Number,p);" +
            "Number.isSafeInteger=function(x){return typeof x==='number'&&Number.isInteger(x)&&Math.abs(x)<=16777215;};" +
            "Number.parseFloat=parseFloat;Number.parseInt=parseInt;Number.prototype.constructor=Number;globalThis.Number=Number;})(Number);");
        LottieExpressionMath.Install(engine);
        LottieExpressionBuiltins.Install(engine);
        LottieExpressionNumberFormat.Install(engine);
        LottieExpressionConstructors.Install(engine);
        LottieExpressionErrors.Install(engine);
        // This vendored JerryScript deliberately has no JSON global (its global
        // table and built-in inventory omit it). Keep the same expression error
        // and fallback behavior; System.Text.Json remains the Lottie file parser.
        engine.Execute("delete globalThis.JSON;delete globalThis.BigInt;delete globalThis.BigInt64Array;delete globalThis.BigUint64Array;" +
            "delete globalThis.WeakRef;delete globalThis.FinalizationRegistry;delete globalThis.SharedArrayBuffer;delete globalThis.Atomics;");
        LottieExpressionRuntime.SetHelper(engine, BinaryName, new ClrFunction(engine, BinaryName, (_, args) =>
            Binary(args[0], args[1], args[2].AsString())));
        var helpers = LottieExpressionRuntime.EvaluateOwned(engine,
            "(function(){return [function(o,k,s,p){var old=" + NumberName + "(o[k]);var v=" + RoundName + "(old+s);o[k]=v;return p?v:old;}," +
            "function(g,s,step,p){var old=" + NumberName + "(g());var v=" + RoundName + "(old+step);s(v);return p?v:old;}," +
            "function(o,k,r,op){var a=o[k],b=r();return o[k]=" + BinaryName + "(a,b,op);}," +
            "function(o,k,s,p){'use strict';var old=" + NumberName + "(o[k]);var v=" + RoundName + "(old+s);o[k]=v;return p?v:old;}," +
            "function(o,k,r,op){'use strict';var a=o[k],b=r();return o[k]=" + BinaryName + "(a,b,op);}];})()").AsArray();
        LottieExpressionRuntime.SetHelper(engine, UpdateName, helpers.Get("0"));
        LottieExpressionRuntime.SetHelper(engine, UpdateBindingName, helpers.Get("1"));
        LottieExpressionRuntime.SetHelper(engine, AssignName, helpers.Get("2"));
        LottieExpressionRuntime.SetHelper(engine, "$__tvg_update_strict", helpers.Get("3"));
        LottieExpressionRuntime.SetHelper(engine, "$__tvg_assign_strict", helpers.Get("4"));
        // The pinned global table predates globalThis. Install adaptations
        // above while it is available, then preserve native name resolution.
        LottieExpressionProfile.Apply(engine);
    }

    private static JsBoolean Bool(bool value) => value ? JsBoolean.True : JsBoolean.False;

    private static JsValue Binary(JsValue lhs, JsValue rhs, string operation)
    {
        if (operation is "==" or "!=")
        {
            bool equal = AbstractEqual(lhs, rhs);
            return Bool(operation == "==" ? equal : !equal);
        }
        if (operation is "<" or "<=" or ">" or ">=")
        {
            // All four operators evaluate/GetValue left then right. Their
            // abstract-relational operand reversal also preserves this
            // primitive-conversion order through its leftFirst flag.
            lhs = Jint.Runtime.TypeConverter.ToPrimitive(lhs, Jint.Runtime.Types.Number);
            rhs = Jint.Runtime.TypeConverter.ToPrimitive(rhs, Jint.Runtime.Types.Number);
            if (lhs.IsString() && rhs.IsString())
            {
                int order = string.CompareOrdinal(lhs.AsString(), rhs.AsString());
                return Bool(operation switch { "<" => order < 0, "<=" => order <= 0, ">" => order > 0, _ => order >= 0 });
            }
            float left = LottieExpressionMath.ToNumber(lhs), right = LottieExpressionMath.ToNumber(rhs);
            return Bool(operation switch { "<" => left < right, "<=" => left <= right, ">" => left > right, _ => left >= right });
        }
        if (operation == "+")
        {
            // The operands have already been evaluated, but primitive
            // conversion happens left then right, exactly once per operand.
            lhs = Jint.Runtime.TypeConverter.ToPrimitive(lhs);
            rhs = Jint.Runtime.TypeConverter.ToPrimitive(rhs);
            if (lhs.IsString() || rhs.IsString())
                return new JsString(Jint.Runtime.TypeConverter.ToString(lhs) + Jint.Runtime.TypeConverter.ToString(rhs));
        }
        float a = LottieExpressionMath.ToNumber(lhs);
        float b = LottieExpressionMath.ToNumber(rhs);
        int Signed(float value) => Jint.Runtime.TypeConverter.ToInt32(new JsNumber(value));
        uint Unsigned(float value) => Jint.Runtime.TypeConverter.ToUint32(new JsNumber(value));
        float result = operation switch
        {
            "+" => a + b,
            "-" => a - b,
            "*" => a * b,
            "/" => a / b,
            "%" => a % b,
            "**" => LottieExpressionMath.Pow(a, b),
            "<<" => Signed(a) << (Signed(b) & 31),
            ">>" => Signed(a) >> (Signed(b) & 31),
            ">>>" => Unsigned(a) >> (Signed(b) & 31),
            "&" => Signed(a) & Signed(b),
            "|" => Signed(a) | Signed(b),
            "^" => Signed(a) ^ Signed(b),
            _ => throw new InvalidOperationException("Unknown numeric operator.")
        };
        return new JsNumber(result);
    }

    // Abstract equality keeps same-type identity/string comparison and only
    // applies the pinned float32 string conversion where ECMAScript requires
    // ToNumber. In particular, objects are not converted for null/undefined
    // or object/object comparisons, and symbols retain their identities.
    private static bool AbstractEqual(JsValue left, JsValue right)
    {
        if (left.Type == right.Type) return left.Equals(right);
        if ((left.IsNull() || left.IsUndefined()) && (right.IsNull() || right.IsUndefined())) return true;
        if (left.IsNumber() && right.IsString()) return left.AsNumber() == Parse(right.AsString());
        if (left.IsString() && right.IsNumber()) return Parse(left.AsString()) == right.AsNumber();
        if (left.IsBoolean()) return AbstractEqual(new JsNumber(left.AsBoolean() ? 1 : 0), right);
        if (right.IsBoolean()) return AbstractEqual(left, new JsNumber(right.AsBoolean() ? 1 : 0));
        if (left.IsObject() && (right.IsString() || right.IsNumber() || right.IsSymbol()))
            return AbstractEqual(Jint.Runtime.TypeConverter.ToPrimitive(left), right);
        if (right.IsObject() && (left.IsString() || left.IsNumber() || left.IsSymbol()))
            return AbstractEqual(left, Jint.Runtime.TypeConverter.ToPrimitive(right));
        return false;
    }

    // ecma_utf8_string_to_number at the pinned revision: keep nine significant
    // digits and exponentiation by squaring in float32 (including each rounding).
    internal static float Parse(string source, bool literal = false)
    {
        var text = TrimSpace(source.AsSpan(), true);
        if (!literal && text.Contains('_')) return float.NaN;
        if (text.IsEmpty) return 0;
        var at = 0;
        var negative = false;
        if (text[0] is '+' or '-') { negative = text[0] == '-'; ++at; }
        if (text[at..].SequenceEqual("Infinity")) return negative ? float.NegativeInfinity : float.PositiveInfinity;
        if (at == 0 && text.Length > 2 && text[0] == '0')
        {
            var radix = char.ToLowerInvariant(text[1]) switch { 'x' => 16, 'b' => 2, 'o' => 8, _ => 10 };
            if (radix != 10)
            {
                float result = 0;
                for (var n = 2; n < text.Length; ++n)
                {
                    if (text[n] == '_') continue;
                    var digit = text[n] is >= '0' and <= '9' ? text[n] - '0' : char.ToLowerInvariant(text[n]) - 'a' + 10;
                    if (digit < 0 || digit >= radix) return float.NaN;
                    result = result * radix + digit;
                }
                return result;
            }
        }
        uint significand = 0;
        var digits = 0;
        var exponent = 0;
        var any = false;
        var fraction = false;
        while (at < text.Length)
        {
            var c = text[at];
            if (c == '_') { ++at; continue; }
            if (c == '.' && !fraction) { fraction = true; ++at; continue; }
            if (c is < '0' or > '9') break;
            ++at;
            any = true;
            var digit = (uint)(c - '0');
            if (digits == 0 && digit == 0) { if (fraction) --exponent; continue; }
            if (digits < 9)
            {
                significand = significand * 10 + digit;
                ++digits;
                if (fraction) --exponent;
            }
            else if (!fraction) ++exponent;
        }
        if (at < text.Length && text[at] is 'e' or 'E')
        {
            ++at;
            var direction = 1;
            if (at < text.Length && text[at] is '+' or '-') { if (text[at] == '-') direction = -1; ++at; }
            if (at == text.Length || text[at] is < '0' or > '9') return float.NaN;
            var e = 0;
            while (at < text.Length)
            {
                if (text[at] == '_') { ++at; continue; }
                if (text[at] is < '0' or > '9') break;
                e = e * 10 + text[at++] - '0';
                if (direction * e > 38) return negative ? float.NegativeInfinity : float.PositiveInfinity;
                if (direction * e < -45) return negative ? -0.0f : 0.0f;
            }
            exponent += direction * e;
        }
        if (!any || at != text.Length) return float.NaN;
        if (significand == 0) return negative ? -0.0f : 0;
        float number = significand;
        float m = exponent < 0 ? 0.1f : 10;
        var magnitude = Math.Abs(exponent);
        while (magnitude != 0)
        {
            if ((magnitude & 1) != 0) number *= m;
            m *= m;
            magnitude /= 2;
        }
        // The pinned converter applies the sign only to zero/infinity.
        return number;
    }

    internal static string Rewrite(Script script, string source, string prefix) => Emit(script, source, prefix);

    private static string Emit(Node node, string source, string prefix, bool reference = false, bool strict = false)
    {
        if (node is Script script) strict = script.Strict;
        if (node is IFunction function && function.Body is FunctionBody functionBody) strict = functionBody.Strict;
        if (node is ClassBody) strict = true;
        string Text(Node child, bool asReference = false) => Emit(child, source, prefix, asReference, strict);
        string RoundName = prefix + "float", NumberName = prefix + "number", UpdateName = prefix + (strict ? "update_strict" : "update");
        string UpdateBindingName = prefix + "update_binding", AssignName = prefix + (strict ? "assign_strict" : "assign"), BinaryName = prefix + "binary";
        if (node is CatchClause handler && handler.Param != null)
        {
            string body = Text(handler.Body);
            string normalize = prefix + "normalize_error";
            if (handler.Param is Identifier)
            {
                string parameter = Text(handler.Param);
                return "catch(" + parameter + "){void (" + parameter + "=" + normalize + "(" + parameter + "));" + body[1..];
            }
            string temporary = prefix + "caught_" + handler.Start;
            return "catch(" + temporary + "){try{throw " + normalize + "(" + temporary + ");}catch(" + Text(handler.Param) + ")" + body + "}";
        }
        if (node is BigIntLiteral) throw new NotSupportedException("The pinned expression runtime has no BigInt literals.");
        // lexer_construct_regexp_object raises PARSER_ERR_UNSUPPORTED_REGEXP
        // when this native profile's JERRY_BUILTIN_REGEXP is disabled.
        if (node is RegExpLiteral) throw new NotSupportedException("The pinned expression runtime has no regular expression literals.");
        if (node is NumericLiteral literal)
        {
            var raw = source[node.Start..node.End];
            var value = raw.Length > 1 && raw[0] == '0' && raw[1] is >= '0' and <= '7' && raw.IndexOfAny(['.', 'e', 'E']) < 0 ? (float)literal.Value : Parse(raw, true);
            return double.IsNaN(value) ? "NaN" : double.IsPositiveInfinity(value) ? "Infinity" :
                ((double)value).ToString("R", CultureInfo.InvariantCulture);
        }
        if (node is UpdateExpression update)
        {
            var step = update.Operator == Operator.Increment ? "1" : "-1";
            var prefixFlag = update.Prefix ? "true" : "false";
            if (update.Argument is MemberExpression member)
                return UpdateName + "((" + Text(member.Object) + ")," + Key(member, source, prefix, strict) + "," + step + "," + prefixFlag + ")";
            var target = Text(update.Argument, true);
            var parameter = prefix + "value";
            return UpdateBindingName + "(()=>" + target + "," + parameter + "=>" + target + "=" + parameter + "," + step + "," + prefixFlag + ")";
        }
        if (node is AssignmentExpression assignment && assignment.Operator.ToString() != "Assignment")
        {
            var op = Token(assignment.Operator);
            if (op is "+" or "-" or "*" or "/" or "%" or "**" or "<<" or ">>" or ">>>" or "&" or "|" or "^")
            {
                var right = Text(assignment.Right);
                if (assignment.Left is MemberExpression member)
                    return AssignName + "((" + Text(member.Object) + ")," + Key(member, source, prefix, strict) + ",()=> (" + right + "),'" + op + "')";
                var left = Text(assignment.Left, true);
                return "(" + left + "=" + BinaryName + "(" + left + "," + right + ",'" + op + "'))";
            }
        }
        if (node is AssignmentExpression simple && simple.Operator.ToString() == "Assignment" &&
            simple.Left is MemberExpression assignmentTarget && assignmentTarget.Object is not Super)
            return prefix + "store((" + Text(assignmentTarget.Object) + ")," + Key(assignmentTarget, source, prefix, strict) + ",(" +
                Text(simple.Right) + ")," + (strict ? "true" : "false") + ")";
        if (node is BinaryExpression operation && Token(operation.Operator) is { Length: > 0 } binaryOp)
            return BinaryName + "((" + Text(operation.Left) + "),(" + Text(operation.Right) + "),'" + binaryOp + "')";
        var output = new StringBuilder();
        var cursor = node.Start;
        foreach (var child in node.ChildNodes)
        {
            output.Append(source, cursor, child.Start - cursor);
            var asReference = node is AssignmentExpression a && ReferenceEquals(child, a.Left) ||
                node is CallExpression call && ReferenceEquals(child, call.Callee) ||
                node is NewExpression construct && ReferenceEquals(child, construct.Callee) ||
                node is UnaryExpression unary && unary.Operator == Operator.Delete ||
                node is Property p && ReferenceEquals(child, p.Key) && !p.Computed;
            output.Append(node is Property key && ReferenceEquals(child, key.Key) && !key.Computed && child is NumericLiteral ?
                "[" + Text(child) + "]" : Text(child, asReference));
            cursor = child.End;
        }
        output.Append(source, cursor, node.End - cursor);
        var expression = output.ToString();
        var wrap = node is BinaryExpression binary && Token(binary.Operator).Length != 0 ||
            node is CallExpression || node is MemberExpression && !reference;
        if (node is UnaryExpression sign && sign.Operator is Operator.UnaryPlus or Operator.UnaryNegation or Operator.BitwiseNot)
            return RoundName + "(" + (sign.Operator == Operator.UnaryPlus ? "+" : sign.Operator == Operator.UnaryNegation ? "-" : "~") + NumberName + "((" + Text(sign.Argument) + ")))";
        return wrap ? RoundName + "(" + expression + ")" : expression;
    }

    private static string Key(MemberExpression member, string source, string prefix, bool strict) => member.Computed ? "(" + Emit(member.Property, source, prefix, strict: strict) + ")" :
        "'" + source[member.Property.Start..member.Property.End] + "'";

    private static string Token(Operator op) => op switch
    {
        Operator.Addition or Operator.AdditionAssignment => "+",
        Operator.Subtraction or Operator.SubtractionAssignment => "-",
        Operator.Multiplication or Operator.MultiplicationAssignment => "*",
        Operator.Division or Operator.DivisionAssignment => "/",
        Operator.Remainder or Operator.RemainderAssignment => "%",
        Operator.Exponentiation or Operator.ExponentiationAssignment => "**",
        Operator.LeftShift or Operator.LeftShiftAssignment => "<<",
        Operator.RightShift or Operator.RightShiftAssignment => ">>",
        Operator.UnsignedRightShift or Operator.UnsignedRightShiftAssignment => ">>>",
        Operator.BitwiseAnd or Operator.BitwiseAndAssignment => "&",
        Operator.BitwiseOr or Operator.BitwiseOrAssignment => "|",
        Operator.BitwiseXor or Operator.BitwiseXorAssignment => "^",
        Operator.Equality => "==",
        Operator.Inequality => "!=",
        Operator.LessThan => "<",
        Operator.LessThanOrEqual => "<=",
        Operator.GreaterThan => ">",
        Operator.GreaterThanOrEqual => ">=",
        _ => ""
    };

    private static float ParseFloat(string source)
    {
        var text = TrimSpace(source.AsSpan(), false);
        var negative = text.Length > 0 && text[0] == '-';
        if (text.Length > 0 && text[0] is '+' or '-') text = text[1..];
        if (text.StartsWith("Infinity")) return negative ? float.NegativeInfinity : float.PositiveInfinity;
        var at = 0;
        while (at < text.Length && char.IsAsciiDigit(text[at])) ++at;
        if (at < text.Length && text[at] == '.') { ++at; while (at < text.Length && char.IsAsciiDigit(text[at])) ++at; }
        var end = at;
        if (at < text.Length && text[at] is 'e' or 'E')
        {
            ++at;
            if (at < text.Length && text[at] is '+' or '-') ++at;
            var digits = at;
            while (at < text.Length && char.IsAsciiDigit(text[at])) ++at;
            if (at != digits) end = at;
        }
        var value = Parse(text[..end].ToString());
        return end == 0 ? float.NaN : negative ? -value : value;
    }

    private static float ParseInt(string source, int radix)
    {
        var text = TrimSpace(source.AsSpan(), false);
        var negative = text.Length > 0 && text[0] == '-';
        if (text.Length > 0 && text[0] is '+' or '-') text = text[1..];
        if (radix != 0 && (radix < 2 || radix > 36)) return float.NaN;
        var prefix = radix is 0 or 16;
        if (radix == 0) radix = 10;
        if (prefix && text.Length > 1 && text[0] == '0' && text[1] is 'x' or 'X') { text = text[2..]; radix = 16; }
        var at = 0;
        float value = 0;
        while (at < text.Length)
        {
            var c = char.ToLowerInvariant(text[at]);
            var digit = c is >= '0' and <= '9' ? c - '0' : c is >= 'a' and <= 'z' ? c - 'a' + 10 : radix;
            if (digit >= radix) break;
            value *= radix;
            value += digit;
            ++at;
        }
        return at == 0 ? float.NaN : negative ? -value : value;
    }

    private static ReadOnlySpan<char> TrimSpace(ReadOnlySpan<char> text, bool trailing)
    {
        int first = 0, end = text.Length;
        while (first < end && WhiteSpace(text[first])) ++first;
        if (trailing) while (end > first && WhiteSpace(text[end - 1])) --end;
        return text[first..end];
    }

    // lit_char_is_white_space and lit-unicode-ranges.inc.h at the pinned
    // revision. NEL(U+0085) is excluded; BOM(U+FEFF) is included.
    private static bool WhiteSpace(char ch) =>
        ch is >= '\t' and <= '\r' or ' ' or '\u00a0' or '\u1680' or
            >= '\u2000' and <= '\u200a' or '\u2028' or '\u2029' or '\u202f' or '\u205f' or '\u3000' or '\ufeff';
}
