using System;
using System.Globalization;
using Jint;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime.Interop;
using Jint.Runtime.Descriptors;

namespace ThorVG;

// Preserve Jint's real buffer/typed-array constructors, prototypes, species,
// iterators and allocation algorithms, adapting their numeric input boundary.
internal static class LottieExpressionConstructors
{
    internal static void Install(Engine engine)
    {
        foreach (var name in new[] { "ArrayBuffer", "Int8Array", "Uint8Array", "Uint8ClampedArray", "Int16Array", "Uint16Array", "Int32Array", "Uint32Array", "Float32Array" })
        {
            var original = (Constructor)engine.GetValue(name);
            var constructor = new NumericConstructor(engine, name, original, name == "ArrayBuffer");
            original.Get("prototype").AsObject().Set("constructor", constructor);
            engine.SetValue(name, constructor);
        }
        var constructorBase = engine.GetValue("Int8Array").AsObject().Prototype!;
        var prototype = engine.GetValue("Int8Array").AsObject().Get("prototype").AsObject().Prototype!;
        AdaptValues(engine, prototype, "set", 0, -1, false);
        AdaptValues(engine, prototype, "map", -1, 0, false);
        AdaptValues(engine, prototype, "sort", -1, 0, false);
        AdaptValues(engine, constructorBase, "from", 0, 1, false);
        AdaptValues(engine, constructorBase, "of", -1, -1, true);
    }

    private static void AdaptValues(Engine engine, ObjectInstance owner, string name, int sourceIndex, int callbackIndex, bool all)
    {
        var original = owner.Get(name);
        int length = (int)original.AsObject().Get("length").AsNumber();
        owner.Set(name, new ClrFunction(engine, name, (receiver, arguments) =>
        {
            var adapted = new JsValue[arguments.Length];
            for (int i = 0; i < adapted.Length; ++i) adapted[i] = arguments[i];
            bool mapped = callbackIndex >= 0 && callbackIndex < adapted.Length &&
                          adapted[callbackIndex] is Jint.Native.Function.Function;
            if (sourceIndex >= 0 && sourceIndex < adapted.Length && adapted[sourceIndex].IsObject() && adapted[sourceIndex] is not JsTypedArray)
                adapted[sourceIndex] = new NumericInput(engine, adapted[sourceIndex].AsObject(), !mapped);
            if (mapped)
            {
                var callback = adapted[callbackIndex];
                adapted[callbackIndex] = new ClrFunction(engine, "", (thisValue, values) =>
                    LottieExpressionBuiltins.NumericArgument(engine, engine.Call(callback, thisValue, values)));
            }
            if (all)
                for (int i = 0; i < adapted.Length; ++i) adapted[i] = LottieExpressionBuiltins.NumericArgument(engine, adapted[i]);
            return engine.Call(original, receiver, adapted);
        }, length, PropertyFlag.Configurable));
    }

    private sealed class NumericConstructor : Constructor
    {
        private readonly Constructor original;
        private readonly bool buffer;

        internal NumericConstructor(Engine engine, string name, Constructor original, bool buffer) : base(engine, name)
        {
            this.original = original;
            this.buffer = buffer;
            Prototype = original.Prototype;
            foreach (var property in original.GetOwnProperties()) DefineOwnProperty(property.Key, property.Value);
        }

        protected override JsValue Call(JsValue receiver, JsValue[] arguments) =>
            Engine.Call(original, receiver, arguments);

        public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            var adapted = new JsValue[arguments.Length];
            for (int i = 0; i < adapted.Length; ++i) adapted[i] = arguments[i];
            if (buffer)
            {
                if (adapted.Length != 0) adapted[0] = LottieExpressionBuiltins.NumericArgument(Engine, adapted[0]);
            }
            else if (adapted.Length != 0)
            {
                if (adapted[0] is JsArrayBuffer)
                {
                    for (int i = 1; i < Math.Min(3, adapted.Length); ++i)
                        adapted[i] = LottieExpressionBuiltins.NumericArgument(Engine, adapted[i]);
                }
                else if (adapted[0].IsObject() && adapted[0] is not JsTypedArray)
                    adapted[0] = new NumericInput(Engine, adapted[0].AsObject());
                else if (adapted[0].IsString())
                    adapted[0] = new JsNumber(LottieExpressionMath.ToNumber(adapted[0]));
            }
            return original.Construct(adapted, newTarget);
        }
    }

    internal sealed class NumericInput(Engine engine, ObjectInstance source, bool elements = true) : ObjectInstance(engine)
    {
        public override JsValue Get(JsValue property, JsValue receiver)
        {
            var value = source.Get(property, source);
            if (property.Equals(GlobalSymbolRegistry.Iterator) && value is Jint.Native.Function.Function)
                return new ClrFunction(Engine, "[Symbol.iterator]", (_, arguments) =>
                {
                    var iterator = Engine.Call(value, source, arguments);
                    return iterator.IsObject() && elements ? new NumericIterator(Engine, iterator.AsObject()) : iterator;
                });
            if (property.IsString())
            {
                string key = property.AsString();
                if (key == "length" || elements && uint.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                    return LottieExpressionBuiltins.NumericArgument(Engine, value);
            }
            return value;
        }
    }

    private sealed class NumericIterator(Engine engine, ObjectInstance source) : ObjectInstance(engine)
    {
        public override JsValue Get(JsValue property, JsValue receiver)
        {
            var method = source.Get(property, source);
            if (property.IsString() && property.AsString() is "next" or "return" && method is Jint.Native.Function.Function)
                return new ClrFunction(Engine, property.AsString(), (_, arguments) =>
                {
                    var result = Engine.Call(method, source, arguments);
                    return result.IsObject() ? new NumericIteratorResult(Engine, result.AsObject()) : result;
                });
            return method;
        }
    }

    private sealed class NumericIteratorResult(Engine engine, ObjectInstance source) : ObjectInstance(engine)
    {
        public override JsValue Get(JsValue property, JsValue receiver)
        {
            var value = source.Get(property, source);
            return property.IsString() && property.AsString() == "value"
                ? LottieExpressionBuiltins.NumericArgument(Engine, value) : value;
        }
    }
}
