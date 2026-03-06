// Ported from taffy/src/tree/node.rs
// A type representing the id of a single node in a tree of nodes

using System;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// A type representing the id of a single node in a tree of nodes.
    /// Internally it is a wrapper around a ulong.
    /// </summary>
    public readonly struct NodeId : IEquatable<NodeId>
    {
        private readonly ulong _value;

        /// <summary>Create a new NodeId from a ulong value</summary>
        public NodeId(ulong value)
        {
            _value = value;
        }

        /// <summary>Get the underlying ulong value</summary>
        public ulong Value => _value;

        // --- Implicit conversions ---

        /// <summary>Implicit conversion from ulong to NodeId</summary>
        public static implicit operator NodeId(ulong value) => new NodeId(value);

        /// <summary>Implicit conversion from NodeId to ulong</summary>
        public static implicit operator ulong(NodeId id) => id._value;

        /// <summary>Implicit conversion from int to NodeId</summary>
        public static implicit operator NodeId(int value) => new NodeId((ulong)value);

        /// <summary>Explicit conversion from NodeId to int</summary>
        public int ToInt() => (int)_value;

        // --- Equality ---

        public bool Equals(NodeId other) => _value == other._value;
        public override bool Equals(object? obj) => obj is NodeId other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();

        public static bool operator ==(NodeId left, NodeId right) => left._value == right._value;
        public static bool operator !=(NodeId left, NodeId right) => left._value != right._value;

        public override string ToString() => $"NodeId({_value})";
    }
}
