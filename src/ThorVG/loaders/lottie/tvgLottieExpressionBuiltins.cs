// Native numeric coercion at supported interpreter builtin boundaries.
// Jint retains the complete builtin algorithms and their evaluation order.
using System;
using System.Collections.Generic;
using Jint;
using Jint.Native;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace ThorVG;

internal static class LottieExpressionBuiltins
{
    internal static void Install(Engine engine)
    {
        var strings = engine.GetValue("String").AsObject();
        var stringPrototype = strings.Get("prototype").AsObject();
        InstallStringProfile(engine, stringPrototype);
        foreach (var name in new[] { "at", "charAt", "charCodeAt", "codePointAt", "repeat", "padStart", "padEnd" })
            Adapt(engine, stringPrototype, name, 0);
        foreach (var name in new[] { "slice", "substring" }) Adapt(engine, stringPrototype, name, 0, 1);
        foreach (var name in new[] { "indexOf", "lastIndexOf", "includes", "startsWith", "endsWith" })
            Adapt(engine, stringPrototype, name, 1);
        InstallSplit(engine, stringPrototype);
        Adapt(engine, strings, "fromCharCode", all: true);
        Adapt(engine, strings, "fromCodePoint", all: true);

        var arrayPrototype = engine.GetValue("Array").AsObject().Get("prototype").AsObject();
        AdaptSequence(engine, arrayPrototype, false);
        var typedPrototype = engine.GetValue("Object").AsObject().Get("getPrototypeOf");
        var typed = engine.Call(typedPrototype, JsValue.Undefined,
            [engine.GetValue("Int8Array").AsObject().Get("prototype")]).AsObject();
        AdaptSequence(engine, typed, true);
        Adapt(engine, typed, "subarray", 0, 1);
        Adapt(engine, typed, "set", 1);
        var bufferPrototype = engine.GetValue("ArrayBuffer").AsObject().Get("prototype").AsObject();
        Adapt(engine, bufferPrototype, "slice", 0, 1);

        var numbers = engine.GetValue("Number").AsObject().Get("prototype").AsObject();
        foreach (var name in new[] { "toFixed", "toPrecision", "toExponential" }) Adapt(engine, numbers, name, 0);
        InstallStore(engine);
    }

    // Copyright JS Foundation and other contributors, http://js.foundation
    // Licensed under Apache License2.0: http://www.apache.org/licenses/LICENSE-2.0
    // ecma_builtin_string_prototype_object_conversion_helper/locale_compare,
    // with the pinned jerry-config.h JERRY_UNICODE_CASE_CONVERSION=0. The
    // non-ASCII code points remain unchanged; both locale variants use this
    // same algorithm and do not inspect locale arguments.
    private static void InstallStringProfile(Engine engine, Jint.Native.Object.ObjectInstance prototype)
    {
        var typeError = (Jint.Native.Error.ErrorConstructor)engine.GetValue("TypeError");
        string Receiver(JsValue value)
        {
            if (value.IsNull() || value.IsUndefined())
                throw new JavaScriptException(typeError, "Cannot convert null or undefined to object");
            return TypeConverter.ToString(value);
        }
        foreach (var lower in new[] { true, false })
        {
            foreach (var locale in new[] { false, true })
            {
                var name = lower ? (locale ? "toLocaleLowerCase" : "toLowerCase") :
                    (locale ? "toLocaleUpperCase" : "toUpperCase");
                prototype.Set(name, new ClrFunction(engine, name, (receiver, _) =>
                {
                    var text = Receiver(receiver);
                    return new JsString(string.Create(text.Length, (text, lower), static (result, state) =>
                    {
                        for (int i = 0; i < result.Length; ++i)
                        {
                            char c = state.text[i];
                            result[i] = state.lower ? (c >= 'A' && c <= 'Z' ? (char)(c + 32) : c) :
                                (c >= 'a' && c <= 'z' ? (char)(c - 32) : c);
                        }
                    }));
                }, 0, PropertyFlag.Configurable));
            }
        }
        prototype.Set("localeCompare", new ClrFunction(engine, "localeCompare", (receiver, arguments) =>
        {
            var text = Receiver(receiver);
            var other = TypeConverter.ToString(arguments.Length == 0 ? JsValue.Undefined : arguments[0]);
            // CESU8 relational order is UTF16 code-unit order, including
            // supplementary characters. Native returns exactly -1,0,1.
            return new JsNumber(Math.Sign(string.CompareOrdinal(text, other)));
        }, 1, PropertyFlag.Configurable));
    }

    private static void InstallStore(Engine engine)
    {
        var setters = LottieExpressionRuntime.EvaluateOwned(engine,
            "[function(o,k,v){o[k]=v;return v;},function(o,k,v){'use strict';o[k]=v;return v;}]").AsArray();
        var sloppy = setters.Get("0");
        var strict = setters.Get("1");
        LottieExpressionRuntime.SetHelper(engine, "$__tvg_store", new ClrFunction(engine, "", (_, arguments) =>
        {
            var target = arguments[0];
            // The pinned native VM and Jint both convert a computed property
            // key after RHS evaluation for an ordinary assignment (rk probe).
            var key = TypeConverter.ToPropertyKey(arguments[1]);
            var original = arguments[2];
            var stored = original;
            if (target is JsTypedArray && key.IsString())
            {
                string text = key.AsString();
                double index = TypeConverter.ToNumber(key);
                if (text == "-0" || TypeConverter.ToString(new JsNumber(index)) == text)
                    stored = NumericArgument(engine, original);
            }
            engine.Call(arguments[3].AsBoolean() ? strict : sloppy, JsValue.Undefined, [target, key, stored]);
            return original;
        }));
    }

