// Copyright JS Foundation and other contributors, http://js.foundation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0
// Distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND.
// Adapted from ecma_builtin_number_prototype_object_to_string at the pinned
// JerryScript revision; Jint retains decimal conversion and Number receivers.
using System;
using System.Collections.Generic;
using System.Text;
using Jint;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace ThorVG;

internal static class LottieExpressionNumberFormat
{
    internal static void Install(Engine engine)
    {
        var prototype = engine.GetValue("Number").AsObject().Get("prototype").AsObject();
        var valueOf = prototype.Get("valueOf");
        var decimalString = prototype.Get("toString");
        var rangeError = (Jint.Native.Error.ErrorConstructor)engine.GetValue("RangeError");
        prototype.Set("toString", new ClrFunction(engine, "toString", (receiver, args) =>
        {
            // The genuine intrinsic reads [[NumberData]] without consulting
            // an overridden valueOf, and rejects all incompatible receivers.
            var number = engine.Call(valueOf, receiver, []);
            uint radix = 10;
            if (args.Length != 0 && !args[0].IsUndefined())
            {
                float argument = LottieExpressionMath.ToNumber(args[0]);
                radix = TypeConverter.ToUint32(new JsNumber(MathF.Truncate(argument)));
                if (radix < 2 || radix > 36) throw new JavaScriptException(rangeError, "Radix is out of range");
            }
            float value = (float)number.AsNumber();
            if (!float.IsFinite(value) || value == 0 || radix == 10)
                return engine.Call(decimalString, number, []);
            return new JsString(Radix(value, radix));
        }, 1, PropertyFlag.Configurable));
        // The pinned profile has no Intl: this method is ordinary decimal
        // conversion and ignores all locale/options arguments.
        prototype.Set("toLocaleString", new ClrFunction(engine, "toLocaleString", (receiver, _) =>
            engine.Call(decimalString, engine.Call(valueOf, receiver, []), []), 0, PropertyFlag.Configurable));
    }

    private static string Radix(float value, uint radix)
    {
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        bool negative = value < 0;
        if (negative) value = -value;
        float integer = MathF.Floor(value);
        float fraction = value - integer;
        int leadingZeros = 0;
        var fractionDigits = new List<byte>(64);
        if (fraction > 0)
        {
            // GNU/Clang's native helper uses double nextafter and narrows its
            // return to float. MSVC uses the explicit next-float bit branch.
            float next = OperatingSystem.IsWindows() ? MathF.BitIncrement(value) : (float)Math.BitIncrement((double)value);
            float precision = MathF.Max((next - value) * .5f, 1.1754943508222875e-38f);
            byte digit;
            do
            {
                fraction *= radix;
                precision *= radix;
                digit = (byte)MathF.Floor(fraction);
                if (digit == 0 && fractionDigits.Count == 0)
                {
                    ++leadingZeros;
                    continue;
                }
                fractionDigits.Add(digit);
                fraction -= digit;
            } while (fraction >= precision);
            if (fraction > .5f || fraction == .5f && (digit & 1) != 0)
            {
                while (fractionDigits.Count != 0)
                {
                    int index = fractionDigits.Count - 1;
                    byte carry = (byte)(fractionDigits[index] + 1);
                    if (carry < radix)
                    {
                        fractionDigits[index] = carry;
                        break;
                    }
                    fractionDigits.RemoveAt(index);
                    if (index == 0) integer += 1;
                }
            }
        }

        int trailingZeros = 0;
        while ((BitConverter.SingleToUInt32Bits(integer / radix) >> 23) > 127 + 23)
        {
            ++trailingZeros;
            integer /= radix;
        }
        ulong integerValue = (ulong)integer;
        Span<char> integerDigits = stackalloc char[64];
        int at = integerDigits.Length;
        do
        {
            integerDigits[--at] = digits[(int)(integerValue % radix)];
            integerValue /= radix;
        } while (integerValue > 0);
        var result = new StringBuilder();
        if (negative) result.Append('-');
        result.Append(integerDigits[at..]);
        result.Append('0', trailingZeros);
        if (fractionDigits.Count != 0)
        {
            result.Append('.');
            result.Append('0', leadingZeros);
            foreach (byte digit in fractionDigits) result.Append(digits[digit]);
        }
        return result.ToString();
    }
}
