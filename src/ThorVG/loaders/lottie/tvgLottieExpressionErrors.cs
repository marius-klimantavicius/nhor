using System.Collections.Generic;
using Jint;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Interop;

namespace ThorVG;

internal static class LottieExpressionErrors
{
    internal static void Install(Engine engine)
    {
        var common = engine.GetValue("Error").AsObject().Get("prototype").AsObject();
        var nativeErrors = new HashSet<ObjectInstance>(ReferenceEqualityComparer.Instance);
        foreach (string name in new[] { "EvalError", "RangeError", "ReferenceError", "SyntaxError", "TypeError", "URIError", "AggregateError" })
            nativeErrors.Add(engine.GetValue(name).AsObject().Get("prototype").AsObject());
        // ecma_new_standard_error uses the common Error prototype because
        // JERRY_BUILTIN_ERRORS is disabled. JERRY_ERROR_MESSAGES is also zero.
        // Adapt at catch entry before user code (including destructuring) can
        // observe a standard interpreter error. Explicit user Error objects
        // and arbitrary thrown values retain their own identity and message.
        LottieExpressionRuntime.SetHelper(engine, "$__tvg_normalize_error", new ClrFunction(engine, "", (_, arguments) =>
        {
            var value = arguments[0];
            if (value.IsObject() && value.AsObject().Prototype is { } prototype && nativeErrors.Contains(prototype))
            {
                var error = value.AsObject();
                error.RemoveOwnProperty("message");
                error.RemoveOwnProperty("stack");
                error.Prototype = common;
            }
            return value;
        }));
    }
}