    private static void AdaptSequence(Engine engine, Jint.Native.Object.ObjectInstance prototype, bool typed)
    {
        foreach (var name in new[] { "at", "flat" }) Adapt(engine, prototype, name, 0);
        foreach (var name in new[] { "slice", "splice" }) Adapt(engine, prototype, name, 0, 1);
        foreach (var name in new[] { "indexOf", "lastIndexOf", "includes" }) Adapt(engine, prototype, name, 1);
        Adapt(engine, prototype, "copyWithin", 0, 1, 2);
        // TypedArray.fill converts its element value as well as its indices.
        if (typed) Adapt(engine, prototype, "fill", 0, 1, 2);
        else Adapt(engine, prototype, "fill", 1, 2);
    }

    // Copyright JS Foundation and other contributors, http://js.foundation
    // Licensed under Apache License2.0: http://www.apache.org/licenses/LICENSE-2.0
    // Port of ecma_builtin_string_prototype_object_split and its dispatcher.
    // Managed UTF16 indices represent the same CESU8 code-unit boundaries.
    private static void InstallSplit(Engine engine, Jint.Native.Object.ObjectInstance prototype)
    {
        var typeError = (Jint.Native.Error.ErrorConstructor)engine.GetValue("TypeError");
        var objectConstructor = engine.GetValue("Object");
        prototype.Set("split", new ClrFunction(engine, "split", (receiver, arguments) =>
        {
            if (receiver.IsNull() || receiver.IsUndefined())
                throw new JavaScriptException(typeError, "Cannot convert null or undefined to object");
            // Native dispatch converts the receiver before consulting @@split.
            string text = TypeConverter.ToString(receiver);
            var separator = arguments.Length == 0 ? JsValue.Undefined : arguments[0];
            var limitValue = arguments.Length < 2 ? JsValue.Undefined : arguments[1];
            if (!separator.IsNull() && !separator.IsUndefined())
            {
                var method = engine.Call(objectConstructor, JsValue.Undefined, [separator]).AsObject().Get(GlobalSymbolRegistry.Split);
                if (!method.IsNull() && !method.IsUndefined())
                {
                    if (method is not Jint.Native.Function.Function)
                        throw new JavaScriptException(typeError, "Split method is not callable");
                    return engine.Call(method, separator, [new JsString(text), limitValue]);
                }
            }
            uint limit = limitValue.IsUndefined() ? uint.MaxValue - 1 :
                TypeConverter.ToUint32(new JsNumber(LottieExpressionMath.ToNumber(limitValue)));
            string delimiter = TypeConverter.ToString(separator);
            var values = new List<JsValue>();
            if (limit == 0) return new JsArray(engine, []);
            if (separator.IsUndefined()) return new JsArray(engine, [new JsString(text)]);
            if (text.Length == 0)
                return new JsArray(engine, delimiter.Length == 0 ? [] : [new JsString(text)]);
            int current = 0, last = 0;
            int compareEnd = Math.Min(text.Length - delimiter.Length + 1, text.Length);
            while (current < compareEnd)
            {
                if (text.AsSpan(current, delimiter.Length).SequenceEqual(delimiter) && last != current + delimiter.Length)
                {
                    values.Add(new JsString(text[last..current]));
                    if (values.Count >= limit) return new JsArray(engine, values.ToArray());
                    current += delimiter.Length;
                    last = current;
                    continue;
                }
                ++current;
            }
            values.Add(new JsString(text[last..]));
            return new JsArray(engine, values.ToArray());
        }, 2, PropertyFlag.Configurable));
    }

    private static void Adapt(Engine engine, Jint.Native.Object.ObjectInstance owner, string name, params int[] indices) =>
        Adapt(engine, owner, name, false, indices);

    private static void Adapt(Engine engine, Jint.Native.Object.ObjectInstance owner, string name, bool all, params int[] indices)
    {
        var original = owner.Get(name);
        // Some methods belong only to Array or TypedArray in this compiled
        // profile. Never introduce an absent method into the other prototype.
        if (original.IsUndefined()) return;
        int length = (int)original.AsObject().Get("length").AsNumber();
        owner.Set(name, new ClrFunction(engine, name, (receiver, arguments) =>
        {
            var adapted = new JsValue[arguments.Length];
            for (int index = 0; index < adapted.Length; ++index) adapted[index] = arguments[index];
            if (all)
            {
                for (int index = 0; index < adapted.Length; ++index) adapted[index] = NumericArgument(engine, adapted[index]);
            }
            else
            {
                foreach (int index in indices)
                    if (index < adapted.Length) adapted[index] = NumericArgument(engine, adapted[index]);
            }
            return engine.Call(original, receiver, adapted);
        }, length, PropertyFlag.Configurable));
    }

    internal static JsValue NumericArgument(Engine engine, JsValue original)
    {
        if (!original.IsString() && !original.IsObject()) return original;
        // Conversion must occur when the original builtin asks for ToNumber,
        // after its receiver/search/length/species checks. Eager conversion in
        // the wrapper changes observable getters and exception ordering.
        var argument = new JsObject(engine);
        argument.Set(GlobalSymbolRegistry.ToPrimitive, new ClrFunction(engine, "[Symbol.toPrimitive]", (_, _) =>
            new JsNumber(LottieExpressionMath.ToNumber(original)), 1));
        return argument;
    }
}
