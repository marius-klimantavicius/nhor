// Ported from src/compute/grid/types/named.rs: compressed sparse row name storage.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Marius.Winter.Taffy;

public sealed class GridLineNames : IReadOnlyList<IReadOnlyList<string>>
{
    private readonly List<string> _names;
    private readonly List<int> _offsets;
    public GridLineNames() { _names = new(); _offsets = new(); }
    internal GridLineNames(int nameCapacity, int lineCapacity)
    {
        _names = new(nameCapacity); _offsets = new(lineCapacity + 1) { 0 };
    }
    internal void StartLine() => _offsets.Add(_names.Count);
    internal void PushName(string name) { _names.Add(name); _offsets[^1] = _names.Count; }
    internal bool CurrentLineContains(string name) => Line(Count - 1).Contains(name);
    public bool IsEmpty => _names.Count == 0;
    public int Count => Math.Max(_offsets.Count - 1, 0);
    public ReadOnlySpan<string> Line(int index) => (uint)index < (uint)Count
        ? CollectionsMarshal.AsSpan(_names).Slice(_offsets[index], _offsets[index + 1] - _offsets[index]) : [];
    public IReadOnlyList<string> this[int index] => (uint)index < (uint)Count
        ? new NameGroup(this, index) : Array.Empty<string>();
    private readonly struct NameGroup(GridLineNames owner, int index) : IReadOnlyList<string>
    {
        public int Count => owner._offsets[index + 1] - owner._offsets[index];
        public string this[int name] => (uint)name < (uint)Count ? owner._names[owner._offsets[index] + name] : throw new ArgumentOutOfRangeException(nameof(name));
        public IEnumerator<string> GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    public IEnumerator<IReadOnlyList<string>> GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
