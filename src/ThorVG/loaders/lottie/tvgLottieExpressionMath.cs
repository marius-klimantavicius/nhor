// Mirrors the pinned JerryScript math built-ins using its float32 numbers.
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Jint;
using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace ThorVG;

internal static class LottieExpressionMath
{
    [DllImport("libm.so.6", EntryPoint = "expm1f", CallingConvention = CallingConvention.Cdecl)]
    private static extern float UnixExpM1(float x);
    [DllImport("libm.so.6", EntryPoint = "log1pf", CallingConvention = CallingConvention.Cdecl)]
    private static extern float UnixLogP1(float x);
    [DllImport("libSystem.B.dylib", EntryPoint = "expm1f", CallingConvention = CallingConvention.Cdecl)]
    private static extern float AppleExpM1(float x);
    [DllImport("libSystem.B.dylib", EntryPoint = "log1pf", CallingConvention = CallingConvention.Cdecl)]
    private static extern float AppleLogP1(float x);
    [DllImport("ucrtbase.dll", EntryPoint = "expm1f", CallingConvention = CallingConvention.Cdecl)]
    private static extern float WindowsExpM1(float x);
    [DllImport("ucrtbase.dll", EntryPoint = "log1pf", CallingConvention = CallingConvention.Cdecl)]
    private static extern float WindowsLogP1(float x);

    private static bool Apple => OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS();
    private static float ExpM1(float x) => OperatingSystem.IsWindows() ? WindowsExpM1(x) : Apple ? AppleExpM1(x) : UnixExpM1(x);
    private static float LogP1(float x) => OperatingSystem.IsWindows() ? WindowsLogP1(x) : Apple ? AppleLogP1(x) : UnixLogP1(x);

    private static readonly Dictionary<string, Func<float, float>> Unary = new()
    {
        ["abs"] = MathF.Abs, ["acos"] = MathF.Acos, ["acosh"] = MathF.Acosh,
        ["asin"] = MathF.Asin, ["asinh"] = MathF.Asinh, ["atan"] = MathF.Atan, ["atanh"] = MathF.Atanh,
        ["cbrt"] = MathF.Cbrt, ["ceil"] = MathF.Ceiling, ["cos"] = MathF.Cos, ["cosh"] = MathF.Cosh,
        ["exp"] = MathF.Exp, ["expm1"] = ExpM1, ["floor"] = MathF.Floor, ["fround"] = x => x,
        ["log"] = MathF.Log, ["log1p"] = LogP1, ["log2"] = MathF.Log2, ["log10"] = MathF.Log10,
        ["round"] = Round, ["sign"] = x => float.IsNaN(x) || x == 0 ? x : x < 0 ? -1 : 1,
        ["sin"] = MathF.Sin, ["sinh"] = MathF.Sinh, ["sqrt"] = MathF.Sqrt,
        ["tan"] = MathF.Tan, ["tanh"] = MathF.Tanh, ["trunc"] = MathF.Truncate
    };

    internal static float ToNumber(JsValue value)
    {
        var primitive = Jint.Runtime.TypeConverter.ToPrimitive(value, Jint.Runtime.Types.Number);
        return primitive.IsString() ? LottieExpressionNumbers.Parse(primitive.AsString()) : (float)Jint.Runtime.TypeConverter.ToNumber(primitive);
    }

    public static void Install(Engine engine)
    {
        var math = engine.GetValue("Math").AsObject();
        foreach (var entry in Unary)
        {
            var function = entry.Value;
            math.Set(entry.Key, new ClrFunction(engine, entry.Key, (_, args) =>
                new JsNumber(function(args.Length == 0 ? float.NaN : ToNumber(args[0]))), 1, PropertyFlag.Configurable));
        }
        float Argument(JsValue[] args, int index) => index < args.Length ? ToNumber(args[index]) : float.NaN;
        math.Set("atan2", new ClrFunction(engine, "atan2", (_, args) => new JsNumber(MathF.Atan2(Argument(args, 0), Argument(args, 1))), 2, PropertyFlag.Configurable));
        math.Set("pow", new ClrFunction(engine, "pow", (_, args) => new JsNumber(Pow(Argument(args, 0), Argument(args, 1))), 2, PropertyFlag.Configurable));
        math.Set("clz32", new ClrFunction(engine, "clz32", (_, args) => new JsNumber(BitOperations.LeadingZeroCount(
            Jint.Runtime.TypeConverter.ToUint32(new JsNumber(Argument(args, 0))))), 1, PropertyFlag.Configurable));
        // The pinned native implementation converts the uint32 product directly
        // to its float32 number type, without an intermediate signed cast.
        math.Set("imul", new ClrFunction(engine, "imul", (_, args) => new JsNumber((float)unchecked(
            Jint.Runtime.TypeConverter.ToUint32(new JsNumber(Argument(args, 0))) * Jint.Runtime.TypeConverter.ToUint32(new JsNumber(Argument(args, 1))))), 2, PropertyFlag.Configurable));
        foreach (var maximum in new[] { false, true })
        {
            var name = maximum ? "max" : "min";
            math.Set(name, new ClrFunction(engine, name, (_, args) =>
            {
                float result = maximum ? float.NegativeInfinity : float.PositiveInfinity;
                foreach (var argument in args)
                {
                    var value = ToNumber(argument);
                    if (float.IsNaN(value)) result = value;
                    if (value == 0 && result == 0)
                    {
                        var negative = float.IsNegative(value);
                        if (maximum ? !negative : negative) result = value;
                    }
                    else if (maximum ? value > result : value < result) result = value;
                }
                return new JsNumber(result);
            }, 2, PropertyFlag.Configurable));
        }
        math.Set("hypot", new ClrFunction(engine, "hypot", (_, args) =>
        {
            float result = 0;
            var infinite = false;
            foreach (var argument in args)
            {
                var value = ToNumber(argument);
                if (float.IsNaN(value) && !infinite) { result = value; continue; }
                if (float.IsInfinity(value)) { infinite = true; result = float.PositiveInfinity; continue; }
                result = TvgMath.MultiplyAdd(value, value, result);
            }
            return new JsNumber(MathF.Sqrt(result));
        }, 2, PropertyFlag.Configurable));
    }

    internal static float Pow(float x, float y)
    {
        if (float.IsNaN(y) || float.IsInfinity(y) && (x == 1 || x == -1)) return float.NaN;
        if (y == 0) return 1;
        return MathF.Pow(x, y);
    }

    private static float Round(float x)
    {
        if (!float.IsFinite(x) || x == 0) return x;
        var fraction = x % 1;
        if (fraction == 0) return x;
        if (x < 0)
        {
            if (x >= -.5f) return -0.0f;
            if (fraction < -.5f) x -= .5f;
        }
        else if (fraction >= .5f) x += .5f;
        return MathF.Truncate(x);
    }
}
