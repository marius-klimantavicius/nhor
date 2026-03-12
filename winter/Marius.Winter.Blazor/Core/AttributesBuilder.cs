using System;
using System.Diagnostics;
using System.Globalization;
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

        public void AddAttribute(string name, string value)
        {
            _underlyingBuilder.AddAttribute(0, name, value);
        }

        public void AddAttribute(string name, int value)
        {
            _underlyingBuilder.AddAttribute(0, name, value.ToString(CultureInfo.InvariantCulture));
        }

        public void AddAttribute(string name, long value)
        {
            _underlyingBuilder.AddAttribute(0, name, value.ToString(CultureInfo.InvariantCulture));
        }

        public void AddAttribute(string name, float value)
        {
            _underlyingBuilder.AddAttribute(0, name, value.ToString(CultureInfo.InvariantCulture));
        }

        public void AddAttribute(string name, double value)
        {
            _underlyingBuilder.AddAttribute(0, name, value.ToString(CultureInfo.InvariantCulture));
        }

        public void AddAttribute(string name, bool value)
        {
            // bool values are converted to ints (which later become strings) to ensure that
            // all values are always rendered, not only 'true' values. This ensures that the
            // element handlers will see all property changes and can handle them as needed.
            _underlyingBuilder.AddAttribute(0, name, value ? "1" : "0");
        }

        public void AddAttribute(string name, Color4 value)
        {
            _underlyingBuilder.AddAttribute(0, name, AttributeHelper.Color4ToString(value));
        }

        public void AddAttribute(string name, Thickness value)
        {
            _underlyingBuilder.AddAttribute(0, name, AttributeHelper.ThicknessToString(value));
        }

        public void AddAttribute(string name, CornerRadius value)
        {
            _underlyingBuilder.AddAttribute(0, name, AttributeHelper.CornerRadiusToString(value));
        }

        public void AddAttribute<T>(string name, T value) where T : struct, Enum
        {
            _underlyingBuilder.AddAttribute(0, name, value.ToString());
        }

        public void AddAttribute(string name, byte[] value)
        {
            _underlyingBuilder.AddAttribute(0, name, AttributeHelper.ByteArrayToString(value));
        }

        public void AddAttribute(string name, TrackSize[] value)
        {
            _underlyingBuilder.AddAttribute(0, name, AttributeHelper.TrackSizeArrayToString(value));
        }

        public void AddAttribute(string name, string[] value)
        {
            _underlyingBuilder.AddAttribute(0, name, AttributeHelper.StringArrayToString(value));
        }

        public void AddAttribute(string name, EventCallback value)
        {
            _underlyingBuilder.AddAttribute(0, name, value);
        }

        public void AddAttribute<T>(string name, EventCallback<T> value)
        {
            _underlyingBuilder.AddAttribute(0, name, value);
        }

        /// <summary>
        /// Fallback for reference types that need to go through WeakObjectStore.
        /// </summary>
        public void AddAttribute(string name, object value)
        {
            Debug.Assert(!value.GetType().IsValueType);
            _underlyingBuilder.AddAttribute(0, name, WeakObjectStore.Add(value));
        }
    }
}