// JerryScript external function call/construct and lazy prototype semantics.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Jint;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace ThorVG;

internal sealed class LottieExpressionFunction : Constructor
{
    private static readonly JsString PrototypeName = new("prototype");
    private static readonly PropertyDescriptor PendingPrototype = new(JsValue.Undefined, PropertyFlag.Writable);
    private static readonly ConditionalWeakTable<Engine, Dispatcher> Dispatchers = new();
    private readonly Func<JsValue, JsValue[], JsValue> handler;
    private readonly Dispatcher dispatcher;

    // Keep Jint's actual CLR-call stack/constraint guard. One dispatcher per
    // engine also avoids allocating a second function for every native callback.
    private sealed class Dispatcher
    {
        internal readonly List<LottieExpressionFunction> Frames = new();
        internal readonly ClrFunction Function;

        internal Dispatcher(Engine engine)
        {
            Function = new ClrFunction(engine, "", (receiver, arguments) => Frames[^1].handler(receiver, arguments));
        }
    }

    internal LottieExpressionFunction(Engine engine, string name, Func<JsValue, JsValue[], JsValue> handler)
        : base(engine, "")
    {
        this.handler = handler;
        dispatcher = Dispatchers.GetValue(engine, static engine => new Dispatcher(engine));
        Prototype = engine.Intrinsics.Function.Get(PrototypeName).AsObject();
        // Native external functions inherit name/length from Function.prototype.
        RemoveOwnProperty("name");
        _length = null;
        _prototypeDescriptor = PendingPrototype;
    }

    protected override JsValue Call(JsValue receiver, JsValue[] arguments)
    {
        dispatcher.Frames.Add(this);
        try { return Engine.Call(dispatcher.Function, receiver, arguments); }
        finally { dispatcher.Frames.RemoveAt(dispatcher.Frames.Count - 1); }
    }

    public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
    {
        var prototype = newTarget.AsObject().Get(PrototypeName) as ObjectInstance ?? Engine.Intrinsics.Object.PrototypeObject;
        var receiver = new JsObject(Engine) { Prototype = prototype };
        var result = Call(receiver, arguments);
        return result as ObjectInstance ?? receiver;
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (property.Equals(PrototypeName) && ReferenceEquals(_prototypeDescriptor, PendingPrototype))
        {
            var prototype = new JsObject(Engine) { Prototype = Engine.Intrinsics.Object.PrototypeObject };
            prototype.DefineOwnProperty("constructor", new PropertyDescriptor(this, PropertyFlag.Writable | PropertyFlag.Configurable));
            _prototypeDescriptor = new PropertyDescriptor(prototype, PropertyFlag.Writable);
        }
        return base.GetOwnProperty(property);
    }

    public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
    {
        GetOwnProperty(PrototypeName);
        return base.GetOwnProperties();
    }
}
