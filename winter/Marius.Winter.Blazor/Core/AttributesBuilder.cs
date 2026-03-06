using System;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Marius.Winter.Blazor.Core
{
    // This wraps a RenderTreeBuilder in such a way that consumers
    // can only call the desired AddAttribute method, can't supply
    // sequence numbers, and can't leak the instance outside their
    // position in the call stack.

    public readonly ref struct AttributesBuilder
    {
        private readonly RenderTreeBuilder _underlyingBuilder;

        public AttributesBuilder(RenderTreeBuilder underlyingBuilder)
        {
            _underlyingBuilder = underlyingBuilder;
        }

        public void AddAttribute(string name, object value)
        {
            // Serialize simple value types as strings so they survive Blazor's
            // render tree without going through WeakObjectStore (which uses
            // WeakReference and can lose boxed structs to GC).
            // Only true reference types (arrays, ILayout, etc.) go through the store.
            if (value != null
                && value is not string
                && value is not int && value is not long
                && value is not float && value is not double
                && value is not System.Delegate)
            {
                if (value is Color4 c)
                    value = AttributeHelper.Color4ToString(c);
                else if (value is Thickness t)
                    value = AttributeHelper.ThicknessToString(t);
                else if (value is CornerRadius cr)
                    value = AttributeHelper.CornerRadiusToString(cr);
                else if (value is Enum e)
                    value = e.ToString();
                else if (value is byte[] bytes)
                    value = AttributeHelper.ByteArrayToString(bytes);
                else if (value is TrackSize[] tracks)
                    value = AttributeHelper.TrackSizeArrayToString(tracks);
                else if (value is string[] strings)
                    value = AttributeHelper.StringArrayToString(strings);
                else
                    value = WeakObjectStore.Add(value);
            }

            _underlyingBuilder.AddAttribute(0, name, value);
        }

        public void AddAttribute(string name, bool value)
        {
            // bool values are converted to ints (which later become strings) to ensure that
            // all values are always rendered, not only 'true' values. This ensures that the
            // element handlers will see all property changes and can handle them as needed.
            _underlyingBuilder.AddAttribute(0, name, value ? 1 : 0);
        }

        public void AddAttribute(string name, EventCallback value)
        {
            _underlyingBuilder.AddAttribute(0, name, value);
        }

        public void AddAttribute<T>(string name, EventCallback<T> value)
        {
            _underlyingBuilder.AddAttribute(0, name, value);
        }
    }
}