using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Marius.Winter.Blazor;

/// <summary>
/// Stores complex objects that can't survive Blazor's render tree serialization
/// (which stringifies non-primitive values). Objects are held via WeakReference
/// so they don't prevent GC when the owning component is collected.
/// Dead entries are swept every 256 additions.
/// </summary>
internal static class WeakObjectStore
{
    private const string Prefix = "__wref:";
    private const int SweepInterval = 256;
    private static long _nextId;
    private static readonly ConcurrentDictionary<string, WeakReference> _store = new();

    public static string Add(object value)
    {
        var id = Interlocked.Increment(ref _nextId);
        var key = Prefix + id;
        _store[key] = new WeakReference(value);

        if (id % SweepInterval == 0)
            Sweep();

        return key;
    }

    public static object? Get(string? key)
    {
        if (key != null && key.StartsWith(Prefix) && _store.TryGetValue(key, out var wr))
            return wr.Target;
        return null;
    }

    public static T? Get<T>(object? attributeValue) where T : class
    {
        // Check for store key first — a string starting with Prefix is always a key,
        // not a direct value. This must come before the `is T` check because when
        // T is `object`, every string matches `is T` and would be returned as-is.
        if (attributeValue is string key && key.StartsWith(Prefix))
            return Get(key) as T;
        if (attributeValue is T direct) return direct;
        return null;
    }

    private static void Sweep()
    {
        foreach (var kvp in _store)
        {
            if (!kvp.Value.IsAlive)
                _store.TryRemove(kvp.Key, out _);
        }
    }
}
